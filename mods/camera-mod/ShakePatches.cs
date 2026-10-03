using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppCinemachine;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MelperCamera;

// Camera shake comes from two Cinemachine sources: Perlin noise on a virtual camera (constant sway)
// and impulse listeners (kicks from explosions). Both only move the rendered view.
static class ShakePatches
{
    // Names of cameras already described in the log, so each one is reported once.
    static readonly HashSet<string> Reported = new();

    internal static void Apply(string reason)
    {
        try
        {
            if (MelperCameraMod.DisableCameraNoise)
            {
                foreach (var noise in FindAll<CinemachineBasicMultiChannelPerlin>())
                {
                    Report($"noise on {CameraName(noise.VirtualCamera, noise)}: amplitude={noise.m_AmplitudeGain} frequency={noise.m_FrequencyGain}", reason);
                    noise.m_AmplitudeGain = 0f;
                }
            }

            if (MelperCameraMod.DisableImpulseShake)
            {
                foreach (var listener in FindAll<CinemachineImpulseListener>())
                {
                    Report($"impulse listener on {CameraName(listener.VirtualCamera, listener)}: gain={listener.m_Gain}", reason);
                    listener.m_Gain = 0f;
                }
                foreach (var listener in FindAll<CinemachineImpulseListener2>())
                {
                    Report($"impulse listener2 on {CameraName(listener.VirtualCamera, listener)}: gain={listener.m_Gain}", reason);
                    listener.m_Gain = 0f;
                }
            }
        }
        catch (Exception e)
        {
            MelperCameraMod.Log.Warning($"shake patch failed ({reason}): {e}");
        }
    }

    static IEnumerable<T> FindAll<T>() where T : Object
    {
        foreach (var obj in Object.FindObjectsOfType(Il2CppType.Of<T>(), true))
        {
            var typed = obj.TryCast<T>();
            if (typed != null)
                yield return typed;
        }
    }

    static string CameraName(CinemachineVirtualCameraBase? vcam, Component fallback) =>
        vcam != null ? vcam.Name : fallback.gameObject.name;

    static void Report(string line, string reason)
    {
        if (Reported.Add(line))
            MelperCameraMod.Log.Msg($"[{reason}] {line} -> 0");
    }

    // Every camera mode switch: the mode's virtual cameras may only now be created or enabled.
    [HarmonyPatch]
    static class OnActivePatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(GROverAllManualCam), typeof(GROverAllAutoCam), typeof(GROverAllZoomCam), typeof(GRFreeFlyCam), typeof(GRShoulderCam) })
                yield return AccessTools.Method(type, nameof(GRCVCam.OnActive));
        }

        static void Postfix(GRCVCam __instance) => Apply(__instance.GetIl2CppType().Name + ".OnActive");
    }
}
