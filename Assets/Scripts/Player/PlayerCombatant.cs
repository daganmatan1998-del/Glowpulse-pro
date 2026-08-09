using Glowpulse.Combat;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.Player
{
    /// <summary>
    /// The player's combat identity. Phase 1 gives it health, stamina and the
    /// shared reaction handling; blocking, parrying and counters are layered on
    /// in the combat phase by overriding the defence hooks.
    /// </summary>
    public sealed class PlayerCombatant : Combatant
    {
        [Header("Base stats")]
        [SerializeField] private float _baseHealth = 140f;
        [SerializeField] private float _baseStamina = 110f;

        /// <summary>Set by the combat system while the guard is up.</summary>
        public bool GuardHeld { get; set; }

        public override bool IsBlocking => GuardHeld && IsAlive && !IsDown;

        protected override void Awake()
        {
            base.Awake();

            Faction = Faction.Player;
            DisplayName = "Player";

            Health?.Configure(_baseHealth);
            Stamina?.Configure(_baseStamina);
        }

        /// <summary>
        /// Applies progression bonuses. Kept as one call so the skill tree does
        /// not need to know how the stats are stored.
        /// </summary>
        public void ApplyStatBonuses(float bonusHealth, float bonusStamina)
        {
            Health?.SetMax(_baseHealth + bonusHealth, preserveRatio: true);
            Stamina?.SetMax(_baseStamina + bonusStamina, preserveRatio: true);
        }
    }
}
