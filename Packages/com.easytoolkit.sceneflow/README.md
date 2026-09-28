# Easy Toolkit Scene Flow

Profile-driven additive scene composition on top of Easy DI contexts. Extracted
and generalized from a production project's initialization domain.

## What it gives you

- **One entry scene** (`Initialization`) with `SceneFlowInitializer`.
- **Persistent role scenes** (for example Systems, UI, Debug) loaded in profile
  order. Each publishes one named Easy DI container through `SceneRoleContext`
  and must be ready before startup continues.
- **Exactly one promoted level** at a time, published as the `LEVEL` container
  by its `LevelContext`. Main menus are levels too (category `Menu`).
- **Transactional level switching**: the candidate loads privately with content
  inactive, prepares, validates and only then replaces the old level. Any
  failure before commit unloads the candidate and restores the previous level.
- **Loading progress** (`LoadingProgress`) for a temporary `Loading` scene.
- **Direct Play Mode**: pressing Play in any catalogued level (or persistent
  role scene) boots the full composition around it.
- **Editor tooling**: scaffold, validator and per-machine Play Mode override.

## Lifetimes

```text
GLOBAL   MainContext (DontDestroyOnLoad): ILevelLoader, LoadingProgress,
         SceneFlowProfile, LevelCatalog, SceneLoadService
SYSTEMS  Systems scene: camera, input hosts, persistent player, services
UI       UI scene: HUD/menus presenters, EventSystem
DEBUG    Debug scene: Editor/development builds only
LEVEL    the one promoted level scene, replaced on every level change
(Loading scene: no context, exists only during startup/transitions)
```

Role names other than `GLOBAL` and `LEVEL` are configuration, not code.

## Startup sequence

1. `SceneFlowInitializer.Awake` creates `GLOBAL` and binds its services.
2. Loading scene loads (optional).
3. Each persistent entry loads; its `SceneRoleContext` must match the configured
   context name and become ready within `contextReadyTimeoutSeconds`.
4. Startup level resolves in this order: queued direct-play level, Editor
   override, a catalogued level already loaded, then the profile default. A
   failing non-default startup level falls back to the default.
5. Loading and Initialization scenes unload, `IsInitialized` becomes true,
   `Initialized` and then `LevelActivated` fire.

## Level switch sequence

```text
LoadLevelAsync(request)
  reject: NotInitialized | Busy | AlreadyActive (unless ForceReload)
  UnknownLevel / SceneUnavailable        <- catalog + Build Settings check
  previous.Suspend(); participants.OnTransitionStarted
  load candidate (second instance when force-reloading the same scene)
  wait LevelContext -> PrepareAsync -> entry point lookup
  previous.Deactivate(); candidate.Activate()      (LEVEL republished)
  participants.TryEnterLevel(activation)           (place party, camera...)
  commit: SetActiveScene, participants.OnTransitionCommitted, unload previous
  any failure before commit -> unload candidate, restore previous,
                               participants.OnTransitionAborted(result)
  after the loading scene is gone -> LevelActivated
```

## Using it

```csharp
// Anywhere after startup (for example a menu button):
if (SceneFlowContexts.TryResolveGlobal(out ILevelLoader loader))
{
    LevelLoadResult result = await loader.LoadLevelAsync(new LevelLoadRequest("sample_level"));
    if (!result.Succeeded) Debug.LogWarning(result);
}
```

Persistent systems that must follow level changes implement
`ILevelTransitionParticipant` and call `loader.RegisterParticipant(this)` from
`OnEnable`. See `PlayerPlacement` in the bootstrap project for a complete
example including rollback.

Level-specific services: subclass `LevelContext` and override
`BindLevelServices`, `OnPrepareAsync`, `TryValidateAuthoring`, `OnActivated`,
`OnSuspended` and `OnResumed`, or add `ContextBinder` components next to it.

## Authoring rules

- Loading is by **level key**, never by scene name. Scene names must be unique
  among Build Settings scenes.
- A level scene has one root object with `LevelContext`; all level content lives
  beneath it (children are the content roots unless set explicitly). Objects
  outside that hierarchy are not managed.
- Every level needs a `LevelEntryPoint` with id `default`.
- Persistent camera, audio listener, EventSystem, player and UI never live in a
  level scene.
- The Initialization scene must be the first enabled Build Settings scene.
- The profile lives at `Resources/SceneFlow/SceneFlowProfile.asset` in any
  `Resources` folder.

## Editor menu

- `Tools/Easy Toolkit/Scene Flow/Create Or Repair Composition...`: idempotent
  scaffold of scenes, profile, catalog, sample level, material and Build
  Settings order. Never overwrites existing scenes or assets. Batch mode:
  `-executeMethod Easy.SceneFlow.Editor.SceneFlowScaffold.CreateOrRepairFromCommandLine -sceneFlowRoot Assets/MyGame`.
- `Tools/Easy Toolkit/Scene Flow/Validate Composition`: profile, catalog, Build
  Settings order and level scene authoring.
- `Tools/Easy Toolkit/Scene Flow/Play Mode Overrides`: per-machine startup level.

## Presentation placeholders

`LoadingProgressOverlay` and `LevelSwitcherOverlay` draw with IMGUI so the
package has no UI dependency. They are placeholders: replace them with
project-owned views that read `LoadingProgress` and `ILevelLoader`.

## Dependencies

Assemblies `Easy.DI` and `Easy.Logging` (same package). No input, UI or render
pipeline dependency.

## Tests

- `Easy.SceneFlow.Tests` (EditMode): catalog, profile, request, gate, progress.
- End-to-end composition is covered by the consuming project's PlayMode smoke
  test, because it needs real scenes in Build Settings.

## Known limits

- Scene loading uses `Task` plus frame polling; there is no cancellation.
- A participant exception during a transition propagates to the caller after
  the transition gate is released; participants should not throw.
- Easy DI keeps containers in a static registry; Enter Play Mode Options with
  domain reload disabled is not supported yet.
