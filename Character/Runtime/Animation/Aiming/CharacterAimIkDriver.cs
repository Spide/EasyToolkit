using UnityEngine;

namespace Easy.Character.Animation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAimIkDriver : MonoBehaviour
    {
        public const string BodyLayerName = "Aim Body IK";
        public const string HandsLayerName = "Aim Hands IK";

        [SerializeField] private Animator animator;

        private Transform characterRoot;
        private Transform aimOrigin;
        private CharacterAimSettings settings = CharacterAimSettings.Default;
        private CharacterAimState state;
        private CharacterAimWeaponRig weaponRig;
        private Quaternion handToMuzzleRotation = Quaternion.identity;
        private CharacterAimRigMode effectiveRigMode;
        private Quaternion weaponRootLocalRotation = Quaternion.identity;
        private bool hasCachedWeaponRotation;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private int bodyLayer = -1;
        private int handsLayer = -1;

        public Vector3 AimOrigin => aimOrigin != null
            ? aimOrigin.position
            : characterRoot != null
                ? characterRoot.position + characterRoot.up * 1.25f
                : transform.position;
        public CharacterAimRigMode EffectiveRigMode => effectiveRigMode;

        private void Awake()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            ResolveAnimatorData();
        }

        private void OnEnable()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            ResolveAnimatorData();
        }

        private void OnDisable()
        {
            state = default;
            ClearWeapon();
        }

        public void Configure(
            Animator targetAnimator,
            Transform targetRoot,
            CharacterAimSettings aimSettings)
        {
            animator = targetAnimator;
            characterRoot = targetRoot != null ? targetRoot : transform.root;
            settings = aimSettings.Clamp();
            ResolveAnimatorData();
        }

        public void SetAimState(in CharacterAimState aimState)
        {
            state = aimState;
        }

        public void BindWeapon(in CharacterAimWeaponRig rig)
        {
            RestoreWeaponRotation();
            weaponRig = rig;
            handToMuzzleRotation = Quaternion.identity;
            effectiveRigMode = rig.ResolveEffectiveMode();
            weaponRootLocalRotation = Quaternion.identity;
            hasCachedWeaponRotation = false;
            if (!rig.IsValid)
                return;

            handToMuzzleRotation = Quaternion.Inverse(rig.RightHand.rotation)
                * rig.Muzzle.rotation;
            if (rig.WeaponRoot == null)
                return;

            weaponRootLocalRotation = rig.WeaponRoot.localRotation;
            hasCachedWeaponRotation = true;
        }

        public void ClearWeapon()
        {
            RestoreWeaponRotation();
            weaponRig = default;
            handToMuzzleRotation = Quaternion.identity;
            effectiveRigMode = CharacterAimRigMode.Disabled;
            weaponRootLocalRotation = Quaternion.identity;
            hasCachedWeaponRotation = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman)
                return;

            if (layerIndex == bodyLayer)
                ApplyBodyAim();
            else if (layerIndex == handsLayer)
                ApplyHandAim();
        }

        private void ApplyBodyAim()
        {
            float weight = state.Weight * state.BodyWeightScale;
            animator.SetLookAtWeight(
                weight,
                settings.BodyWeight,
                settings.HeadWeight,
                eyesWeight: 0f,
                clampWeight: settings.LookClampWeight);
            if (weight <= 0f)
                return;

            Vector3 direction = ResolvePoseDirection();
            animator.SetLookAtPosition(AimOrigin + direction * 10f);
        }

        private void ApplyHandAim()
        {
            float weight = state.Weight * state.HandsWeightScale;
            if (weight <= 0f
                || !weaponRig.IsValid
                || effectiveRigMode == CharacterAimRigMode.Disabled)
            {
                RestoreWeaponRotation();
                ClearHandWeights();
                return;
            }

            Transform rightHand = weaponRig.RightHand;
            RestoreWeaponRotation();
            Vector3 direction = ResolveWeaponDirection();
            Quaternion aimCorrection = CalculateForwardCorrection(
                weaponRig.Muzzle.forward,
                direction);
            Vector3 rightHandDisplacement = ApplyRightArmAim(
                rightHand,
                aimCorrection,
                weight);
            bool preserveWeaponRootRotation = hasCachedWeaponRotation
                && weaponRig.WeaponRoot != null;
            Quaternion desiredHandRotation = rightHand.rotation;
            float rightHandWeight = 0f;
            if (!preserveWeaponRootRotation)
            {
                desiredHandRotation = CalculateForwardAlignedHandRotation(
                    rightHand.rotation,
                    handToMuzzleRotation,
                    direction);
                rightHandWeight = weight * settings.RightHandRotationWeight;
            }
            animator.SetIKRotationWeight(
                AvatarIKGoal.RightHand,
                rightHandWeight);
            animator.SetIKRotation(AvatarIKGoal.RightHand, desiredHandRotation);

            bool useSupportHand = effectiveRigMode
                == CharacterAimRigMode.PrimaryAndSupportHands;
            float supportPositionWeight = useSupportHand
                ? weight * settings.SupportHandPositionWeight
                : 0f;
            float supportRotationWeight = useSupportHand
                ? weight * settings.SupportHandRotationWeight
                : 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, supportPositionWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, supportRotationWeight);
            if (!useSupportHand)
            {
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                return;
            }

            ResolveSupportGripPose(
                rightHand,
                aimCorrection,
                rightHandDisplacement,
                out Vector3 targetGripPosition,
                out Quaternion targetGripRotation);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, targetGripPosition);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, targetGripRotation);
            ApplySupportElbowHint(targetGripPosition, weight);
        }

        private void ResolveSupportGripPose(
            Transform rightHand,
            Quaternion aimCorrection,
            Vector3 rightHandDisplacement,
            out Vector3 position,
            out Quaternion rotation)
        {
            Vector3 currentPosition;
            Quaternion currentRotation;
            if (weaponRig.SupportGrip != null)
            {
                currentPosition = weaponRig.SupportGrip.position;
                currentRotation = weaponRig.SupportGrip.rotation;
            }
            else
            {
                Transform weaponRoot = weaponRig.WeaponRoot;
                currentPosition = weaponRoot.TransformPoint(
                    weaponRig.SupportGripLocalPosition);
                currentRotation = weaponRoot.rotation
                    * weaponRig.SupportGripLocalRotation;
            }

            position = CalculatePredictedSupportGripPosition(
                rightHand.position,
                currentPosition,
                rightHandDisplacement,
                aimCorrection);
            rotation = aimCorrection * currentRotation;
        }

        private void ApplySupportElbowHint(Vector3 supportTarget, float weight)
        {
            bool canHint = leftUpperArm != null
                && leftLowerArm != null
                && leftHand != null;
            float hintWeight = canHint
                ? weight * settings.SupportElbowHintWeight
                : 0f;
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, hintWeight);
            if (!canHint)
                return;

            animator.SetIKHintPosition(
                AvatarIKHint.LeftElbow,
                CalculateLimbHintTarget(
                    leftUpperArm.position,
                    leftLowerArm.position,
                    leftHand.position,
                    supportTarget));
        }

        private Vector3 ApplyRightArmAim(
            Transform rightHand,
            Quaternion aimCorrection,
            float weight)
        {
            bool canPositionArm = rightUpperArm != null && rightHand != null;
            float handPositionWeight = canPositionArm
                ? weight * settings.RightHandPositionWeight
                : 0f;
            animator.SetIKPositionWeight(
                AvatarIKGoal.RightHand,
                handPositionWeight);
            Vector3 handDisplacement = Vector3.zero;
            if (canPositionArm)
            {
                Vector3 handTarget = CalculateRotatedLimbTarget(
                    rightUpperArm.position,
                    rightHand.position,
                    aimCorrection);
                animator.SetIKPosition(
                    AvatarIKGoal.RightHand,
                    handTarget);
                handDisplacement = (handTarget - rightHand.position)
                    * Mathf.Clamp01(handPositionWeight);
            }

            bool canHintElbow = canPositionArm && rightLowerArm != null;
            float elbowHintWeight = canHintElbow
                ? weight * settings.RightElbowHintWeight
                : 0f;
            animator.SetIKHintPositionWeight(
                AvatarIKHint.RightElbow,
                elbowHintWeight);
            if (canHintElbow)
            {
                animator.SetIKHintPosition(
                    AvatarIKHint.RightElbow,
                    CalculateRotatedLimbTarget(
                        rightUpperArm.position,
                        rightLowerArm.position,
                        aimCorrection));
            }

            return handDisplacement;
        }

        private Vector3 ResolveWeaponDirection()
        {
            Vector3 direction = state.TargetWorldPosition - weaponRig.Muzzle.position;
            float sqrMagnitude = direction.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.000001f)
                return ResolvePoseDirection();

            return direction / Mathf.Sqrt(sqrMagnitude);
        }

        private void RestoreWeaponRotation()
        {
            if (hasCachedWeaponRotation && weaponRig.WeaponRoot != null)
                weaponRig.WeaponRoot.localRotation = weaponRootLocalRotation;
        }

        public static Quaternion CalculateDesiredHandRotation(
            Quaternion currentHandRotation,
            Quaternion currentMuzzleRotation,
            Quaternion desiredMuzzleRotation)
        {
            Quaternion handToMuzzle = Quaternion.Inverse(currentHandRotation)
                * currentMuzzleRotation;
            return CalculateDesiredHandRotation(
                handToMuzzle,
                desiredMuzzleRotation);
        }

        public static Quaternion CalculateDesiredHandRotation(
            Quaternion handToMuzzleRotation,
            Quaternion desiredMuzzleRotation)
        {
            return desiredMuzzleRotation * Quaternion.Inverse(handToMuzzleRotation);
        }

        public static Quaternion CalculateForwardAlignedHandRotation(
            Quaternion currentHandRotation,
            Quaternion handToMuzzleRotation,
            Vector3 desiredMuzzleForward)
        {
            float sqrMagnitude = desiredMuzzleForward.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.000001f)
                return currentHandRotation;

            Vector3 desiredForward = desiredMuzzleForward / Mathf.Sqrt(sqrMagnitude);
            Quaternion currentMuzzleRotation = currentHandRotation
                * handToMuzzleRotation;
            Vector3 currentForward = currentMuzzleRotation * Vector3.forward;
            Quaternion correction = CalculateForwardCorrection(
                currentForward,
                desiredForward);
            return correction * currentHandRotation;
        }

        public static Quaternion CalculateForwardCorrection(
            Vector3 currentForward,
            Vector3 desiredForward)
        {
            float currentSqrMagnitude = currentForward.sqrMagnitude;
            float desiredSqrMagnitude = desiredForward.sqrMagnitude;
            if (!float.IsFinite(currentSqrMagnitude)
                || !float.IsFinite(desiredSqrMagnitude)
                || currentSqrMagnitude <= 0.000001f
                || desiredSqrMagnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.FromToRotation(
                currentForward / Mathf.Sqrt(currentSqrMagnitude),
                desiredForward / Mathf.Sqrt(desiredSqrMagnitude));
        }

        public static Vector3 CalculateRotatedLimbTarget(
            Vector3 pivot,
            Vector3 currentPosition,
            Quaternion aimCorrection)
        {
            return pivot + aimCorrection * (currentPosition - pivot);
        }

        public static Vector3 CalculateLimbHintTarget(
            Vector3 limbRoot,
            Vector3 currentHint,
            Vector3 currentEffector,
            Vector3 targetEffector)
        {
            Quaternion correction = CalculateForwardCorrection(
                currentEffector - limbRoot,
                targetEffector - limbRoot);
            return CalculateRotatedLimbTarget(
                limbRoot,
                currentHint,
                correction);
        }

        public static Vector3 CalculatePredictedSupportGripPosition(
            Vector3 currentPrimaryHand,
            Vector3 currentSupportGrip,
            Vector3 primaryHandDisplacement,
            Quaternion aimCorrection)
        {
            return currentPrimaryHand
                + primaryHandDisplacement
                + aimCorrection * (currentSupportGrip - currentPrimaryHand);
        }

        public static Vector3 CalculatePredictedGripPosition(
            Vector3 currentHandPosition,
            Vector3 currentHandScale,
            Quaternion desiredHandRotation,
            Vector3 handLocalGrip)
        {
            Matrix4x4 predictedHand = Matrix4x4.TRS(
                currentHandPosition,
                desiredHandRotation,
                currentHandScale);
            return predictedHand.MultiplyPoint3x4(handLocalGrip);
        }

        private void ClearHandWeights()
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
        }

        private Vector3 ResolvePoseDirection()
        {
            Transform root = characterRoot != null ? characterRoot : transform.root;
            Vector3 up = root.up;
            Vector3 forward = Quaternion.AngleAxis(state.Yaw, up) * root.forward;
            Vector3 right = Vector3.Cross(up, forward);
            float rightSqrMagnitude = right.sqrMagnitude;
            if (rightSqrMagnitude <= 0.000001f)
                right = root.right;
            else
                right /= Mathf.Sqrt(rightSqrMagnitude);

            return Quaternion.AngleAxis(-state.Pitch, right) * forward;
        }

        private void ResolveAnimatorData()
        {
            if (animator == null)
                return;

            bodyLayer = animator.GetLayerIndex(BodyLayerName);
            handsLayer = animator.GetLayerIndex(HandsLayerName);
            if (!animator.isHuman)
            {
                aimOrigin = null;
                rightUpperArm = null;
                rightLowerArm = null;
                leftUpperArm = null;
                leftLowerArm = null;
                leftHand = null;
                return;
            }

            aimOrigin = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            aimOrigin = aimOrigin != null
                ? aimOrigin
                : animator.GetBoneTransform(HumanBodyBones.Chest);
            aimOrigin = aimOrigin != null
                ? aimOrigin
                : animator.GetBoneTransform(HumanBodyBones.Spine);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }
    }
}
