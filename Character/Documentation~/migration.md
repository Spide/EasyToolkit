# Adapter-First Migration

Use this sequence when an existing game already has stable scene and prefab components.

1. Keep existing MonoBehaviours and serialized APIs in place.
2. Add `CharacterMotor` beside the existing `CharacterController`. Convert the existing input snapshot into `CharacterMotorCommand` and call `Simulate` from the established update owner.
3. Keep NavMesh, traversal, knockback, and cutscene movement in the game. Suspend the package motor before those systems take authority and resume it afterward.
4. Add `CharacterCameraRig` beside the existing camera facade. Keep player lookup, cursor policy, recoil sources, and game-specific visibility outside the package; delegate final framing and collision to the package rig.
5. Add `CharacterAnimatorDriver` beside the existing animation facade. Delegate locomotion parameters and cadence while retaining attacks, damage, death, weapons, and traversal in the game.
6. Update prefab/setup tooling to install the package components. Do not replace existing component scripts or GUIDs during the first migration.
7. After all consumers depend on the adapter surface, move additional generic behavior only when it has no game-domain dependency.

This order keeps serialized assets valid and makes each ownership handoff independently testable.
