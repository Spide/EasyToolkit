using UnityEngine;

namespace Easy.Character.Animation
{
    public readonly struct CharacterAnimationSettings
    {
        public CharacterAnimationSettings(
            string movementSpeedParameter,
            string movementSpeedMultiplierParameter,
            string movementDirectionXParameter,
            string movementDirectionYParameter,
            string aimingParameter,
            string crouchedParameter,
            string sprintingParameter,
            string jumpingParameter,
            string fallingParameter,
            string movementLayer,
            string aimingLayer,
            string crouchLayer,
            string sprintLayer,
            string jumpLayer,
            string fallLayer,
            float idleSpeedThreshold,
            float minimumMovingMultiplier,
            float normalReferenceSpeed,
            float sprintReferenceSpeed,
            float maximumSprintMultiplier,
            float parameterDampSeconds,
            float locomotionLayerBlendSeconds,
            float airborneLayerBlendSeconds)
        {
            MovementSpeedParameter = movementSpeedParameter;
            MovementSpeedMultiplierParameter = movementSpeedMultiplierParameter;
            MovementDirectionXParameter = movementDirectionXParameter;
            MovementDirectionYParameter = movementDirectionYParameter;
            AimingParameter = aimingParameter;
            CrouchedParameter = crouchedParameter;
            SprintingParameter = sprintingParameter;
            JumpingParameter = jumpingParameter;
            FallingParameter = fallingParameter;
            MovementLayer = movementLayer;
            AimingLayer = aimingLayer;
            CrouchLayer = crouchLayer;
            SprintLayer = sprintLayer;
            JumpLayer = jumpLayer;
            FallLayer = fallLayer;
            IdleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
            MinimumMovingMultiplier = Mathf.Max(0.1f, minimumMovingMultiplier);
            NormalReferenceSpeed = Mathf.Max(0.1f, normalReferenceSpeed);
            SprintReferenceSpeed = Mathf.Max(NormalReferenceSpeed, sprintReferenceSpeed);
            MaximumSprintMultiplier = Mathf.Max(1f, maximumSprintMultiplier);
            ParameterDampSeconds = Mathf.Max(0f, parameterDampSeconds);
            LocomotionLayerBlendSeconds = Mathf.Max(0f, locomotionLayerBlendSeconds);
            AirborneLayerBlendSeconds = Mathf.Max(0f, airborneLayerBlendSeconds);
        }

        public string MovementSpeedParameter { get; }
        public string MovementSpeedMultiplierParameter { get; }
        public string MovementDirectionXParameter { get; }
        public string MovementDirectionYParameter { get; }
        public string AimingParameter { get; }
        public string CrouchedParameter { get; }
        public string SprintingParameter { get; }
        public string JumpingParameter { get; }
        public string FallingParameter { get; }
        public string MovementLayer { get; }
        public string AimingLayer { get; }
        public string CrouchLayer { get; }
        public string SprintLayer { get; }
        public string JumpLayer { get; }
        public string FallLayer { get; }
        public float IdleSpeedThreshold { get; }
        public float MinimumMovingMultiplier { get; }
        public float NormalReferenceSpeed { get; }
        public float SprintReferenceSpeed { get; }
        public float MaximumSprintMultiplier { get; }
        public float ParameterDampSeconds { get; }
        public float LocomotionLayerBlendSeconds { get; }
        public float AirborneLayerBlendSeconds { get; }

        public static CharacterAnimationSettings Default => new(
            "movement_speed",
            "movement_speed_multiplier",
            "movement_direction_x",
            "movement_direction_y",
            "is_aiming",
            "is_crouched",
            "is_sprinting",
            "is_jumping",
            "is_falling",
            "Movement",
            "Aiming",
            "Crouch",
            "Sprint",
            "Jump",
            "Fall",
            0.15f,
            0.8f,
            2.5f,
            8f,
            1.35f,
            0.12f,
            0.15f,
            0.08f);
    }

    [CreateAssetMenu]
    public sealed class CharacterAnimationProfile : ScriptableObject
    {
        [Header("Parameters")]
        [SerializeField] private string movementSpeedParameter = "movement_speed";
        [SerializeField] private string movementSpeedMultiplierParameter = "movement_speed_multiplier";
        [SerializeField] private string movementDirectionXParameter = "movement_direction_x";
        [SerializeField] private string movementDirectionYParameter = "movement_direction_y";
        [SerializeField] private string aimingParameter = "is_aiming";
        [SerializeField] private string crouchedParameter = "is_crouched";
        [SerializeField] private string sprintingParameter = "is_sprinting";
        [SerializeField] private string jumpingParameter = "is_jumping";
        [SerializeField] private string fallingParameter = "is_falling";

        [Header("Layers")]
        [SerializeField] private string movementLayer = "Movement";
        [SerializeField] private string aimingLayer = "Aiming";
        [SerializeField] private string crouchLayer = "Crouch";
        [SerializeField] private string sprintLayer = "Sprint";
        [SerializeField] private string jumpLayer = "Jump";
        [SerializeField] private string fallLayer = "Fall";

        [Header("Cadence")]
        [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.15f;
        [SerializeField, Min(0.1f)] private float minimumMovingMultiplier = 0.8f;
        [SerializeField, Min(0.1f)] private float normalReferenceSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float sprintReferenceSpeed = 8f;
        [SerializeField, Min(1f)] private float maximumSprintMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float parameterDampSeconds = 0.12f;
        [SerializeField, Min(0f)] private float locomotionLayerBlendSeconds = 0.15f;
        [SerializeField, Min(0f)] private float airborneLayerBlendSeconds = 0.08f;

        public string MovementSpeedParameter => movementSpeedParameter;
        public string MovementSpeedMultiplierParameter => movementSpeedMultiplierParameter;
        public string MovementDirectionXParameter => movementDirectionXParameter;
        public string MovementDirectionYParameter => movementDirectionYParameter;
        public string AimingParameter => aimingParameter;
        public string CrouchedParameter => crouchedParameter;
        public string SprintingParameter => sprintingParameter;
        public string JumpingParameter => jumpingParameter;
        public string FallingParameter => fallingParameter;
        public string MovementLayer => movementLayer;
        public string AimingLayer => aimingLayer;
        public string CrouchLayer => crouchLayer;
        public string SprintLayer => sprintLayer;
        public string JumpLayer => jumpLayer;
        public string FallLayer => fallLayer;
        public float IdleSpeedThreshold => idleSpeedThreshold;
        public float MinimumMovingMultiplier => minimumMovingMultiplier;
        public float NormalReferenceSpeed => normalReferenceSpeed;
        public float SprintReferenceSpeed => sprintReferenceSpeed;
        public float MaximumSprintMultiplier => maximumSprintMultiplier;
        public float ParameterDampSeconds => parameterDampSeconds;
        public float LocomotionLayerBlendSeconds => locomotionLayerBlendSeconds;
        public float AirborneLayerBlendSeconds => airborneLayerBlendSeconds;

        public CharacterAnimationSettings Settings => new(
            movementSpeedParameter,
            movementSpeedMultiplierParameter,
            movementDirectionXParameter,
            movementDirectionYParameter,
            aimingParameter,
            crouchedParameter,
            sprintingParameter,
            jumpingParameter,
            fallingParameter,
            movementLayer,
            aimingLayer,
            crouchLayer,
            sprintLayer,
            jumpLayer,
            fallLayer,
            idleSpeedThreshold,
            minimumMovingMultiplier,
            normalReferenceSpeed,
            sprintReferenceSpeed,
            maximumSprintMultiplier,
            parameterDampSeconds,
            locomotionLayerBlendSeconds,
            airborneLayerBlendSeconds);
    }
}
