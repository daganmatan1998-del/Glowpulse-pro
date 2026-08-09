using UnityEngine;

namespace Glowpulse.Core
{
    /// <summary>Anything that can receive a hit.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Faction Faction { get; }
        Transform Transform { get; }

        /// <summary>Applies a hit and reports what happened so the attacker can react.</summary>
        HitResult ApplyDamage(in DamageInfo info);
    }

    /// <summary>Anything the lock-on system can focus on.</summary>
    public interface ITargetable
    {
        bool IsTargetable { get; }
        Faction Faction { get; }
        Transform Transform { get; }

        /// <summary>Point the camera and the reticle should aim at (roughly chest height).</summary>
        Vector3 AimPoint { get; }
    }

    /// <summary>A character that can be staggered, knocked down or shoved.</summary>
    public interface IStaggerable
    {
        void ApplyStagger(float duration, Vector3 direction, HitImpact impact);
        void ApplyKnockback(Vector3 velocity);
        bool IsDown { get; }
    }

    /// <summary>Implemented by systems that want a per-frame tick from a central pump.</summary>
    public interface IManagedTick
    {
        void ManagedTick(float deltaTime);
    }
}
