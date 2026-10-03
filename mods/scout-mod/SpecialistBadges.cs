using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MelperScout;

// Small specialist portraits beside each player's avatar in the game's own top panel: on the side where
// the name and HP bar are, top edge right under the name plate, side edge against the avatar.
// The game itself shows each player's specialist count there, so this is public information.
static class SpecialistBadges
{
    const string BadgeName = "MelperScout.Specialists";
    const float SizeOfAvatar = 0.42f;
    const float Gap = 2f;
    // Clearance from the avatar and from the name plate, as a share of a portrait's size.
    const float Clearance = 1f / 3f;

    // Avatar image pointer -> the badge row we attached to it and the specialists it shows.
    static readonly Dictionary<IntPtr, (GameObject Row, string Key)> Rows = new();

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
            var slider = Alive(panel.lifeSlider) ? panel.lifeSlider.GetComponent<RectTransform>() : null;
            var name = Alive(panel.nameText) ? panel.nameText.rectTransform : null;
            Attach(image, slider, name, team?.Specialists ?? new List<SpecialistInfo>());
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

    static void Attach(GRImage avatar, RectTransform? hpBar, RectTransform? nameText, List<SpecialistInfo> specialists)
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
        var box = avatarRect.rect;
        // In the avatar's own coordinates: the HP bar says which side to use, and the row starts
        // below whichever is lower, the HP bar or the name plate under it.
        bool toRight = true;
        float top = box.yMax;
        if (hpBar != null)
        {
            var (minX, maxX, minY) = Bounds(avatarRect, hpBar);
            toRight = (minX + maxX) / 2f >= box.center.x;
            top = minY;
        }
        if (nameText != null)
            top = Math.Min(top, Bounds(avatarRect, nameText).MinY);
        float room = top - box.yMin;
        float size = Math.Max(16f, Math.Min(box.height * SizeOfAvatar, room > 16f ? room : float.MaxValue));
        MelperScoutMod.Log.Msg($"specialists by avatar: {(toRight ? "right" : "left")} side, top {top - box.yMax:F0} from avatar top, size {size:F0}");

        var row = new GameObject(BadgeName, Types<RectTransform>());
        row.transform.SetParent(avatarRect, false);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(toRight ? 0f : 1f, 1f);
        rowRect.sizeDelta = new Vector2(specialists.Count * (size + Gap) - Gap, size);
        // Anchored to the avatar's pivot-relative coordinates via localPosition, then pushed out from the
        // avatar and down from the name plate by a third of a portrait.
        float clearance = size * Clearance;
        rowRect.localPosition = new Vector3(toRight ? box.xMax + clearance : box.xMin - clearance, top - clearance, 0f);

        for (int i = 0; i < specialists.Count; i++)
        {
            var specialist = specialists[i];
            var sprite = FaceSprite(specialist);
            if (sprite == null)
                continue;
            var icon = new GameObject("Face", Types<RectTransform>());
            icon.transform.SetParent(rowRect, false);
            var rect = icon.GetComponent<RectTransform>();
            // Fill outward from the avatar: left to right on its right side, right to left on its left side.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(toRight ? 0f : 1f, 1f);
            rect.anchoredPosition = new Vector2((toRight ? 1f : -1f) * i * (size + Gap), 0f);
            rect.sizeDelta = new Vector2(size, size);
            var img = icon.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
        Rows[avatar.Pointer] = (row, key);
    }

    static (float MinX, float MaxX, float MinY) Bounds(RectTransform space, RectTransform target)
    {
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        target.GetWorldCorners(corners);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue;
        for (int c = 0; c < 4; c++)
        {
            var local = space.InverseTransformPoint(corners[c]);
            minX = Math.Min(minX, local.x);
            maxX = Math.Max(maxX, local.x);
            minY = Math.Min(minY, local.y);
        }
        return (minX, maxX, minY);
    }

    static Sprite? FaceSprite(SpecialistInfo specialist)
    {
        foreach (var (_, sprite) in Candidates(specialist))
            if (sprite != null)
                return sprite;
        return null;
    }

    // Ways the game names a specialist's picture; the first is the face-only 128x128 square.
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

    static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type> Types<T>()
    {
        var types = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(1);
        types[0] = Il2CppType.Of<T>();
        return types;
    }

    static bool Alive(Object? obj) => obj != null && !obj.WasCollected && obj;
}
