using UnityEngine;
using UnityEngine.InputSystem;

namespace Easy.Character.InputSystem
{
    [DisallowMultipleComponent]
    public sealed class InputSystemCharacterInputSource : MonoBehaviour,
        ICharacterMotorCommandSource,
        ICharacterCameraCommandSource
    {
        [SerializeField] private Transform movementReference;
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference look;
        [SerializeField] private InputActionReference sprint;
        [SerializeField] private InputActionReference crouch;
        [SerializeField] private InputActionReference jump;
        [SerializeField] private InputActionReference cameraYaw;
        [SerializeField] private InputActionReference cameraShoulder;
        [SerializeField] private InputActionReference zoom;
        [SerializeField] private InputActionReference orbit;
        [SerializeField] private bool enableReferencedActions = true;

        private int enabledActionMask;
        private bool crouched;

        private void OnEnable()
        {
            if (!enableReferencedActions)
                return;

            EnableReferencedActions();
        }

        private void OnDisable()
        {
            DisableOwnedActions();
        }

        private void EnableReferencedActions()
        {
            enabledActionMask = 0;
            EnableIfNeeded(move, 0);
            EnableIfNeeded(look, 1);
            EnableIfNeeded(sprint, 2);
            EnableIfNeeded(crouch, 3);
            EnableIfNeeded(jump, 4);
            EnableIfNeeded(cameraYaw, 5);
            EnableIfNeeded(cameraShoulder, 6);
            EnableIfNeeded(zoom, 7);
            EnableIfNeeded(orbit, 8);
        }

        private void DisableOwnedActions()
        {
            DisableIfOwned(move, 0);
            DisableIfOwned(look, 1);
            DisableIfOwned(sprint, 2);
            DisableIfOwned(crouch, 3);
            DisableIfOwned(jump, 4);
            DisableIfOwned(cameraYaw, 5);
            DisableIfOwned(cameraShoulder, 6);
            DisableIfOwned(zoom, 7);
            DisableIfOwned(orbit, 8);
            enabledActionMask = 0;
        }

        public void SetMovementReference(Transform reference)
        {
            movementReference = reference;
        }

        public void ConfigureActions(
            InputActionReference moveAction,
            InputActionReference lookAction,
            InputActionReference sprintAction,
            InputActionReference crouchAction,
            InputActionReference jumpAction,
            InputActionReference yawAction,
            InputActionReference shoulderAction,
            InputActionReference zoomAction,
            InputActionReference orbitAction)
        {
            bool reacquireActions = isActiveAndEnabled && enableReferencedActions;
            if (reacquireActions)
                DisableOwnedActions();

            move = moveAction;
            look = lookAction;
            sprint = sprintAction;
            crouch = crouchAction;
            jump = jumpAction;
            cameraYaw = yawAction;
            cameraShoulder = shoulderAction;
            zoom = zoomAction;
            orbit = orbitAction;
            if (reacquireActions)
                EnableReferencedActions();
        }

        public bool TryGetMotorCommand(out CharacterMotorCommand command)
        {
            Vector2 movement = ReadVector2(move);
            if (WasPressed(crouch))
                crouched = !crouched;

            Vector3 direction = CalculateCameraRelativeDirection(
                movement,
                movementReference);
            command = new CharacterMotorCommand(
                direction,
                Mathf.Clamp01(movement.magnitude),
                IsPressed(sprint),
                crouched,
                WasPressed(jump));
            return true;
        }

        public bool TryGetCameraCommand(out CharacterCameraCommand command)
        {
            Vector2 scroll = ReadVector2(zoom);
            command = new CharacterCameraCommand(
                ReadVector2(look),
                ReadFloat(cameraYaw),
                ReadFloat(cameraShoulder),
                Mathf.Abs(scroll.y) > Mathf.Abs(scroll.x) ? scroll.y : scroll.x,
                orbit == null || orbit.action == null || IsPressed(orbit));
            return true;
        }

        public static Vector3 CalculateCameraRelativeDirection(
            Vector2 input,
            Transform reference)
        {
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            Vector3 forward = Flatten(reference != null ? reference.forward : Vector3.forward);
            Vector3 right = Flatten(reference != null ? reference.right : Vector3.right);
            Vector3 direction = forward * input.y + right * input.x;
            float sqrMagnitude = direction.sqrMagnitude;
            return !float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.0001f
                ? Vector3.zero
                : direction / Mathf.Sqrt(sqrMagnitude);
        }

        private void EnableIfNeeded(InputActionReference reference, int bit)
        {
            InputAction action = reference != null ? reference.action : null;
            if (action == null || action.enabled)
                return;
            action.Enable();
            enabledActionMask |= 1 << bit;
        }

        private void DisableIfOwned(InputActionReference reference, int bit)
        {
            if ((enabledActionMask & (1 << bit)) == 0)
                return;
            reference?.action?.Disable();
        }

        private static Vector2 ReadVector2(InputActionReference reference)
        {
            InputAction action = reference != null ? reference.action : null;
            return action != null && action.enabled
                ? action.ReadValue<Vector2>()
                : Vector2.zero;
        }

        private static float ReadFloat(InputActionReference reference)
        {
            InputAction action = reference != null ? reference.action : null;
            return action != null && action.enabled ? action.ReadValue<float>() : 0f;
        }

        private static bool IsPressed(InputActionReference reference)
        {
            InputAction action = reference != null ? reference.action : null;
            return action != null && action.enabled && action.IsPressed();
        }

        private static bool WasPressed(InputActionReference reference)
        {
            InputAction action = reference != null ? reference.action : null;
            return action != null && action.enabled && action.WasPressedThisFrame();
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            float sqrMagnitude = value.sqrMagnitude;
            return !float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.0001f
                ? Vector3.forward
                : value / Mathf.Sqrt(sqrMagnitude);
        }
    }
}
