namespace Glowpulse.Core.Settings
{
    /// <summary>
    /// Every rebindable action, as a stable identity.
    ///
    /// The enum is what the binding table, the settings screen and the save file
    /// all agree on, so adding an action is one entry here plus a default - not a
    /// change in three places that can drift apart.
    /// </summary>
    public enum GameAction
    {
        MoveForward = 0,
        MoveBackward = 1,
        MoveLeft = 2,
        MoveRight = 3,
        Jump = 4,
        Attack = 5,
        HeavyAttack = 6,
        Dodge = 7,
        Block = 8,
        Interact = 9,
        Sprint = 10,
        Grab = 11,
        LockOn = 12,

        Count = 13
    }

    public static class GameActions
    {
        /// <summary>Label shown in the controls list.</summary>
        public static string DisplayName(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveForward: return "Move Forward";
                case GameAction.MoveBackward: return "Move Backward";
                case GameAction.MoveLeft: return "Move Left";
                case GameAction.MoveRight: return "Move Right";
                case GameAction.Jump: return "Jump";
                case GameAction.Attack: return "Attack";
                case GameAction.HeavyAttack: return "Heavy Attack";
                case GameAction.Dodge: return "Dodge";
                case GameAction.Block: return "Block";
                case GameAction.Interact: return "Interact";
                case GameAction.Sprint: return "Sprint";
                case GameAction.Grab: return "Grab";
                case GameAction.LockOn: return "Lock On";
                default: return action.ToString();
            }
        }

        /// <summary>
        /// The order the settings screen lists actions in - movement first, then
        /// what you do with it. The enum order is an implementation detail; this
        /// is the one people read.
        /// </summary>
        public static readonly GameAction[] Listed =
        {
            GameAction.MoveForward, GameAction.MoveBackward,
            GameAction.MoveLeft, GameAction.MoveRight,
            GameAction.Sprint, GameAction.Jump, GameAction.Dodge,
            GameAction.Attack, GameAction.HeavyAttack, GameAction.Block,
            GameAction.Grab, GameAction.LockOn, GameAction.Interact
        };
    }
}
