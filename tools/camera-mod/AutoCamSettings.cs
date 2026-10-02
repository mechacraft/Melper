using Il2CppCinemachine;

namespace MelperCamera;

// Settings for the Auto ("Aerial") battle camera, GROverAllAutoCam / OverAll_Auto_Cam.
// Change a value, close the game, then rebuild and deploy:
//   dotnet build tools\camera-mod -c Release -p:Deploy=true
// In game, Ctrl+Shift+A switches all of these off and back on, to compare with the game's own camera.
// The game's values are printed once per launch in MelonLoader\Latest.log as "auto cam defaults: ...".
static class AutoCamSettings
{
    // How the camera fits the units into the frame:
    //   DollyOnly     - moves closer/further, lens stays fixed (no zoom pumping);
    //   DollyThenZoom - moves, then zooms the lens for the rest (game default, "breathes" with zoom);
    //   ZoomOnly      - only zooms the lens.
    public const CinemachineGroupComposer.AdjustmentMode AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyOnly;

    // Closest and farthest the camera may get to the units, in world units. Game: 1 and 1500.
    // For scale: the camera's resting distance from its follow point is about 1060.
    public const float MinDistance = 700f;
    public const float MaxDistance = 2000f;

    // Narrowest and widest lens angle for the zoom modes, in degrees. Game: 5 and 90; the lens itself is 20.
    // Ignored with DollyOnly.
    public const float MinFov = 15f;
    public const float MaxFov = 90f;

    // Smoothing, in seconds-ish: bigger = smoother and calmer, but lags behind the units more. 0 = no smoothing,
    // the camera repeats every step and shot of the units, which looks like swaying.
    //   FrameDamping  - how slowly the distance to the units changes. Game: 4.
    //   AimDamping    - how slowly the view turns after the centre of the units. Game: 4.
    //   FollowDamping - how slowly the camera itself moves after the units. Game: 4.
    public const float FrameDamping = 4f;
    public const float AimDamping = 4f;
    public const float FollowDamping = 4f;

    // Extra empty space around the units, per side, as a fraction of the frame.
    // 0.05 = 5% more on each side. 0 = game default (the units fill 80% of the frame).
    public const float FramingPaddingPerSide = 0f;

    // Camera pitch above the horizon, in degrees: 90 = straight down. Game: 39.4. Capped at MaxTiltDegrees.
    public const float TiltDegrees = 80f;

    // Upper limit for the pitch. Near 90 the camera has no stable "up" and starts to spin.
    public const float MaxTiltDegrees = 82f;

    // true = the camera looks straight along the battlefield ("north" from your side) and ignores which way
    // the units face. false = game behaviour: turned about 16 degrees to the side and swaying with the units'
    // average facing.
    public const bool AlignToMap = true;

    // true = the camera never turns: it slides after the units and moves closer/further, but its pitch and
    // direction stay exactly as set here. false = game behaviour: it also turns a little toward the units.
    public const bool LockViewAngle = true;

    // Turns the camera around the vertical axis, in degrees, from the direction above (straight along the
    // battlefield with AlignToMap, the game's otherwise). The camera circles the units and keeps looking at
    // them. Positive = clockwise seen from above.
    public const float YawDegrees = 0f;

    // Length of the transition into the Auto camera, in seconds. Game: 3 s ease in/out. 0 = instant cut.
    public const float SwitchBlendSeconds = 1f;
}
