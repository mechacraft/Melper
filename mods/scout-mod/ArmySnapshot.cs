using System;
using System.Collections.Generic;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using System.Linq;
using Il2CppGameRiver;
using Il2CppGameRiver.Fight;
using Il2CppInterop.Runtime.InteropTypes;

namespace MelperScout;

sealed class SpecialistInfo
{
    public int Id;
    public string Name = "";
    public string PicName = "";
    public string IconName = "";
    // BattleSuite draws specialists with the reinforcement picture, so try that first.
    public string[] IconCandidates => new[] { PicName, IconName };
}

sealed class UnitTypeInfo
{
    public int MechTypeId;
    public string Name = "";
    public int Cards;
    public int Mechs;
    public int MaxLevel;
    public string SmallPortrait = "";
    public string[] IconCandidates => new[] { $"Mech_Default_{MechTypeId}_1", SmallPortrait };
}

sealed class TeamSnapshot
{
    public int TeamIndex;
    public bool IsLocal;
    public long Value;
    public bool ValueComplete = true;
    public long MaxHealth;
    public List<SpecialistInfo> Specialists = new();
    public List<UnitTypeInfo> Units = new();
    // What each unit type did in the fight, read when it ended; empty until then.
    public List<UnitDamageInfo> Damage = new();
}

sealed class MatchSnapshot
{
    public IntPtr Match;
    public int Round;
    // False when no fight has been seen in this match yet, so only the specialists are known.
    public bool HasArmy;
    // True once the fight that started with this snapshot has ended and its damage was read.
    public bool HasDamage;
    public List<TeamSnapshot> Teams = new();
}

// Read-only. Armies are read only when a fight starts, when they are on the field for everyone to see,
// so nothing the opponent buys or moves during a deployment phase is ever read. Specialists are
// public in the game's own UI, so they are also read during deployment.
static class ArmySnapshotReader
{
    internal static Match? CurrentMatch()
    {
        var fight = FightController.Current;
        if (!Usable(fight))
            return null;
        var match = fight.GetMatch()?.TryCast<Match>();
        return Usable(match) ? match : null;
    }

    internal static MatchSnapshot? Read(bool includeArmy)
    {
        var match = CurrentMatch();
        if (match == null)
            return null;
        var manager = match.GetPlayerManager();
        var controllers = manager?.GetPlayerControllers();
        if (!Usable(manager) || !Usable(controllers))
            return null;

        int? localTeam = ReadLocalTeam();
        var snapshot = new MatchSnapshot { Match = match.Pointer, Round = match.RoundCount, HasArmy = includeArmy };
        for (int i = 0; i < manager!.GetPlayerCount(); i++)
        {
            var controller = controllers![i];
            if (!Usable(controller))
                continue;
            int team = controller.GetTeamIndex();
            if (team < 0 || team > 1)
                continue;
            var teamSnapshot = new TeamSnapshot { TeamIndex = team, IsLocal = team == localTeam };
            ReadSpecialists(controller, teamSnapshot);
            if (includeArmy)
                ReadUnits(controller, teamSnapshot);
            snapshot.Teams.Add(teamSnapshot);
        }
        snapshot.Teams.Sort((a, b) => a.TeamIndex.CompareTo(b.TeamIndex));
        return snapshot;
    }

    static int? ReadLocalTeam()
    {
        try
        {
            var client = Il2CppGameRiver.Client.MatchClient.Current;
            var local = Usable(client) ? client.GetLocalPlayerController() : null;
            return Usable(local) ? local!.GetTeamIndex() : null;
        }
        catch
        {
            return null;
        }
    }

    static void ReadSpecialists(PlayerController controller, TeamSnapshot team)
    {
        try
        {
            var officers = controller.GetOfficerManager()?.GetOfficers()?.TryCast<Il2CppCollections.List<Officer>>();
            if (!Usable(officers))
                return;
            for (int i = 0; i < officers!.Count; i++)
            {
                var officer = officers[i];
                var data = Usable(officer) ? officer.data : null;
                if (!Usable(data))
                    continue;
                int id = data!.GetID();
                if (id <= 0 || team.Specialists.Any(s => s.Id == id))
                    continue;
                team.Specialists.Add(new SpecialistInfo
                {
                    Id = id,
                    Name = Safe(data.GetName) ?? $"#{id}",
                    PicName = Safe(officer!.GetReinforcePicName) ?? "",
                    IconName = Safe(data.GetIconName) ?? "",
                });
            }
        }
        catch (Exception e)
        {
            MelperScoutMod.Log.Warning($"team {team.TeamIndex}: specialists read failed: {e.Message}");
        }
    }

    static void ReadUnits(PlayerController controller, TeamSnapshot team)
    {
        var units = controller.GetUnitManager()?.units;
        if (!Usable(units))
        {
            team.ValueComplete = false;
            return;
        }

        var byType = new Dictionary<int, UnitTypeInfo>();
        var cardIds = new HashSet<int>();
        for (int i = 0; i < units!.Count; i++)
        {
            var unit = units[i];
            if (!Usable(unit))
                continue;
            try
            {
                var data = unit.GetUnitData();
                int mechTypeId = Usable(data) ? data.GetMechID() : unit.GetID();
                if (mechTypeId <= 0)
                    continue;
                if (!byType.TryGetValue(mechTypeId, out var info))
                {
                    info = new UnitTypeInfo { MechTypeId = mechTypeId, Name = (Usable(data) ? Safe(data.GetSubtitle) : null) ?? $"#{mechTypeId}",
                        SmallPortrait = Safe(unit.GetSmallPortraitImageName) ?? "",
                    };
                    byType.Add(mechTypeId, info);
                }
                info.Cards++;
                info.Mechs += Math.Max(0, unit.GetMechCount());
                info.MaxLevel = Math.Max(info.MaxLevel, (int)unit.GetLevel() + 1); // CardLevel.Level1 is 0
                cardIds.Add(unit.GetID());

                team.MaxHealth += Math.Max(0, unit.GetBaseLife());
                int supply = unit.GetSupply();
                long? equipment = EquipmentValue(unit);
                if (supply < 0 || equipment == null)
                    team.ValueComplete = false;
                else
                    team.Value += supply + equipment.Value;
            }
            catch (Exception e)
            {
                team.ValueComplete = false;
                MelperScoutMod.Log.Warning($"team {team.TeamIndex}: unit {i} read failed: {e.Message}");
            }
        }

        foreach (int cardId in cardIds)
        {
            long? tech = TechnologyValue(controller, cardId);
            if (tech == null)
                team.ValueComplete = false;
            else
                team.Value += tech.Value;
        }

        team.Units = byType.Values.OrderByDescending(u => u.Cards).ThenBy(u => u.MechTypeId).ToList();
    }

    static long? EquipmentValue(CardElement unit)
    {
        if (!unit.HasEquipment())
            return 0;
        var config = GRSingletonMonoStatic<Config>.Instance;
        var equipments = unit.GetEquipments()?.TryCast<Il2CppCollections.List<Equipment>>();
        if (!Usable(config) || !Usable(equipments))
            return null;
        long sum = 0;
        for (int i = 0; i < equipments!.Count; i++)
        {
            var data = equipments[i]?.data;
            if (!Usable(data))
                return null;
            sum += config!.GetReinforceItemPrice(data!);
        }
        return sum;
    }

    // Technologies are bought per unit type, so they count once per type rather than once per card.
    static long? TechnologyValue(PlayerController controller, int cardId)
    {
        var manager = controller.GetTechnologyManager();
        if (!Usable(manager) || !manager.TryGetTechnologyManager(cardId, out var unitManager))
            return 0;
        var technologies = unitManager?.technologies;
        if (!Usable(technologies))
            return null;
        long sum = 0;
        for (int i = 0; i < technologies!.Count; i++)
        {
            var tech = technologies[i];
            if (!Usable(tech))
                return null;
            if (tech.isActive)
                sum += tech.GetSupply();
        }
        return sum;
    }

    static string? Safe(Func<string> getter)
    {
        try
        {
            string value = getter();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    internal static bool Usable(Il2CppObjectBase? obj) => obj != null && obj.Pointer != IntPtr.Zero && !obj.WasCollected;
}
