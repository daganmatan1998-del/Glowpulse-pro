using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// The combat animation set, authored as rotation offsets from the rig's rest
    /// pose and registered into <see cref="PoseLibrary"/>.
    ///
    /// Registration is idempotent and self-healing: <see cref="EnsureRegistered"/>
    /// re-adds the clips if the library was cleared. That matters because
    /// PoseLibrary resets between play sessions, and with the editor's "no domain
    /// reload" option a one-shot static constructor would run before the reset
    /// and never again, leaving every attack silently unanimated.
    ///
    /// Rig convention: a limb's rest pose hangs along local -Y, so rotating a
    /// shoulder about X by roughly -90 degrees points that arm straight forward.
    /// Positive yaw on the chest turns the character's right shoulder forward.
    /// </summary>
    public static class CombatPoses
    {
        public const string LightJab = "atk_light_jab";
        public const string LightCross = "atk_light_cross";
        public const string LightKick = "atk_light_kick";
        public const string HeavyHook = "atk_heavy_hook";
        public const string HeavyOverhead = "atk_heavy_overhead";
        public const string HeavyUppercut = "atk_heavy_uppercut";
        public const string Counter = "atk_counter";
        public const string Finisher = "atk_finisher";
        public const string Grab = "atk_grab";
        public const string GrabHold = "atk_grab_hold";
        public const string Throw = "atk_throw";
        public const string GuardImpact = "guard_impact";
        public const string ParryDeflect = "guard_parry";
        public const string GuardBreak = "guard_break";

        private static PoseClip[] _clips;

        /// <summary>
        /// Registers every combat clip, building them the first time. Cheap enough
        /// to call from any Awake that depends on them.
        /// </summary>
        public static void EnsureRegistered()
        {
            if (_clips == null)
            {
                _clips = new[]
                {
                    BuildLightJab(), BuildLightCross(), BuildLightKick(),
                    BuildHeavyHook(), BuildHeavyOverhead(), BuildHeavyUppercut(),
                    BuildCounter(), BuildFinisher(),
                    BuildGrab(), BuildGrabHold(), BuildThrow(),
                    BuildGuardImpact(), BuildParryDeflect(), BuildGuardBreak()
                };
            }
            else if (PoseLibrary.Has(LightJab))
            {
                return;
            }

            for (int i = 0; i < _clips.Length; i++) PoseLibrary.Register(_clips[i]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad() => EnsureRegistered();

        private static PoseKey K(float t, float x, float y, float z) => new PoseKey(t, new Vector3(x, y, z));

        // ---- light attacks ---------------------------------------------------------

        private static PoseClip BuildLightJab()
        {
            // Lead-hand jab: quick, short, snaps straight back to guard.
            return PoseClip.New("atk_light_jab", 0.34f)
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.06f, -30, -6, 6), K(0.14f, -86, -14, 8), K(0.34f, 0, 0, 0))
                .Track(RigBone.LowerArmL,
                    K(0f, 0, 0, 0), K(0.06f, -46, 0, 0), K(0.14f, 12, 0, 0), K(0.34f, 0, 0, 0))
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.14f, -18, 0, -10), K(0.34f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.14f, 3, -20, 0), K(0.34f, 0, 0, 0))
                .Track(RigBone.Hips,
                    K(0f, 0, 0, 0), K(0.14f, 0, -9, 0), K(0.34f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.14f, 2, -6, 0), K(0.34f, 0, 0, 0));
        }

        private static PoseClip BuildLightCross()
        {
            // Rear-hand cross: more hip rotation behind it than the jab.
            return PoseClip.New("atk_light_cross", 0.38f)
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.07f, -24, 14, -6), K(0.16f, -90, 16, -8), K(0.38f, 0, 0, 0))
                .Track(RigBone.LowerArmR,
                    K(0f, 0, 0, 0), K(0.07f, -50, 0, 0), K(0.16f, 10, 0, 0), K(0.38f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.16f, -26, 0, 14), K(0.38f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.06f, 0, -12, 0), K(0.16f, 4, 30, 0), K(0.38f, 0, 0, 0))
                .Track(RigBone.Hips,
                    K(0f, 0, 0, 0), K(0.06f, 0, -6, 0), K(0.16f, 0, 22, 0), K(0.38f, 0, 0, 0))
                .Track(RigBone.ThighR,
                    K(0f, 0, 0, 0), K(0.16f, 0, 14, 0), K(0.38f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.16f, 3, 10, 0), K(0.38f, 0, 0, 0));
        }

        private static PoseClip BuildLightKick()
        {
            // Front snap kick: knee up, then the shin whips out and returns.
            return PoseClip.New("atk_light_kick", 0.44f, fullBody: true)
                .Track(RigBone.ThighR,
                    K(0f, 0, 0, 0), K(0.12f, -62, 0, 0), K(0.2f, -92, 0, 0), K(0.3f, -70, 0, 0),
                    K(0.44f, 0, 0, 0))
                .Track(RigBone.ShinR,
                    K(0f, 0, 0, 0), K(0.12f, 84, 0, 0), K(0.2f, 6, 0, 0), K(0.3f, 46, 0, 0),
                    K(0.44f, 0, 0, 0))
                .Track(RigBone.FootR,
                    K(0f, 0, 0, 0), K(0.2f, -26, 0, 0), K(0.44f, 0, 0, 0))
                .Track(RigBone.ThighL,
                    K(0f, 0, 0, 0), K(0.2f, 12, 0, 0), K(0.44f, 0, 0, 0))
                .Track(RigBone.Root,
                    K(0f, 0, 0, 0), K(0.2f, -13, 0, 0), K(0.44f, 0, 0, 0))
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.2f, -14, 0, 0), K(0.44f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.2f, -20, 0, 40), K(0.44f, 0, 0, 0))
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.2f, 20, 0, -34), K(0.44f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.2f, -0.06f, 0, 0), K(0.44f, 0, 0, 0));
        }

        // ---- heavy attacks -------------------------------------------------------------

        private static PoseClip BuildHeavyHook()
        {
            // Wide hook. The long wind-back is the tell that makes it punishable.
            return PoseClip.New("atk_heavy_hook", 0.76f, fullBody: true)
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.26f, 16, -46, -18), K(0.42f, -74, 58, -34), K(0.76f, 0, 0, 0))
                .Track(RigBone.LowerArmR,
                    K(0f, 0, 0, 0), K(0.26f, -70, 0, 0), K(0.42f, -34, 0, 0), K(0.76f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.26f, -50, 0, 26), K(0.42f, -30, 0, 18), K(0.76f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.26f, -6, -34, 0), K(0.42f, 6, 44, 0), K(0.76f, 0, 0, 0))
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.26f, -4, -18, 0), K(0.42f, 8, 22, 0), K(0.76f, 0, 0, 0))
                .Track(RigBone.Hips,
                    K(0f, 0, 0, 0), K(0.26f, 0, -22, 0), K(0.42f, 0, 34, 0), K(0.76f, 0, 0, 0))
                .Track(RigBone.ThighR,
                    K(0f, 0, 0, 0), K(0.26f, -14, -10, 0), K(0.42f, 8, 20, 0), K(0.76f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.26f, 4, -14, 0), K(0.42f, 6, 20, 0), K(0.76f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.26f, -0.07f, 0, 0), K(0.42f, -0.03f, 0, 0), K(0.76f, 0, 0, 0));
        }

        private static PoseClip BuildHeavyOverhead()
        {
            // Two-handed overhead smash: arms all the way up, then straight down.
            return PoseClip.New("atk_heavy_overhead", 0.94f, fullBody: true)
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.34f, -158, 0, 14), K(0.52f, -34, 0, 8), K(0.94f, 0, 0, 0))
                .Track(RigBone.LowerArmL,
                    K(0f, 0, 0, 0), K(0.34f, -54, 0, 0), K(0.52f, -8, 0, 0), K(0.94f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.34f, -22, 0, 0), K(0.52f, 30, 0, 0), K(0.94f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.34f, -16, 0, 0), K(0.52f, 24, 0, 0), K(0.94f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.34f, -18, 0, 0), K(0.52f, 20, 0, 0), K(0.94f, 0, 0, 0))
                .Track(RigBone.ThighL,
                    K(0f, 0, 0, 0), K(0.34f, -20, 0, 0), K(0.52f, 26, 0, 0), K(0.94f, 0, 0, 0))
                .Track(RigBone.ShinL,
                    K(0f, 0, 0, 0), K(0.52f, 34, 0, 0), K(0.94f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.34f, 0.04f, 0, 0), K(0.56f, -0.16f, 0, 0), K(0.94f, 0, 0, 0));
        }

        private static PoseClip BuildHeavyUppercut()
        {
            // Drops into a crouch, then drives up through the target.
            return PoseClip.New("atk_heavy_uppercut", 0.8f, fullBody: true)
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.24f, 34, 10, -12), K(0.4f, -118, 18, -22), K(0.8f, 0, 0, 0))
                .Track(RigBone.LowerArmR,
                    K(0f, 0, 0, 0), K(0.24f, -40, 0, 0), K(0.4f, -74, 0, 0), K(0.8f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.24f, -40, 0, 22), K(0.4f, -58, 0, 26), K(0.8f, 0, 0, 0))
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.24f, 24, -14, 0), K(0.4f, -16, 20, 0), K(0.8f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.24f, 16, -20, 0), K(0.4f, -14, 26, 0), K(0.8f, 0, 0, 0))
                .Track(RigBone.Hips,
                    K(0f, 0, 0, 0), K(0.24f, 0, -16, 0), K(0.4f, 0, 22, 0), K(0.8f, 0, 0, 0))
                .Track(RigBone.ThighL,
                    K(0f, 0, 0, 0), K(0.24f, -34, 0, 0), K(0.4f, -6, 0, 0), K(0.8f, 0, 0, 0))
                .Track(RigBone.ShinL,
                    K(0f, 0, 0, 0), K(0.24f, 48, 0, 0), K(0.4f, 8, 0, 0), K(0.8f, 0, 0, 0))
                .Track(RigBone.ThighR,
                    K(0f, 0, 0, 0), K(0.24f, -30, 0, 0), K(0.4f, 10, 0, 0), K(0.8f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.24f, -0.22f, 0, 0), K(0.42f, 0.06f, 0, 0), K(0.8f, 0, 0, 0));
        }

        // ---- reactive moves -------------------------------------------------------------

        private static PoseClip BuildCounter()
        {
            // Short, brutal elbow thrown out of a deflection - almost no wind-up,
            // because the wind-up already happened when the attack was parried.
            return PoseClip.New("atk_counter", 0.46f, fullBody: true)
                .Track(RigBone.UpperArmR,
                    K(0f, -30, 20, -20), K(0.1f, -96, 44, -26), K(0.2f, -80, 10, -14), K(0.46f, 0, 0, 0))
                .Track(RigBone.LowerArmR,
                    K(0f, -80, 0, 0), K(0.1f, -104, 0, 0), K(0.46f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, -40, 0, 20), K(0.1f, -20, 0, 30), K(0.46f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, -24, 0), K(0.1f, 6, 40, 0), K(0.46f, 0, 0, 0))
                .Track(RigBone.Hips,
                    K(0f, 0, -14, 0), K(0.1f, 0, 28, 0), K(0.46f, 0, 0, 0))
                .Track(RigBone.Root,
                    K(0f, 0, 0, 0), K(0.1f, 6, 0, 0), K(0.46f, 0, 0, 0));
        }

        private static PoseClip BuildFinisher()
        {
            // Axe kick onto a downed opponent: raise the leg high, drive it down.
            return PoseClip.New("atk_finisher", 0.96f, fullBody: true)
                .Track(RigBone.ThighR,
                    K(0f, 0, 0, 0), K(0.3f, -118, 0, 0), K(0.46f, -30, 0, 0), K(0.96f, 0, 0, 0))
                .Track(RigBone.ShinR,
                    K(0f, 0, 0, 0), K(0.3f, 24, 0, 0), K(0.46f, 4, 0, 0), K(0.96f, 0, 0, 0))
                .Track(RigBone.FootR,
                    K(0f, 0, 0, 0), K(0.3f, 20, 0, 0), K(0.46f, -30, 0, 0), K(0.96f, 0, 0, 0))
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.3f, 16, 0, 0), K(0.46f, -26, 0, 0), K(0.96f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.3f, -70, 0, 30), K(0.46f, 20, 0, 34), K(0.96f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.ThighL,
                    K(0f, 0, 0, 0), K(0.3f, 10, 0, 0), K(0.46f, -14, 0, 0), K(0.96f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.3f, 0.05f, 0, 0), K(0.5f, -0.14f, 0, 0), K(0.96f, 0, 0, 0));
        }

        // ---- grappling ---------------------------------------------------------------------

        private static PoseClip BuildGrab()
        {
            return PoseClip.New("atk_grab", 0.58f)
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.16f, -74, -12, 16), K(0.3f, -66, -20, 12), K(0.58f, 0, 0, 0))
                .Track(RigBone.LowerArmL,
                    K(0f, 0, 0, 0), K(0.16f, -30, 0, 0), K(0.3f, -14, 0, 0), K(0.58f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.16f, 12, 0, 0), K(0.58f, 0, 0, 0));
        }

        private static PoseClip BuildGrabHold()
        {
            // Held while an enemy is grappled. Loops so it can run for any duration.
            PoseClip clip = PoseClip.New("atk_grab_hold", 0.9f)
                .Track(RigBone.UpperArmL,
                    K(0f, -64, -18, 14), K(0.45f, -60, -20, 12), K(0.9f, -64, -18, 14))
                .Track(RigBone.LowerArmL, K(0f, -20, 0, 0), K(0.9f, -20, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Track(RigBone.Spine, K(0f, 10, 0, 0), K(0.45f, 13, 0, 0), K(0.9f, 10, 0, 0));
            clip.Loop = true;
            return clip;
        }

        private static PoseClip BuildThrow()
        {
            // Swings the held opponent across the body and releases.
            return PoseClip.New("atk_throw", 0.78f, fullBody: true)
                .Track(RigBone.UpperArmL,
                    K(0f, -62, -18, 14), K(0.22f, -88, -52, 22), K(0.4f, -54, 60, 30), K(0.78f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.Chest,
                    K(0f, 8, 0, 0), K(0.22f, 4, -44, 0), K(0.4f, 6, 52, 0), K(0.78f, 0, 0, 0))
                .Track(RigBone.Hips,
                    K(0f, 0, 0, 0), K(0.22f, 0, -30, 0), K(0.4f, 0, 38, 0), K(0.78f, 0, 0, 0))
                .Track(RigBone.Spine,
                    K(0f, 6, 0, 0), K(0.22f, -10, -20, 0), K(0.4f, 14, 24, 0), K(0.78f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.22f, -0.1f, 0, 0), K(0.78f, 0, 0, 0));
        }

        // ---- defensive reactions --------------------------------------------------------------

        private static PoseClip BuildGuardImpact()
        {
            // Guard holds, but the whole body absorbs the blow.
            return PoseClip.New("guard_impact", 0.24f)
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.06f, 16, 0, 12), K(0.24f, 0, 0, 0))
                .Track(RigBone.UpperArmR, K(0f, 0, 0, 0), K(0.06f, 16, 0, -12), K(0.24f, 0, 0, 0))
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.06f, -12, 0, 0), K(0.24f, 0, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.06f, -8, 0, 0), K(0.24f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.06f, -0.04f, 0, 0), K(0.24f, 0, 0, 0));
        }

        private static PoseClip BuildParryDeflect()
        {
            // A small, precise sweep - the read is "that was deliberate", not "I got lucky".
            return PoseClip.New("guard_parry", 0.32f)
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.08f, -46, 34, -26), K(0.18f, -30, -20, -10), K(0.32f, 0, 0, 0))
                .Track(RigBone.LowerArmR,
                    K(0f, 0, 0, 0), K(0.08f, -40, 0, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.08f, 0, 22, 0), K(0.18f, 0, -12, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.08f, 0, 14, 0), K(0.32f, 0, 0, 0));
        }

        private static PoseClip BuildGuardBreak()
        {
            // Arms flung wide - the punish window is meant to be obvious.
            return PoseClip.New("guard_break", 0.62f, fullBody: true)
                .Track(RigBone.UpperArmL,
                    K(0f, 0, 0, 0), K(0.12f, 40, 0, 66), K(0.34f, 20, 0, 48), K(0.62f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.Spine,
                    K(0f, 0, 0, 0), K(0.12f, -30, 0, 0), K(0.34f, -18, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.12f, -26, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.ThighL,
                    K(0f, 0, 0, 0), K(0.12f, 18, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.ThighR,
                    K(0f, 0, 0, 0), K(0.12f, -20, 0, 0), K(0.62f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.12f, -0.08f, 0, 0), K(0.62f, 0, 0, 0));
        }
    }
}
