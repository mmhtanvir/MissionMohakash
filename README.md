# Mission Mohakash

You are an astronaut alone on the Moon. Pack your rocket, keep the outpost running (power, oxygen, water, food) and finish the science task before time runs out.

A NASA Space Apps 2026 learning game. Not an official NASA product.

---

## 1. Download

Pick your computer and click the link. Each download is about 350 MB.

| Computer | Download |
|---|---|
| Windows 10 / 11 (64-bit) | [MissionMohakash-Windows.zip](https://github.com/mmhtanvir/MissionMohakash/releases/latest/download/MissionMohakash-Windows.zip) |
| Mac (Apple Silicon or Intel) | [MissionMohakash-Mac.zip](https://github.com/mmhtanvir/MissionMohakash/releases/latest/download/MissionMohakash-Mac.zip) |
| Linux (64-bit) | [MissionMohakash-Linux.zip](https://github.com/mmhtanvir/MissionMohakash/releases/latest/download/MissionMohakash-Linux.zip) |

All downloads are also on the [Releases page](https://github.com/mmhtanvir/MissionMohakash/releases/latest).

## 2. Install and start

### Windows
1. Right-click the zip > **Extract All...** > **Extract**.
2. Open the new folder and double-click **MissionMohakash.exe**.
3. If a blue "Windows protected your PC" box appears: click **More info** > **Run anyway**.

Keep the `.exe` inside its folder. It needs the `MissionMohakash_Data` folder next to it.

### Mac
1. Double-click the zip. You get **MissionMohakash.app**. Drag it to your **Applications** folder.
2. The first time, **right-click** the app > **Open** > **Open**.
3. If macOS still says the app "is damaged" or "can't be opened", open **Terminal** and run:
   ```
   xattr -cr /Applications/MissionMohakash.app
   ```
   Then double-click the app again.

### Linux
1. Unzip it:
   ```
   unzip MissionMohakash-Linux.zip -d MissionMohakash
   cd MissionMohakash
   ```
2. Make it runnable and start it:
   ```
   chmod +x MissionMohakash.x86_64
   ./MissionMohakash.x86_64
   ```

## 3. Your first game

1. The intro video plays. Press **Skip** if you want.
2. On the main menu click **PLAY** and pick **Mission 1**.
3. Read the briefing, then **plan your loadout** (what goes on the rocket) and press **LAUNCH**.
4. Click once inside the game so the mouse turns the camera.
5. Follow the **"Next:"** hint in the top-right corner. It always tells you what to do.
6. Press **H** any time to see all controls. Press **Esc** to pause.

When the mission ends you get a report that explains *why* things went well or badly. Pass a mission to unlock the next one.

### Tips
- **Green circle** = OK, **yellow diamond** = warning, **red square** = danger.
- Outside, stay inside the **cyan ring**. Go too far and your suit runs out of oxygen before you get back.
- Running (Shift) uses suit oxygen faster.
- Inside the habitat you can speed up time with **2** or **3**.

## 4. Controls

**On foot**

| Key | What it does |
|---|---|
| W A S D or arrow keys | Walk |
| Mouse | Look around |
| Shift (hold) | Run |
| Space | Jump (slow, Moon gravity) |
| Mouse wheel | Zoom camera |
| E | Use / pick up / set down |
| T | Torch on / off |
| F | Inventory |
| Q | Wrist watch (habitat status) |
| V (inside habitat) | Drive the remote rover. C swaps camera. V or Esc to come back |

**Rover**

| Key | What it does |
|---|---|
| R | Get in / get out (stand next to it) |
| W / S | Forward / backward |
| A / D | Steer |
| Shift (hold) | Full speed |

**Mission**

| Key | What it does |
|---|---|
| M (hold) | Full map. Click to place a mark, right-click to remove |
| G | Drop a map mark where you stand |
| J | Mission plan board (your 72 h schedule) |
| O | Objectives |
| Tab | Power panel (switch machines on / off) |
| 1 / 2 / 3 | Time speed 1x / 2x / 4x (2x and 4x only inside the habitat) |
| P | Pause / resume mission time |
| H or F1 | Show / hide controls |
| Esc | Pause menu (frees the mouse) |

## 5. Problems?

| Problem | Fix |
|---|---|
| Mouse doesn't turn the camera | Click once inside the game window. |
| Mouse is stuck in the game | Press **Esc**. |
| Game is slow / choppy | Close other programs. On a laptop, plug in the charger. |
| Windows: "missing MissionMohakash_Data" or nothing happens | You started the `.exe` from inside the zip. Extract the zip first. |
| Mac: "app is damaged" | Run the `xattr -cr` command from the Mac steps above. |
| Linux: "Permission denied" | Run `chmod +x MissionMohakash.x86_64` first. |
| Want to replay locked missions from the start | Main menu > **SETTINGS** > **Reset progress**. |

---

## For developers

Unity **6000.6.5f1** (Universal 3D / URP). Open the folder in Unity Hub, then open `Assets/Scenes/Intro.unity` and press Play.
If materials look pink, run menu **Mission Mohakash > Rebuild Project Setup**.

Builds: menu **Mission Mohakash > Build macOS App / Build Windows EXE / Build Linux**. Output goes to `Builds/`.
More detail: `MISSION_MOHAKASH_HANDOFF.md`.

## NASA data and tools we used

The game fetches live data from three NASA services at mission briefing time and shows it in a "REAL NASA DATA" panel. All calls are optional — if there is no internet the game plays the same and the panel stays hidden. All API code is in `Assets/Scripts/Sim/NasaData.cs`.

---

### 1. DONKI — Space Weather Database

**What it is:** NASA's real-time space-weather notification feed ([kauai.ccmc.gsfc.nasa.gov/DONKI](https://kauai.ccmc.gsfc.nasa.gov/DONKI)), served through [api.nasa.gov](https://api.nasa.gov/).

**Exact endpoint called:**
```
GET https://api.nasa.gov/DONKI/notifications
        ?type=all
        &startDate=<30 days ago>
        &endDate=<today>
        &api_key=DEMO_KEY
```

**How it works in code (`NasaData.SpaceWeather`):**
1. On the briefing screen, `SpaceWeather(callback)` is called once per run. The result is cached so the network is only hit once.
2. `UnityWebRequest` fires the GET with a 15-second timeout.
3. The JSON response is a top-level array of notification objects, each with a `messageType` and `messageIssueTime`. Because Unity's `JsonUtility` requires a wrapper object, the code wraps the array: `JsonUtility.FromJson<Notes>("{\"a\":" + json + "}")`.
4. `Summarize()` counts each type — `FLR` (solar flares), `CME` (coronal mass ejections), `SEP` (radiation storms), `GST` (geomagnetic storms) — and builds one kid-readable sentence, e.g. *"In the last 30 days NASA saw 3 flashes (solar flares), 2 clouds of hot gas (CMEs). Latest: 9 Nov."*
5. If the Sun was quiet, it shows *"The real Sun was quiet this month: NASA saw no big storms."*
6. That sentence appears on the briefing screen next to the in-game solar storm so players can see that space-weather hazards are not invented for the game.

**Connection to the dust storm mechanic:** When DONKI reports real CMEs or radiation storms, the briefing flags the same week's conditions that the Level 6 dust storm simulates — grounding the fiction in real events.

---

### 2. Moon Trek — LRO WAC Global Mosaic

**What it is:** NASA's Moon Trek portal ([trek.nasa.gov/moon](https://trek.nasa.gov/moon/)) serving WMTS (Web Map Tile Service) tiles from the Lunar Reconnaissance Orbiter Wide Angle Camera global mosaic at 303 pixels-per-degree resolution.

**Exact tiles fetched:**
```
GET https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02
        /1.0.0/default/default028mm/0/0/0.jpg   ← western hemisphere
GET https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02
        /1.0.0/default/default028mm/0/0/1.jpg   ← eastern hemisphere
```

**How it works in code (`NasaData.TrekMap`):**
1. Both tiles are requested in parallel with `UnityWebRequestTexture.GetTexture`.
2. A counter (`left = 2`) decrements on each completion callback; only when both are done does the stitch run.
3. A new `Texture2D` is created at `(west.width + east.width) × height`, the two tile pixel arrays are written side-by-side with `SetPixels`, and `Apply()` uploads to the GPU.
4. The stitched texture is displayed as the world map on the Moon-level briefing screen, so players see the real lunar surface they are about to land on.

---

### 3. Mars Trek — Viking MDIM 2.1 Colour Mosaic

**What it is:** NASA's Mars Trek portal ([trek.nasa.gov/mars](https://trek.nasa.gov/mars/)) serving the Viking MDIM 2.1 global colour mosaic at 232 m/pixel resolution.

**Exact tiles fetched:**
```
GET https://trek.nasa.gov/tiles/Mars/EQ/Mars_Viking_MDIM21_ClrMosaic_global_232m
        /1.0.0/default/default028mm/0/0/0.jpg   ← western hemisphere
GET https://trek.nasa.gov/tiles/Mars/EQ/Mars_Viking_MDIM21_ClrMosaic_global_232m
        /1.0.0/default/default028mm/0/0/1.jpg   ← eastern hemisphere
```

**How it works in code:** Identical pipeline to Moon Trek above — parallel fetch, stitch, display. Shown on the Mars-level briefing screen (Level 6, "Dust Storm Survival") so players see the real Martian surface before landing.

---

### API key

All three services use NASA's free `DEMO_KEY` (30 calls/hour per IP, no sign-up needed). To raise the limit, get a personal key at [api.nasa.gov](https://api.nasa.gov/) and set `NasaData.ApiKey` in `Assets/Scripts/Sim/NasaData.cs`.

---

### How the NASA data drives the dust storm mechanic

The dust storm in Level 6 is grounded in real measurements:

- **Solar irradiance:** during a Martian dust storm, surface sunlight drops to ~25% of clear-sky value (matching MRO/CRISM observations). In code: `SunAt(h)` returns `sun * 0.25f` while `InStorm(h)` is true.
- **Dust accumulation rate:** ~3% panel coverage per hour in a storm vs ~2% per day in calm — matching settling rates measured by the Mars Exploration Rover program. Each panel tracks its own `PanelDust` value independently; the global `Dust = average(PanelDust)`.
- **Power model:** `Supply = SolarPeak × Sun × (1 − Dust)`. At full storm with 50% dust buildup, a three-panel outpost delivers ~37.5% of rated power — consistent with the power shortfalls recorded during the 2018 global Martian dust storm that ended the Opportunity rover mission.
- **Per-panel cleaning:** the player must walk to each panel and press **E** individually, mirroring the real challenge Spirit and Opportunity faced (they relied on wind events; MissionMohakash makes it an active task).
- **Dust colour:** the orange-brown overlay (`#B87539`) matches the iron-oxide dust hue documented in Mars surface spectroscopy.

## Sources, datasets and libraries

### Live NASA APIs

| Source | Used for |
|---|---|
| **NASA DONKI** — Space Weather Database ([api.nasa.gov/DONKI](https://api.nasa.gov/DONKI/notifications)) | Fetches the last 30 days of real solar flares (FLR), CMEs, radiation storms (SEP), and geomagnetic storms (GST). Summarised into one sentence on the mission briefing screen. Code: `NasaData.SpaceWeather()` |
| **Moon Trek WMTS** — LRO WAC Global Mosaic 303ppd v02 ([trek.nasa.gov/moon](https://trek.nasa.gov/moon/)) | Downloads 2 hemisphere tiles and stitches them into the real lunar map shown on Moon-level briefing screens. Code: `NasaData.TrekMap()` |
| **Mars Trek WMTS** — Viking MDIM 2.1 Colour Mosaic 232m ([trek.nasa.gov/mars](https://trek.nasa.gov/mars/)) | Same pipeline — real Mars map on the Level 6 (Dust Storm) briefing screen. Code: `NasaData.TrekMap()` |

### Scientific mission data & papers

These are not live calls — they are real-world numbers and mission results the simulation values were derived from.

| Reference | What it drives |
|---|---|
| **LRO instruments** (LOLA, LROC, Diviner, LEND) | Shackleton crater dimensions (21 km wide, 4.2 km deep), PSR temperatures (~40 K), rim illumination (86–92% annual), hydrogen/ice deposits. All shown in the Lunar Intelligence dashboard. |
| **Apollo 17 / Taurus–Littrow ALSEP** (1972) | Seismometer placement rule: ≥80 m from base noise. Moonquake mechanic references Apollo passive seismic network data. |
| **NASA EMU spacesuit specs** | `SuitO2Max = 8.5 h`, `SuitBattMax = 8.2 h`, `SuitCO2Max = 8.0 h` — modelled on the real EMU's ~8-hour EVA endurance. |
| **Water electrolysis stoichiometry** (ISS / Sabatier process) | OGS rate: `0.10 kg O2/h` consuming `1.125 L water/kg O2`. |
| **ISS CDRA CO₂ scrubber data** | Scrubber rate `0.06 kg CO₂/h`; crew metabolic output `0.042 kg CO₂/h`. |
| **NASA MOXIE experiment** (Perseverance rover, 2021) | On Mars levels, the OGS produces O₂ from atmospheric CO₂ instead of water — explicitly modelled on MOXIE. |
| **Mars Exploration Rover program** (Spirit & Opportunity) | Dust buildup rate (~3%/h in storm, ~2%/day calm) and the 2018 global dust storm that ended Opportunity. Drives the per-panel `PanelDust` mechanic. |
| **MRO/CRISM observations** | Storm solar irradiance `Sun × 0.25` during a Martian dust storm (`MissionModel.SunAt()`). |
| **Lunar synodic month** | `SynodicDayHours = 708.7` (29.53 Earth days). Drives sun position and shadow windows (`LunarClock.cs`). |
| **Lunar south pole solar geometry** | `EarthAzimuth = 85°, EarthElevation = 7°` — Earth's real position from the south pole. Sun elevation adapted for playability. |
| **Artemis III candidate landing sites** | Shackleton Crater Rim, Malapert Mountain, Connecting Ridge, de Gerlache Crater, Haworth Crater — real candidate zones with real coordinates, terrain, illumination, and comms data (`LunarIntelligenceUi.cs`). |
| **LCROSS impact mission** (2009) | Haworth Crater volatile data (water ice, methane, ammonia confirmed). |
| **NASA space radiation / NSRL data** | EVA dose rate `0.04 mSv/h`, SPE `6 mSv/h` outside, `0.01 mSv/h` inside habitat (`MissionModel.cs`). |
| **Mars iron-oxide dust spectroscopy** | Dust overlay colour `#B87539` matches the iron-oxide hue of real Martian dust (`SolarPanelDust.cs`). |

### Unity packages and libraries

| Package | Version | Purpose |
|---|---|---|
| **Unity 6** (Universal 3D / URP) | 6000.6.5f1 | Engine, rendering, physics, terrain |
| **Universal Render Pipeline** (`com.unity.render-pipelines.universal`) | 17.6.0 | URP shaders, post-processing, shadows |
| **glTFast** (`com.unity.cloud.gltfast`) | 6.20.0 | Runtime loading of Tripo3D-generated `.glb` models without re-import |
| **Input System** (`com.unity.inputsystem`) | 1.20.0 | Keyboard, mouse, gamepad, and virtual touch controls |
| **UI Toolkit / uGUI** (`com.unity.ugui`) | 2.6.0 | All in-game UI panels and USS stylesheets |
| **Test Framework** (`com.unity.test-framework`) | 1.8.0 | 12 NUnit EditMode simulation tests (`SimTests.cs`) |
| **Timeline** (`com.unity.timeline`) | 6.6.0 | Intro cinematic and landing sequence |
| **AI Navigation** (`com.unity.ai.navigation`) | 2.0.15 | NavMesh for astronaut walk-to-interaction pathfinding |
| **UnityWebRequest** (built-in) | — | DONKI API calls and Moon/Mars Trek tile downloads |

## Design and AI tools

- Design (Figma): https://www.figma.com/design/Ylv8cr01wxyFhV6LQoHukZ/practices?node-id=314-2

| AI tool | How we used it |
|---|---|
| **Claude** ([claude.ai](https://claude.ai/)) | Core development — writing and refactoring game code, simulation logic, UI, and build scripts |
| **Kiro** ([kiro.dev](https://kiro.dev/)) | Core development — agentic coding sessions: spec-driven feature implementation, code review, and iterative debugging directly in the codebase |
| **Antigravity** | Core development — additional code generation and debugging support |
| **Tripo3D** ([tripo3d.ai](https://www.tripo3d.ai/)) | 3D model generation — all in-game assets generated from text prompts and loaded as GLB files |
