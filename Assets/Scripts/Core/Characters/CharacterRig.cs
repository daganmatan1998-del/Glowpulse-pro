using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>
    /// Named joints of the humanoid rig. Deliberately a subset of Unity's
    /// HumanBodyBones so a real rigged model can be mapped onto it one-to-one
    /// when authored art replaces the placeholders.
    /// </summary>
    public enum RigBone
    {
        Root = 0,
        Hips,
        Spine,
        Chest,
        Neck,
        Head,
        ShoulderL,
        UpperArmL,
        LowerArmL,
        HandL,
        ShoulderR,
        UpperArmR,
        LowerArmR,
        HandR,
        ThighL,
        ShinL,
        FootL,
        ThighR,
        ShinR,
        FootR,
        Count
    }

    /// <summary>
    /// Holds the joint transforms of a character and their rest pose. Animation
    /// systems reset to the rest pose each frame and then layer offsets on top,
    /// which keeps procedural animation from drifting.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        [SerializeField] private Transform[] _bones = new Transform[(int)RigBone.Count];

        private Vector3[] _restPos;
        private Quaternion[] _restRot;

        /// <summary>Total standing height in metres. Drives camera framing and hit heights.</summary>
        public float Height { get; private set; } = 1.8f;

        /// <summary>Roughly chest height in local space - the point attacks aim at.</summary>
        public float AimHeight { get; private set; } = 1.25f;

        public Transform Root => Get(RigBone.Root);
        public Transform Hips => Get(RigBone.Hips);
        public Transform Chest => Get(RigBone.Chest);
        public Transform Head => Get(RigBone.Head);
        public Transform HandL => Get(RigBone.HandL);
        public Transform HandR => Get(RigBone.HandR);
        public Transform FootL => Get(RigBone.FootL);
        public Transform FootR => Get(RigBone.FootR);

        public Transform Get(RigBone bone)
        {
            int i = (int)bone;
            return i >= 0 && i < _bones.Length ? _bones[i] : null;
        }

        public void Set(RigBone bone, Transform t)
        {
            int i = (int)bone;
            if (i >= 0 && i < _bones.Length) _bones[i] = t;
        }

        public void Configure(float height, float aimHeight)
        {
            Height = height;
            AimHeight = aimHeight;
        }

        /// <summary>Captures the current local transforms as the rest pose.</summary>
        public void CaptureRestPose()
        {
            int n = _bones.Length;
            _restPos = new Vector3[n];
            _restRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                Transform t = _bones[i];
                _restPos[i] = t != null ? t.localPosition : Vector3.zero;
                _restRot[i] = t != null ? t.localRotation : Quaternion.identity;
            }
        }

        public Vector3 RestPosition(RigBone bone)
        {
            int i = (int)bone;
            return _restPos != null && i < _restPos.Length ? _restPos[i] : Vector3.zero;
        }

        public Quaternion RestRotation(RigBone bone)
        {
            int i = (int)bone;
            return _restRot != null && i < _restRot.Length ? _restRot[i] : Quaternion.identity;
        }

        /// <summary>Snaps every joint back to the rest pose. Called at the start of each animation frame.</summary>
        public void ResetToRest()
        {
            if (_restPos == null) return;
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform t = _bones[i];
                if (t == null) continue;
                t.localPosition = _restPos[i];
                t.localRotation = _restRot[i];
            }
        }

        /// <summary>World point attacks and the lock-on reticle aim at.</summary>
        public Vector3 AimPoint => transform.position + transform.up * AimHeight;
    }
}
