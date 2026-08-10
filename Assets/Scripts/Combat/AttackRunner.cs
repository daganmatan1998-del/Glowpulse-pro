using System;
using System.Collections.Generic;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// Runs a single <see cref="AttackDefinition"/> from wind-up to recovery:
    /// advances the timeline, opens and closes the hitbox, resolves hits, and
    /// reports the lunge velocity the owner should move at this frame.
    ///
    /// Both the player and every enemy drive their attacks through one of these,
    /// so a move behaves identically no matter who threw it - the difference
    /// between a player jab and an enemy jab is entirely in the data.
    /// </summary>
    public sealed class AttackRunner
    {
        private readonly MeleeHitbox _hitbox = new MeleeHitbox();
        private readonly List<Combatant> _victims = new List<Combatant>(8);

        private AttackDefinition _move;
        private float _time;
        private bool _swingOpened;

        /// <summary>Raised for every victim the move connects with.</summary>
        public event Action<AttackDefinition, Combatant, HitResult, DamageInfo> Landed;

        /// <summary>Raised as the active frames begin, for whoosh audio.</summary>
        public event Action<AttackDefinition> SwingOpened;

        public AttackDefinition Move => _move;
        public float Time => _time;
        public bool IsRunning => _move != null;
        public bool Finished => _move == null || _time >= _move.Duration;

        /// <summary>True once the hitbox has closed and the owner is in recovery.</summary>
        public bool InRecovery => _move != null && _time > _move.ActiveEnd;

        /// <summary>True while the move can still be chained out of.</summary>
        public bool InComboWindow => _move != null && _move.InComboWindow(_time);

        /// <summary>Number of characters hit by the current swing.</summary>
        public int HitCount => _hitbox.HitCount;

        /// <summary>Scales the damage of everything this runner throws.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        public void Begin(AttackDefinition move)
        {
            _move = move;
            _time = 0f;
            _swingOpened = false;
        }

        public void Cancel()
        {
            _move = null;
            _time = 0f;
            _swingOpened = false;
        }

        /// <summary>
        /// Skips the timeline to just past the active frames. Used when a move's
        /// effect was already resolved by hand, such as a throw.
        /// </summary>
        public void SkipToRecovery(AttackDefinition move)
        {
            _move = move;
            _time = move.ActiveEnd + 0.001f;
            _swingOpened = true;
        }

        /// <summary>
        /// Advances the move by one frame and returns the world-space velocity the
        /// owner should move at.
        /// </summary>
        public Vector3 Tick(float dt, Transform owner, Faction faction, GameObject attacker,
            ITargetable trackTarget = null)
        {
            if (_move == null) return Vector3.zero;

            _time += dt;
            AttackDefinition move = _move;

            if (!_swingOpened && _time >= move.ActiveStart)
            {
                _swingOpened = true;
                _hitbox.BeginSwing();
                SwingOpened?.Invoke(move);
                CombatFeedback.Swing(move, MeleeHitbox.Center(owner, move));
            }

            if (move.IsActiveAt(_time)) ResolveHits(move, owner, faction, attacker);

            return Lunge(move, owner, trackTarget);
        }

        private void ResolveHits(AttackDefinition move, Transform owner, Faction faction,
            GameObject attacker)
        {
            _victims.Clear();
            if (_hitbox.Query(owner, move, faction, _victims) == 0) return;

            bool playerAttacker = faction == Faction.Player;

            for (int i = 0; i < _victims.Count; i++)
            {
                Combatant victim = _victims[i];

                Vector3 point = ContactPoint(victim, MeleeHitbox.Center(owner, move));
                Vector3 direction = MathUtil.FlatDirection(victim.transform.position - owner.position);
                if (direction.sqrMagnitude < 0.0001f) direction = owner.forward;

                DamageInfo info = move.BuildDamage(attacker, faction, point, direction, DamageMultiplier);
                HitResult result = victim.ApplyDamage(in info);

                switch (result)
                {
                    case HitResult.Hit:
                    case HitResult.Killed:
                        victim.ApplyStagger(info.StaggerDuration, direction, info.Impact);
                        victim.ApplyKnockback(direction * info.KnockbackForce);
                        break;

                    case HitResult.Blocked:
                        // A blocked hit still shoves the defender back a little,
                        // which is what makes holding a guard feel like work.
                        victim.ApplyKnockback(direction * (info.KnockbackForce * 0.3f));
                        break;
                }

                CombatFeedback.Landed(move, in info, result, point, playerAttacker);
                Landed?.Invoke(move, victim, result, info);
            }
        }

        /// <summary>
        /// The lunge runs across the wind-up and active frames and stops dead in
        /// recovery, which is what makes a whiffed heavy feel over-committed.
        /// </summary>
        private Vector3 Lunge(AttackDefinition move, Transform owner, ITargetable target)
        {
            float lungeEnd = move.ActiveEnd;
            if (_time > lungeEnd || lungeEnd <= 0f) return Vector3.zero;

            float distance = move.LungeDistance;

            // Close extra ground when the target is beyond reach, so attacks do
            // not fall short of someone backing away.
            if (target != null && target.IsTargetable && move.LungeTrackingBonus > 0f)
            {
                float gap = MathUtil.FlatDistance(owner.position, target.Transform.position);
                float reach = move.HitboxOffset.z + move.HitboxRadius;
                if (gap > reach) distance += Mathf.Min(gap - reach, move.LungeTrackingBonus);
            }

            // Front-load the step so it happens with the swing, not after it.
            float u = Mathf.Clamp01(_time / lungeEnd);
            const float averageOfSine = 2f / Mathf.PI;
            float speed = distance / lungeEnd * (Mathf.Sin(u * Mathf.PI) / averageOfSine);

            return owner.forward * speed;
        }

        private static Vector3 ContactPoint(Combatant victim, Vector3 from)
        {
            // Placing the spark on the victim's surface rather than at their pivot
            // is a small thing that makes a hit look like contact.
            var collider = victim.GetComponent<Collider>();
            return collider != null ? collider.ClosestPoint(from) : victim.AimPoint;
        }

        /// <summary>Plays the move's animation clip, rescaled to the move's length.</summary>
        public static void PlayClip(ICharacterAnimator animator, AttackDefinition move, float fadeIn = 0.04f)
        {
            if (animator == null || move == null || string.IsNullOrEmpty(move.ClipId)) return;

            PoseClip clip = PoseLibrary.Get(move.ClipId);
            float speed = clip != null ? move.ClipSpeed(clip.Duration) : 1f;
            animator.PlayAction(move.ClipId, speed, fadeIn);
        }
    }
}
