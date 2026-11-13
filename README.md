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

## Design and AI tools

- Design (Figma): https://www.figma.com/design/Ylv8cr01wxyFhV6LQoHukZ/practices?node-id=314-2
- AI tools used: ChatGPT, Claude, Antigravity, **Tripo3D**

### Tripo3D — AI-generated 3D models

All in-game 3D models (astronaut suit, habitat modules, rovers, solar arrays, equipment crates, rocks, and surface hardware) were generated using [Tripo3D](https://www.tripo3d.ai/), an AI text-to-3D model service. The workflow was:

1. Write a short text prompt describing the object (e.g. *"NASA lunar habitat module, white cylindrical pressurised shell, airlock on one end, solar-panel mast, realistic sci-fi"*).
2. Tripo3D generates a GLB/glTF mesh with UV-mapped textures.
3. The downloaded `.glb` files are placed in `Assets/Resources/Models/` and loaded at runtime via the `com.unity.cloud.gltfast` package — no Unity re-import step needed.
4. Materials are then overridden in code (`SurfaceKit.cs`) to apply procedural normal maps, hull-plating detail, and URP-compatible shaders so everything matches the game's visual style.
