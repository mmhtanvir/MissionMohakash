"""Assemble habitat + fitted interior + airlock capsule with new door + swing-down stairs + astronaut, in metres,
with glTF animations hatch_open / hatch_close.
usage: build.py exterior.glb interior_fitted.glb hatch.glb astronaut.glb out.glb"""
import sys, numpy as np, trimesh, pygltflib
from trimesh.transformations import rotation_matrix, quaternion_from_matrix
from trimesh.visual.material import PBRMaterial

ext_p, int_p, hatch_p, astro_p, out_p = sys.argv[1:6]
HAB = 23.0           # metres per habitat unit (4.4 m module diameter)
K = 4.45 * 1.25      # metres per hatch-model unit (2.0 m door opening: a suited astronaut with the pack walks through)
FLOOR = 0.090 * HAB  # east-arm interior floor height, measured
DOCK_X = 0.452 * HAB # east end cap, capsule back face goes here
SILL = np.array([0.0, 0.20, 0.27])    # hatch-local top of stairs (pivot), 5.5 cm proud of the frame so the folded ladder clears the door
HOLE_C = np.array([0.0, 0.39])        # hatch-local opening centre (x, y)
DOOR_R, DOOR_T = 0.19, 0.025
DOOR_FRONT = 0.215   # door flush with the frame front; it swings outward to the side, clear of the chamber
HINGE = np.array([DOOR_R + 0.01, HOLE_C[1], DOOR_FRONT])
DOOR_OPEN_DEG = 100  # positive = swings outward

def rot_x(deg, about):
    return trimesh.transformations.rotation_matrix(np.radians(deg), [1, 0, 0], about)

# ---------- split the hatch model ----------
h = trimesh.load(hatch_p, force='mesh')
c = h.triangles_center
old_door = (c[:, 0] > 0.12) & (c[:, 2] > 0.225) & (c[:, 1] > 0.2)
stairs = (c[:, 2] > 0.212) & (np.abs(c[:, 0]) < 0.075) & (c[:, 1] < 0.235)
legs = ~stairs & ((c[:, 1] < 0.10) | ((np.abs(c[:, 0]) > 0.245) & (c[:, 1] < 0.22)))
def sub(mask):
    m = h.copy(); m.update_faces(mask); m.remove_unreferenced_vertices(); return m
body = sub(~(old_door | stairs | legs))
st = sub(stairs); st.apply_translation([0, 0, SILL[2] - 0.215])

# stairs = ladder (swings down from the sill) + landing pad (fixed on the ground); scale both about the sill
# so the pad reaches the ground once the capsule is raised to FLOOR
s_st = (FLOOR / K) / (SILL[1] - st.vertices[:, 1].min())
cs = st.triangles_center
on_ground = cs[:, 1] < 0.03
ladder = st.copy(); ladder.update_faces(~on_ground); ladder.remove_unreferenced_vertices()
pad = st.copy(); pad.update_faces(on_ground & (cs[:, 2] > 0.31)); pad.remove_unreferenced_vertices()  # drop the part under the capsule
for m in (ladder, pad): m.apply_transform(trimesh.transformations.scale_matrix(s_st, SILL))
_, _, vt = np.linalg.svd(ladder.vertices[:, 1:] - ladder.vertices[:, 1:].mean(0))
d = np.array([0, *vt[0]]); d *= np.sign(d[2])                    # slope, top -> bottom
def angle_to(v_from, v_to):  # rotation about +x taking v_from to v_to (both in y-z plane)
    a = np.arctan2(v_from[2], v_from[1]); b = np.arctan2(v_to[2], v_to[1])
    return (np.degrees(b - a) + 180) % 360 - 180
STOW = angle_to(d, np.array([0, 1.0, 0.2]))   # stowed: upright with a slight outward lean

# ---------- new door (hinge-local) ----------
def part(mesh, rgba, metal=0.0, rough=0.6):
    mesh.visual = trimesh.visual.TextureVisuals(material=PBRMaterial(baseColorFactor=rgba, metallicFactor=metal, roughnessFactor=rough))
    return mesh
centre = np.array([HOLE_C[0], HOLE_C[1], DOOR_FRONT - DOOR_T / 2]) - HINGE
def at(m, off): m.apply_translation(centre + off); return m
door = trimesh.util.concatenate([
    at(part(trimesh.creation.cylinder(DOOR_R - 0.02, DOOR_T * 0.9, sections=64), [235, 235, 230, 255]), [0, 0, 0]),
    at(part(trimesh.creation.annulus(DOOR_R - 0.025, DOOR_R, DOOR_T * 1.2, sections=64), [140, 145, 150, 255], 0.8, 0.35), [0, 0, 0]),
    at(part(trimesh.creation.cylinder(0.04, DOOR_T * 1.3, sections=32), [40, 55, 70, 255], 0.2, 0.1), [0, 0.08, 0]),       # window
    at(part(trimesh.creation.box([0.015, 0.11, 0.015]), [190, 195, 200, 255], 0.9, 0.3), [-0.11, -0.02, DOOR_T * 0.9]),   # outer handle
    at(part(trimesh.creation.box([0.015, 0.11, 0.015]), [190, 195, 200, 255], 0.9, 0.3), [-0.11, -0.02, -DOOR_T * 0.9]),  # inner handle
    *[at(part(trimesh.creation.box([0.03, 0.04, 0.03]), [120, 125, 130, 255], 0.8, 0.4), [DOOR_R - 0.005, y, 0]) for y in (-0.11, 0.11)],
])

# ---------- check: stowed ladder clears the closed door and fits the capsule height; never below ground ----------
stowed = trimesh.transform_points(ladder.vertices, rot_x(STOW, SILL))
in_door = np.hypot(stowed[:, 0] - HOLE_C[0], stowed[:, 1] - HOLE_C[1]) < DOOR_R
assert (stowed[in_door, 2] > DOOR_FRONT + 0.035).all(), ('ladder hits door handle', stowed[in_door, 2].min())
assert stowed[:, 1].max() < body.vertices[:, 1].max(), ('stowed ladder taller than capsule', stowed[:, 1].max())
smooth = lambda f: f * f * (3 - 2 * f)
pose = {'hatch_door': lambda f: DOOR_OPEN_DEG * smooth(f), 'stairs': lambda f: STOW * (1 - smooth(f))}
for f in np.linspace(0, 1, 41):
    p = trimesh.transform_points(ladder.vertices, rot_x(pose['stairs'](f), SILL))
    assert ((p[:, 1] - SILL[1]) * K + FLOOR).min() > -0.03, ('ladder below ground at', f)
print('slope', d.round(3), 'stairs scale %.2f  stow %.0f deg  door clearance %.3f  stowed top %.3f / capsule %.3f' % (
    s_st, STOW, stowed[in_door, 2].min() - DOOR_FRONT, stowed[:, 1].max(), body.vertices[:, 1].max()))

# ---------- export meshes, then build node tree + animations in glTF ----------
ladder.apply_translation(-SILL)
ext = trimesh.load(ext_p, force='mesh'); inter = trimesh.load(int_p, force='mesh')
astro = trimesh.load(astro_p, force='mesh')
meshes = {'exterior': ext, 'interior': inter, 'airlock_body': body, 'hatch_door': door,
          'stairs': ladder, 'landing_pad': pad, 'astronaut': astro}
sc = trimesh.Scene()
for n, m in meshes.items(): sc.add_geometry(m, node_name=n, geom_name=n)
sc.export(out_p, include_normals=True)

g = pygltflib.GLTF2().load(out_p)
idx = {n.name: i for i, n in enumerate(g.nodes)}
for n in g.nodes: n.children, n.matrix = [], None
def q(deg, axis):  # glTF quaternion xyzw
    w, x, y, z = quaternion_from_matrix(rotation_matrix(np.radians(deg), axis)); return [float(x), float(y), float(z), float(w)]
def node(name, T=None, R=None, S=None, children=()):
    n = g.nodes[idx[name]]
    n.translation = None if T is None else [float(v) for v in T]
    n.rotation, n.scale = R, None if S is None else [float(S)] * 3
    n.children = [idx[c] for c in children]
    return idx[name]
airlock = len(g.nodes); g.nodes.append(pygltflib.Node(name='airlock', children=[idx['airlock_body'], idx['hatch_door'], idx['stairs'], idx['landing_pad']],
    translation=[DOCK_X + 0.49 * K, FLOOR - SILL[1] * K, 0.0], rotation=q(90, [0, 1, 0]), scale=[K] * 3))
node('exterior', S=HAB); node('interior', S=HAB); node('airlock_body')
node('hatch_door', T=HINGE)
node('stairs', T=SILL, R=q(STOW, [1, 0, 0])); node('landing_pad')
foot = trimesh.transform_points([pad.vertices.max(0) * [0, 1, 1]], rotation_matrix(np.radians(90), [0, 1, 0]))[0] * K
node('astronaut', T=[DOCK_X + 0.49 * K + foot[0] + 1.5, 0, foot[2] + 1.4], R=q(-90, [0, 1, 0]), S=1.9 / astro.extents[1])
g.scenes[g.scene or 0].nodes = [idx['exterior'], idx['interior'], airlock, idx['astronaut']]

# animations: door swings in (0-1.5 s), then the ladder swings down (1.5-3.5 s); close = reverse
blob = bytearray(g.binary_blob())
def acc(arr, typ):
    arr = np.asarray(arr, np.float32); off = len(blob); blob.extend(arr.tobytes())
    while len(blob) % 4: blob.append(0)
    g.bufferViews.append(pygltflib.BufferView(buffer=0, byteOffset=off, byteLength=arr.nbytes))
    a = pygltflib.Accessor(bufferView=len(g.bufferViews) - 1, componentType=pygltflib.FLOAT, count=len(arr), type=typ)
    if typ == 'SCALAR': a.min, a.max = [float(arr.min())], [float(arr.max())]
    g.accessors.append(a); return len(g.accessors) - 1
def anim(name, reverse):
    ch, sm = [], []
    tracks = [('hatch_door', [0, 1.5], [0, 1, 0]), ('stairs', [1.5, 3.5], [1, 0, 0])]
    for nname, (t0, t1), axis in tracks:
        f = np.linspace(0, 1, 25)   # 25 keys: <=15 deg per step keeps slerp on the intended side
        if reverse: t0, t1 = 3.5 - t1, 3.5 - t0
        times = t0 + f * (t1 - t0)
        rots = [q(pose[nname](1 - x if reverse else x), axis) for x in f]
        sm.append(pygltflib.AnimationSampler(input=acc(times, 'SCALAR'), output=acc(rots, 'VEC4'), interpolation='LINEAR'))
        ch.append(pygltflib.AnimationChannel(sampler=len(sm) - 1, target=pygltflib.AnimationChannelTarget(node=idx[nname], path='rotation')))
    g.animations.append(pygltflib.Animation(name=name, channels=ch, samplers=sm))
anim('hatch_open', False); anim('hatch_close', True)
g.buffers[0].byteLength = len(blob); g.set_binary_blob(bytes(blob))
g.save_binary(out_p)
print('wrote', out_p)
