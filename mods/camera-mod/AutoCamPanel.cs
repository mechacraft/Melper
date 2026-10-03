using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MelperCamera;

// F6: a panel sliding in from the right edge to change AutoCamSettings live.
// Built with uGUI like MelperScout's panel (IMGUI texture drawing breaks under Il2CppInterop). Buttons are
// hit-tested here by hand, so no Il2Cpp delegates are needed; the panel's raycast-target background makes
// the game's UI treat the mouse as over UI, so clicks on it don't reach the battlefield.
static class AutoCamPanel
{
    const float Width = 430f;
    const float Pad = 10f;
    const float RowHeight = 28f;
    const float LabelWidth = 180f;
    const float ButtonSize = 26f;
    const float ValueWidth = 160f;
    const float SlideSeconds = 0.18f;
    const float RepeatDelay = 0.4f;
    const float RepeatEvery = 0.07f;
    const float SaveDelay = 1.5f;

    sealed class Hit
    {
        public RectTransform Rect = null!;
        public Image Image = null!;
        public Action Click = () => { };
        public bool Repeats;
    }

    static readonly List<Hit> Hits = new();
    static readonly List<(AutoCamSettings.Setting Setting, Text Text)> Values = new();

    static GameObject? _root;
    static RectTransform? _panel;
    static Text? _live;
    static Text? _toggleText;
    static Font? _font;
    static float _height;
    static bool _open;
    static float _shown; // 0 = hidden off screen, 1 = fully in
    static Hit? _held;
    static float _nextRepeat;
    static float _saveAt = -1f;

    static readonly Color ButtonColor = new(1f, 1f, 1f, 0.12f);
    static readonly Color ButtonHover = new(1f, 1f, 1f, 0.25f);
    static readonly Color ButtonDown = new(1f, 0.85f, 0.45f, 0.45f);

    internal static void Toggle()
    {
        _open = !_open;
        if (!_open)
            SaveNow();
    }

    internal static void Update()
    {
        try
        {
            if (!_open && _shown <= 0f)
            {
                if (Alive(_root) && _root!.activeSelf)
                    _root.SetActive(false);
                return;
            }
            if (!EnsureCreated())
                return;
            _root!.SetActive(true);

            float dt = Time.unscaledDeltaTime / SlideSeconds;
            _shown = Mathf.Clamp01(_shown + (_open ? dt : -dt));
            float eased = 1f - (1f - _shown) * (1f - _shown);
            _panel!.anchoredPosition = new Vector2(Mathf.Lerp(Width + 20f, -12f, eased), -120f);

            if (_open)
                HandleMouse();
            Refresh();

            if (_saveAt > 0f && Time.unscaledTime >= _saveAt)
                SaveNow();
        }
        catch (Exception e)
        {
            MelperCameraMod.Log.Warning($"auto cam panel failed: {e}");
            _open = false;
            _shown = 0f;
        }
    }

    static void SaveNow()
    {
        _saveAt = -1f;
        AutoCamSettings.Save();
    }

    static void HandleMouse()
    {
        Vector2 mouse = Input.mousePosition;
        Hit? over = null;
        foreach (var hit in Hits)
        {
            if (Alive(hit.Rect) && RectTransformUtility.RectangleContainsScreenPoint(hit.Rect, mouse, null))
            {
                over = hit;
                break;
            }
        }

        if (Input.GetMouseButtonDown(0) && over != null)
        {
            _held = over;
            _nextRepeat = Time.unscaledTime + RepeatDelay;
            over.Click();
        }
        else if (Input.GetMouseButton(0) && _held != null && _held == over && _held.Repeats && Time.unscaledTime >= _nextRepeat)
        {
            _nextRepeat = Time.unscaledTime + RepeatEvery;
            _held.Click();
        }
        if (!Input.GetMouseButton(0))
            _held = null;

        foreach (var hit in Hits)
            hit.Image.color = hit == _held && hit == over ? ButtonDown : hit == over ? ButtonHover : ButtonColor;
    }

    static void Change(AutoCamSettings.Setting setting, int direction)
    {
        if (setting.Choices != null)
        {
            int count = setting.Choices.Length;
            setting.Value = ((int)Math.Round(setting.Value) + direction + count) % count;
        }
        else
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            setting.Value += direction * setting.Step * (shift ? 5f : 1f);
        }
        _saveAt = Time.unscaledTime + SaveDelay;
    }

    static void Refresh()
    {
        foreach (var (setting, text) in Values)
        {
            string value = setting.Text;
            if (Math.Abs(setting.Value - setting.Default) > setting.Step * 0.01f)
                value += " •";
            if (text.text != value)
                text.text = value;
        }

        if (_toggleText != null)
        {
            string toggle = AutoCamPatches.Enabled ? "Правки: вкл" : "Правки: выкл (игра)";
            if (_toggleText.text != toggle)
                _toggleText.text = toggle;
        }

        if (_live != null)
        {
            var p = AutoCamPatches.ViewProbe.LastAt >= 0f && Time.unscaledTime - AutoCamPatches.ViewProbe.LastAt < 1f
                ? $"Сейчас: наклон {AutoCamPatches.ViewProbe.LastPitch:0.0}°, курс {AutoCamPatches.ViewProbe.LastHeading:0}°, " +
                  $"до юнитов {AutoCamPatches.ViewProbe.LastDistance:0}"
                : "Сейчас: auto-камера не активна";
            if (_live.text != p)
                _live.text = p;
        }
    }

    static bool EnsureCreated()
    {
        if (Alive(_root) && Alive(_panel))
            return true;
        _font ??= FindGameFont();
        if (_font == null)
            return false;
        Hits.Clear();
        Values.Clear();

        _root = NewObject("MelperCamera.Panel", null);
        Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31980;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();

        var settings = AutoCamSettings.All;
        _height = Pad * 2 + RowHeight * (settings.Length + 4);

        _panel = NewObject("Panel", _root.transform).GetComponent<RectTransform>();
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(1f, 1f);
        _panel.sizeDelta = new Vector2(Width, _height);
        var background = _panel.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.8f);
        background.raycastTarget = true;

        float y = Pad;
        AddText(_panel, "Auto-камера   (F6 — закрыть, Shift — шаг ×5)", 14, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleLeft, Pad, y, Width - Pad * 2, RowHeight, bold: true);
        y += RowHeight;

        foreach (var setting in settings)
        {
            var s = setting;
            AddText(_panel, s.Label, 14, Color.white, TextAnchor.MiddleLeft, Pad, y, LabelWidth, RowHeight);
            float x = Pad + LabelWidth;
            AddButton(_panel, "−", x, y + (RowHeight - ButtonSize) / 2, ButtonSize, ButtonSize, () => Change(s, -1), repeats: s.Choices == null);
            x += ButtonSize + 4f;
            var value = AddText(_panel, "", 14, Color.white, TextAnchor.MiddleCenter, x, y, ValueWidth, RowHeight).GetComponent<Text>();
            Values.Add((s, value));
            x += ValueWidth + 4f;
            AddButton(_panel, "+", x, y + (RowHeight - ButtonSize) / 2, ButtonSize, ButtonSize, () => Change(s, +1), repeats: s.Choices == null);
            y += RowHeight;
        }

        y += 6f;
        _live = AddText(_panel, "", 13, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleLeft, Pad, y, Width - Pad * 2, RowHeight).GetComponent<Text>();
        y += RowHeight + 4f;

        float half = (Width - Pad * 3) / 2;
        AddButton(_panel, "Сбросить (•)", Pad, y, half, ButtonSize, AutoCamSettings.ResetToDefaults, repeats: false);
        _toggleText = AddButton(_panel, "", Pad * 2 + half, y, half, ButtonSize, AutoCamPatches.Toggle, repeats: false);
        return true;
    }

    static Text AddButton(RectTransform parent, string label, float x, float y, float width, float height, Action click, bool repeats)
    {
        var go = NewObject("Button", parent);
        var rect = go.GetComponent<RectTransform>();
        Place(rect, x, y, width, height);
        var image = go.AddComponent<Image>();
        image.color = ButtonColor;
        image.raycastTarget = true;
        Hits.Add(new Hit { Rect = rect, Image = image, Click = click, Repeats = repeats });
        var text = AddText(rect, label, 15, Color.white, TextAnchor.MiddleCenter, 0f, 0f, width, height, bold: true);
        return text.GetComponent<Text>();
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
        return rect;
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
                if (Alive(font) && !text!.transform.root.name.StartsWith("Melper", StringComparison.Ordinal))
                    return font;
            }
        }
        catch (Exception e)
        {
            MelperCameraMod.Log.Warning($"game font lookup failed: {e.Message}");
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static bool Alive(Object? obj) => obj != null && !obj.WasCollected && obj;
}
