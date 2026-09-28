using UnityEngine;

namespace Easy.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private UnityEngine.CharacterController characterController;
        [SerializeField] private CharacterMotorProfile profile;
        [SerializeField] private MonoBehaviour commandSourceBehaviour;
        [SerializeField] private CharacterUpdateMode updateMode = CharacterUpdateMode.Automatic;
        [SerializeField] private bool useAcceleration = true;

        private ICharacterMotorCommandSource commandSource;
        private CharacterMotorSettings runtimeSettings = CharacterMotorSettings.Default;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float explicitFacingSpeedDegreesPerSecond;
        private float jumpGravity;
        private bool jumpRequested;
        private bool suspended;
        private CharacterMotorState state;

        public CharacterMotorState State => state;
        public bool IsGrounded => state.IsGrounded;
        public bool IsSuspended => suspended;
        public CharacterUpdateMode UpdateMode => updateMode;
        public UnityEngine.CharacterController Controller => characterController;

        private void Awake()
        {
            ResolveReferences();
            runtimeSettings = profile != null ? profile.Settings : CharacterMotorSettings.Default;
            RefreshState(Vector3.zero, characterController != null && characterController.isGrounded, false, false);
        }

        private void Update()
        {
            if (updateMode != CharacterUpdateMode.Automatic || suspended)
                return;

            CharacterMotorCommand command = default;
            commandSource?.TryGetMotorCommand(out command);
            Simulate(command, Time.deltaTime);
        }

        public void Configure(
            CharacterMotorProfile motorProfile,
            CharacterUpdateMode mode = CharacterUpdateMode.Automatic,
            MonoBehaviour source = null)
        {
            profile = motorProfile;
            runtimeSettings = profile != null ? profile.Settings : CharacterMotorSettings.Default;
            explicitFacingSpeedDegreesPerSecond = 0f;
            updateMode = mode;
            commandSourceBehaviour = source;
            commandSource = source as ICharacterMotorCommandSource;
            ResolveReferences();
        }

        public void ConfigureRuntime(
            CharacterMotorSettings settings,
            CharacterUpdateMode mode,
            bool accelerate)
        {
            runtimeSettings = settings;
            explicitFacingSpeedDegreesPerSecond = 0f;
            updateMode = mode;
            useAcceleration = accelerate;
            ResolveReferences();
        }

        public void SetCommandSource(MonoBehaviour source)
        {
            commandSourceBehaviour = source;
            commandSource = source as ICharacterMotorCommandSource;
        }

        public void RequestJump()
        {
            if (!suspended)
                jumpRequested = true;
        }

        public CharacterMotorState Simulate(
            in CharacterMotorCommand command,
            float deltaTime)
        {
            ResolveReferences();
            if (suspended
                || characterController == null
                || !characterController.enabled
                || deltaTime <= 0f)
            {
                RefreshState(Vector3.zero, IsControllerGrounded(), command.SprintHeld, command.Crouched);
                return state;
            }

            Vector3 direction = FlattenAndNormalize(command.WorldDirection);
            float speed = ResolveSpeed(command);
            Vector3 desiredVelocity = direction * (speed * command.InputAmount);
            float changeRate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude
                ? runtimeSettings.Acceleration
                : runtimeSettings.Deceleration;
            planarVelocity = !useAcceleration || changeRate <= 0f
                ? desiredVelocity
                : MoveTowardsVector(planarVelocity, desiredVelocity, changeRate * deltaTime);

            bool groundedBeforeMove = characterController.isGrounded;
            if (groundedBeforeMove && verticalVelocity < 0f)
                verticalVelocity = runtimeSettings.GroundedVerticalSpeed;

            bool consumeJump = command.JumpRequested || jumpRequested;
            jumpRequested = false;
            if (consumeJump && groundedBeforeMove)
            {
                jumpGravity = CalculateJumpGravity(
                    runtimeSettings.JumpHeight,
                    runtimeSettings.JumpTimeToApex);
                verticalVelocity = CalculateJumpSpeed(
                    runtimeSettings.JumpHeight,
                    jumpGravity);
                groundedBeforeMove = false;
            }
            else
            {
                float gravity = verticalVelocity > 0f && jumpGravity < 0f
                    ? jumpGravity
                    : Physics.gravity.y * runtimeSettings.GravityMultiplier;
                verticalVelocity += gravity * deltaTime;
            }

            Vector3 facingDirection = CalculateFacingDirection(command, direction);
            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                if (command.FacingMode == CharacterMotorFacingMode.ExplicitDirection
                    && runtimeSettings.ExplicitFacingAccelerationDegreesPerSecondSquared > 0f
                    && runtimeSettings.ExplicitFacingDecelerationDegreesPerSecondSquared > 0f)
                {
                    float signedAngle = Vector3.SignedAngle(
                        transform.forward,
                        facingDirection,
                        Vector3.up);
                    float rotationDelta = AdvanceExplicitFacingRotation(
                        signedAngle,
                        explicitFacingSpeedDegreesPerSecond,
                        runtimeSettings.RotationSpeedDegrees,
                        runtimeSettings.ExplicitFacingAccelerationDegreesPerSecondSquared,
                        runtimeSettings.ExplicitFacingDecelerationDegreesPerSecondSquared,
                        deltaTime,
                        out explicitFacingSpeedDegreesPerSecond);
                    if (!Mathf.Approximately(rotationDelta, 0f))
                    {
                        transform.rotation = Quaternion.AngleAxis(
                            rotationDelta,
                            Vector3.up) * transform.rotation;
                    }
                }
                else
                {
                    explicitFacingSpeedDegreesPerSecond = 0f;
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        runtimeSettings.RotationSpeedDegrees * deltaTime);
                }
            }
            else
            {
                explicitFacingSpeedDegreesPerSecond = 0f;
            }

            Vector3 previousPosition = transform.position;
            CollisionFlags collision = characterController.Move(
                (planarVelocity + Vector3.up * verticalVelocity) * deltaTime);
            bool grounded = (collision & CollisionFlags.Below) != 0
                || characterController.isGrounded;
            if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = runtimeSettings.GroundedVerticalSpeed;
                jumpGravity = 0f;
            }

            Vector3 actualVelocity = (transform.position - previousPosition) / deltaTime;
            RefreshState(actualVelocity, grounded, command.SprintHeld, command.Crouched);
            return state;
        }

        public void Suspend(bool disableController = false)
        {
            suspended = true;
            CancelMotion();
            if (disableController && characterController != null)
                characterController.enabled = false;
            RefreshState(Vector3.zero, false, false, false);
        }

        public void Resume(bool enableController = true)
        {
            ResolveReferences();
            if (enableController && characterController != null)
                characterController.enabled = true;
            suspended = false;
            jumpRequested = false;
            verticalVelocity = runtimeSettings.GroundedVerticalSpeed;
            RefreshState(Vector3.zero, IsControllerGrounded(), false, false);
        }

        public void CancelMotion()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            explicitFacingSpeedDegreesPerSecond = 0f;
            jumpGravity = 0f;
            jumpRequested = false;
        }

        public static float CalculateJumpGravity(float height, float timeToApex)
        {
            float positiveHeight = Mathf.Max(0f, height);
            float positiveTime = Mathf.Max(0f, timeToApex);
            return positiveHeight <= 0f || positiveTime <= 0.0001f
                ? 0f
                : -2f * positiveHeight / (positiveTime * positiveTime);
        }

        public static float CalculateJumpSpeed(float height, float gravity)
        {
            float positiveHeight = Mathf.Max(0f, height);
            float gravityMagnitude = Mathf.Abs(gravity);
            return positiveHeight <= 0f || gravityMagnitude <= 0.0001f
                ? 0f
                : Mathf.Sqrt(2f * positiveHeight * gravityMagnitude);
        }

        public static Vector3 AdvancePlanarVelocity(
            Vector3 current,
            Vector3 target,
            float changeRate,
            float deltaTime)
        {
            return changeRate <= 0f
                ? target
                : MoveTowardsVector(current, target, changeRate * Mathf.Max(0f, deltaTime));
        }

        public static float CalculateMovementSpeed(
            in CharacterMotorSettings settings,
            in CharacterMotorCommand command)
        {
            if (command.SpeedOverride > 0f)
                return command.SpeedOverride;

            float postureMultiplier = command.Crouched
                ? settings.CrouchMultiplier
                : command.SprintHeld ? settings.SprintMultiplier : 1f;
            return settings.WalkSpeed * postureMultiplier;
        }

        public static Vector3 CalculateFacingDirection(
            in CharacterMotorCommand command,
            Vector3 movementDirection)
        {
            return command.FacingMode switch
            {
                CharacterMotorFacingMode.MovementDirection =>
                    FlattenAndNormalize(movementDirection),
                CharacterMotorFacingMode.ExplicitDirection =>
                    FlattenAndNormalize(command.WorldFacingDirection),
                _ => Vector3.zero,
            };
        }

        public static float AdvanceExplicitFacingRotation(
            float signedAngleDegrees,
            float currentSpeedDegreesPerSecond,
            float maximumSpeedDegreesPerSecond,
            float accelerationDegreesPerSecondSquared,
            float decelerationDegreesPerSecondSquared,
            float deltaTime,
            out float nextSpeedDegreesPerSecond)
        {
            if (!float.IsFinite(signedAngleDegrees)
                || !float.IsFinite(currentSpeedDegreesPerSecond)
                || deltaTime <= 0f)
            {
                nextSpeedDegreesPerSecond = float.IsFinite(currentSpeedDegreesPerSecond)
                    ? currentSpeedDegreesPerSecond
                    : 0f;
                return 0f;
            }

            float remainingAngle = Mathf.Abs(signedAngleDegrees);
            if (remainingAngle <= 0.001f || maximumSpeedDegreesPerSecond <= 0f)
            {
                nextSpeedDegreesPerSecond = 0f;
                return 0f;
            }

            float acceleration = Mathf.Max(0f, accelerationDegreesPerSecondSquared);
            float deceleration = Mathf.Max(0f, decelerationDegreesPerSecondSquared);
            if (acceleration <= 0f || deceleration <= 0f)
            {
                nextSpeedDegreesPerSecond = 0f;
                return Mathf.Clamp(
                    signedAngleDegrees,
                    -maximumSpeedDegreesPerSecond * deltaTime,
                    maximumSpeedDegreesPerSecond * deltaTime);
            }

            float direction = Mathf.Sign(signedAngleDegrees);
            bool reversing = currentSpeedDegreesPerSecond * direction < 0f;
            float desiredSpeed;
            float changeRate;
            if (reversing)
            {
                desiredSpeed = 0f;
                changeRate = deceleration;
            }
            else
            {
                float brakingSpeed = Mathf.Sqrt(2f * deceleration * remainingAngle);
                desiredSpeed = direction * Mathf.Min(
                    maximumSpeedDegreesPerSecond,
                    brakingSpeed);
                changeRate = Mathf.Abs(desiredSpeed) < Mathf.Abs(currentSpeedDegreesPerSecond)
                    ? deceleration
                    : acceleration;
            }

            nextSpeedDegreesPerSecond = Mathf.MoveTowards(
                currentSpeedDegreesPerSecond,
                desiredSpeed,
                changeRate * deltaTime);
            float delta = nextSpeedDegreesPerSecond * deltaTime;
            if (delta * direction >= 0f && Mathf.Abs(delta) >= remainingAngle)
            {
                nextSpeedDegreesPerSecond = 0f;
                return signedAngleDegrees;
            }

            return delta;
        }

        private static Vector3 MoveTowardsVector(
            Vector3 current,
            Vector3 target,
            float maximumDelta)
        {
            Vector3 difference = target - current;
            float sqrDistance = difference.sqrMagnitude;
            if (sqrDistance <= maximumDelta * maximumDelta || sqrDistance <= 0.0000001f)
                return target;
            return current + difference / Mathf.Sqrt(sqrDistance) * maximumDelta;
        }

        private float ResolveSpeed(in CharacterMotorCommand command)
        {
            return CalculateMovementSpeed(runtimeSettings, command);
        }

        private void ResolveReferences()
        {
            characterController = characterController != null
                ? characterController
                : GetComponent<UnityEngine.CharacterController>();
            commandSource = commandSourceBehaviour as ICharacterMotorCommandSource;
        }

        private bool IsControllerGrounded()
        {
            return characterController != null
                && characterController.enabled
                && characterController.isGrounded;
        }

        private void RefreshState(
            Vector3 velocity,
            bool grounded,
            bool sprinting,
            bool crouched)
        {
            bool jumping = !grounded && velocity.y > 0.0001f;
            bool falling = !grounded
                && velocity.y < -runtimeSettings.FallSpeedThreshold;
            state = new CharacterMotorState(
                velocity,
                grounded,
                jumping,
                falling,
                sprinting,
                crouched,
                suspended);
        }

        private static Vector3 FlattenAndNormalize(Vector3 direction)
        {
            direction.y = 0f;
            float sqrMagnitude = direction.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.0001f)
                return Vector3.zero;
            return direction / Mathf.Sqrt(sqrMagnitude);
        }
    }
}
