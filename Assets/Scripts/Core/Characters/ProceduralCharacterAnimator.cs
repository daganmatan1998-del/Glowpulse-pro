using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>
    /// Drives a <see cref="CharacterRig"/> without any imported animation.
    /// Three layers are composited every frame:
    ///
    ///   1. Locomotion - a procedural walk/run cycle plus idle breathing.
    ///   2. Stance     - the combat guard or relaxed idle, blended over the top.
    ///   3. Action     - a one-shot <see cref="PoseClip"/> (attack, dodge, death).
    ///
    /// A physical impulse spring is added last so hits visibly rock the body.
    /// The result is written as offsets from the rig's rest pose, so nothing
    /// accumulates drift.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class ProceduralCharacterAnimator : MonoBehaviour, ICharacterAnimator
    {
        [Header("Gait")]
        [SerializeField] private float _strideLength = 0.92f;
        [SerializeField] private float _runSpeedReference = 6.0f;
        [SerializeField] private float _legSwing = 42f;
        [SerializeField] private float _armSwing = 32f;

        [Header("Feel")]
        [SerializeField] private float _stanceBlendSharpness = 9f;
        [SerializeField] private float _leanIntoTurns = 0.035f;
        [SerializeField] private float _maxTurnLean = 11f;

        private CharacterRig _rig;
        private Quaternion[] _accum;
        private bool[] _touched;

        private float _phase;
        private float _gait;          // 0..1 blend from idle to full run
        private float _airborne;      // 0..1
        private float _turnRate;
        private float _speed;

        private CharacterStance _stance = CharacterStance.Relaxed;
        private float _combatWeight;
        private float _guardWeight;

        private PoseClip _action;
        private float _actionTime;
        private float _actionSpeed = 1f;
        private float _actionWeight;
        private float _actionTargetWeight;
        private float _actionFadeRate = 12f;

        private Vector3 _impulse;
        private Vector3 _impulseVelocity;

        private Transform _lookTarget;
        private float _lookWeight;
        private float _seed;

        public CharacterRig Rig => _rig;
        public bool IsPlayingAction => _action != null && _actionTargetWeight > 0f;
        public string CurrentActionId => _action?.Id;

        public float ActionNormalizedTime =>
            _action == null ? 1f : Mathf.Clamp01(_actionTime / _action.Duration);

        /// <summary>True once a non-looping action has run past its last frame.</summary>
        public bool ActionFinished => _action == null || _actionTime >= _action.Duration;

        public void Bind(CharacterRig rig)
        {
            _rig = rig;
            _accum = new Quaternion[(int)HumanBone.Count];
            _touched = new bool[(int)HumanBone.Count];
            _seed = Random.value * 100f;
        }

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<CharacterRig>();
            if (_accum == null) Bind(_rig);
        }

        // ---- ICharacterAnimator ------------------------------------------------

        public void SetLocomotion(Vector3 localVelocity, bool grounded, float verticalVelocity)
        {
            localVelocity.y = 0f;
            _speed = localVelocity.magnitude;
            _airborne = grounded ? 0f : 1f;
        }

        public void SetStance(CharacterStance stance) => _stance = stance;

        public void SetTurnRate(float degreesPerSecond) => _turnRate = degreesPerSecond;

        public void SetLookTarget(Transform target) => _lookTarget = target;

        public bool PlayAction(string clipId, float speedMultiplier = 1f, float fadeIn = 0.06f)
        {
            PoseClip clip = PoseLibrary.Get(clipId);
            if (clip == null)
            {
                Debug.LogWarning($"[ProceduralCharacterAnimator] Unknown clip '{clipId}'.", this);
                return false;
            }

            _action = clip;
            _actionTime = 0f;
            _actionSpeed = Mathf.Max(0.01f, speedMultiplier);
            _actionTargetWeight = 1f;
            _actionFadeRate = 1f / Mathf.Max(0.01f, fadeIn);
            // A fresh action starts partly blended in so fast attacks read
            // immediately instead of easing out of the idle pose.
            _actionWeight = Mathf.Max(_actionWeight, 0.35f);
            return true;
        }

        public void StopAction(float fadeOut = 0.12f)
        {
            _actionTargetWeight = 0f;
            _actionFadeRate = 1f / Mathf.Max(0.01f, fadeOut);
        }

        public void AddImpulse(Vector3 localDirection, float strength)
        {
            _impulseVelocity += localDirection.normalized * strength;
        }

        public void ResetPose()
        {
            _action = null;
            _actionWeight = 0f;
            _actionTargetWeight = 0f;
            _impulse = Vector3.zero;
            _impulseVelocity = Vector3.zero;
            _phase = 0f;
            _gait = 0f;
            _rig?.ResetToRest();
        }

        // ---- evaluation ---------------------------------------------------------

        private void LateUpdate()
        {
            if (_rig == null || _accum == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _rig.ResetToRest();
            for (int i = 0; i < _accum.Length; i++)
            {
                _accum[i] = Quaternion.identity;
                _touched[i] = false;
            }

            Vector3 hipsOffset = Vector3.zero;

            UpdateStanceWeights(dt);
            ApplyLocomotion(dt, ref hipsOffset);
            ApplyStance(ref hipsOffset);
            float pivot01 = UpdateAction(dt, ref hipsOffset);
            ApplyImpulse(dt);

            Commit(hipsOffset, pivot01);
            ApplyLookAt(dt);
        }

        private void UpdateStanceWeights(float dt)
        {
            bool combat = _stance == CharacterStance.Combat || _stance == CharacterStance.Guarding;
            bool guard = _stance == CharacterStance.Guarding;
            bool grounded = _stance != CharacterStance.Dead && _stance != CharacterStance.Downed;

            _combatWeight = MathUtil.Damp(_combatWeight, combat && grounded ? 1f : 0f, _stanceBlendSharpness, dt);
            _guardWeight = MathUtil.Damp(_guardWeight, guard && grounded ? 1f : 0f, _stanceBlendSharpness * 1.6f, dt);
        }

        private void ApplyLocomotion(float dt, ref Vector3 hipsOffset)
        {
            float targetGait = Mathf.Clamp01(_speed / Mathf.Max(0.1f, _runSpeedReference));
            _gait = MathUtil.Damp(_gait, targetGait, 12f, dt);

            float scale = _rig.Height / 1.8f;
            float stride = Mathf.Max(0.2f, _strideLength * scale);

            // While airborne the legs stop cycling; the jump clip takes over.
            float groundGait = _gait * (1f - _airborne);

            if (_speed > 0.05f)
                _phase += Mathf.PI * (_speed / stride) * dt;
            else
                _phase = Mathf.MoveTowardsAngle(_phase * Mathf.Rad2Deg, 0f, 360f * dt) * Mathf.Deg2Rad;

            float s = Mathf.Sin(_phase);
            float c = Mathf.Cos(_phase);

            // Legs: thighs swing in opposition, knees only bend backwards.
            float swing = _legSwing * groundGait;
            Add(HumanBone.ThighL, new Vector3(-s * swing, 0f, 0f));
            Add(HumanBone.ThighR, new Vector3(s * swing, 0f, 0f));
            Add(HumanBone.ShinL, new Vector3(Mathf.Max(0f, Mathf.Sin(_phase - 0.7f)) * swing * 1.35f, 0f, 0f));
            Add(HumanBone.ShinR, new Vector3(Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI - 0.7f)) * swing * 1.35f, 0f, 0f));
            Add(HumanBone.FootL, new Vector3(s * swing * 0.35f, 0f, 0f));
            Add(HumanBone.FootR, new Vector3(-s * swing * 0.35f, 0f, 0f));

            // Arms counter-swing; guarding suppresses it so the hands stay up.
            float armGait = groundGait * (1f - _guardWeight * 0.85f);
            float arm = _armSwing * armGait;
            Add(HumanBone.UpperArmL, new Vector3(s * arm, 0f, 0f));
            Add(HumanBone.UpperArmR, new Vector3(-s * arm, 0f, 0f));
            Add(HumanBone.LowerArmL, new Vector3(-Mathf.Abs(s) * arm * 0.45f - armGait * 10f, 0f, 0f));
            Add(HumanBone.LowerArmR, new Vector3(-Mathf.Abs(s) * arm * 0.45f - armGait * 10f, 0f, 0f));

            // Pelvis and torso counter-rotate, which is what makes a walk read as a walk.
            Add(HumanBone.Hips, new Vector3(0f, s * 5.5f * groundGait, c * 2.5f * groundGait));
            Add(HumanBone.Chest, new Vector3(groundGait * 5f, -s * 7f * groundGait, 0f));
            Add(HumanBone.Spine, new Vector3(groundGait * 4f, 0f, 0f));

            // Vertical bob, twice per stride.
            hipsOffset.y += -Mathf.Abs(c) * 0.05f * groundGait * scale;

            // Idle breathing keeps the character alive when standing still.
            float idle = (1f - _gait) * (1f - _airborne);
            if (idle > 0.01f)
            {
                float breath = Mathf.Sin(Time.time * 1.25f + _seed);
                Add(HumanBone.Chest, new Vector3(breath * 1.4f * idle, 0f, 0f));
                Add(HumanBone.Head, new Vector3(breath * -0.8f * idle, Mathf.Sin(Time.time * 0.4f + _seed) * 2.5f * idle, 0f));
                hipsOffset.y += breath * 0.006f * idle * scale;
            }

            // Bank into turns and lean into a sprint.
            float lean = Mathf.Clamp(-_turnRate * _leanIntoTurns, -_maxTurnLean, _maxTurnLean) * _gait;
            Add(HumanBone.Root, new Vector3(_gait * 5.5f * (1f - _airborne), 0f, lean));
        }

        private void ApplyStance(ref Vector3 hipsOffset)
        {
            float scale = _rig.Height / 1.8f;

            if (_combatWeight > 0.01f)
            {
                float w = _combatWeight;
                // Bladed, slightly crouched fighting stance.
                Blend(HumanBone.Hips, new Vector3(0f, 16f, 0f), w * 0.8f);
                Blend(HumanBone.Chest, new Vector3(4f, -12f, 0f), w);
                Blend(HumanBone.Head, new Vector3(3f, 10f, 0f), w * 0.7f);
                Blend(HumanBone.UpperArmL, new Vector3(-40f, 0f, 24f), w);
                Blend(HumanBone.LowerArmL, new Vector3(-78f, 0f, -10f), w);
                Blend(HumanBone.UpperArmR, new Vector3(-30f, 0f, -20f), w);
                Blend(HumanBone.LowerArmR, new Vector3(-66f, 0f, 8f), w);
                hipsOffset.y -= 0.045f * w * scale;
            }

            if (_guardWeight > 0.01f)
            {
                float w = _guardWeight;
                Blend(HumanBone.UpperArmL, new Vector3(-62f, 0f, 30f), w);
                Blend(HumanBone.LowerArmL, new Vector3(-104f, 0f, -18f), w);
                Blend(HumanBone.UpperArmR, new Vector3(-62f, 0f, -30f), w);
                Blend(HumanBone.LowerArmR, new Vector3(-104f, 0f, 18f), w);
                Blend(HumanBone.Chest, new Vector3(9f, 0f, 0f), w);
                Blend(HumanBone.Head, new Vector3(7f, 0f, 0f), w);
                hipsOffset.y -= 0.035f * w * scale;
            }
        }

        /// <summary>Advances and samples the one-shot layer. Returns the root pivot to use.</summary>
        private float UpdateAction(float dt, ref Vector3 hipsOffset)
        {
            _actionWeight = Mathf.MoveTowards(_actionWeight, _actionTargetWeight, _actionFadeRate * dt);

            if (_action == null)
            {
                if (_actionWeight <= 0.001f) return 0f;
                _actionWeight = 0f;
                return 0f;
            }

            _actionTime += dt * _actionSpeed;

            if (!_action.Loop && _actionTime >= _action.Duration && !_action.HoldLastFrame
                && _actionTargetWeight > 0f)
            {
                StopAction();
            }

            if (_actionWeight <= 0.001f)
            {
                if (_actionTargetWeight <= 0f) _action = null;
                return 0f;
            }

            Vector3 clipHips = Vector3.zero;
            _action.Sample(_actionTime, _actionWeight, _accum, _touched, ref clipHips);

            float scale = _rig.Height / 1.8f;
            hipsOffset += clipHips * scale;
            return _action.RootPivot01 * _actionWeight;
        }

        /// <summary>Critically damped spring that rocks the torso when hit.</summary>
        private void ApplyImpulse(float dt)
        {
            const float stiffness = 130f;
            const float damping = 15f;

            _impulseVelocity += -stiffness * _impulse * dt - damping * _impulseVelocity * dt;
            _impulse += _impulseVelocity * dt;

            if (_impulse.sqrMagnitude < 1e-6f && _impulseVelocity.sqrMagnitude < 1e-6f) return;

            // A push along +Z (into the character) tips the chest backwards.
            Add(HumanBone.Spine, new Vector3(-_impulse.z * 14f, _impulse.x * 9f, _impulse.x * 7f));
            Add(HumanBone.Chest, new Vector3(-_impulse.z * 10f, _impulse.x * 12f, _impulse.x * 8f));
            Add(HumanBone.Head, new Vector3(-_impulse.z * 16f, _impulse.x * 14f, _impulse.x * 10f));
        }

        private void Commit(Vector3 hipsOffset, float pivot01)
        {
            for (int i = 0; i < _accum.Length; i++)
            {
                if (!_touched[i]) continue;
                Transform bone = _rig.Get((HumanBone)i);
                if (bone == null) continue;
                bone.localRotation = _rig.RestRotation((HumanBone)i) * _accum[i];
            }

            Transform hips = _rig.Hips;
            if (hips != null && hipsOffset.sqrMagnitude > 1e-8f)
                hips.localPosition = _rig.RestPosition(HumanBone.Hips) + hipsOffset;

            Transform root = _rig.Root;
            if (root == null || !_touched[(int)HumanBone.Root]) return;

            // Rotating the Root about the character's origin would swing the body
            // around its ankles. Offsetting the root position makes the same
            // rotation appear to pivot around a point further up the body.
            Vector3 restRoot = _rig.RestPosition(HumanBone.Root);
            if (pivot01 > 0.001f)
            {
                Vector3 pivot = new Vector3(0f, _rig.Height * pivot01, 0f);
                root.localPosition = restRoot + pivot - _accum[(int)HumanBone.Root] * pivot;
            }
            else
            {
                root.localPosition = restRoot;
            }
        }

        private void ApplyLookAt(float dt)
        {
            bool want = _lookTarget != null && _stance != CharacterStance.Dead
                                            && _stance != CharacterStance.Downed;
            _lookWeight = MathUtil.Damp(_lookWeight, want ? 1f : 0f, 8f, dt);
            if (_lookWeight < 0.01f) return;

            Transform head = _rig.Head;
            if (head == null || _lookTarget == null) return;

            Vector3 to = _lookTarget.position + Vector3.up * 1.4f - head.position;
            if (to.sqrMagnitude < 0.01f) return;

            // Clamp to a believable neck range so the head never spins around.
            Quaternion desired = Quaternion.LookRotation(to.normalized, transform.up);
            Quaternion local = Quaternion.Inverse(head.parent.rotation) * desired;
            Vector3 e = local.eulerAngles;
            float yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, e.y), -62f, 62f);
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -28f, 34f);

            Quaternion clamped = Quaternion.Euler(pitch, yaw, 0f);
            head.localRotation = Quaternion.Slerp(head.localRotation, clamped, _lookWeight * 0.6f);
        }

        // ---- layer helpers -------------------------------------------------------

        private void Add(HumanBone bone, Vector3 euler)
        {
            int i = (int)bone;
            _accum[i] = _accum[i] * Quaternion.Euler(euler);
            _touched[i] = true;
        }

        private void Blend(HumanBone bone, Vector3 euler, float weight)
        {
            if (weight <= 0.001f) return;
            int i = (int)bone;
            _accum[i] = Quaternion.Slerp(_accum[i], Quaternion.Euler(euler), Mathf.Clamp01(weight));
            _touched[i] = true;
        }
    }
}
