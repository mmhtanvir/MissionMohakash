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

The game connects to these NASA services. They are shown live on the mission briefing screen ("REAL NASA DATA").
If there is no internet, the game still plays the same; the panel just stays hidden. Code: `Assets/Scripts/Sim/NasaData.cs`.

| NASA source | What the game does with it |
|---|---|
| **DONKI: Space Weather Database** ([official page](https://kauai.ccmc.gsfc.nasa.gov/DONKI), API via [api.nasa.gov](https://api.nasa.gov/)) | Asks `https://api.nasa.gov/DONKI/notifications` for the last 30 days of real Sun events (solar flares, CMEs, radiation storms, geomagnetic storms) and shows them as "Real Sun news", next to the game's own solar storm. |
| **Moon Trek** ([trek.nasa.gov/moon](https://trek.nasa.gov/moon/)) | Downloads the LRO WAC global mosaic tiles (`trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02`) and shows the real map of the Moon for Moon levels. |
| **Mars Trek** ([trek.nasa.gov/mars](https://trek.nasa.gov/mars/)) | Downloads the Viking MDIM 2.1 colour mosaic tiles (`trek.nasa.gov/tiles/Mars/EQ/Mars_Viking_MDIM21_ClrMosaic_global_232m`) and shows the real map of Mars for Mars levels. |

The API key is NASA's free `DEMO_KEY` (limited to 30 calls an hour). To use your own key, get one at [api.nasa.gov](https://api.nasa.gov/) and change `NasaData.ApiKey`.

### How the NASA data drives the dust storm mechanic

The dust storm in Level 6 ("Dust Storm Survival") is informed by real Martian science and NASA DONKI solar-event data:

- **DONKI solar events** surface as in-game alerts on the briefing screen. When a real CME or radiation storm appears in the last 30 days of DONKI data, the briefing flags it as "REAL SUN NEWS" so players see that space-weather hazards are not invented for the game.
- **Mars dust storm physics** are modelled from published NASA data: during a storm, solar irradiance at the Martian surface drops to roughly 25% of its clear-sky value ([NASA MRO/CRISM observations](https://mars.nasa.gov/mro/)) and dust settles on solar panels at ~3% coverage per hour (vs ~2% per day in calm conditions), matching the opacity and settling rates measured by the Mars Exploration Rover program.
- **Per-panel dust buildup**: each solar array accumulates its own dust layer independently. The orange-brown overlay colour (`#B87539`) matches the iron-oxide dust hue documented in Mars surface spectroscopy. The player must walk up to each panel individually and press **E** to wipe it — mirroring the real maintenance challenge faced by Mars landers (the Spirit and Opportunity rovers relied on wind events to clear their panels; MissionMohakash makes that cleaning an active player task).
- **Power model**: `Supply = SolarPeak × Sun × (1 − Dust)`. With a full storm (`Sun = 0.25`) and 50% dust buildup, a three-panel outpost delivers only `~37.5%` of its rated power — consistent with the severe power shortfalls recorded during the 2018 global Martian dust storm that ended the Opportunity rover mission.

## Design and AI tools

- Design (Figma): https://www.figma.com/design/Ylv8cr01wxyFhV6LQoHukZ/practices?node-id=314-2
- AI tools used: ChatGPT, Claude, Antigravity
