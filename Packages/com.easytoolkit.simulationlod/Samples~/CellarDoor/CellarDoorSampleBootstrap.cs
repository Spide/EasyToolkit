using UnityEngine;

namespace Easy.SimulationLod.Samples
{
    /// <summary>
    /// Code-built "cellar wakes when the door opens" scene: a driver, an observer that walks towards the house, a
    /// Dormant cellar volume with sleeping agents, and a door that activates it. Agents are tinted by tier (green Full,
    /// yellow Minimal, grey Off, blue Dormant). Add this component to an empty object in an empty scene and press Play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CellarDoorSampleBootstrap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int sleepers = 8;
        [Tooltip("The door opens on its own after this many seconds. 0: only through the button or OpenDoor().")]
        [SerializeField, Min(0f)] private float openDoorAfterSeconds = 6f;
        [SerializeField, Min(0f)] private float observerSpeed = 2f;

        private SimulationLodDriver driver;
        private SimulationVolume cellar;
        private Transform observer;
        private Transform door;
        private float startTime;

        public SimulationVolume Cellar => cellar;

        /// <summary>Opens the door: the cellar wakes nearest to the door first, two per frame, within the driver budget.</summary>
        public void OpenDoor()
        {
            if (cellar == null || cellar.IsActive) return;
            door.localRotation = Quaternion.Euler(0f, 90f, 0f);
            cellar.Activate(door.position);
        }

        private void Awake()
        {
            driver = new GameObject("Simulation LOD Driver").AddComponent<SimulationLodDriver>();
            var settings = SimulationLodSettings.Default;
            settings.FullRadius = 12f;
            settings.MinimalRadius = 25f;
            settings.ObserverViewRadius = 10f;
            driver.Settings = settings;

            observer = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            observer.name = "Observer";
            observer.position = new Vector3(0f, 1f, -30f);
            observer.gameObject.AddComponent<SimulationObserver>();

            // The cellar: a box volume under the house; its occupants sleep until the door opens.
            var cellarObject = new GameObject("Cellar");
            cellarObject.transform.position = new Vector3(0f, -2f, 0f);
            cellar = cellarObject.AddComponent<SimulationVolume>();
            cellar.Center = Vector3.zero;
            cellar.Size = new Vector3(10f, 4f, 10f);
            cellar.InactiveTier = SimulationTier.Dormant;
            cellar.WakeMode = SimulationVolumeWakeMode.NearestFirst;
            cellar.WakePerFrame = 2;
            cellar.Activated += v => Debug.Log($"[Cellar door sample] {v.name} activated: waking {v.OccupantCount} occupants.");
            cellar.WakeCompleted += v => Debug.Log($"[Cellar door sample] {v.name}: every occupant is awake.");

            for (int i = 0; i < sleepers; i++)
            {
                GameObject agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agent.name = "Sleeper " + i;
                agent.transform.position = new Vector3(-4f + (i % 4) * 2.5f, -3f, -3f + (i / 4) * 3f);
                agent.AddComponent<CellarDoorSampleTint>();
                agent.AddComponent<SimulationAgent>();
            }

            // The ground hides the cellar: Dormant ignores the camera, so sleepers belong out of sight.
            Transform ground = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
            ground.name = "Ground";
            ground.localScale = new Vector3(8f, 1f, 8f);

            door = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            door.name = "Door";
            door.position = new Vector3(0f, 1f, -5f);
            door.localScale = new Vector3(2f, 2f, 0.2f);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 25f, -25f);
            camera.transform.LookAt(Vector3.zero);
            driver.SetViewCamera(camera);
            startTime = Time.time;
        }

        private void Update()
        {
            if (observer.position.z < -8f) observer.position += Vector3.forward * (observerSpeed * Time.deltaTime);
            if (openDoorAfterSeconds > 0f && Time.time - startTime >= openDoorAfterSeconds) OpenDoor();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 320f, 140f), GUI.skin.box);
            GUILayout.Label($"Full {driver.FullCount}  Minimal {driver.MinimalCount}  Off {driver.OffCount}  Dormant {driver.DormantCount}");
            GUILayout.Label(cellar.IsWaking ? $"Cellar waking: {cellar.PendingWakeCount} left" : cellar.IsActive ? "Cellar awake" : "Cellar asleep");
            if (!cellar.IsActive && GUILayout.Button("Open the cellar door")) OpenDoor();
            GUILayout.EndArea();
        }
    }
}
