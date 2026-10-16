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

## Milestone 06 - high-quality CrewCasual

| File | What it does |
|---|---|
| `crew_hq.py` | `python3 crew_hq.py ../../Assets/Resources/Models/CrewCasual.glb /tmp/crew_tex` - builds the casual crew member from smooth lofted shapes (sculpted head, eyes, ears, hair shell, 5-finger hands, watch, sneakers, sleeve patches) with generated textures + normal maps (knit tee, fleece joggers, skin pores, painted face, hair strands, shoe) and smooth skin weights. Same skeleton, sockets and 15 clips as `crew.build_casual`; upper arms are bound in a 14 deg A-pose. |
| `preview_hq.py` | `python3 preview_hq.py Model.glb out.png bind|face|poses` - textured pyrender preview with CPU skinning (needs pyrender + PyOpenGL >= 3.1.9). |
| `original/CrewCasual_v2_blocky.glb` | The previous primitive-built model, kept for rollback. |

`glbkit.py` now accepts per-vertex smooth weights (`geo.JW = (joints[n,4], weights[n,4])`).
| `suit_clips.py` | HelmetOff / HelmetOn / WalkBack clips (per-frame IK along a wrist path); used by rig_eva.py and crew_hq.py. |
| `split_helmet.py` | `python3 split_helmet.py rigged.glb AstronautEVA.glb` - moves the helmet into a rigid `EVA_Helmet` node under Head. |
