# Mission Mohakash - Milestone 1 import (Unity 6.6 / URP, Apple Silicon Mac)

This is a **Unity-importable source bundle** (scripts + editor menu), not a full Unity project. Scenes are generated
inside Unity by one menu click, because hand-written scene files are fragile and I cannot run Unity here.

## Steps
1. Unzip this file. Close nothing; keep Unity open.
2. Easiest: open Terminal, type `bash ` (with a trailing space), drag `copy_into_project.sh` into the Terminal window,
   type a space, drag your **MissionMohakash project folder** into the window, press Return.
   Manual alternative: in Finder copy the `Assets/Scripts`, `Assets/Editor` and `Assets/Video` folders into your project's `Assets` folder.
3. Switch to Unity. Wait for the spinner (compiling). Open the **Console**. If you see red errors, copy them to me.
4. Menu bar: **Mission Mohakash > Create Milestone 1 Scene**. Click OK.
5. Press **Play**, then **click inside the Game view** (this locks the mouse).

## Input setting
Scripts use the new Input System if enabled. Check: Edit > Project Settings > Player > Other Settings > **Active Input Handling**
= "Input System Package (New)" or "Both". If it is "Input Manager (Old)" the code falls back to the old API automatically.
If keys do nothing, change it (Unity restarts) and press Play again.

## If the menu does not appear
Compile errors block menus. Fix the Console errors first. Manual fallback: File > New Scene > Empty, create an empty GameObject,
Add Component > MissionBootstrap, save, press Play.

See `Docs/TESTING.md` for the test checklist and `MISSION_MOHAKASH_HANDOFF.md` for full status.
