"""Nest the cutaway interior inside the habitat exterior (same origin/units), check no poke-through.
usage: integrate.py exterior.glb interior.glb outdir"""
import sys, os, numpy as np, trimesh
ext_p, int_p, out = sys.argv[1:4]
YAW = 90         # interior N/S arms (narrow) -> exterior E/W arms (narrow)
SCALE = 0.92     # interior arms end inside the exterior end caps, hull ~5% inside (calibration knob)
NOZZLE_CUT = 0.12  # interior side nozzles near arm tips stick out past the exterior hull; hull itself is <0.115
LEG_CUT = 0.06   # interior faces fully below this are landing legs; exterior has its own

ext = trimesh.load(ext_p, force='mesh')
inter = trimesh.load(int_p, force='mesh')
inter.update_faces(~(inter.vertices[inter.faces][:, :, 1] < LEG_CUT).all(1))
inter.remove_unreferenced_vertices()
inter.apply_transform(trimesh.transformations.rotation_matrix(np.radians(YAW), [0, 1, 0]))
Vf = inter.vertices[inter.faces]
lat = np.where(np.abs(Vf[:, :, 0]) > np.abs(Vf[:, :, 2]), np.abs(Vf[:, :, 2]), np.abs(Vf[:, :, 0]))  # offset from arm axis
inter.update_faces(~((lat > NOZZLE_CUT) & (np.abs(Vf[:, :, [0, 2]]).max(2) > 0.16)).any(1))
inter.remove_unreferenced_vertices()

def arm_profile(V, n=12):
    """per arm, per bin along its axis: (max |lateral|, max y, min y) of hull above the legs"""
    prof = {}
    for name, ax, sgn in (('E', 0, 1), ('W', 0, -1), ('N', 2, -1), ('S', 2, 1)):
        lat = 2 - ax
        a = V[(V[:, ax] * sgn > 0.16) & (np.abs(V[:, lat]) < 0.14) & (V[:, 1] > LEG_CUT)]
        bins = np.linspace(0.16, 0.42, n + 1)
        idx = np.digitize(a[:, ax] * sgn, bins) - 1
        prof[name] = [(np.abs(b[:, lat]).max(), b[:, 1].max(), b[:, 1].min()) if len(b) else None
                      for b in (a[idx == i] for i in range(n))]
    return prof

pe = arm_profile(ext.vertices)
ey = (np.median([e[2] for k in pe for e in pe[k] if e]), np.median([e[1] for k in pe for e in pe[k] if e]))
s = SCALE
inter.apply_scale(s)
lo, hi = inter.bounds
inter.apply_translation([0, (ey[0] + ey[1]) / 2 - (lo[1] + hi[1]) / 2, 0])
print('scale %.3f  exterior arm y %.3f..%.3f  interior bounds %s' % (s, *ey, inter.bounds.round(3).tolist()))

# check: interior never wider/taller than the exterior hull at the same spot along each arm
pi = arm_profile(inter.vertices)
bad = [(k, j) for k in pe for j, (e, i) in enumerate(zip(pe[k], pi[k])) if e and i and (i[0] > e[0] or i[1] > e[1])]
print('poke-through bins:', [(k, j, [round(x, 3) for x in pe[k][j]], [round(x, 3) for x in pi[k][j]]) for k, j in bad] or 'none')

os.makedirs(out, exist_ok=True)
inter.export(os.path.join(out, 'lunar_habitat_interior_fitted.glb'), include_normals=True)
both = trimesh.Scene()
both.add_geometry(ext, node_name='exterior', geom_name='exterior')
both.add_geometry(inter, node_name='interior', geom_name='interior')
both.export(os.path.join(out, 'lunar_habitat_with_interior.glb'), include_normals=True)
print('wrote', out)
