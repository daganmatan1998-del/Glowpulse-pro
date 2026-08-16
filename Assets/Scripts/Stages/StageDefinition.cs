using System.Collections.Generic;
using Glowpulse.Enemies;
using Glowpulse.World;
using UnityEngine;

namespace Glowpulse.Stages
{
    /// <summary>What a stage asks of the player.</summary>
    public enum StageObjective
    {
        DefeatAll = 0,
        DefeatMiniBoss = 1,
        DefeatFinalBoss = 2
    }

    /// <summary>The shape of the ground a stage is fought on.</summary>
    public enum ArenaKind
    {
        /// <summary>Streets and alleys. Tight, with the city around you.</summary>
        Streets = 0,

        /// <summary>Interior: pillars, crates, a low roof. Cover to fight around.</summary>
        Warehouse = 1,

        /// <summary>Open ground at night, containers and water on three sides.</summary>
        Docks = 2,

        /// <summary>A pit ringed by a barrier. Nowhere to run.</summary>
        Underground = 3,

        /// <summary>A rooftop above the skyline. Wide, exposed, edges that matter.</summary>
        Rooftop = 4
    }

    /// <summary>One group of enemies a stage sends in.</summary>
    public struct EnemyWave
    {
        public EnemyKind Kind;
        public int Count;

        /// <summary>Seconds after the stage starts before this wave appears.</summary>
        public float Delay;

        public EnemyWave(EnemyKind kind, int count, float delay = 0f)
        {
            Kind = kind;
            Count = count;
            Delay = delay;
        }
    }

    /// <summary>
    /// A stage, as data.
    ///
    /// Everything that makes stage three different from stage one lives in one of
    /// these: the arena it is fought in, the light it is fought under, and which
    /// fighters turn up. Adding a stage is an entry in
    /// <see cref="StageCatalogue"/>, not a new class - and because it is plain
    /// data the whole progression can be checked in a test rather than by playing
    /// through five fights.
    /// </summary>
    public sealed class StageDefinition
    {
        /// <summary>Zero-based. Displayed as "STAGE 1" for index 0.</summary>
        public int Index;

        public string Name;

        /// <summary>One line shown under the title on the card.</summary>
        public string Tagline;

        public ArenaKind Arena;
        public TimeOfDayPreset TimeOfDay;
        public StageObjective Objective = StageObjective.DefeatAll;

        public readonly List<EnemyWave> Waves = new List<EnemyWave>(4);

        /// <summary>Paid out on the first clear.</summary>
        public int ExperienceReward;

        public int MoneyReward;

        /// <summary>How wide the fighting area is, in metres.</summary>
        public float ArenaRadius = 16f;

        /// <summary>Whether civilians are on the street. False once the fights go private.</summary>
        public bool HasCivilians = true;

        public int Number => Index + 1;

        public string DisplayNumber => "STAGE " + Number;

        /// <summary>Total enemies across every wave. The HUD counts against this.</summary>
        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Waves.Count; i++) total += Waves[i].Count;
                return total;
            }
        }

        public bool HasBoss
        {
            get
            {
                for (int i = 0; i < Waves.Count; i++)
                    if (EnemyArchetype.IsBoss(Waves[i].Kind)) return true;
                return false;
            }
        }

        /// <summary>The line shown in the HUD under OBJECTIVE.</summary>
        public string ObjectiveText
        {
            get
            {
                switch (Objective)
                {
                    case StageObjective.DefeatMiniBoss: return "DEFEAT THE BULLDOZER";
                    case StageObjective.DefeatFinalBoss: return "DEFEAT KASKADE";
                    default: return "DEFEAT ALL ENEMIES";
                }
            }
        }

        public StageDefinition Wave(EnemyKind kind, int count, float delay = 0f)
        {
            Waves.Add(new EnemyWave(kind, count, delay));
            return this;
        }

        /// <summary>
        /// Reports anything that would make the stage unplayable. Run by the tests
        /// and once at load, because a stage with no enemies is a stage that can
        /// never be completed and therefore blocks every stage behind it.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(Name)) problems.Add($"stage {Number} has no name");
            if (Waves.Count == 0) problems.Add($"stage {Number} has no enemies");
            if (ArenaRadius < 6f) problems.Add($"stage {Number} has no room to fight in");

            for (int i = 0; i < Waves.Count; i++)
            {
                if (Waves[i].Count <= 0)
                    problems.Add($"stage {Number} wave {i} sends nobody");
                if (Waves[i].Delay < 0f)
                    problems.Add($"stage {Number} wave {i} arrives before the stage starts");
            }

            // An objective the stage cannot satisfy would leave the player fighting
            // forever with nothing left to kill.
            if (Objective == StageObjective.DefeatMiniBoss && !Contains(EnemyKind.MiniBoss))
                problems.Add($"stage {Number} asks for a mini-boss it never spawns");

            if (Objective == StageObjective.DefeatFinalBoss && !Contains(EnemyKind.FinalBoss))
                problems.Add($"stage {Number} asks for a final boss it never spawns");

            return problems;
        }

        public bool Contains(EnemyKind kind)
        {
            for (int i = 0; i < Waves.Count; i++)
                if (Waves[i].Kind == kind) return true;
            return false;
        }
    }
}
