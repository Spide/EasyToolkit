# EasyToolkit

A lightweight collection of reusable Unity C# modules for common gameplay and architecture tasks.

EasyToolkit is organized as independent modules (DI, scene flow, save, state handling, state machine, pooling, logging, behavior tree, control contexts and character) so you can use only what your project needs. Each module has its own assembly definition.

---

## Modules

- **`Easy.DI.Module`** — small dependency-injection container with parent container chaining and constructor injection.
- **`Easy.State.Module`** — dynamic key/value state objects and helper utilities for combining state values.
- **`Easy.StateMachine.Module`** — generic finite state machine with regular and global (“any”) transitions.
- **`Easy.Pooling.Module`** — generic and Unity-friendly object pools.
- **`Easy.Logging.Module`** — colored logger wrapper around Unity logging.
- **`Easy.BehaviourTree`** — behavior tree nodes, decorators, and builders.
- **`Easy.Control.Module`** — event-driven control contexts with priority and propagation handling.
- **`Easy.Save`** — versioned JSON saves with migrations, safe keys, atomic writes and stable identity.
- **`Easy.SceneFlow`** — profile-driven additive scene composition: bootstrap scene, persistent role scenes with DI contexts, transactional level switching with rollback, loading progress and direct level Play Mode. See `SceneFlow/README.md`.
- **`Easy.Character.*`** — character motor, Input System adapter, camera modes, locomotion animation and aim IK (Core, InputSystem, Camera, Animation, Editor). See `Character/README.md`.

---

## Getting started

1. Copy the folders you need into your Unity project (or include the whole repository under `Assets/`).
2. Make sure the `.asmdef` files are included so Unity can compile each module assembly.
3. Reference module namespaces in your scripts (for example: `Easy.Pooling`, `Easy.StateMachine`, etc.).

---

## Usage examples

### State (`Easy.State`)

```csharp
var baseStats = new GenericState<WeaponAttribute>();
baseStats.Set(WeaponAttribute.DAMAGE, 100);

var perks = new GenericState<WeaponAttribute>();
perks.Set(WeaponAttribute.DAMAGE, 50);

var penalties = new GenericState<WeaponAttribute>();
penalties.Set(WeaponAttribute.DAMAGE, -20);

var totalDamage = StateUtils.Sum(WeaponAttribute.DAMAGE, baseStats, perks, penalties);
// totalDamage = 130
```

### State machine (`Easy.StateMachine`)

```csharp
var fsm = new StateMachine<MyState>();
fsm.AddTransition(idleState, attackState, () => targetInRange);
fsm.AddAnyTransition(deadState, () => health <= 0);

fsm.SetState(idleState);

// call every frame
fsm.Tick();
```

### Pooling (`Easy.Pooling`)

```csharp
var pool = new Pool<Bullet>(() => new Bullet(), limit: 64);

var bullet = pool.Get();
// ...use bullet...
pool.Push(bullet);
```

For Unity `MonoBehaviour` pooling, see `GameObjectPool<T>` and `Examples/EasyToolkitPoolExamples.cs`.

### Dependency injection (`Easy.DI`)

```csharp
var root = new DIContainer("Root");
root.Bind(new AudioService());

var scene = new DIContainer("Scene", root);
scene.Bind<PlayerController>(); // constructor dependencies are resolved

var player = scene.Resolve<PlayerController>();
```

Use `[ResolveBy("name")]` on constructor parameters to resolve explicit named bindings.

### Control contexts (`Easy.Control`)

```csharp
public class GameplayContext : ControlContext
{
    [ControlEvent("Jump")]
    private void Jump() { /* ... */ }
}

var handler = new ControlContextEventHandler();
handler.AddContext(gameplayContext);
gameplayContext.Activate();

handler.TriggerEvent("Jump");
```

Contexts are ordered by priority; the first active context that handles an event stops propagation.

### Behavior tree (`Easy.BehaviourTree`)

You can construct trees manually (`Selector`, `Sequence`, decorators) or with builders (`TreeBuilder`, decorator/composite builders).

See `BehaviourTree/Tests/BehaviourTreeTest.cs` for complete examples covering:
- selectors and sequences,
- conditional decorators,
- repeat/running/proxy decorators,
- builder-based tree composition,
- utility selector usage.

### Scene flow (`Easy.SceneFlow`)

```csharp
// After startup, from any scene:
if (SceneFlowContexts.TryResolveGlobal(out ILevelLoader loader))
{
    LevelLoadResult result = await loader.LoadLevelAsync(new LevelLoadRequest("sample_level"));
    if (!result.Succeeded) Debug.LogWarning(result);
}
```

`Tools > Easy Toolkit > Scene Flow > Create Or Repair Composition...` scaffolds the Initialization, Loading, Systems, UI, Debug, MainMenu and SampleLevel scenes plus profile, catalog and Build Settings. Persistent systems that follow level changes implement `ILevelTransitionParticipant`.

### Logging (`Easy.Logging`)

```csharp
private static readonly EasyLogger LOGGER =
    LoggerFactory.GetLogger(typeof(MyClass), Color.cyan, "[MyClass]");

LOGGER.Log("Hello {0}", playerName);
LOGGER.LogWarning("Potential issue");
LOGGER.LogError("Something failed");
```

---


## Package distribution and versioning

To make EasyToolkit easier to share across projects, this repo now includes a root `package.json` so it can be consumed as a Unity Package Manager (UPM) package.

### Recommended sharing workflow

1. Tag releases with semantic versions (`v0.2.0`, `v0.3.0`, etc.).
2. Consume via UPM git URL in other projects, for example:
   - `https://github.com/<org>/EasyToolkit.git#v0.2.0`
3. Bump `package.json` version on each release.
4. Keep `.asmdef` references name-based (not GUID-based) for portability between repos/projects.

### Suggested versioning policy

- **PATCH** (`0.2.1`): bugfixes, no API break.
- **MINOR** (`0.3.0`): backward-compatible features.
- **MAJOR** (`1.0.0`): breaking API changes.

## Repository layout

- `BehaviourTree/` — behavior tree runtime, decorators, builders, and tests
- `Control/` — control context/event routing system
- `DI/` — dependency injection container and Unity context binders
- `Logging/` — logger wrappers and config
- `Poolers/` — generic pools and Unity object pools
- `Save/` — versioned save system, serializers, migrations and tests
- `SceneFlow/` — scene composition runtime, editor scaffold/validator and tests
- `Character/` — character motor, input, camera, animation, editor tools, tests and `Samples~`
- `State/` — generic state containers and helpers
- `StateMachine/` — finite state machine implementation
- `Examples/` — sample MonoBehaviour usage scripts

---

## Notes

- The toolkit is modular by design—import only the assemblies you need.
- Many folders include `Tests` or `Examples` files you can use as integration references.
- To run the package tests in a consuming project, add `"testables": ["com.easytoolkit.core"]` to its `Packages/manifest.json`.
- Minimum Unity and Input System versions are declared in `package.json`; newer-only APIs are guarded with `UNITY_*_OR_NEWER` defines.

## Changes in 0.3.0

- Added `SceneFlow` and `Character` modules.
- Behaviour tree debugger: snapshot copy/save, property-backed trees, safer target handling.
- **Breaking (Control):** `ControlContextHandler.cs` renamed to `ControlContextEventHandler.cs` (class name unchanged); `ControlContext` priority is now a constructor argument (`protected ControlContext(int priority = 0)`) instead of a mutable `priority` field; activation events fire only on actual state changes; per-call logging removed from hot paths.
