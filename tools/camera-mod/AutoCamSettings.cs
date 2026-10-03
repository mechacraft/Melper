using System;
using MelonLoader;
using Il2CppCinemachine;

namespace MelperCamera;

// Settings for the Auto ("Aerial") battle camera, GROverAllAutoCam / OverAll_Auto_Cam.
// F7 in game opens a panel to change them live; changes are saved to UserData\MelonPreferences.cfg,
// section [MelperCameraAuto], and that file wins over the defaults below. The panel's "Сбросить" button
// goes back to these defaults. Ctrl+Shift+A switches all of them off and back on, to compare with the game.
// The game's own values are printed once per launch in MelonLoader\Latest.log as "auto cam defaults: ...".
static class AutoCamSettings
{
    internal sealed class Setting
    {
        public readonly string Key, Label;
        public readonly float Default, Min, Max, Step;
        public readonly string[]? Choices; // a switch: the value is the index of the choice
        public readonly bool Percent;
        public MelonPreferences_Entry<float>? Entry;

        public Setting(string key, string label, float def, float min, float max, float step, bool percent = false)
        {
            Key = key; Label = label; Default = def; Min = min; Max = max; Step = step; Percent = percent;
        }

        public Setting(string key, string label, int def, params string[] choices)
            : this(key, label, def, 0, choices.Length - 1, 1) => Choices = choices;

        public float Value
        {
            get => Entry != null ? Math.Clamp(Entry.Value, Min, Max) : Default;
            set
            {
                if (Entry != null)
                    Entry.Value = Math.Clamp((float)Math.Round(value / Step) * Step, Min, Max);
            }
        }

        public bool On => Value >= 0.5f;

        public string Text => Choices != null ? Choices[(int)Math.Round(Value)]
            : Percent ? $"{Value * 100f:0.#}%"
            : Step < 1f ? $"{Value:0.0#}" : $"{Value:0}";
    }

    // ---- Defaults. Edit here, or live with F7 in game. ----

    // How the camera fits the units into the frame. Game: "наезд, потом зум" (it "breathes" with zoom).
    internal static readonly Setting Adjustment = new("AdjustmentMode", "Подгонка кадра",
        (int)CinemachineGroupComposer.AdjustmentMode.DollyOnly, "только зум", "только наезд", "наезд, потом зум");

    // Pitch above the horizon: 90 = straight down. Game: 39.4.
    internal static readonly Setting Tilt = new("TiltDegrees", "Наклон, °", 70f, 30f, MaxTiltDegrees, 1f);

    // Turn around the vertical axis from straight along the field. Positive = clockwise seen from above.
    internal static readonly Setting Yaw = new("YawDegrees", "Поворот, °", 0f, -180f, 180f, 5f);

    // Look straight along the battlefield from your side, ignoring which way the units face.
    // Off = game: about 16 degrees to the side and swaying with the units' facing.
    internal static readonly Setting Align = new("AlignToMap", "Вдоль поля", 1, "нет (как в игре)", "да");

    // Never turn the camera: it only slides after the units and moves closer/further.
    internal static readonly Setting Lock = new("LockViewAngle", "Жёсткий угол", 1, "нет (как в игре)", "да");

    // Closest and farthest the camera may get to the units, in world units. Game: 1 and 1500.
    internal static readonly Setting MinDist = new("MinDistance", "Мин. дистанция", 200f, 100f, 3000f, 50f);
    internal static readonly Setting MaxDist = new("MaxDistance", "Макс. дистанция", 2000f, 500f, 6000f, 100f);

    // Extra empty space around the units on each side. 0 = game (the units fill 80% of the frame).
    internal static readonly Setting Padding = new("FramingPaddingPerSide", "Запас по краям", 0f, 0f, 0.3f, 0.01f, percent: true);

    // Smoothing: bigger = calmer, but lags behind the units more. 0 = repeats every step of the units. Game: 4.
    internal static readonly Setting FrameDamp = new("FrameDamping", "Плавность дистанции", 4f, 0f, 20f, 0.5f);
    internal static readonly Setting FollowDamp = new("FollowDamping", "Плавность движения", 4f, 0f, 20f, 0.5f);
    internal static readonly Setting AimDamp = new("AimDamping", "Плавность взгляда", 4f, 0f, 20f, 0.5f);

    // Lens angle limits for the zoom modes, in degrees. Game: 5 and 90; the lens itself is 20.
    internal static readonly Setting FovMin = new("MinFov", "Мин. FOV (зум)", 15f, 1f, 90f, 1f);
    internal static readonly Setting FovMax = new("MaxFov", "Макс. FOV (зум)", 90f, 1f, 120f, 1f);

    // Transition into the Auto camera, seconds. Game: 3 s. 0 = instant cut.
    internal static readonly Setting Blend = new("SwitchBlendSeconds", "Переход в auto, с", 1f, 0f, 3f, 0.1f);

    internal static readonly Setting[] All =
        { Adjustment, Tilt, Yaw, Align, Lock, MinDist, MaxDist, Padding, FrameDamp, FollowDamp, AimDamp, FovMin, FovMax, Blend };

    // Hard limit for the pitch: near 90 the camera has no stable "up" and can spin.
    public const float MaxTiltDegrees = 85f;

    static MelonPreferences_Category? _category;

    internal static void Init()
    {
        _category = MelonPreferences.CreateCategory("MelperCameraAuto");
        foreach (var s in All)
            s.Entry = _category.CreateEntry(s.Key, s.Default);
    }

    internal static void Save() => _category?.SaveToFile(false);

    internal static void ResetToDefaults()
    {
        foreach (var s in All)
            s.Value = s.Default;
        Save();
    }

    public static CinemachineGroupComposer.AdjustmentMode AdjustmentMode => (CinemachineGroupComposer.AdjustmentMode)(int)Math.Round(Adjustment.Value);
    public static float TiltDegrees => Tilt.Value;
    public static float YawDegrees => Yaw.Value;
    public static bool AlignToMap => Align.On;
    public static bool LockViewAngle => Lock.On;
    public static float MinDistance => MinDist.Value;
    public static float MaxDistance => Math.Max(MaxDist.Value, MinDist.Value);
    public static float FramingPaddingPerSide => Padding.Value;
    public static float FrameDamping => FrameDamp.Value;
    public static float FollowDamping => FollowDamp.Value;
    public static float AimDamping => AimDamp.Value;
    public static float MinFov => FovMin.Value;
    public static float MaxFov => Math.Max(FovMax.Value, FovMin.Value);
    public static float SwitchBlendSeconds => Blend.Value;
}
