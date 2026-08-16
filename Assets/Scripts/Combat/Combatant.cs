using System;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// The hub every fighting character has: identity, health, stamina, and the
    /// single entry point through which all damage flows.
    ///
    /// Subclasses decide how a hit is *received* - the player can block and parry,
    /// enemies can have super armour - by overriding <see cref="EvaluateDefence"/>
    /// and <see cref="OnHitTaken"/>. Attackers only ever call
    /// <see cref="ApplyDamage"/> and read the returned <see cref="HitResult"/>,
    /// so they never need to know which kind of character they hit.
    /// </summary>
    [DisallowMultipleComponent]
    public class Combatant : MonoBehaviour, IDamageable, ITargetable, IStaggerable
    {
        [SerializeField] protected Faction _faction = Faction.Neutral;
        [SerializeField] protected string _displayName = "Combatant";

        [Tooltip("Height above the pivot that attacks and the lock-on reticle aim at. " +
                 "Left at zero it is taken from the character rig.")]
        [SerializeField] protected float _aimHeight;

        private Health _health;
        private Stamina _stamina;

        private CharacterMotor _motor;
        private float _controlReturnsAt;
        private bool _down;

        /// <summary>Raised for every resolved incoming hit, including blocks and misses.</summary>
        public event Action<DamageInfo, HitResult> HitTaken;

        public event Action<Combatant> Died;

        /// <summary>Character was staggered for the given number of seconds.</summary>
        public event Action<float> Staggered;

        /// <summary>Character was put on the floor for the given number of seconds.</summary>
        public event Action<float> KnockedDown;

        /// <summary>Character is back in control after a stagger or knockdown.</summary>
        public event Action Recovered;

        public Faction Faction
        {
            get => _faction;
            set => _faction = value;
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public Health Health => _health != null ? _health : _health = GetComponent<Health>();
        public Stamina Stamina => _stamina != null ? _stamina : _stamina = GetComponent<Stamina>();

        public ICharacterAnimator Animator { get; protected set; }
        public CharacterRig Rig { get; protected set; }

        public Transform Transform => transform;

        public virtual bool IsAlive => Health != null && Health.IsAlive;

        public virtual bool IsTargetable => IsAlive && isActiveAndEnabled;

        /// <summary>True while the character is holding a guard. Overridden by types that can block.</summary>
        public virtual bool IsBlocking => false;

        /// <summary>True while lying on the floor after a knockdown.</summary>
        public bool IsDown => _down;

        /// <summary>True while a stagger or knockdown has taken control away.</summary>
        public bool IsReacting => Time.time < _controlReturnsAt;

        /// <summary>The character currently holding this one in a grapple, or null.</summary>
        public Transform GrabbedBy { get; private set; }

        public bool IsGrabbed => GrabbedBy != null;

        /// <summary>Whether a grab can be started on this character right now.</summary>
        public virtual bool CanBeGrabbed => IsAlive && !IsDown && !IsGrabbed;

        /// <summary>
        /// Puts the character into a held state. The holder is responsible for
        /// positioning them until <see cref="Release"/> is called.
        /// </summary>
        public virtual void Grabbed(Transform holder)
        {
            if (holder == null) return;
            GrabbedBy = holder;
            Motor?.ClearImpulse();
            Animator?.SetStance(CharacterStance.Guarding);
        }

        public virtual void Release()
        {
            if (GrabbedBy == null) return;
            GrabbedBy = null;
            Animator?.SetStance(CharacterStance.Combat);
        }

        protected CharacterMotor Motor => _motor != null ? _motor : _motor = GetComponent<CharacterMotor>();

        public float AimHeight => _aimHeight > 0.01f ? _aimHeight : (Rig != null ? Rig.AimHeight : 1.2f);

        public Vector3 AimPoint => transform.position + Vector3.up * AimHeight;

        protected virtual void Awake()
        {
            _health = GetComponent<Health>();
            _stamina = GetComponent<Stamina>();
            _motor = GetComponent<CharacterMotor>();
            Rig = GetComponent<CharacterRig>();
            Animator = GetComponent<ICharacterAnimator>();

            if (_health != null) _health.Died += HandleDeath;
        }

        protected virtual void OnEnable() => TargetRegistry.Register(this);

        protected virtual void OnDisable() => TargetRegistry.Unregister(this);

        protected virtual void OnDestroy()
        {
            TargetRegistry.Unregister(this);
            if (_health != null) _health.Died -= HandleDeath;
        }

        /// <summary>Wires up a rig and animator built at runtime.</summary>
        public void BindVisuals(CharacterRig rig, ICharacterAnimator animator)
        {
            Rig = rig;
            Animator = animator;
        }

        // ---- damage pipeline ----------------------------------------------------

        public HitResult ApplyDamage(in DamageInfo info)
        {
            if (!IsAlive) return HitResult.Missed;

            // Friendly fire is off: an attack only lands on a hostile faction.
            if (info.Attacker != null && info.Attacker != gameObject
                && !info.AttackerFaction.IsHostileTo(_faction))
                return HitResult.Missed;

            Health health = Health;
            if (health == null) return HitResult.Immune;

            if (health.IsInvulnerable)
            {
                // Dodge i-frames swallowed the attack. The defender wants to know:
                // a clean evade is what opens a counter.
                OnEvaded(in info);
                HitTaken?.Invoke(info, HitResult.Immune);
                return HitResult.Immune;
            }

            HitResult result = EvaluateDefence(in info);

            switch (result)
            {
                case HitResult.Missed:
                case HitResult.Immune:
                    HitTaken?.Invoke(info, result);
                    return result;

                case HitResult.Parried:
                    OnParried(in info);
                    HitTaken?.Invoke(info, result);
                    return result;

                case HitResult.Blocked:
                {
                    float chip = info.Amount * Mathf.Clamp01(info.BlockedDamageFraction);
                    if (chip > 0f) health.Damage(chip);
                    Stamina?.SpendUnchecked(info.BlockStaminaCost);
                    OnBlocked(in info);
                    HitTaken?.Invoke(info, HitResult.Blocked);
                    return HitResult.Blocked;
                }
            }

            float applied = health.Damage(ModifyIncomingDamage(info.Amount, in info));
            bool killed = !health.IsAlive;

            OnHitTaken(in info, applied, killed);

            HitResult final = killed ? HitResult.Killed : HitResult.Hit;
            HitTaken?.Invoke(info, final);
            return final;
        }

        /// <summary>
        /// Decides whether the hit lands. Default behaviour has no defences, so
        /// everything connects. Return Blocked, Parried, Missed or Immune to
        /// divert the hit before any damage is applied.
        /// </summary>
        protected virtual HitResult EvaluateDefence(in DamageInfo info) => HitResult.Hit;

        /// <summary>Called after damage has been applied but before the event fires.</summary>
        protected virtual void OnHitTaken(in DamageInfo info, float appliedDamage, bool killed) { }

        protected virtual void OnBlocked(in DamageInfo info) { }

        protected virtual void OnParried(in DamageInfo info) { }

        /// <summary>Called when invulnerability frames caused the hit to pass through.</summary>
        protected virtual void OnEvaded(in DamageInfo info) { }

        /// <summary>
        /// Last chance to change how much damage actually lands. Armour, difficulty
        /// scaling and resistances all hook in here so the rule lives in one place.
        /// </summary>
        protected virtual float ModifyIncomingDamage(float amount, in DamageInfo info) => amount;

        // ---- physical reactions --------------------------------------------------

        /// <summary>
        /// Takes control away for a moment and plays the matching reaction.
        /// A knockdown supersedes a stagger; a stagger never shortens one that is
        /// already running, so rapid chip hits cannot stunlock-cancel a big one.
        /// </summary>
        public virtual void ApplyStagger(float duration, Vector3 direction, HitImpact impact)
        {
            if (!IsAlive) return;
            if (duration <= 0f) duration = DamageInfo.DefaultStagger(impact);

            bool knockdown = impact == HitImpact.Knockdown || impact == HitImpact.Launch;

            if (!knockdown && IsReacting && Time.time + duration < _controlReturnsAt) return;

            Vector3 local = transform.InverseTransformDirection(direction.normalized);
            Animator?.AddImpulse(local, Mathf.Lerp(0.6f, 2.4f, (int)impact / 4f));

            if (knockdown)
            {
                _down = true;
                _controlReturnsAt = Time.time + duration;
                Animator?.SetStance(CharacterStance.Downed);
                Animator?.PlayAction(PoseLibrary.Knockdown);
                KnockedDown?.Invoke(duration);
            }
            else
            {
                _controlReturnsAt = Time.time + duration;
                Animator?.PlayAction(impact == HitImpact.Light ? PoseLibrary.HitLight : PoseLibrary.HitHeavy);
                Staggered?.Invoke(duration);
            }
        }

        public virtual void ApplyKnockback(Vector3 velocity)
        {
            Motor?.AddImpulse(velocity);
        }

        protected virtual void Update()
        {
            if (_controlReturnsAt <= 0f || Time.time < _controlReturnsAt) return;
            if (!IsAlive) return;

            _controlReturnsAt = 0f;

            if (_down)
            {
                // Getting up is its own beat - the character is still vulnerable
                // for the length of the stand-up animation.
                _down = false;
                PoseClip getUp = PoseLibrary.Get(PoseLibrary.GetUp);
                float duration = getUp != null ? getUp.Duration : 0.9f;
                _controlReturnsAt = Time.time + duration;
                Animator?.SetStance(CharacterStance.Combat);
                Animator?.PlayAction(PoseLibrary.GetUp);
                Staggered?.Invoke(duration);
                return;
            }

            Recovered?.Invoke();
        }

        protected virtual void HandleDeath()
        {
            Release();
            _down = true;
            _controlReturnsAt = 0f;
            Animator?.SetStance(CharacterStance.Dead);
            Animator?.PlayAction(PoseLibrary.Death);
            Died?.Invoke(this);
        }

        /// <summary>Convenience for the many places that need "the combatant on this collider".</summary>
        public static Combatant From(Component component)
        {
            if (component == null) return null;
            return component.GetComponentInParent<Combatant>();
        }
    }
}
