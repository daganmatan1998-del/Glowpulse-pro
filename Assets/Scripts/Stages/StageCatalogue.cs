using System.Collections.Generic;
using Glowpulse.Enemies;
using Glowpulse.World;

namespace Glowpulse.Stages
{
    /// <summary>
    /// The five stages, in order.
    ///
    /// The progression is a difficulty curve made of composition rather than of
    /// numbers: stage one teaches the basic fight, stage two introduces something
    /// you cannot mash through, stage three adds a flanker to the mix so you can
    /// no longer face one direction, stage four makes you break a guard under
    /// pressure and then puts a wall in front of you, and stage five combines all
    /// of it and ends with a fight that changes as it loses.
    ///
    /// Each stage also changes where and when it is fought, because five fights
    /// in the same square would be one fight repeated five times whatever the
    /// enemy list said.
    /// </summary>
    public static class StageCatalogue
    {
        private static StageDefinition[] _stages;

        public static IReadOnlyList<StageDefinition> Stages
        {
            get
            {
                if (_stages == null) Build();
                return _stages;
            }
        }

        public static int Count => Stages.Count;

        public static StageDefinition Get(int index)
        {
            if (_stages == null) Build();
            if (index < 0 || index >= _stages.Length) return null;
            return _stages[index];
        }

        /// <summary>The last stage, so callers do not have to know the count.</summary>
        public static StageDefinition Last => Get(Count - 1);

        private static void Build()
        {
            _stages = new[]
            {
                new StageDefinition
                {
                    Index = 0,
                    Name = "THE STREETS",
                    Tagline = "Somebody has been leaning on the shopkeepers.",
                    Arena = ArenaKind.Streets,
                    TimeOfDay = TimeOfDayPreset.GoldenHour,
                    ArenaRadius = 17f,
                    ExperienceReward = 250,
                    MoneyReward = 120,
                    HasCivilians = true
                }
                // Brawlers first so the opening fight teaches the basic exchange,
                // then a pair of runners to show that standing still is punished.
                .Wave(EnemyKind.Brawler, 3)
                .Wave(EnemyKind.Runner, 2, 6f),

                new StageDefinition
                {
                    Index = 1,
                    Name = "THE ABANDONED WAREHOUSE",
                    Tagline = "Whatever they are moving, it is in here.",
                    Arena = ArenaKind.Warehouse,
                    TimeOfDay = TimeOfDayPreset.Dusk,
                    ArenaRadius = 15f,
                    ExperienceReward = 400,
                    MoneyReward = 200,

                    // Indoors, so the street empties out.
                    HasCivilians = false
                }
                // The bruiser is the lesson: poise means mashing stops working.
                .Wave(EnemyKind.Brawler, 4)
                .Wave(EnemyKind.Bruiser, 2, 8f),

                new StageDefinition
                {
                    Index = 2,
                    Name = "THE DOCKS",
                    Tagline = "Cold night, open ground, nowhere to hide.",
                    Arena = ArenaKind.Docks,
                    TimeOfDay = TimeOfDayPreset.Night,
                    ArenaRadius = 22f,
                    ExperienceReward = 650,
                    MoneyReward = 320,
                    HasCivilians = false
                }
                // Open ground plus flankers: the first fight you cannot win facing
                // one direction.
                .Wave(EnemyKind.Runner, 3)
                .Wave(EnemyKind.Bruiser, 2, 7f)
                .Wave(EnemyKind.Elite, 1, 15f),

                new StageDefinition
                {
                    Index = 3,
                    Name = "THE UNDERGROUND ARENA",
                    Tagline = "They paid to watch this one.",
                    Arena = ArenaKind.Underground,
                    TimeOfDay = TimeOfDayPreset.Night,
                    Objective = StageObjective.DefeatMiniBoss,
                    ArenaRadius = 13f,
                    ExperienceReward = 1000,
                    MoneyReward = 550,
                    HasCivilians = false
                }
                // Defenders force the guard-breaking tools out, and then the
                // mini-boss arrives while the player is already committed.
                .Wave(EnemyKind.Defender, 2)
                .Wave(EnemyKind.Elite, 2, 9f)
                .Wave(EnemyKind.MiniBoss, 1, 20f),

                new StageDefinition
                {
                    Index = 4,
                    Name = "THE ROOFTOP",
                    Tagline = "The whole city is watching this one end.",
                    Arena = ArenaKind.Rooftop,
                    TimeOfDay = TimeOfDayPreset.Dusk,
                    Objective = StageObjective.DefeatFinalBoss,
                    ArenaRadius = 20f,
                    ExperienceReward = 2000,
                    MoneyReward = 1200,
                    HasCivilians = false
                }
                // Everything the player has learned, then the fight that changes
                // as it loses.
                .Wave(EnemyKind.Elite, 2)
                .Wave(EnemyKind.Defender, 1, 6f)
                .Wave(EnemyKind.Runner, 2, 11f)
                .Wave(EnemyKind.FinalBoss, 1, 18f)
            };
        }

        /// <summary>Every problem across every stage, for the tests and a load check.</summary>
        public static List<string> ValidateAll()
        {
            var problems = new List<string>();

            for (int i = 0; i < Count; i++)
            {
                StageDefinition stage = Get(i);
                if (stage.Index != i)
                    problems.Add($"stage at position {i} thinks it is stage {stage.Number}");

                problems.AddRange(stage.Validate());
            }

            return problems;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _stages = null;
    }
}
