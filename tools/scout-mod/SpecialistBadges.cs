using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MelperScout;

// Small specialist portraits right under each player's avatar in the game's own top panel.
// The game itself shows each player's specialist count there, so this is public information.
static class SpecialistBadges
{
    const string BadgeName = "MelperScout.Specialists";
    const float SizeOfAvatar = 0.42f;
    const float Gap = 2f;

    // Avatar image pointer -> the badge row we attached to it and the specialists it shows.
    static readonly Dictionary<IntPtr, (GameObject Row, string Key)> Rows = new();
    static readonly HashSet<string> Dumped = new(StringComparer.Ordinal);

    internal static void Refresh(MatchSnapshot? live)
    {
        var byTeam = live?.Teams.ToDictionary(t => t.TeamIndex) ?? new Dictionary<int, TeamSnapshot>();
        var seen = new HashSet<IntPtr>();

        var panels = Object.FindObjectsOfType<PlayerInfoPanel>();
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            var avatar = Alive(panel) ? panel.mainPlayerPortrait : null;
            var image = Alive(avatar) ? avatar!.protraitImage : null;
            var controller = Alive(avatar) ? avatar!.playerController : null;
            if (!Alive(image) || !ArmySnapshotReader.Usable(controller))
                continue;
            seen.Add(image!.Pointer);
            byTeam.TryGetValue(controller!.GetTeamIndex(), out var team);
            Attach(image, team?.Specialists ?? new List<SpecialistInfo>());
        }

        foreach (var key in Rows.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            if (Alive(Rows[key].Row))
                Object.Destroy(Rows[key].Row);
            Rows.Remove(key);
        }
    }

    internal static void Clear()
    {
        foreach (var (row, _) in Rows.Values)
            if (Alive(row))
                Object.Destroy(row);
        Rows.Clear();
    }

    static void Attach(GRImage avatar, List<SpecialistInfo> specialists)
    {
        string key = string.Join(",", specialists.Select(s => s.Id));
        if (Rows.TryGetValue(avatar.Pointer, out var existing) && Alive(existing.Row))
        {
            if (existing.Key == key)
                return;
            Object.Destroy(existing.Row);
        }
        Rows.Remove(avatar.Pointer);
        if (specialists.Count == 0)
            return;

        var avatarRect = avatar.rectTransform;
        float size = Math.Max(16f, avatarRect.rect.height * SizeOfAvatar);

        var row = new GameObject(BadgeName, Types<RectTransform>());
        row.transform.SetParent(avatarRect, false);
        var rowRect = row.GetComponent<RectTransform>();
        // Just under the avatar, starting at its left edge.
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0f, 0f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -Gap);
        rowRect.sizeDelta = new Vector2(specialists.Count * (size + Gap), size);

        for (int i = 0; i < specialists.Count; i++)
        {
            var specialist = specialists[i];
            DumpCandidates(specialist);
            var sprite = FaceSprite(specialist);
            if (sprite == null)
                continue;
            var icon = new GameObject("Face", Types<RectTransform>());
            icon.transform.SetParent(rowRect, false);
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(i * (size + Gap), 0f);
            rect.sizeDelta = new Vector2(size, size);
            var img = icon.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
        Rows[avatar.Pointer] = (row, key);
    }

    static Sprite? FaceSprite(SpecialistInfo specialist)
    {
        foreach (var (_, sprite) in Candidates(specialist))
            if (sprite != null)
                return sprite;
        return null;
    }

    // Every way the game might name a specialist's picture, in order of preference.
    static IEnumerable<(string Label, Sprite? Sprite)> Candidates(SpecialistInfo s)
    {
        var manager = GRSingletonMonoStatic<GRUIManager>.Instance?.GetSpriteManager();
        if (!ArmySnapshotReader.Usable(manager))
            yield break;
        yield return ($"officer-0_{s.IconName}", Get(() => manager!.GetSprite(s.IconName, SpriteType.Officer, 0)));
        yield return ($"plain_{s.IconName}", Get(() => manager!.GetSprite(s.IconName)));
        yield return ($"plain_O_{s.IconName}", Get(() => manager!.GetSprite("O_" + s.IconName)));
        yield return ($"officer-1_{s.IconName}", Get(() => manager!.GetSprite(s.IconName, SpriteType.Officer, 1)));
        yield return ($"plain_{s.PicName}", Get(() => manager!.GetSprite(s.PicName)));
    }

    static Sprite? Get(Func<Sprite> read)
    {
        try
        {
            var sprite = read();
            return Alive(sprite) ? sprite : null;
        }
        catch
        {
            return null;
        }
    }

    // Diagnostics: save every candidate picture once, to pick the one that is just the face.
    static void DumpCandidates(SpecialistInfo specialist)
    {
        if (!MelperScoutMod.DumpSprites || !Dumped.Add(specialist.IconName))
            return;
        string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "MelperScout", "sprites");
        Directory.CreateDirectory(dir);
        foreach (var (label, sprite) in Candidates(specialist))
        {
            if (sprite == null)
            {
                MelperScoutMod.Log.Msg($"sprite {label}: none");
                continue;
            }
            try
            {
                var r = sprite.textureRect;
                string file = Path.Combine(dir, $"{specialist.Id}_{label}.png");
                File.WriteAllBytes(file, ToPng(sprite));
                MelperScoutMod.Log.Msg($"sprite {label}: '{sprite.name}' {r.width}x{r.height} -> {file}");
            }
            catch (Exception e)
            {
                MelperScoutMod.Log.Msg($"sprite {label}: '{sprite.name}' not saved: {e.Message}");
            }
        }
    }

    static byte[] ToPng(Sprite sprite)
    {
        var texture = sprite.texture;
        var r = sprite.textureRect;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
            copy.Apply();
            byte[] png = ImageConversion.EncodeToPNG(copy);
            Object.Destroy(copy);
            return png;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type> Types<T>()
    {
        var types = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(1);
        types[0] = Il2CppType.Of<T>();
        return types;
    }

    static bool Alive(Object? obj) => obj != null && !obj.WasCollected && obj;
}
