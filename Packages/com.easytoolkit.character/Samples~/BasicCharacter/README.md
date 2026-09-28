# Basic Character Sample

1. Create an empty scene object and add `BasicCharacterSampleBootstrap`.
2. Assign the actions from `BasicCharacter.inputactions` to the bootstrap's serialized Input Action fields. Leave Orbit empty to allow look input without a hold button.
3. Enter Play Mode. The bootstrap creates a capsule motor and a camera when references are empty.
4. Optionally assign consumer-owned `CharacterMotorProfile`, `CharacterCameraProfile`, `CharacterAnimationProfile`, and Animator assets.

The sample intentionally contains no third-party animation clips.
