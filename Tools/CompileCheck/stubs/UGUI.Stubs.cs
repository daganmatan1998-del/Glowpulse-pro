// Compile-check stubs for com.unity.ugui (UnityEngine.UI / UnityEngine.EventSystems).
// These declare only the surface the Glowpulse project actually uses, with the
// same signatures as the real package. Unity always compiles against the real
// assembly; this file exists so the project can be type-checked without Unity.
#pragma warning disable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour { }

    public class BaseInputModule : UIBehaviour { }

    public class PointerInputModule : BaseInputModule { }

    public class StandaloneInputModule : PointerInputModule
    {
        public string horizontalAxis { get; set; }
        public string verticalAxis { get; set; }
        public string submitButton { get; set; }
        public string cancelButton { get; set; }
    }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; }
        public GameObject firstSelectedGameObject { get; set; }
        public void SetSelectedGameObject(GameObject go) { }
        public GameObject currentSelectedGameObject { get; }
    }

    public class BaseEventData { }
    public class PointerEventData : BaseEventData { }

    public interface IEventSystemHandler { }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData e); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData e); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData e); }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;

    public abstract class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; }
        public RectTransform rectTransform { get; }
        public bool raycastTarget { get; set; }
        public Material material { get; set; }
        public Canvas canvas { get; }
        public virtual void SetAllDirty() { }
        public virtual void SetVerticesDirty() { }
    }

    public abstract class MaskableGraphic : Graphic
    {
        public bool maskable { get; set; }
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public Sprite sprite { get; set; }
        public Sprite overrideSprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public bool fillCenter { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public bool fillClockwise { get; set; }
        public int fillOrigin { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool alignByGeometry { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
        public float lineSpacing { get; set; }
        public bool supportRichText { get; set; }
        public TextGenerationSettings GetGenerationSettings(Vector2 extents) => default;
        public TextGenerator cachedTextGenerator { get; }
    }

    public class Shadow : UIBehaviour
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
        public bool useGraphicAlpha { get; set; }
    }

    public class Outline : Shadow { }

    public class Selectable : UIBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public Transition transition { get; set; }
        public ColorBlock colors { get; set; }
        public Graphic targetGraphic { get; set; }
        public bool interactable { get; set; }
        public Navigation navigation { get; set; }
        public void Select() { }
    }

    [Serializable]
    public struct ColorBlock
    {
        public Color normalColor;
        public Color highlightedColor;
        public Color pressedColor;
        public Color selectedColor;
        public Color disabledColor;
        public float colorMultiplier;
        public float fadeDuration;
        public static ColorBlock defaultColorBlock { get; }
    }

    [Serializable]
    public struct Navigation
    {
        public enum Mode { None, Horizontal, Vertical, Automatic, Explicit }
        public Mode mode;
        public Selectable selectOnUp;
        public Selectable selectOnDown;
        public Selectable selectOnLeft;
        public Selectable selectOnRight;
    }

    public class Button : Selectable
    {
        [Serializable] public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; }
    }

    public class Slider : Selectable
    {
        [Serializable] public class SliderEvent : UnityEvent<float> { }
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        public void SetValueWithoutNotify(float input) { }
        public float value { get; set; }
        public float normalizedValue { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public bool wholeNumbers { get; set; }
        public Direction direction { get; set; }
        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public SliderEvent onValueChanged { get; set; }
    }

    public class Toggle : Selectable
    {
        [Serializable] public class ToggleEvent : UnityEvent<bool> { }
        public void SetIsOnWithoutNotify(bool value) { }
        public bool isOn { get; set; }
        public Graphic graphic { get; set; }
        public ToggleEvent onValueChanged { get; set; }
    }

    public class Dropdown : Selectable
    {
        [Serializable] public class DropdownEvent : UnityEvent<int> { }
        [Serializable] public class OptionData { public string text; public Sprite image; public OptionData() { } public OptionData(string t) { text = t; } }
        public List<OptionData> options { get; set; }
        public int value { get; set; }
        public Text captionText { get; set; }
        public Text itemText { get; set; }
        public RectTransform template { get; set; }
        public DropdownEvent onValueChanged { get; set; }
        public void AddOptions(List<string> opts) { }
        public void ClearOptions() { }
        public void RefreshShownValue() { }
    }

    public class ScrollRect : UIBehaviour
    {
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public float scrollSensitivity { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
    }

    public class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : UIBehaviour { }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public float referencePixelsPerUnit { get; set; }
    }

    public class GraphicRaycaster : UIBehaviour
    {
        public bool ignoreReversedGraphics { get; set; }
    }

    public class LayoutElement : UIBehaviour
    {
        public float minWidth { get; set; }
        public float minHeight { get; set; }
        public float preferredWidth { get; set; }
        public float preferredHeight { get; set; }
        public float flexibleWidth { get; set; }
        public float flexibleHeight { get; set; }
        public bool ignoreLayout { get; set; }
    }

    public abstract class LayoutGroup : UIBehaviour
    {
        public RectOffset padding { get; set; }
        public TextAnchor childAlignment { get; set; }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool reverseArrangement { get; set; }
    }

    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class GridLayoutGroup : LayoutGroup
    {
        public Vector2 cellSize { get; set; }
        public Vector2 spacing { get; set; }
        public int constraintCount { get; set; }
    }

    public class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }

    public class AspectRatioFitter : UIBehaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode { get; set; }
        public float aspectRatio { get; set; }
    }

    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform rect) { }
        public static void MarkLayoutForRebuild(RectTransform rect) { }
    }
}
