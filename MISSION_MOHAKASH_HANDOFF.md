# MISSION MOHAKASH - HANDOFF (Milestone 02)

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

## Continue in a new chat
Continue Mission Mohakash from this handoff. First inspect the attached ZIP and this handoff file. Do not restart, re-plan from scratch, or claim unfinished features are complete. Preserve existing working files, identify the next highest-priority task, implement it, test what is possible, and deliver an updated ZIP plus an updated MISSION_MOHAKASH_HANDOFF.md before the context limit.
