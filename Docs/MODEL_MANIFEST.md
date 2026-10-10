# Mission Mohakash - 3D model manifest

All files are glTF 2.0 binary (.glb), validated with the Khronos glTF validator (0 errors, 0 warnings).
Units = metres. +Y up. Object FRONT faces +Z. Origin = centre of the base on the ground (unless noted).
Flat-shaded PBR materials, no textures (so no missing-texture pink). Emissive lights use KHR_materials_emissive_strength.

## Import into Unity 6
1. Window > Package Manager > + > Add package by name: `com.unity.cloud.gltfast` (if not found: Add package from git URL `https://github.com/atteneder/glTFast.git`).
2. Copy the `Assets/` folder from this ZIP into your project. Unity imports every .glb as a prefab-like asset.
3. Drag a model into the scene (or create a prefab from it). Animated models get an `Animation` component with the clips below.
4. Astronaut: add `AstronautAnimator.cs` to the astronaut root; call `SetMoveSpeed(speed)` each frame, and `Play(AstronautState.Wave/Interact/Death)`.
5. Rocks: add `MoonRockScatterer.cs` to an empty object, assign moon_rock_01..06 to `variants` -> 260 rocks.
Tip: set Static on base buildings; the 260 rocks reuse 6 meshes (GPU-instancing friendly).

## Files
| File | Item | Size m (W x H x D) | Tris | KB | Clips |
|---|---|---|---|---|---|
| Characters/astronaut.glb | Astronaut (you), rigged | 0.79 x 1.97 x 0.64 | 9124 | 700 | Idle, Walk, Run, Wave, Interact, Death |
| Base/habitat.glb | Habitat (HAB) | 10.80 x 6.25 x 7.33 | 9212 | 314 | Open, Close, Operate |
| Base/airlock_suit_station.glb | Airlock / Suit Station (SUI) | 8.66 x 3.49 x 5.50 | 23324 | 774 | Open, Close |
| Base/oxygen_generator.glb | Oxygen Generator module (OGS) | 4.50 x 3.24 x 3.00 | 3664 | 138 | Operate |
| Base/storage_module.glb | Storage Module (STO) | 3.00 x 2.86 x 6.40 | 5024 | 149 | Open, Close |
| Base/garage.glb | Garage (GAR) | 8.00 x 6.48 x 9.50 | 8888 | 236 | Open, Close |
| Base/comms_antenna.glb | Comms antenna (COM) | 3.60 x 10.46 x 3.60 | 3708 | 146 | Operate |
| Base/radiation_monitor.glb | Radiation Monitor post (RAD) | 1.40 x 3.00 x 1.10 | 1296 | 61 | Operate |
| Base/solar_array.glb | Solar arrays (SOL-S) | 6.70 x 2.82 x 4.40 | 4960 | 141 | Track |
| Vehicles/scout_rover.glb | Scout Rover (ROV-SC) | 1.92 x 2.22 x 2.51 | 4452 | 199 | Drive, Operate |
| Vehicles/lander.glb | Lander | 7.80 x 5.64 x 7.78 | 4164 | 145 | - |
| Props/battery.glb | Battery (BAT) | 0.41 x 0.43 x 0.22 | 1244 | 42 | - |
| Props/seismometer_crate.glb | Seismometer crate | 0.82 x 0.42 x 0.60 | 2544 | 72 | Open, Close |
| Props/seismometer_backpack.glb | Seismometer on your back | 0.40 x 0.62 x 0.36 | 1780 | 62 | - |
| Props/seismometer_deployed.glb | Deployed seismometer body | 1.00 x 0.62 x 1.04 | 1764 | 64 | Operate |
| Props/tank_oxygen.glb | Oxygen tank (TNK-O) | 1.40 x 2.75 x 1.40 | 1996 | 79 | - |
| Props/tank_water.glb | Water tank (TNK-W) | 1.40 x 2.75 x 1.40 | 1996 | 79 | - |
| Rocks/sample_rock_a.glb | Sample rock A | 0.41 x 0.24 x 0.42 | 320 | 40 | - |
| Rocks/sample_rock_b.glb | Sample rock B | 0.34 x 0.20 x 0.28 | 880 | 110 | - |
| Rocks/sample_rock_c.glb | Sample rock C | 0.45 x 0.30 x 0.37 | 880 | 110 | - |
| Rocks/moon_rock_01.glb | Scattered moon rock variant 1 | 0.38 x 0.18 x 0.34 | 80 | 11 | - |
| Rocks/moon_rock_02.glb | Scattered moon rock variant 2 | 1.05 x 0.53 x 0.99 | 80 | 11 | - |
| Rocks/moon_rock_03.glb | Scattered moon rock variant 3 | 1.04 x 0.63 x 1.18 | 320 | 40 | - |
| Rocks/moon_rock_04.glb | Scattered moon rock variant 4 | 1.95 x 0.78 x 1.24 | 320 | 40 | - |
| Rocks/moon_rock_05.glb | Scattered moon rock variant 5 | 2.75 x 1.33 x 2.84 | 320 | 40 | - |
| Rocks/moon_rock_06.glb | Scattered moon rock variant 6 | 0.72 x 0.22 x 0.56 | 80 | 11 | - |

## Astronaut (astronaut.glb)
- 17-bone skeleton: Hips > Spine > Chest > Neck > Head; Left/Right UpperArm > LowerArm > Hand; Left/Right UpperLeg > LowerLeg > Foot. Height 1.97 m.
- Clips (30 fps, in place / no root motion): Idle 4.0s loop, Walk 1.0s loop (~1.7 m/s), Run 0.6s loop (~4.2 m/s), Wave 2.4s once, Interact 1.6s once (reach + press), Death 2.6s once (clamp last frame; ends lying on back).
- Sockets (empty child nodes): `Socket_Back` (seismometer pack mounts here), `Socket_HandR`, `Socket_HandL`, `Socket_Helmet`.
- Seismometer on back: parent seismometer_backpack.glb to `Socket_Back` with zero local position/rotation (its origin is the mounting plane, it extends behind the player).
- Mesh is rigidly skinned per segment (suit-style); unity Generic rig. Rig type Humanoid is not required.

## Animated props
- Doors/lids: clips `Open` / `Close` (habitat Door slides, airlock hatch swings, storage doors swing, garage RollDoor coils, crate Lid).
- Machines: clip `Operate` loops (OGS fan, HAB beacon, COM dish scan + blinking light, RAD sensor spin, deployed seismometer LED).
- Solar array: `Track` (slow sun tracking). Rover: `Drive` (all six wheels spin; scale playback speed by speed). Steering: rotate nodes `Steer_FL/FR/ML/MR/RL/RR` about Y from script. `Operate` pans the camera mast.

## Regenerating
`tools/` contains the Python generator (numpy only): `python3 tools/build_all.py <outdir>`.
