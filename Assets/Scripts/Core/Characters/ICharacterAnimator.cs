using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>Broad body attitude. Drives the idle pose and how locomotion reads.</summary>
    public enum CharacterStance
    {
        Relaxed = 0,
        Combat = 1,
        Guarding = 2,
        Downed = 3,
        Dead = 4
    }

    /// <summary>
    /// The only animation surface gameplay code is allowed to touch. The
    /// placeholder implementation drives a procedural rig; a later Mecanim
    /// implementation maps the same calls onto an Animator's parameters and
    /// state names, so swapping in real animation does not touch combat code.
    /// </summary>
    public interface ICharacterAnimator
    {
        CharacterRig Rig { get; }

        /// <summary>Velocity in the character's own space, plus ground state.</summary>
        void SetLocomotion(Vector3 localVelocity, bool grounded, float verticalVelocity);

        void SetStance(CharacterStance stance);

        /// <summary>Degrees per second the body is turning - drives banking into turns.</summary>
        void SetTurnRate(float degreesPerSecond);

        /// <summary>Head tracking. Pass null to look straight ahead.</summary>
        void SetLookTarget(Transform target);

        /// <summary>Starts a one-shot action. Returns false if the clip is unknown.</summary>
        bool PlayAction(string clipId, float speedMultiplier = 1f, float fadeIn = 0.06f);

        void StopAction(float fadeOut = 0.12f);

        bool IsPlayingAction { get; }
        string CurrentActionId { get; }

        /// <summary>0..1 progress through the current action.</summary>
        float ActionNormalizedTime { get; }

        /// <summary>A short physical jolt, e.g. absorbing a hit. Direction is in character space.</summary>
        void AddImpulse(Vector3 localDirection, float strength);

        /// <summary>Immediately returns the body to its rest pose and clears all layers.</summary>
        void ResetPose();
    }
}
