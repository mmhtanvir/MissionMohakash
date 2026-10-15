# crew_v3 - rigging + animation tools (Milestone 05)

Pure Python (numpy, scipy, Pillow, matplotlib). No Unity needed.

| File | What it does |
|---|---|
| `rig_eva.py` | `python3 rig_eva.py original/AstronautEVA_static_original.glb out.glb` - takes the owner's static, unrigged AstronautEVA.glb (textured suit) and adds a 17-bone skeleton, skin weights, sockets and 15 clips. Meshes/materials/textures are untouched. |
| `poses_extra.py` | PickUp, PutDown, CarryIdle/Walk/Run. Forward kinematics + IK place the hands (carry pose holds a box at `Socket_Hold`; pick-up reaches the ground). Dimension tables `EVA` and `CASUAL`. |
| `crew.py` | CrewCasual generator (same as crew_v2) now with Socket_Hold/Back/HandL/HandR + the extra clips: `python3 -c "import crew;crew.build_casual().save('CrewCasual.glb')"` |
| `check_glb.py` | Reads a GLB the way Unity does (nodes, skin, animation channels), skins it on the CPU and renders contact sheets: `python3 check_glb.py Model.glb sheet.png` |
| `original/AstronautEVA_static_original.glb` | The owner's static model, kept so the rig can be re-run if the model changes. |

If you replace the astronaut mesh with another static model: put it in `original/`, adjust the joint positions in `rig_eva.py` (`SK`, `compute_weights`) to the new proportions, rerun, check with `check_glb.py`.
