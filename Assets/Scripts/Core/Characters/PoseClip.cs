using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>A single keyframe: rotation offset from the rest pose, in degrees.</summary>
    public struct PoseKey
    {
        public float Time;
        public Vector3 Euler;

        public PoseKey(float time, Vector3 euler)
        {
            Time = time;
            Euler = euler;
        }
    }

    /// <summary>All keys for one joint.</summary>
    public struct BoneTrack
    {
        public HumanBone Bone;
        public PoseKey[] Keys;
    }

    /// <summary>
    /// A hand-authored animation expressed as rotation offsets from the rig's
    /// rest pose. This is the placeholder stand-in for imported clips: attacks,
    /// dodges, hit reactions and deaths are all defined as these.
    ///
    /// Because everything is expressed as offsets, the same clip works on any
    /// body proportions - a lanky runner and a heavy bruiser both read correctly.
    /// </summary>
    public sealed class PoseClip
    {
        public string Id { get; private set; }
        public float Duration { get; private set; }

        /// <summary>Full-body clips override the legs too; otherwise only the upper body is driven.</summary>
        public bool FullBody { get; set; }

        public bool Loop { get; set; }

        /// <summary>Stay on the last frame instead of fading out. Used by death and knockdown.</summary>
        public bool HoldLastFrame { get; set; }

        /// <summary>Vertical hips offset in metres over time - used for crouches and rolls.</summary>
        public PoseKey[] HipsOffset { get; set; }

        /// <summary>
        /// Height, as a fraction of the character's height, that rotation of the
        /// Root joint pivots around. 0 is the feet - correct for toppling over -
        /// while a roll needs roughly hip height so the body tumbles in place
        /// instead of swinging around the ankles.
        /// </summary>
        public float RootPivot01 { get; set; }

        private readonly List<BoneTrack> _tracks = new List<BoneTrack>(12);

        public IReadOnlyList<BoneTrack> Tracks => _tracks;

        public static PoseClip New(string id, float duration, bool fullBody = false)
        {
            return new PoseClip { Id = id, Duration = Mathf.Max(0.01f, duration), FullBody = fullBody };
        }

        /// <summary>Adds a joint track. Keys must be supplied in ascending time order.</summary>
        public PoseClip Track(HumanBone bone, params PoseKey[] keys)
        {
            if (keys == null || keys.Length == 0) return this;
            _tracks.Add(new BoneTrack { Bone = bone, Keys = keys });
            return this;
        }

        /// <summary>Mirrors a track onto the opposite limb, negating yaw and roll.</summary>
        public PoseClip Mirror(HumanBone source, HumanBone destination)
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].Bone != source) continue;
                PoseKey[] src = _tracks[i].Keys;
                var mirrored = new PoseKey[src.Length];
                for (int k = 0; k < src.Length; k++)
                    mirrored[k] = new PoseKey(src[k].Time,
                        new Vector3(src[k].Euler.x, -src[k].Euler.y, -src[k].Euler.z));
                _tracks.Add(new BoneTrack { Bone = destination, Keys = mirrored });
                return this;
            }

            return this;
        }

        public PoseClip Hips(params PoseKey[] keys)
        {
            HipsOffset = keys;
            return this;
        }

        /// <summary>Sets the Root rotation pivot as a fraction of character height.</summary>
        public PoseClip Pivot(float heightFraction)
        {
            RootPivot01 = heightFraction;
            return this;
        }

        /// <summary>Marks the clip as terminal - it settles on its last frame.</summary>
        public PoseClip Hold()
        {
            HoldLastFrame = true;
            return this;
        }

        /// <summary>
        /// Evaluates every track at <paramref name="time"/> seconds and blends the
        /// result into <paramref name="accumulator"/> by <paramref name="weight"/>.
        /// </summary>
        public void Sample(float time, float weight, Quaternion[] accumulator, bool[] touched,
            ref Vector3 hipsOffset)
        {
            if (weight <= 0.001f) return;
            float t = Loop ? Mathf.Repeat(time, Duration) : Mathf.Clamp(time, 0f, Duration);

            for (int i = 0; i < _tracks.Count; i++)
            {
                BoneTrack track = _tracks[i];
                int index = (int)track.Bone;
                if (index < 0 || index >= accumulator.Length) continue;

                Vector3 euler = Evaluate(track.Keys, t);
                Quaternion target = Quaternion.Euler(euler);
                accumulator[index] = weight >= 0.999f
                    ? target
                    : Quaternion.Slerp(accumulator[index], target, weight);
                if (touched != null) touched[index] = true;
            }

            if (HipsOffset != null && HipsOffset.Length > 0)
                hipsOffset += Evaluate(HipsOffset, t) * weight;
        }

        /// <summary>Smoothstep interpolation between the surrounding keys.</summary>
        private static Vector3 Evaluate(PoseKey[] keys, float t)
        {
            int n = keys.Length;
            if (n == 1 || t <= keys[0].Time) return keys[0].Euler;
            if (t >= keys[n - 1].Time) return keys[n - 1].Euler;

            for (int i = 1; i < n; i++)
            {
                if (t > keys[i].Time) continue;
                float span = keys[i].Time - keys[i - 1].Time;
                float u = span <= 1e-5f ? 1f : (t - keys[i - 1].Time) / span;
                u = u * u * (3f - 2f * u);
                return Vector3.LerpUnclamped(keys[i - 1].Euler, keys[i].Euler, u);
            }

            return keys[n - 1].Euler;
        }
    }
}
