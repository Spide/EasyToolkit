# Cellar Door Sample

1. Import the sample from the Package Manager (Easy Toolkit Simulation LOD > Samples).
2. In an empty scene, add `CellarDoorSampleBootstrap` to an empty object and press Play.
3. The bootstrap builds a driver, an observer capsule walking towards the house, a Dormant cellar volume with
   sleeping capsules (blue) and a door. The sleepers stay asleep although the observer comes close.
4. The door opens after six seconds, or with the on-screen button, or from code with `OpenDoor()`. The cellar
   wakes nearest to the door first, two per frame, within the driver's activation budget, and the agents then
   follow the normal tiers (green Full, yellow Minimal, grey Off).

The cellar lies below the floor, out of the camera's view: Dormant ignores the camera by design, so Dormant
volumes belong inside buildings, cellars and other places the player cannot see into.
