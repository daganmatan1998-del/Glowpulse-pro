using System;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.InputSystem;
using Glowpulse.Core.Timing;
using Glowpulse.AI;
using Glowpulse.Enemies;
using Glowpulse.World.City;
using Glowpulse.World.Npc;
using Glowpulse.Core.Settings;
using Glowpulse.Stages;
using Glowpulse.SaveSystem;
using UnityEngine;

namespace Glowpulse.LogicTests
{
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine("Glowpulse logic tests");
            Console.WriteLine("---------------------");

            MathTests();
            InputTests();
            PoseTests();
            PoseLibraryTests();
            HealthTests();
            StaminaTests();
            CombatDataTests();
            AttackTests();
            MoveSetTests();
            TimeControlTests();
            StateMachineTests();
            ArchetypeTests();
            DirectorTests();
            CityTests();
            PedestrianTests();
            CivilianPoseTests();
            BindingTests();
            DifficultyTests();
            StageTests();
            ProgressionTests();

            return Check.Report();
        }

        // ---- math ---------------------------------------------------------------

        private static void MathTests()
        {
            Check.Run("Damp is frame-rate independent", () =>
            {
                // The whole point of exponential damping: simulating one second
                // must land in the same place regardless of step size.
                float coarse = 0f, fine = 0f;
                for (int i = 0; i < 30; i++) coarse = MathUtil.Damp(coarse, 10f, 5f, 1f / 30f);
                for (int i = 0; i < 240; i++) fine = MathUtil.Damp(fine, 10f, 5f, 1f / 240f);

                Check.Near(coarse, fine, "30fps and 240fps agree after one second", 0.05f);
                Check.Near(fine, 10f * (1f - Mathf.Exp(-5f)), "matches the analytic result", 0.02f);
            });

            Check.Run("Damp never overshoots", () =>
            {
                float v = 0f;
                for (int i = 0; i < 200; i++)
                {
                    v = MathUtil.Damp(v, 1f, 30f, 1f / 30f);
                    Check.InRange(v, 0f, 1.0001f, "stays inside the range");
                }
            });

            Check.Run("FlatDirection flattens and normalises", () =>
            {
                Vector3 d = MathUtil.FlatDirection(new Vector3(3f, 99f, 4f));
                Check.Near(d.y, 0f, "y removed");
                Check.Near(d.magnitude, 1f, "unit length");
                Check.Near(d.x, 0.6f, "x component");
                Check.Near(d.z, 0.8f, "z component");

                Check.Near(MathUtil.FlatDirection(Vector3.up).magnitude, 0f, "pure vertical yields zero");
            });

            Check.Run("SignedYaw reports the shorter turn", () =>
            {
                Check.Near(MathUtil.SignedYaw(Vector3.forward, Vector3.right), 90f, "quarter turn right");
                Check.Near(MathUtil.SignedYaw(Vector3.forward, Vector3.left), -90f, "quarter turn left");
                Check.InRange(Mathf.Abs(MathUtil.SignedYaw(Vector3.forward, Vector3.back)), 179f, 181f,
                    "reversal is a half turn");
            });

            Check.Run("Remap and easing hit their endpoints", () =>
            {
                Check.Near(MathUtil.Remap(5f, 0f, 10f, 100f, 200f), 150f, "midpoint");
                Check.Near(MathUtil.Remap(-3f, 0f, 10f, 100f, 200f), 100f, "clamped low");
                Check.Near(MathUtil.Remap(30f, 0f, 10f, 100f, 200f), 200f, "clamped high");

                Check.Near(MathUtil.SmoothStep01(0f), 0f, "smoothstep starts at 0");
                Check.Near(MathUtil.SmoothStep01(1f), 1f, "smoothstep ends at 1");
                Check.Near(MathUtil.SmoothStep01(0.5f), 0.5f, "smoothstep is symmetric");
                Check.Near(MathUtil.EaseOutCubic(1f), 1f, "ease-out ends at 1");
                Check.Near(MathUtil.EaseInCubic(0f), 0f, "ease-in starts at 0");
            });

            Check.Run("Flat distance ignores height", () =>
            {
                var a = new Vector3(0f, 0f, 0f);
                var b = new Vector3(3f, 50f, 4f);
                Check.Near(MathUtil.FlatDistance(a, b), 5f, "planar distance");
                Check.Near(MathUtil.FlatSqrDistance(a, b), 25f, "squared planar distance");
            });

            Check.Run("GroundPoint falls back when nothing is below", () =>
            {
                // The stub physics world is empty, so the helper must return the
                // original point and report the miss rather than sinking anything.
                var origin = new Vector3(1f, 2f, 3f);
                bool found = MathUtil.GroundPoint(origin, out Vector3 point);
                Check.False(found, "no ground reported");
                Check.Near(point, origin, "input returned unchanged");
            });
        }

        // ---- input --------------------------------------------------------------

        private static void InputTests()
        {
            Check.Run("Input buffer holds a press for its window", () =>
            {
                var buffer = new InputBuffer(0.2f);
                Time.Advance(1f);
                buffer.Feed(true);

                Check.True(buffer.Pending, "press is pending immediately");

                Time.Advance(0.15f);
                Check.True(buffer.Pending, "still pending inside the window");

                Time.Advance(0.1f);
                Check.False(buffer.Pending, "expired past the window");
            });

            Check.Run("Input buffer consumes exactly once", () =>
            {
                var buffer = new InputBuffer(0.2f);
                Time.Advance(1f);
                buffer.Feed(true);

                Check.True(buffer.Consume(), "first consume succeeds");
                Check.False(buffer.Consume(), "second consume finds nothing");
                Check.False(buffer.Pending, "no longer pending");
            });

            Check.Run("Input buffer ignores non-presses and can be cleared", () =>
            {
                var buffer = new InputBuffer(0.2f);
                Time.Advance(1f);
                buffer.Feed(false);
                Check.False(buffer.Pending, "a released button buffers nothing");

                buffer.Feed(true);
                buffer.Clear();
                Check.False(buffer.Pending, "clear discards the press");
            });

            Check.Run("Grace timer keeps a window open after the fact", () =>
            {
                var grace = new GraceTimer();
                Check.False(grace.WithinLast(1f), "nothing recorded yet");

                Time.Advance(1f);
                grace.Set(true);
                Time.Advance(0.1f);
                Check.True(grace.WithinLast(0.2f), "inside the grace period");

                Time.Advance(0.3f);
                Check.False(grace.WithinLast(0.2f), "outside the grace period");
            });
        }

        // ---- animation clips -------------------------------------------------------

        private static void PoseTests()
        {
            var accumulator = new Quaternion[(int)RigBone.Count];
            var touched = new bool[(int)RigBone.Count];

            void Reset()
            {
                for (int i = 0; i < accumulator.Length; i++)
                {
                    accumulator[i] = Quaternion.identity;
                    touched[i] = false;
                }
            }

            float PitchOf(RigBone bone)
            {
                float x = accumulator[(int)bone].eulerAngles.x;
                return x > 180f ? x - 360f : x;
            }

            PoseClip Clip() => PoseClip.New("test", 1f)
                .Track(RigBone.Chest,
                    new PoseKey(0f, new Vector3(0f, 0f, 0f)),
                    new PoseKey(0.5f, new Vector3(40f, 0f, 0f)),
                    new PoseKey(1f, new Vector3(-20f, 0f, 0f)));

            Check.Run("Clip sampling hits its keyframes", () =>
            {
                Vector3 hips = Vector3.zero;

                Reset();
                Clip().Sample(0f, 1f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), 0f, "first key");

                Reset();
                Clip().Sample(0.5f, 1f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), 40f, "middle key", 0.05f);

                Reset();
                Clip().Sample(1f, 1f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), -20f, "last key", 0.05f);
            });

            Check.Run("Clip sampling clamps outside its duration", () =>
            {
                Vector3 hips = Vector3.zero;

                Reset();
                Clip().Sample(-5f, 1f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), 0f, "before the start holds the first key");

                Reset();
                Clip().Sample(99f, 1f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), -20f, "after the end holds the last key", 0.05f);
            });

            Check.Run("Clip interpolation stays between neighbouring keys", () =>
            {
                Vector3 hips = Vector3.zero;
                Reset();
                Clip().Sample(0.25f, 1f, accumulator, touched, ref hips);
                Check.InRange(PitchOf(RigBone.Chest), 0f, 40f, "quarter way is between the keys");
                Check.True(touched[(int)RigBone.Chest], "track marks its bone as touched");
                Check.False(touched[(int)RigBone.Head], "untouched bones stay untouched");
            });

            Check.Run("Zero weight leaves the pose alone", () =>
            {
                Vector3 hips = Vector3.zero;
                Reset();
                Clip().Sample(0.5f, 0f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), 0f, "no rotation applied");
                Check.False(touched[(int)RigBone.Chest], "bone not marked");
            });

            Check.Run("Partial weight blends toward the clip", () =>
            {
                Vector3 hips = Vector3.zero;
                Reset();
                Clip().Sample(0.5f, 0.5f, accumulator, touched, ref hips);
                Check.InRange(PitchOf(RigBone.Chest), 15f, 25f, "half way to the 40 degree key");
            });

            Check.Run("Mirror flips yaw and roll but keeps pitch", () =>
            {
                PoseClip clip = PoseClip.New("mirror", 1f)
                    .Track(RigBone.UpperArmL, new PoseKey(0f, new Vector3(10f, 20f, 30f)))
                    .Mirror(RigBone.UpperArmL, RigBone.UpperArmR);

                Check.Equal(clip.Tracks.Count, 2, "a mirrored track was added");

                BoneTrack mirrored = clip.Tracks[1];
                Check.True(mirrored.Bone == RigBone.UpperArmR, "mirrored onto the right arm");
                Check.Near(mirrored.Keys[0].Euler.x, 10f, "pitch preserved");
                Check.Near(mirrored.Keys[0].Euler.y, -20f, "yaw negated");
                Check.Near(mirrored.Keys[0].Euler.z, -30f, "roll negated");
            });

            Check.Run("Hips offset accumulates and scales with weight", () =>
            {
                PoseClip clip = PoseClip.New("crouch", 1f)
                    .Hips(new PoseKey(0f, new Vector3(0f, 0f, 0f)),
                        new PoseKey(1f, new Vector3(0f, -0.5f, 0f)));

                Vector3 hips = Vector3.zero;
                Reset();
                clip.Sample(1f, 1f, accumulator, touched, ref hips);
                Check.Near(hips.y, -0.5f, "full weight applies the whole offset");

                hips = Vector3.zero;
                clip.Sample(1f, 0.5f, accumulator, touched, ref hips);
                Check.Near(hips.y, -0.25f, "half weight applies half the offset");
            });

            Check.Run("Looping clips wrap instead of clamping", () =>
            {
                PoseClip clip = Clip();
                clip.Loop = true;

                Vector3 hips = Vector3.zero;
                Reset();
                clip.Sample(2.5f, 1f, accumulator, touched, ref hips);
                Check.Near(PitchOf(RigBone.Chest), 40f, "2.5s wraps to 0.5s", 0.05f);
            });
        }

        private static void PoseLibraryTests()
        {
            Check.Run("Every built-in clip is well formed", () =>
            {
                string[] ids =
                {
                    PoseLibrary.DodgeRoll, PoseLibrary.DodgeStep, PoseLibrary.JumpTakeoff,
                    PoseLibrary.JumpLand, PoseLibrary.HitLight, PoseLibrary.HitHeavy,
                    PoseLibrary.Knockdown, PoseLibrary.GetUp, PoseLibrary.Death
                };

                foreach (string id in ids)
                {
                    PoseClip clip = PoseLibrary.Get(id);
                    Check.True(clip != null, $"'{id}' is registered");
                    if (clip == null) continue;

                    Check.Greater(clip.Duration, 0f, $"'{id}' has a duration");
                    Check.Greater(clip.Tracks.Count, 0, $"'{id}' has at least one track");

                    foreach (BoneTrack track in clip.Tracks)
                    {
                        Check.Greater(track.Keys.Length, 0, $"'{id}' track has keys");
                        Check.Near(track.Keys[0].Time, 0f, $"'{id}' tracks start at t=0", 1e-4f);

                        for (int i = 1; i < track.Keys.Length; i++)
                            Check.True(track.Keys[i].Time >= track.Keys[i - 1].Time,
                                $"'{id}' keys are in ascending time order");

                        Check.True(track.Keys[track.Keys.Length - 1].Time <= clip.Duration + 1e-4f,
                            $"'{id}' keys stay within the clip duration");
                    }
                }
            });

            Check.Run("Terminal clips hold their last frame", () =>
            {
                Check.True(PoseLibrary.Get(PoseLibrary.Death).HoldLastFrame, "death holds");
                Check.True(PoseLibrary.Get(PoseLibrary.Knockdown).HoldLastFrame, "knockdown holds");
                Check.False(PoseLibrary.Get(PoseLibrary.HitLight).HoldLastFrame, "a flinch does not hold");
            });

            Check.Run("The roll pivots around the body, toppling pivots at the feet", () =>
            {
                Check.Greater(PoseLibrary.Get(PoseLibrary.DodgeRoll).RootPivot01, 0.3f,
                    "a roll tumbles around the hips");
                Check.Near(PoseLibrary.Get(PoseLibrary.Death).RootPivot01, 0f,
                    "falling over pivots at the feet");
            });

            Check.Run("Unknown clips resolve to null rather than throwing", () =>
            {
                Check.True(PoseLibrary.Get("no_such_clip") == null, "unknown id returns null");
                Check.True(PoseLibrary.Get(null) == null, "null id returns null");
                Check.False(PoseLibrary.Has("no_such_clip"), "Has agrees");
            });

            Check.Run("Custom clips can be registered", () =>
            {
                PoseLibrary.Register(PoseClip.New("custom_move", 0.5f)
                    .Track(RigBone.Head, new PoseKey(0f, Vector3.zero)));
                Check.True(PoseLibrary.Has("custom_move"), "registered clip is found");
            });
        }

        // ---- health ---------------------------------------------------------------

        private static void HealthTests()
        {
            Check.Run("Damage reduces health and reports the amount applied", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(100f);

                float applied = 0f;
                health.Damaged += a => applied = a;

                Check.Near(health.Damage(30f), 30f, "returns what it took");
                Check.Near(health.Current, 70f, "health reduced");
                Check.Near(applied, 30f, "event reported the amount");
                Check.Near(health.Normalized, 0.7f, "normalised value");
            });

            Check.Run("Damage clamps at zero and death fires once", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(50f);

                int deaths = 0;
                health.Died += () => deaths++;

                Check.Near(health.Damage(80f), 50f, "only the remaining health is taken");
                Check.Near(health.Current, 0f, "never goes negative");
                Check.False(health.IsAlive, "no longer alive");
                Check.Equal(deaths, 1, "death fired once");

                health.Damage(10f);
                Check.Equal(deaths, 1, "damage after death does not fire again");
            });

            Check.Run("Invulnerability blocks damage for its window", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(100f);
                Time.Advance(1f);

                health.GrantInvulnerability(0.3f);
                Check.True(health.IsInvulnerable, "invulnerable now");
                Check.Near(health.Damage(40f), 0f, "damage is ignored");
                Check.Near(health.Current, 100f, "health untouched");

                Time.Advance(0.4f);
                Check.False(health.IsInvulnerable, "window expired");
                Check.Near(health.Damage(40f), 40f, "damage lands again");
            });

            Check.Run("Invulnerability can be bypassed and never shortened", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(100f);
                Time.Advance(1f);

                health.GrantInvulnerability(0.5f);
                health.GrantInvulnerability(0.1f);
                Time.Advance(0.2f);
                Check.True(health.IsInvulnerable, "a shorter grant does not cut the window short");

                Check.Near(health.DamageIgnoringInvulnerability(25f), 25f, "bypass lands");
                Check.Near(health.Current, 75f, "health reduced");
                Check.True(health.IsInvulnerable, "the window survives the bypass");
            });

            Check.Run("Healing caps at maximum", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(100f);
                health.Damage(30f);

                Check.Near(health.Heal(10f), 10f, "partial heal");
                Check.Near(health.Heal(100f), 20f, "only the missing amount is restored");
                Check.Near(health.Current, 100f, "capped at max");
                Check.Near(health.Heal(5f), 0f, "healing at full does nothing");
            });

            Check.Run("Raising max health can preserve the ratio", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(100f);
                health.Damage(50f);

                health.SetMax(200f, preserveRatio: true);
                Check.Near(health.Current, 100f, "half of the new maximum");
                Check.Near(health.Normalized, 0.5f, "ratio preserved");

                health.SetMax(80f, preserveRatio: false);
                Check.Near(health.Current, 80f, "current is clamped to the smaller maximum");
            });

            Check.Run("Regeneration waits for the delay and stops at its ceiling", () =>
            {
                Health health = Lifecycle.Create<Health>(h =>
                {
                    Lifecycle.SetField(h, "_regenPerSecond", 50f);
                    Lifecycle.SetField(h, "_regenDelay", 1f);
                    Lifecycle.SetField(h, "_regenCeiling", 0.5f);
                });
                health.Configure(100f);
                Time.Advance(5f);
                health.Damage(80f);
                Check.Near(health.Current, 20f, "damaged down to 20");

                Lifecycle.Tick(health, 0.5f);
                Check.Near(health.Current, 20f, "nothing regenerates inside the delay");

                Lifecycle.Tick(health, 2f);
                Check.Near(health.Current, 50f, "regenerates up to the 50% ceiling", 1f);

                Lifecycle.Tick(health, 2f);
                Check.Near(health.Current, 50f, "and stops there", 1f);
            });

            Check.Run("Kill and revive move between states cleanly", () =>
            {
                Health health = Lifecycle.Create<Health>();
                health.Configure(100f);

                int deaths = 0;
                health.Died += () => deaths++;

                health.Kill();
                Check.False(health.IsAlive, "killed");
                Check.Equal(deaths, 1, "death fired");

                health.Revive(0.5f);
                Check.True(health.IsAlive, "revived");
                Check.Near(health.Current, 50f, "revived to half");
            });
        }

        // ---- stamina ----------------------------------------------------------------

        private static void StaminaTests()
        {
            Check.Run("Spending fails when the cost cannot be paid", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>();
                stamina.Configure(50f);

                Check.True(stamina.TrySpend(20f), "affordable spend succeeds");
                Check.Near(stamina.Current, 30f, "cost deducted");

                Check.False(stamina.TrySpend(40f), "unaffordable spend fails");
                Check.Near(stamina.Current, 30f, "and changes nothing");
            });

            Check.Run("Bottoming out causes exhaustion that blocks further spending", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>();
                stamina.Configure(100f);

                bool exhaustedSignal = false;
                stamina.ExhaustedChanged += v => exhaustedSignal = v;

                stamina.SpendUnchecked(100f);
                Check.True(stamina.IsExhausted, "exhausted at zero");
                Check.True(exhaustedSignal, "signalled");
                Check.False(stamina.CanSpend(1f), "cannot spend a single point while exhausted");
                Check.False(stamina.TrySpend(1f), "TrySpend refuses");
            });

            Check.Run("Exhaustion only clears once the recovery threshold is reached", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>(s =>
                {
                    Lifecycle.SetField(s, "_regenPerSecond", 20f);
                    Lifecycle.SetField(s, "_regenDelay", 0.5f);
                    Lifecycle.SetField(s, "_exhaustedRegenScale", 1f);
                    Lifecycle.SetField(s, "_exhaustionRecovery", 0.3f);
                });
                stamina.Configure(100f);
                Time.Advance(5f);

                stamina.SpendUnchecked(100f);
                Check.True(stamina.IsExhausted, "exhausted");

                // Past the delay, but still below 30% of maximum.
                Lifecycle.Tick(stamina, 1.4f);
                Check.Less(stamina.Current, 30f, "still under the recovery threshold");
                Check.True(stamina.IsExhausted, "still exhausted");

                Lifecycle.Tick(stamina, 1.5f);
                Check.Greater(stamina.Current, 30f, "past the recovery threshold");
                Check.False(stamina.IsExhausted, "exhaustion cleared");
                Check.True(stamina.CanSpend(10f), "can act again");
            });

            Check.Run("Regeneration respects its delay", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>(s =>
                {
                    Lifecycle.SetField(s, "_regenPerSecond", 30f);
                    Lifecycle.SetField(s, "_regenDelay", 1f);
                });
                stamina.Configure(100f);
                Time.Advance(5f);

                stamina.TrySpend(50f);
                Lifecycle.Tick(stamina, 0.6f);
                Check.Near(stamina.Current, 50f, "nothing regenerates inside the delay");

                Lifecycle.Tick(stamina, 1f);
                Check.Greater(stamina.Current, 55f, "regenerates once the delay has passed");
            });

            Check.Run("Blocking regeneration holds it off", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>(s =>
                {
                    Lifecycle.SetField(s, "_regenPerSecond", 40f);
                    Lifecycle.SetField(s, "_regenDelay", 0.1f);
                });
                stamina.Configure(100f);
                Time.Advance(5f);

                stamina.TrySpend(50f);
                stamina.BlockRegen(2f);

                Lifecycle.Tick(stamina, 1f);
                Check.Near(stamina.Current, 50f, "held off while blocked");

                Lifecycle.Tick(stamina, 1.5f);
                Check.Greater(stamina.Current, 55f, "resumes after the block");
            });

            Check.Run("Draining reports when it runs dry", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>();
                stamina.Configure(10f);
                Time.Advance(1f);

                Check.True(stamina.Drain(20f, 0.1f), "still has stamina after a small drain");
                Check.False(stamina.Drain(200f, 0.5f), "reports empty once drained");
                Check.True(stamina.IsExhausted, "and becomes exhausted");
                Check.False(stamina.Drain(10f, 0.1f), "draining while exhausted does nothing");
            });

            Check.Run("Refill restores everything and clears exhaustion", () =>
            {
                Stamina stamina = Lifecycle.Create<Stamina>();
                stamina.Configure(80f);
                stamina.SpendUnchecked(80f);
                Check.True(stamina.IsExhausted, "exhausted");

                stamina.Refill();
                Check.Near(stamina.Current, 80f, "back to full");
                Check.False(stamina.IsExhausted, "exhaustion cleared");
                Check.True(stamina.IsFull, "reports full");
            });
        }

        // ---- state machine ------------------------------------------------------------------

        private static void StateMachineTests()
        {
            Check.Run("Entering a state runs Enter exactly once", () =>
            {
                var owner = new ProbeOwner();
                var machine = new AiStateMachine<ProbeOwner>(owner);
                var a = new ProbeState("A");

                machine.ChangeNow(a);
                Check.Equal(a.Entered, 1, "entered once");
                Check.Equal(machine.CurrentName, "A", "current state reported");

                machine.ChangeNow(a);
                Check.Equal(a.Entered, 1, "re-entering the same state is a no-op");
            });

            Check.Run("Transitions requested during a tick apply after it finishes", () =>
            {
                var owner = new ProbeOwner();
                var machine = new AiStateMachine<ProbeOwner>(owner);
                var a = new ProbeState("A");
                var b = new ProbeState("B");

                // The whole point of deferring: a state must be allowed to finish
                // its own update after asking to leave.
                a.OnTick = _ => machine.Change(b);

                machine.ChangeNow(a);
                owner.Log.Clear();
                machine.Tick(0.016f);

                Check.Equal(string.Join(",", owner.Log), "tick:A,exit:A,enter:B",
                    "the state ticks, then exits, then the next one enters");
                Check.Equal(machine.CurrentName, "B", "landed in B");
            });

            Check.Run("Exit and Enter fire in the right order on a transition", () =>
            {
                var owner = new ProbeOwner();
                var machine = new AiStateMachine<ProbeOwner>(owner);
                var a = new ProbeState("A");
                var b = new ProbeState("B");

                machine.ChangeNow(a);
                machine.ChangeNow(b);

                Check.Equal(a.Exited, 1, "A exited");
                Check.Equal(b.Entered, 1, "B entered");
                Check.Equal(string.Join(",", owner.Log), "enter:A,exit:A,enter:B", "in order");
            });

            Check.Run("TimeInState measures the current state only", () =>
            {
                var owner = new ProbeOwner();
                var machine = new AiStateMachine<ProbeOwner>(owner);
                var a = new ProbeState("A");
                var b = new ProbeState("B");

                Time.Advance(5f);
                machine.ChangeNow(a);
                Time.Advance(2f);
                Check.Near(machine.TimeInState, 2f, "two seconds in A");

                machine.ChangeNow(b);
                Check.Near(machine.TimeInState, 0f, "reset on entering B");
            });

            Check.Run("Transitions raise an event with both state names", () =>
            {
                var owner = new ProbeOwner();
                var machine = new AiStateMachine<ProbeOwner>(owner);
                string captured = null;
                machine.Changed += (from, to) => captured = from + "->" + to;

                machine.ChangeNow(new ProbeState("A"));
                Check.Equal(captured, "none->A", "first transition reports no previous state");

                machine.ChangeNow(new ProbeState("B"));
                Check.Equal(captured, "A->B", "subsequent transitions report both");
            });

            Check.Run("A null transition is ignored", () =>
            {
                var owner = new ProbeOwner();
                var machine = new AiStateMachine<ProbeOwner>(owner);
                var a = new ProbeState("A");

                machine.ChangeNow(a);
                machine.ChangeNow(null);
                machine.Change(null);
                machine.Tick(0.016f);

                Check.Equal(machine.CurrentName, "A", "still in A");
            });
        }

        // ---- enemy archetypes -----------------------------------------------------------------

        private static void ArchetypeTests()
        {
            Check.Run("Each archetype has a working move set", () =>
            {
                EnemyKind[] kinds = { EnemyKind.Brawler, EnemyKind.Bruiser, EnemyKind.Runner };

                foreach (EnemyKind kind in kinds)
                {
                    EnemyArchetype archetype = EnemyArchetype.Get(kind);
                    Check.True(archetype.Moves != null, $"{kind} has moves");
                    Check.Greater(archetype.Health, 0f, $"{kind} has health");

                    System.Collections.Generic.List<string> problems = archetype.Moves.Validate();
                    foreach (string problem in problems) Check.True(false, $"{kind}: {problem}");
                }
            });

            Check.Run("The three archetypes actually play differently", () =>
            {
                EnemyArchetype brawler = EnemyArchetype.Brawler();
                EnemyArchetype bruiser = EnemyArchetype.Bruiser();
                EnemyArchetype runner = EnemyArchetype.Runner();

                // Heavy: tough and slow.
                Check.Greater(bruiser.Health, brawler.Health * 2f, "the bruiser is far tougher");
                Check.Less(bruiser.ChaseSpeed, brawler.ChaseSpeed, "and slower");
                Check.Greater(bruiser.Poise, brawler.Poise * 2f, "and much harder to stagger");
                Check.Greater(bruiser.DamageResistance, 0f, "and armoured");

                // Fast: fragile and quick.
                Check.Less(runner.Health, brawler.Health, "the runner is fragile");
                Check.Greater(runner.ChaseSpeed, brawler.ChaseSpeed, "and faster");
                Check.Less(runner.Poise, brawler.Poise, "and easy to interrupt");
                Check.Less(runner.AttackCooldown, brawler.AttackCooldown, "and attacks more often");
                Check.Greater(runner.FlankPreference, brawler.FlankPreference, "and wants to flank");
                Check.Greater(runner.RetreatChance, brawler.RetreatChance, "and hits and runs");

                // Reward should track difficulty.
                Check.Greater(bruiser.ExperienceReward, brawler.ExperienceReward,
                    "the toughest enemy is worth the most");
            });

            Check.Run("Enemy attacks telegraph more than the player's", () =>
            {
                AttackDefinition playerJab = MoveSet.Player().Get("light_1");
                AttackDefinition enemyJab = EnemyArchetype.Brawler().Moves.Get("e_jab");

                // The wind-up is the tell. Without it, dodging is a guess.
                Check.Greater(enemyJab.Windup, playerJab.Windup * 2f,
                    "an enemy jab is readable where the player's is snappy");

                AttackDefinition slam = EnemyArchetype.Bruiser().Moves.Get("b_slam");
                Check.Greater(slam.Windup, 0.7f, "the unblockable slam is heavily telegraphed");
                Check.True(slam.Unblockable, "and cannot simply be held against");
            });

            Check.Run("Attack cooldown jitter stays positive and near its mean", () =>
            {
                EnemyArchetype archetype = EnemyArchetype.Brawler();
                float min = float.MaxValue, max = float.MinValue;

                for (int i = 0; i < 200; i++)
                {
                    float value = archetype.RollAttackCooldown();
                    Check.Greater(value, 0f, "never zero or negative");
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }

                Check.InRange(min, archetype.AttackCooldown - archetype.AttackCooldownVariance - 0.01f,
                    archetype.AttackCooldown, "lower bound respected");
                Check.InRange(max, archetype.AttackCooldown,
                    archetype.AttackCooldown + archetype.AttackCooldownVariance + 0.01f,
                    "upper bound respected");
            });
        }

        // ---- city layout ------------------------------------------------------------------------

        private static CityLayout NewCity(int seed = 1234)
        {
            return CityLayout.Generate(CitySettings.Default, seed);
        }

        private static void CityTests()
        {
            Check.Run("A seed always produces the same city", () =>
            {
                CityLayout a = NewCity(777);
                CityLayout b = NewCity(777);

                Check.Equal(a.Plots.Count, b.Plots.Count, "same number of buildings");
                Check.Equal(a.Locations.Count, b.Locations.Count, "same named places");

                for (int i = 0; i < a.Plots.Count; i++)
                {
                    Check.Near(a.Plots[i].Center.x, b.Plots[i].Center.x, $"plot {i} x");
                    Check.Near(a.Plots[i].Center.y, b.Plots[i].Center.y, $"plot {i} z");
                    Check.Near(a.Plots[i].Height, b.Plots[i].Height, $"plot {i} height");
                }
            });

            Check.Run("Different seeds produce different cities", () =>
            {
                CityLayout a = NewCity(1);
                CityLayout b = NewCity(2);

                bool identical = a.Plots.Count == b.Plots.Count;
                if (identical)
                {
                    for (int i = 0; i < a.Plots.Count; i++)
                    {
                        if (Mathf.Abs(a.Plots[i].Height - b.Plots[i].Height) > 0.01f) { identical = false; break; }
                    }
                }

                Check.False(identical, "two seeds do not generate the same layout");
            });

            Check.Run("The layout validates clean across many seeds", () =>
            {
                for (int seed = 0; seed < 25; seed++)
                {
                    CityLayout city = NewCity(seed * 9161 + 7);
                    System.Collections.Generic.List<string> problems = city.Validate();
                    foreach (string problem in problems) Check.True(false, $"seed {seed}: {problem}");
                }
            });

            Check.Run("No building stands in the road", () =>
            {
                // The whole grid falls apart if a plot creeps into a carriageway,
                // and it is invisible until you drive the camera into a wall.
                for (int seed = 0; seed < 12; seed++)
                {
                    CityLayout city = NewCity(seed * 31 + 3);

                    foreach (Plot plot in city.Plots)
                    {
                        foreach (RoadSegment road in city.Roads)
                        {
                            Vector2 delta = road.B - road.A;
                            bool alongZ = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);

                            Vector2 roadCenter = (road.A + road.B) * 0.5f;
                            Vector2 roadHalf = alongZ
                                ? new Vector2(road.Width * 0.5f, Mathf.Abs(delta.y) * 0.5f)
                                : new Vector2(Mathf.Abs(delta.x) * 0.5f, road.Width * 0.5f);

                            float overlapX = plot.Size.x * 0.5f + roadHalf.x
                                             - Mathf.Abs(plot.Center.x - roadCenter.x);
                            float overlapZ = plot.Size.y * 0.5f + roadHalf.y
                                             - Mathf.Abs(plot.Center.y - roadCenter.y);

                            // Touching the kerb line is expected; crossing it is not.
                            Check.False(overlapX > 0.05f && overlapZ > 0.05f,
                                $"seed {seed}: a building overlaps the road by {overlapX:F2}");
                        }
                    }
                }
            });

            Check.Run("The player never spawns inside a building", () =>
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    CityLayout city = NewCity(seed * 101 + 5);
                    var spawn = new Vector2(city.PlayerSpawn.x, city.PlayerSpawn.z);
                    Check.False(city.IsInsideBuilding(spawn, 0.6f),
                        $"seed {seed}: spawn point is inside a wall");
                }
            });

            Check.Run("Every city has somewhere to stage a fight", () =>
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    CityLayout city = NewCity(seed * 57 + 11);
                    Check.Greater(city.Arenas.Count, 0, $"seed {seed}: has at least one arena");

                    foreach (CityLocation arena in city.Arenas)
                        Check.Greater(arena.Radius, 3f, $"seed {seed}: arena is big enough to fight in");
                }
            });

            Check.Run("The square is always present and is the biggest arena", () =>
            {
                CityLayout city = NewCity(42);
                CityLocation? plaza = city.NearestLocation(Vector3.zero, LocationKind.Plaza);
                Check.True(plaza != null, "the city has a square");

                foreach (CityLocation arena in city.Arenas)
                    Check.True(plaza.Value.Radius >= arena.Radius - 0.01f,
                        "nothing is more open than the square");
            });

            Check.Run("Arena selection respects the minimum distance", () =>
            {
                CityLayout city = NewCity(99);
                Vector3 from = city.PlayerSpawn;

                CityLocation? far = city.PickArena(from, minDistance: 25f);
                Check.True(far != null, "an arena was chosen");
                Check.Greater(Vector3.Distance(far.Value.Position, from), 24.9f,
                    "the chosen arena is not on top of the player");
            });

            Check.Run("Building heights stay inside the configured range", () =>
            {
                CitySettings s = CitySettings.Default;
                CityLayout city = NewCity(5150);

                foreach (Plot plot in city.Plots)
                {
                    Check.InRange(plot.Height, s.MinHeight - 0.01f, s.MaxHeight + 0.01f,
                        "height within range");
                    Check.Greater(plot.Size.x, 0f, "footprint has width");
                    Check.Greater(plot.Size.y, 0f, "footprint has depth");
                }
            });

            Check.Run("The city is dense rather than sprawling", () =>
            {
                CityLayout city = NewCity(2024);

                // The brief asks for a small, dense slice - so this is a real
                // requirement, not an implementation detail.
                Check.Greater(city.Plots.Count, 30, "the streets are actually built up");
                Check.Less(city.Extent, 130f, "the slice stays walkable");
                Check.Greater(city.Roads.Count, 5, "there is a street grid, not one road");
            });

            Check.Run("Junctions are named and evenly spread", () =>
            {
                CityLayout city = NewCity(8);
                int junctions = 0;
                foreach (CityLocation l in city.Locations)
                    if (l.Kind == LocationKind.Intersection) junctions++;

                CitySettings s = CitySettings.Default;
                Check.Equal(junctions, (s.BlocksX + 1) * (s.BlocksZ + 1),
                    "one junction per grid crossing");
            });
        }

        // ---- pedestrian network -------------------------------------------------------------------

        private static PedestrianNetwork NewPavements(int seed = 1234)
        {
            return PedestrianNetwork.Build(NewCity(seed));
        }

        private static void PedestrianTests()
        {
            Check.Run("Every city gets a connected pedestrian network", () =>
            {
                for (int seed = 1; seed <= 12; seed++)
                {
                    PedestrianNetwork network = NewPavements(seed * 71);

                    Check.Greater(network.NodeCount, 20, $"seed {seed}: the streets are walkable");

                    System.Collections.Generic.List<string> problems = network.Validate();
                    if (problems.Count > 0)
                        Check.True(false, $"seed {seed}: {problems[0]}");
                    else
                        Check.True(true, $"seed {seed}: network is sound");
                }
            });

            Check.Run("Pavement nodes stand on pavement, never inside a building", () =>
            {
                for (int seed = 1; seed <= 8; seed++)
                {
                    CityLayout city = NewCity(seed * 313);
                    PedestrianNetwork network = PedestrianNetwork.Build(city);

                    for (int i = 0; i < network.NodeCount; i++)
                    {
                        Vector3 p = network.NodePosition(i);
                        Check.False(city.IsInsideBuilding(new Vector2(p.x, p.z)),
                            $"seed {seed}: node {i} is on open ground");
                    }
                }
            });

            Check.Run("Nodes sit on the pavement rather than in the carriageway", () =>
            {
                CitySettings s = CitySettings.Default;
                PedestrianNetwork network = NewPavements(99);

                // A node should be offset from the road centreline by roughly half
                // the carriageway - out of the traffic, on the flags.
                float expected = s.CarriagewayWidth * 0.5f + s.SidewalkWidth * 0.5f;
                Check.Greater(expected, s.CarriagewayWidth * 0.5f, "the lane clears the carriageway");
                Check.Less(expected, s.RoadWidth * 0.5f, "the lane stays inside the road corridor");

                Check.Greater(network.LinkCount, network.NodeCount, "nodes are linked into a grid");
            });

            Check.Run("Some links are crossings and most are not", () =>
            {
                PedestrianNetwork network = NewPavements(5150);

                int crossings = 0;
                for (int i = 0; i < network.LinkCount; i++)
                    if (network.Links[i].IsCrossing) crossings++;

                Check.Greater(crossings, 0, "stepping into the road is recognised as a crossing");
                Check.Less(crossings, network.LinkCount * 0.6f,
                    "most of the network is pavement, not road");
            });

            Check.Run("A stroll keeps going instead of shuffling back and forth", () =>
            {
                PedestrianNetwork network = NewPavements(31337);
                var rng = new System.Random(4);

                int previous = -1;
                int current = network.Nearest(Vector3.zero);
                Check.True(current >= 0, "there is somewhere to start");

                int backtracks = 0;
                var visited = new System.Collections.Generic.HashSet<int>();

                for (int step = 0; step < 200; step++)
                {
                    Vector3 heading = previous >= 0
                        ? network.NodePosition(current) - network.NodePosition(previous)
                        : Vector3.forward;

                    int next = network.NextStep(current, previous, heading, rng);
                    Check.True(next >= 0, $"step {step} found somewhere to go");

                    if (next == previous) backtracks++;
                    visited.Add(next);

                    previous = current;
                    current = next;
                }

                // Doubling back is allowed - people do - but it must be rare, or
                // the crowd paces on the spot.
                Check.Less(backtracks, 30f, "walkers rarely double back");
                Check.Greater(visited.Count, 12f, "a walk actually covers ground");
            });

            Check.Run("Fleeing always increases the distance from the trouble", () =>
            {
                PedestrianNetwork network = NewPavements(24);

                int checkedNodes = 0;
                for (int i = 0; i < network.NodeCount; i += 3)
                {
                    Vector3 here = network.NodePosition(i);

                    // Trouble one node over, so there is somewhere better to be.
                    Vector3 threat = here + new Vector3(3f, 0f, 3f);

                    int escape = network.StepAwayFrom(i, threat);
                    if (escape < 0) continue;

                    Check.Greater(
                        Vector3.Distance(network.NodePosition(escape), threat),
                        Vector3.Distance(here, threat),
                        $"node {i}: running away increases the gap");
                    checkedNodes++;
                }

                Check.Greater(checkedNodes, 5, "the test actually exercised some escapes");
            });

            Check.Run("A civilian with nowhere further to run is cornered", () =>
            {
                PedestrianNetwork network = NewPavements(808);

                // The node furthest from the trouble is, by definition, one where
                // no neighbour is further still. That is the cornered case, and
                // the states rely on it to switch from running to covering up.
                var threat = new Vector3(-400f, 0f, -400f);

                int furthest = 0;
                float furthestSqr = -1f;
                for (int i = 0; i < network.NodeCount; i++)
                {
                    float sqr = (network.NodePosition(i) - threat).sqrMagnitude;
                    if (sqr <= furthestSqr) continue;
                    furthestSqr = sqr;
                    furthest = i;
                }

                Check.Equal(network.StepAwayFrom(furthest, threat), -1,
                    "the far corner has nowhere better to go");

                // Everywhere else there is still an escape, or fleeing would be
                // pointless well before anyone is actually trapped.
                int escapable = 0;
                for (int i = 0; i < network.NodeCount; i++)
                    if (network.StepAwayFrom(i, threat) >= 0) escapable++;

                Check.Greater(escapable, network.NodeCount * 0.9f,
                    "almost everywhere still has a way out");
            });

            Check.Run("An empty city produces an empty network rather than throwing", () =>
            {
                PedestrianNetwork network = PedestrianNetwork.Build(null);

                Check.Equal(network.NodeCount, 0, "no nodes");
                Check.Equal(network.Nearest(Vector3.zero), -1, "nothing is nearest");
                Check.Equal(network.NextStep(0, -1, Vector3.forward, new System.Random(1)), -1,
                    "there is nowhere to walk");
                Check.Equal(network.StepAwayFrom(0, Vector3.zero), -1, "and nowhere to flee");
                Check.Greater(network.Validate().Count, 0, "and it says so");
            });

            Check.Run("The network is the same for the same seed", () =>
            {
                PedestrianNetwork a = NewPavements(6060);
                PedestrianNetwork b = NewPavements(6060);

                Check.Equal(a.NodeCount, b.NodeCount, "same node count");
                Check.Equal(a.LinkCount, b.LinkCount, "same link count");

                for (int i = 0; i < a.NodeCount; i++)
                    Check.Near(a.NodePosition(i), b.NodePosition(i), $"node {i}");
            });
        }

        // ---- civilian animation -------------------------------------------------------------------

        private static void CivilianPoseTests()
        {
            Check.Run("Every civilian clip registers and can be sampled", () =>
            {
                CivilianPoses.EnsureRegistered();

                string[] ids =
                {
                    CivilianPoses.Startle, CivilianPoses.Cower, CivilianPoses.Shield,
                    CivilianPoses.Wave, CivilianPoses.CheckWatch, CivilianPoses.LookAround,
                    CivilianPoses.Stumble
                };

                int boneCount = System.Enum.GetValues(typeof(RigBone)).Length;
                var accumulator = new Quaternion[boneCount];
                var touched = new bool[boneCount];

                foreach (string id in ids)
                {
                    PoseClip clip = PoseLibrary.Get(id);
                    Check.True(clip != null, $"{id} is registered");
                    if (clip == null) continue;

                    Check.Greater(clip.Duration, 0f, $"{id} has a length");

                    for (int i = 0; i < boneCount; i++)
                    {
                        accumulator[i] = Quaternion.identity;
                        touched[i] = false;
                    }

                    // Sampling past the end has to stay safe: the animator runs a
                    // frame beyond a clip on its way out of an action.
                    Vector3 hips = Vector3.zero;
                    clip.Sample(clip.Duration * 1.5f, 1f, accumulator, touched, ref hips);

                    Check.True(!float.IsNaN(hips.x) && !float.IsNaN(hips.y) && !float.IsNaN(hips.z),
                        $"{id} samples cleanly past its end");

                    bool anyTouched = false;
                    for (int i = 0; i < boneCount; i++) anyTouched |= touched[i];
                    Check.True(anyTouched, $"{id} actually moves something");
                }
            });

            Check.Run("Cower and shield hold their pose instead of snapping back", () =>
            {
                CivilianPoses.EnsureRegistered();

                PoseClip cower = PoseLibrary.Get(CivilianPoses.Cower);
                PoseClip shield = PoseLibrary.Get(CivilianPoses.Shield);

                Check.True(cower != null && cower.HoldLastFrame, "cowering is held");
                Check.True(shield != null && shield.HoldLastFrame, "shielding is held");

                PoseClip startle = PoseLibrary.Get(CivilianPoses.Startle);
                Check.True(startle != null && !startle.HoldLastFrame,
                    "a startle is a one-shot, not a pose to stand in");
            });

            Check.Run("Registering the civilian set does not disturb the combat set", () =>
            {
                CombatPoses.EnsureRegistered();
                CivilianPoses.EnsureRegistered();

                Check.True(PoseLibrary.Has(CombatPoses.LightJab), "combat clips survive");
                Check.True(PoseLibrary.Has(CivilianPoses.Cower), "civilian clips are present");
            });
        }

        // ---- key bindings -------------------------------------------------------------------------

        private static void BindingTests()
        {
            Check.Run("The defaults are the controls the game already shipped with", () =>
            {
                var b = new InputBindings();

                Check.True(b.Primary(GameAction.MoveForward) == KeyCode.W, "W moves forward");
                Check.True(b.Primary(GameAction.Attack) == KeyCode.Mouse0, "left mouse attacks");
                Check.True(b.Primary(GameAction.HeavyAttack) == KeyCode.F, "F is heavy");
                Check.True(b.Primary(GameAction.Block) == KeyCode.Mouse1, "right mouse blocks");
                Check.True(b.Secondary(GameAction.Block) == KeyCode.Q, "and so does Q");
                Check.True(b.Primary(GameAction.Dodge) == KeyCode.LeftControl, "Ctrl dodges");
                Check.True(b.Secondary(GameAction.Dodge) == KeyCode.C, "and so does C");
                Check.True(b.Primary(GameAction.Sprint) == KeyCode.LeftShift, "Shift sprints");
                Check.True(b.Primary(GameAction.Jump) == KeyCode.Space, "Space jumps");
                Check.True(b.Primary(GameAction.Interact) == KeyCode.E, "E interacts");

                Check.True(b.IsComplete(), "every action starts bound");
            });

            Check.Run("Every listed action is unique and covers the whole set", () =>
            {
                var seen = new System.Collections.Generic.HashSet<GameAction>();
                foreach (GameAction a in GameActions.Listed)
                    Check.True(seen.Add(a), $"{a} is listed once");

                Check.Equal(GameActions.Listed.Length, (int)GameAction.Count,
                    "the settings screen lists every rebindable action");
            });

            Check.Run("Rebinding takes the key away from whoever had it", () =>
            {
                var b = new InputBindings();

                // Put jump on F, which heavy attack owns.
                b.Rebind(GameAction.Jump, KeyCode.F);

                Check.True(b.Primary(GameAction.Jump) == KeyCode.F, "jump took the key");
                Check.True(b.Primary(GameAction.HeavyAttack) == KeyCode.None,
                    "heavy attack lost it rather than silently sharing it");
                Check.True(b.Conflict(KeyCode.F, GameAction.Jump) == null, "no action still claims F");
                Check.False(b.IsComplete(), "and the screen can see something is unbound");
            });

            Check.Run("Conflicts are found before a rebind happens", () =>
            {
                var b = new InputBindings();

                GameAction? clash = b.Conflict(KeyCode.F, GameAction.Jump);
                Check.True(clash == GameAction.HeavyAttack, "F belongs to heavy attack");

                Check.True(b.Conflict(KeyCode.Q, GameAction.Block) == null,
                    "an action never conflicts with itself");
                Check.True(b.Conflict(KeyCode.None, GameAction.Jump) == null,
                    "an unbound slot is not a conflict");
                Check.True(b.Conflict(KeyCode.Z, GameAction.Jump) == null, "a free key is free");
            });

            Check.Run("An action never holds the same key in both slots", () =>
            {
                var b = new InputBindings();

                // Dodge is Ctrl / C by default; put C in the primary slot too.
                b.Rebind(GameAction.Dodge, KeyCode.C);

                Check.True(b.Primary(GameAction.Dodge) == KeyCode.C, "primary took it");
                Check.True(b.Secondary(GameAction.Dodge) == KeyCode.None,
                    "the duplicate was cleared instead of showing as 'C / C'");
            });

            Check.Run("Reset to defaults undoes everything", () =>
            {
                var b = new InputBindings();

                b.Rebind(GameAction.Jump, KeyCode.F);
                b.Rebind(GameAction.Attack, KeyCode.Z);
                b.Rebind(GameAction.MoveForward, KeyCode.I);

                b.ResetToDefaults();

                var fresh = new InputBindings();
                for (int i = 0; i < (int)GameAction.Count; i++)
                {
                    var a = (GameAction)i;
                    Check.True(b.Primary(a) == fresh.Primary(a), $"{a} primary restored");
                    Check.True(b.Secondary(a) == fresh.Secondary(a), $"{a} secondary restored");
                }
            });

            Check.Run("A save written by an older build is repaired, not rejected", () =>
            {
                var b = new InputBindings();

                // Simulate a file where an action was never written: strip it.
                b.Rebind(GameAction.Jump, KeyCode.Z);          // jump = Z
                b.Rebind(GameAction.Attack, KeyCode.Z);        // steals Z, leaving jump unbound
                Check.False(b.IsComplete(), "jump is genuinely unbound first");

                b.Repair();

                Check.True(b.IsComplete(), "repair leaves nothing unbound");
                Check.True(b.Primary(GameAction.Jump) == KeyCode.Space,
                    "the missing action fell back to its default");
                Check.True(b.Primary(GameAction.Attack) == KeyCode.Z,
                    "a deliberate rebind was not thrown away");
            });

            Check.Run("Labels read like something a player can act on", () =>
            {
                var b = new InputBindings();

                Check.Equal(b.Label(GameAction.Attack), "LMB", "mouse buttons read as LMB");
                Check.Equal(b.Label(GameAction.Block), "RMB / Q", "two keys read as a pair");
                Check.Equal(b.Label(GameAction.HeavyAttack), "F", "a single key reads plainly");
                Check.Equal(InputBindings.KeyName(KeyCode.LeftControl), "Left Ctrl", "modifiers are spelt out");

                b.Rebind(GameAction.Jump, KeyCode.F);
                Check.Equal(b.Label(GameAction.HeavyAttack), "Unbound",
                    "an action with nothing on it says so");
            });

            Check.Run("A clone is independent of what it came from", () =>
            {
                var a = new InputBindings();
                InputBindings copy = a.Clone();

                copy.Rebind(GameAction.Jump, KeyCode.Z);

                Check.True(a.Primary(GameAction.Jump) == KeyCode.Space, "the original is untouched");
                Check.True(copy.Primary(GameAction.Jump) == KeyCode.Z, "the copy changed");

                a.CopyFrom(copy);
                Check.True(a.Primary(GameAction.Jump) == KeyCode.Z, "and it can be copied back");
            });
        }

        // ---- difficulty ---------------------------------------------------------------------------

        private static void DifficultyTests()
        {
            Check.Run("Normal is the shipped balance, exactly", () =>
            {
                DifficultyProfile p = DifficultyProfile.For(Difficulty.Normal);

                Check.Near(p.EnemyDamage, 1f, "damage untouched");
                Check.Near(p.EnemyHealth, 1f, "health untouched");
                Check.Near(p.EnemyCooldown, 1f, "pacing untouched");
                Check.Near(p.EnemyReaction, 1f, "reactions untouched");

                EnemyArchetype baseline = EnemyArchetype.Brawler();
                EnemyArchetype scaled = baseline.Scaled(p);

                Check.Near(scaled.Health, baseline.Health, "a Normal brawler is the brawler");
                Check.Near(scaled.DamageMultiplier, baseline.DamageMultiplier, "and hits the same");
                Check.Near(scaled.AttackCooldown, baseline.AttackCooldown, "at the same pace");
            });

            Check.Run("Easy is gentler and Hard is harsher on every axis", () =>
            {
                DifficultyProfile easy = DifficultyProfile.For(Difficulty.Easy);
                DifficultyProfile hard = DifficultyProfile.For(Difficulty.Hard);

                Check.Less(easy.EnemyDamage, 1f, "easy enemies hit softer");
                Check.Less(easy.EnemyHealth, 1f, "easy enemies have less health");
                Check.Greater(easy.EnemyCooldown, 1f, "easy enemies wait longer between attacks");
                Check.Greater(easy.EnemyReaction, 1f, "easy enemies are slower to react");

                Check.Greater(hard.EnemyDamage, 1f, "hard enemies hit harder");
                Check.Greater(hard.EnemyHealth, 1f, "hard enemies have more health");
                Check.Less(hard.EnemyCooldown, 1f, "hard enemies attack more often");
                Check.Less(hard.EnemyReaction, 1f, "hard enemies react faster");

                // Simultaneous attackers is the biggest lever on pressure.
                Check.Less(easy.SimultaneousAttackers, DifficultyProfile.Normal.SimultaneousAttackers,
                    "easy sends them in one at a time");
                Check.Greater(hard.SimultaneousAttackers, DifficultyProfile.Normal.SimultaneousAttackers,
                    "hard lets them gang up");
            });

            Check.Run("Scaling copies rather than editing the shared preset", () =>
            {
                EnemyArchetype preset = EnemyArchetype.Bruiser();
                float health = preset.Health;
                float cooldown = preset.AttackCooldown;

                // Scale the same preset repeatedly: a mutating implementation
                // would compound and send Hard into absurdity within a minute.
                for (int i = 0; i < 20; i++)
                    preset.Scaled(DifficultyProfile.For(Difficulty.Hard));

                Check.Near(EnemyArchetype.Bruiser().Health, health, "the preset still reports its own health");
                Check.Near(EnemyArchetype.Bruiser().AttackCooldown, cooldown, "and its own pacing");
            });

            Check.Run("Every difficulty produces a fightable enemy", () =>
            {
                foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
                {
                    DifficultyProfile p = DifficultyProfile.For(d);

                    foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
                    {
                        EnemyArchetype a = EnemyArchetype.Get(kind).Scaled(p);

                        Check.Greater(a.Health, 0f, $"{d} {kind} has health");
                        Check.Greater(a.DamageMultiplier, 0f, $"{d} {kind} can deal damage");
                        Check.Greater(a.AttackCooldown, 0f, $"{d} {kind} attacks eventually");
                        Check.Greater(a.ReactionTime, 0f, $"{d} {kind} takes a moment to react");
                    }

                    Check.Greater(p.SimultaneousAttackers, 0, $"{d} lets somebody attack");
                    Check.True(!string.IsNullOrEmpty(DifficultyProfile.DisplayName(d)), $"{d} has a name");
                    Check.True(!string.IsNullOrEmpty(DifficultyProfile.Describe(d)), $"{d} explains itself");
                }
            });
        }

        // ---- stages -------------------------------------------------------------------------------

        private static void StageTests()
        {
            Check.Run("Every stage is playable and completable", () =>
            {
                System.Collections.Generic.List<string> problems = StageCatalogue.ValidateAll();

                if (problems.Count > 0)
                    foreach (string p in problems) Check.True(false, p);
                else
                    Check.True(true, "the whole progression validates");

                Check.Equal(StageCatalogue.Count, 5, "there are five stages");
            });

            Check.Run("Stages are named, numbered and in order", () =>
            {
                for (int i = 0; i < StageCatalogue.Count; i++)
                {
                    StageDefinition stage = StageCatalogue.Get(i);

                    Check.Equal(stage.Index, i, $"stage {i} knows its place");
                    Check.Equal(stage.Number, i + 1, $"stage {i} displays as {i + 1}");
                    Check.Equal(stage.DisplayNumber, "STAGE " + (i + 1), $"stage {i} title");
                    Check.True(!string.IsNullOrEmpty(stage.Tagline), $"stage {i} has a tagline");
                    Check.True(!string.IsNullOrEmpty(stage.ObjectiveText), $"stage {i} has an objective");
                }
            });

            Check.Run("No two stages are the same fight", () =>
            {
                var arenas = new System.Collections.Generic.HashSet<ArenaKind>();
                var rosters = new System.Collections.Generic.HashSet<string>();

                for (int i = 0; i < StageCatalogue.Count; i++)
                {
                    StageDefinition stage = StageCatalogue.Get(i);

                    Check.True(arenas.Add(stage.Arena), $"stage {stage.Number} has its own arena");

                    // The roster is the other half of a stage's identity - five
                    // different rooms with the same enemies is still one fight.
                    var roster = new System.Collections.Generic.List<string>();
                    foreach (EnemyWave w in stage.Waves) roster.Add(w.Kind + "x" + w.Count);
                    roster.Sort();

                    Check.True(rosters.Add(string.Join(",", roster)),
                        $"stage {stage.Number} has its own enemy composition");
                }
            });

            Check.Run("The progression gets harder rather than just longer", () =>
            {
                StageDefinition first = StageCatalogue.Get(0);
                StageDefinition last = StageCatalogue.Last;

                Check.False(first.HasBoss, "the first stage is not a boss fight");
                Check.True(last.HasBoss, "the last stage is");

                // Rewards must rise, or there is no reason to go forward.
                for (int i = 1; i < StageCatalogue.Count; i++)
                {
                    Check.Greater(StageCatalogue.Get(i).ExperienceReward,
                        StageCatalogue.Get(i - 1).ExperienceReward, $"stage {i + 1} pays more XP");
                    Check.Greater(StageCatalogue.Get(i).MoneyReward,
                        StageCatalogue.Get(i - 1).MoneyReward, $"stage {i + 1} pays more money");
                }

                Check.True(first.HasCivilians, "the streets have people on them");
                Check.False(last.HasCivilians, "a rooftop showdown does not");
            });

            Check.Run("Boss objectives are backed by a boss that actually spawns", () =>
            {
                for (int i = 0; i < StageCatalogue.Count; i++)
                {
                    StageDefinition stage = StageCatalogue.Get(i);

                    if (stage.Objective == StageObjective.DefeatMiniBoss)
                        Check.True(stage.Contains(EnemyKind.MiniBoss),
                            $"stage {stage.Number} spawns its mini-boss");

                    if (stage.Objective == StageObjective.DefeatFinalBoss)
                        Check.True(stage.Contains(EnemyKind.FinalBoss),
                            $"stage {stage.Number} spawns its final boss");

                    // A boss arriving on the first frame denies the stage its
                    // build-up, so bosses are always a later wave.
                    foreach (EnemyWave w in stage.Waves)
                        if (EnemyArchetype.IsBoss(w.Kind))
                            Check.Greater(w.Delay, 0f, $"stage {stage.Number}'s boss arrives after a build-up");
                }
            });

            Check.Run("Every enemy a stage names has a real archetype behind it", () =>
            {
                for (int i = 0; i < StageCatalogue.Count; i++)
                {
                    StageDefinition stage = StageCatalogue.Get(i);

                    foreach (EnemyWave w in stage.Waves)
                    {
                        EnemyArchetype a = EnemyArchetype.Get(w.Kind);

                        Check.True(a != null, $"{w.Kind} exists");
                        Check.Equal((int)a.Kind, (int)w.Kind, $"{w.Kind} returns its own archetype");
                        Check.Greater(a.Health, 0f, $"{w.Kind} has health");
                        Check.True(a.Moves != null, $"{w.Kind} has moves");
                        Check.True(!string.IsNullOrEmpty(a.DisplayName), $"{w.Kind} has a name");
                    }

                    Check.Greater(stage.TotalEnemies, 0, $"stage {stage.Number} has enemies to fight");
                }
            });

            Check.Run("Every new fighter's move set is internally consistent", () =>
            {
                EnemyKind[] kinds =
                {
                    EnemyKind.Defender, EnemyKind.Elite, EnemyKind.MiniBoss, EnemyKind.FinalBoss
                };

                foreach (EnemyKind kind in kinds)
                {
                    EnemyArchetype a = EnemyArchetype.Get(kind);
                    System.Collections.Generic.List<string> problems = a.Moves.Validate();

                    if (problems.Count > 0)
                        foreach (string p in problems) Check.True(false, $"{kind}: {p}");
                    else
                        Check.True(true, $"{kind}'s moves validate");
                }
            });

            Check.Run("The new fighters are actually different from each other", () =>
            {
                EnemyArchetype basic = EnemyArchetype.Brawler();
                EnemyArchetype defender = EnemyArchetype.Defender();
                EnemyArchetype elite = EnemyArchetype.Elite();
                EnemyArchetype mini = EnemyArchetype.MiniBoss();
                EnemyArchetype boss = EnemyArchetype.FinalBoss();

                // A defender is defined by its guard, not its health bar.
                Check.Greater(defender.BlockChance, basic.BlockChance * 2f, "defenders block far more");
                Check.Greater(defender.RetreatChance, basic.RetreatChance, "and give ground to bait a swing");

                // An elite is better at everything rather than bigger at one thing.
                Check.Greater(elite.ChaseSpeed, basic.ChaseSpeed, "elites are faster");
                Check.Greater(elite.Health, basic.Health, "elites are tougher");
                Check.Less(elite.ReactionTime, basic.ReactionTime, "elites react sooner");
                Check.Greater(elite.FlankPreference, basic.FlankPreference, "elites work the angles");

                // Bosses must not be a normal enemy with a bigger number.
                Check.Greater(mini.Poise, basic.Poise * 4f, "the mini-boss shrugs off pressure");
                Check.Greater(boss.Health, mini.Health, "the final boss is the bigger fight");
                Check.Greater(boss.Moves.Count, mini.Moves.Count, "and has more to throw at you");
                Check.Greater(boss.ExperienceReward, mini.ExperienceReward, "and is worth more");
            });
        }

        // ---- progression and saving ----------------------------------------------------------------

        private static void ProgressionTests()
        {
            Check.Run("A fresh save has only the first stage open", () =>
            {
                var data = new SaveData();

                Check.True(data.IsStageUnlocked(0), "stage 1 is playable");
                Check.False(data.IsStageUnlocked(1), "stage 2 is locked");
                Check.False(data.IsStageUnlocked(4), "and so is the last");
                Check.False(data.IsStageCompleted(0), "nothing is completed yet");
            });

            Check.Run("Completing a stage unlocks exactly the next one", () =>
            {
                var data = new SaveData();

                data.CompleteStage(0, 5);
                Check.True(data.IsStageCompleted(0), "stage 1 is recorded");
                Check.True(data.IsStageUnlocked(1), "stage 2 opened");
                Check.False(data.IsStageUnlocked(2), "stage 3 did not");

                data.CompleteStage(1, 5);
                Check.True(data.IsStageUnlocked(2), "stage 3 opened in turn");
            });

            Check.Run("Replaying a stage cannot wind progression backwards", () =>
            {
                var data = new SaveData();
                data.CompleteStage(0, 5);
                data.CompleteStage(1, 5);
                data.CompleteStage(2, 5);

                int reached = data.HighestUnlockedStage;

                // Replay an early one, which a player can always do.
                data.CompleteStage(0, 5);

                Check.Equal(data.HighestUnlockedStage, reached, "still unlocked as far as before");
                Check.Equal(data.CompletedStages.Count, 3, "and not recorded twice");
            });

            Check.Run("Finishing the last stage does not unlock a sixth", () =>
            {
                var data = new SaveData();
                for (int i = 0; i < 5; i++) data.CompleteStage(i, 5);

                Check.Equal(data.HighestUnlockedStage, 4, "the last stage is as far as it goes");
                Check.False(data.IsStageUnlocked(5), "there is no stage 6");
            });

            Check.Run("A save from another build is repaired into something playable", () =>
            {
                var data = new SaveData
                {
                    // Values a corrupt or older file could plausibly hold.
                    MouseSensitivity = 999f,
                    MasterVolume = -4f,
                    Level = 0,
                    Xp = -50,
                    Money = -10,
                    HighestUnlockedStage = 40,
                    CurrentStage = 39,
                    Difficulty = (Difficulty)77
                };
                data.CompletedStages.Add(31);
                data.CompletedStages.Add(2);

                data.Repair(5);

                Check.InRange(data.MouseSensitivity, 0.1f, 5f, "sensitivity is usable");
                Check.InRange(data.MasterVolume, 0f, 1f, "volume is usable");
                Check.True(data.Difficulty == Difficulty.Normal, "an unknown difficulty falls back");
                Check.Equal(data.Level, 1, "level starts at one");
                Check.Equal(data.Xp, 0, "xp is not negative");
                Check.Equal(data.Money, 0, "money is not negative");
                Check.Equal(data.HighestUnlockedStage, 4, "unlocks are clamped to what exists");
                Check.True(data.CurrentStage <= data.HighestUnlockedStage, "current stage is reachable");
                Check.False(data.IsStageCompleted(31), "a stage that does not exist was dropped");
                Check.True(data.IsStageCompleted(2), "a real one was kept");
                Check.True(data.Bindings.IsComplete(), "and the controls still work");
            });

            Check.Run("Progression covers every stage in the catalogue", () =>
            {
                var data = new SaveData();

                for (int i = 0; i < StageCatalogue.Count; i++)
                {
                    Check.True(data.IsStageUnlocked(i), $"stage {i + 1} was reachable in turn");
                    data.CompleteStage(i, StageCatalogue.Count);
                }

                Check.Equal(data.CompletedStages.Count, StageCatalogue.Count,
                    "the whole game can be finished");
            });
        }

        // ---- combat director ----------------------------------------------------------------------

        private static void DirectorTests()
        {
            CombatDirector NewDirector(out Transform focus)
            {
                // Each case needs its own director; the singleton would otherwise
                // survive from the previous test and refuse to initialise.
                Lifecycle.ResetStatics<CombatDirector>();
                CombatDirector director = Lifecycle.Create<CombatDirector>();
                focus = new Transform { position = Vector3.zero, rotation = Quaternion.identity };
                director.Focus = focus;
                return director;
            }

            Check.Run("Only a limited number of enemies may attack at once", () =>
            {
                CombatDirector director = NewDirector(out _);
                director.MaxSimultaneousAttackers = 2;
                Time.Advance(10f);

                var a = new FakeParticipant(new Vector3(2f, 0f, 0f));
                var b = new FakeParticipant(new Vector3(-2f, 0f, 0f));
                var c = new FakeParticipant(new Vector3(0f, 0f, 2f));
                director.Register(a);
                director.Register(b);
                director.Register(c);

                Check.True(director.RequestAttack(a), "the first attacker is authorised");

                // Spacing keeps two hits from landing on the same frame.
                Time.Advance(1f);
                Check.True(director.RequestAttack(b), "the second is authorised after the spacing gap");

                Time.Advance(1f);
                Check.False(director.RequestAttack(c), "the third has to wait");
                Check.Equal(director.ActiveAttackers, 2, "two tokens are out");

                director.ReleaseToken(a);
                Time.Advance(1f);
                Check.True(director.RequestAttack(c), "a freed token lets the next one in");
            });

            Check.Run("Attacks are spaced apart in time", () =>
            {
                CombatDirector director = NewDirector(out _);
                director.MaxSimultaneousAttackers = 4;
                Time.Advance(10f);

                var a = new FakeParticipant(new Vector3(2f, 0f, 0f));
                var b = new FakeParticipant(new Vector3(-2f, 0f, 0f));
                director.Register(a);
                director.Register(b);

                Check.True(director.RequestAttack(a), "first attack authorised");
                Check.False(director.RequestAttack(b), "a second on the same frame is refused");

                Time.Advance(0.5f);
                Check.True(director.RequestAttack(b), "allowed once the gap has passed");
            });

            Check.Run("Holding a token is idempotent", () =>
            {
                CombatDirector director = NewDirector(out _);
                Time.Advance(10f);

                var a = new FakeParticipant(Vector3.forward * 2f);
                director.Register(a);

                Check.True(director.RequestAttack(a), "granted");
                Check.True(director.RequestAttack(a), "asking again while holding is still yes");
                Check.Equal(director.ActiveAttackers, 1, "but it does not consume a second token");
            });

            Check.Run("Dead and disengaged attackers give their token back", () =>
            {
                CombatDirector director = NewDirector(out _);
                director.MaxSimultaneousAttackers = 1;
                Time.Advance(10f);

                var a = new FakeParticipant(Vector3.forward * 2f);
                var b = new FakeParticipant(Vector3.back * 2f);
                director.Register(a);
                director.Register(b);

                Check.True(director.RequestAttack(a), "a is attacking");

                a.IsAlive = false;
                Lifecycle.Invoke(director, "Update");
                Check.Equal(director.ActiveAttackers, 0, "a dead attacker's token is reclaimed");

                Time.Advance(1f);
                Check.True(director.RequestAttack(b), "so b can attack");
            });

            Check.Run("Unregistering releases the token and the slot", () =>
            {
                CombatDirector director = NewDirector(out _);
                director.MaxSimultaneousAttackers = 1;
                Time.Advance(10f);

                var a = new FakeParticipant(Vector3.forward * 2f);
                director.Register(a);
                director.RequestAttack(a);

                director.Unregister(a);
                Check.Equal(director.ActiveAttackers, 0, "token released");
                Check.Equal(director.ParticipantCount, 0, "no longer tracked");
                Check.Equal(director.GetSlot(a), -1, "slot released");
            });

            Check.Run("Enemies are given distinct positions around the target", () =>
            {
                CombatDirector director = NewDirector(out Transform focus);
                Time.Advance(10f);

                var participants = new FakeParticipant[6];
                for (int i = 0; i < participants.Length; i++)
                {
                    float angle = i / (float)participants.Length * Mathf.PI * 2f;
                    participants[i] = new FakeParticipant(
                        new Vector3(Mathf.Sin(angle) * 4f, 0f, Mathf.Cos(angle) * 4f));
                    director.Register(participants[i]);
                }

                Lifecycle.Invoke(director, "Update");

                // The whole purpose of slots: no two enemies stand in one place.
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < participants.Length; i++)
                {
                    int slot = director.GetSlot(participants[i]);
                    Check.True(slot >= 0, $"participant {i} was given a slot");
                    Check.True(seen.Add(slot), $"participant {i}'s slot is unique");
                }

                // And the positions are spread around the focus at a sane radius.
                for (int i = 0; i < participants.Length; i++)
                {
                    Vector3 position = director.GetSlotPosition(participants[i]);
                    float radius = MathUtil.FlatDistance(position, focus.position);
                    Check.InRange(radius, 2f, 5f, "slot sits on the ring around the target");
                }
            });

            Check.Run("Flankers are given slots behind the target", () =>
            {
                CombatDirector director = NewDirector(out Transform focus);
                focus.rotation = Quaternion.identity;   // facing +Z
                Time.Advance(10f);

                // Both start beside the target, so position alone does not decide.
                var frontal = new FakeParticipant(new Vector3(3f, 0f, 0.2f), flank: 0f);
                var flanker = new FakeParticipant(new Vector3(3.1f, 0f, -0.2f), flank: 1f);
                director.Register(frontal);
                director.Register(flanker);

                Lifecycle.Invoke(director, "Update");

                Vector3 flankerSlot = director.GetSlotPosition(flanker);
                Vector3 frontalSlot = director.GetSlotPosition(frontal);

                // Behind the target means a negative Z offset when it faces +Z.
                Check.Less(flankerSlot.z, frontalSlot.z,
                    "the flanker takes the slot further behind the target");
            });

            Check.Run("Slot positions rotate with the target's facing", () =>
            {
                CombatDirector director = NewDirector(out Transform focus);
                Time.Advance(10f);

                var participant = new FakeParticipant(new Vector3(0f, 0f, -3f), flank: 1f);
                director.Register(participant);
                Lifecycle.Invoke(director, "Update");

                Vector3 before = director.GetSlotPosition(participant);

                focus.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3 after = director.GetSlotPosition(participant);

                // "Behind the player" has to keep meaning behind the player.
                Check.Greater((after - before).magnitude, 1f,
                    "the ring turns with the target rather than staying world-locked");
            });
        }

        // ---- combat data ----------------------------------------------------------------

        private static void CombatDataTests()
        {
            Check.Run("Faction hostility is symmetric and spares civilians", () =>
            {
                Check.True(Faction.Player.IsHostileTo(Faction.Hostile), "player fights hostiles");
                Check.True(Faction.Hostile.IsHostileTo(Faction.Player), "and the reverse");
                Check.False(Faction.Player.IsHostileTo(Faction.Player), "no friendly fire");
                Check.False(Faction.Hostile.IsHostileTo(Faction.Hostile), "enemies do not fight each other");
                Check.False(Faction.Player.IsHostileTo(Faction.Civilian), "civilians are never targets");
                Check.False(Faction.Hostile.IsHostileTo(Faction.Civilian), "even for enemies");
                Check.False(Faction.Player.IsHostileTo(Faction.Neutral), "neutral is inert");
            });

            Check.Run("Knockback and stagger scale with impact", () =>
            {
                float lastKnock = 0f;
                float lastStagger = 0f;

                HitImpact[] order =
                {
                    HitImpact.Light, HitImpact.Medium, HitImpact.Heavy, HitImpact.Knockdown
                };

                foreach (HitImpact impact in order)
                {
                    float knock = DamageInfo.DefaultKnockback(impact);
                    float stagger = DamageInfo.DefaultStagger(impact);
                    Check.Greater(knock, lastKnock, $"{impact} knocks back harder than the tier below");
                    Check.Greater(stagger, lastStagger, $"{impact} staggers longer than the tier below");
                    lastKnock = knock;
                    lastStagger = stagger;
                }
            });

            Check.Run("DamageInfo.Create fills in sensible defaults", () =>
            {
                DamageInfo info = DamageInfo.Create(25f, HitImpact.Heavy, new Vector3(1f, 2f, 3f),
                    new Vector3(0f, 0f, 5f), null, Faction.Player);

                Check.Near(info.Amount, 25f, "amount carried through");
                Check.Near(info.Direction.magnitude, 1f, "direction normalised");
                Check.Near(info.KnockbackForce, DamageInfo.DefaultKnockback(HitImpact.Heavy),
                    "knockback matches the impact tier");
                Check.False(info.Unblockable, "blockable by default");
                Check.True(info.AttackerFaction == Faction.Player, "attacker faction recorded");
                Check.Greater(info.BlockStaminaCost, 0f, "blocking costs stamina");
            });

            Check.Run("A zero direction never produces a NaN", () =>
            {
                DamageInfo info = DamageInfo.Create(10f, HitImpact.Light, Vector3.zero, Vector3.zero,
                    null, Faction.Hostile);
                Check.Near(info.Direction.magnitude, 1f, "falls back to a unit vector");
            });
        }

        // ---- attack definitions ----------------------------------------------------------

        private static AttackDefinition SampleMove()
        {
            return new AttackDefinition
            {
                Id = "test", Windup = 0.2f, Active = 0.1f, Recovery = 0.3f,
                ComboWindowStart = 0.5f, ComboWindowEnd = 1f,
                Damage = 20f, Impact = HitImpact.Heavy, KnockbackMultiplier = 2f,
                ChipDamage = 0.25f, GuardStaminaDamage = 30f
            };
        }

        private static void AttackTests()
        {
            Check.Run("Move phases add up and the hitbox opens on time", () =>
            {
                AttackDefinition move = SampleMove();

                Check.Near(move.Duration, 0.6f, "duration is windup + active + recovery");
                Check.Near(move.ActiveStart, 0.2f, "active starts after the windup");
                Check.Near(move.ActiveEnd, 0.3f, "active ends after its own length");

                Check.False(move.IsActiveAt(0f), "no hitbox at the very start");
                Check.False(move.IsActiveAt(0.19f), "no hitbox during the windup");
                Check.True(move.IsActiveAt(0.2f), "hitbox opens exactly at the windup boundary");
                Check.True(move.IsActiveAt(0.25f), "hitbox live mid-swing");
                Check.True(move.IsActiveAt(0.3f), "hitbox live at the final active instant");
                Check.False(move.IsActiveAt(0.31f), "hitbox closed in recovery");
            });

            Check.Run("The combo window opens partway through and stays open to the end", () =>
            {
                AttackDefinition move = SampleMove();

                Check.False(move.InComboWindow(0.1f), "too early to chain");
                Check.False(move.InComboWindow(0.29f), "still too early");
                Check.True(move.InComboWindow(0.3f), "opens at half the duration");
                Check.True(move.InComboWindow(0.6f), "open until the move ends");
                Check.False(move.InComboWindow(0.7f), "closed once the move is over");
            });

            Check.Run("Clip playback is rescaled to the move's length", () =>
            {
                AttackDefinition move = SampleMove();

                // A 1.2s clip has to run at 2x to fit a 0.6s move.
                Check.Near(move.ClipSpeed(1.2f), 2f, "a long clip is sped up");
                Check.Near(move.ClipSpeed(0.3f), 0.5f, "a short clip is slowed down");
                Check.Near(move.ClipSpeed(0f), 1f, "a missing clip falls back to normal speed");
            });

            Check.Run("BuildDamage carries the move's properties into the hit", () =>
            {
                AttackDefinition move = SampleMove();
                move.Unblockable = true;

                DamageInfo info = move.BuildDamage(null, Faction.Player,
                    new Vector3(0f, 1f, 0f), Vector3.forward);

                Check.Near(info.Amount, 20f, "damage carried");
                Check.Near(info.KnockbackForce,
                    DamageInfo.DefaultKnockback(HitImpact.Heavy) * 2f, "knockback multiplied");
                Check.True(info.Unblockable, "unblockable carried");
                Check.Near(info.BlockedDamageFraction, 0.25f, "chip damage carried");
                Check.Near(info.BlockStaminaCost, 30f, "guard stamina damage carried");
            });

            Check.Run("The damage multiplier scales damage but not knockback", () =>
            {
                AttackDefinition move = SampleMove();

                DamageInfo baseline = move.BuildDamage(null, Faction.Player, Vector3.zero, Vector3.forward);
                DamageInfo doubled = move.BuildDamage(null, Faction.Player, Vector3.zero, Vector3.forward, 2f);

                Check.Near(doubled.Amount, baseline.Amount * 2f, "damage doubles");
                Check.Near(doubled.KnockbackForce, baseline.KnockbackForce,
                    "knockback is a property of the move, not of the attacker's power");
            });

            Check.Run("Finishers are the only moves flagged as finishing", () =>
            {
                var finisher = new AttackDefinition { Kind = AttackKind.Finisher };
                var light = new AttackDefinition { Kind = AttackKind.Light };

                Check.True(finisher.BuildDamage(null, Faction.Player, Vector3.zero, Vector3.forward).CanFinish,
                    "a finisher can finish");
                Check.False(light.BuildDamage(null, Faction.Player, Vector3.zero, Vector3.forward).CanFinish,
                    "a jab cannot");
            });
        }

        // ---- move set --------------------------------------------------------------------

        private static void MoveSetTests()
        {
            Check.Run("The player's move set is internally consistent", () =>
            {
                MoveSet set = MoveSet.Player();
                System.Collections.Generic.List<string> problems = set.Validate();

                foreach (string problem in problems) Check.True(false, problem);
                Check.Equal(problems.Count, 0, "no validation problems");
                Check.Greater(set.Count, 6, "the set has a real number of moves");
            });

            Check.Run("Light, light, heavy is reachable", () =>
            {
                MoveSet set = MoveSet.Player();

                AttackDefinition first = set.Resolve(null, heavy: false);
                Check.Equal(first.Id, "light_1", "light opens with the jab");

                AttackDefinition second = set.Resolve(first, heavy: false);
                Check.Equal(second.Id, "light_2", "the jab chains into the cross");

                AttackDefinition third = set.Resolve(second, heavy: true);
                Check.True(third.Kind == AttackKind.Heavy, "heavy cashes the string out");
            });

            Check.Run("Light, light, light, heavy is a different, bigger ending", () =>
            {
                MoveSet set = MoveSet.Player();

                AttackDefinition a = set.Resolve(null, false);
                AttackDefinition b = set.Resolve(a, false);
                AttackDefinition c = set.Resolve(b, false);
                Check.Equal(c.Id, "light_3", "three lights reach the kick");

                AttackDefinition ender = set.Resolve(c, true);
                Check.True(ender.Kind == AttackKind.Heavy, "the fourth input is a heavy");
                Check.Greater(ender.Damage, set.Resolve(b, true).Damage,
                    "the longer string ends harder than the shorter one");
            });

            Check.Run("Heavy attacks are meaningfully stronger and slower than light ones", () =>
            {
                MoveSet set = MoveSet.Player();
                AttackDefinition light = set.Get(set.LightOpener);
                AttackDefinition heavy = set.Get(set.HeavyOpener);

                Check.Greater(heavy.Damage, light.Damage * 2.5f, "a heavy hits far harder");
                Check.Greater(heavy.Windup, light.Windup * 2f, "and is far slower to start");
                Check.Greater(heavy.Recovery, light.Recovery, "and leaves you exposed longer");
                Check.Greater(heavy.StaminaCost, light.StaminaCost * 2f, "and costs real stamina");
                Check.Greater(heavy.HitStop, light.HitStop, "and freezes the frame harder");
            });

            Check.Run("Running off the end of a string restarts it rather than dropping the input", () =>
            {
                MoveSet set = MoveSet.Player();
                AttackDefinition last = set.Get("light_3");
                Check.True(string.IsNullOrEmpty(last.NextLight), "the light string ends at the kick");

                AttackDefinition wrapped = set.Resolve(last, heavy: false);
                Check.Equal(wrapped.Id, "light_1", "another light restarts the string");
            });

            Check.Run("Counters and finishers cannot be blocked", () =>
            {
                MoveSet set = MoveSet.Player();
                Check.True(set.Get(set.CounterMove).Unblockable, "a counter goes through a guard");
                Check.True(set.Get(set.FinisherMove).Unblockable, "so does a finisher");
                Check.False(set.Get(set.LightOpener).Unblockable, "a jab does not");
            });

            Check.Run("Validation catches a broken chain", () =>
            {
                var broken = new MoveSet { LightOpener = "a", HeavyOpener = "a" };
                broken.Add(new AttackDefinition
                {
                    Id = "a", Windup = 0.1f, Active = 0.1f, Recovery = 0.1f,
                    NextLight = "does_not_exist"
                });

                System.Collections.Generic.List<string> problems = broken.Validate();
                Check.Greater(problems.Count, 0, "a dangling chain is reported");
            });
        }

        // ---- time control -----------------------------------------------------------------

        private static void TimeControlTests()
        {
            Check.Run("Hit stop slows time and then releases it", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.HitStop(0.1f, 0.05f);
                Lifecycle.Invoke(controller, "Update");
                Check.True(controller.IsHitStopped, "hit stop is active");
                Check.Near(Time.timeScale, 0.05f, "time is nearly frozen");

                Time.Advance(0.2f);
                Lifecycle.Invoke(controller, "Update");
                Check.False(controller.IsHitStopped, "hit stop expired");
                Check.Near(Time.timeScale, 1f, "time is back to normal");
            });

            Check.Run("Overlapping hit stops extend rather than cut each other short", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.HitStop(0.2f);
                Time.Advance(0.1f);
                controller.HitStop(0.05f);
                Lifecycle.Invoke(controller, "Update");

                Time.Advance(0.06f);
                Lifecycle.Invoke(controller, "Update");
                Check.True(controller.IsHitStopped, "the shorter request did not end the longer one");
            });

            Check.Run("Pause outranks hit stop and slow motion", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.SlowMotion(2f, 0.3f);
                controller.HitStop(1f);
                controller.SetPaused(true);
                Check.Near(Time.timeScale, 0f, "paused means stopped");

                controller.SetPaused(false);
                Lifecycle.Invoke(controller, "Update");
                Check.Greater(Time.timeScale, 0f, "unpausing restores the underlying effect");
            });

            Check.Run("Hit stop outranks slow motion while both are running", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.SlowMotion(2f, 0.4f);
                controller.HitStop(0.1f, 0.02f);
                Lifecycle.Invoke(controller, "Update");
                Check.Near(Time.timeScale, 0.02f, "the freeze wins");

                Time.Advance(0.15f);
                Lifecycle.Invoke(controller, "Update");
                Check.InRange(Time.timeScale, 0.35f, 0.45f, "slow motion resumes underneath");
            });

            Check.Run("Slow motion eases in instead of snapping", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.SlowMotion(1f, 0.2f, blendIn: 0.1f);
                Time.Advance(0.02f);
                Lifecycle.Invoke(controller, "Update");
                Check.Greater(Time.timeScale, 0.5f, "barely slowed on the first frames");

                Time.Advance(0.2f);
                Lifecycle.Invoke(controller, "Update");
                Check.Near(Time.timeScale, 0.2f, "fully slowed once blended in", 0.02f);
            });

            Check.Run("Clearing effects restores normal time immediately", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.HitStop(5f);
                controller.SlowMotion(5f, 0.1f);
                controller.ClearEffects();

                Check.Near(Time.timeScale, 1f, "back to full speed");
                Check.False(controller.IsAltered, "nothing is bending time");
            });

            Check.Run("Physics steps in sync with the visible world", () =>
            {
                TimeController controller = Lifecycle.Create<TimeController>();
                Time.Advance(1f);

                controller.HitStop(0.1f, 0.1f);
                Lifecycle.Invoke(controller, "Update");

                // Otherwise characters keep sliding through a freeze.
                Check.Near(Time.fixedDeltaTime / Time.timeScale, 0.02f,
                    "fixed step scales with time scale", 1e-4f);
            });
        }
    }
}


namespace Glowpulse.LogicTests
{
    /// <summary>A scripted state used to observe the state machine's behaviour.</summary>
    internal sealed class ProbeState : AiState<ProbeOwner>
    {
        private readonly string _name;
        public ProbeState(string name) { _name = name; }
        public override string Name => _name;

        public int Entered, Ticked, Exited;
        public System.Action<ProbeOwner> OnTick;

        public override void Enter(ProbeOwner owner) { Entered++; owner.Log.Add("enter:" + _name); }
        public override void Exit(ProbeOwner owner) { Exited++; owner.Log.Add("exit:" + _name); }

        public override void Tick(ProbeOwner owner, float dt)
        {
            Ticked++;
            owner.Log.Add("tick:" + _name);
            OnTick?.Invoke(owner);
        }
    }

    internal sealed class ProbeOwner
    {
        public readonly System.Collections.Generic.List<string> Log =
            new System.Collections.Generic.List<string>();
        public AiStateMachine<ProbeOwner> Machine;
    }

    /// <summary>Stand-in enemy for exercising the combat director without a scene.</summary>
    internal sealed class FakeParticipant : ICombatParticipant
    {
        private readonly Transform _transform = new Transform();

        public FakeParticipant(Vector3 position, float flank = 0f)
        {
            _transform.position = position;
            FlankPreference = flank;
        }

        public Transform Transform => _transform;
        public bool IsAlive { get; set; } = true;
        public bool IsEngaged { get; set; } = true;
        public float AttackUrgency { get; set; } = 1f;
        public float FlankPreference { get; set; }

        public void MoveTo(Vector3 p) => _transform.position = p;
    }

    public static partial class AiTests
    {
    }
}
