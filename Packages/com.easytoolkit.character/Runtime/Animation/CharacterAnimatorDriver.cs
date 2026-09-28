using UnityEngine;

namespace Easy.Character.Animation
{
    [DisallowMultipleComponent]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterAnimationProfile profile;
        [SerializeField] private global::Easy.Character.CharacterMotor motor;
        [SerializeField] private bool readMotorAutomatically = true;
        [SerializeField] private bool manageLocomotionLayers = true;
        [SerializeField] private MonoBehaviour[] extensionSourceBehaviours;

        private int movementSpeed;
        private int movementSpeedMultiplier;
        private int movementDirectionX;
        private int movementDirectionY;
        private int aiming;
        private int crouched;
        private int sprinting;
        private int jumping;
        private int falling;
        private int movementLayer = -1;
        private int aimingLayer = -1;
        private int crouchLayer = -1;
        private int sprintLayer = -1;
        private int jumpLayer = -1;
        private int fallLayer = -1;
        private CharacterAnimationSettings settings = CharacterAnimationSettings.Default;
        private float[] currentLayerWeights;
        private float[] targetLayerWeights;
        private float[] layerDurations;
        private bool[] controlledLayers;
        private bool configured;
        private bool suppressed;
        private ICharacterAnimationExtensionStateSource[] extensionSources;

        public CharacterAnimationProfile Profile => profile;

        private void Awake()
        {
            EnsureConfigured();
        }

        private void LateUpdate()
        {
            if (!readMotorAutomatically || motor == null)
                return;

            global::Easy.Character.CharacterMotorState state = motor.State;
            Apply(
                new CharacterAnimationFrame(
                    state.Velocity,
                    transform,
                    false,
                    state.IsCrouched,
                    state.IsSprinting,
                    state.IsJumping,
                    state.IsFalling),
                Time.deltaTime);
        }

        public void Configure(
            Animator targetAnimator,
            CharacterAnimationProfile animationProfile,
            bool automatic,
            bool manageLayers)
        {
            animator = targetAnimator;
            profile = animationProfile;
            settings = profile != null ? profile.Settings : CharacterAnimationSettings.Default;
            readMotorAutomatically = automatic;
            manageLocomotionLayers = manageLayers;
            motor = motor != null ? motor : GetComponent<global::Easy.Character.CharacterMotor>();
            configured = false;
            EnsureConfigured();
        }

        public void ConfigureRuntime(
            Animator targetAnimator,
            CharacterAnimationSettings runtimeSettings,
            bool automatic,
            bool manageLayers)
        {
            animator = targetAnimator;
            settings = runtimeSettings;
            readMotorAutomatically = automatic;
            manageLocomotionLayers = manageLayers;
            motor = motor != null ? motor : GetComponent<global::Easy.Character.CharacterMotor>();
            configured = false;
            EnsureConfigured();
        }

        public void SetSuppressed(bool value, bool immediate)
        {
            suppressed = value;
            if (!manageLocomotionLayers || !immediate)
                return;

            SetLayerImmediate(aimingLayer, false);
            SetLayerImmediate(crouchLayer, false);
            SetLayerImmediate(sprintLayer, false);
            SetLayerImmediate(jumpLayer, false);
            SetLayerImmediate(fallLayer, false);
        }

        public void SetExtensionSources(MonoBehaviour[] sources)
        {
            extensionSourceBehaviours = sources;
            CacheExtensionSources();
        }

        public void Apply(in CharacterAnimationFrame frame, float deltaTime)
        {
            EnsureConfigured();
            if (animator == null)
                return;

            Vector3 horizontalVelocity = frame.WorldVelocity;
            horizontalVelocity.y = 0f;
            float speed = horizontalVelocity.magnitude;
            Transform reference = frame.FacingReference != null
                ? frame.FacingReference
                : transform;
            Vector3 localDirection = speed > 0.0001f
                ? reference.InverseTransformDirection(horizontalVelocity / speed)
                : Vector3.zero;
            bool hasExtensionState = TryGetHighestPriorityExtensionState(
                out CharacterAnimationExtensionState extensionState);
            float playback = frame.ForceNormalPlayback
                || (hasExtensionState && extensionState.ForceNormalPlayback)
                ? 1f
                : CalculatePlaybackMultiplier(
                    speed,
                    settings.IdleSpeedThreshold,
                    settings.MinimumMovingMultiplier,
                    settings.NormalReferenceSpeed,
                    settings.SprintReferenceSpeed,
                    settings.MaximumSprintMultiplier);
            float damp = settings.ParameterDampSeconds;
            if (damp > 0f && deltaTime > 0f)
            {
                animator.SetFloat(movementSpeed, speed, damp, deltaTime);
                animator.SetFloat(movementSpeedMultiplier, playback, damp, deltaTime);
                animator.SetFloat(movementDirectionX, localDirection.x, damp, deltaTime);
                animator.SetFloat(movementDirectionY, localDirection.z, damp, deltaTime);
            }
            else
            {
                animator.SetFloat(movementSpeed, speed);
                animator.SetFloat(movementSpeedMultiplier, playback);
                animator.SetFloat(movementDirectionX, localDirection.x);
                animator.SetFloat(movementDirectionY, localDirection.z);
            }

            animator.SetBool(aiming, frame.IsAiming);
            animator.SetBool(crouched, frame.IsCrouched);
            animator.SetBool(sprinting, frame.IsSprinting);
            animator.SetBool(jumping, frame.IsJumping);
            animator.SetBool(falling, frame.IsFalling);

            if (!manageLocomotionLayers)
                return;

            bool allowLayers = !suppressed
                && (!hasExtensionState || !extensionState.SuppressLocomotionLayers);
            bool useFall = allowLayers && frame.IsFalling;
            bool useJump = allowLayers && !useFall && frame.IsJumping;
            float sprintWeight = allowLayers
                && !useFall
                && !useJump
                && !frame.IsAiming
                && !frame.IsCrouched
                ? CalculateSprintLayerWeight(
                    speed,
                    frame.IsSprinting,
                    settings.NormalReferenceSpeed,
                    settings.SprintReferenceSpeed)
                : 0f;
            bool useCrouch = allowLayers && !useFall && !useJump && frame.IsCrouched;
            bool useAiming = allowLayers && !useFall && !useJump && frame.IsAiming;
            SetLayerTarget(aimingLayer, useAiming, settings.LocomotionLayerBlendSeconds);
            SetLayerTarget(crouchLayer, useCrouch, settings.LocomotionLayerBlendSeconds);
            SetLayerTarget(sprintLayer, sprintWeight, settings.LocomotionLayerBlendSeconds);
            SetLayerTarget(jumpLayer, useJump, settings.AirborneLayerBlendSeconds);
            SetLayerTarget(fallLayer, useFall, settings.AirborneLayerBlendSeconds);
            AdvanceLayerWeights(deltaTime);
        }

        public static float CalculatePlaybackMultiplier(
            float speed,
            float idleThreshold,
            float minimumMovingMultiplier,
            float normalReferenceSpeed,
            float sprintReferenceSpeed,
            float maximumSprintMultiplier)
        {
            speed = Mathf.Max(0f, speed);
            idleThreshold = Mathf.Max(0f, idleThreshold);
            if (speed <= idleThreshold)
                return 1f;

            minimumMovingMultiplier = Mathf.Max(0.1f, minimumMovingMultiplier);
            normalReferenceSpeed = Mathf.Max(idleThreshold + 0.0001f, normalReferenceSpeed);
            sprintReferenceSpeed = Mathf.Max(normalReferenceSpeed + 0.0001f, sprintReferenceSpeed);
            maximumSprintMultiplier = Mathf.Max(1f, maximumSprintMultiplier);
            if (speed <= normalReferenceSpeed)
            {
                return Mathf.Lerp(
                    minimumMovingMultiplier,
                    1f,
                    Mathf.InverseLerp(idleThreshold, normalReferenceSpeed, speed));
            }

            return Mathf.Lerp(
                1f,
                maximumSprintMultiplier,
                Mathf.InverseLerp(normalReferenceSpeed, sprintReferenceSpeed, speed));
        }

        public static float CalculateSprintLayerWeight(
            float horizontalSpeed,
            bool sprinting,
            float normalReferenceSpeed,
            float sprintReferenceSpeed)
        {
            if (!sprinting)
                return 0f;

            normalReferenceSpeed = Mathf.Max(0f, normalReferenceSpeed);
            sprintReferenceSpeed = Mathf.Max(
                normalReferenceSpeed + 0.0001f,
                sprintReferenceSpeed);
            return Mathf.InverseLerp(
                normalReferenceSpeed,
                sprintReferenceSpeed,
                Mathf.Max(0f, horizontalSpeed));
        }

        private void EnsureConfigured()
        {
            if (configured)
                return;
            animator = animator != null ? animator : GetComponent<Animator>();
            animator = animator != null ? animator : GetComponentInChildren<Animator>();
            motor = motor != null ? motor : GetComponent<global::Easy.Character.CharacterMotor>();
            if (animator == null)
                return;

            if (profile != null)
                settings = profile.Settings;
            movementSpeed = Animator.StringToHash(settings.MovementSpeedParameter);
            movementSpeedMultiplier = Animator.StringToHash(settings.MovementSpeedMultiplierParameter);
            movementDirectionX = Animator.StringToHash(settings.MovementDirectionXParameter);
            movementDirectionY = Animator.StringToHash(settings.MovementDirectionYParameter);
            aiming = Animator.StringToHash(settings.AimingParameter);
            crouched = Animator.StringToHash(settings.CrouchedParameter);
            sprinting = Animator.StringToHash(settings.SprintingParameter);
            jumping = Animator.StringToHash(settings.JumpingParameter);
            falling = Animator.StringToHash(settings.FallingParameter);
            movementLayer = animator.GetLayerIndex(settings.MovementLayer);
            aimingLayer = animator.GetLayerIndex(settings.AimingLayer);
            crouchLayer = animator.GetLayerIndex(settings.CrouchLayer);
            sprintLayer = animator.GetLayerIndex(settings.SprintLayer);
            jumpLayer = animator.GetLayerIndex(settings.JumpLayer);
            fallLayer = animator.GetLayerIndex(settings.FallLayer);
            int layerCount = animator.layerCount;
            currentLayerWeights = new float[layerCount];
            targetLayerWeights = new float[layerCount];
            layerDurations = new float[layerCount];
            controlledLayers = new bool[layerCount];
            RegisterControlledLayer(movementLayer, 1f);
            RegisterControlledLayer(aimingLayer, 0f);
            RegisterControlledLayer(crouchLayer, 0f);
            RegisterControlledLayer(sprintLayer, 0f);
            RegisterControlledLayer(jumpLayer, 0f);
            RegisterControlledLayer(fallLayer, 0f);
            if (movementLayer >= 0)
                animator.SetLayerWeight(movementLayer, 1f);
            CacheExtensionSources();
            configured = true;
        }

        public static float AdvanceLayerWeight(
            float current,
            float target,
            float duration,
            float deltaTime)
        {
            return duration <= 0f
                ? target
                : Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / duration);
        }

        public static bool TrySelectHighestPriorityExtensionState(
            ICharacterAnimationExtensionStateSource[] sources,
            out CharacterAnimationExtensionState state)
        {
            state = default;
            if (sources == null)
                return false;

            bool found = false;
            int selectedPriority = int.MinValue;
            for (int i = 0; i < sources.Length; i++)
            {
                ICharacterAnimationExtensionStateSource source = sources[i];
                if (source == null
                    || source.Priority < selectedPriority
                    || !source.TryGetAnimationExtensionState(out CharacterAnimationExtensionState candidate))
                {
                    continue;
                }

                found = true;
                selectedPriority = source.Priority;
                state = candidate;
            }
            return found;
        }

        private void SetLayerTarget(int layer, bool active, float duration)
        {
            SetLayerTarget(layer, active ? 1f : 0f, duration);
        }

        private void SetLayerTarget(int layer, float weight, float duration)
        {
            if (layer < 0 || targetLayerWeights == null || layer >= targetLayerWeights.Length)
                return;
            targetLayerWeights[layer] = Mathf.Clamp01(weight);
            layerDurations[layer] = Mathf.Max(0f, duration);
            controlledLayers[layer] = true;
        }

        private void SetLayerImmediate(int layer, bool active)
        {
            if (layer < 0 || animator == null)
                return;
            float weight = active ? 1f : 0f;
            animator.SetLayerWeight(layer, weight);
            if (currentLayerWeights == null || layer >= currentLayerWeights.Length)
                return;
            currentLayerWeights[layer] = weight;
            targetLayerWeights[layer] = weight;
        }

        private void AdvanceLayerWeights(float deltaTime)
        {
            if (animator == null || currentLayerWeights == null)
                return;
            for (int i = 0; i < currentLayerWeights.Length; i++)
            {
                if (!controlledLayers[i])
                    continue;
                float target = targetLayerWeights[i];
                float duration = layerDurations[i];
                float next = AdvanceLayerWeight(
                    currentLayerWeights[i],
                    target,
                    duration,
                    deltaTime);
                if (Mathf.Approximately(next, currentLayerWeights[i]))
                    continue;
                currentLayerWeights[i] = next;
                animator.SetLayerWeight(i, next);
            }
        }

        private void RegisterControlledLayer(int layer, float weight)
        {
            if (layer < 0 || layer >= currentLayerWeights.Length)
                return;
            controlledLayers[layer] = true;
            currentLayerWeights[layer] = weight;
            targetLayerWeights[layer] = weight;
        }

        private void CacheExtensionSources()
        {
            int sourceCount = extensionSourceBehaviours != null
                ? extensionSourceBehaviours.Length
                : 0;
            extensionSources = sourceCount > 0
                ? new ICharacterAnimationExtensionStateSource[sourceCount]
                : null;
            for (int i = 0; i < sourceCount; i++)
            {
                extensionSources[i] = extensionSourceBehaviours[i]
                    as ICharacterAnimationExtensionStateSource;
            }
        }

        private bool TryGetHighestPriorityExtensionState(
            out CharacterAnimationExtensionState state)
        {
            return TrySelectHighestPriorityExtensionState(extensionSources, out state);
        }
    }
}
