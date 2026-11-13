# habitat_v3 - cross-shaped surface habitat + airlock capsule (Tripo models)

Sources (owner's Tripo GLBs): lunar habitat 3d model.glb (exterior), space station 3d model.glb (cutaway interior, used only
to check the fit), lunar habitat hatch 3d model.glb (airlock capsule + ladder). Python 3 + numpy, trimesh, pygltflib, Pillow;
gltfpack (npm) for UV-seam-aware simplification.

| Step | Command |
|---|---|
| fit the cutaway interior into the exterior (habitat units) | `python integrate.py exterior.glb interior.glb out/` |
| check the airlock geometry (new door, ladder, stow/deploy clearances) | `build.py` is the shared geometry step, imported by game_build.py |
| game files in metres + anchors | `python game_build.py build.py exterior.glb out/lunar_habitat_interior_fitted.glb hatch.glb astronaut.glb game/` |
| simplify + textures (8 MB) | `python game_pack.py game/ game/final/ <dir with node_modules/gltfpack>` -> `SurfaceHabitat.glb` |

`anchors.json` = every point MoonWorld.BuildOutpost / CapsuleHatch use (glTF metres; Unity world offset = (x, y, -z)).
Characters: `prep_char.py` (simplify + scale a static Tripo character), then `tools/crew_v3/rig_static.py`.
The habitat interior is built in code (World/HabitatInterior.cs); the generated cutaway did not hold up at eye level.
