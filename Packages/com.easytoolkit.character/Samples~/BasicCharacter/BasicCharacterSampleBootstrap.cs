using Easy.Character.Animation;
using Easy.Character.CameraSystem;
using Easy.Character.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Easy.Character.Samples
{
    [DisallowMultipleComponent]
    public sealed class BasicCharacterSampleBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform character;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private InputSystemCharacterInputSource inputSource;
        [SerializeField] private CharacterMotorProfile motorProfile;
        [SerializeField] private CharacterCameraProfile cameraProfile;
        [SerializeField] private CharacterAnimationProfile animationProfile;
        [SerializeField] private RuntimeAnimatorController animatorController;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference look;
        [SerializeField] private InputActionReference sprint;
        [SerializeField] private InputActionReference crouch;
        [SerializeField] private InputActionReference jump;
        [SerializeField] private InputActionReference cameraYaw;
        [SerializeField] private InputActionReference cameraShoulder;
        [SerializeField] private InputActionReference zoom;
        [SerializeField] private InputActionReference orbit;

        private void Awake()
        {
            if (character == null)
                character = CreateCharacter();
            if (controlledCamera == null)
                controlledCamera = CreateCamera();
            inputSource = inputSource != null
                ? inputSource
                : gameObject.AddComponent<InputSystemCharacterInputSource>();
            inputSource.ConfigureActions(
                move,
                look,
                sprint,
                crouch,
                jump,
                cameraYaw,
                cameraShoulder,
                zoom,
                orbit);
            inputSource.SetMovementReference(controlledCamera.transform);

            CharacterMotor motor = character.GetComponent<CharacterMotor>();
            motor = motor != null ? motor : character.gameObject.AddComponent<CharacterMotor>();
            motor.Configure(motorProfile, CharacterUpdateMode.Automatic, inputSource);

            CharacterCameraRig cameraRig = controlledCamera.GetComponent<CharacterCameraRig>();
            cameraRig = cameraRig != null
                ? cameraRig
                : controlledCamera.gameObject.AddComponent<CharacterCameraRig>();
            cameraRig.Configure(cameraProfile, CharacterUpdateMode.Automatic, inputSource);
            cameraRig.SetTarget(character, alignYaw: true);

            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null || animationProfile == null)
                return;
            animator.runtimeAnimatorController = animatorController;
            CharacterAnimatorDriver animation =
                character.GetComponent<CharacterAnimatorDriver>();
            animation = animation != null
                ? animation
                : character.gameObject.AddComponent<CharacterAnimatorDriver>();
            animation.Configure(animator, animationProfile, automatic: true, manageLayers: true);
        }

        private static Transform CreateCharacter()
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Easy Character";
            Object.Destroy(capsule.GetComponent<CapsuleCollider>());
            UnityEngine.CharacterController controller =
                capsule.AddComponent<UnityEngine.CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.35f;
            return capsule.transform;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Easy Character Camera");
            cameraObject.tag = "MainCamera";
            return cameraObject.AddComponent<Camera>();
        }
    }
}
