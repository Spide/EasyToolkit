# Changelog

All modules share one version; one tag (`vX.Y.Z`) releases the whole set.

## 0.4.0 - 2026-10-22

### Added

- New module `com.easytoolkit.simulationlod` (assemblies `Easy.SimulationLod`,
  `Easy.SimulationLod.BehaviourTree`, `Easy.SimulationLod.Editor`; depends on
  `behaviourtree`, so list `behaviourtree` and `logging` in the manifest too).
  A generic port of a project's tested AI simulation LOD:
  - Tiers `Full`, `Minimal`, `Off` and the new `Dormant` (asleep whatever the
    distance, camera and stickiness; left only by an explicit activation).
  - `SimulationLodDriver`: distance and district (`SimulationSegmentMap`)
    tiers with hysteresis, view camera and observer-view protection,
    round-robin evaluation, per-frame budgets (evaluations, activation
    changes, thinks, Minimal thinks, think time), noise and damage wake,
    staggered wake-all, managed parking (`IsManagedParked`, `TryWakeManaged`,
    `SuspendManaged`, `ReadmitManaged`), `ISimulationLodPolicy`.
  - `SimulationAgent` with whole-object parking (`IsParked`, `IsParking`,
    `IsUnparking`) and `ISimulationTierListener` components; built-in
    listeners for Animator culling, NavMeshAgent and Behaviour toggling.
  - `SimulationVolume`: occupants by containment or explicit list (innermost
    wins), Dormant or capped while inactive, activation by method call,
    observer trigger, noise or occupant damage, all-at-once / staggered /
    nearest-first wakes within the driver budget, optional deactivation when
    observers leave, events and gizmos. Membership tracking (`TrackMembership`,
    default on) re-checks containment on the driver's evaluation slice, so
    agents that enter or spawn later join and leavers are released. A volume
    hold never takes an awake agent that is sticky or on screen, and a
    Minimal/Off cap never applies on screen; Dormant stays camera-agnostic.
  - Sample "Cellar Door" (`Samples~/CellarDoor`).
  - `SimulationAgent.ReadmitOnEnable` (default on): an agent suspended by an
    outside deactivation (pooling) is readmitted when enabled again; an
    explicit `SuspendManaged` stays out. A readmission is a new admission
    (volume assigned again, hold applied at once). Disabling the driver
    reactivates every object it parked and completes an interrupted wake-all.
  - Behaviour tree integration: `SimulationTreeTicker<T,V>` and
    `SimulationBehaviourTreeRunner<T,V>` scale the tick rate with the tier
    through the frame think budget. The behaviour tree module is unchanged.
  - EditMode and PlayMode tests, including a zero-allocation steady-frame test.

### Compatibility

- Minimum versions are unchanged (`unity: 2021.3`). No existing module changed.

## 0.3.1 - 2026-10-01

### Added

- `com.easytoolkit.state`: `GenericState<K>.Plus(K key, float value)` adds a
  float to the stored number and returns the new total. A missing key counts as
  0; stored numeric values (int, float, double, ...) are converted; the sum is
  stored as float; any other stored type throws `InvalidCastException` and
  leaves the state unchanged. `OnChange` is raised. Not part of `IState<K>`,
  so existing implementations are unaffected.

### Changed

- `com.easytoolkit.character` tests: the zero-allocation check of the runtime
  math hot paths now measures real managed allocations through the profiler
  `GC.Alloc` recorder (`AllocationProbe`). The previous
  `GC.GetAllocatedBytesForCurrentThread` delta always read 0 in the Editor's
  Mono runtime. New `CharacterPackageAllocationTests` cover the Input System
  adapter with `InputTestFixture`. The test asmdef references
  `Unity.InputSystem` and `Unity.InputSystem.TestFramework` (shipped with
  Input System 1.7.0, so no minimum version changed).

### Compatibility

- Minimum versions are unchanged (`unity: 2021.3`, Input System `1.7.0`).
  No runtime behavior changed apart from the added `Plus` overload.

## 0.3.0 - 2026-09-28

### Breaking

- **Repository layout.** The single package `com.easytoolkit.core` is replaced
  by one package per module under `Packages/com.easytoolkit.<module>`.
  Migration: in the project manifest, replace the single
  `com.easytoolkit.core` entry with one entry per used module, for example
  `"com.easytoolkit.di": "https://github.com/Spide/EasyToolkit.git?path=/Packages/com.easytoolkit.di#v0.3.0"`,
  and list its EasyToolkit dependencies too. Assembly names are unchanged
  (`Easy.DI`, `Easy.Logging`, ...), so asmdef references keep working. Script
  GUIDs are unchanged.
- **Control.** `ControlContextHandler.cs` was renamed to
  `ControlContextEventHandler.cs` (the class name `ControlContextEventHandler` is
  unchanged). `ControlContext` priority is now a constructor argument
  (`protected ControlContext(int priority = 0)`) instead of a mutable
  `priority` field. Activation events fire only on real state changes.
  Logging was removed from per-event paths.
- The examples assembly `EasyToolkit` was renamed to `EasyToolkit.Examples` and
  moved to `Assets/Examples` (development host only).

### Added

- `com.easytoolkit.sceneflow`: bootstrap scene, persistent role scenes with DI
  contexts, transactional level switching with rollback, loading progress,
  direct level Play Mode, editor scaffold, validator and overrides window.
- `com.easytoolkit.character`: motor, Input System adapter, camera modes,
  locomotion animation, aim IK, editor tools, tests and a Basic Character sample.
- Behaviour tree debugger: copy or save snapshots, property-backed trees, safer
  handling of destroyed targets. Diagnostics no longer filter by a
  game-specific namespace.
- The repository is a Unity development host with all modules testable, a
  Scene Flow sandbox, and a PlayMode composition test.

### Compatibility

- Minimum versions are unchanged (`unity: 2021.3`, Input System `1.7.0`).
  Newer-only APIs are guarded (`FindAnyObjectByType` for 2022.2+, `GetEntityId`
  for 6000.4+).
- Verified: EditMode 60/60 and PlayMode 4/4 on Unity 6000.0.32f1 (development
  host). EditMode 60/60 and PlayMode 4/4 on 6000.5.8f1 in a consumer project
  that took every module through `?path=` Git URLs.

## 0.2.0

- Single package `com.easytoolkit.core` with DI, State, StateMachine, Pooling,
  Logging, BehaviourTree, Control and Save.
