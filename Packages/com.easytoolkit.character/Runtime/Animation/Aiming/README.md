# Aim IK

## Purpose

The aiming subdomain provides allocation-free target commands, torso/root turn
separation, and optional Humanoid hand IK. It has no combat, inventory, camera,
AI, or scene-context dependency.

## Ownership

This subdomain owns generic target-to-pose math, root-turn policy, Humanoid body
and hand IK, and cached weapon-transform bindings. Consumers own target
selection, firing, equipment, animation clips, and suppression policy.

## Entry Points

- `CharacterAimController` evaluates a world target, rotates the character root
  only outside the configured torso range, accelerates/brakes that correction,
  and publishes `CharacterAimState` with signed root angular speed.
- `CharacterAimIkDriver` runs on the same GameObject as the Humanoid `Animator`.
- `CharacterAimProfile` owns yaw, pitch, turn, blend, body, and hand weights.
- `CharacterAimWeaponRig` binds the muzzle/right hand, an explicit
  `CharacterAimRigMode`, and an optional support-grip transform without
  introducing a weapon-system dependency.

## Animator Contract

Controllers must contain `Aim Body IK` followed by `Aim Hands IK`. Both layers
use empty states, weight `1`, and IK Pass. The package editor builder creates the
layers idempotently. Generic and Legacy avatars are ignored safely.

## Dependencies

Runtime code depends only on UnityEngine's Animator and Humanoid IK APIs. The
editor assembly owns consumer-controller layer generation.

## Extension Rules

Consumers feed commands explicitly and own the decision to suppress aiming.
Consumer animation adapters may use the published root angular speed for
planted lower-body pivot clips while the package IK remains authoritative for
the torso and hands.
Commands may independently scale body and hand weights, allowing a
consumer-owned full-body pose to retain most of its torso animation while the
hands still align a weapon to the exact target. These scales do not change the
profile and are copied into the read-only state without allocation.
Combat semantics, weapon definitions, target acquisition, and input remain in
the consuming project. `PrimaryHandOnly` leaves the support arm animation-owned;
`PrimaryAndSupportHands` locks the support hand and elbow to the predicted aimed
grip pose. A missing support grip degrades safely to primary-hand IK. Per-frame
evaluation and IK callbacks must not allocate.
