using System.Collections;
using Glowpulse.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// The card that names a stage as it begins: fade in, hold, fade out.
    ///
    /// It runs on unscaled time so it plays at the same speed whether the game is
    /// paused behind it or running, and it never blocks input - the player can be
    /// moving before it has finished clearing. A title card that takes the game
    /// away for three seconds is a loading screen with ambitions.
    /// </summary>
    public sealed class StageTitleCard : MonoBehaviour
    {
        private const float FadeIn = 0.55f;
        private const float Hold = 1.7f;
        private const float FadeOut = 0.7f;

        private CanvasGroup _group;
        private Text _number;
        private Text _name;
        private Text _tagline;
        private Image _rule;
        private Coroutine _playing;

        public static StageTitleCard Create(Transform parent)
        {
            var go = new GameObject("Stage Title Card");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<StageTitleCard>();
            card.Build();
            return card;
        }

        private void Build()
        {
            RectTransform root = UiKit.Stretch("Root", transform);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Never eats a click: the fight carries on underneath.
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // A band rather than a full blackout, so the arena stays visible.
            RectTransform band = UiKit.Rect("Band", root);
            band.anchorMin = new Vector2(0f, 0.42f);
            band.anchorMax = new Vector2(1f, 0.72f);
            band.offsetMin = band.offsetMax = Vector2.zero;

            var fill = band.gameObject.AddComponent<Image>();
            fill.color = new Color(0.024f, 0.027f, 0.031f, 0.72f);
            fill.raycastTarget = false;

            RectTransform content = UiKit.Stretch("Content", band, 24f);
            UiKit.Column(content, 4f, TextAnchor.MiddleCenter);

            _number = UiKit.Label("Number", content, "STAGE 1", 30, UiKit.Sun,
                TextAnchor.MiddleCenter);
            _number.fontStyle = FontStyle.Bold;
            UiKit.Size(_number.gameObject, height: 38f);

            _name = UiKit.Label("Name", content, string.Empty, 58, UiKit.Bone,
                TextAnchor.MiddleCenter);
            _name.fontStyle = FontStyle.Bold;
            UiKit.Size(_name.gameObject, height: 70f);

            _rule = UiKit.Divider(content);
            UiKit.Size(_rule.gameObject, width: 340f, height: 2f);

            _tagline = UiKit.Label("Tagline", content, string.Empty, UiKit.BodySize, UiKit.Muted,
                TextAnchor.MiddleCenter);
            UiKit.Size(_tagline.gameObject, height: 28f);
        }

        public void Play(StageDefinition stage)
        {
            if (stage == null) return;

            _number.text = stage.DisplayNumber;
            _name.text = stage.Name;
            _tagline.text = stage.Tagline;

            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            yield return Fade(0f, 1f, FadeIn);

            float held = 0f;
            while (held < Hold)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(1f, 0f, FadeOut);
            _playing = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;

                // Smoothstep rather than linear: a linear fade reads as a light
                // switch, which is the opposite of cinematic.
                float u = Mathf.Clamp01(t / duration);
                _group.alpha = Mathf.Lerp(from, to, u * u * (3f - 2f * u));
                yield return null;
            }

            _group.alpha = to;
        }
    }
}
