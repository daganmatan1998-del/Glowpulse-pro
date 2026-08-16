using Glowpulse.AI;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>
    /// A pedestrian's whole behaviour, as shared stateless singletons - the same
    /// arrangement the enemy AI uses, so a crowd of any size still allocates
    /// nothing while it thinks.
    ///
    /// The interesting part is not the walking, it is the reaction. A fight
    /// breaking out on a street should visibly change the street: people nearby
    /// bolt, people cornered cover up, and people at a safe distance stop and
    /// watch. That last one costs almost nothing and does more for the feeling of
    /// a living city than any amount of ambient wandering.
    /// </summary>
    public static class CivilianStates
    {
        public static readonly StrollState Stroll = new StrollState();
        public static readonly PauseState Pause = new PauseState();
        public static readonly StartledState Startled = new StartledState();
        public static readonly FleeState Flee = new FleeState();
        public static readonly CowerState Cower = new CowerState();
        public static readonly WatchState Watch = new WatchState();

        /// <summary>Inside this range of trouble, a civilian runs instead of watching.</summary>
        public const float PanicRadius = 11f;

        /// <summary>Beyond this, the fight is somebody else's problem.</summary>
        public const float CalmRadius = 22f;

        /// <summary>Walking the pavement graph from corner to corner.</summary>
        public sealed class StrollState : AiState<CivilianBrain>
        {
            public override string Name => "Stroll";

            public override void Enter(CivilianBrain brain)
            {
                brain.Animator?.SetStance(CharacterStance.Relaxed);
                if (!brain.HasRoute) brain.ChooseNextStop();
            }

            public override void Tick(CivilianBrain brain, float dt)
            {
                if (brain.HasThreat && brain.ThreatDistance < CalmRadius)
                {
                    brain.Machine.Change(Startled);
                    return;
                }

                if (!brain.HasRoute && !brain.ChooseNextStop())
                {
                    brain.Stand();
                    return;
                }

                brain.MoveTowardsTarget(brain.WalkSpeed);

                if (brain.DistanceToTarget() > 0.6f) return;

                // Arrived. Most of the time carry on; occasionally stop, which is
                // what breaks up a crowd that otherwise all moves at one speed.
                if (brain.Rng.NextDouble() < 0.22)
                {
                    brain.Machine.Change(Pause);
                    return;
                }

                brain.ChooseNextStop();
            }
        }

        /// <summary>Standing about: waiting, checking the time, greeting somebody.</summary>
        public sealed class PauseState : AiState<CivilianBrain>
        {
            private const float MinDuration = 1.6f;

            public override string Name => "Pause";

            public override void Enter(CivilianBrain brain)
            {
                brain.Stand();

                double roll = brain.Rng.NextDouble();
                string clip = roll < 0.4 ? CivilianPoses.LookAround
                    : roll < 0.7 ? CivilianPoses.CheckWatch
                    : CivilianPoses.Wave;

                brain.Animator?.PlayAction(clip);
            }

            public override void Tick(CivilianBrain brain, float dt)
            {
                brain.Stand();

                if (brain.HasThreat && brain.ThreatDistance < CalmRadius)
                {
                    brain.Machine.Change(Startled);
                    return;
                }

                if (brain.Machine.TimeInState < MinDuration) return;
                if (brain.Animator != null && brain.Animator.IsPlayingAction) return;

                brain.Machine.Change(Stroll);
            }
        }

        /// <summary>
        /// The half-second between hearing something and deciding what to do about
        /// it. Short, but leaving it out is what makes NPCs look like they were
        /// told about the fight rather than noticing it.
        /// </summary>
        public sealed class StartledState : AiState<CivilianBrain>
        {
            private const float Duration = 0.45f;

            public override string Name => "Startled";

            public override void Enter(CivilianBrain brain)
            {
                brain.Stand();
                brain.Animator?.PlayAction(CivilianPoses.Startle);
                AudioSignal(brain);
            }

            public override void Tick(CivilianBrain brain, float dt)
            {
                brain.Stand();
                brain.FaceTowards(brain.ThreatPoint - brain.transform.position);

                if (brain.Machine.TimeInState < Duration) return;

                if (!brain.HasThreat)
                {
                    brain.Machine.Change(Stroll);
                    return;
                }

                float distance = brain.ThreatDistance;

                if (distance < PanicRadius)
                    brain.Machine.Change(brain.IsCornered() ? (AiState<CivilianBrain>)Cower : Flee);
                else
                    brain.Machine.Change(Watch);
            }

            private static void AudioSignal(CivilianBrain brain)
            {
                // A gasp from the crowd is the cheapest possible cue that the
                // player's fight has been noticed by the world.
                Audio.AudioManager.PlayAt(Audio.Sfx.Grunt, brain.transform.position, 0.3f, 0.22f);
            }
        }

        /// <summary>Running away, one pavement corner at a time.</summary>
        public sealed class FleeState : AiState<CivilianBrain>
        {
            public override string Name => "Flee";

            public override void Enter(CivilianBrain brain)
            {
                brain.Animator?.SetStance(CharacterStance.Combat);
                brain.ChooseEscape();
            }

            public override void Tick(CivilianBrain brain, float dt)
            {
                if (!brain.HasThreat || brain.ThreatDistance > CalmRadius)
                {
                    brain.Machine.Change(Watch);
                    return;
                }

                if (brain.IsCornered())
                {
                    brain.Machine.Change(Cower);
                    return;
                }

                if (!brain.HasRoute || brain.DistanceToTarget() < 0.8f)
                {
                    if (!brain.ChooseEscape())
                    {
                        brain.Machine.Change(Cower);
                        return;
                    }
                }

                brain.MoveTowardsTarget(brain.FleeSpeed);
            }

            public override void Exit(CivilianBrain brain)
            {
                brain.Animator?.SetStance(CharacterStance.Relaxed);
            }
        }

        /// <summary>Nowhere left to run: crouch, cover up, wait for it to pass.</summary>
        public sealed class CowerState : AiState<CivilianBrain>
        {
            private const float MinDuration = 1.2f;

            public override string Name => "Cower";

            public override void Enter(CivilianBrain brain)
            {
                brain.Stand();
                brain.Animator?.PlayAction(CivilianPoses.Cower);
            }

            public override void Tick(CivilianBrain brain, float dt)
            {
                brain.Stand();
                brain.FaceTowards(brain.ThreatPoint - brain.transform.position);

                if (brain.Machine.TimeInState < MinDuration) return;
                if (brain.HasThreat && brain.ThreatDistance < PanicRadius && brain.IsCornered()) return;

                brain.Machine.Change(brain.HasThreat ? (AiState<CivilianBrain>)Flee : Stroll);
            }

            public override void Exit(CivilianBrain brain)
            {
                brain.Animator?.StopAction();
            }
        }

        /// <summary>
        /// Standing at a safe distance watching the fight. Bystanders gathering to
        /// watch is what turns a brawl into a scene.
        /// </summary>
        public sealed class WatchState : AiState<CivilianBrain>
        {
            private const float Duration = 4.5f;

            public override string Name => "Watch";

            public override void Enter(CivilianBrain brain)
            {
                brain.Stand();
                brain.Animator?.SetStance(CharacterStance.Relaxed);
            }

            public override void Tick(CivilianBrain brain, float dt)
            {
                if (!brain.HasThreat)
                {
                    brain.Machine.Change(Stroll);
                    return;
                }

                float distance = brain.ThreatDistance;

                if (distance < PanicRadius)
                {
                    // It came to them. Watching is over.
                    brain.Machine.Change(brain.IsCornered() ? (AiState<CivilianBrain>)Cower : Flee);
                    return;
                }

                brain.FaceTowards(brain.ThreatPoint - brain.transform.position);

                // Keep edging back, slowly, the way people actually watch trouble.
                if (distance < CalmRadius * 0.75f)
                {
                    Vector3 away = MathUtil.FlatDirection(brain.transform.position - brain.ThreatPoint);
                    brain.SetDesiredVelocity(away * brain.WalkSpeed * 0.45f);
                }
                else
                {
                    brain.Stand();
                }

                if (brain.Machine.TimeInState <= Duration) return;

                // Seen enough. Forgetting this particular alarm is what stops the
                // civilian bouncing straight back into a startle on the next tick
                // while the fight is still going; a fresh commotion re-alarms them.
                brain.ClearAlarm();
                brain.Machine.Change(Stroll);
            }
        }
    }
}
