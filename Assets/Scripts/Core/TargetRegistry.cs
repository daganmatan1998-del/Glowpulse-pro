using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Core
{
    /// <summary>
    /// Keeps track of every lockable character in the scene. Characters register
    /// themselves when they wake up, which means lock-on, AI target search and
    /// crowd reactions never have to scan the scene with FindObjectsOfType.
    /// </summary>
    public static class TargetRegistry
    {
        private static readonly List<ITargetable> All = new List<ITargetable>(64);

        public static IReadOnlyList<ITargetable> Targets => All;

        public static void Register(ITargetable target)
        {
            if (target == null || All.Contains(target)) return;
            All.Add(target);
        }

        public static void Unregister(ITargetable target)
        {
            if (target == null) return;
            All.Remove(target);
        }

        /// <summary>
        /// Collects live targets of a hostile faction within a radius.
        /// The results list is cleared first and reused by the caller, so this
        /// allocates nothing per call.
        /// </summary>
        public static void Query(Vector3 origin, float radius, Faction seekerFaction,
            List<ITargetable> results, bool hostileOnly = true)
        {
            results.Clear();
            float sqr = radius * radius;

            for (int i = All.Count - 1; i >= 0; i--)
            {
                ITargetable target = All[i];

                // Registrations outlive destroyed objects for one frame at most;
                // prune them lazily rather than paying for OnDestroy ordering.
                if (target == null || target.Transform == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                if (!target.IsTargetable) continue;
                if (hostileOnly && !seekerFaction.IsHostileTo(target.Faction)) continue;
                if ((target.Transform.position - origin).sqrMagnitude > sqr) continue;

                results.Add(target);
            }
        }

        /// <summary>Nearest hostile target within a radius, or null.</summary>
        public static ITargetable Nearest(Vector3 origin, float radius, Faction seekerFaction)
        {
            ITargetable best = null;
            float bestSqr = radius * radius;

            for (int i = All.Count - 1; i >= 0; i--)
            {
                ITargetable target = All[i];
                if (target == null || target.Transform == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                if (!target.IsTargetable) continue;
                if (!seekerFaction.IsHostileTo(target.Faction)) continue;

                float sqr = (target.Transform.position - origin).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = target;
            }

            return best;
        }

        public static int CountHostiles(Vector3 origin, float radius, Faction seekerFaction)
        {
            int count = 0;
            float sqr = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                ITargetable t = All[i];
                if (t == null || t.Transform == null || !t.IsTargetable) continue;
                if (!seekerFaction.IsHostileTo(t.Faction)) continue;
                if ((t.Transform.position - origin).sqrMagnitude <= sqr) count++;
            }

            return count;
        }

        public static void Clear() => All.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();
    }
}
