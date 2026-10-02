using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MelperCamera.MelperCameraMod), "MelperCamera", "0.1.0", "Melper")]
[assembly: MelonGame("GameRiver", "Mechabellum")]

namespace MelperCamera;

// Client-side camera tweaks only: nothing here touches the battle simulation or game config.
// MelonLoader applies the [HarmonyPatch] classes below by itself.
public sealed class MelperCameraMod : MelonMod
{
    const float MultiplierStep = 0.25f;
    const float MinMultiplier = 1f;
    const float MaxMultiplier = 4f;

    static MelonPreferences_Category _prefs = null!;
    static MelonPreferences_Entry<bool> _hideCameraTips = null!;
    static MelonPreferences_Entry<float> _zoomOutMultiplier = null!;
    static MelonPreferences_Entry<bool> _disableCameraNoise = null!;
    static MelonPreferences_Entry<bool> _disableImpulseShake = null!;

    internal static bool HideCameraTips => _hideCameraTips.Value;
    internal static bool DisableCameraNoise => _disableCameraNoise.Value;
    internal static bool DisableImpulseShake => _disableImpulseShake.Value;
    internal static float ZoomOutMultiplier => Math.Clamp(_zoomOutMultiplier.Value, MinMultiplier, MaxMultiplier);

    internal static MelonLogger.Instance Log = null!;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        _prefs = MelonPreferences.CreateCategory("MelperCamera");
        _hideCameraTips = _prefs.CreateEntry("HideCameraTips", true, description: "Hide the camera controls hint at the bottom of the screen.");
        _zoomOutMultiplier = _prefs.CreateEntry("ZoomOutMultiplier", 1.5f, description: "Max camera distance = game's own max * this (1..4). Ctrl+= / Ctrl+- change it in game.");
        _disableCameraNoise = _prefs.CreateEntry("DisableCameraNoise", true, description: "Turn off the handheld-style Perlin noise that sways some camera modes.");
        _disableImpulseShake = _prefs.CreateEntry("DisableImpulseShake", true, description: "Turn off camera shake from explosions and impacts.");
        Log.Msg($"loaded: HideCameraTips={HideCameraTips} ZoomOutMultiplier={ZoomOutMultiplier} " +
                $"DisableCameraNoise={DisableCameraNoise} DisableImpulseShake={DisableImpulseShake}");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => ShakePatches.Apply($"scene {sceneName}");

    public override void OnUpdate()
    {
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            return;

        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.A))
        {
            AutoCamPatches.Toggle();
            return;
        }

        float delta = 0f;
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            delta = MultiplierStep;
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            delta = -MultiplierStep;
        if (delta == 0f)
            return;

        _zoomOutMultiplier.Value = Math.Clamp(ZoomOutMultiplier + delta, MinMultiplier, MaxMultiplier);
        _prefs.SaveToFile(false);
        ZoomPatches.ApplyToAll();
        Log.Msg($"ZoomOutMultiplier={ZoomOutMultiplier}");
    }
}

[HarmonyPatch(typeof(CameraTips), nameof(CameraTips.ShowTips))]
static class CameraTipsPatch
{
    static bool _logged;

    // Postfix rather than a skipping prefix, so a tip the game already shows also gets hidden.
    static void Postfix(CameraTips __instance)
    {
        if (!MelperCameraMod.HideCameraTips)
            return;

        var tips = __instance.tips;
        if (tips != null)
        {
            for (int i = 0; i < tips.Count; i++)
                tips[i]?.SetActive(false);
        }

        if (!_logged)
        {
            _logged = true;
            MelperCameraMod.Log.Msg($"CameraTips.ShowTips hit: hid {tips?.Count ?? 0} tips");
        }
    }
}

static class ZoomPatches
{
    // Game's own maxDis per controller, recorded before we first scale it, so a repeated Init doesn't compound.
    static readonly Dictionary<IntPtr, float> OriginalMaxDis = new();
    static readonly List<ZoomCameraController> Controllers = new();
    static bool _initLogged;
    static bool _getterLogged;

    static float Target(ZoomCameraController zoom) =>
        OriginalMaxDis.TryGetValue(zoom.Pointer, out float original) ? original * MelperCameraMod.ZoomOutMultiplier : zoom.maxDis;

    internal static void Apply(ZoomCameraController zoom)
    {
        if (!OriginalMaxDis.ContainsKey(zoom.Pointer))
        {
            OriginalMaxDis[zoom.Pointer] = zoom.maxDis;
            Controllers.Add(zoom);
        }
        zoom.maxDis = Target(zoom);
    }

    internal static void ApplyToAll()
    {
        Controllers.RemoveAll(z => z == null || z.WasCollected);
        foreach (var zoom in Controllers)
            Apply(zoom);
    }

    [HarmonyPatch(typeof(ZoomCameraController), nameof(ZoomCameraController.Init))]
    static class InitPatch
    {
        static void Postfix(ZoomCameraController __instance)
        {
            float before = __instance.maxDis;
            Apply(__instance);
            if (_initLogged)
                return;
            _initLogged = true;
            float farClip = Camera.main != null ? Camera.main.farClipPlane : -1f;
            MelperCameraMod.Log.Msg(
                $"ZoomCameraController.Init hit: minDis={__instance.minDis} maxDis {before} -> {__instance.maxDis} " +
                $"ZoomMaxDis={ZoomCameraController.ZoomMaxDis} farClipPlane={farClip}");
        }
    }

    // Backup in case the game reads the limit through the getter rather than the field.
    [HarmonyPatch(typeof(ZoomCameraController), nameof(ZoomCameraController.GetMaxDis))]
    static class GetMaxDisPatch
    {
        static void Postfix(ZoomCameraController __instance, ref float __result)
        {
            float original = __result;
            __result = Math.Max(__result, Target(__instance));
            if (_getterLogged)
                return;
            _getterLogged = true;
            MelperCameraMod.Log.Msg($"ZoomCameraController.GetMaxDis hit: {original} -> {__result}");
        }
    }
}
