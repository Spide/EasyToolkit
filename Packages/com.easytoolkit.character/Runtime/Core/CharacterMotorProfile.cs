using System;
using UnityEngine;

namespace Easy.Character
{
    [Serializable]
    public readonly struct CharacterMotorSettings
    {
        public CharacterMotorSettings(
            float walkSpeed,
            float sprintMultiplier,
            float crouchMultiplier,
            float acceleration,
            float deceleration,
            float rotationSpeedDegrees,
            float gravityMultiplier,
            float jumpHeight,
            float jumpTimeToApex,
            float groundedVerticalSpeed,
            float fallSpeedThreshold)
            : this(
                walkSpeed,
                sprintMultiplier,
                crouchMultiplier,
                acceleration,
                deceleration,
                rotationSpeedDegrees,
                0f,
                0f,
                gravityMultiplier,
                jumpHeight,
                jumpTimeToApex,
                groundedVerticalSpeed,
                fallSpeedThreshold)
        {
        }

        public CharacterMotorSettings(
            float walkSpeed,
            float sprintMultiplier,
            float crouchMultiplier,
            float acceleration,
            float deceleration,
            float rotationSpeedDegrees,
            float explicitFacingAccelerationDegreesPerSecondSquared,
            float explicitFacingDecelerationDegreesPerSecondSquared,
            float gravityMultiplier,
            float jumpHeight,
            float jumpTimeToApex,
            float groundedVerticalSpeed,
            float fallSpeedThreshold)
        {
            WalkSpeed = Mathf.Max(0f, walkSpeed);
            SprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            CrouchMultiplier = Mathf.Clamp(crouchMultiplier, 0.1f, 1f);
            Acceleration = Mathf.Max(0f, acceleration);
            Deceleration = Mathf.Max(0f, deceleration);
            RotationSpeedDegrees = Mathf.Max(0f, rotationSpeedDegrees);
            ExplicitFacingAccelerationDegreesPerSecondSquared = Mathf.Max(
                0f,
                explicitFacingAccelerationDegreesPerSecondSquared);
            ExplicitFacingDecelerationDegreesPerSecondSquared = Mathf.Max(
                0f,
                explicitFacingDecelerationDegreesPerSecondSquared);
            GravityMultiplier = Mathf.Max(0f, gravityMultiplier);
            JumpHeight = Mathf.Max(0f, jumpHeight);
            JumpTimeToApex = Mathf.Max(0.01f, jumpTimeToApex);
            GroundedVerticalSpeed = Mathf.Min(0f, groundedVerticalSpeed);
            FallSpeedThreshold = Mathf.Max(0f, fallSpeedThreshold);
        }

        public float WalkSpeed { get; }
        public float SprintMultiplier { get; }
        public float CrouchMultiplier { get; }
        public float Acceleration { get; }
        public float Deceleration { get; }
        public float RotationSpeedDegrees { get; }
        public float ExplicitFacingAccelerationDegreesPerSecondSquared { get; }
        public float ExplicitFacingDecelerationDegreesPerSecondSquared { get; }
        public float GravityMultiplier { get; }
        public float JumpHeight { get; }
        public float JumpTimeToApex { get; }
        public float GroundedVerticalSpeed { get; }
        public float FallSpeedThreshold { get; }

        public static CharacterMotorSettings Default => new(
            2.5f,
            3.25f,
            0.55f,
            12f,
            16f,
            720f,
            720f,
            1080f,
            1f,
            1.2f,
            0.3f,
            -2f,
            0.5f);
    }

    [CreateAssetMenu]
    public sealed class CharacterMotorProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 3.25f;
        [SerializeField, Range(0.1f, 1f)] private float crouchMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float acceleration = 12f;
        [SerializeField, Min(0f)] private float deceleration = 16f;
        [SerializeField, Min(0f)] private float rotationSpeedDegrees = 720f;
        [SerializeField, Min(0f)]
        private float explicitFacingAccelerationDegreesPerSecondSquared = 720f;
        [SerializeField, Min(0f)]
        private float explicitFacingDecelerationDegreesPerSecondSquared = 1080f;
        [SerializeField, Min(0f)] private float gravityMultiplier = 1f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField, Min(0.01f)] private float jumpTimeToApex = 0.3f;
        [SerializeField] private float groundedVerticalSpeed = -2f;
        [SerializeField, Min(0f)] private float fallSpeedThreshold = 0.5f;

        public CharacterMotorSettings Settings => new(
            walkSpeed,
            sprintMultiplier,
            crouchMultiplier,
            acceleration,
            deceleration,
            rotationSpeedDegrees,
            explicitFacingAccelerationDegreesPerSecondSquared,
            explicitFacingDecelerationDegreesPerSecondSquared,
            gravityMultiplier,
            jumpHeight,
            jumpTimeToApex,
            groundedVerticalSpeed,
            fallSpeedThreshold);
    }
}
