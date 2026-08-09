using System;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.InputSystem;
using Glowpulse.Core.Timing;
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
