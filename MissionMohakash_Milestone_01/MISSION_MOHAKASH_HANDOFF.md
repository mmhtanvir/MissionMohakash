# MISSION MOHAKASH - HANDOFF (after Milestone 01: Milestones 0 + 1)

## Goal
Mission Mohakash (also "OUTPOST ZERO"): a full-3D third-person Unity/C# astronaut mission trainer. Four missions: 1 First Light (Moon),
2 Into the Shadow (Moon), 3 Jezero Air (Mars), 4 Arcadia Endurance (Mars). Priority is a playable Mission 1 vertical slice, then Mission 2.
Owner is a Unity beginner using free Claude.ai in a browser (no Claude Code); wants real files in a ZIP + this handoff after every milestone.

## Environment
- Unity 6.6 (6000.6.5f1), Apple Silicon Mac. Project folder `MissionMohakash`, template Universal 3D (URP).
- Source project assumption: standard layout (`Assets/`, `ProjectSettings/`). This bundle is a **source bundle**, not a whole project.
- Input: `InputReader` wrapper; new Input System if Active Input Handling = New or Both, else legacy. Render pipeline: URP (materials via Shader.Find).
- Packages needed: Input System (normally already in the URP template). No other dependencies.
- Build target / web: NOT investigated. No browser support claimed.

## What was NOT available in chat 1
- `Junior_Astronaut_Mission_Trainer_GDD.pdf` - not attached, NOT read. `Docs/GDD_REQUIREMENTS.md` only holds secondhand notes from the master prompt.
- `VIDEO-2026-10-08-16-07-16.mp4` - not attached, not included, nothing substituted.
- Unity itself: code has NOT been compiled or run by the author. Treat everything as **unverified until the owner tests it**.

## Import
See `README_IMPORT.md`: run `copy_into_project.sh <project path>` (or copy Assets/Scripts, Assets/Editor, Assets/Video), wait for compile,
menu **Mission Mohakash > Create Milestone 1 Scene**, press Play, click Game view.

## File inventory
| Path | Purpose |
|---|---|
| Assets/Scripts/Core/InputReader.cs | Only input access point (new/legacy by define) |
| Assets/Scripts/Core/Placeholder.cs | Primitive + URP material helpers (all placeholder visuals) |
| Assets/Scripts/Gameplay/MissionBootstrap.cs | Builds entire world at runtime: sun, ground, craters, rocks, mountains, Earth, stars, lander, 6 stations, player, camera, HUD |
| Assets/Scripts/Gameplay/MissionState.cs | Oxygen/power/science, objectives, data log (provenance), interaction rules, win/fail, report text |
| Assets/Scripts/Gameplay/Interactable.cs | Equipment data component + static registry |
| Assets/Scripts/Gameplay/PlayerController.cs | CharacterController movement, jump, sprint, nearest-interactable, E interact, procedural limb swing, area clamp (110 m) |
| Assets/Scripts/Gameplay/ThirdPersonCamera.cs | Orbit camera, spring-arm sphere cast, ground floor, cursor lock (Esc / click) |
| Assets/Scripts/Gameplay/AstronautPlaceholder.cs | Primitive astronaut with limb pivots |
| Assets/Scripts/UI/HudUI.cs | IMGUI HUD, prompt, toast, next-objective hint, mission report, R restart |
| Assets/Editor/MohakashSceneBuilder.cs | Menu that creates `Assets/Scenes/M1_Sandbox.unity` and adds it to Build Settings |
| Assets/Video/Intro/README.md | Where to put the MP4 |
| Docs/CHANGELOG.md, DECISIONS.md, TESTING.md, GDD_REQUIREMENTS.md | Log, decisions D1-D7, test checklist, secondhand GDD notes |
| README_IMPORT.md, copy_into_project.sh | Import instructions / helper |

## Architecture
`MissionBootstrap` (Awake) -> creates `MissionState` (singleton), world, `Interactable` stations, `Player` (`PlayerController` + `CharacterController` + `AstronautRig`),
camera (`ThirdPersonCamera`, target = player), `HudUI`. `PlayerController` finds nearest unused `Interactable` and calls `MissionState.Interact()`;
`MissionState` enforces requirements/costs, logs data, sets win/fail; `HudUI` reads MissionState + PlayerController only. Player is on layer 2 so the camera cast ignores it.

## Implemented (written, UNTESTED in Unity)
WASD camera-relative move, mouse look, Shift sprint, Space jump, Esc cursor release / click re-lock, spring-arm camera with ground floor,
placeholder astronaut with walk swing, HUD with labeled GAME UNITS, 6 interactable stations, resource trade-offs (sprint drains O2 2x; instruments cost power;
one-use solar charger +25 power for -6 O2; optional relay), airlock gated on seismometer + 2 samples, provenance data log, success/failure report, R restart.
Mission 1 prototype numbers are game abstractions (DECISIONS D6).

## Not implemented / placeholder
Intro video + menu (Milestone 2); briefing/loadout, EVA/suit safety, GDD-accurate Mission 1 content (needs GDD); real 3D models, rigged animation, Animator;
terrain/real Moon & Mars visuals; rover; Missions 2-4; audio; settings/credits; save data; WebGL/web build investigation; real UI (IMGUI is temporary);
mission lock/unlock screen. All visuals are primitives.

## Known risks
1. Never compiled - possible compile errors (report Console text to fix).
2. `Shader.Find` materials may be stripped in player/web builds.
3. Mouse sensitivity and jump/gravity feel unverified.
4. Restart needs the scene in Build Settings (menu does this).
5. GDD unread: mission content/values may not match it.

## Controls
WASD move, Shift sprint, Space jump, E interact, mouse look, Esc release cursor (click to re-lock), R restart after mission ends.

## Next tasks (priority)
1. Owner tests per Docs/TESTING.md and reports Console errors/behavior; fix them first.
2. Re-attach GDD PDF; write a real Docs/GDD_REQUIREMENTS.md; align Mission 1 (briefing, loadout, suit/EVA safety, seismometer logic, report wording).
3. Milestone 2: intro scene with VideoPlayer using the real MP4 (re-attach it), full-screen, aspect-preserving, Skip after delay, once per launch, main menu (Start, Missions, Settings/Credits).
4. Replace IMGUI HUD with real UI; add briefing + loadout screen.
5. Milestone 4: licensed/original astronaut model + Animator, Moon terrain; investigate Unity 6.6 web build target and document limits.
6. Milestone 5: Mission 2 "Into the Shadow" (ice, darkness, comms, rovers).

## Continue in a new chat
Continue Mission Mohakash from this handoff. First inspect the attached ZIP and this handoff file. Do not restart, re-plan from scratch, or claim unfinished features are complete. Preserve existing working files, identify the next highest-priority task, implement it, test what is possible, and deliver an updated ZIP plus an updated MISSION_MOHAKASH_HANDOFF.md before the context limit.
