# Decision log

| # | Decision | Reason |
|---|----------|--------|
| D1 | Full 3D third-person presentation, as requested. The GDD's camera direction (top-down/isometric per the master prompt) could NOT be checked because the GDD was not attached. GDD systems/learning goals are to be preserved when the GDD is available. | User request; GDD unavailable in chat 1 |
| D2 | Scene is generated at runtime by `MissionBootstrap`; an editor menu creates a tiny scene that holds only that component. No hand-written .unity/.prefab YAML. | Invalid hand-written serialization could corrupt the project; cannot run Unity to validate |
| D3 | One input wrapper (`InputReader`): new Input System when enabled, legacy only if project is Old-only. | Avoids silent mixing of input systems |
| D4 | HUD uses IMGUI (OnGUI), keyboard-only interaction. | No Canvas/EventSystem/asset dependencies for first slice. Replace later |
| D5 | All geometry is primitive placeholders; all numbers are fictional GAME UNITS. Gravity/jump tuned for feel, not real lunar values. | Master prompt rules 7-8 |
| D6 | Mission 1 prototype power budget: start 80; required tasks cost 70; optional relay 20; one-use solar charger +25 for -6 O2. The level is always solvable. | Gives a real resource trade-off. Numbers are game abstractions, NOT from the GDD |
| D7 | Web (WebGL/Unity Web) build not investigated yet; no browser claim made. `Shader.Find` is used for materials, which may be stripped in builds - to fix with real material assets in Milestone 4. | Honesty about risk |
