using System.Collections;
using Easy.Character.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Easy.Character.Tests
{
    public sealed class CharacterPackagePlayModeTests
    {
        [UnityTest]
        public IEnumerator Motor_MovesThenCancelsWithoutResidualPlanarVelocity()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject character = new("Character Motor Test");
            try
            {
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(20f, 1f, 20f);
                UnityEngine.CharacterController controller =
                    character.AddComponent<UnityEngine.CharacterController>();
                controller.center = new Vector3(0f, 1f, 0f);
                controller.height = 2f;
                controller.radius = 0.35f;
                CharacterMotor motor = character.AddComponent<CharacterMotor>();
                CharacterMotorSettings settings = new(
                    4f,
                    2f,
                    0.5f,
                    0f,
                    0f,
                    720f,
                    1f,
                    1.2f,
                    0.3f,
                    -2f,
                    0.5f);
                motor.ConfigureRuntime(settings, CharacterUpdateMode.Manual, accelerate: false);
                motor.Resume();

                yield return new WaitForFixedUpdate();
                CharacterMotorState moving = motor.Simulate(
                    new CharacterMotorCommand(Vector3.forward, 1f),
                    0.1f);
                Assert.Greater(character.transform.position.z, 0f);
                Assert.Greater(moving.HorizontalSpeed, 0f);

                motor.CancelMotion();
                CharacterMotorState stopped = motor.Simulate(default, 0.1f);
                Assert.AreEqual(0f, stopped.Velocity.x, 0.0001f);
                Assert.AreEqual(0f, stopped.Velocity.z, 0.0001f);
            }
            finally
            {
                Object.Destroy(floor);
                Object.Destroy(character);
            }
        }

        [UnityTest]
        public IEnumerator Motor_ExplicitFacingIsIndependentFromMovementDirection()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject character = new("Character Facing Test");
            try
            {
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(20f, 1f, 20f);
                UnityEngine.CharacterController controller =
                    character.AddComponent<UnityEngine.CharacterController>();
                controller.center = new Vector3(0f, 1f, 0f);
                controller.height = 2f;
                controller.radius = 0.35f;
                CharacterMotor motor = character.AddComponent<CharacterMotor>();
                CharacterMotorSettings settings = new(
                    4f,
                    2f,
                    0.5f,
                    0f,
                    0f,
                    720f,
                    1f,
                    1.2f,
                    0.3f,
                    -2f,
                    0.5f);
                motor.ConfigureRuntime(settings, CharacterUpdateMode.Manual, accelerate: false);
                motor.Resume();

                yield return new WaitForFixedUpdate();
                motor.Simulate(
                    new CharacterMotorCommand(
                        Vector3.left,
                        1f,
                        CharacterMotorFacingMode.ExplicitDirection,
                        Vector3.forward),
                    0.1f);

                Assert.Less(character.transform.position.x, 0f);
                Assert.Greater(Vector3.Dot(character.transform.forward, Vector3.forward), 0.999f);

                motor.CancelMotion();
                motor.Simulate(
                    new CharacterMotorCommand(Vector3.right, 1f),
                    0.25f);
                Assert.Greater(Vector3.Dot(character.transform.forward, Vector3.right), 0.999f);

                motor.CancelMotion();
                character.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                Vector3 idleFacing = character.transform.forward;
                motor.Simulate(default, 0.25f);
                Assert.Greater(
                    Vector3.Dot(character.transform.forward, idleFacing),
                    0.999f);
            }
            finally
            {
                Object.Destroy(floor);
                Object.Destroy(character);
            }
        }

        [UnityTest]
        public IEnumerator CameraRig_SwitchesTargetAndAllModes()
        {
            GameObject cameraObject = new("Character Camera Test");
            GameObject firstTarget = new("First Target");
            GameObject secondTarget = new("Second Target");
            GameObject collisionWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                collisionWall.transform.position = new Vector3(0f, 1.5f, -2f);
                collisionWall.transform.localScale = new Vector3(4f, 4f, 0.25f);
                Physics.SyncTransforms();
                CharacterCameraRig rig = cameraObject.AddComponent<CharacterCameraRig>();
                rig.ConfigureRuntime(CharacterCameraSettings.Default, CharacterUpdateMode.Manual);
                rig.SetTarget(firstTarget.transform, alignYaw: true);
                rig.SetMode(CharacterCameraMode.ThirdPerson);
                rig.Simulate(default, 1f);
                Assert.AreSame(firstTarget.transform, rig.Target);
                Assert.Greater(cameraObject.transform.position.z, -2f);

                rig.SetTarget(secondTarget.transform);
                rig.SetMode(CharacterCameraMode.TopDown);
                rig.Simulate(default, 0.1f);
                Assert.AreEqual(CharacterCameraMode.TopDown, rig.Mode);

                rig.SetMode(CharacterCameraMode.FirstPerson);
                rig.Simulate(default, 0.1f);
                Assert.AreEqual(CharacterCameraMode.FirstPerson, rig.Mode);
                Assert.AreSame(secondTarget.transform, rig.Target);
                yield return null;
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(firstTarget);
                Object.Destroy(secondTarget);
                Object.Destroy(collisionWall);
            }
        }
    }
}
