using Easy.Character.Animation;
using Easy.Character.CameraSystem;
using Easy.Character.Editor;
using Easy.Character.InputSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Easy.Character.Tests
{
    public sealed class CharacterPackageTests
    {
        [Test]
        public void MotorJump_UsesHeightAndTimeToApex()
        {
            float gravity = CharacterMotor.CalculateJumpGravity(1.2f, 0.3f);
            float speed = CharacterMotor.CalculateJumpSpeed(1.2f, gravity);

            Assert.AreEqual(-26.666666f, gravity, 0.0001f);
            Assert.AreEqual(8f, speed, 0.0001f);
            Assert.AreEqual(0.3f, speed / Mathf.Abs(gravity), 0.0001f);
        }

        [Test]
        public void MotorVelocity_AcceleratesWithoutOvershooting()
        {
            Vector3 velocity = CharacterMotor.AdvancePlanarVelocity(
                Vector3.zero,
                new Vector3(0f, 0f, 4f),
                10f,
                0.1f);

            Assert.AreEqual(new Vector3(0f, 0f, 1f), velocity);
        }

        [Test]
        public void MotorSpeed_UsesPostureAndExplicitOverride()
        {
            CharacterMotorSettings settings = CharacterMotorSettings.Default;

            Assert.AreEqual(
                2.5f,
                CharacterMotor.CalculateMovementSpeed(
                    settings,
                    new CharacterMotorCommand(Vector3.forward, 1f)));
            Assert.AreEqual(
                8.125f,
                CharacterMotor.CalculateMovementSpeed(
                    settings,
                    new CharacterMotorCommand(Vector3.forward, 1f, sprintHeld: true)));
            Assert.AreEqual(
                1.375f,
                CharacterMotor.CalculateMovementSpeed(
                    settings,
                    new CharacterMotorCommand(Vector3.forward, 1f, crouched: true)));
            Assert.AreEqual(
                6f,
                CharacterMotor.CalculateMovementSpeed(
                    settings,
                    new CharacterMotorCommand(Vector3.forward, 1f, speedOverride: 6f)));
        }

        [Test]
        public void MotorFacingPolicy_PreservesLegacyCommandsAndSupportsExplicitDirection()
        {
            CharacterMotorCommand legacyMovement = new(
                Vector3.right,
                1f,
                faceMovement: true);
            CharacterMotorCommand legacyPreserve = new(
                Vector3.right,
                1f,
                faceMovement: false);
            CharacterMotorCommand explicitFacing = new(
                Vector3.left,
                1f,
                CharacterMotorFacingMode.ExplicitDirection,
                new Vector3(0f, 4f, 2f));
            CharacterMotorCommand verticalFacing = new(
                Vector3.left,
                1f,
                CharacterMotorFacingMode.ExplicitDirection,
                Vector3.up);
            CharacterMotorCommand zeroFacing = new(
                Vector3.left,
                1f,
                CharacterMotorFacingMode.ExplicitDirection,
                Vector3.zero);

            Assert.IsTrue(legacyMovement.FaceMovement);
            Assert.AreEqual(
                CharacterMotorFacingMode.MovementDirection,
                legacyMovement.FacingMode);
            Assert.AreEqual(
                Vector3.right,
                CharacterMotor.CalculateFacingDirection(
                    legacyMovement,
                    legacyMovement.WorldDirection));
            Assert.IsFalse(legacyPreserve.FaceMovement);
            Assert.AreEqual(
                CharacterMotorFacingMode.Preserve,
                legacyPreserve.FacingMode);
            Assert.AreEqual(
                Vector3.zero,
                CharacterMotor.CalculateFacingDirection(
                    legacyPreserve,
                    legacyPreserve.WorldDirection));
            Assert.AreEqual(
                Vector3.forward,
                CharacterMotor.CalculateFacingDirection(
                    explicitFacing,
                    explicitFacing.WorldDirection));
            Assert.AreEqual(
                Vector3.zero,
                CharacterMotor.CalculateFacingDirection(
                    verticalFacing,
                    verticalFacing.WorldDirection));
            Assert.AreEqual(
                Vector3.zero,
                CharacterMotor.CalculateFacingDirection(
                    zeroFacing,
                    zeroFacing.WorldDirection));
        }

        [Test]
        public void ExplicitFacingRotation_AcceleratesBrakesAndDoesNotOvershoot()
        {
            float angle = 90f;
            float speed = 0f;
            float previousSpeed = 0f;
            bool accelerated = false;
            bool decelerated = false;
            for (int i = 0; i < 240 && angle > 0.001f; i++)
            {
                float delta = CharacterMotor.AdvanceExplicitFacingRotation(
                    angle,
                    speed,
                    480f,
                    720f,
                    1080f,
                    1f / 60f,
                    out speed);
                Assert.GreaterOrEqual(delta, 0f);
                angle -= delta;
                accelerated |= speed > previousSpeed + 0.001f;
                decelerated |= speed < previousSpeed - 0.001f;
                previousSpeed = speed;
            }

            Assert.IsTrue(accelerated);
            Assert.IsTrue(decelerated);
            Assert.AreEqual(0f, angle, 0.001f);
            Assert.AreEqual(0f, speed, 0.001f);
        }

        [Test]
        public void ExplicitFacingRotation_BrakesBeforeReversingDirection()
        {
            float delta = CharacterMotor.AdvanceExplicitFacingRotation(
                -90f,
                120f,
                480f,
                720f,
                1080f,
                0.05f,
                out float speed);

            Assert.Greater(speed, 0f);
            Assert.Less(speed, 120f);
            Assert.Greater(delta, 0f);
        }

        [Test]
        public void Motor_SuspendsAndResumesExternalOwnership()
        {
            GameObject character = new("Motor Character");
            try
            {
                CharacterMotor motor = character.AddComponent<CharacterMotor>();
                motor.ConfigureRuntime(
                    CharacterMotorSettings.Default,
                    CharacterUpdateMode.Manual,
                    accelerate: true);

                motor.Suspend(disableController: true);
                Assert.IsTrue(motor.IsSuspended);
                Assert.IsFalse(motor.Controller.enabled);
                Assert.IsTrue(motor.State.IsSuspended);

                motor.Resume(enableController: true);
                Assert.IsFalse(motor.IsSuspended);
                Assert.IsTrue(motor.Controller.enabled);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void InputDirection_UsesCameraPlanarBasis()
        {
            GameObject referenceObject = new("Input Reference");
            try
            {
                referenceObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3 direction =
                    InputSystemCharacterInputSource.CalculateCameraRelativeDirection(
                        Vector2.up,
                        referenceObject.transform);

                Assert.AreEqual(Vector3.right.x, direction.x, 0.0001f);
                Assert.AreEqual(Vector3.right.z, direction.z, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(referenceObject);
            }
        }

        [Test]
        public void CustomCommandSource_UsesOnlyPackageContracts()
        {
            ICharacterMotorCommandSource motorSource = new TestCommandSource();
            ICharacterCameraCommandSource cameraSource = (ICharacterCameraCommandSource)motorSource;

            Assert.IsTrue(motorSource.TryGetMotorCommand(out CharacterMotorCommand motor));
            Assert.AreEqual(Vector3.forward, motor.WorldDirection);
            Assert.IsTrue(cameraSource.TryGetCameraCommand(out CharacterCameraCommand camera));
            Assert.AreEqual(new Vector2(1f, -1f), camera.Look);
        }

        [Test]
        public void CameraBlend_IsStableAtZeroAndConverges()
        {
            Assert.AreEqual(0f, CharacterCameraRig.CalculateSmoothedBlend(12f, 0f));
            float blend = CharacterCameraRig.CalculateSmoothedBlend(12f, 0.1f);
            Assert.Greater(blend, 0f);
            Assert.Less(blend, 1f);
        }

        [Test]
        public void CameraZoom_ClampsDistanceAndPreservesDirection()
        {
            Vector3 offset = new(0f, 10f, -8f);
            Vector3 zoomed = CharacterCameraRig.CalculateZoomedOffset(
                offset,
                offset,
                1000f,
                8f,
                5f,
                18f);

            Assert.AreEqual(5f, zoomed.magnitude, 0.0001f);
            Assert.Greater(Vector3.Dot(offset.normalized, zoomed.normalized), 0.999f);
        }

        [Test]
        public void AnimationCadence_FollowsMovementSpeed()
        {
            Assert.AreEqual(
                1f,
                CharacterAnimatorDriver.CalculatePlaybackMultiplier(
                    0f, 0.15f, 0.8f, 2.5f, 8f, 1.35f));
            Assert.AreEqual(
                1f,
                CharacterAnimatorDriver.CalculatePlaybackMultiplier(
                    2.5f, 0.15f, 0.8f, 2.5f, 8f, 1.35f),
                0.0001f);
            Assert.AreEqual(
                1.35f,
                CharacterAnimatorDriver.CalculatePlaybackMultiplier(
                    12f, 0.15f, 0.8f, 2.5f, 8f, 1.35f),
                0.0001f);
        }

        [Test]
        public void SprintLayerWeight_BlendsFromNormalToSprintReferenceSpeed()
        {
            Assert.AreEqual(
                0f,
                CharacterAnimatorDriver.CalculateSprintLayerWeight(
                    8f, false, 2.5f, 8f),
                0.0001f);
            Assert.AreEqual(
                0f,
                CharacterAnimatorDriver.CalculateSprintLayerWeight(
                    2.5f, true, 2.5f, 8f),
                0.0001f);
            Assert.AreEqual(
                0.5f,
                CharacterAnimatorDriver.CalculateSprintLayerWeight(
                    5.25f, true, 2.5f, 8f),
                0.0001f);
            Assert.AreEqual(
                1f,
                CharacterAnimatorDriver.CalculateSprintLayerWeight(
                    20f, true, 2.5f, 8f),
                0.0001f);
        }

        [Test]
        public void AnimationLayerBlend_ConvergesWithoutRestarting()
        {
            float weight = CharacterAnimatorDriver.AdvanceLayerWeight(0f, 1f, 0.1f, 0.05f);
            Assert.AreEqual(0.5f, weight, 0.0001f);
            weight = CharacterAnimatorDriver.AdvanceLayerWeight(weight, 0f, 0.1f, 0.025f);
            Assert.AreEqual(0.25f, weight, 0.0001f);
        }

        [Test]
        public void AimAngles_PreserveCameraPitchAndSignedYaw()
        {
            CharacterAimController.CalculateLocalAngles(
                Quaternion.identity,
                Vector3.zero,
                new Vector3(1f, 1f, 1f),
                out float yaw,
                out float pitch);

            Assert.AreEqual(45f, yaw, 0.001f);
            Assert.Greater(pitch, 0f);
            CharacterAimController.CalculateLocalAngles(
                Quaternion.identity,
                Vector3.zero,
                new Vector3(-1f, -1f, 1f),
                out yaw,
                out pitch);
            Assert.AreEqual(-45f, yaw, 0.001f);
            Assert.Less(pitch, 0f);
        }

        [Test]
        public void AimRootTurn_UsesTorsoDeadZoneAndStopAngle()
        {
            CharacterAimSettings settings = CharacterAimSettings.Default;
            Assert.AreEqual(
                0f,
                CharacterAimController.CalculateRootTurnDelta(
                    40f,
                    false,
                    settings,
                    100f,
                    out bool turning));
            Assert.IsFalse(turning);

            Assert.AreEqual(
                35f,
                CharacterAimController.CalculateRootTurnDelta(
                    70f,
                    false,
                    settings,
                    100f,
                    out turning),
                0.001f);
            Assert.IsFalse(turning);
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 120f)]
        public void AimRootTurn_AcceleratesAndSettlesAtResidualYaw(float deltaTime)
        {
            CharacterAimSettings settings = CharacterAimSettings.Default;
            float yaw = 100f;
            float speed = 0f;
            bool turning = false;
            float maximumObservedSpeed = 0f;
            for (int i = 0; i < 600 && (yaw > settings.RootTurnStopYaw + 0.001f || turning); i++)
            {
                float delta = CharacterAimController.AdvanceRootTurn(
                    yaw,
                    turning,
                    speed,
                    settings,
                    deltaTime,
                    out speed,
                    out turning);
                yaw -= delta;
                maximumObservedSpeed = Mathf.Max(maximumObservedSpeed, Mathf.Abs(speed));
            }

            Assert.AreEqual(settings.RootTurnStopYaw, yaw, 0.001f);
            Assert.AreEqual(0f, speed, 0.001f);
            Assert.IsFalse(turning);
            Assert.LessOrEqual(
                maximumObservedSpeed,
                settings.RootTurnDegreesPerSecond + 0.001f);
        }

        [Test]
        public void AimRootTurn_ReversalBrakesBeforeChangingDirection()
        {
            float delta = CharacterAimController.AdvanceRootTurn(
                -90f,
                true,
                120f,
                CharacterAimSettings.Default,
                0.05f,
                out float speed,
                out bool turning);

            Assert.IsTrue(turning);
            Assert.Greater(speed, 0f);
            Assert.Less(speed, 120f);
            Assert.Greater(delta, 0f);
        }

        [Test]
        public void AimCommand_PreservesIndependentBodyAndHandScales()
        {
            CharacterAimCommand command = new(
                Vector3.forward,
                isAiming: true,
                bodyWeightScale: 0.35f,
                handsWeightScale: 1f);
            CharacterAimState state = new(
                supported: true,
                active: true,
                Vector3.forward,
                yaw: 0f,
                pitch: 0f,
                weight: 1f,
                rootTurning: false,
                command.BodyWeightScale,
                command.HandsWeightScale);

            Assert.AreEqual(0.35f, state.BodyWeightScale, 0.0001f);
            Assert.AreEqual(1f, state.HandsWeightScale, 0.0001f);
        }

        [Test]
        public void AimSettings_ClampPitchYawAndInvalidCloseTargetsSafely()
        {
            CharacterAimSettings settings = CharacterAimSettings.Default;
            Assert.AreEqual(1f, settings.RightHandPositionWeight);
            Assert.AreEqual(0.8f, settings.RightElbowHintWeight);
            Assert.AreEqual(0f, settings.RightHandRotationWeight);
            Assert.AreEqual(0.8f, settings.SupportElbowHintWeight);
            CharacterAimController.CalculateLocalAngles(
                Quaternion.identity,
                Vector3.zero,
                Vector3.up,
                out float yaw,
                out float pitch);

            Assert.AreEqual(0f, yaw, 0.001f);
            Assert.AreEqual(90f, pitch, 0.001f);
            Assert.AreEqual(settings.MaximumUpPitch, Mathf.Clamp(
                pitch,
                -settings.MaximumDownPitch,
                settings.MaximumUpPitch));

            CharacterAimController.CalculateLocalAngles(
                Quaternion.identity,
                Vector3.one,
                Vector3.one,
                out yaw,
                out pitch);
            Assert.AreEqual(0f, yaw);
            Assert.AreEqual(0f, pitch);
            Assert.IsFalse(float.IsNaN(yaw));
            Assert.IsFalse(float.IsNaN(pitch));
        }

        [Test]
        public void AimHands_AlignMuzzleAndPredictSupportGrip()
        {
            Quaternion handRotation = Quaternion.Euler(8f, 20f, -6f);
            Quaternion handToMuzzle = Quaternion.Euler(3f, -12f, 5f);
            Quaternion muzzleRotation = handRotation * handToMuzzle;
            Quaternion desiredMuzzleRotation = Quaternion.LookRotation(
                new Vector3(0.4f, 0.35f, 1f).normalized,
                Vector3.up);

            Quaternion desiredHandRotation =
                CharacterAimIkDriver.CalculateDesiredHandRotation(
                    handRotation,
                    muzzleRotation,
                    desiredMuzzleRotation);
            Quaternion resultingMuzzleRotation = desiredHandRotation * handToMuzzle;
            Assert.Less(
                Quaternion.Angle(desiredMuzzleRotation, resultingMuzzleRotation),
                0.001f);

            Vector3 handPosition = new(1f, 1.2f, -0.5f);
            Vector3 handLocalGrip = new(-0.2f, 0.05f, 0.4f);
            Vector3 predictedGrip = CharacterAimIkDriver.CalculatePredictedGripPosition(
                handPosition,
                Vector3.one,
                desiredHandRotation,
                handLocalGrip);
            Vector3 expectedGrip = Matrix4x4.TRS(
                handPosition,
                desiredHandRotation,
                Vector3.one).MultiplyPoint3x4(handLocalGrip);
            Assert.AreEqual(expectedGrip.x, predictedGrip.x, 0.0001f);
            Assert.AreEqual(expectedGrip.y, predictedGrip.y, 0.0001f);
            Assert.AreEqual(expectedGrip.z, predictedGrip.z, 0.0001f);
        }

        [Test]
        public void AimHands_ForwardAlignmentPreservesBarrelDirectionWithoutForcingRoll()
        {
            Quaternion currentHand = Quaternion.Euler(12f, 24f, -18f);
            Quaternion handToMuzzle = Quaternion.Euler(70f, 8f, 35f);
            Vector3 desiredForward = new Vector3(0.5f, -0.1f, 1f).normalized;

            Quaternion desiredHand =
                CharacterAimIkDriver.CalculateForwardAlignedHandRotation(
                    currentHand,
                    handToMuzzle,
                    desiredForward);
            Vector3 resultingForward = desiredHand
                * handToMuzzle
                * Vector3.forward;

            Assert.Greater(Vector3.Dot(resultingForward, desiredForward), 0.99999f);
        }

        [Test]
        public void AimWeaponRoot_ForwardCorrectionMatchesExactTargetDirection()
        {
            Vector3 currentForward = Quaternion.Euler(45f, 20f, 0f)
                * Vector3.forward;
            Vector3 desiredForward = new Vector3(-0.2f, 0.15f, 1f).normalized;

            Quaternion correction = CharacterAimIkDriver.CalculateForwardCorrection(
                currentForward,
                desiredForward);
            Vector3 correctedForward = correction * currentForward;

            Assert.Greater(Vector3.Dot(
                correctedForward.normalized,
                desiredForward), 0.99999f);
        }

        [Test]
        public void AimRightArm_RotatesHandAndElbowAroundUpperArmPivot()
        {
            Vector3 upperArm = new(0.25f, 1.5f, 0f);
            Vector3 hand = upperArm + new Vector3(0f, 0.5f, 0.5f);
            Vector3 elbow = upperArm + new Vector3(0f, 0.25f, 0.25f);
            Vector3 currentForward = new Vector3(0f, 1f, 1f).normalized;
            Quaternion correction = CharacterAimIkDriver.CalculateForwardCorrection(
                currentForward,
                Vector3.forward);

            Vector3 handTarget = CharacterAimIkDriver.CalculateRotatedLimbTarget(
                upperArm,
                hand,
                correction);
            Vector3 elbowTarget = CharacterAimIkDriver.CalculateRotatedLimbTarget(
                upperArm,
                elbow,
                correction);

            Assert.AreEqual(
                Vector3.Distance(upperArm, hand),
                Vector3.Distance(upperArm, handTarget),
                0.0001f);
            Assert.AreEqual(
                Vector3.Distance(upperArm, elbow),
                Vector3.Distance(upperArm, elbowTarget),
                0.0001f);
            Assert.AreEqual(upperArm.y, handTarget.y, 0.0001f);
            Assert.AreEqual(upperArm.y, elbowTarget.y, 0.0001f);
            Assert.Greater(handTarget.z, hand.z);
        }

        [Test]
        public void AimRigMode_MissingSupportGripFallsBackToPrimaryHand()
        {
            GameObject root = new("Aim Rig Root");
            GameObject muzzle = new("Muzzle");
            GameObject rightHand = new("Right Hand");
            try
            {
                muzzle.transform.SetParent(root.transform);
                rightHand.transform.SetParent(root.transform);
                CharacterAimWeaponRig primary = new(
                    muzzle.transform,
                    rightHand.transform,
                    root.transform,
                    CharacterAimRigMode.PrimaryHandOnly);
                CharacterAimWeaponRig missingSupport = new(
                    muzzle.transform,
                    rightHand.transform,
                    root.transform,
                    CharacterAimRigMode.PrimaryAndSupportHands);

                Assert.AreEqual(
                    CharacterAimRigMode.PrimaryHandOnly,
                    primary.ResolveEffectiveMode());
                Assert.AreEqual(
                    CharacterAimRigMode.PrimaryHandOnly,
                    missingSupport.ResolveEffectiveMode());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AimRigMode_SupportMarkerEnablesBothHands()
        {
            GameObject root = new("Aim Rig Root");
            GameObject muzzle = new("Muzzle");
            GameObject rightHand = new("Right Hand");
            GameObject supportGrip = new("Support Grip");
            try
            {
                muzzle.transform.SetParent(root.transform);
                rightHand.transform.SetParent(root.transform);
                supportGrip.transform.SetParent(root.transform);
                CharacterAimWeaponRig rig = new(
                    muzzle.transform,
                    rightHand.transform,
                    root.transform,
                    CharacterAimRigMode.PrimaryAndSupportHands,
                    supportGrip.transform);

                Assert.IsTrue(rig.HasSupportGrip);
                Assert.AreSame(supportGrip.transform, rig.SupportGrip);
                Assert.AreEqual(
                    CharacterAimRigMode.PrimaryAndSupportHands,
                    rig.ResolveEffectiveMode());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AimSupportElbow_RotatesHintTowardGripWithoutChangingReach()
        {
            Vector3 upperArm = new(-0.25f, 1.5f, 0f);
            Vector3 elbow = upperArm + new Vector3(-0.2f, -0.1f, 0.25f);
            Vector3 hand = upperArm + new Vector3(-0.35f, -0.2f, 0.5f);
            Vector3 grip = upperArm + new Vector3(-0.1f, 0.15f, 0.7f);

            Vector3 hint = CharacterAimIkDriver.CalculateLimbHintTarget(
                upperArm,
                elbow,
                hand,
                grip);

            Assert.AreEqual(
                Vector3.Distance(upperArm, elbow),
                Vector3.Distance(upperArm, hint),
                0.0001f);
            Assert.IsFalse(float.IsNaN(hint.x));
            Assert.IsFalse(float.IsNaN(hint.y));
            Assert.IsFalse(float.IsNaN(hint.z));
        }

        [Test]
        public void AimSupportGrip_FollowsPrimaryHandTranslationAndAimRotation()
        {
            Vector3 primaryHand = new(0.3f, 1.2f, 0.2f);
            Vector3 supportGrip = primaryHand + new Vector3(-0.25f, 0f, 0.4f);
            Vector3 displacement = new(0f, 0.1f, 0.15f);
            Quaternion correction = Quaternion.Euler(-30f, 15f, 0f);

            Vector3 predicted =
                CharacterAimIkDriver.CalculatePredictedSupportGripPosition(
                    primaryHand,
                    supportGrip,
                    displacement,
                    correction);
            Vector3 expected = primaryHand
                + displacement
                + correction * (supportGrip - primaryHand);

            Assert.AreEqual(expected.x, predicted.x, 0.0001f);
            Assert.AreEqual(expected.y, predicted.y, 0.0001f);
            Assert.AreEqual(expected.z, predicted.z, 0.0001f);
        }

        [Test]
        public void AnimationExtension_HighestActivePriorityWins()
        {
            ICharacterAnimationExtensionStateSource[] sources =
            {
                new TestAnimationExtension(10, true, false),
                new TestAnimationExtension(50, false, true),
                new TestAnimationExtension(100, true, true, active: false),
            };

            Assert.IsTrue(CharacterAnimatorDriver.TrySelectHighestPriorityExtensionState(
                sources,
                out CharacterAnimationExtensionState state));
            Assert.IsFalse(state.SuppressLocomotionLayers);
            Assert.IsTrue(state.ForceNormalPlayback);
        }

        [Test]
        public void RuntimeMathHotPaths_DoNotAllocateManagedMemory()
        {
            CharacterMotorSettings settings = CharacterMotorSettings.Default;
            CharacterMotorCommand command = new(Vector3.forward, 1f, sprintHeld: true);
            CharacterMotorCommand facingCommand = new(
                Vector3.left,
                1f,
                CharacterMotorFacingMode.ExplicitDirection,
                Vector3.forward);
            Vector3 velocity = Vector3.zero;
            float sink = CharacterMotor.CalculateMovementSpeed(settings, command);
            sink += CharacterMotor.CalculateFacingDirection(
                facingCommand,
                facingCommand.WorldDirection).z;
            sink += CharacterMotor.AdvanceExplicitFacingRotation(
                90f, 0f, 480f, 720f, 1080f, 0.016f, out _);
            sink += CharacterCameraRig.CalculateSmoothedBlend(12f, 0.016f);
            sink += CharacterAnimatorDriver.CalculatePlaybackMultiplier(
                4f, 0.15f, 0.8f, 2.5f, 8f, 1.35f);
            sink += CharacterAnimatorDriver.CalculateSprintLayerWeight(
                4f, true, 2.5f, 8f);
            CharacterAimController.CalculateLocalAngles(
                Quaternion.identity,
                Vector3.zero,
                new Vector3(1f, 1f, 1f),
                out float aimYaw,
                out float aimPitch);
            sink += aimYaw + aimPitch;
            sink += CharacterAimController.CalculateRootTurnDelta(
                70f,
                false,
                CharacterAimSettings.Default,
                12f,
                out _);
            sink += CharacterAimController.AdvanceRootTurn(
                70f,
                false,
                0f,
                CharacterAimSettings.Default,
                0.016f,
                out _,
                out _);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                velocity = CharacterMotor.AdvancePlanarVelocity(
                    velocity,
                    Vector3.forward * 4f,
                    12f,
                    0.016f);
                sink += CharacterMotor.CalculateMovementSpeed(settings, command);
                sink += CharacterMotor.AdvanceExplicitFacingRotation(
                    90f, 0f, 480f, 720f, 1080f, 0.016f, out _);
                sink += CharacterCameraRig.CalculateSmoothedBlend(12f, 0.016f);
                sink += CharacterAnimatorDriver.CalculatePlaybackMultiplier(
                    4f, 0.15f, 0.8f, 2.5f, 8f, 1.35f);
                sink += CharacterAnimatorDriver.CalculateSprintLayerWeight(
                    4f, true, 2.5f, 8f);
                CharacterAimController.CalculateLocalAngles(
                    Quaternion.identity,
                    Vector3.zero,
                    new Vector3(1f, 1f, 1f),
                    out aimYaw,
                    out aimPitch);
                sink += aimYaw + aimPitch;
                sink += CharacterAimController.CalculateRootTurnDelta(
                    70f,
                    false,
                    CharacterAimSettings.Default,
                    12f,
                    out _);
                sink += CharacterAimController.AdvanceRootTurn(
                    70f,
                    false,
                    0f,
                    CharacterAimSettings.Default,
                    0.016f,
                    out _,
                    out _);
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreNotEqual(float.MinValue, sink + velocity.x);
            Assert.AreEqual(before, after);
        }

        [Test]
        public void AnimatorBuilder_RepeatedBuildDoesNotDuplicateLayersOrParameters()
        {
            const string ControllerPath = "Assets/__EasyCharacterBuilderTest.controller";
            CharacterAnimationSet animationSet =
                ScriptableObject.CreateInstance<CharacterAnimationSet>();
            CharacterAnimationProfile profile =
                ScriptableObject.CreateInstance<CharacterAnimationProfile>();
            try
            {
                AnimatorController first = CharacterAnimatorControllerBuilder.Build(
                    animationSet,
                    profile,
                    ControllerPath);
                AnimatorControllerLayer[] reordered = first.layers;
                AnimatorControllerLayer body = reordered[6];
                reordered[6] = reordered[7];
                reordered[7] = body;
                first.layers = reordered;
                AnimatorController second = CharacterAnimatorControllerBuilder.Build(
                    animationSet,
                    profile,
                    ControllerPath);

                Assert.IsNotNull(first);
                Assert.AreSame(first, second);
                Assert.AreEqual(8, second.layers.Length);
                Assert.AreEqual(9, second.parameters.Length);
                Assert.AreEqual(
                    CharacterAimIkDriver.BodyLayerName,
                    second.layers[6].name);
                Assert.IsTrue(second.layers[6].iKPass);
                Assert.AreEqual(
                    CharacterAimIkDriver.HandsLayerName,
                    second.layers[7].name);
                Assert.IsTrue(second.layers[7].iKPass);
            }
            finally
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                Object.DestroyImmediate(animationSet);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PackageRuntimeSources_DependOnlyOnUnityAndCharacterAssemblies()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(CharacterMotor).Assembly);
            Assert.IsNotNull(package);

            // Works both as a standalone package and as a module folder of a larger package.
            string runtimeRoot = System.IO.Path.Combine(package.resolvedPath, "Character", "Runtime");
            if (!System.IO.Directory.Exists(runtimeRoot))
                runtimeRoot = System.IO.Path.Combine(package.resolvedPath, "Runtime");

            string[] sources = System.IO.Directory.GetFiles(
                runtimeRoot,
                "*.cs",
                System.IO.SearchOption.AllDirectories);
            Assert.IsNotEmpty(sources);
            for (int i = 0; i < sources.Length; i++)
            {
                string[] lines = System.IO.File.ReadAllLines(sources[i]);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex].Trim();
                    if (line.StartsWith("using Easy.", System.StringComparison.Ordinal))
                        StringAssert.StartsWith("using Easy.Character", line, sources[i]);
                }
            }

            System.Reflection.Assembly[] assemblies =
            {
                typeof(CharacterMotor).Assembly,
                typeof(InputSystemCharacterInputSource).Assembly,
                typeof(CharacterCameraRig).Assembly,
                typeof(CharacterAnimatorDriver).Assembly,
            };
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                System.Reflection.AssemblyName[] references =
                    assemblies[assemblyIndex].GetReferencedAssemblies();
                for (int referenceIndex = 0; referenceIndex < references.Length; referenceIndex++)
                {
                    string name = references[referenceIndex].Name;
                    bool allowed = name.StartsWith("Unity", System.StringComparison.Ordinal)
                        || name.StartsWith("System", System.StringComparison.Ordinal)
                        || name == "mscorlib"
                        || name == "netstandard"
                        || name.StartsWith("Easy.Character", System.StringComparison.Ordinal);
                    Assert.IsTrue(allowed, $"{assemblies[assemblyIndex].GetName().Name} references {name}.");
                }
            }
        }

        private sealed class TestAnimationExtension : ICharacterAnimationExtensionStateSource
        {
            private readonly CharacterAnimationExtensionState state;
            private readonly bool active;

            public TestAnimationExtension(
                int priority,
                bool suppressLocomotion,
                bool forceNormalPlayback,
                bool active = true)
            {
                Priority = priority;
                state = new CharacterAnimationExtensionState(
                    suppressLocomotion,
                    forceNormalPlayback);
                this.active = active;
            }

            public int Priority { get; }

            public bool TryGetAnimationExtensionState(
                out CharacterAnimationExtensionState extensionState)
            {
                extensionState = state;
                return active;
            }
        }

        private sealed class TestCommandSource :
            ICharacterMotorCommandSource,
            ICharacterCameraCommandSource
        {
            public bool TryGetMotorCommand(out CharacterMotorCommand command)
            {
                command = new CharacterMotorCommand(Vector3.forward, 1f);
                return true;
            }

            public bool TryGetCameraCommand(out CharacterCameraCommand command)
            {
                command = new CharacterCameraCommand(
                    new Vector2(1f, -1f),
                    0f,
                    0f,
                    0f,
                    orbitHeld: true);
                return true;
            }
        }
    }
}
