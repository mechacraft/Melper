using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppCinemachine;
using Il2CppGameRiver.Client;
using UnityEngine;

namespace MelperCamera;

// Applies AutoCamSettings to the Auto battle camera. Camera-only state: nothing here feeds the simulation.
//
// How the game drives this camera (from GameAssembly): cmCam has a CinemachineTransposer body (position =
// follow point + m_FollowOffset) and a CinemachineGroupComposer aim that frames a CinemachineTargetGroup of
// units. OnActive raises cmCam's priority, so the CinemachineBrain blends to it; DoUpdate moves blendState
// 0 -> 1 when it sees the blend and 1 -> 2 when it ends, and only in state 2 does it drop dead units.
static class AutoCamPatches
{
    // One tuned camera field: the value the game wrote last and the value we wrote over it. When the field no
    // longer holds ours, the game has written it again, and its new value becomes the base.
    sealed class Knob
    {
        public float Game;
        public float Ours = float.NaN;
        public Action<float> Set = _ => { };
    }

    sealed class CamState
    {
        public readonly Dictionary<string, Knob> Knobs = new();
        public Vector3 GameOffset, OurOffset;
        public bool HasOurOffset;
        public CinemachineTransposer? Transposer;
    }

    static readonly Dictionary<IntPtr, CamState> States = new();
    static readonly Dictionary<IntPtr, GROverAllAutoCam> Cams = new();
    static readonly HashSet<string> Logged = new();

    // Ctrl+Shift+A flips this, to compare the game's own camera with the tuned one in the same battle.
    static bool _enabled = true;
    internal static bool Enabled => _enabled;

    // Blend shortening for the switch into this camera.
    static CinemachineBrain? _brain;
    static CinemachineBlendDefinition? _savedDefaultBlend;
    static CinemachineBlenderSettings? _savedCustomBlends;
    static int _activatedFrame = -1;
    static float _activatedAt;
    static bool _snapFrames;

    static void LogOnce(string key, string line)
    {
        if (Logged.Add(key))
            MelperCameraMod.Log.Msg(line);
    }

    [HarmonyPatch(typeof(GROverAllAutoCam), nameof(GROverAllAutoCam.OnActive))]
    static class OnActivePatch
    {
        // Before the game raises the camera's priority, so the brain picks up our blend when it starts one.
        static void Prefix(GROverAllAutoCam __instance)
        {
            if (!_enabled)
                return;
            try
            {
                var brain = SwitchPatches.FindBrain();
                if (brain == null)
                    return;
                RestoreBlend();
                // Capped first, so the blends given back after the switch are the capped ones.
                SwitchPatches.CapBlends(brain);
                _brain = brain;
                _savedDefaultBlend = brain.m_DefaultBlend;
                _savedCustomBlends = brain.m_CustomBlends;
                var style = AutoCamSettings.SwitchBlendSeconds > 0f ? CinemachineBlendDefinition.Style.EaseOut : CinemachineBlendDefinition.Style.Cut;
                brain.m_DefaultBlend = new CinemachineBlendDefinition(style, AutoCamSettings.SwitchBlendSeconds);
                brain.m_CustomBlends = null;
                LogOnce("blend",
                    $"auto cam switch blend: {_savedDefaultBlend.m_Style} {_savedDefaultBlend.m_Time}s " +
                    $"(custom blends {(_savedCustomBlends != null ? "present" : "none")}) -> {style} {AutoCamSettings.SwitchBlendSeconds}s");
            }
            catch (Exception e)
            {
                MelperCameraMod.Log.Warning($"auto cam blend setup failed: {e}");
            }
        }

        static void Postfix(GROverAllAutoCam __instance)
        {
            _activatedFrame = Time.frameCount;
            _activatedAt = Time.unscaledTime;
            _snapFrames = _enabled;
            Cams[__instance.Pointer] = __instance;
            if (_enabled)
                Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(GROverAllAutoCam), nameof(GROverAllAutoCam.DoUpdate))]
    static class DoUpdatePatch
    {
        static void Postfix(GROverAllAutoCam __instance)
        {
            try
            {
                Cams[__instance.Pointer] = __instance;

                // The brain reads the blend definition in LateUpdate of the activation frame; give it back after.
                if (_savedDefaultBlend != null && Time.frameCount > _activatedFrame + 1)
                    RestoreBlend();

                if (!_enabled)
                    return;
                Apply(__instance);

                // Teleport into the framed position for the first frames instead of easing in through damping.
                var cam = __instance.cmCam;
                if (_snapFrames && cam != null)
                {
                    cam.PreviousStateIsValid = false;
                    if (Time.frameCount > _activatedFrame + 2)
                        _snapFrames = false;
                }

                if (cam != null && __instance.IsCurCam() && _brain != null && !_brain.IsBlending)
                    ViewProbe.Sample(cam);

                // A short blend can finish before DoUpdate ever sees it, which would leave blendState at 0.
                if (__instance.blendState < 2 && Time.unscaledTime - _activatedAt > AutoCamSettings.SwitchBlendSeconds + 0.5f
                    && (_brain == null || !_brain.IsBlending))
                {
                    LogOnce("blendState", $"auto cam blendState {__instance.blendState} -> 2 (blend already over)");
                    __instance.blendState = 2;
                }
            }
            catch (Exception e)
            {
                LogOnce("doupdate-error", $"auto cam update failed: {e}");
            }
        }
    }

    internal static void Toggle()
    {
        _enabled = !_enabled;
        foreach (var autoCam in Cams.Values)
        {
            if (autoCam == null || autoCam.WasCollected)
                continue;
            if (_enabled)
                Apply(autoCam);
            else
                RestoreGameValues(autoCam);
        }
        MelperCameraMod.Log.Msg($"auto cam tweaks {(_enabled ? "ON" : "OFF (game defaults)")}");
    }

    static void RestoreGameValues(GROverAllAutoCam autoCam)
    {
        var cam = autoCam.cmCam;
        if (cam == null || !States.TryGetValue(cam.Pointer, out var state))
            return;
        foreach (var knob in state.Knobs.Values)
        {
            if (float.IsNaN(knob.Ours))
                continue;
            knob.Set(knob.Game);
            knob.Ours = float.NaN;
        }
        if (state.HasOurOffset && state.Transposer != null)
        {
            state.Transposer.m_FollowOffset = state.GameOffset;
            state.HasOurOffset = false;
        }
    }

    static void Apply(GROverAllAutoCam autoCam)
    {
        var cam = autoCam.cmCam;
        if (cam == null)
            return;
        var composer = cam.GetCinemachineComponent(CinemachineCore.Stage.Aim)?.TryCast<CinemachineGroupComposer>();
        var transposer = cam.GetCinemachineComponent(CinemachineCore.Stage.Body)?.TryCast<CinemachineTransposer>();

        if (!States.TryGetValue(cam.Pointer, out var state))
        {
            state = new CamState();
            States[cam.Pointer] = state;
            LogDefaults(cam, composer, transposer);
        }
        state.Transposer = transposer;

        if (composer != null)
        {
            Tune(state, "adjustment mode", (int)composer.m_AdjustmentMode, _ => (int)AutoCamSettings.AdjustmentMode,
                v => composer.m_AdjustmentMode = (CinemachineGroupComposer.AdjustmentMode)(int)v);
            Tune(state, "min distance", composer.m_MinimumDistance, _ => AutoCamSettings.MinDistance,
                v => { composer.m_MinimumDistance = v; autoCam.minDis = v; });
            Tune(state, "max distance", composer.m_MaximumDistance, _ => AutoCamSettings.MaxDistance,
                v => { composer.m_MaximumDistance = v; autoCam.maxDis = v; });
            Tune(state, "min FOV", composer.m_MinimumFOV, _ => AutoCamSettings.MinFov, v => composer.m_MinimumFOV = v);
            Tune(state, "max FOV", composer.m_MaximumFOV, _ => AutoCamSettings.MaxFov, v => composer.m_MaximumFOV = v);
            Tune(state, "frame damping", composer.m_FrameDamping, _ => AutoCamSettings.FrameDamping, v => composer.m_FrameDamping = v);
            Tune(state, "group framing size", composer.m_GroupFramingSize,
                game => game / (1f + 2f * AutoCamSettings.FramingPaddingPerSide), v => composer.m_GroupFramingSize = v);
            // A dead zone over the whole screen: the composer aims once when the camera starts and never turns
            // it after that. The camera sits at a fixed offset from the same target group it looks at, so the
            // units stay in the middle anyway.
            bool locked = AutoCamSettings.LockViewAngle;
            Tune(state, "soft zone width", composer.m_SoftZoneWidth, game => locked ? 2f : game, v => composer.m_SoftZoneWidth = v);
            Tune(state, "soft zone height", composer.m_SoftZoneHeight, game => locked ? 2f : game, v => composer.m_SoftZoneHeight = v);
            Tune(state, "dead zone width", composer.m_DeadZoneWidth, game => locked ? 2f : game, v => composer.m_DeadZoneWidth = v);
            Tune(state, "dead zone height", composer.m_DeadZoneHeight, game => locked ? 2f : game, v => composer.m_DeadZoneHeight = v);
            Tune(state, "lookahead", composer.m_LookaheadTime, game => locked ? 0f : game, v => composer.m_LookaheadTime = v);
            Tune(state, "aim damping h", composer.m_HorizontalDamping, _ => AutoCamSettings.AimDamping, v => composer.m_HorizontalDamping = v);
            Tune(state, "aim damping v", composer.m_VerticalDamping, _ => AutoCamSettings.AimDamping, v => composer.m_VerticalDamping = v);
        }

        if (transposer != null)
        {
            // World space: the offset no longer turns with the target group's rotation (the units' facing).
            bool aligned = AutoCamSettings.AlignToMap;
            Tune(state, "binding mode", (int)transposer.m_BindingMode, game => aligned ? (int)CinemachineTransposer.BindingMode.WorldSpace : game,
                v => transposer.m_BindingMode = (CinemachineTransposer.BindingMode)(int)v);
            Tune(state, "follow damping x", transposer.m_XDamping, _ => AutoCamSettings.FollowDamping, v => transposer.m_XDamping = v);
            Tune(state, "follow damping y", transposer.m_YDamping, _ => AutoCamSettings.FollowDamping, v => transposer.m_YDamping = v);
            Tune(state, "follow damping z", transposer.m_ZDamping, _ => AutoCamSettings.FollowDamping, v => transposer.m_ZDamping = v);
        }

        if (transposer != null)
        {
            // The field no longer holds ours: the game wrote it (first time, or ReversVCam), and that is the new base.
            if (!(state.HasOurOffset && transposer.m_FollowOffset == state.OurOffset))
            {
                state.GameOffset = transposer.m_FollowOffset;
                state.HasOurOffset = false;
            }
            var wanted = OurOffset(state.GameOffset, cam.Follow, out float before, out float after, out float side);
            if (!state.HasOurOffset || wanted != state.OurOffset)
            {
                state.OurOffset = wanted;
                state.HasOurOffset = true;
                transposer.m_FollowOffset = wanted;
                LogOnce($"tilt {side}", $"auto cam tilt {before:F1} -> {after:F1} deg, side {side} deg, yaw {AutoCamSettings.YawDegrees:+0.#;-0.#;0} deg, " +
                                        $"follow offset {state.GameOffset} -> {wanted}");
            }

            if (AutoCamSettings.LockViewAngle)
            {
                // World-space direction from the camera to the units.
                var world = AutoCamSettings.AlignToMap || cam.Follow == null ? wanted : cam.Follow.rotation * wanted;
                LockedCamName = cam.Name;
                LockedRotation = Quaternion.LookRotation(-world, Vector3.up);
            }
        }
    }

    // The game turns the target group to face from the player's side (180 degrees for one of the sides), and
    // with LockToTargetWithWorldUp that turn carried the offset along. In world space we take that side from
    // the group's rotation ourselves, rounded to 90 degrees so the units' facing can't sway it.
    static Vector3 OurOffset(Vector3 gameOffset, Transform? follow, out float before, out float after, out float side)
    {
        var tilted = Tilt(gameOffset, out before, out after);
        side = 0f;
        if (!AutoCamSettings.AlignToMap)
            return Quaternion.Euler(0f, AutoCamSettings.YawDegrees, 0f) * tilted;

        float flat = new Vector2(tilted.x, tilted.z).magnitude;
        var straight = new Vector3(0f, tilted.y, tilted.z < 0f ? -flat : flat);
        if (follow != null)
            side = Mathf.Repeat(Mathf.Round(follow.eulerAngles.y / 90f) * 90f, 360f);
        return Quaternion.Euler(0f, side + AutoCamSettings.YawDegrees, 0f) * straight;
    }

    // Set while the Auto camera is tuned with LockViewAngle; applied to the real camera after Cinemachine.
    static string? LockedCamName;
    static Quaternion LockedRotation;

    // Cinemachine's aim still turns the camera a few degrees toward the units, so the rotation is overwritten
    // on the output camera after the brain has placed it. Only while the Auto camera is fully live.
    [HarmonyPatch(typeof(CinemachineBrain), nameof(CinemachineBrain.ManualUpdate))]
    static class BrainPatch
    {
        static void Postfix(CinemachineBrain __instance)
        {
            // Only the battle camera's brain: other screens (e.g. the unit tech view) drive brains of their own.
            if (!_enabled || !AutoCamSettings.LockViewAngle || LockedCamName == null || _brain == null
                || __instance.Pointer != _brain.Pointer)
                return;
            try
            {
                if (__instance.IsBlending)
                {
                    // Steer the blend's rotation toward ours instead of the composer's own aim, so there is
                    // nothing left to snap when the blend ends.
                    var blend = __instance.ActiveBlend;
                    var a = blend?.CamA;
                    var b = blend?.CamB;
                    if (blend == null || a == null || b == null)
                        return;
                    bool aOurs = a.Name == LockedCamName, bOurs = b.Name == LockedCamName;
                    if (!aOurs && !bOurs)
                        return;
                    var from = aOurs ? LockedRotation : a.State.FinalOrientation;
                    var to = bOurs ? LockedRotation : b.State.FinalOrientation;
                    __instance.transform.rotation = Quaternion.Slerp(from, to, blend.BlendWeight);
                    return;
                }
                var live = __instance.ActiveVirtualCamera;
                if (live == null || live.Name != LockedCamName)
                    return;
                __instance.transform.rotation = LockedRotation;
            }
            catch (Exception e)
            {
                LogOnce("brain-error", $"auto cam view lock failed: {e}");
            }
        }
    }

    static void Tune(CamState state, string name, float current, Func<float, float> target, Action<float> set)
    {
        if (!state.Knobs.TryGetValue(name, out var knob))
            state.Knobs[name] = knob = new Knob();
        knob.Set = set;
        if (current == knob.Ours)
        {
            // Still ours; the setting may have changed since (F7 panel).
            float wanted = target(knob.Game);
            if (wanted != knob.Ours)
            {
                knob.Ours = wanted;
                set(wanted);
            }
            return;
        }
        knob.Game = current;
        knob.Ours = target(current);
        set(knob.Ours);
        LogOnce(name, $"auto cam {name} {knob.Game} -> {knob.Ours}");
    }

    // Rotates the offset to the set pitch around the follow point, keeping its length and compass direction.
    static Vector3 Tilt(Vector3 offset, out float beforeDeg, out float afterDeg)
    {
        float length = offset.magnitude;
        var flat = new Vector3(offset.x, 0f, offset.z);
        beforeDeg = Mathf.Atan2(offset.y, flat.magnitude) * Mathf.Rad2Deg;
        afterDeg = beforeDeg;
        if (length < 0.001f || flat.sqrMagnitude < 0.000001f)
            return offset;

        afterDeg = Mathf.Clamp(AutoCamSettings.TiltDegrees, 1f, AutoCamSettings.MaxTiltDegrees);
        float rad = afterDeg * Mathf.Deg2Rad;
        return flat.normalized * (length * Mathf.Cos(rad)) + Vector3.up * (Mathf.Sign(offset.y == 0f ? 1f : offset.y) * length * Mathf.Sin(rad));
    }

    static void LogDefaults(CinemachineVirtualCamera cam, CinemachineGroupComposer? composer, CinemachineTransposer? transposer)
    {
        var aim = cam.GetCinemachineComponent(CinemachineCore.Stage.Aim);
        var body = cam.GetCinemachineComponent(CinemachineCore.Stage.Body);
        MelperCameraMod.Log.Msg($"auto cam defaults: vcam={cam.Name} fov={cam.m_Lens.FieldOfView} " +
                                $"aim={aim?.GetIl2CppType().Name ?? "none"} body={body?.GetIl2CppType().Name ?? "none"} " +
                                $"follow={cam.Follow?.name ?? "none"} lookAt={cam.LookAt?.name ?? "none"} " +
                                $"(same object: {cam.Follow != null && cam.Follow == cam.LookAt})");
        if (composer != null)
            MelperCameraMod.Log.Msg(
                $"auto cam defaults: framingSize={composer.m_GroupFramingSize} framingMode={composer.m_FramingMode} " +
                $"adjustment={composer.m_AdjustmentMode} dollyIn={composer.m_MaxDollyIn} dollyOut={composer.m_MaxDollyOut} " +
                $"distance={composer.m_MinimumDistance}..{composer.m_MaximumDistance} fov={composer.m_MinimumFOV}..{composer.m_MaximumFOV} " +
                $"damping h={composer.m_HorizontalDamping} v={composer.m_VerticalDamping} frame={composer.m_FrameDamping}");
        if (transposer != null)
            MelperCameraMod.Log.Msg(
                $"auto cam defaults: binding={transposer.m_BindingMode} followOffset={transposer.m_FollowOffset} " +
                $"damping x={transposer.m_XDamping} y={transposer.m_YDamping} z={transposer.m_ZDamping} yaw={transposer.m_YawDamping}");
    }

    // Diagnostics: how much the camera's view angle and distance actually wander, summed up every 10 s.
    internal static class ViewProbe
    {
        // Latest sample, shown live in the F7 panel.
        internal static float LastPitch, LastHeading, LastDistance, LastAt = -1f;

        const float Window = 10f;
        static float _start = -1f, _heading0;
        static float _pitchMin, _pitchMax, _headMin, _headMax, _distMin, _distMax;

        internal static void Sample(CinemachineVirtualCamera cam)
        {
            var main = Camera.main;
            if (main == null)
                return;
            var t = main.transform;
            float pitch = Mathf.DeltaAngle(0f, t.eulerAngles.x);
            float heading = t.eulerAngles.y;
            float dist = cam.LookAt != null ? Vector3.Distance(t.position, cam.LookAt.position) : float.NaN;
            float now = Time.unscaledTime;
            LastPitch = pitch; LastHeading = heading; LastDistance = dist; LastAt = now;

            if (_start < 0f)
            {
                _start = now;
                _heading0 = heading;
                _pitchMin = _pitchMax = pitch;
                _headMin = _headMax = 0f;
                _distMin = _distMax = dist;
                return;
            }
            float head = Mathf.DeltaAngle(_heading0, heading);
            _pitchMin = Mathf.Min(_pitchMin, pitch); _pitchMax = Mathf.Max(_pitchMax, pitch);
            _headMin = Mathf.Min(_headMin, head); _headMax = Mathf.Max(_headMax, head);
            _distMin = Mathf.Min(_distMin, dist); _distMax = Mathf.Max(_distMax, dist);

            if (now - _start < Window)
                return;
            MelperCameraMod.Log.Msg(
                $"auto cam view over {now - _start:F0}s: pitch {_pitchMin:F1}..{_pitchMax:F1} deg, " +
                $"heading {_heading0 + _headMin:F1}..{_heading0 + _headMax:F1} deg, distance to lookAt {_distMin:F0}..{_distMax:F0}");
            _start = -1f;
        }
    }

    static void RestoreBlend()
    {
        if (_brain != null && _savedDefaultBlend != null)
        {
            _brain.m_DefaultBlend = _savedDefaultBlend;
            _brain.m_CustomBlends = _savedCustomBlends;
        }
        _savedDefaultBlend = null;
        _savedCustomBlends = null;
    }
}
