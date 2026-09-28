using System;
using UnityEngine;

namespace Easy.Character.Animation
{
    [Serializable]
    public struct CharacterAimSettings
    {
        [SerializeField, Range(0f, 90f)] private float torsoYawRange;
        [SerializeField, Range(0f, 90f)] private float rootTurnStopYaw;
        [SerializeField, Range(0f, 90f)] private float maximumPoseYaw;
        [SerializeField, Range(0f, 89f)] private float maximumDownPitch;
        [SerializeField, Range(0f, 89f)] private float maximumUpPitch;
        [SerializeField, Min(1f)] private float rootTurnDegreesPerSecond;
        [SerializeField, Min(1f)] private float rootTurnAccelerationDegreesPerSecondSquared;
        [SerializeField, Min(1f)] private float rootTurnDecelerationDegreesPerSecondSquared;
        [SerializeField, Min(0f)] private float blendSeconds;
        [SerializeField, Min(0f)] private float poseDampSeconds;
        [SerializeField, Range(0f, 1f)] private float bodyWeight;
        [SerializeField, Range(0f, 1f)] private float headWeight;
        [SerializeField, Range(0f, 1f)] private float lookClampWeight;
        [SerializeField, Range(0f, 1f)] private float rightHandPositionWeight;
        [SerializeField, Range(0f, 1f)] private float rightElbowHintWeight;
        [SerializeField, Range(0f, 1f)] private float rightHandRotationWeight;
        [SerializeField, Range(0f, 1f)] private float supportHandPositionWeight;
        [SerializeField, Range(0f, 1f)] private float supportHandRotationWeight;
        [SerializeField, Range(0f, 1f)] private float supportElbowHintWeight;

        public CharacterAimSettings(
            float torsoYawRange,
            float rootTurnStopYaw,
            float maximumPoseYaw,
            float maximumDownPitch,
            float maximumUpPitch,
            float rootTurnDegreesPerSecond,
            float blendSeconds,
            float poseDampSeconds,
            float bodyWeight,
            float headWeight,
            float lookClampWeight,
            float rightHandPositionWeight,
            float rightElbowHintWeight,
            float rightHandRotationWeight,
            float supportHandPositionWeight,
            float supportHandRotationWeight,
            float supportElbowHintWeight)
            : this(
                torsoYawRange,
                rootTurnStopYaw,
                maximumPoseYaw,
                maximumDownPitch,
                maximumUpPitch,
                rootTurnDegreesPerSecond,
                720f,
                960f,
                blendSeconds,
                poseDampSeconds,
                bodyWeight,
                headWeight,
                lookClampWeight,
                rightHandPositionWeight,
                rightElbowHintWeight,
                rightHandRotationWeight,
                supportHandPositionWeight,
                supportHandRotationWeight,
                supportElbowHintWeight)
        {
        }

        public CharacterAimSettings(
            float torsoYawRange,
            float rootTurnStopYaw,
            float maximumPoseYaw,
            float maximumDownPitch,
            float maximumUpPitch,
            float rootTurnDegreesPerSecond,
            float rootTurnAccelerationDegreesPerSecondSquared,
            float rootTurnDecelerationDegreesPerSecondSquared,
            float blendSeconds,
            float poseDampSeconds,
            float bodyWeight,
            float headWeight,
            float lookClampWeight,
            float rightHandPositionWeight,
            float rightElbowHintWeight,
            float rightHandRotationWeight,
            float supportHandPositionWeight,
            float supportHandRotationWeight,
            float supportElbowHintWeight)
        {
            this.torsoYawRange = torsoYawRange;
            this.rootTurnStopYaw = rootTurnStopYaw;
            this.maximumPoseYaw = maximumPoseYaw;
            this.maximumDownPitch = maximumDownPitch;
            this.maximumUpPitch = maximumUpPitch;
            this.rootTurnDegreesPerSecond = rootTurnDegreesPerSecond;
            this.rootTurnAccelerationDegreesPerSecondSquared =
                rootTurnAccelerationDegreesPerSecondSquared;
            this.rootTurnDecelerationDegreesPerSecondSquared =
                rootTurnDecelerationDegreesPerSecondSquared;
            this.blendSeconds = blendSeconds;
            this.poseDampSeconds = poseDampSeconds;
            this.bodyWeight = bodyWeight;
            this.headWeight = headWeight;
            this.lookClampWeight = lookClampWeight;
            this.rightHandPositionWeight = rightHandPositionWeight;
            this.rightElbowHintWeight = rightElbowHintWeight;
            this.rightHandRotationWeight = rightHandRotationWeight;
            this.supportHandPositionWeight = supportHandPositionWeight;
            this.supportHandRotationWeight = supportHandRotationWeight;
            this.supportElbowHintWeight = supportElbowHintWeight;
        }

        public float TorsoYawRange => torsoYawRange;
        public float RootTurnStopYaw => rootTurnStopYaw;
        public float MaximumPoseYaw => maximumPoseYaw;
        public float MaximumDownPitch => maximumDownPitch;
        public float MaximumUpPitch => maximumUpPitch;
        public float RootTurnDegreesPerSecond => rootTurnDegreesPerSecond;
        public float RootTurnAccelerationDegreesPerSecondSquared =>
            rootTurnAccelerationDegreesPerSecondSquared;
        public float RootTurnDecelerationDegreesPerSecondSquared =>
            rootTurnDecelerationDegreesPerSecondSquared;
        public float BlendSeconds => blendSeconds;
        public float PoseDampSeconds => poseDampSeconds;
        public float BodyWeight => bodyWeight;
        public float HeadWeight => headWeight;
        public float LookClampWeight => lookClampWeight;
        public float RightHandPositionWeight => rightHandPositionWeight;
        public float RightElbowHintWeight => rightElbowHintWeight;
        public float RightHandRotationWeight => rightHandRotationWeight;
        public float SupportHandPositionWeight => supportHandPositionWeight;
        public float SupportHandRotationWeight => supportHandRotationWeight;
        public float SupportElbowHintWeight => supportElbowHintWeight;

        public static CharacterAimSettings Default => new(
            torsoYawRange: 45f,
            rootTurnStopYaw: 35f,
            maximumPoseYaw: 60f,
            maximumDownPitch: 40f,
            maximumUpPitch: 55f,
            rootTurnDegreesPerSecond: 240f,
            rootTurnAccelerationDegreesPerSecondSquared: 720f,
            rootTurnDecelerationDegreesPerSecondSquared: 960f,
            blendSeconds: 0.12f,
            poseDampSeconds: 0.08f,
            bodyWeight: 0.75f,
            headWeight: 0.2f,
            lookClampWeight: 0.35f,
            rightHandPositionWeight: 1f,
            rightElbowHintWeight: 0.8f,
            rightHandRotationWeight: 0f,
            supportHandPositionWeight: 1f,
            supportHandRotationWeight: 0.9f,
            supportElbowHintWeight: 0.8f);

        public CharacterAimSettings Clamp()
        {
            float clampedRange = Mathf.Clamp(torsoYawRange, 0f, 90f);
            float clampedStop = Mathf.Clamp(rootTurnStopYaw, 0f, clampedRange);
            float clampedMaximum = Mathf.Clamp(maximumPoseYaw, clampedRange, 90f);
            return new CharacterAimSettings(
                clampedRange,
                clampedStop,
                clampedMaximum,
                Mathf.Clamp(maximumDownPitch, 0f, 89f),
                Mathf.Clamp(maximumUpPitch, 0f, 89f),
                Mathf.Max(1f, rootTurnDegreesPerSecond),
                Mathf.Max(1f, rootTurnAccelerationDegreesPerSecondSquared),
                Mathf.Max(1f, rootTurnDecelerationDegreesPerSecondSquared),
                Mathf.Max(0f, blendSeconds),
                Mathf.Max(0f, poseDampSeconds),
                Mathf.Clamp01(bodyWeight),
                Mathf.Clamp01(headWeight),
                Mathf.Clamp01(lookClampWeight),
                Mathf.Clamp01(rightHandPositionWeight),
                Mathf.Clamp01(rightElbowHintWeight),
                Mathf.Clamp01(rightHandRotationWeight),
                Mathf.Clamp01(supportHandPositionWeight),
                Mathf.Clamp01(supportHandRotationWeight),
                Mathf.Clamp01(supportElbowHintWeight));
        }
    }

    [CreateAssetMenu(
        fileName = "CharacterAimProfile",
        menuName = "Easy Toolkit/Character/Aim Profile")]
    public sealed class CharacterAimProfile : ScriptableObject
    {
        [SerializeField] private CharacterAimSettings settings = CharacterAimSettings.Default;

        public CharacterAimSettings Settings => settings.Clamp();

        private void OnValidate()
        {
            settings = settings.Clamp();
        }
    }
}
