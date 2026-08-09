using UnityEngine;

namespace Glowpulse.Core
{
    /// <summary>Small math helpers shared across movement, camera and AI.</summary>
    public static class MathUtil
    {
        /// <summary>
        /// Frame-rate independent exponential smoothing. <paramref name="sharpness"/>
        /// is "how much of the gap is closed per second", so the result is identical
        /// at 30 and 240 fps - unlike a raw Lerp(a, b, speed * dt).
        /// </summary>
        public static float Damp(float current, float target, float sharpness, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
        }

        public static Vector3 Damp(Vector3 current, Vector3 target, float sharpness, float dt)
        {
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
        }

        public static Quaternion Damp(Quaternion current, Quaternion target, float sharpness, float dt)
        {
            return Quaternion.Slerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
        }

        public static float DampAngle(float current, float target, float sharpness, float dt)
        {
            return Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-sharpness * dt));
        }

        /// <summary>Flattens a vector onto the XZ plane and normalises it.</summary>
        public static Vector3 FlatDirection(Vector3 v)
        {
            v.y = 0f;
            float m = v.magnitude;
            return m > 1e-5f ? v / m : Vector3.zero;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>Signed yaw in degrees needed to turn <paramref name="from"/> onto <paramref name="to"/>.</summary>
        public static float SignedYaw(Vector3 from, Vector3 to)
        {
            return Mathf.DeltaAngle(
                Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg,
                Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        }

        public static float Remap(float v, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMax, inMin)) return outMin;
            return outMin + (outMax - outMin) * Mathf.Clamp01((v - inMin) / (inMax - inMin));
        }

        /// <summary>Smooth 0..1 curve with zero derivative at both ends.</summary>
        public static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        /// <summary>Deterministic 0..1 noise from a float seed - cheap stand-in for Perlin on one axis.</summary>
        public static float Noise01(float t, float seed)
        {
            return Mathf.PerlinNoise(t, seed);
        }

        /// <summary>Symmetric -1..1 noise.</summary>
        public static float NoiseSigned(float t, float seed)
        {
            return Mathf.PerlinNoise(t, seed) * 2f - 1f;
        }

        /// <summary>Projects a world point onto the ground, returning false if nothing was found.</summary>
        public static bool GroundPoint(Vector3 origin, out Vector3 point, float up = 3f, float down = 20f,
            int mask = ~0)
        {
            if (Physics.Raycast(origin + Vector3.up * up, Vector3.down, out RaycastHit hit, up + down, mask,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            point = origin;
            return false;
        }

        /// <summary>Uniform random point inside a ring on the XZ plane.</summary>
        public static Vector3 RandomPointInRing(Vector3 center, float innerRadius, float outerRadius)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Lerp(innerRadius * innerRadius, outerRadius * outerRadius, Random.value));
            return center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }
    }
}
