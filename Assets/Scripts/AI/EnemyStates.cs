using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Enemies;
using UnityEngine;

namespace Glowpulse.AI
{
    /// <summary>
    /// The enemy behaviour states, one shared instance each.
    ///
    /// The chain the design calls for reads directly here:
    /// idle -> patrol -> alert -> chase -> combat -> attack -> react -> recover -> die.
    /// Combat is the hub: everything that is not a reaction returns to it.
    /// </summary>
    public static class EnemyStates
    {
        public static readonly AiState<EnemyBrain> Idle = new IdleState();
        public static readonly AiState<EnemyBrain> Patrol = new PatrolState();
        public static readonly AiState<EnemyBrain> Alert = new AlertState();
        public static readonly AiState<EnemyBrain> Chase = new ChaseState();
        public static readonly AiState<EnemyBrain> Combat = new CombatState();
        public static readonly AiState<EnemyBrain> Attack = new AttackState();
        public static readonly AiState<EnemyBrain> React = new ReactState();
        public static readonly AiState<EnemyBrain> Recover = new RecoverState();
        public static readonly AiState<EnemyBrain> Search = new SearchState();
        public static readonly AiState<EnemyBrain> Dead = new DeadState();

        /// <summary>Shared check: has this enemy noticed something worth reacting to?</summary>
        private static bool ShouldEngage(EnemyBrain brain)
        {
            return brain.CanSeeTarget && brain.DistanceToTarget <= brain.Archetype.SightRange;
        }

        // ---- idle -----------------------------------------------------------------

        private sealed class IdleState : AiState<EnemyBrain>
        {
            public override string Name => "Idle";

            public override void Enter(EnemyBrain brain)
            {
                brain.PatrolPauseUntil = Time.time + brain.RollPatrolPause();
            }

            public override void Tick(EnemyBrain brain, float dt)
            {
                brain.SetDesiredVelocity(Vector3.zero);

                // Idle enemies glance around, which reads as alive and also sells
                // the vision cone to the player.
                float sweep = Mathf.Sin(Time.time * 0.5f + brain.GetInstanceID() * 0.37f) * 55f;
                brain.FaceTowards(Quaternion.Euler(0f, sweep, 0f) * Vector3.forward);

                if (ShouldEngage(brain))
                {
                    brain.Machine.Change(Alert);
                    return;
                }

                if (Time.time >= brain.PatrolPauseUntil) brain.Machine.Change(Patrol);
            }
        }

        // ---- patrol ---------------------------------------------------------------

        private sealed class PatrolState : AiState<EnemyBrain>
        {
            public override string Name => "Patrol";

            public override void Enter(EnemyBrain brain)
            {
                brain.PatrolTarget = brain.PickPatrolPoint();
            }

            public override void Tick(EnemyBrain brain, float dt)
            {
                if (ShouldEngage(brain))
                {
                    brain.Machine.Change(Alert);
                    return;
                }

                brain.MoveTo(brain.PatrolTarget, brain.Archetype.WalkSpeed);

                float distance = MathUtil.FlatDistance(brain.transform.position, brain.PatrolTarget);
                if (distance <= 0.6f) brain.Machine.Change(Idle);

                // Give up on an unreachable point rather than shuffling forever.
                if (brain.Machine.TimeInState > 12f) brain.Machine.Change(Idle);
            }
        }

        // ---- alert ------------------------------------------------------------------

        private sealed class AlertState : AiState<EnemyBrain>
        {
            public override string Name => "Alert";

            public override void Enter(EnemyBrain brain)
            {
                brain.Animator?.SetStance(CharacterStance.Combat);
                // Reacting is not instant. This pause is the player's window to
                // strike first, and it is what makes stealth approaches matter.
                CombatDirector.Instance?.RaiseAlarm(brain.transform.position,
                    brain.Archetype.HearingRange);
            }

            public override void Tick(EnemyBrain brain, float dt)
            {
                brain.SetDesiredVelocity(Vector3.zero);

                if (brain.HasTarget) brain.FaceTarget();
                else brain.FaceTowards(brain.LastKnownTargetPosition - brain.transform.position);

                if (brain.Machine.TimeInState < brain.Archetype.ReactionTime) return;

                brain.Machine.Change(brain.RemembersTarget ? Chase : Search);
            }
        }

        // ---- chase --------------------------------------------------------------------

        private sealed class ChaseState : AiState<EnemyBrain>
        {
            public override string Name => "Chase";

            public override void Tick(EnemyBrain brain, float dt)
            {
                if (!brain.RemembersTarget)
                {
                    brain.Machine.Change(Search);
                    return;
                }

                EnemyArchetype archetype = brain.Archetype;

                // Head for the slot the director assigned rather than straight at
                // the player, so a group arrives spread out instead of in a line.
                CombatDirector director = CombatDirector.Instance;
                Vector3 destination = director != null
                    ? director.GetSlotPosition(brain, archetype.PreferredRange)
                    : brain.LastKnownTargetPosition;

                brain.MoveTo(destination, archetype.ChaseSpeed);

                if (brain.CanSeeTarget) brain.FaceTarget();

                if (brain.CanSeeTarget && brain.DistanceToTarget <= archetype.PreferredRange * 1.35f)
                    brain.Machine.Change(Combat);
            }
        }

        // ---- combat -------------------------------------------------------------------------

        private sealed class CombatState : AiState<EnemyBrain>
        {
            public override string Name => "Combat";

            public override void Enter(EnemyBrain brain)
            {
                brain.Animator?.SetStance(CharacterStance.Combat);
            }

            public override void Exit(EnemyBrain brain)
            {
                brain.Combatant.GuardUp = false;
            }

            public override void Tick(EnemyBrain brain, float dt)
            {
                if (!brain.RemembersTarget)
                {
                    brain.Machine.Change(Search);
                    return;
                }

                EnemyArchetype archetype = brain.Archetype;
                float distance = brain.DistanceToTarget;

                brain.FaceTarget();

                // Drifted too far to fight: go back to closing the gap.
                if (distance > archetype.PreferredRange * 2.2f || !brain.CanSeeTarget)
                {
                    brain.Machine.Change(Chase);
                    return;
                }

                if (brain.AttackReady && distance <= archetype.AttackRange * 1.15f)
                {
                    if (brain.TryBeginAttack())
                    {
                        brain.Machine.Change(Attack);
                        return;
                    }
                }

                HoldPosition(brain, distance, archetype);
                MaybeGuard(brain);
            }

            /// <summary>
            /// Waiting for a turn is an active behaviour: close if too far, back off
            /// if crowding, and circle otherwise so the fight keeps moving.
            /// </summary>
            private static void HoldPosition(EnemyBrain brain, float distance, EnemyArchetype archetype)
            {
                CombatDirector director = CombatDirector.Instance;

                // With its cooldown up, the enemy closes the gap rather than
                // circling. Preferred range sits outside attack range, so an
                // enemy that only ever holds station can never actually reach
                // far enough to commit to a swing.
                if (brain.AttackReady && brain.CanSeeTarget && brain.HasTarget)
                {
                    brain.MoveTo(brain.Target.Transform.position, archetype.ChaseSpeed * 0.8f);
                    return;
                }

                if (distance < archetype.PreferredRange * 0.7f)
                {
                    brain.BackAway(archetype.StrafeSpeed);
                    return;
                }

                if (distance > archetype.PreferredRange * 1.25f)
                {
                    Vector3 destination = director != null
                        ? director.GetSlotPosition(brain, archetype.PreferredRange)
                        : brain.Target.Transform.position;
                    brain.MoveTo(destination, archetype.StrafeSpeed * 1.4f);
                    return;
                }

                brain.Circle(archetype.StrafeSpeed);
            }

            /// <summary>
            /// Enemies waiting their turn sometimes raise a guard, which makes the
            /// crowd feel like it is participating rather than idling.
            /// </summary>
            private static void MaybeGuard(EnemyBrain brain)
            {
                bool waiting = !brain.AttackReady;
                bool wantsGuard = waiting && brain.DistanceToTarget < brain.Archetype.PreferredRange * 1.4f;

                if (!wantsGuard)
                {
                    brain.Combatant.GuardUp = false;
                    return;
                }

                // Decide once per approach rather than re-rolling every frame.
                if (!brain.Combatant.GuardUp && Random.value < brain.Archetype.BlockChance * Time.deltaTime * 4f)
                    brain.Combatant.GuardUp = true;
            }
        }

        // ---- attack -----------------------------------------------------------------------------

        private sealed class AttackState : AiState<EnemyBrain>
        {
            public override string Name => "Attack";

            public override void Tick(EnemyBrain brain, float dt)
            {
                if (brain.TickAttack(dt)) return;

                // Fast enemies hit and get out; heavies stand their ground.
                if (Random.value < brain.Archetype.RetreatChance)
                    brain.Machine.Change(Reposition);
                else
                    brain.Machine.Change(Combat);
            }

            public override void Exit(EnemyBrain brain)
            {
                brain.CancelAttack();
            }
        }

        /// <summary>Short disengage after an attack. Shares the combat state's data.</summary>
        public static readonly AiState<EnemyBrain> Reposition = new RepositionState();

        private sealed class RepositionState : AiState<EnemyBrain>
        {
            public override string Name => "Reposition";

            public override void Tick(EnemyBrain brain, float dt)
            {
                if (!brain.RemembersTarget)
                {
                    brain.Machine.Change(Search);
                    return;
                }

                brain.FaceTarget();
                brain.BackAway(brain.Archetype.StrafeSpeed * 1.3f);

                if (brain.Machine.TimeInState > 0.75f ||
                    brain.DistanceToTarget > brain.Archetype.PreferredRange * 1.6f)
                    brain.Machine.Change(Combat);
            }
        }

        // ---- reactions ------------------------------------------------------------------------------

        private sealed class ReactState : AiState<EnemyBrain>
        {
            public override string Name => "React";

            public override void Enter(EnemyBrain brain)
            {
                brain.CancelAttack();
                brain.SetDesiredVelocity(Vector3.zero);
            }

            public override void Tick(EnemyBrain brain, float dt)
            {
                // The reaction is owned by the combatant's stagger timer; this
                // state just holds still until it reports recovery.
                brain.SetDesiredVelocity(Vector3.zero);

                if (brain.HasTarget && !brain.Combatant.IsDown) brain.FaceTarget();

                // Safety net in case a recovery event is ever missed.
                if (!brain.Combatant.IsReacting) brain.Machine.Change(Recover);
            }
        }

        private sealed class RecoverState : AiState<EnemyBrain>
        {
            public override string Name => "Recover";

            public override void Tick(EnemyBrain brain, float dt)
            {
                brain.SetDesiredVelocity(Vector3.zero);
                if (brain.HasTarget) brain.FaceTarget();

                // A beat of hesitation after being hit, which is the player's
                // window to press the advantage.
                if (brain.Machine.TimeInState < 0.35f) return;

                brain.Machine.Change(brain.RemembersTarget ? Combat : Search);
            }
        }

        // ---- search -----------------------------------------------------------------------------------

        private sealed class SearchState : AiState<EnemyBrain>
        {
            public override string Name => "Search";

            public override void Tick(EnemyBrain brain, float dt)
            {
                if (brain.CanSeeTarget)
                {
                    brain.Machine.Change(Combat);
                    return;
                }

                Vector3 destination = brain.LastKnownTargetPosition;
                brain.MoveTo(destination, brain.Archetype.WalkSpeed * 1.35f);

                bool arrived = MathUtil.FlatDistance(brain.transform.position, destination) < 1.2f;

                // Look around at the last known spot before giving up.
                if (arrived)
                {
                    brain.SetDesiredVelocity(Vector3.zero);
                    float sweep = Mathf.Sin(Time.time * 2.2f) * 85f;
                    brain.FaceTowards(Quaternion.Euler(0f, sweep, 0f) * Vector3.forward);
                }

                if (brain.Machine.TimeInState > 6f || (arrived && brain.Machine.TimeInState > 3f))
                    brain.Machine.Change(Idle);
            }

            public override void Exit(EnemyBrain brain)
            {
                brain.Animator?.SetStance(CharacterStance.Relaxed);
            }
        }

        // ---- death ------------------------------------------------------------------------------------

        private sealed class DeadState : AiState<EnemyBrain>
        {
            public override string Name => "Dead";

            public override void Enter(EnemyBrain brain)
            {
                brain.CancelAttack();
                brain.SetDesiredVelocity(Vector3.zero);
                brain.Combatant.GuardUp = false;

                // Stop taking part in the encounter, but leave the body so the
                // player can see what they did.
                CombatDirector.Instance?.Unregister(brain);

                var controller = brain.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
            }

            public override void Tick(EnemyBrain brain, float dt)
            {
                brain.SetDesiredVelocity(Vector3.zero);
            }
        }
    }
}
