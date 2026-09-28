using UnityEngine;

namespace Easy.Character.Animation
{
    public readonly struct CharacterAnimationFrame
    {
        public CharacterAnimationFrame(
            Vector3 worldVelocity,
            Transform facingReference,
            bool isAiming,
            bool isCrouched,
            bool isSprinting,
            bool isJumping,
            bool isFalling,
            bool forceNormalPlayback = false)
        {
            WorldVelocity = worldVelocity;
            FacingReference = facingReference;
            IsAiming = isAiming;
            IsCrouched = isCrouched;
            IsSprinting = isSprinting;
            IsJumping = isJumping;
            IsFalling = isFalling;
            ForceNormalPlayback = forceNormalPlayback;
        }

        public Vector3 WorldVelocity { get; }
        public Transform FacingReference { get; }
        public bool IsAiming { get; }
        public bool IsCrouched { get; }
        public bool IsSprinting { get; }
        public bool IsJumping { get; }
        public bool IsFalling { get; }
        public bool ForceNormalPlayback { get; }
    }
}
