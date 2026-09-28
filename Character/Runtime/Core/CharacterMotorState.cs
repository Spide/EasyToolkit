using UnityEngine;

namespace Easy.Character
{
    public readonly struct CharacterMotorState
    {
        public CharacterMotorState(
            Vector3 velocity,
            bool isGrounded,
            bool isJumping,
            bool isFalling,
            bool isSprinting,
            bool isCrouched,
            bool isSuspended)
        {
            Velocity = velocity;
            IsGrounded = isGrounded;
            IsJumping = isJumping;
            IsFalling = isFalling;
            IsSprinting = isSprinting;
            IsCrouched = isCrouched;
            IsSuspended = isSuspended;
        }

        public Vector3 Velocity { get; }
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        public float VerticalSpeed => Velocity.y;
        public bool IsGrounded { get; }
        public bool IsJumping { get; }
        public bool IsFalling { get; }
        public bool IsSprinting { get; }
        public bool IsCrouched { get; }
        public bool IsSuspended { get; }
    }
}
