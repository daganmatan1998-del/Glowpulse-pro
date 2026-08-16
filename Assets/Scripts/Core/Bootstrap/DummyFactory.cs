using Glowpulse.Combat;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Core.Bootstrap
{
    /// <summary>Creates training dummies for tuning and verifying combat.</summary>
    public static class DummyFactory
    {
        public static TrainingDummy Create(string name, Vector3 position, Quaternion rotation,
            in CharacterStyle style, Transform parent = null, bool blocks = false)
        {
            CharacterFactory.Spec spec = CharacterFactory.Spec.For(name, style,
                GameLayers.Enemy, GameTags.Enemy);

            CharacterFactory.Build build = CharacterFactory.Begin(spec, position, rotation);
            if (parent != null) build.GameObject.transform.SetParent(parent, true);

            var dummy = build.GameObject.AddComponent<TrainingDummy>();
            dummy.BlocksAttacks = blocks;

            CharacterFactory.Finish(build, dummy);

            build.Animator.SetStance(CharacterStance.Combat);
            return dummy;
        }
    }
}
