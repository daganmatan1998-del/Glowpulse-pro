using Glowpulse.Combat;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.Core.Bootstrap
{
    /// <summary>
    /// Builds the parts every character shares - collider, motor, health,
    /// stamina, rig and animator - and hands back an inactive GameObject for the
    /// caller to finish.
    ///
    /// Objects are built inactive on purpose. Unity runs Awake the moment a
    /// component is added to a live GameObject, so a component added early would
    /// wake up before its siblings exist and fail to find them.
    /// </summary>
    public static class CharacterFactory
    {
        /// <summary>Physical and visual description of a character to build.</summary>
        public struct Spec
        {
            public string Name;
            public CharacterStyle Style;
            public int Layer;
            public string Tag;
            public float Radius;
            public float StepOffset;
            public float SlopeLimit;
            public float Gravity;
            public float FallMultiplier;
            public bool CastShadows;

            public static Spec For(string name, in CharacterStyle style, int layer, string tag = null)
            {
                return new Spec
                {
                    Name = name,
                    Style = style,
                    Layer = layer,
                    Tag = tag,
                    Radius = 0.32f * Mathf.Clamp(style.Build, 0.7f, 1.6f),
                    StepOffset = 0.42f,
                    SlopeLimit = 52f,
                    Gravity = -24f,
                    FallMultiplier = 1.7f,
                    CastShadows = true
                };
            }
        }

        /// <summary>Everything the caller needs to finish wiring a character.</summary>
        public struct Build
        {
            public GameObject GameObject;
            public CharacterRig Rig;
            public ProceduralCharacterAnimator Animator;
            public CharacterMotor Motor;
            public Health Health;
            public Stamina Stamina;
        }

        /// <summary>
        /// Creates the character shell. The result is inactive: add the
        /// <see cref="Combatant"/> subclass and any controllers, then call
        /// <see cref="Finish"/>.
        /// </summary>
        public static Build Begin(in Spec spec, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(spec.Name);
            go.SetActive(false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.layer = spec.Layer;
            if (!string.IsNullOrEmpty(spec.Tag)) TrySetTag(go, spec.Tag);

            float height = Mathf.Max(0.8f, spec.Style.Height);
            float radius = Mathf.Clamp(spec.Radius, 0.15f, height * 0.35f);

            var controller = go.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            controller.slopeLimit = spec.SlopeLimit;
            controller.stepOffset = Mathf.Min(spec.StepOffset, height * 0.4f);
            controller.skinWidth = Mathf.Max(0.01f, radius * 0.08f);

            var motor = go.AddComponent<CharacterMotor>();
            var health = go.AddComponent<Health>();
            var stamina = go.AddComponent<Stamina>();

            CharacterRig rig = CharacterRigFactory.Build(go, spec.Style, spec.CastShadows);

            var animator = go.AddComponent<ProceduralCharacterAnimator>();
            animator.Bind(rig);

            motor.Configure(height, radius, spec.StepOffset, spec.SlopeLimit,
                spec.Gravity, spec.FallMultiplier);

            return new Build
            {
                GameObject = go,
                Rig = rig,
                Animator = animator,
                Motor = motor,
                Health = health,
                Stamina = stamina
            };
        }

        /// <summary>Activates the character and binds its visuals to the combatant.</summary>
        public static void Finish(in Build build, Combatant combatant)
        {
            build.GameObject.SetActive(true);
            combatant?.BindVisuals(build.Rig, build.Animator);
        }

        /// <summary>
        /// Sets a tag only if the project defines it, so a clone with a
        /// regenerated TagManager warns instead of throwing.
        /// </summary>
        public static void TrySetTag(GameObject go, string tag)
        {
            if (go == null || string.IsNullOrEmpty(tag)) return;
            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[CharacterFactory] Tag '{tag}' is not defined in this project. " +
                                 "Run Glowpulse > Validate Project Setup to add it.");
            }
        }
    }
}
