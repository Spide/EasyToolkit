using UnityEngine;

namespace Easy.Character.Animation
{
    [DisallowMultipleComponent]
    public sealed class CharacterAimController : MonoBehaviour
    {
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterAimProfile profile;

        private CharacterAimSettings settings = CharacterAimSettings.Default;
        private CharacterAimIkDriver ikDriver;
        private Animator configuredAnimator;
        private Transform configuredRoot;
        private CharacterAimProfile configuredProfile;
        private CharacterAimState state;
        private float poseYaw;
        private float posePitch;
        private float poseWeight;
        private float rootTurnSpeedDegreesPerSecond;
        private bool rootTurning;

        public CharacterAimProfile Profile => profile;
        public CharacterAimState State => state;
        public bool IsSupported => animator != null && animator.isHuman;
        public CharacterAimRigMode EffectiveRigMode => ikDriver != null
            ? ikDriver.EffectiveRigMode
            : CharacterAimRigMode.Disabled;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Reset()
        {
            characterRoot = transform;
            animator = GetComponent<Animator>();
            animator = animator != null ? animator : GetComponentInChildren<Animator>();
        }

        private void OnDisable()
        {
            Clear(immediate: true);
        }

        public void Configure(
            Animator targetAnimator,
            CharacterAimProfile aimProfile,
            Transform targetRoot = null)
        {
            animator = targetAnimator;
            profile = aimProfile;
            characterRoot = targetRoot != null ? targetRoot : transform;
            settings = profile != null ? profile.Settings : CharacterAimSettings.Default;
            ikDriver = null;
            configuredAnimator = null;
            configuredRoot = null;
            configuredProfile = null;
            EnsureReferences();
        }

        public CharacterAimState Apply(in CharacterAimCommand command, float deltaTime)
        {
            EnsureReferences();
            bool supported = IsSupported;
            bool targetValid = IsFinite(command.TargetWorldPosition);
            bool active = supported
                && command.IsAiming
                && !command.Suppressed
                && targetValid;

            if (command.Suppressed)
            {
                poseWeight = 0f;
                poseYaw = 0f;
                posePitch = 0f;
                rootTurnSpeedDegreesPerSecond = 0f;
                rootTurning = false;
            }
            else
            {
                poseWeight = AdvanceWeight(
                    poseWeight,
                    active ? 1f : 0f,
                    settings.BlendSeconds,
                    deltaTime);
            }

            Vector3 target = targetValid ? command.TargetWorldPosition : state.TargetWorldPosition;
            if (active)
            {
                Vector3 origin = ikDriver != null
                    ? ikDriver.AimOrigin
                    : characterRoot.position + characterRoot.up * 1.25f;
                CalculateLocalAngles(
                    characterRoot.rotation,
                    origin,
                    target,
                    out float targetYaw,
                    out float targetPitch);

                if (command.AllowRootTurn)
                {
                    float rootDelta = AdvanceRootTurn(
                        targetYaw,
                        rootTurning,
                        rootTurnSpeedDegreesPerSecond,
                        settings,
                        deltaTime,
                        out rootTurnSpeedDegreesPerSecond,
                        out rootTurning);
                    if (!Mathf.Approximately(rootDelta, 0f))
                    {
                        characterRoot.rotation = Quaternion.AngleAxis(
                            rootDelta,
                            characterRoot.up) * characterRoot.rotation;
                        CalculateLocalAngles(
                            characterRoot.rotation,
                            origin,
                            target,
                            out targetYaw,
                            out targetPitch);
                    }
                }
                else
                {
                    rootTurnSpeedDegreesPerSecond = 0f;
                    rootTurning = false;
                }

                targetYaw = Mathf.Clamp(
                    targetYaw,
                    -settings.MaximumPoseYaw,
                    settings.MaximumPoseYaw);
                targetPitch = Mathf.Clamp(
                    targetPitch,
                    -settings.MaximumDownPitch,
                    settings.MaximumUpPitch);
                float damp = CalculateDampFactor(settings.PoseDampSeconds, deltaTime);
                poseYaw = Mathf.LerpAngle(poseYaw, targetYaw, damp);
                posePitch = Mathf.LerpAngle(posePitch, targetPitch, damp);
            }
            else
            {
                rootTurnSpeedDegreesPerSecond = 0f;
                rootTurning = false;
            }

            state = new CharacterAimState(
                supported,
                active,
                target,
                poseYaw,
                posePitch,
                poseWeight,
                rootTurning,
                command.BodyWeightScale,
                command.HandsWeightScale,
                rootTurnSpeedDegreesPerSecond);
            ikDriver?.SetAimState(state);
            return state;
        }

        public void BindWeapon(in CharacterAimWeaponRig weaponRig)
        {
            EnsureReferences();
            ikDriver?.BindWeapon(weaponRig);
        }

        public void ClearWeapon()
        {
            ikDriver?.ClearWeapon();
        }

        public void Clear(bool immediate)
        {
            rootTurning = false;
            rootTurnSpeedDegreesPerSecond = 0f;
            if (immediate)
            {
                poseWeight = 0f;
                poseYaw = 0f;
                posePitch = 0f;
            }

            state = new CharacterAimState(
                IsSupported,
                active: false,
                state.TargetWorldPosition,
                poseYaw,
                posePitch,
                poseWeight,
                rootTurning: false,
                bodyWeightScale: 1f,
                handsWeightScale: 1f,
                rootTurnSpeedDegreesPerSecond: 0f);
            ikDriver?.SetAimState(state);
        }

        public static void CalculateLocalAngles(
            Quaternion rootRotation,
            Vector3 origin,
            Vector3 target,
            out float yaw,
            out float pitch)
        {
            Vector3 direction = target - origin;
            float sqrMagnitude = direction.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.000001f)
            {
                yaw = 0f;
                pitch = 0f;
                return;
            }

            Vector3 local = Quaternion.Inverse(rootRotation) * direction;
            float planar = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(local.y, Mathf.Max(0.000001f, planar)) * Mathf.Rad2Deg;
        }

        public static float CalculateRootTurnDelta(
            float yaw,
            bool wasTurning,
            CharacterAimSettings aimSettings,
            float maximumStep,
            out bool remainsTurning)
        {
            CharacterAimSettings clamped = aimSettings.Clamp();
            float absoluteYaw = Mathf.Abs(yaw);
            bool shouldTurn = wasTurning
                ? absoluteYaw > clamped.RootTurnStopYaw
                : absoluteYaw > clamped.TorsoYawRange;
            if (!shouldTurn || maximumStep <= 0f)
            {
                remainsTurning = shouldTurn;
                return 0f;
            }

            float desiredResidual = Mathf.Sign(yaw) * clamped.RootTurnStopYaw;
            float delta = Mathf.Clamp(
                yaw - desiredResidual,
                -maximumStep,
                maximumStep);
            remainsTurning = Mathf.Abs(yaw - delta) > clamped.RootTurnStopYaw + 0.001f;
            return delta;
        }

        public static float AdvanceRootTurn(
            float yaw,
            bool wasTurning,
            float currentSpeedDegreesPerSecond,
            CharacterAimSettings aimSettings,
            float deltaTime,
            out float nextSpeedDegreesPerSecond,
            out bool remainsTurning)
        {
            CharacterAimSettings clamped = aimSettings.Clamp();
            float absoluteYaw = Mathf.Abs(yaw);
            bool shouldTurn = wasTurning
                ? absoluteYaw > clamped.RootTurnStopYaw
                : absoluteYaw > clamped.TorsoYawRange;
            if (!shouldTurn || deltaTime <= 0f || !float.IsFinite(yaw))
            {
                nextSpeedDegreesPerSecond = shouldTurn && deltaTime <= 0f
                    ? currentSpeedDegreesPerSecond
                    : 0f;
                remainsTurning = shouldTurn;
                return 0f;
            }

            float direction = Mathf.Sign(yaw);
            float remainingAngle = Mathf.Max(0f, absoluteYaw - clamped.RootTurnStopYaw);
            float currentSpeed = float.IsFinite(currentSpeedDegreesPerSecond)
                ? currentSpeedDegreesPerSecond
                : 0f;
            bool reversing = currentSpeed * direction < 0f;
            float desiredSpeed;
            float changeRate;
            if (reversing)
            {
                desiredSpeed = 0f;
                changeRate = clamped.RootTurnDecelerationDegreesPerSecondSquared;
            }
            else
            {
                float brakingSpeed = Mathf.Sqrt(
                    2f
                    * clamped.RootTurnDecelerationDegreesPerSecondSquared
                    * remainingAngle);
                desiredSpeed = direction * Mathf.Min(
                    clamped.RootTurnDegreesPerSecond,
                    brakingSpeed);
                changeRate = Mathf.Abs(desiredSpeed) < Mathf.Abs(currentSpeed)
                    ? clamped.RootTurnDecelerationDegreesPerSecondSquared
                    : clamped.RootTurnAccelerationDegreesPerSecondSquared;
            }

            nextSpeedDegreesPerSecond = Mathf.MoveTowards(
                currentSpeed,
                desiredSpeed,
                changeRate * deltaTime);
            float delta = nextSpeedDegreesPerSecond * deltaTime;
            bool movingTowardTarget = delta * direction >= 0f;
            if (movingTowardTarget && Mathf.Abs(delta) >= remainingAngle)
            {
                delta = direction * remainingAngle;
                nextSpeedDegreesPerSecond = 0f;
                remainsTurning = false;
                return delta;
            }

            remainsTurning = true;
            return delta;
        }

        public static float AdvanceWeight(
            float current,
            float target,
            float duration,
            float deltaTime)
        {
            if (duration <= 0f || deltaTime <= 0f)
                return deltaTime <= 0f && duration > 0f ? current : target;

            return Mathf.MoveTowards(current, target, deltaTime / duration);
        }

        private static float CalculateDampFactor(float duration, float deltaTime)
        {
            if (duration <= 0f || deltaTime <= 0f)
                return duration <= 0f ? 1f : 0f;

            return 1f - Mathf.Exp(-deltaTime / duration);
        }

        private void EnsureReferences()
        {
            characterRoot = characterRoot != null ? characterRoot : transform;
            animator = animator != null ? animator : GetComponent<Animator>();
            animator = animator != null ? animator : GetComponentInChildren<Animator>();
            settings = profile != null ? profile.Settings : settings.Clamp();
            if (animator == null)
                return;

            ikDriver = ikDriver != null ? ikDriver : animator.GetComponent<CharacterAimIkDriver>();
            ikDriver = ikDriver != null
                ? ikDriver
                : animator.gameObject.AddComponent<CharacterAimIkDriver>();
            if (configuredAnimator == animator
                && configuredRoot == characterRoot
                && configuredProfile == profile)
            {
                return;
            }

            ikDriver.Configure(animator, characterRoot, settings);
            configuredAnimator = animator;
            configuredRoot = characterRoot;
            configuredProfile = profile;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x)
            && float.IsFinite(value.y)
            && float.IsFinite(value.z);
    }
}
