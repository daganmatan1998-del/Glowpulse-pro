using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>
    /// A bystander's combat identity. Civilians are not fighters and are not
    /// punching bags: the faction rules already make them immune to every attack
    /// in the game, and this type makes that explicit rather than incidental.
    ///
    /// It still derives from <see cref="Combatant"/>, because everything else the
    /// class provides - staggering, being knocked over, getting back up, the
    /// animation hooks that go with all three - is exactly what a shoved
    /// pedestrian needs, and reimplementing it would mean maintaining two copies
    /// of a reaction pipeline that is already tested.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CivilianCombatant : Combatant
    {
        /// <summary>
        /// Civilians are never lock-on targets. A target reticle snapping to a
        /// pedestrian mid-fight is one of the fastest ways to make a combat system
        /// feel broken.
        /// </summary>
        public override bool IsTargetable => false;

        protected override void Awake()
        {
            base.Awake();
            _faction = Faction.Civilian;
            CivilianPoses.EnsureRegistered();
        }

        /// <summary>
        /// Nothing can hurt a civilian. Damage is refused here as well as by the
        /// faction rules so that a future attack which skips faction filtering -
        /// an explosion, a thrown enemy - still cannot kill a bystander by
        /// accident.
        /// </summary>
        protected override HitResult EvaluateDefence(in DamageInfo info) => HitResult.Immune;

        public override bool CanBeGrabbed => false;

        /// <summary>
        /// Barged into by somebody moving fast. Knocks the civilian off their line
        /// and plays a stumble, without any of the damage machinery - the physical
        /// reaction is the whole point.
        /// </summary>
        public void Shove(Vector3 direction, float strength)
        {
            if (!IsAlive || IsDown) return;

            Vector3 push = MathUtil.FlatDirection(direction) * Mathf.Clamp(strength, 1f, 8f);
            ApplyKnockback(push);

            Animator?.AddImpulse(transform.InverseTransformDirection(push.normalized), 1.3f);
            Animator?.PlayAction(CivilianPoses.Stumble);
        }
    }
}
