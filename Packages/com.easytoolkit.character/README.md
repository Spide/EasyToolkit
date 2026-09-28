# Easy Toolkit Character

Reusable Unity character building blocks with no game-domain or dependency-injection coupling.

## Modules

- `Easy.Character.Core`: direct `CharacterController` motor, profiles, commands, and state.
- `Easy.Character.InputSystem`: optional Input System command source using serialized action references.
- `Easy.Character.Camera`: third-person, first-person, and top-down camera rig.
- `Easy.Character.Animation`: profile-driven locomotion plus optional Humanoid
  upper-body and weapon aim IK.
- `Easy.Character.Editor`: consumer-owned Animator controller generation.

The package intentionally contains no character models or third-party animation clips. Assign consumer-owned clips through `CharacterAnimationSet`.

`CharacterAnimatorDriver` keeps normal locomotion active as the base and blends
the optional Sprint layer by actual horizontal speed between the animation
profile's normal and sprint reference speeds. Sprint intent gates this blend,
while posture and airborne states suppress it.

`CharacterMotorCommand` separates travel from facing through
`CharacterMotorFacingMode`. Commands may face their movement direction, preserve
the current root rotation, or rotate toward an explicit world direction. The
explicit mode supports camera-facing strafing without coupling Core to a camera
or input implementation. Profiles may add angular acceleration and braking to
explicit facing; settings created through the legacy constructor keep the
original constant-speed rotation behavior.

`CharacterAimController` accelerates and brakes root correction outside the
configured torso range and publishes its signed angular speed through
`CharacterAimState`. Consumers can use that state to drive project-owned pivot
footwork without putting licensed clips in the package.

## Ownership

The package owns direct movement, camera presentation, locomotion animation,
and generic Humanoid aim posing. Combat target selection, AI, navigation,
traversal rules, interaction, inventory, and application contexts integrate
through commands or adapters.

## Update Modes

Use `Automatic` mode when package command sources own the normal Unity update loop. Use `Manual` mode when a game coordinates input, NavMesh handoff, traversal, cutscenes, or deterministic update order. `CharacterMotor.Suspend`, `Resume`, and `CancelMotion` make movement ownership explicit.

## Documentation

- `Documentation~/api.md`: runtime contracts and extension points.
- `Documentation~/integration.md`: standalone setup and command-adapter guidance.
- `Documentation~/migration.md`: adapter-first migration for an existing controller.
- `Runtime/Animation/Aiming/README.md`: aim ownership, setup, and extension rules.
- `Samples~/BasicCharacter/README.md`: capsule sample setup.
