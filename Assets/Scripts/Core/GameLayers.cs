using UnityEngine;

namespace Glowpulse.Core
{
    /// <summary>
    /// Single source of truth for the project's layers and tags.
    /// Layer indices are resolved by name at first use so the game keeps working
    /// even if TagManager.asset is regenerated with a different ordering.
    /// </summary>
    public static class GameLayers
    {
        public const string PlayerName = "Player";
        public const string EnemyName = "Enemy";
        public const string NpcName = "NPC";
        public const string GroundName = "Ground";
        public const string EnvironmentName = "Environment";
        public const string PropName = "Prop";
        public const string HitboxName = "Hitbox";
        public const string InteractableName = "Interactable";

        private static bool _resolved;

        private static int _player, _enemy, _npc, _ground, _environment, _prop, _hitbox, _interactable;

        public static int Player { get { Resolve(); return _player; } }
        public static int Enemy { get { Resolve(); return _enemy; } }
        public static int Npc { get { Resolve(); return _npc; } }
        public static int Ground { get { Resolve(); return _ground; } }
        public static int Environment { get { Resolve(); return _environment; } }
        public static int Prop { get { Resolve(); return _prop; } }
        public static int Hitbox { get { Resolve(); return _hitbox; } }
        public static int Interactable { get { Resolve(); return _interactable; } }

        // Every mask resolves the layer indices first. Reading a mask before
        // Resolve() has run would otherwise silently produce "Default only",
        // which breaks ground detection and hit registration in ways that are
        // very hard to trace back to here.

        /// <summary>Everything a character or the camera should treat as solid world.</summary>
        public static LayerMask WorldMask
        {
            get { Resolve(); return Mask(_ground, _environment, _prop, 0); }
        }

        /// <summary>Solid geometry the camera must not pass through.</summary>
        public static LayerMask CameraBlockerMask
        {
            get { Resolve(); return Mask(_ground, _environment, _prop, 0); }
        }

        /// <summary>All character layers - used for hit detection and target search.</summary>
        public static LayerMask CharacterMask
        {
            get { Resolve(); return Mask(_player, _enemy, _npc); }
        }

        public static LayerMask EnemyMask
        {
            get { Resolve(); return Mask(_enemy); }
        }

        public static LayerMask PlayerMask
        {
            get { Resolve(); return Mask(_player); }
        }

        /// <summary>Anything that should block line of sight between two characters.</summary>
        public static LayerMask SightBlockerMask
        {
            get { Resolve(); return Mask(_ground, _environment, _prop, 0); }
        }

        public static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _player = Find(PlayerName, 6);
            _enemy = Find(EnemyName, 7);
            _npc = Find(NpcName, 8);
            _ground = Find(GroundName, 9);
            _environment = Find(EnvironmentName, 10);
            _prop = Find(PropName, 11);
            _hitbox = Find(HitboxName, 12);
            _interactable = Find(InteractableName, 13);
        }

        /// <summary>
        /// Applies the collision matrix the combat systems assume. Called from the
        /// bootstrap so standalone builds match the editor, and from the editor
        /// validator so the project asset stays in sync.
        /// </summary>
        public static void ApplyCollisionMatrix()
        {
            Resolve();

            // Hitboxes are pure triggers that only ever need to find characters.
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(_hitbox, i, true);
            Physics.IgnoreLayerCollision(_hitbox, _player, false);
            Physics.IgnoreLayerCollision(_hitbox, _enemy, false);
            Physics.IgnoreLayerCollision(_hitbox, _npc, false);

            // Civilians never physically block combatants; they steer around them
            // in their own avoidance code instead of fighting the character controller.
            Physics.IgnoreLayerCollision(_npc, _npc, true);
            Physics.IgnoreLayerCollision(_npc, _enemy, true);
        }

        private static LayerMask Mask(params int[] layers)
        {
            int m = 0;
            for (int i = 0; i < layers.Length; i++) m |= 1 << layers[i];
            return m;
        }

        private static int Find(string name, int fallback)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : fallback;
        }

        /// <summary>Assigns a layer to a transform and its whole hierarchy.</summary>
        public static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }

    /// <summary>Tag strings used by the project.</summary>
    public static class GameTags
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string Npc = "NPC";
        public const string MissionMarker = "MissionMarker";
        public const string Interactable = "Interactable";
    }
}
