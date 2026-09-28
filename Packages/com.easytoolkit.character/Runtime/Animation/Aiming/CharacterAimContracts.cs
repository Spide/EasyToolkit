using UnityEngine;

namespace Easy.Character.Animation
{
    public enum CharacterAimRigMode
    {
        Disabled = 0,
        PrimaryHandOnly = 1,
        PrimaryAndSupportHands = 2,
    }

    public readonly struct CharacterAimCommand
    {
        public CharacterAimCommand(
            Vector3 targetWorldPosition,
            bool isAiming,
            bool allowRootTurn = true,
            bool suppressed = false,
            float bodyWeightScale = 1f,
            float handsWeightScale = 1f)
        {
            TargetWorldPosition = targetWorldPosition;
            IsAiming = isAiming;
            AllowRootTurn = allowRootTurn;
            Suppressed = suppressed;
            BodyWeightScale = Mathf.Clamp01(bodyWeightScale);
            HandsWeightScale = Mathf.Clamp01(handsWeightScale);
        }

        public Vector3 TargetWorldPosition { get; }
        public bool IsAiming { get; }
        public bool AllowRootTurn { get; }
        public bool Suppressed { get; }
        public float BodyWeightScale { get; }
        public float HandsWeightScale { get; }
    }

    public readonly struct CharacterAimState
    {
        public CharacterAimState(
            bool supported,
            bool active,
            Vector3 targetWorldPosition,
            float yaw,
            float pitch,
            float weight,
            bool rootTurning,
            float bodyWeightScale = 1f,
            float handsWeightScale = 1f,
            float rootTurnSpeedDegreesPerSecond = 0f)
        {
            Supported = supported;
            Active = active;
            TargetWorldPosition = targetWorldPosition;
            Yaw = yaw;
            Pitch = pitch;
            Weight = weight;
            RootTurning = rootTurning;
            RootTurnSpeedDegreesPerSecond = float.IsFinite(rootTurnSpeedDegreesPerSecond)
                ? rootTurnSpeedDegreesPerSecond
                : 0f;
            BodyWeightScale = Mathf.Clamp01(bodyWeightScale);
            HandsWeightScale = Mathf.Clamp01(handsWeightScale);
        }

        public bool Supported { get; }
        public bool Active { get; }
        public Vector3 TargetWorldPosition { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public float Weight { get; }
        public bool RootTurning { get; }
        public float RootTurnSpeedDegreesPerSecond { get; }
        public float BodyWeightScale { get; }
        public float HandsWeightScale { get; }
    }

    public readonly struct CharacterAimWeaponRig
    {
        private readonly bool hasLegacySupportGrip;

        public CharacterAimWeaponRig(
            Transform muzzle,
            Transform rightHand,
            Transform weaponRoot,
            bool hasSupportGrip,
            Vector3 supportGripLocalPosition,
            Quaternion supportGripLocalRotation)
        {
            Muzzle = muzzle;
            RightHand = rightHand;
            WeaponRoot = weaponRoot;
            Mode = hasSupportGrip
                ? CharacterAimRigMode.PrimaryAndSupportHands
                : CharacterAimRigMode.PrimaryHandOnly;
            SupportGrip = null;
            hasLegacySupportGrip = hasSupportGrip;
            SupportGripLocalPosition = supportGripLocalPosition;
            SupportGripLocalRotation = supportGripLocalRotation;
        }

        public CharacterAimWeaponRig(
            Transform muzzle,
            Transform rightHand,
            Transform weaponRoot,
            CharacterAimRigMode mode,
            Transform supportGrip = null)
        {
            Muzzle = muzzle;
            RightHand = rightHand;
            WeaponRoot = weaponRoot;
            Mode = mode;
            SupportGrip = supportGrip;
            hasLegacySupportGrip = false;
            SupportGripLocalPosition = Vector3.zero;
            SupportGripLocalRotation = Quaternion.identity;
        }

        public Transform Muzzle { get; }
        public Transform RightHand { get; }
        public Transform WeaponRoot { get; }
        public CharacterAimRigMode Mode { get; }
        public Transform SupportGrip { get; }
        public bool HasSupportGrip => SupportGrip != null || hasLegacySupportGrip;
        public Vector3 SupportGripLocalPosition { get; }
        public Quaternion SupportGripLocalRotation { get; }
        public bool IsValid => Muzzle != null && RightHand != null;

        public CharacterAimRigMode ResolveEffectiveMode()
        {
            if (!IsValid || Mode == CharacterAimRigMode.Disabled)
                return CharacterAimRigMode.Disabled;

            return Mode == CharacterAimRigMode.PrimaryAndSupportHands
                && HasSupportGrip
                && WeaponRoot != null
                    ? CharacterAimRigMode.PrimaryAndSupportHands
                    : CharacterAimRigMode.PrimaryHandOnly;
        }
    }
}
