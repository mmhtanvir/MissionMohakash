"""Game versions of the habitat (metres, glTF axes; Unity/glTFast mirrors X on import):
  SurfaceHabitat_src.glb  exterior + airlock capsule; door and stairs are pivot nodes (rest = closed / stowed)
  HabitatInterior_src.glb cutaway interior, floor of the airlock arm at y = 0
and anchors.json with every point the C# needs.
usage: game_build.py build.py exterior.glb interior_fitted.glb hatch.glb astronaut.glb outdir"""
import sys, os, json, numpy as np, trimesh
build_py, ext_p, int_p, hatch_p, astro_p, out = sys.argv[1:7]
src = open(build_py).read().split('# ---------- export meshes')[0]
sys.argv = [build_py, ext_p, int_p, hatch_p, astro_p, '/dev/null']
exec(compile(src, build_py, 'exec'))   # -> body, door (hinge-local), ladder, pad, STOW, SILL, HINGE, K, HAB, FLOOR, DOCK_X

R90 = rotation_matrix(np.radians(90), [0, 1, 0])
A = trimesh.transformations.translation_matrix([DOCK_X + 0.49 * K, FLOOR - SILL[1] * K, 0.0]) @ R90 @ trimesh.transformations.scale_matrix(K)
W = lambda p: trimesh.transform_points(np.atleast_2d(p), A)[0]

ext = trimesh.load(ext_p, force='mesh'); ext.apply_scale(HAB)
body.apply_transform(A); pad.apply_transform(A)
hinge_w, sill_w = W(HINGE), W(SILL)
door.apply_transform(R90 @ trimesh.transformations.scale_matrix(K))                      # hinge-local, metres
ladder.apply_transform(rot_x(STOW, SILL)); ladder.apply_transform(A); ladder.apply_translation(-sill_w)   # stowed, sill-local

sc = trimesh.Scene()
sc.add_geometry(ext, node_name='exterior', geom_name='exterior')
sc.add_geometry(body, node_name='airlock_body', geom_name='airlock_body')
sc.add_geometry(pad, node_name='landing_pad', geom_name='landing_pad')
sc.add_geometry(door, node_name='hatch_door', geom_name='hatch_door', transform=trimesh.transformations.translation_matrix(hinge_w))
sc.add_geometry(ladder, node_name='stairs', geom_name='stairs', transform=trimesh.transformations.translation_matrix(sill_w))
os.makedirs(out, exist_ok=True)
sc.export(os.path.join(out, 'SurfaceHabitat_src.glb'), include_normals=True)

inter = trimesh.load(int_p, force='mesh'); inter.apply_scale(HAB); inter.apply_translation([0, -FLOOR, 0])
inter.export(os.path.join(out, 'HabitatInterior_src.glb'), include_normals=True)

# ---- anchors (glTF metres; C# applies the X mirror) ----
# capsule chamber: floor = highest up-facing surface inside the capsule shell, below the sill
cb = body.triangles_center; up = body.face_normals[:, 1] > 0.9
inside = up & (cb[:, 0] > DOCK_X + 0.3) & (cb[:, 0] < sill_w[0] - 0.3) & (np.abs(cb[:, 2]) < 0.6) & (cb[:, 1] < sill_w[1] + 0.05)
hist, edges = np.histogram(cb[inside, 1], bins=60, weights=body.area_faces[inside])
cap_floor = float(edges[hist.argmax()])
deployed = trimesh.transform_points(ladder.vertices, rotation_matrix(np.radians(-STOW), [0, 0, -1]))   # check only
anch = {
    'hinge': hinge_w.tolist(), 'sill': sill_w.tolist(), 'door_radius': DOOR_R * K, 'door_open_deg_gltf_y': DOOR_OPEN_DEG,
    'stairs_axis_gltf': [0, 0, -1], 'stairs_deploy_deg_gltf': -STOW,
    'capsule_back_x': DOCK_X, 'capsule_floor_y': cap_floor, 'pad_bounds': pad.bounds.tolist(), 'body_bounds': body.bounds.tolist(),
    'deployed_stairs_bounds': (deployed + sill_w).min(0).tolist() + (deployed + sill_w).max(0).tolist(),
    'exterior_bounds': ext.bounds.tolist(), 'interior_bounds': inter.bounds.tolist(),
}
json.dump(anch, open(os.path.join(out, 'anchors.json'), 'w'), indent=1)
print(json.dumps(anch, indent=1))
