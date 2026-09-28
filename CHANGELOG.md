# Changelog

All modules share one version; one tag (`vX.Y.Z`) releases the whole set.

## 0.3.0 - unreleased

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
  host). EditMode 60/60 on 6000.5.8f1.

## 0.2.0

- Single package `com.easytoolkit.core` with DI, State, StateMachine, Pooling,
  Logging, BehaviourTree, Control and Save.
