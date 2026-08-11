using Glowpulse.Audio;
using Glowpulse.CameraSystem;
using Glowpulse.Core;
using Glowpulse.Core.Timing;
using Glowpulse.VFX;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// Turns a resolved hit into everything the player feels: the freeze, the
    /// shake, the sparks and the sound.
    ///
    /// Keeping it in one place means "does combat feel good" is a question about
    /// one file, and it guarantees a hit never lands with only half its feedback.
    /// The ordering matters - hit stop first, so the freeze starts on the same
    /// frame as the spark.
    /// </summary>
    public static class CombatFeedback
    {
        /// <summary>Scales all hit stop. Dropped to zero it makes combat feel weightless.</summary>
        public static float HitStopScale = 1f;

        /// <summary>Scales all camera shake, for an accessibility setting.</summary>
        public static float ShakeScale = 1f;

        /// <summary>Enables the brief slow-motion on the heaviest hits.</summary>
        public static bool SlowMotionEnabled = true;

        /// <summary>
        /// Violence happened at a position, with a severity from 0 to 1.
        ///
        /// This exists so the rest of the game can react to a fight without combat
        /// having to know who is listening. The crowd uses it to panic; missions
        /// and, later, a police response can hang off the same signal. Combat
        /// itself stays a leaf.
        /// </summary>
        public static event System.Action<Vector3, float> Commotion;

        /// <summary>
        /// Reports a commotion from outside the normal hit pipeline - a boss
        /// changing phase, an explosion, anything the crowd should react to.
        /// </summary>
        public static void RaiseCommotion(Vector3 point, float severity)
        {
            Commotion?.Invoke(point, Mathf.Clamp01(severity));
        }

        /// <summary>A hit that landed on a character.</summary>
        public static void Landed(AttackDefinition move, in DamageInfo info, HitResult result,
            Vector3 point, bool isPlayerAttacker)
        {
            switch (result)
            {
                case HitResult.Blocked:
                    Blocked(move, info, point);
                    return;
                case HitResult.Parried:
                    Parried(move, info, point);
                    return;
                case HitResult.Missed:
                case HitResult.Immune:
                    return;
            }

            bool killed = result == HitResult.Killed;

            // The player's own hits get the full treatment; hits taken get a
            // muted version, so the screen does not thrash when surrounded.
            float weight = isPlayerAttacker ? 1f : 0.45f;

            TimeController.RequestHitStop(move.HitStop * HitStopScale * weight);

            CameraShaker.Shake(move.CameraShake * ShakeScale * weight);
            if (move.CameraKick > 0f)
                CameraShaker.Kick(info.Direction, move.CameraKick * ShakeScale * weight);

            if (SlowMotionEnabled && move.SlowMotionScale < 0.999f && isPlayerAttacker)
                TimeController.RequestSlowMotion(move.SlowMotionDuration, move.SlowMotionScale);

            ImpactVisual visual = ResolveVisual(move, killed);
            ImpactEffects.Play(visual, point, info.Direction, VisualScale(move));

            AudioManager.PlayAt(ResolveImpactSound(move), point, isPlayerAttacker ? 1f : 0.8f);

            if (!killed && info.Impact >= HitImpact.Medium)
                AudioManager.PlayAt(Sfx.Grunt, point, 0.7f, 0.14f);

            RaiseCommotion(point, killed ? 1f : Mathf.Clamp01(0.5f + (int)info.Impact * 0.15f));
        }

        /// <summary>A hit that was absorbed on the defender's guard.</summary>
        public static void Blocked(AttackDefinition move, in DamageInfo info, Vector3 point)
        {
            TimeController.RequestHitStop(move.HitStop * 0.6f * HitStopScale);
            CameraShaker.Shake(move.CameraShake * 0.4f * ShakeScale);
            ImpactEffects.Play(ImpactVisual.Block, point, info.Direction, 0.9f);
            AudioManager.PlayAt(Sfx.Block, point, 0.9f);
            RaiseCommotion(point, 0.55f);
        }

        /// <summary>
        /// A perfectly timed block. This gets the loudest, brightest, slowest
        /// feedback in the game because it is the hardest thing to do.
        /// </summary>
        public static void Parried(AttackDefinition move, in DamageInfo info, Vector3 point)
        {
            TimeController.RequestHitStop(0.13f * HitStopScale, 0.01f);
            if (SlowMotionEnabled) TimeController.RequestSlowMotion(0.3f, 0.4f);

            CameraShaker.Shake(0.32f * ShakeScale);
            ImpactEffects.Play(ImpactVisual.Parry, point, info.Direction, 1.15f);
            AudioManager.PlayAt(Sfx.Parry, point, 1f, 0.03f);
            RaiseCommotion(point, 0.8f);
        }

        /// <summary>The defender's guard was broken by accumulated damage.</summary>
        public static void GuardBroken(Vector3 point)
        {
            TimeController.RequestHitStop(0.1f * HitStopScale);
            CameraShaker.Shake(0.38f * ShakeScale);
            ImpactEffects.Play(ImpactVisual.HeavyHit, point, Vector3.up, 1.2f);
            AudioManager.PlayAt(Sfx.GuardBreak, point);
        }

        /// <summary>A character hitting the floor.</summary>
        public static void Knockdown(Vector3 point)
        {
            CameraShaker.Shake(0.18f * ShakeScale);
            ImpactEffects.Play(ImpactVisual.Slam, point, Vector3.up, 1f);
            AudioManager.PlayAt(Sfx.BodyFall, point, 0.9f);
        }

        /// <summary>The swing itself, played as the active frames open.</summary>
        public static void Swing(AttackDefinition move, Vector3 point)
        {
            bool heavy = move.Kind == AttackKind.Heavy || move.Kind == AttackKind.Finisher;
            AudioManager.PlayAt(heavy ? Sfx.WhooshHeavy : Sfx.Whoosh, point, heavy ? 0.85f : 0.6f, 0.12f);
        }

        public static void Dodge(Vector3 point)
        {
            AudioManager.PlayAt(Sfx.Dodge, point, 0.5f, 0.1f);
            ImpactEffects.Play(ImpactVisual.Dust, point, Vector3.up, 0.6f);
        }

        public static void Land(Vector3 point, float speed)
        {
            float weight = Mathf.Clamp01(speed / 12f);
            AudioManager.PlayAt(Sfx.Land, point, 0.3f + weight * 0.6f);
            if (weight > 0.4f)
            {
                ImpactEffects.Play(ImpactVisual.Dust, point, Vector3.up, 0.5f + weight);
                CameraShaker.Shake(weight * 0.16f * ShakeScale);
            }
        }

        public static void Death(Vector3 point)
        {
            AudioManager.PlayAt(Sfx.Death, point, 0.9f, 0.12f);
            ImpactEffects.Play(ImpactVisual.Dust, point, Vector3.up, 0.8f);
        }

        // ---- mapping ---------------------------------------------------------------

        private static ImpactVisual ResolveVisual(AttackDefinition move, bool killed)
        {
            if (killed || move.Impact == HitImpact.Knockdown || move.Kind == AttackKind.Throw)
                return ImpactVisual.Slam;

            return move.Kind == AttackKind.Heavy || move.Kind == AttackKind.Counter
                   || move.Kind == AttackKind.Finisher
                ? ImpactVisual.HeavyHit
                : ImpactVisual.LightHit;
        }

        private static float VisualScale(AttackDefinition move)
        {
            switch (move.Kind)
            {
                case AttackKind.Finisher: return 1.5f;
                case AttackKind.Heavy: return 1.2f;
                case AttackKind.Counter: return 1.25f;
                case AttackKind.Throw: return 1.3f;
                default: return 0.9f;
            }
        }

        private static string ResolveImpactSound(AttackDefinition move)
        {
            switch (move.ImpactTag)
            {
                case "kick": return Sfx.Kick;
                case "heavy": return Sfx.Heavy;
                case "finisher": return Sfx.Finisher;
                case "throw": return Sfx.Throw;
                case "grab": return Sfx.Grab;
                default: return Sfx.Punch;
            }
        }

        /// <summary>
        /// Drops subscribers between play sessions. Without this the editor's
        /// "no domain reload" option keeps last session's listeners alive, and the
        /// event starts firing into destroyed objects the moment combat resumes.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Commotion = null;
            HitStopScale = 1f;
            ShakeScale = 1f;
            SlowMotionEnabled = true;
        }
    }
}
