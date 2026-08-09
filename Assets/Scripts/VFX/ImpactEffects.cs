using System.Collections.Generic;
using Glowpulse.Core.Pooling;
using UnityEngine;

namespace Glowpulse.VFX
{
    /// <summary>The visual vocabulary of an impact.</summary>
    public enum ImpactVisual
    {
        /// <summary>Small spark burst. Light attacks.</summary>
        LightHit = 0,

        /// <summary>Bigger burst plus a shockwave ring. Heavy attacks.</summary>
        HeavyHit = 1,

        /// <summary>Flat sparks along the guard. Blocks.</summary>
        Block = 2,

        /// <summary>Bright ring flash. Parries.</summary>
        Parry = 3,

        /// <summary>Ground dust, for landings, dodges and knockdowns.</summary>
        Dust = 4,

        /// <summary>Heavy slam - ring plus dust. Knockdowns and throws.</summary>
        Slam = 5
    }

    /// <summary>
    /// Spawns the combat particle effects. Every effect is a pooled, procedurally
    /// built ParticleSystem, so nothing is instantiated or destroyed during a
    /// fight and there is no imported art to depend on.
    ///
    /// The restraint is deliberate: effects are short, small and directional so
    /// they read as impact rather than covering up the fight.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ImpactEffects : MonoBehaviour
    {
        private const int VisualCount = 6;

        private readonly ObjectPool[] _pools = new ObjectPool[VisualCount];
        private readonly List<ActiveEffect> _active = new List<ActiveEffect>(32);

        private static ImpactEffects _instance;

        public static ImpactEffects Instance => _instance;

        private struct ActiveEffect
        {
            public GameObject Go;
            public ImpactVisual Visual;
            public float ReturnAt;
        }

        public static ImpactEffects Install(GameObject host)
        {
            if (_instance != null) return _instance;
            _instance = host.GetComponent<ImpactEffects>() ?? host.AddComponent<ImpactEffects>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;

            for (int i = 0; i < VisualCount; i++)
            {
                var visual = (ImpactVisual)i;
                GameObject prototype = BuildPrototype(visual);
                prototype.transform.SetParent(transform, false);
                _pools[i] = new ObjectPool(prototype, transform, prewarm: 4, maxSize: 24);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Plays an effect at a point, oriented so its cone fires back along the
        /// direction the blow came from.
        /// </summary>
        public static void Play(ImpactVisual visual, Vector3 position, Vector3 direction, float scale = 1f)
        {
            if (_instance == null) return;
            _instance.Spawn(visual, position, direction, scale);
        }

        private void Spawn(ImpactVisual visual, Vector3 position, Vector3 direction, float scale)
        {
            ObjectPool pool = _pools[(int)visual];
            if (pool == null) return;

            // Sparks should fly back toward the attacker, which is the opposite
            // of the direction the victim was pushed.
            Vector3 forward = -direction;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.up;

            Quaternion rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            GameObject go = pool.Rent(position, rotation);
            go.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.35f, 3f);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }

            _active.Add(new ActiveEffect
            {
                Go = go,
                Visual = visual,
                // Unscaled: an effect must still finish while hit stop holds the
                // game still, otherwise it freezes mid-burst.
                ReturnAt = Time.unscaledTime + LifetimeOf(visual)
            });
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (now < _active[i].ReturnAt) continue;
                _pools[(int)_active[i].Visual].Return(_active[i].Go);
                _active.RemoveAt(i);
            }
        }

        private static float LifetimeOf(ImpactVisual visual)
        {
            switch (visual)
            {
                case ImpactVisual.HeavyHit: return 1.1f;
                case ImpactVisual.Slam: return 1.5f;
                case ImpactVisual.Dust: return 1.4f;
                case ImpactVisual.Parry: return 0.8f;
                default: return 0.7f;
            }
        }

        // ---- prototype construction ------------------------------------------------

        private static GameObject BuildPrototype(ImpactVisual visual)
        {
            var go = new GameObject("FX_" + visual);
            go.SetActive(false);

            switch (visual)
            {
                case ImpactVisual.LightHit:
                    AddSparks(go, count: 9, speed: 5.5f, size: 0.11f, life: 0.26f,
                        Hex("FFE9B0"), EffectTextures.Spark, angle: 28f);
                    break;

                case ImpactVisual.HeavyHit:
                    AddSparks(go, count: 20, speed: 8.5f, size: 0.16f, life: 0.4f,
                        Hex("FFD08A"), EffectTextures.Spark, angle: 36f);
                    AddRing(go, Hex("FFF0D0"), 0.35f, 2.6f, 0.28f);
                    break;

                case ImpactVisual.Block:
                    AddSparks(go, count: 12, speed: 4.5f, size: 0.09f, life: 0.24f,
                        Hex("BFD8FF"), EffectTextures.Spark, angle: 58f);
                    break;

                case ImpactVisual.Parry:
                    AddSparks(go, count: 16, speed: 6.5f, size: 0.1f, life: 0.3f,
                        Hex("FFFFFF"), EffectTextures.Spark, angle: 70f);
                    AddRing(go, Hex("CFE8FF"), 0.3f, 3.4f, 0.34f);
                    break;

                case ImpactVisual.Dust:
                    AddDust(go, count: 12, radius: 0.45f, size: 0.7f, life: 0.9f, Hex("9C8F7E"));
                    break;

                case ImpactVisual.Slam:
                    AddRing(go, Hex("E8DCC6"), 0.5f, 4.5f, 0.42f);
                    AddDust(go, count: 20, radius: 0.7f, size: 1.1f, life: 1.1f, Hex("8E8271"));
                    AddSparks(go, count: 10, speed: 5f, size: 0.13f, life: 0.35f,
                        Hex("FFDCA8"), EffectTextures.Spark, angle: 75f);
                    break;
            }

            return go;
        }

        private static void AddSparks(GameObject host, int count, float speed, float size, float life,
            Color color, Texture2D texture, float angle)
        {
            GameObject child = NewChild(host, "Sparks");
            ParticleSystem ps = child.AddComponent<ParticleSystem>();
            ConfigureCommon(ps, life, size, speed, color, count);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.05f;

            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 1.1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

            ApplyRenderer(ps, texture, stretch: true);
        }

        private static void AddDust(GameObject host, int count, float radius, float size, float life,
            Color color)
        {
            GameObject child = NewChild(host, "Dust");
            ParticleSystem ps = child.AddComponent<ParticleSystem>();
            ConfigureCommon(ps, life, size, 1.4f, color, count);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;

            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = -0.05f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.6f));

            ApplyRenderer(ps, EffectTextures.Smoke, stretch: false);
        }

        private static void AddRing(GameObject host, Color color, float startSize, float endSize, float life)
        {
            GameObject child = NewChild(host, "Ring");
            ParticleSystem ps = child.AddComponent<ParticleSystem>();
            ConfigureCommon(ps, life, startSize, 0f, color, 1);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 0f;
            main.startSpeed = 0f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(endSize / Mathf.Max(0.01f, startSize),
                AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));

            ApplyRenderer(ps, EffectTextures.Ring, stretch: false);

            // A ring should lie flat against the impact rather than face the camera.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private static GameObject NewChild(GameObject host, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(host.transform, false);
            return child;
        }

        private static void ConfigureCommon(ParticleSystem ps, float life, float size, float speed,
            Color color, int burstCount)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSize = size;
            main.startSpeed = speed;
            main.startColor = color;
            main.maxParticles = Mathf.Max(4, burstCount * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Unscaled time keeps effects animating through hit stop, which is
            // what makes the freeze read as impact rather than a stutter.
            main.useUnscaledTime = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Material _particleMaterial;

        private static void ApplyRenderer(ParticleSystem ps, Texture2D texture, bool stretch)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = stretch
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;

            if (stretch)
            {
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 2.6f;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            // Each effect carries its own texture, so it needs its own material
            // instance built from the shared template.
            var instance = new Material(GetParticleMaterial()) { name = "FX_" + texture.name };
            SetTexture(instance, texture);
            renderer.sharedMaterial = instance;
        }

        private static Material GetParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            _particleMaterial = new Material(shader) { name = "FX_Particle" };

            // Additive-over-alpha reads best for sparks and keeps effects from
            // darkening the scene behind them.
            if (_particleMaterial.HasProperty("_Surface")) _particleMaterial.SetFloat("_Surface", 1f);
            if (_particleMaterial.HasProperty("_Blend")) _particleMaterial.SetFloat("_Blend", 1f);
            _particleMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _particleMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _particleMaterial.SetFloat("_ZWrite", 0f);
            _particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _particleMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            _particleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return _particleMaterial;
        }

        private static void SetTexture(Material material, Texture2D texture)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _particleMaterial = null;
        }
    }
}
