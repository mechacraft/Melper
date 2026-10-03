using System;
using System.Runtime.InteropServices;
using Il2CppCinemachine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace MelperCamera;

// Edits blend definitions in place, one plain int/float field at a time.
//
// Il2CppInterop's setters for struct fields (brain.m_DefaultBlend = ..., entry.m_Blend = ...) and for struct
// array elements (blends[i] = ...) memcpy the whole struct without the GC write barrier. The game runs the
// incremental GC, and CinemachineBlendDefinition holds an AnimationCurve reference, so such a copy could leave
// the heap pointing at a freed curve; Unity's asset unload on the next scene change (leaving a match) then
// walked into it and crashed in GameAssembly. Plain value fields carry no references, so writing them is safe.
static class NativeBlend
{
    static readonly int Header = 2 * IntPtr.Size;
    static readonly int ArrayHeader = 4 * IntPtr.Size;

    static readonly IntPtr BlendClass = Il2CppClassPointerStore<CinemachineBlendDefinition>.NativeClassPtr;
    static readonly IntPtr CustomBlendClass = Il2CppClassPointerStore<CinemachineBlenderSettings.CustomBlend>.NativeClassPtr;

    // Offsets of fields inside a value type include the boxed object header; inside an object or array they don't apply.
    static readonly int DefaultBlendInBrain = Offset(Il2CppClassPointerStore<CinemachineBrain>.NativeClassPtr, "m_DefaultBlend");
    static readonly int BlendInCustomBlend = Offset(CustomBlendClass, "m_Blend") - Header;
    static readonly int StyleInBlend = Offset(BlendClass, "m_Style") - Header;
    static readonly int TimeInBlend = Offset(BlendClass, "m_Time") - Header;
    static readonly int CustomBlendSize = ValueSize(CustomBlendClass);

    static bool _checked;

    static int Offset(IntPtr klass, string field)
    {
        var info = IL2CPP.il2cpp_class_get_field_from_name(klass, field);
        if (info == IntPtr.Zero)
            throw new MissingFieldException(field);
        return (int)IL2CPP.il2cpp_field_get_offset(info);
    }

    static int ValueSize(IntPtr klass)
    {
        uint align = 0;
        return IL2CPP.il2cpp_class_value_size(klass, ref align);
    }

    static void Write(IntPtr blend, CinemachineBlendDefinition.Style style, float time)
    {
        Marshal.WriteInt32(blend + StyleInBlend, (int)style);
        Marshal.WriteInt32(blend + TimeInBlend, BitConverter.SingleToInt32Bits(time));
    }

    internal static void SetDefault(CinemachineBrain brain, CinemachineBlendDefinition.Style style, float time)
    {
        Write(brain.Pointer + DefaultBlendInBrain, style, time);
        Check(brain.m_DefaultBlend, style, time);
    }

    internal static void SetCustomTime(Il2CppReferenceArray<CinemachineBlenderSettings.CustomBlend> blends, int index, float time)
    {
        if (index < 0 || index >= blends.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        var blend = blends.Pointer + ArrayHeader + index * CustomBlendSize + BlendInCustomBlend;
        Marshal.WriteInt32(blend + TimeInBlend, BitConverter.SingleToInt32Bits(time));
    }

    // Reads the first write back through the generated getter, so wrong offsets show up in the log.
    static void Check(CinemachineBlendDefinition read, CinemachineBlendDefinition.Style style, float time)
    {
        if (_checked)
            return;
        _checked = true;
        bool ok = read.m_Style == style && read.m_Time == time;
        string line = $"in-place blend write {(ok ? "ok" : "MISMATCH")}: wrote {style} {time}s, read {read.m_Style} {read.m_Time}s";
        if (ok)
            MelperCameraMod.Log.Msg(line);
        else
            MelperCameraMod.Log.Warning(line);
    }
}
