using System;
using System.Linq;
using HarmonyLib;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MelperScout.MelperScoutMod), "MelperScout", "0.1.0", "Melper")]
[assembly: MelonGame("GameRiver", "Mechabellum")]

namespace MelperScout;

// Shows both armies as they were when the last fight started, plus both teams' specialists.
// Read-only: armies are snapshotted only at fight start, when everything is on the field anyway,
// and shown during the next deployment. The opponent's purchases and moves are never read while they deploy.
// Specialists are public in the game's own UI, so they are read live and drawn next to each player's avatar.
public sealed class MelperScoutMod : MelonMod
{
    static MelonPreferences_Category _prefs = null!;
    static MelonPreferences_Entry<bool> _showOverlay = null!;
    static MelonPreferences_Entry<bool> _showOwnTeam = null!;
    static MelonPreferences_Entry<float> _scale = null!;
    static MelonPreferences_Entry<float> _offsetRight = null!;
    static MelonPreferences_Entry<float> _offsetTop = null!;
    static MelonPreferences_Entry<bool> _showSpecialists = null!;

    internal static bool ShowOwnTeam => _showOwnTeam.Value;
    internal static float Scale => Math.Clamp(_scale.Value, 0.5f, 3f);
    internal static float OffsetRight => _offsetRight.Value;
    internal static float OffsetTop => _offsetTop.Value;

    internal static MelonLogger.Instance Log = null!;

    const float SpecialistRefreshSeconds = 1f;

    static MatchSnapshot? _snapshot;
    // Bumped on every change, so the panel is rebuilt only when there is something new to show.
    static int _snapshotVersion;

    internal static MatchSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            _snapshotVersion++;
        }
    }

    internal static bool InDeployment;
    static float _nextSpecialistRefresh;
    static bool _drawFaulted;
    static bool _badgesFaulted;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        _prefs = MelonPreferences.CreateCategory("MelperScout");
        _showOverlay = _prefs.CreateEntry("ShowOverlay", true, description: "Show the panel during deployment. F6 in game toggles it.");
        _showOwnTeam = _prefs.CreateEntry("ShowOwnTeam", true, description: "Show your own army next to the opponent's.");
        _scale = _prefs.CreateEntry("Scale", 1f, description: "Panel size (0.5..3).");
        _offsetRight = _prefs.CreateEntry("OffsetRight", 24f, description: "Distance from the right edge of the screen, in pixels.");
        _offsetTop = _prefs.CreateEntry("OffsetTop", 150f, description: "Distance from the top of the screen, in pixels.");
        _showSpecialists = _prefs.CreateEntry("ShowSpecialists", true, description: "Small specialist portraits under each player's avatar.");
        Log.Msg($"loaded: ShowOverlay={_showOverlay.Value} ShowOwnTeam={ShowOwnTeam} Scale={Scale}");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        InDeployment = false;
        Snapshot = null;
        ScoutCanvas.ClearSprites();
        SpecialistBadges.Clear();
    }

    public override void OnUpdate()
    {
        if (Time.unscaledTime >= _nextSpecialistRefresh)
        {
            _nextSpecialistRefresh = Time.unscaledTime + SpecialistRefreshSeconds;
            RefreshSpecialists();
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            _showOverlay.Value = !_showOverlay.Value;
            _prefs.SaveToFile(false);
            Log.Msg($"ShowOverlay={_showOverlay.Value}");
        }

        UpdatePanel();
    }

    static void UpdatePanel()
    {
        if (_drawFaulted)
            return;
        try
        {
            if (_showOverlay.Value && InDeployment && Snapshot is { HasArmy: true })
                ScoutCanvas.Show(Snapshot, _snapshotVersion);
            else
                ScoutCanvas.Hide();
        }
        catch (Exception e)
        {
            // Stop for the session instead of failing (and logging) every frame.
            _drawFaulted = true;
            Log.Error($"panel failed, disabled until restart: {e}");
            try { ScoutCanvas.Hide(); } catch { }
        }
    }

    internal static void OnFightStart()
    {
        InDeployment = false;
        try
        {
            Snapshot = ArmySnapshotReader.Read(includeArmy: true);
        }
        catch (Exception e)
        {
            Snapshot = null;
            Log.Warning($"snapshot failed: {e.Message}");
            return;
        }
        if (Snapshot == null)
        {
            Log.Msg("fight start: no match to read");
            return;
        }
        foreach (var team in Snapshot.Teams)
        {
            string units = string.Join(", ", team.Units.Select(u => $"{u.MechTypeId}x{u.Cards}(L{u.MaxLevel},{u.Mechs})"));
            string specialists = string.Join(", ", team.Specialists.Select(s => $"{s.Id}:{s.PicName}/{s.IconName}"));
            Log.Msg($"round {Snapshot.Round} team {team.TeamIndex}{(team.IsLocal ? " (you)" : "")}: " +
                    $"value={team.Value}{(team.ValueComplete ? "" : "?")} hp={team.MaxHealth} units=[{units}] specialists=[{specialists}]");
        }
    }

    internal static void OnEnterDeployment()
    {
        InDeployment = true;
        _nextSpecialistRefresh = 0f;
        try
        {
            // A snapshot from another match (or an earlier replay) must not leak into this one.
            var match = ArmySnapshotReader.CurrentMatch();
            if (Snapshot != null && (match == null || match.Pointer != Snapshot.Match || match.RoundCount < Snapshot.Round))
                Snapshot = null;
        }
        catch (Exception e)
        {
            Snapshot = null;
            Log.Warning($"deployment check failed: {e.Message}");
        }
    }

    // Runs once a second in and out of matches; outside a match there is nothing to read and the badges go away.
    static void RefreshSpecialists()
    {
        if (_badgesFaulted || !_showSpecialists.Value)
            return;
        try
        {
            SpecialistBadges.Refresh(ArmySnapshotReader.Read(includeArmy: false));
        }
        catch (Exception e)
        {
            _badgesFaulted = true;
            Log.Error($"specialist badges failed, disabled until restart: {e}");
            try { SpecialistBadges.Clear(); } catch { }
        }
    }

    // Harmony postfixes run inside the game's own methods; an exception must never escape into them.
    internal static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Warning($"hook failed: {e}");
        }
    }

    internal static void OnMatchEnd()
    {
        InDeployment = false;
        Snapshot = null;
    }
}

[HarmonyPatch(typeof(BattleSystem), nameof(BattleSystem.OnFightStart))]
static class FightStartPatch
{
    static void Postfix() => MelperScoutMod.Guard(MelperScoutMod.OnFightStart);
}

[HarmonyPatch(typeof(BattleSystem), nameof(BattleSystem.OnEnterDeploymentAfter))]
static class EnterDeploymentPatch
{
    static void Postfix() => MelperScoutMod.Guard(MelperScoutMod.OnEnterDeployment);
}

[HarmonyPatch(typeof(MatchClient), nameof(MatchClient.OnGameOver))]
static class GameOverPatch
{
    static void Postfix() => MelperScoutMod.Guard(MelperScoutMod.OnMatchEnd);
}

[HarmonyPatch(typeof(MatchClient), nameof(MatchClient.OnQuitStartedMatch))]
static class QuitMatchPatch
{
    static void Postfix() => MelperScoutMod.Guard(MelperScoutMod.OnMatchEnd);
}
