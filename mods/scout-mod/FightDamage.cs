using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppGameRiver;
using Il2CppGameRiver.Fight;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace MelperScout;

sealed class UnitDamageInfo
{
    // 0 for buildings, which are summed into one row.
    public int MechTypeId;
    public string Name = "";
    public string[] IconCandidates = Array.Empty<string>();
    public long Damage;
    public long Kills;
    public long Taken;
}

// Read-only. Reads the game's own per-unit statistics for both teams once a fight is over,
// the numbers the fight just showed on the field. Nothing is read during deployment.
static class FightDamageReader
{
    const int MaxRecorderDepth = 8;

    internal static BattleStatisticManager? CurrentManager()
    {
        var fight = FightController.Current;
        if (!ArmySnapshotReader.Usable(fight))
            return null;
        var manager = fight.GetBattleStatisticManager();
        return ArmySnapshotReader.Usable(manager) ? manager : null;
    }

    // Fills Damage for every team of the snapshot; false when the game had no statistics to read.
    internal static bool Fill(MatchSnapshot snapshot, RoundStatisticData? data)
    {
        if (!ArmySnapshotReader.Usable(data))
            return false;
        bool any = false;
        foreach (var team in snapshot.Teams)
        {
            try
            {
                var records = new Il2CppCollections.List<UnitDamageStatisticData>();
                data!.GetUnitDatas(team.TeamIndex, ref records);
                team.Damage = Group(team, records);
                any = true;
            }
            catch (Exception e)
            {
                MelperScoutMod.Log.Warning($"team {team.TeamIndex}: damage read failed: {e.Message}");
            }
        }
        return any;
    }

    static List<UnitDamageInfo> Group(TeamSnapshot team, Il2CppCollections.List<UnitDamageStatisticData> records)
    {
        var byType = new Dictionary<int, UnitDamageInfo>();
        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (!ArmySnapshotReader.Usable(record))
                continue;
            int mechTypeId = MechTypeOf(record.DamageRecorder);
            if (mechTypeId < 0)
                mechTypeId = 0;
            if (!byType.TryGetValue(mechTypeId, out var info))
            {
                var unit = team.Units.FirstOrDefault(u => u.MechTypeId == mechTypeId);
                info = new UnitDamageInfo
                {
                    MechTypeId = mechTypeId,
                    Name = mechTypeId == 0 ? "Buildings" : unit?.Name ?? $"#{mechTypeId}",
                    IconCandidates = mechTypeId == 0 ? Array.Empty<string>() : unit?.IconCandidates ?? new[] { $"Mech_Default_{mechTypeId}_1" },
                };
                byType.Add(mechTypeId, info);
            }
            info.Damage += Math.Max(0, record.DamageReal);
            info.Kills += Math.Max(0, record.KillCount);
            info.Taken += Math.Max(0, record.DamageTaken);
        }
        return byType.Values
            .Where(d => d.Damage > 0 || d.Taken > 0)
            .OrderByDescending(d => d.Damage)
            .ThenBy(d => d.MechTypeId)
            .ToList();
    }

    // Summoned mechs record under their own recorder; walk up to the squad that owns them.
    // Anything that never reaches a squad (buildings) comes back as -1.
    static int MechTypeOf(IDamageRecorder? recorder)
    {
        for (int depth = 0; depth < MaxRecorderDepth && ArmySnapshotReader.Usable(recorder); depth++)
        {
            var squad = recorder!.TryCast<MechTeam>();
            if (ArmySnapshotReader.Usable(squad))
                return squad!.GetMechID();
            var parent = recorder.GetDamageRecorder();
            if (!ArmySnapshotReader.Usable(parent) || parent!.Pointer == recorder.Pointer)
                break;
            recorder = parent;
        }
        return -1;
    }
}
