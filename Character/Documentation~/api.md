# API Overview

## Core

- `CharacterMotor` applies `CharacterMotorCommand` values through Unity's `CharacterController` and returns immutable `CharacterMotorState` snapshots.
- `CharacterMotorProfile` stores walk, sprint, crouch, acceleration, rotation, explicit-facing angular acceleration/braking, gravity, jump, grounding, and fall settings.
- `CharacterMotorFacingMode` selects movement-facing rotation, an independent explicit world-facing direction, or preserved root rotation. Explicit facing uses stopping-distance-limited acceleration and braking when those profile values are configured. The legacy `faceMovement` command constructor maps to movement-facing or preserve behavior.
- `ICharacterMotorCommandSource` supplies commands without coupling the motor to a specific input framework.
- `CharacterUpdateMode.Manual` lets another movement owner control simulation order and call `Suspend`, `Resume`, or `CancelMotion` during handoff.

## Input System

`InputSystemCharacterInputSource` implements both motor and camera command-source contracts. Assign actions through serialized `InputActionReference` fields or `ConfigureActions`. It enables only actions that were disabled when it acquired them and disables only those same actions during teardown.

## Camera

`CharacterCameraRig` owns third-person orbit/collision, top-down rotation/zoom, first-person placement, target switching, and camera-relative planar basis. Use `CharacterCameraProfile` for authored tuning or `ConfigureRuntime` for adapter-owned values.

## Animation

`CharacterAnimatorDriver` maps immutable `CharacterAnimationFrame` values to configurable Animator parameters and optional layer weights. `CharacterAnimationProfile` owns parameter names, layer names, damping, cadence, and blend durations.

Implement `ICharacterAnimationExtensionStateSource` for consumer-owned full-body states such as attacks or traversal. The highest active priority may suppress package locomotion layers or force normal locomotion playback without the package knowing the gameplay domain.

`CharacterAnimationSet` references consumer-owned Humanoid clips. The editor builder creates or idempotently updates a consumer-owned Animator controller; no clips are copied into the package.

### Humanoid Aim IK

- `CharacterAimController` consumes an allocation-free `CharacterAimCommand`,
  applies the profile's torso dead zone and root-turn hysteresis, accelerates
  and brakes root correction without overshoot, and exposes a read-only
  `CharacterAimState` including signed root angular speed.
- Each command can independently scale body and hand IK weights. This lets a
  consumer-owned full-body state retain its authored torso pose while keeping
  weapon-hand alignment, without adding gameplay concepts to the package.
- `CharacterAimProfile` owns yaw/pitch limits, maximum root speed, root angular
  acceleration/deceleration, blend timing, look distribution, and hand IK weights.
- `CharacterAimIkDriver` must live beside the Humanoid `Animator`. `Aim Body IK`
  distributes the pose through the spine/head. With a bound weapon root,
  `Aim Hands IK` rotates the right hand and elbow positions around the upper arm
  toward the exact target and preserves the authored right-palm rotation.
  `PrimaryAndSupportHands` additionally places the left hand on a support marker
  and stabilizes its elbow; `PrimaryHandOnly` clears every support-arm IK weight.
- `CharacterAimWeaponRig` binds cached muzzle, right-hand, weapon-root, and
  optional support-grip data without per-frame component lookup. Its effective
  mode falls back to primary-hand IK when a requested support grip is missing.

The consumer-owned Animator controller must contain the two empty aiming layers
in body-then-hands order with IK Pass enabled. The package editor builder creates
or repairs those layers idempotently. Generic and Legacy animators are ignored.

## Allocation Contract

Commands, states, and settings are readonly structs. Runtime polling and simulation use cached interfaces, fixed arrays, and non-allocating physics casts. Configure components and extension-source arrays outside per-frame paths.
