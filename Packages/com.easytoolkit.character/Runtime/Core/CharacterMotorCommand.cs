using UnityEngine;

namespace Easy.Character
{
    public readonly struct CharacterMotorCommand
    {
        public CharacterMotorCommand(
            Vector3 worldDirection,
            float inputAmount,
            bool sprintHeld = false,
            bool crouched = false,
            bool jumpRequested = false,
            bool faceMovement = true,
            float speedOverride = 0f)
        {
            WorldDirection = worldDirection;
            InputAmount = Mathf.Clamp01(inputAmount);
            SprintHeld = sprintHeld;
            Crouched = crouched;
            JumpRequested = jumpRequested;
            FaceMovement = faceMovement;
            FacingMode = faceMovement
                ? CharacterMotorFacingMode.MovementDirection
                : CharacterMotorFacingMode.Preserve;
            WorldFacingDirection = Vector3.zero;
            SpeedOverride = Mathf.Max(0f, speedOverride);
        }

        public CharacterMotorCommand(
            Vector3 worldDirection,
            float inputAmount,
            CharacterMotorFacingMode facingMode,
            Vector3 worldFacingDirection,
            bool sprintHeld = false,
            bool crouched = false,
            bool jumpRequested = false,
            float speedOverride = 0f)
        {
            WorldDirection = worldDirection;
            InputAmount = Mathf.Clamp01(inputAmount);
            SprintHeld = sprintHeld;
            Crouched = crouched;
            JumpRequested = jumpRequested;
            FaceMovement = facingMode == CharacterMotorFacingMode.MovementDirection;
            FacingMode = facingMode;
            WorldFacingDirection = worldFacingDirection;
            SpeedOverride = Mathf.Max(0f, speedOverride);
        }

        public Vector3 WorldDirection { get; }
        public float InputAmount { get; }
        public bool SprintHeld { get; }
        public bool Crouched { get; }
        public bool JumpRequested { get; }
        public bool FaceMovement { get; }
        public CharacterMotorFacingMode FacingMode { get; }
        public Vector3 WorldFacingDirection { get; }
        public float SpeedOverride { get; }
    }

    public enum CharacterMotorFacingMode
    {
        Preserve = 0,
        MovementDirection = 1,
        ExplicitDirection = 2,
    }

    public readonly struct CharacterCameraCommand
    {
        public CharacterCameraCommand(
            Vector2 look,
            float yaw,
            float shoulder,
            float zoom,
            bool orbitHeld = false)
        {
            Look = look;
            Yaw = yaw;
            Shoulder = shoulder;
            Zoom = zoom;
            OrbitHeld = orbitHeld;
        }

        public Vector2 Look { get; }
        public float Yaw { get; }
        public float Shoulder { get; }
        public float Zoom { get; }
        public bool OrbitHeld { get; }
    }

    public interface ICharacterMotorCommandSource
    {
        bool TryGetMotorCommand(out CharacterMotorCommand command);
    }

    public interface ICharacterCameraCommandSource
    {
        bool TryGetCameraCommand(out CharacterCameraCommand command);
    }

    public enum CharacterUpdateMode
    {
        Automatic = 0,
        Manual = 1,
    }
}
