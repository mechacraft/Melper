using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppCinemachine;
using Il2CppGameRiver.Client;
using UnityEngine;

namespace MelperCamera;

// The game switches camera modes by raising one virtual camera's priority; the CinemachineBrain then blends
// to it with a blend picked from its custom blends (per camera pair) or its default blend. Nothing in the
// game's code sets these, so capping them once in place covers every switch.
static class SwitchPatches
{
    static readonly HashSet<string> Logged = new();

    internal static CinemachineBrain? FindBrain()
    {
        var main = Camera.main;
        return main != null ? main.GetComponent<CinemachineBrain>() : null;
    }

    internal static void CapBlends(CinemachineBrain brain)
    {
        float max = Math.Max(0f, CameraSwitchSettings.MaxSwitchSeconds);

        // The getters return copies of the native structs, only for reading; NativeBlend writes the time in place.
        var def = brain.m_DefaultBlend;
        if (NeedsCap(def, max, "default"))
            NativeBlend.SetDefault(brain, def.m_Style, max);

        var blends = brain.m_CustomBlends?.m_CustomBlends;
        if (blends == null)
            return;
        for (int i = 0; i < blends.Length; i++)
        {
            var entry = blends[i];
            if (NeedsCap(entry.m_Blend, max, $"{entry.m_From} -> {entry.m_To}"))
                NativeBlend.SetCustomTime(blends, i, max);
        }
    }

    static bool NeedsCap(CinemachineBlendDefinition blend, float max, string name)
    {
        if (blend.m_Style == CinemachineBlendDefinition.Style.Cut || blend.m_Time <= max)
            return false;
        if (Logged.Add(name))
            MelperCameraMod.Log.Msg($"switch blend {name}: {blend.m_Style} {blend.m_Time}s -> {max}s");
        return true;
    }

    // Before the game raises the new camera's priority; the brain starts the blend later in the frame.
    [HarmonyPatch]
    static class OnActivePatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(GROverAllManualCam), typeof(GROverAllAutoCam), typeof(GROverAllZoomCam), typeof(GRFreeFlyCam), typeof(GRShoulderCam) })
                yield return AccessTools.Method(type, nameof(GRCVCam.OnActive));
        }

        static void Prefix()
        {
            try
            {
                var brain = FindBrain();
                if (brain != null)
                    CapBlends(brain);
            }
            catch (Exception e)
            {
                if (Logged.Add("error"))
                    MelperCameraMod.Log.Warning($"switch blend cap failed: {e}");
            }
        }
    }
}
