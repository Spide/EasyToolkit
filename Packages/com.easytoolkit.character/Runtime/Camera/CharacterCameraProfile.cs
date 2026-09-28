using System;
using UnityEngine;

namespace Easy.Character.CameraSystem
{
    public enum CharacterCameraMode
    {
        ThirdPerson = 0,
        TopDown = 1,
        FirstPerson = 2,
    }

    [Serializable]
    public readonly struct CharacterCameraSettings
    {
        public CharacterCameraSettings(
            Vector3 targetOffset,
            Vector3 shoulderOffset,
            float followDistance,
            float followSharpness,
            float pitchDegrees,
            float minimumPitch,
            float maximumPitch,
            float rotationDegreesPerSecond,
            Vector3 topDownOffset,
            float topDownSharpness,
            float minimumTopDownDistance,
            float maximumTopDownDistance,
            float zoomSpeed,
            Vector3 firstPersonOffset,
            float collisionRadius,
            float collisionPadding,
            LayerMask collisionMask)
        {
            TargetOffset = targetOffset;
            ShoulderOffset = shoulderOffset;
            FollowDistance = Mathf.Max(0f, followDistance);
            FollowSharpness = Mathf.Max(0f, followSharpness);
            PitchDegrees = pitchDegrees;
            MinimumPitch = Mathf.Min(minimumPitch, maximumPitch);
            MaximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            RotationDegreesPerSecond = Mathf.Max(0f, rotationDegreesPerSecond);
            TopDownOffset = topDownOffset;
            TopDownSharpness = Mathf.Max(0f, topDownSharpness);
            MinimumTopDownDistance = Mathf.Max(0.1f, minimumTopDownDistance);
            MaximumTopDownDistance = Mathf.Max(MinimumTopDownDistance, maximumTopDownDistance);
            ZoomSpeed = Mathf.Max(0f, zoomSpeed);
            FirstPersonOffset = firstPersonOffset;
            CollisionRadius = Mathf.Max(0f, collisionRadius);
            CollisionPadding = Mathf.Max(0f, collisionPadding);
            CollisionMask = collisionMask;
        }

        public Vector3 TargetOffset { get; }
        public Vector3 ShoulderOffset { get; }
        public float FollowDistance { get; }
        public float FollowSharpness { get; }
        public float PitchDegrees { get; }
        public float MinimumPitch { get; }
        public float MaximumPitch { get; }
        public float RotationDegreesPerSecond { get; }
        public Vector3 TopDownOffset { get; }
        public float TopDownSharpness { get; }
        public float MinimumTopDownDistance { get; }
        public float MaximumTopDownDistance { get; }
        public float ZoomSpeed { get; }
        public Vector3 FirstPersonOffset { get; }
        public float CollisionRadius { get; }
        public float CollisionPadding { get; }
        public LayerMask CollisionMask { get; }

        public static CharacterCameraSettings Default => new(
            new Vector3(0f, 1.45f, 0f),
            new Vector3(0.55f, 0f, 0f),
            4.5f,
            14f,
            16f,
            -40f,
            70f,
            120f,
            new Vector3(0f, 10f, -8f),
            10f,
            5f,
            18f,
            8f,
            new Vector3(0f, 1.65f, 0.08f),
            0.25f,
            0.1f,
            ~0);
    }

    [CreateAssetMenu]
    public sealed class CharacterCameraProfile : ScriptableObject
    {
        [SerializeField] private Vector3 targetOffset = new(0f, 1.45f, 0f);
        [SerializeField] private Vector3 shoulderOffset = new(0.55f, 0f, 0f);
        [SerializeField, Min(0f)] private float followDistance = 4.5f;
        [SerializeField, Min(0f)] private float followSharpness = 14f;
        [SerializeField] private float pitchDegrees = 16f;
        [SerializeField] private float minimumPitch = -40f;
        [SerializeField] private float maximumPitch = 70f;
        [SerializeField, Min(0f)] private float rotationDegreesPerSecond = 120f;
        [SerializeField] private Vector3 topDownOffset = new(0f, 10f, -8f);
        [SerializeField, Min(0f)] private float topDownSharpness = 10f;
        [SerializeField, Min(0.1f)] private float minimumTopDownDistance = 5f;
        [SerializeField, Min(0.1f)] private float maximumTopDownDistance = 18f;
        [SerializeField, Min(0f)] private float zoomSpeed = 8f;
        [SerializeField] private Vector3 firstPersonOffset = new(0f, 1.65f, 0.08f);
        [SerializeField, Min(0f)] private float collisionRadius = 0.25f;
        [SerializeField, Min(0f)] private float collisionPadding = 0.1f;
        [SerializeField] private LayerMask collisionMask = ~0;

        public CharacterCameraSettings Settings => new(
            targetOffset,
            shoulderOffset,
            followDistance,
            followSharpness,
            pitchDegrees,
            minimumPitch,
            maximumPitch,
            rotationDegreesPerSecond,
            topDownOffset,
            topDownSharpness,
            minimumTopDownDistance,
            maximumTopDownDistance,
            zoomSpeed,
            firstPersonOffset,
            collisionRadius,
            collisionPadding,
            collisionMask);
    }
}
