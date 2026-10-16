# MISSION MOHAKASH - HANDOFF (Milestone 05 - animated astronaut)

## Goal
Mission Mohakash (OUTPOST ZERO, "Junior Astronaut Mission Trainer") is a NASA Space Apps 2026 educational game: one astronaut plans a
cargo loadout, runs a Moon/Mars outpost whose power, oxygen, water and suit systems are linked, completes a science objective and reads a
report that explains *why* things went right or wrong. Presentation is full 3D third-person (owner's choice; see DECISIONS D1).
Priority: a complete Mission 1 vertical slice, then Mission 2.

## Environment
- Unity **6.6 (6000.6.5f1)**, Apple Silicon Mac, template **Universal 3D (URP)**, project folder `~/Documents/nasa-spaceapps/MissionMohakash`.
- Input: **Input System package only** (Project Settings > Player > Active Input Handling = Input System Package). All reads in `GameInput.cs`.
- Packages added: `com.unity.cloud.gltfast` 6.20.0 (GLB import). Everything else came with the template.
- Build target: macOS standalone built and run. **WebGL module is not installed**, so web builds are NOT verified (install "Web Build Support"
  in Unity Hub, then test; known risks: build size ~100 MB+, VideoPlayer must use StreamingAssets URL `intro.mp4`, which this code already does).

## Import / open
Open the project folder in Unity Hub. If assets look missing (pink/empty), run menu **Mission Mohakash > Rebuild Project Setup**, then
open `Assets/Scenes/Intro.unity` and press Play. To copy into another project: copy `Assets/Scripts`, `Assets/Editor`, `Assets/Resources`,
`Assets/Tests`, `Assets/StreamingAssets`, add glTFast 6.20.0 to `Packages/manifest.json`, then run Rebuild Project Setup.

## Files (all in `Assets/` unless noted)
| Path | Purpose |
|---|---|
| Scripts/Core/GameInput.cs | Only input access; AutoTest injection hooks |
| Scripts/Core/Game.cs | Scene names/flow, settings (PlayerPrefs), mission unlocks, cursor |
| Scripts/Sim/Loadout.cs | M1 equipment catalogue (GDD §40), loadout limits + warnings, GDD recommended plan (522 kg / 125 Cr) |
| Scripts/Sim/MissionModel.cs | Pure-C# Mission 1 simulation: clock, power bus, life support, suit, radiation, rover energy, SEIS, hazards, events, cause chain |
| Scripts/Sim/MissionReport.cs | GDD §26 score, stars, suggestions, Real-vs-Game cards |
| Scripts/Mission1.cs | Mission director: phases, world objects, interactions, EVA/habitat/rover, speed, end |
| Scripts/World/MoonWorld.cs | Procedural ridge terrain, sky/stars/Earth/sun, rocks, outpost layout, rings, beacons |
| Scripts/World/Astronaut.cs | CharacterController player, lunar gravity jump, Animator states |
| Scripts/World/Rover.cs | Scout rover driving, slope limit, battery use |
| Scripts/World/OrbitCamera.cs | Third-person orbit camera with spring arm + ground floor |
| Scripts/World/Interactable.cs | E-interaction registry + HUD markers |
| Scripts/World/SceneKit.cs | Camera + post-processing |
| Scripts/UI/Ui.cs, Resources/UI/Mohakash.uss | UI Toolkit helpers and stylesheet |
| Scripts/UI/MissionUi.cs | Briefing, loadout, HUD, systems panel, habitat panel, pause, report |
| Scripts/UI/MainMenu.cs, SettingsUi.cs, IntroPlayer.cs | Menu (PLAY -> Mission Select cards, How to play, Settings, Credits), settings, intro video scene |
| Editor/PlayFromIntro.cs | Editor Play always starts from Intro.unity |
| Scripts/Dev/AutoTest.cs | `-autotest <dir>` scripted playthrough with screenshots |
| Editor/ProjectSetup.cs | Generates materials, textures, terrain layer, PanelSettings, Animator, scenes, build list; Build macOS |
| Tests/EditMode/SimTests.cs | 10 simulation tests |
| Resources/Models/*.glb | CC0 Quaternius models |
| Resources/Animation, Resources/Materials, Resources/UI | Generated assets |
| Scenes/Intro, MainMenu, Mission1 | Generated bootstrap scenes (build order Intro, MainMenu, Mission1) |
| StreamingAssets/Intro/VIDEO-2026-10-08-16-07-16.mp4 | The team's intro video (played by IntroPlayer) |
| Docs/ (project root) | CHANGELOG, DECISIONS, TESTING, GDD_REQUIREMENTS |

## Architecture
`Mission1` (MonoBehaviour) owns a `MissionModel` (pure C#), a `MoonWorld`, the `Astronaut`, an optional `Rover`, an `OrbitCamera` and
`MissionUi`. Each frame: input -> world actions -> `Model.Advance(hours)` (1-minute ticks) -> sun/rings. The model raises `OnEvent`
(alerts, auto-slow) and keeps `Events`, `Data` (provenance) and `CauseChain`. At the end, `MissionReport` scores the model. UI only reads
the model/director and calls director methods. Scenes hold one bootstrap component; everything else is built at runtime from `Resources`.

## Implemented and tested (see Docs/TESTING.md)
Simple menus (Mission Select cards, plain-language briefing, standard-gear + extras loadout with an Advanced full cargo list), intro scene playing the team video `StreamingAssets/Intro/VIDEO-2026-10-08-16-07-16.mp4` (1280x720, 10 s; verified in the built app, Skip shown), main menu with 3D backdrop, missions lock state, settings, credits; Mission 1 complete loop with
briefing, loadout (mass/budget/warnings), animated human astronaut (walk/run/jump/interact/death), rover, seismometer carry/deploy,
radiation reading, samples, habitat (suit refill, rest, repair), systems panel (load switches, OGS modes, shadow forecast), scripted
shadow window / SPE / moonquake, failure states, report with cause chain and data provenance. 12/12 tests; scripted success (score 87)
and failure runs pass.

## Not implemented / placeholders
- Real NASA data (LOLA terrain/illumination, Apollo quake catalogue): terrain is procedural "inspired by". No Data Sources manifest screen.
- Construction grid placement (modules auto-placed), NAV/CAM items, map states, Judge Mode, glossary hover, audio, save/checkpoint.
- Missions 2-4 (shown locked). WebGL build not attempted.

## Known risks
1. Not yet played by a human with real mouse/keyboard (only scripted input). Mouse sensitivity may need tuning (Settings slider).
2. Balance: the recommended plan scored 87; the shadow window always sheds loads for ~1.5 h with 1 battery (intended teaching moment).
3. 1x = 30 s per mission hour (D4) - may feel slow; 4x inside the habitat.

## Controls
WASD move, mouse look, Shift sprint, Space jump, E interact/deploy, R rover in/out, Tab systems, 1/2/3 = 1x/2x/4x, P pause sim,
Esc pause menu, scroll wheel zoom, H / F1 controls list (also in main menu and pause menu).

## Next tasks (priority)
1. Owner plays Mission 1 with the TESTING.md checklist; fix feel/balance issues.
3. Real LOLA south-pole terrain: offline Python script -> 16-bit heightmap PNG for a 1 km tile + illumination curve; Data Sources screen.
4. Install WebGL module; first web build; document size/perf/video limits.
5. Mission 2 "Into the Shadow": crater-floor darkness, RELAY, ice prospecting (SPEC/ICE-D), WEU water, GEN option (reuse MissionModel patterns).
6. Audio (suit breathing, alarms), Judge Mode, construction placement.

## Milestone 03 - structure art pass (habitat + base buildings)
Replaced the 11 Quaternius structure GLBs in `Assets/Resources/Models/` with new procedural, textured sci-fi models modelled on the owner's reference
screenshots (white riveted panel hulls, gold geodesic domes, blue neon strips, yellow-rail lattice tower with guy wires, gold roof solar rails).
Terrain, rover (Rover.glb / RoundRover.glb), astronaut and all C# code are UNCHANGED. Same file names, same origins and footprints, so no code edits.

| File (Resources/Models) | Game role (MoonWorld.BuildOutpost) | New look |
|---|---|---|
| GeodesicDome.glb | HAB habitat (also main-menu backdrop) | gold-foil geodesic dome on a white panel foundation ring, neon base strip, beacon |
| Connector.glb | SUI airlock tunnel | white ribbed corridor, glowing hatch, portholes, neon underlight |
| HouseLong.glb | OGS oxygen generator | long hull module, roof fans, pipes, vent grilles, glowing door |
| HouseCylinder.glb | STO storage | cylindrical tank-hall, ribs, domed roof, hatch door |
| RoofAntenna.glb | COM antenna (origin: ground = local y 4.05, as before) | ~14 m tapered base + yellow cage + tank cluster + lattice mast + dishes + 4 guy wires |
| RoofRadar.glb | RAD monitor (same origin offset) | cabinet, sensor head, dish, trefoil plate |
| BaseLarge.glb | GAR garage (door faces local +Z) | bunker with side buttress wedges, roll-up door, gold roof solar rail, dish |
| SolarPanelGround.glb | SOL-S array (panel faces +Z) | tilted 2-panel cell array on pedestal |
| PickupCrate.glb | BAT battery | dark case, hull caps, LEDs, terminals |
| PickupJar.glb | TNK-O / TNK-W tanks (same file for both) | white gas bottle with blue band and valve |
| Spaceship.glb | Lander | gold-foil descent stage, white ascent stage, 4 legs, ladder |

Textures are embedded in each GLB (512 px hull/dark/gold/solar, PNG normal maps; ~9 MB total). `ModelMaterialFixer` already carries base-colour, normal,
emissive, metallic and roughness into URP Lit, so no setup is needed. Generator source: `tools/structures_v2/` (`python3 textures.py` then
`python3 build_structs.py <outdir>`; needs numpy, Pillow, matplotlib). All 11 GLBs pass the Khronos glTF validator with 0 errors.
NOT verified in Unity by me (no Unity here): in-engine look, lighting, and whether the Windows .exe needs a rebuild (it does, to include new models).
Known limits: TNK-O and TNK-W share one file so they look identical; lander/tower are larger than the Quaternius originals (tower ~14 m on purpose).
Possible follow-ups: distinct water/oxygen tank files (needs a 2-line MoonWorld.cs change), floodlight masts + landing pad as GLBs, dust berms around bases.

## Milestone 04 - new astronaut, casual crew member, bunk sleep animation
- NEW `Resources/Models/AstronautEVA.glb`: redesigned suit (white, navy stripes, dark visor with amber band, chest control box, wrist pad, steel gloves, backpack). Clips: Idle, Walk, Run, Jump, Wave, Interact, Death.
- NEW `Resources/Models/CrewCasual.glb`: suitless crew member (navy tee, grey joggers, white sneakers, short black hair). Same clips plus LieDown, Sleep (loop), GetUp. Same 17-bone skeleton names as the EVA rig.
- NEW `Scripts/Models/CrewRig.cs`: plays the baked clips through the legacy Animation component (glTFast default). No Animator controller needed.
- REWRITTEN `Scripts/World/Astronaut.cs`: loads both characters; `SetIndoors(bool)` swaps suit <-> casual; `SetSleeping(want,pos,yaw,exit)` runs Approach -> LieDown -> Sleep -> GetUp -> Leave. Old Quaternius Astronaut.glb, AstronautSuit.cs and LocomotionBlender.cs are no longer used by the player (left in project, harmless).
- `Mission1.cs`: EnterHabitat/ExitHabitat call `Player.SetIndoors`; new `SyncSleep()` each frame keeps the character in the bunk while `Model.Resting && Model.Inside`.
- `HabitatInterior.cs`: `BedPosition/BedYaw/BedExit` statics and `SetBedCovered()` (blanket rises over the sleeper).
NOT verified in Unity (no Unity here): compile, clip names after import, bed alignment. If the character stands in bind pose (arms down) the importer Animation Method is not Legacy.

## Milestone 05 - the astronaut moves: walk, run, jump, pick up, carry, put down, lie in bed
**Root causes found (the characters never animated in Unity):**
1. All GLB `.meta` files had `animationMethod: 2` = glTFast **Mecanim** (editor default). `CrewRig` needs **Legacy (1)**; with Mecanim there is no `Animation` component, so every character stayed in bind pose. Fixed in `AstronautEVA.glb.meta`, `CrewCasual.glb.meta`, `Astronaut.glb.meta`, and `Editor/CharacterImportFix.cs` forces Legacy on every re-import so it cannot regress.
2. The owner's new `AstronautEVA.glb` (2.2 MB textured suit, 14k vertices) was a **static mesh**: no skeleton, no skin, no clips. It was rigged with `tools/crew_v3/rig_eva.py` (A-pose bind, weights by capsule distance, hard rules for backpack/helmet/visor). Mesh, materials and textures are unchanged. The unrigged original is kept in `tools/crew_v3/original/`.

**Clips (both `AstronautEVA.glb` and `CrewCasual.glb`):** Idle Walk Run Jump Wave Interact Death LieDown Sleep GetUp **PickUp PutDown CarryIdle CarryWalk CarryRun**. Sockets on both: `Socket_Hold` (in front of chest, where a held crate sits), `Socket_HandL/R`, `Socket_Back`, EVA also `Socket_Helmet`.

**Code**
- `Scripts/World/Astronaut.cs` (rewritten): `PickUp(item, carriedScale, onGrab, keep, onDone)` walks to the item, crouches, grabs (onGrab fires at the grab frame), stands up holding it; `PutDown(point, placedScale, onPlaced)` lowers it, lets go, stands up; `IsCarrying`, `Busy`, `DropHeld()`. While carrying, locomotion plays CarryIdle/Walk/Run. Bunk sequence now works with whichever character is active. `Astronaut.CasualIndoors` (default true) = suit changes into the casual crew model inside the habitat; set false and the suited astronaut stays on and sleeps in the bunk.
- `Scripts/Models/CrewRig.cs`: `Socket(name)` lookup.
- `Scripts/Mission1.cs`: seismometer crate / deployed seismometer are picked up with the animation and carried in both hands (old back-mounted copy removed); E in open ground lowers it and places it 0.6 m ahead; regolith samples are crouched for, held, then bagged; E is ignored while the hands are busy; the rover can still be boarded while carrying. `DeploySeismometer(bool instant)` for scripted tests.
- `Scripts/Dev/AutoTest.cs`: waits lengthened for the new animations (pick-up ~3.5 s, deploy ~3.4 s, sample ~3.8 s).
- Preview of every pose: `Docs/astronaut_rig_preview.png` (CPU-skinned from the final GLB, orange box = held crate).

**Verified here:** both GLBs re-read the way Unity reads them (nodes, skin, channels), all 15 clips sampled and skinned on the CPU, hand targets solved to < 4 cm, hold pose puts both hands on the crate. **NOT verified (no Unity here):** C# compile (brace balance checked only), in-engine look of the skin weights at shoulders/knees, bed height for the suited astronaut, crate/rock alignment in the hands, timing of the grab/release moments (constants at the top of `Astronaut.cs`: PickGrabAt, PutReleaseAt, ...).
**Known limits:** suited astronaut lying in the bunk is supine on its life-support pack (raised 0.27 m), looks like lying on a backpack; the default keeps the casual model for sleeping. Skin weights are procedural: expect small stretching at the shoulder pads. Jump while carrying plays the normal Jump clip (arms open, crate stays at the chest).

## Continue in a new chat
Continue Mission Mohakash from this handoff. First inspect the attached ZIP and this handoff file. Do not restart, re-plan from scratch, or claim unfinished features are complete. Preserve existing working files, identify the next highest-priority task, implement it, test what is possible, and deliver an updated ZIP plus an updated MISSION_MOHAKASH_HANDOFF.md before the context limit.

## Milestone 06 - footprints, tyre tracks, crew model, habitat life (2026-10-16)
- **Ground prints**: `World/GroundPrints.cs` (pooled fading decals), `World/Footsteps.cs` (boot prints, landing prints, none indoors), `World/RoverTracks.cs` (two tread strips measured from the Wheel_* meshes).
- **CrewCasual.glb** rebuilt by `tools/crew_v3/crew_hq.py` (smooth lofted body, sculpted head, hair, hands, textures, smooth weights). Head is its own skinned mesh `Crew_Head` (shown alone inside the suit neck ring during helmet off/on).
- **AstronautEVA.glb** re-rigged: `rig_eva.py original/AstronautEVA_static_original.glb tmp.glb` then `split_helmet.py tmp.glb AstronautEVA.glb` (helmet = rigid node `EVA_Helmet` under Head). New clips on both characters: HelmetOff, HelmetOn, WalkBack (`suit_clips.py`, per-frame IK).
- **Airlock + suit dock sequence** (Mission1 `EnterRoutine` / `ExitRoutine`, Astronaut `SuitOff` / `SuitOn` coroutines): outer hatch slides open -> walk into the tunnel -> inner door (`SlidingDoor.cs`) opens from the new airlock vestibule -> walk to the open suit alcove -> helmet off onto the shelf -> step back out of the suit while it slides into the alcove. Leaving is the reverse. `EnterHabitatInstant/ExitHabitatInstant` kept for AutoTest.
- **Habitat**: normal gravity indoors (Astronaut.EarthGravity, 0.45 m hop); draped blanket over the sleeper's baked pose (`HabitatInterior.UpdateBlanket`); window is a portal camera at the real HAB showing terrain, Earth and sky with correct parallax (`World/HabitatWindow.cs`, `MoonWorld.CenterSky`).
- **HAB exterior** is now `SurfaceHabitat.glb` (NASA Artemis-style surface habitat: MLI-quilted horizontal module, landing legs, berthing adapter for the SUI tunnel, windows at +X z=-3.5, radiators, vertical solar mast, S-band dish, suitports + porch). Generator: `tools/structures_v2/surface_hab.py`. Interior got ceiling chamfers, overhead handrails, cargo transfer bags.
- Verified: all game scripts compile with Unity's Roslyn against the engine/package DLLs (0 errors, 0 warnings); characters and habitat checked in pyrender previews. **NOT verified in Unity**: the sequences' timing/camera framing, portal window image, blanket fit, hatch alignment with the Connector door.
- **Airlock cycle (M06c)**: Connector.glb rebuilt with a real open doorway + lit recess (`structures.py build_connector`); the sliding outer hatch covers it (`MoonWorld.AirlockInside/AirlockFront`). Between the doors, in the 2.8 m vestibule: coming in = repressurise 0 -> 101.3 kPa (red beacon, wall vents) then a dust-removal air shower while the astronaut spins 360; going out = suit leak check then depressurise to vacuum. Status panel (TextMesh) on the vestibule's outer wall, toasts explain each step (`HabitatInterior.Repressurize/Depressurize`).
- **Outer hatch (M06d)**: `World/HingedHatch.cs` replaces the sliding outer panels: button press at the panel beside the doorway (`MoonWorld.HatchPanelPoint`, `Astronaut.Press`) -> lamp blinks amber, lever turns, bolts retract -> seal break pop with a dust/air puff -> swings 100 deg outward on the +Z hinge with a small settle, recess light comes up; closing is the reverse. Suit is now put on deliberately at the alcove's "EVA suit" station (`Mission1.SuitUp`); the airlock refuses to cycle without it.
- **Airlock camera (M06e)**: `OrbitCamera.FollowShot(offset)` / `EndShot()` - during the airlock/hatch sequences the camera holds the astronaut's eye height (feet + 1.65 m) at a fixed world offset chosen on the open side of every wall (outside: +2.6 x, -0.7 z in front of the hatch; chamber: front-left coming in, behind-right going out), so it never swings through the tunnel or chamber walls. EndShot hands back to the orbit camera without a jump.
- **Walk-in surface airlock (M06f)**: Connector.glb is now a hollow chamber (floor y 1.22, ceiling 3.45, half-width 0.95 local; doorways at local x -0.68 outer / 2.45 inner; hoops outside the shell). `World/AirlockChamber.cs` sits inside it (inner SlidingDoor to the HAB, vents, air shower, beacon, pressure panel, Repressurize/Depressurize). Going in: button -> hatch opens and STAYS open -> camera follows the astronaut in -> hatch closes -> cycle -> inner door -> short fade only at the inner door into the habitat room doorway (`HabitatInterior.DoorwayPoint`). Going out mirrors it (fade at the room door into the chamber's inner doorway). The interior vestibule is now just a dark corridor behind the room door. Suit-up walks straight to the alcove (no stepping back), the suit slides out, one step in.
- **glTFast mirrors X on import (M06g)** - previews must apply diag(-1,1,1) before the spawn transform (see scratchpad-style `unity_view.py` approach). Fixes: SurfaceHabitat spawned at yaw 180 (adapter + windows on +X, windows at z +3.5/+2.6), Connector spawned at yaw 0 and raised (floor 0.57 m) with a 3-rise stair + handrails + legs, hab adapter raised to y 1.70 to meet it. Hatch opens on its own at the stair foot and stays open until the astronaut and camera are inside. Interior east wall now has the same two round portholes (z 3.5 / 2.6, 1.65 m) as the outside, each a round portal view; the outside -X windows were removed (no matching wall space inside).
- **NASA-referenced outpost hardware (M06h)**: `tools/structures_v2/nasa_structs.py` builds SolarArrayVSAT (Vertical Solar Array Technology; `Tracker` node turned to the Sun by `World/SunTracker.cs`), LunarBattery, OxygenPlant (electrolysis skid), OxygenTank / WaterTank (separate files now), LogisticsPallet (STO), CommsTerminal (dish spawned at yaw = EarthAzimuth), RadiationStation, RoverGarage. Real size, origin on the ground; MoonWorld.BuildOutpost spawns them at scale 1. The old HouseLong/HouseCylinder/RoofAntenna/RoofRadar/BaseLarge/SolarPanelGround GLBs are no longer used outdoors (PickupCrate/PickupJar still used for the seismometer crate and indoor props).
