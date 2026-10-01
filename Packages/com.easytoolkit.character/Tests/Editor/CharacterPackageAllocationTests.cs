using System;
using System.Reflection;
using Easy.Character.Animation;
using Easy.Character.CameraSystem;
using Easy.Character.Editor;
using Easy.Character.InputSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Easy.Character.Tests
{
    /// <summary>
    /// Allocation assertions for the per-frame component paths of the package:
    /// input adapter, motor, camera rig and Animator driver. They replace the
    /// Development Player profiling check with an Editor (Mono) measurement;
    /// only the measured call is counted, device state changes run outside it.
    /// (System.GC.GetAllocatedBytesForCurrentThread reports zero deltas in the Editor,
    /// so the GC.Alloc profiler recorder is used instead.)
    /// </summary>
    public sealed class CharacterPackageAllocationTests : InputTestFixture
    {
        private const string ControllerPath = "Assets/__EasyCharacterAllocationTest.controller";

        private GameObject floor;
        private GameObject wall;
        private GameObject character;
        private GameObject cameraObject;
        private InputActionAsset actions;
        private Gamepad gamepad;
        private Mouse mouse;
        private CharacterMotor motor;
        private CharacterCameraRig rig;
        private InputSystemCharacterInputSource source;
        private CharacterAnimatorDriver driver;
        private Animator animator;
        private AnimatorController controller;

        public override void Setup()
        {
            base.Setup();
            gamepad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, -60f);
            wall.transform.localScale = new Vector3(40f, 4f, 0.25f);

            character = new GameObject("Allocation Character");
            var controllerComponent = character.AddComponent<UnityEngine.CharacterController>();
            controllerComponent.center = new Vector3(0f, 1f, 0f);
            controllerComponent.height = 2f;
            controllerComponent.radius = 0.35f;
            motor = character.AddComponent<CharacterMotor>();
            motor.ConfigureRuntime(
                CharacterMotorSettings.Default,
                CharacterUpdateMode.Manual,
                accelerate: true);
            source = character.AddComponent<InputSystemCharacterInputSource>();
            BuildActions();
            source.SetMovementReference(character.transform);
            motor.SetCommandSource(source);

            cameraObject = new GameObject("Allocation Camera");
            rig = cameraObject.AddComponent<CharacterCameraRig>();
            rig.ConfigureRuntime(CharacterCameraSettings.Default, CharacterUpdateMode.Manual);
            rig.SetTarget(character.transform, alignYaw: true);
            rig.SetCommandSource(source);

            CharacterAnimationSet animationSet =
                ScriptableObject.CreateInstance<CharacterAnimationSet>();
            CharacterAnimationProfile profile =
                ScriptableObject.CreateInstance<CharacterAnimationProfile>();
            controller = CharacterAnimatorControllerBuilder.Build(
                animationSet,
                profile,
                ControllerPath);
            UnityEngine.Object.DestroyImmediate(animationSet);
            UnityEngine.Object.DestroyImmediate(profile);
            animator = character.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            driver = character.AddComponent<CharacterAnimatorDriver>();
            driver.ConfigureRuntime(
                animator,
                CharacterAnimationSettings.Default,
                automatic: true,
                manageLayers: true);

            Physics.SyncTransforms();
            motor.Resume();
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(character);
            UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(wall);
            if (actions != null)
                UnityEngine.Object.DestroyImmediate(actions);
            AssetDatabase.DeleteAsset(ControllerPath);
            base.TearDown();
        }

        [Test]
        public void AllocationProbe_DetectsManagedAllocation()
        {
            object keep = null;
            long allocated = Measure(null, () => keep = new byte[256]);

            Assert.IsNotNull(keep);
            Assert.Greater(allocated, 0L);
        }

        [Test]
        public void InputAdapter_WarmedCommandReadsDoNotAllocate()
        {
            CharacterMotorCommand motorCommand = default;
            CharacterCameraCommand cameraCommand = default;
            float sink = 0f;

            long allocated = Measure(DriveInput, () =>
            {
                source.TryGetMotorCommand(out motorCommand);
                source.TryGetCameraCommand(out cameraCommand);
                sink += motorCommand.InputAmount + cameraCommand.Look.x;
            });

            Assert.Greater(sink, 0f, "Input adapter never observed input.");
            Assert.AreEqual(0L, allocated, "Input adapter command reads allocated.");
        }

        [Test]
        public void Motor_WarmedSimulateWalkSprintCrouchAndJumpDoNotAllocate()
        {
            Vector3 start = character.transform.position;
            Action update = PrivateMethod(motor, "Update");
            CharacterMotorCommand command = default;

            long allocated = Measure(DriveInput, () =>
            {
                source.TryGetMotorCommand(out command);
                motor.Simulate(in command, 0.016f);
                motor.Simulate(
                    new CharacterMotorCommand(
                        Vector3.left,
                        1f,
                        CharacterMotorFacingMode.ExplicitDirection,
                        Vector3.forward,
                        sprintHeld: true,
                        jumpRequested: true),
                    0.016f);
                update();
            });

            Assert.Greater(
                (character.transform.position - start).sqrMagnitude,
                0.0001f,
                "Motor never moved the character.");
            Assert.AreEqual(0L, allocated, "Motor simulate path allocated.");
        }

        [Test]
        public void Camera_WarmedSimulateAllModesDoNotAllocate()
        {
            float sink = 0f;
            CharacterCameraCommand command = default;
            CharacterCameraMode[] modes =
            {
                CharacterCameraMode.ThirdPerson,
                CharacterCameraMode.TopDown,
                CharacterCameraMode.FirstPerson,
            };
            Action update = PrivateMethod(rig, "LateUpdate");

            for (int i = 0; i < modes.Length; i++)
            {
                rig.SetMode(modes[i]);
                CharacterCameraMode mode = modes[i];
                long allocated = Measure(DriveInput, () =>
                {
                    source.TryGetCameraCommand(out command);
                    rig.Simulate(in command, 0.016f);
                    sink += cameraObject.transform.position.y;
                });
                Assert.AreEqual(0L, allocated, "Camera simulate allocated in " + mode + ".");
            }

            // Automatic LateUpdate path (uses Time.unscaledDeltaTime).
            rig.ConfigureRuntime(CharacterCameraSettings.Default, CharacterUpdateMode.Automatic);
            rig.SetTarget(character.transform, alignYaw: true);
            rig.SetMode(CharacterCameraMode.ThirdPerson);
            long automatic = Measure(DriveInput, update);
            Assert.AreEqual(0L, automatic, "Camera LateUpdate allocated.");
            Assert.AreNotEqual(float.MinValue, sink);
        }

        [Test]
        public void Animator_WarmedApplyAndLateUpdateDoNotAllocate()
        {
            Action lateUpdate = PrivateMethod(driver, "LateUpdate");
            Transform reference = character.transform;
            Assert.AreEqual(8, animator.layerCount);

            long apply = Measure(null, () =>
            {
                driver.Apply(
                    new CharacterAnimationFrame(
                        new Vector3(2f, 0f, 3f),
                        reference,
                        isAiming: false,
                        isCrouched: false,
                        isSprinting: true,
                        isJumping: false,
                        isFalling: false),
                    0.016f);
                driver.Apply(
                    new CharacterAnimationFrame(
                        Vector3.zero,
                        reference,
                        isAiming: true,
                        isCrouched: true,
                        isSprinting: false,
                        isJumping: true,
                        isFalling: false),
                    0.016f);
                driver.Apply(
                    new CharacterAnimationFrame(
                        new Vector3(0f, -6f, 1f),
                        reference,
                        isAiming: false,
                        isCrouched: false,
                        isSprinting: false,
                        isJumping: false,
                        isFalling: true),
                    0.016f);
            });
            long late = Measure(DriveInput, () =>
            {
                source.TryGetMotorCommand(out CharacterMotorCommand command);
                motor.Simulate(in command, 0.016f);
                lateUpdate();
            });

            Assert.AreEqual(0L, apply, "Animator driver Apply allocated.");
            Assert.AreEqual(0L, late, "Animator driver LateUpdate allocated.");
        }

        private void BuildActions()
        {
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = actions.AddActionMap("Gameplay");
            InputAction move = map.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            InputAction look = map.AddAction("Look", InputActionType.Value, "<Gamepad>/rightStick");
            InputAction sprint = map.AddAction("Sprint", InputActionType.Button, "<Gamepad>/leftStickPress");
            InputAction crouch = map.AddAction("Crouch", InputActionType.Button, "<Gamepad>/buttonEast");
            InputAction jump = map.AddAction("Jump", InputActionType.Button, "<Gamepad>/buttonSouth");
            InputAction yaw = map.AddAction("Yaw", InputActionType.Value, "<Gamepad>/rightTrigger");
            InputAction shoulder = map.AddAction("Shoulder", InputActionType.Value, "<Gamepad>/leftTrigger");
            InputAction zoom = map.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
            InputAction orbit = map.AddAction("Orbit", InputActionType.Button, "<Gamepad>/buttonWest");
            map.Enable();
            source.ConfigureActions(
                InputActionReference.Create(move),
                InputActionReference.Create(look),
                InputActionReference.Create(sprint),
                InputActionReference.Create(crouch),
                InputActionReference.Create(jump),
                InputActionReference.Create(yaw),
                InputActionReference.Create(shoulder),
                InputActionReference.Create(zoom),
                InputActionReference.Create(orbit));
        }

        private void DriveInput(int frame)
        {
            bool on = (frame & 1) == 0;
            Set(gamepad.leftStick, on ? new Vector2(0.7f, 0.7f) : new Vector2(-0.5f, 0.2f));
            Set(gamepad.rightStick, on ? new Vector2(0.4f, -0.3f) : Vector2.zero);
            Set(gamepad.rightTrigger, on ? 0.3f : 0f);
            Set(gamepad.leftTrigger, on ? 0f : 0.2f);
            Set(mouse.scroll, on ? new Vector2(0f, 1f) : new Vector2(0f, -1f));
            Set(gamepad.leftStickButton, on ? 1f : 0f);
            Set(gamepad.buttonEast, frame % 8 == 0 ? 1f : 0f);
            Set(gamepad.buttonSouth, frame % 5 == 0 ? 1f : 0f);
            Set(gamepad.buttonWest, on ? 1f : 0f);
        }

        private static long Measure(Action<int> prepare, Action measured)
        {
            return AllocationProbe.Measure(prepare, measured);
        }

        private static Action PrivateMethod(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, target.GetType().Name + "." + name + " not found.");
            return (Action)method.CreateDelegate(typeof(Action), target);
        }
    }
}
