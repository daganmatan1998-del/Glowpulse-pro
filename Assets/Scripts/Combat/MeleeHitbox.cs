using System.Collections.Generic;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// Resolves melee hits with overlap queries rather than physical trigger
    /// colliders.
    ///
    /// Trigger colliders would need enabling and disabling on exact frames, would
    /// fire callbacks a frame late, and would miss anything the swing passed
    /// through between fixed steps. Querying the volume directly each frame the
    /// hitbox is live is simpler, frame-accurate and allocation free.
    /// </summary>
    public sealed class MeleeHitbox
    {
        private readonly Collider[] _overlap = new Collider[24];
        private readonly HashSet<Combatant> _hitThisSwing = new HashSet<Combatant>();
        private readonly List<Combatant> _found = new List<Combatant>(8);

        /// <summary>Number of distinct victims the current swing has connected with.</summary>
        public int HitCount => _hitThisSwing.Count;

        /// <summary>Clears the per-swing memory. Call when a move's active frames begin.</summary>
        public void BeginSwing() => _hitThisSwing.Clear();

        /// <summary>World-space centre of a move's hitbox for an attacker.</summary>
        public static Vector3 Center(Transform owner, AttackDefinition move)
        {
            return owner.TransformPoint(move.HitboxOffset);
        }

        /// <summary>
        /// Finds every hostile combatant inside the move's hitbox that this swing
        /// has not already hit, appending them to <paramref name="results"/>.
        /// Returns how many were added.
        /// </summary>
        public int Query(Transform owner, AttackDefinition move, Faction attackerFaction,
            List<Combatant> results)
        {
            _found.Clear();

            Vector3 center = Center(owner, move);
            int count;

            if (move.Shape == HitboxShape.Capsule)
            {
                Vector3 forward = owner.forward * (move.HitboxLength * 0.5f);
                count = Physics.OverlapCapsuleNonAlloc(center - forward, center + forward,
                    move.HitboxRadius, _overlap, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                count = Physics.OverlapSphereNonAlloc(center, move.HitboxRadius, _overlap,
                    GameLayers.CharacterMask, QueryTriggerInteraction.Ignore);
            }

            int added = 0;
            for (int i = 0; i < count; i++)
            {
                Combatant victim = Combatant.From(_overlap[i]);
                if (victim == null) continue;
                if (victim.transform == owner) continue;
                if (!victim.IsAlive) continue;
                if (!attackerFaction.IsHostileTo(victim.Faction)) continue;

                // One hit per victim per swing, no matter how many frames the
                // hitbox overlaps them or how many colliders they have.
                if (!_hitThisSwing.Add(victim)) continue;

                results.Add(victim);
                added++;

                if (_hitThisSwing.Count >= move.MaxTargets) break;
            }

            return added;
        }

        /// <summary>
        /// Finds the single best target in front of the owner within a radius.
        /// Used by grabs and finishers, which act on one specific character.
        /// </summary>
        public Combatant QuerySingle(Transform owner, AttackDefinition move, Faction attackerFaction,
            bool requireDowned = false)
        {
            Vector3 center = Center(owner, move);
            int count = Physics.OverlapSphereNonAlloc(center, move.HitboxRadius, _overlap,
                GameLayers.CharacterMask, QueryTriggerInteraction.Ignore);

            Combatant best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Combatant victim = Combatant.From(_overlap[i]);
                if (victim == null || victim.transform == owner || !victim.IsAlive) continue;
                if (!attackerFaction.IsHostileTo(victim.Faction)) continue;
                if (requireDowned && !victim.IsDown) continue;

                Vector3 to = victim.transform.position - owner.position;
                float forwardness = Vector3.Dot(owner.forward, MathUtil.FlatDirection(to));
                if (forwardness < 0.2f) continue;

                // Prefer whatever is closest to straight ahead.
                float score = MathUtil.Flat(to).magnitude * (2f - forwardness);
                if (score >= bestScore) continue;

                bestScore = score;
                best = victim;
            }

            return best;
        }

        /// <summary>Draws the hitbox while the move is selected. Editor only.</summary>
        public static void DrawGizmo(Transform owner, AttackDefinition move)
        {
            if (owner == null || move == null) return;

            Vector3 center = Center(owner, move);
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.4f);

            if (move.Shape == HitboxShape.Capsule)
            {
                Vector3 forward = owner.forward * (move.HitboxLength * 0.5f);
                Gizmos.DrawWireSphere(center - forward, move.HitboxRadius);
                Gizmos.DrawWireSphere(center + forward, move.HitboxRadius);
                Gizmos.DrawLine(center - forward, center + forward);
            }
            else
            {
                Gizmos.DrawWireSphere(center, move.HitboxRadius);
            }
        }
    }
}
