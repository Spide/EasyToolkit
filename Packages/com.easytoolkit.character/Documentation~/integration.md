# Integration Guide

## Standalone Setup

1. Add a Unity `CharacterController` and `CharacterMotor` to the character root.
2. Assign a `CharacterMotorProfile`, or use the runtime defaults.
3. Add `InputSystemCharacterInputSource` and assign `InputActionReference` fields.
4. Assign the input source to the motor and set its movement reference to the active camera.
5. Add `CharacterCameraRig` to the camera, assign the target and profile, and use the same input source.
6. Optionally add `CharacterAnimatorDriver` and assign a consumer-owned Animator and animation profile.
7. For ranged Humanoid aiming, add `CharacterAimController` to the character
   root, assign a `CharacterAimProfile`, and build/update the Animator controller
   so its owned `Aim Body IK` and `Aim Hands IK` layers have IK Pass enabled.

## Adapter Integration

Applications with an existing input router should construct `CharacterMotorCommand` and `CharacterCameraCommand` directly. Keep modal routing, combat, traversal, AI, and navigation authority outside this package.

Use `CharacterMotorFacingMode.ExplicitDirection` when movement and facing have
different references, such as camera-relative third-person strafing. Supply the
planar camera heading as `WorldFacingDirection`; use `MovementDirection` for
free-running or top-down movement and `Preserve` while an external aim or cover
system owns root rotation. Configure explicit-facing angular acceleration and
deceleration to remove start/stop snaps while retaining continuous camera-yaw
tracking.

Use manual update mode when another system owns update order or switches between direct and navigation movement. Call `CharacterMotor.Suspend` before another movement owner takes control and `Resume` when returning to direct movement.

The same adapter may feed `CharacterCameraRig.Simulate` after applying application-specific sensitivity, cursor, touch, or modal-input rules. The package camera still owns final mode transforms and non-allocating collision resolution.

Feed the exact application-owned world target to `CharacterAimController.Apply`.
Use `CharacterAimWeaponRig` after equipping a ranged weapon; bind its visual
weapon root and select `PrimaryHandOnly` or `PrimaryAndSupportHands`. Two-handed
rigs should provide a weapon-child support-grip transform; the package predicts
that marker through the primary-arm aim correction and stabilizes both elbows.
Set
`suppressed` for full-body ownership such as
traversal, death, jumping, or falling. The IK pose clamps visually, but the
application remains authoritative for hitscan and projectile direction.
`CharacterAimState.RootTurnSpeedDegreesPerSecond` is suitable for driving a
consumer-owned planted-turn layer. Do not derive turn animation from raw
frame-to-frame transform yaw when the aim controller owns root correction.

## Animation Assets

`CharacterAnimationSet` stores references to clips owned by the consuming project. The package does not redistribute character models or third-party animation packs. Use **Easy Toolkit > Character > Build Animator Controller** to generate or update a controller in the consuming project's `Assets` folder.

## Allocation Contract

Motor, camera, input polling, and animation updates use fixed state and non-allocating physics queries. Avoid replacing command sources through per-frame component discovery.
