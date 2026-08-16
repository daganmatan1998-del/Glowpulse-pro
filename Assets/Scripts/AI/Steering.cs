using System.Collections.Generic;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.AI
{
    /// <summary>
    /// Local steering for characters that move without a navmesh: seek a point,
    /// push away from neighbours, and slide along obstacles instead of grinding
    /// into them.
    ///
    /// A beat 'em up is fought in open streets and small arenas, where steering
    /// reads better than pathfinding - enemies flow around each other instead of
    /// queueing along a computed path. Long-range navigation around buildings is
    /// a separate concern for the open-world phase.
    /// </summary>
    public static class Steering
    {
        private static readonly Collider[] Neighbours = new Collider[16];

        /// <summary>Velocity that heads for a point and eases off as it arrives.</summary>
        public static Vector3 Arrive(Vector3 position, Vector3 target, float maxSpeed,
            float slowRadius = 1.4f, float stopRadius = 0.25f)
        {
            Vector3 to = MathUtil.Flat(target - position);
            float distance = to.magnitude;

            if (distance <= stopRadius) return Vector3.zero;
            if (distance < 1e-4f) return Vector3.zero;

            float speed = distance < slowRadius
                ? maxSpeed * (distance - stopRadius) / Mathf.Max(0.01f, slowRadius - stopRadius)
                : maxSpeed;

            return to / distance * Mathf.Clamp(speed, 0f, maxSpeed);
        }

        /// <summary>Velocity that circles a point at a fixed radius.</summary>
        public static Vector3 Orbit(Vector3 position, Vector3 center, float radius, float speed,
            int direction)
        {
            Vector3 fromCenter = MathUtil.Flat(position - center);
            float distance = fromCenter.magnitude;
            if (distance < 0.05f) return Vector3.zero;

            Vector3 radial = fromCenter / distance;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * Mathf.Sign(direction);

            // Blend the tangent with a correction toward the desired radius, so
            // the orbit settles onto the ring instead of spiralling.
            float error = Mathf.Clamp((distance - radius) / Mathf.Max(0.5f, radius), -1f, 1f);
            Vector3 desired = (tangent * (1f - Mathf.Abs(error) * 0.6f) - radial * error).normalized;

            return desired * speed;
        }

        /// <summary>
        /// Repulsion from nearby characters. Without this, everything converges on
        /// the same point and the crowd becomes one clump.
        /// </summary>
        public static Vector3 Separation(Transform self, float radius, float strength,
            LayerMask mask)
        {
            int count = Physics.OverlapSphereNonAlloc(self.position + Vector3.up, radius, Neighbours,
                mask, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            int considered = 0;

            for (int i = 0; i < count; i++)
            {
                Transform other = Neighbours[i].transform;
                if (other == self || other.IsChildOf(self)) continue;

                Vector3 away = MathUtil.Flat(self.position - other.position);
                float distance = away.magnitude;
                if (distance < 1e-3f)
                {
                    // Exactly overlapping: pick a deterministic direction from the
                    // instance ids so the two do not both jump the same way.
                    float angle = (self.GetInstanceID() ^ other.GetInstanceID()) % 360;
                    away = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    distance = 0.01f;
                }

                // Inverse falloff: only genuinely close neighbours matter.
                push += away / distance * (1f - Mathf.Clamp01(distance / radius));
                considered++;
            }

            if (considered == 0) return Vector3.zero;
            return push / considered * strength;
        }

        /// <summary>
        /// Steers around walls using three forward whiskers. Returns a correction
        /// to add to the desired velocity, or zero when the path is clear.
        /// </summary>
        public static Vector3 AvoidObstacles(Transform self, Vector3 desiredVelocity, float probeDistance,
            LayerMask mask, float bodyRadius = 0.35f)
        {
            Vector3 direction = MathUtil.FlatDirection(desiredVelocity);
            if (direction.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 origin = self.position + Vector3.up * 0.9f;
            Vector3 correction = Vector3.zero;

            // Centre whisker first: a head-on wall needs the strongest response.
            if (Physics.SphereCast(origin, bodyRadius, direction, out RaycastHit hit, probeDistance,
                    mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 slide = Vector3.ProjectOnPlane(direction, MathUtil.FlatDirection(hit.normal));
                float severity = 1f - Mathf.Clamp01(hit.distance / probeDistance);
                correction += MathUtil.FlatDirection(slide) * severity * 1.4f;
                correction += MathUtil.FlatDirection(hit.normal) * severity * 0.6f;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 whisker = Quaternion.Euler(0f, 38f * side, 0f) * direction;
                if (!Physics.Raycast(origin, whisker, out RaycastHit sideHit, probeDistance * 0.8f,
                        mask, QueryTriggerInteraction.Ignore)) continue;

                float severity = 1f - Mathf.Clamp01(sideHit.distance / (probeDistance * 0.8f));
                correction += MathUtil.FlatDirection(sideHit.normal) * severity * 0.7f;
            }

            return correction;
        }

        /// <summary>
        /// Combines a desired velocity with separation and avoidance, then clamps
        /// the result so steering corrections cannot make a character sprint.
        /// </summary>
        public static Vector3 Resolve(Transform self, Vector3 desired, float maxSpeed,
            float separationRadius, float separationStrength, LayerMask characterMask,
            LayerMask obstacleMask, float avoidDistance)
        {
            Vector3 combined = desired;
            combined += Separation(self, separationRadius, separationStrength, characterMask);

            if (desired.sqrMagnitude > 0.01f)
                combined += AvoidObstacles(self, desired, avoidDistance, obstacleMask) * maxSpeed * 0.5f;

            combined.y = 0f;
            float speed = combined.magnitude;
            return speed > maxSpeed ? combined / speed * maxSpeed : combined;
        }
    }
}
