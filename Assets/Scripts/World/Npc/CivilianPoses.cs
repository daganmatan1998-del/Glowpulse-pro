using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>
    /// What a bystander's body does. Registered into <see cref="PoseLibrary"/> the
    /// same self-healing way the combat set is, so the clips survive a domain
    /// reload being skipped in the editor.
    ///
    /// These clips carry most of the weight of making a crowd feel alive. A
    /// civilian who only ever walks is set dressing; one who flinches when a fight
    /// starts near them, throws their hands up, and then peers back over a
    /// shoulder is a person.
    /// </summary>
    public static class CivilianPoses
    {
        public const string Startle = "civ_startle";
        public const string Cower = "civ_cower";
        public const string Shield = "civ_shield";
        public const string Wave = "civ_wave";
        public const string CheckWatch = "civ_check_watch";
        public const string LookAround = "civ_look_around";
        public const string Stumble = "civ_stumble";

        private static PoseClip[] _clips;

        public static void EnsureRegistered()
        {
            if (_clips == null)
            {
                _clips = new[]
                {
                    BuildStartle(), BuildCower(), BuildShield(),
                    BuildWave(), BuildCheckWatch(), BuildLookAround(), BuildStumble()
                };
            }
            else if (PoseLibrary.Has(Startle))
            {
                return;
            }

            for (int i = 0; i < _clips.Length; i++) PoseLibrary.Register(_clips[i]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad() => EnsureRegistered();

        private static PoseKey K(float t, float x, float y, float z) => new PoseKey(t, new Vector3(x, y, z));

        /// <summary>A sharp recoil away from a noise, then a wary settle.</summary>
        private static PoseClip BuildStartle()
        {
            return PoseClip.New(Startle, 0.5f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.08f, 14, 0, 0), K(0.26f, 6, 0, 0), K(0.5f, 0, 0, 0))
                .Track(RigBone.Chest, K(0f, 0, 0, 0), K(0.08f, 10, 0, 0), K(0.5f, 0, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.07f, -16, 0, 0), K(0.3f, -4, 0, 0), K(0.5f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.1f, -34, 0, 26), K(0.32f, -14, 0, 14), K(0.5f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.LowerArmL, K(0f, 0, 0, 0), K(0.1f, -46, 0, 0), K(0.5f, 0, 0, 0))
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Hips(K(0f, 0, 0, 0), K(0.1f, -0.06f, 0, 0), K(0.5f, 0, 0, 0));
        }

        /// <summary>Crouched, head down, arms over the head. Held until released.</summary>
        private static PoseClip BuildCower()
        {
            return PoseClip.New(Cower, 0.55f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.55f, 34, 0, 0))
                .Track(RigBone.Chest, K(0f, 0, 0, 0), K(0.55f, 20, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.55f, 26, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.55f, -128, 0, 44))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.LowerArmL, K(0f, 0, 0, 0), K(0.55f, -104, 0, 0))
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Track(RigBone.ThighL, K(0f, 0, 0, 0), K(0.55f, -34, 0, 0))
                .Mirror(RigBone.ThighL, RigBone.ThighR)
                .Track(RigBone.ShinL, K(0f, 0, 0, 0), K(0.55f, 52, 0, 0))
                .Mirror(RigBone.ShinL, RigBone.ShinR)
                .Hips(K(0f, 0, 0, 0), K(0.55f, -0.34f, 0, 0))
                .Hold();
        }

        /// <summary>Arms up in front of the face, backing off. A standing flinch.</summary>
        private static PoseClip BuildShield()
        {
            return PoseClip.New(Shield, 0.45f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.12f, 18, 0, 0), K(0.45f, 12, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.12f, 14, 0, 0), K(0.45f, 10, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.14f, -96, 0, 30), K(0.45f, -88, 0, 28))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.LowerArmL, K(0f, 0, 0, 0), K(0.14f, -88, 0, 0), K(0.45f, -84, 0, 0))
                .Mirror(RigBone.LowerArmL, RigBone.LowerArmR)
                .Hold();
        }

        /// <summary>Greeting somebody across the street.</summary>
        private static PoseClip BuildWave()
        {
            return PoseClip.New(Wave, 1.5f)
                .Track(RigBone.UpperArmR,
                    K(0f, 0, 0, 0), K(0.3f, -132, 0, -22), K(1.2f, -132, 0, -22), K(1.5f, 0, 0, 0))
                .Track(RigBone.LowerArmR,
                    K(0f, 0, 0, 0), K(0.3f, -20, 0, 0), K(0.55f, -20, 0, -34), K(0.8f, -20, 0, 22),
                    K(1.05f, -20, 0, -30), K(1.2f, -20, 0, 0), K(1.5f, 0, 0, 0))
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.35f, 0, -16, 0), K(1.2f, 0, -16, 0), K(1.5f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.35f, 0, -10, 0), K(1.2f, 0, -10, 0), K(1.5f, 0, 0, 0));
        }

        /// <summary>Glances at a wrist, then hurries on. Sells "late for something".</summary>
        private static PoseClip BuildCheckWatch()
        {
            return PoseClip.New(CheckWatch, 1.2f)
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.28f, -62, 0, 30), K(0.9f, -62, 0, 30), K(1.2f, 0, 0, 0))
                .Track(RigBone.LowerArmL, K(0f, 0, 0, 0), K(0.28f, -86, 0, 0), K(0.9f, -86, 0, 0), K(1.2f, 0, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.3f, 22, 12, 0), K(0.9f, 22, 12, 0), K(1.2f, 0, 0, 0))
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.3f, 8, 6, 0), K(0.9f, 8, 6, 0), K(1.2f, 0, 0, 0));
        }

        /// <summary>An unhurried look left and right. The default "waiting" idle.</summary>
        private static PoseClip BuildLookAround()
        {
            return PoseClip.New(LookAround, 2.4f)
                .Track(RigBone.Head,
                    K(0f, 0, 0, 0), K(0.5f, 0, -40, 0), K(1.0f, 0, -34, 0), K(1.5f, 0, 44, 0),
                    K(2.0f, 0, 36, 0), K(2.4f, 0, 0, 0))
                .Track(RigBone.Chest,
                    K(0f, 0, 0, 0), K(0.5f, 0, -12, 0), K(1.5f, 0, 14, 0), K(2.4f, 0, 0, 0));
        }

        /// <summary>Shoved off balance: a lurch, a catch, and back upright.</summary>
        private static PoseClip BuildStumble()
        {
            return PoseClip.New(Stumble, 0.7f, fullBody: true)
                .Track(RigBone.Spine, K(0f, 0, 0, 0), K(0.12f, -26, 10, 8), K(0.4f, 16, 0, 0), K(0.7f, 0, 0, 0))
                .Track(RigBone.Head, K(0f, 0, 0, 0), K(0.12f, -22, 16, 10), K(0.7f, 0, 0, 0))
                .Track(RigBone.UpperArmL, K(0f, 0, 0, 0), K(0.14f, -76, 0, 52), K(0.42f, -30, 0, 24), K(0.7f, 0, 0, 0))
                .Mirror(RigBone.UpperArmL, RigBone.UpperArmR)
                .Track(RigBone.ThighL, K(0f, 0, 0, 0), K(0.18f, -52, 0, 0), K(0.45f, 10, 0, 0), K(0.7f, 0, 0, 0))
                .Track(RigBone.ShinL, K(0f, 0, 0, 0), K(0.18f, 40, 0, 0), K(0.7f, 0, 0, 0))
                .Track(RigBone.ThighR, K(0f, 0, 0, 0), K(0.22f, 30, 0, 0), K(0.7f, 0, 0, 0))
                .Hips(K(0f, 0, 0, 0), K(0.14f, -0.12f, 0, 0), K(0.4f, -0.05f, 0, 0), K(0.7f, 0, 0, 0));
        }
    }
}
