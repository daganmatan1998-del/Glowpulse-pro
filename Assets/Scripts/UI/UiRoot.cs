using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// The canvas everything else hangs off, plus the event system that makes it
    /// clickable.
    ///
    /// Built at runtime like the rest of the game. Screens are created once and
    /// then shown and hidden rather than rebuilt, because rebuilding a menu every
    /// time it opens allocates on a frame the player is already waiting on.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class UiRoot : MonoBehaviour
    {
        private static UiRoot _instance;

        public static UiRoot Instance => _instance;

        public Canvas Canvas { get; private set; }

        /// <summary>Layer for the always-on HUD. Sits under every menu.</summary>
        public RectTransform HudLayer { get; private set; }

        /// <summary>Layer for full-screen menus.</summary>
        public RectTransform MenuLayer { get; private set; }

        /// <summary>Layer for transitions and title cards, above everything.</summary>
        public RectTransform OverlayLayer { get; private set; }

        public static UiRoot Install(GameObject host)
        {
            if (_instance != null) return _instance;

            var go = new GameObject("~UI");
            go.transform.SetParent(host.transform, false);
            _instance = go.AddComponent<UiRoot>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // A high sorting order keeps the UI above anything a world-space
            // effect might draw.
            Canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Matching on height keeps text the same size on an ultrawide as on
            // 16:9, rather than shrinking everything to fit the extra width.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            gameObject.AddComponent<GraphicRaycaster>();

            HudLayer = UiKit.Stretch("HUD", transform);
            MenuLayer = UiKit.Stretch("Menus", transform);
            OverlayLayer = UiKit.Stretch("Overlay", transform);

            EnsureEventSystem();
        }

        /// <summary>
        /// A scene assembled from code has no EventSystem, and without one no
        /// button in the game responds to a click - a failure that looks like the
        /// UI being dead rather than like a missing component.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("~EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
