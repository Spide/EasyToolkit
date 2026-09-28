using System;
using UnityEngine;

namespace Easy.Character.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class CharacterCameraRig : MonoBehaviour
    {
        private const int CollisionHitCapacity = 16;

        [SerializeField] private CharacterCameraProfile profile;
        [SerializeField] private Transform target;
        [SerializeField] private MonoBehaviour commandSourceBehaviour;
        [SerializeField] private CharacterUpdateMode updateMode = CharacterUpdateMode.Automatic;
        [SerializeField] private CharacterCameraMode mode;

        private readonly RaycastHit[] collisionHits = new RaycastHit[CollisionHitCapacity];
        private ICharacterCameraCommandSource commandSource;
        private CharacterCameraSettings settings = CharacterCameraSettings.Default;
        private float yawDegrees;
        private float pitchDegrees;
        private Vector3 topDownOffset;
        private Vector2 externalRotationOffset;
        private bool initialized;

        public event Action<CharacterCameraMode> ModeChanged;

        public CharacterCameraMode Mode => mode;
        public Transform Target => target;
        public Vector3 PlanarForward => CalculatePlanarDirection(transform.forward, Vector3.forward);
        public Vector3 PlanarRight => CalculatePlanarDirection(transform.right, Vector3.right);

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void LateUpdate()
        {
            if (updateMode != CharacterUpdateMode.Automatic)
                return;

            CharacterCameraCommand command = default;
            commandSource?.TryGetCameraCommand(out command);
            Simulate(command, Time.unscaledDeltaTime);
        }

        public void Configure(
            CharacterCameraProfile cameraProfile,
            CharacterUpdateMode modeValue = CharacterUpdateMode.Automatic,
            MonoBehaviour source = null)
        {
            profile = cameraProfile;
            settings = profile != null ? profile.Settings : CharacterCameraSettings.Default;
            updateMode = modeValue;
            commandSourceBehaviour = source;
            commandSource = source as ICharacterCameraCommandSource;
            initialized = false;
            InitializeIfNeeded();
        }

        public void ConfigureRuntime(
            CharacterCameraSettings runtimeSettings,
            CharacterUpdateMode modeValue)
        {
            settings = runtimeSettings;
            updateMode = modeValue;
            initialized = false;
            InitializeIfNeeded();
        }

        public void SetCommandSource(MonoBehaviour source)
        {
            commandSourceBehaviour = source;
            commandSource = source as ICharacterCameraCommandSource;
        }

        public void UpdateRuntimeSettings(CharacterCameraSettings runtimeSettings)
        {
            settings = runtimeSettings;
            pitchDegrees = Mathf.Clamp(
                pitchDegrees,
                settings.MinimumPitch,
                settings.MaximumPitch);
        }

        public void SetTarget(Transform nextTarget, bool alignYaw = false)
        {
            target = nextTarget;
            if (alignYaw && target != null)
                yawDegrees = target.eulerAngles.y;
        }

        public void SetMode(CharacterCameraMode nextMode)
        {
            if (mode == nextMode)
                return;
            mode = nextMode;
            ModeChanged?.Invoke(mode);
        }

        public void SetOrbit(float yaw, float pitch)
        {
            yawDegrees = yaw;
            pitchDegrees = Mathf.Clamp(pitch, settings.MinimumPitch, settings.MaximumPitch);
        }

        public void SetTopDownOffset(Vector3 offset)
        {
            topDownOffset = offset;
        }

        public void SetExternalRotationOffset(Vector2 offset)
        {
            externalRotationOffset = offset;
        }

        public void Simulate(in CharacterCameraCommand command, float deltaTime)
        {
            InitializeIfNeeded();
            if (target == null || deltaTime < 0f)
                return;

            ApplyCommand(command, deltaTime);
            switch (mode)
            {
                case CharacterCameraMode.FirstPerson:
                    ApplyFirstPersonPose();
                    break;
                case CharacterCameraMode.TopDown:
                    ApplyTopDownPose(deltaTime);
                    break;
                default:
                    ApplyThirdPersonPose(deltaTime);
                    break;
            }
        }

        public static Vector3 CalculatePlanarDirection(Vector3 value, Vector3 fallback)
        {
            value.y = 0f;
            float sqrMagnitude = value.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.0001f)
                return fallback;
            return value / Mathf.Sqrt(sqrMagnitude);
        }

        public static float CalculateSmoothedBlend(float sharpness, float deltaTime)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * Mathf.Max(0f, deltaTime));
        }

        public static Vector3 CalculateZoomedOffset(
            Vector3 currentOffset,
            Vector3 fallbackOffset,
            float zoomInput,
            float zoomSpeed,
            float minimumDistance,
            float maximumDistance)
        {
            float distance = CalculateZoomedDistance(
                currentOffset.magnitude,
                zoomInput,
                zoomSpeed,
                minimumDistance,
                maximumDistance);
            return CalculatePlanarAwareDirection(currentOffset, fallbackOffset) * distance;
        }

        public static float CalculateZoomedDistance(
            float currentDistance,
            float zoomInput,
            float zoomSpeed,
            float minimumDistance,
            float maximumDistance)
        {
            float minimum = Mathf.Max(0.1f, minimumDistance);
            float maximum = Mathf.Max(minimum, maximumDistance);
            return Mathf.Clamp(
                currentDistance - zoomInput * Mathf.Max(0f, zoomSpeed) * 0.01f,
                minimum,
                maximum);
        }

        public static Vector3 RotateWorldOffset(Vector3 offset, float yawDegrees)
        {
            return Mathf.Abs(yawDegrees) <= 0.0001f
                ? offset
                : Quaternion.Euler(0f, yawDegrees, 0f) * offset;
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
                return;
            if (profile != null)
                settings = profile.Settings;
            commandSource = commandSourceBehaviour as ICharacterCameraCommandSource;
            pitchDegrees = Mathf.Clamp(
                settings.PitchDegrees,
                settings.MinimumPitch,
                settings.MaximumPitch);
            topDownOffset = settings.TopDownOffset;
            if (target != null && Mathf.Abs(yawDegrees) <= 0.0001f)
                yawDegrees = target.eulerAngles.y;
            initialized = true;
        }

        private void ApplyCommand(in CharacterCameraCommand command, float deltaTime)
        {
            Vector2 look = command.OrbitHeld ? command.Look : Vector2.zero;
            float yawInput = look.x + command.Yaw;
            if (mode == CharacterCameraMode.TopDown)
            {
                float rotation = (yawInput + command.Shoulder)
                    * settings.RotationDegreesPerSecond
                    * deltaTime;
                if (Mathf.Abs(rotation) > 0.0001f)
                    topDownOffset = Quaternion.Euler(0f, rotation, 0f) * topDownOffset;

                if (Mathf.Abs(command.Zoom) > 0.0001f)
                {
                    topDownOffset = CalculateZoomedOffset(
                        topDownOffset,
                        settings.TopDownOffset,
                        command.Zoom,
                        settings.ZoomSpeed,
                        settings.MinimumTopDownDistance,
                        settings.MaximumTopDownDistance);
                }
                return;
            }

            yawDegrees += yawInput * settings.RotationDegreesPerSecond * deltaTime;
            pitchDegrees = Mathf.Clamp(
                pitchDegrees - look.y * settings.RotationDegreesPerSecond * deltaTime,
                settings.MinimumPitch,
                settings.MaximumPitch);
        }

        private void ApplyThirdPersonPose(float deltaTime)
        {
            Quaternion rotation = ResolveRotation();
            Vector3 pivot = target.position + settings.TargetOffset;
            Vector3 desiredOffset = rotation
                * (settings.ShoulderOffset + Vector3.back * settings.FollowDistance);
            Vector3 desiredPosition = ResolveCollision(pivot, desiredOffset);
            float blend = CalculateSmoothedBlend(settings.FollowSharpness, deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
        }

        private void ApplyTopDownPose(float deltaTime)
        {
            Vector3 desiredPosition = target.position + topDownOffset;
            float blend = CalculateSmoothedBlend(settings.TopDownSharpness, deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            Vector3 lookDirection = target.position - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private void ApplyFirstPersonPose()
        {
            transform.SetPositionAndRotation(
                target.TransformPoint(settings.FirstPersonOffset),
                ResolveRotation());
        }

        private Quaternion ResolveRotation()
        {
            return Quaternion.Euler(
                pitchDegrees - externalRotationOffset.y,
                yawDegrees + externalRotationOffset.x,
                0f);
        }

        private Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredOffset)
        {
            float distance = desiredOffset.magnitude;
            if (distance <= 0.0001f)
                return pivot;

            Vector3 direction = desiredOffset / distance;
            int hitCount = Physics.SphereCastNonAlloc(
                pivot,
                settings.CollisionRadius,
                direction,
                collisionHits,
                distance,
                settings.CollisionMask,
                QueryTriggerInteraction.Ignore);
            float allowedDistance = distance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = collisionHits[i];
                if (hit.collider == null
                    || hit.collider.transform == target
                    || hit.collider.transform.IsChildOf(target))
                {
                    continue;
                }

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, hit.distance - settings.CollisionPadding));
            }
            return pivot + direction * allowedDistance;
        }

        private static Vector3 CalculatePlanarAwareDirection(Vector3 value, Vector3 fallback)
        {
            float sqrMagnitude = value.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.0001f)
            {
                sqrMagnitude = fallback.sqrMagnitude;
                return sqrMagnitude > 0.0001f
                    ? fallback / Mathf.Sqrt(sqrMagnitude)
                    : new Vector3(0f, 0.75f, -0.66f).normalized;
            }
            return value / Mathf.Sqrt(sqrMagnitude);
        }
    }
}
