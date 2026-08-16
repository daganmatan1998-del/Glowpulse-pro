using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>
    /// The project's placeholder animation set, authored in code as rotation
    /// offsets. Ids are stable strings so a Mecanim-backed animator can map them
    /// onto imported clips without touching gameplay code.
    /// </summary>
    public static class PoseLibrary
    {
        // ---- stable clip ids -------------------------------------------------
        public const string DodgeRoll = "dodge_roll";
        public const string DodgeStep = "dodge_step";
        public const string JumpTakeoff = "jump_takeoff";
        public const string JumpLand = "jump_land";
        public const string HitLight = "hit_light";
        public const string HitHeavy = "hit_heavy";
        public const string Knockdown = "knockdown";
        public const string GetUp = "get_up";
        public const string Death = "death";

        private static Dictionary<string, PoseClip> _clips;

        public static PoseClip Get(string id)
        {
            EnsureBuilt();
            return id != null && _clips.TryGetValue(id, out PoseClip clip) ? clip : null;
        }

        /// <summary>Registers or replaces a clip. Lets other systems add their own moves.</summary>
        public static void Register(PoseClip clip)
        {
            if (clip == null || string.IsNullOrEmpty(clip.Id)) return;
            EnsureBuilt();
            _clips[clip.Id] = clip;
        }

        public static bool Has(string id)
        {
            EnsureBuilt();
            return id != null && _clips.ContainsKey(id);
        }

        private static void EnsureBuilt()
        {
            if (_clips != null) return;
            _clips = new Dictionary<string, PoseClip>(32);
            BuildDefaults();
        }

        private static PoseKey K(float t, float x, float y, float z) => new PoseKey(t, new Vector3(x, y, z));

        private static void Add(PoseClip clip) => _clips[clip.Id] = clip;

        private static void BuildDefaults()
        {
            // ---- dodge roll: tuck, rotate through, rise ------------------------
            Add(PoseClip.New(DodgeRoll, 0.62f, fullBody: true)
                .Track(RigBone.Root,
                    K(0f, 0, 0, 0), K(0.12f, 60, 0, 0), K(0.34f, 300, 0, 0), K(0.46f, 360, 0, 0), K(0.62f, 360, 0, 0))
                .Track(RigBone.Spine,
                    K(0f, 8, 0, 0), K(0.2f, 42, 0, 0), K(0.42f, 30, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 10, 0, 0), K(0.2f, 34, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, -6, 0, 0), K(0.2f, -34, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.ThighL,
                    K(0f, -20, 0, 0), K(0.18f, -95, 0, 0), K(0.4f, -70, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.ShinL,
                    K(0f, 25, 0, 0), K(0.18f, 110, 0, 0), K(0.4f, 60, 0, 0), K(0.62f, 0, 0, 0))
                .Track(RigBone.UpperArmL,
                    K(0f, -20, 0, 10), K(0.2f, -80, 0, 24), K(0.62f, 0, 0, 0))
                .Track(RigBone.LowerArmL,
                    K(0f, -30, 0, 0), K(0.2f, -95, 0, 0), K(0.62f, 0, 0, 0))
                .Mirror(RigBone.ThighL, RigBone.ThighR)
                .Mirror(RigBone.ShinL, RigBone.ShinR)
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Pivot(0.55f)
                .Hips(K(0f, 0, 0, 0), K(0.2f, -0.22f, 0, 0), K(0.44f, -0.12f, 0, 0), K(0.62f, 0, 0, 0)));

            // ---- side/back hop: a quick low sidestep ---------------------------
            Add(PoseClip.New(DodgeStep, 0.36f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.1f, 16, 0, 0), K(0.36f, 0, 0, 0))
                .Track(RigBone.ThighL, K(0f, 0, 0, 0), K(0.12f, -46, 0, 0), K(0.36f, 0, 0, 0))
                .Track(RigBone.ShinL, K(0f, 0, 0, 0), K(0.12f, 60, 0, 0), K(0.36f, 0, 0, 0))
                .Track(RigBone.ThighR, K(0f, 0, 0, 0), K(0.14f, 26, 0, 0), K(0.36f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.12f, -34, 0, 18), K(0.36f, 0, 0, 0))
                .Track(RigBone.UpperArmR, K(0f, 0, 0, 0), K(0.12f, -34, 0, -18), K(0.36f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.12f, -0.14f, 0, 0), K(0.36f, 0, 0, 0)));

            // ---- jump ----------------------------------------------------------
            Add(PoseClip.New(JumpTakeoff, 0.34f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 14, 0, 0), K(0.1f, -6, 0, 0), K(0.34f, 2, 0, 0))
                .Track(RigBone.ThighL, K(0f, -42, 0, 0), K(0.12f, 8, 0, 0), K(0.34f, -18, 0, 0))
                .Track(RigBone.ShinL, K(0f, 55, 0, 0), K(0.12f, 4, 0, 0), K(0.34f, 34, 0, 0))
                .Track(RigBone.ThighR, K(0f, -42, 0, 0), K(0.12f, 8, 0, 0), K(0.34f, 14, 0, 0))
                .Track(RigBone.ShinR, K(0f, 55, 0, 0), K(0.12f, 4, 0, 0), K(0.34f, 12, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 30, 0, 0), K(0.12f, -70, 0, 12), K(0.34f, -40, 0, 8))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Hips(K(0f, -0.16f, 0, 0), K(0.12f, 0.03f, 0, 0), K(0.34f, 0, 0, 0)));

            Add(PoseClip.New(JumpLand, 0.32f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 20, 0, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.ThighL, K(0f, -48, 0, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.ShinL, K(0f, 62, 0, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.ThighR, K(0f, -48, 0, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.ShinR, K(0f, 62, 0, 0), K(0.32f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, -50, 0, 20), K(0.32f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Hips(K(0f, -0.22f, 0, 0), K(0.32f, 0, 0, 0)));

            // ---- hit reactions ---------------------------------------------------
            Add(PoseClip.New(HitLight, 0.26f)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.07f, -16, 8, 6), K(0.26f, 0, 0, 0))
                .Track(RigBone.Chest, K(0f, 0, 0, 0), K(0.07f, -12, 12, 8), K(0.26f, 0, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.06f, -18, 14, 10), K(0.26f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.08f, -18, 0, 22), K(0.26f, 0, 0, 0))
                .Track(RigBone.UpperArmR, K(0f, 0, 0, 0), K(0.08f, -10, 0, -14), K(0.26f, 0, 0, 0)));

            Add(PoseClip.New(HitHeavy, 0.52f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.1f, -34, 14, 10), K(0.3f, -14, 6, 4), K(0.52f, 0, 0, 0))
                .Track(RigBone.Chest, K(0f, 0, 0, 0), K(0.1f, -22, 20, 12), K(0.52f, 0, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.08f, -34, 26, 16), K(0.52f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.1f, -44, 0, 34), K(0.52f, 0, 0, 0))
                .Track(RigBone.UpperArmR, K(0f, 0, 0, 0), K(0.1f, -30, 0, -24), K(0.52f, 0, 0, 0))
                .Track(RigBone.ThighL, K(0f, 0, 0, 0), K(0.14f, 22, 0, 0), K(0.52f, 0, 0, 0))
                .Track(RigBone.ThighR, K(0f, 0, 0, 0), K(0.14f, -26, 0, 0), K(0.52f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.12f, -0.1f, 0, 0), K(0.52f, 0, 0, 0)));

            // ---- knockdown, get up, death ----------------------------------------
            Add(PoseClip.New(Knockdown, 0.85f, fullBody: true)
                .Track(RigBone.Root, K(0f, 0, 0, 0), K(0.3f, -46, 0, 0), K(0.55f, -84, 0, 0), K(0.85f, -88, 0, 0))
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.25f, -28, 0, 0), K(0.85f, 8, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.3f, -22, 0, 0), K(0.85f, 16, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.25f, -70, 0, 40), K(0.85f, -20, 0, 62))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.ThighL, K(0f, 0, 0, 0), K(0.3f, 40, 0, 0), K(0.85f, 12, 0, 0))
                .Track(RigBone.ThighR, K(0f, 0, 0, 0), K(0.3f, 30, 0, 0), K(0.85f, 20, 0, 0))
                .Pivot(0f).Hold());

            Add(PoseClip.New(GetUp, 0.95f, fullBody: true)
                .Track(RigBone.Root, K(0f, -88, 0, 0), K(0.45f, -54, 0, 0), K(0.8f, -12, 0, 0), K(0.95f, 0, 0, 0))
                .Track(RigBone.Spine, K(0f, 8, 0, 0), K(0.5f, 30, 0, 0), K(0.95f, 0, 0, 0))
                .Track(RigBone.ThighL, K(0f, 12, 0, 0), K(0.45f, -80, 0, 0), K(0.95f, 0, 0, 0))
                .Track(RigBone.ShinL, K(0f, 0, 0, 0), K(0.45f, 90, 0, 0), K(0.95f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, -20, 0, 62), K(0.5f, -50, 0, 20), K(0.95f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Pivot(0f));

            Add(PoseClip.New(Death, 1.1f, fullBody: true)
                .Track(RigBone.Root, K(0f, 0, 0, 0), K(0.35f, -40, 0, 12), K(0.75f, -86, 0, 16), K(1.1f, -90, 0, 16))
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.3f, -20, 10, 0), K(1.1f, 6, 4, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.3f, -26, 14, 0), K(1.1f, 22, 8, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.4f, -40, 0, 46), K(1.1f, -8, 0, 68))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.ThighL, K(0f, 0, 0, 0), K(0.5f, 34, 0, 0), K(1.1f, 16, 0, 6))
                .Track(RigBone.ThighR, K(0f, 0, 0, 0), K(0.5f, 20, 0, 0), K(1.1f, 8, 0, -4))
                .Pivot(0f).Hold());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _clips = null;
    }
}
