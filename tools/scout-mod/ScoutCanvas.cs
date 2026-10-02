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

// uGUI panel in the top right corner with both armies: one column per team, opponent on the right.
// Specialists are drawn by SpecialistBadges next to the players' avatars instead.
// Built with the game's own UI system (like BattleSuite) rather than IMGUI, whose texture drawing
// throws ObjectCollectedException under Il2CppInterop.
static class ScoutCanvas
{
    const float ColumnWidth = 236f;
    const float Padding = 8f;
    const float HeaderHeight = 22f;
    const float LineHeight = 20f;
    const float UnitSize = 38f;
    const float UnitCell = 46f;
    const float UnitLabelHeight = 16f;
    const int UnitsPerRow = 5;

    static readonly Dictionary<string, Sprite?> Sprites = new(StringComparer.Ordinal);
    static readonly HashSet<string> ReportedMissing = new(StringComparer.Ordinal);
    static readonly List<(RectTransform Rect, string Text)> HoverTargets = new();

    static GameObject? _root;
    static RectTransform? _rootRect;
    static Canvas? _canvas;
    static GameObject? _panel;
    static GameObject? _tooltip;
    static Text? _tooltipText;
    static Font? _font;
    static int _builtVersion = -1;
    static float _builtScale;

    internal static void ClearSprites() => Sprites.Clear();

    internal static void Hide()
    {
        if (Alive(_root))
            _root!.SetActive(false);
    }

    internal static void Show(MatchSnapshot snapshot, int version)
    {
        if (!EnsureCreated())
            return;
        _root!.SetActive(true);
        float scale = MelperScoutMod.Scale;
        if (version != _builtVersion || scale != _builtScale || !Alive(_panel))
        {
            Build(snapshot, scale);
            _builtVersion = version;
            _builtScale = scale;
        }
        UpdateTooltip();
    }

    static bool EnsureCreated()
    {
        if (Alive(_root) && Alive(_canvas))
            return true;
        _font ??= FindGameFont();
        if (_font == null)
            return false;

        _root = NewObject("MelperScout.Canvas", null);
        Object.DontDestroyOnLoad(_root);
        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 31990; // just under BattleSuite's 32000
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _rootRect = _root.GetComponent<RectTransform>();
        _panel = null;
        _tooltip = null;
        _builtVersion = -1;
        return true;
    }

    static void Build(MatchSnapshot snapshot, float s)
    {
        if (Alive(_panel))
            Object.Destroy(_panel);
        HoverTargets.Clear();

        var teams = snapshot.Teams
            .Where(t => MelperScoutMod.ShowOwnTeam || !t.IsLocal)
            .OrderBy(t => t.IsLocal ? 0 : 1)
            .ThenBy(t => t.TeamIndex)
            .ToList();
        if (teams.Count == 0)
        {
            _panel = null;
            return;
        }

        float width = ColumnWidth * teams.Count + Padding * (teams.Count + 1);
        float height = Padding * 2 + LineHeight + teams.Max(ColumnHeight);

        _panel = NewObject("Panel", _rootRect);
        var panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-MelperScoutMod.OffsetRight, -MelperScoutMod.OffsetTop);
        panelRect.sizeDelta = new Vector2(width, height);
        panelRect.localScale = new Vector3(s, s, 1f);
        var background = _panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);
        background.raycastTarget = false;

        string title = $"Армии на начало боя {snapshot.Round} раунда   (F6 — скрыть)";
        AddText(panelRect, title, 13, Color.white, TextAnchor.MiddleLeft, Padding, Padding * 0.5f, width - Padding * 2, LineHeight);

        float x = Padding;
        foreach (var team in teams)
        {
            BuildTeam(panelRect, team, x, Padding + LineHeight);
            x += ColumnWidth + Padding;
        }
    }

    static float ColumnHeight(TeamSnapshot team)
    {
        int unitRows = Math.Max(1, (team.Units.Count + UnitsPerRow - 1) / UnitsPerRow);
        return HeaderHeight + LineHeight * 2 + unitRows * (UnitSize + UnitLabelHeight);
    }

    static void BuildTeam(RectTransform parent, TeamSnapshot team, float x, float y)
    {
        string who = team.IsLocal ? "Вы" : "Соперник";
        AddText(parent, $"{who} (команда {team.TeamIndex + 1})", 15, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleLeft, x, y, ColumnWidth, HeaderHeight, bold: true);
        y += HeaderHeight;

        string value = team.ValueComplete ? team.Value.ToString("N0") : $"≈{team.Value:N0}";
        AddText(parent, $"Армия: {value}", 13, Color.white, TextAnchor.MiddleLeft, x, y, ColumnWidth, LineHeight);
        y += LineHeight;
        AddText(parent, $"Здоровье: {team.MaxHealth:N0}", 13, Color.white, TextAnchor.MiddleLeft, x, y, ColumnWidth, LineHeight);
        y += LineHeight;

        for (int i = 0; i < team.Units.Count; i++)
        {
            var unit = team.Units[i];
            float ux = x + (i % UnitsPerRow) * UnitCell;
            float uy = y + (i / UnitsPerRow) * (UnitSize + UnitLabelHeight);
            var rect = AddIcon(parent, unit.IconCandidates, unit.Name, ux, uy, UnitSize);
            AddText(parent, $"×{unit.Cards} ур.{unit.MaxLevel}", 11, Color.white, TextAnchor.MiddleCenter, ux - 4f, uy + UnitSize, UnitCell, UnitLabelHeight);
            HoverTargets.Add((rect, $"{unit.Name}: {unit.Cards} отр., {unit.Mechs} шт., макс. уровень {unit.MaxLevel}"));
        }
    }

    // An icon, or the name as text when the game has no sprite for it.
    static RectTransform AddIcon(RectTransform parent, string[] candidates, string fallbackName, float x, float y, float size)
    {
        var sprite = candidates.Select(Resolve).FirstOrDefault(sp => sp != null);
        if (sprite == null)
            return AddText(parent, Short(fallbackName), 11, Color.white, TextAnchor.MiddleCenter, x, y, size, size);
        var go = NewObject("Icon", parent);
        var rect = go.GetComponent<RectTransform>();
        Place(rect, x, y, size, size);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    static RectTransform AddText(RectTransform parent, string text, int size, Color color, TextAnchor anchor, float x, float y, float width, float height, bool bold = false)
    {
        var go = NewObject("Text", parent);
        var rect = go.GetComponent<RectTransform>();
        Place(rect, x, y, width, height);
        var label = go.AddComponent<Text>();
        label.font = _font;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.color = color;
        label.alignment = anchor;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = text;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        return rect;
    }

    static void UpdateTooltip()
    {
        Vector2 mouse = Input.mousePosition;
        string? hover = null;
        foreach (var (rect, text) in HoverTargets)
        {
            if (Alive(rect) && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse, null))
            {
                hover = text;
                break;
            }
        }

        if (hover == null)
        {
            if (Alive(_tooltip))
                _tooltip!.SetActive(false);
            return;
        }

        if (!Alive(_tooltip))
        {
            _tooltip = NewObject("Tooltip", _rootRect);
            var background = _tooltip.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.9f);
            background.raycastTarget = false;
            var tooltipRect = _tooltip.GetComponent<RectTransform>();
            tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0f, 0f);
            tooltipRect.pivot = new Vector2(1f, 1f);
            var textRect = AddText(tooltipRect, "", 13, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 10f, 10f);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            _tooltipText = textRect.GetComponent<Text>();
        }

        _tooltip!.SetActive(true);
        _tooltip.transform.SetAsLastSibling();
        _tooltipText!.text = hover;
        float scaleFactor = _canvas!.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        var rt = _tooltip.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(_tooltipText.preferredWidth + 14f, 24f);
        rt.anchoredPosition = mouse / scaleFactor + new Vector2(-8f, -14f);
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static GameObject NewObject(string name, Transform? parent)
    {
        var types = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(1);
        types[0] = Il2CppType.Of<RectTransform>();
        var go = new GameObject(name, types);
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    // Borrow the font of the game's own labels, so Cyrillic renders the way the game renders it.
    static Font? FindGameFont()
    {
        try
        {
            var texts = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Text>());
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i]?.TryCast<Text>();
                var font = Alive(text) ? text!.font : null;
                if (Alive(font) && !text!.transform.root.name.StartsWith("MelperScout", StringComparison.Ordinal))
                {
                    MelperScoutMod.Log.Msg($"font: {font!.name}");
                    return font;
                }
            }
        }
        catch (Exception e)
        {
            MelperScoutMod.Log.Warning($"game font lookup failed: {e.Message}");
        }
        var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        MelperScoutMod.Log.Msg($"font: builtin {(builtin != null ? builtin.name : "none")}");
        return builtin;
    }

    static Sprite? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        if (Sprites.TryGetValue(name, out var cached) && (cached == null || Alive(cached)))
            return cached;

        var manager = GRSingletonMonoStatic<GRUIManager>.Instance?.GetSpriteManager();
        if (!ArmySnapshotReader.Usable(manager))
            return null; // not cached: the manager may just not be ready yet
        Sprite? sprite = null;
        try
        {
            sprite = manager!.GetSprite(name);
        }
        catch
        {
        }
        if (!Alive(sprite))
            sprite = null;
        if (sprite == null && ReportedMissing.Add(name))
            MelperScoutMod.Log.Msg($"no sprite named '{name}'");
        Sprites[name] = sprite;
        return sprite;
    }

    static string Short(string name) => name.Length <= 8 ? name : name.Substring(0, 7) + "…";

    static bool Alive(Object? obj) => obj != null && !obj.WasCollected && obj;
}
