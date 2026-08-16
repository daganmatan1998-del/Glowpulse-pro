using System;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// The project's look, and the handful of widgets built from it.
    ///
    /// The rest of the game is assembled in code rather than stored in scene
    /// assets, and the UI follows the same rule: a menu is a method, reviewable in
    /// a diff, and it cannot break by losing a prefab reference. The palette lives
    /// here so a restyle is one file rather than a hunt through every screen.
    /// </summary>
    public static class UiKit
    {
        // The game's existing colours: warm bone on near-black, with the cyan and
        // amber the combat effects already use as the two accents.
        public static readonly Color Ink = new Color(0.043f, 0.047f, 0.055f, 1f);
        public static readonly Color Bone = new Color(0.929f, 0.902f, 0.855f, 1f);
        public static readonly Color Muted = new Color(0.929f, 0.902f, 0.855f, 0.55f);
        public static readonly Color Line = new Color(0.929f, 0.902f, 0.855f, 0.18f);
        public static readonly Color Cyan = new Color(0.275f, 0.761f, 0.784f, 1f);
        public static readonly Color Sun = new Color(1f, 0.824f, 0.631f, 1f);
        public static readonly Color Danger = new Color(0.847f, 0.337f, 0.247f, 1f);
        public static readonly Color PanelFill = new Color(0.055f, 0.063f, 0.075f, 0.94f);
        public static readonly Color Veil = new Color(0.024f, 0.027f, 0.031f, 0.86f);

        public const int TitleSize = 46;
        public const int HeadingSize = 22;
        public const int BodySize = 17;
        public const int SmallSize = 14;

        private static Font _font;

        /// <summary>
        /// The built-in font. Requesting it by name works in the editor but not in
        /// a player build, so the legacy dynamic font is used instead - it is the
        /// one font guaranteed to exist everywhere.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
#if UNITY_2022_1_OR_NEWER
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                return _font;
            }
        }

        // ---- construction --------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        /// <summary>A rect that fills its parent, optionally inset on every side.</summary>
        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Stretch(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Stretch(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A row that lays its children out left to right.</summary>
        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing = 10f,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = align;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            return group;
        }

        public static VerticalLayoutGroup Column(RectTransform rect, float spacing = 8f,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = align;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            return group;
        }

        public static LayoutElement Size(GameObject go, float width = -1f, float height = -1f,
            float flexibleWidth = -1f)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = width;
            if (height >= 0f) element.preferredHeight = height;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>A framed button with the project's hover and press tints.</summary>
        public static Button Button(string label, Transform parent, Action onClick,
            bool accent = false)
        {
            RectTransform rect = Rect("Button " + label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = accent ? Sun : new Color(1f, 1f, 1f, 0.06f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Colors(accent);

            Text text = Label("Text", rect, label, BodySize,
                accent ? Ink : Bone, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;

            if (onClick != null) button.onClick.AddListener(() => onClick());

            Size(rect.gameObject, height: 40f);
            return button;
        }

        private static ColorBlock Colors(bool accent)
        {
            ColorBlock c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = accent ? new Color(1f, 0.94f, 0.85f) : new Color(1f, 1f, 1f, 1.6f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            c.selectedColor = c.highlightedColor;
            c.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            c.fadeDuration = 0.08f;
            return c;
        }

        /// <summary>A labelled slider whose value is reported as it is dragged.</summary>
        public static Slider Slider(string label, Transform parent, float min, float max,
            float value, Action<float> onChanged, out Text readout)
        {
            RectTransform row = Rect("Row " + label, parent);
            Row(row, 12f);
            Size(row.gameObject, height: 34f);

            Text name = Label("Label", row, label, BodySize, Bone);
            Size(name.gameObject, width: 210f);

            RectTransform track = Rect("Slider", row);
            Size(track.gameObject, height: 14f, flexibleWidth: 1f);

            var background = track.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            RectTransform fillArea = Stretch("Fill Area", track);
            RectTransform fill = Stretch("Fill", fillArea);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Cyan;

            RectTransform handleArea = Stretch("Handle Area", track);
            RectTransform handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(14f, 22f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Bone;

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(value, min, max);

            readout = Label("Value", row, string.Empty, SmallSize, Muted, TextAnchor.MiddleRight);
            Size(readout.gameObject, width: 62f);

            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        /// <summary>A one-pixel rule, for separating sections without a heavy box.</summary>
        public static Image Divider(Transform parent)
        {
            RectTransform rect = Rect("Divider", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Line;
            Size(rect.gameObject, height: 1f);
            return image;
        }

        public static Text Heading(string text, Transform parent)
        {
            Text label = Label("Heading " + text, parent, text.ToUpperInvariant(), HeadingSize, Sun);
            label.fontStyle = FontStyle.Bold;
            Size(label.gameObject, height: 30f);
            return label;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _font = null;
    }
}
