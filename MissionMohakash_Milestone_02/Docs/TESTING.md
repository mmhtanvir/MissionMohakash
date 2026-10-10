# Testing - Milestone 02

## What was actually run (2026-10-09, Unity 6000.6.5f1, Apple Silicon)
| Check | How | Result |
|---|---|---|
| Compile | Unity batch mode | 0 errors |
| EditMode tests (10) | `-runTests -testPlatform EditMode` | **10/10 pass** |
| macOS player build | `ProjectSetup.BuildMac` | Succeeded, 112 MB |
| Full playthrough (scripted) | built app with `-autotest <dir>` | Menu -> briefing -> loadout -> walk/sprint/jump -> pick up SEIS -> rover drive -> deploy SEIS 98 m out -> RAD reading -> 3 samples -> habitat -> systems panel -> shadow window -> report. **Score 87, 3/3 primary, moonquake recorded, no failure.** |
| Failure path (scripted) | required items + SEIS + 1 array only | **Fails at h39.4**, cause chain "CO2 built up -> crew health 0", score capped (19) |
| Screenshots | 16 images from the run | Reviewed by eye; lighting, HUD overlap and report layout fixed after the first run |

Not tested by a human with a real mouse/keyboard yet. Intro video verified in the built app: "[Intro] Playing VIDEO-2026-10-08-16-07-16.mp4 (1280x720, 10.0 s)", screenshot shows the video full screen with Skip, then the menu loads.

## Run the tests yourself
- Unity: Window > General > Test Runner > EditMode > Run All.
- Scripted playthrough: build (menu **Mission Mohakash > Build macOS App**), then in Terminal:
  `"Builds/Mac/MissionMohakash.app/Contents/MacOS/Mission Mohakash" -screen-fullscreen 0 -autotest ~/Desktop/mohakash_shots`

## Manual checklist (please tick and report)
- [ ] Intro: with your MP4 in `Assets/StreamingAssets/Intro/`, it plays full screen, Skip appears after 2 s, then the menu. Returning to the menu does not replay it.
- [ ] Menu buttons: Missions shows M1 playable, M2-M4 locked. Settings sliders work. Credits readable.
- [ ] Briefing -> Loadout: + / - change mass and cost bars; warnings change; LAUNCH disabled when over the limits.
- [ ] Mouse look after launch; WASD; Shift sprint; Space jump (slow, lunar); Esc opens the pause menu.
- [ ] E at the orange crate picks up the seismometer; orange ring shows 80 m; E outside the ring deploys (green light).
- [ ] R next to the rover drives it; R again to get out. Rover battery drops while driving.
- [ ] E at the RAD post logs a reading; E at cyan beacons collects samples; entering the habitat delivers them.
- [ ] Inside: suit refills; 1/2/3 keys change speed; Tab opens Systems; OGS OFF/AUTO/ON and load switches work.
- [ ] Around h22 a RAD alert appears; around h40 the sun goes and loads are shed if the battery runs out.
- [ ] Report appears at h72 (or on failure) with score, cause chain, data log. Retry and Main menu work.
