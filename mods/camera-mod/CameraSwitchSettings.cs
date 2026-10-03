namespace MelperCamera;

// Settings for switching between camera modes (manual, auto, zoom, free fly, shoulder).
// Change a value, close the game, then rebuild and deploy:
//   dotnet build mods\camera-mod -c Release -p:Deploy=true
// What the game had and what the mod changed is printed once in MelonLoader\Latest.log as "switch blend ...".
static class CameraSwitchSettings
{
    // Longest transition between any two camera modes, in seconds. The game uses up to 3 s.
    // Shorter transitions are left as they are; 0 = instant cut everywhere.
    // The switch into the Auto camera has its own length: AutoCamSettings.SwitchBlendSeconds.
    public const float MaxSwitchSeconds = 1f;
}
