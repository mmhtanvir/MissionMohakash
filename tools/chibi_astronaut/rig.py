# Put the chibi "space astronaut" mesh on the AstronautEVA skeleton (keeps its 20 legacy clips, sockets, helmet/pack nodes).
# blender -b -P rig.py -- AstronautEVA.glb SpaceAstronaut.glb EvaBackpack.glb HelmetCamera.glb out.glb
import bpy, sys, numpy as np
from mathutils import Vector, Matrix

eva, astro, backpack, camera, out = sys.argv[sys.argv.index("--") + 1:]
H = 1.8                      # final height in metres (CharacterController is 1.8 m)
bpy.ops.wm.read_factory_settings(use_empty=True)

def imp(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

def joined_mesh(objs, name):
    ms = [o for o in objs if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in ms: o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1: bpy.ops.object.join()
    m = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in objs:
        if o.name in bpy.data.objects and o != m and o.type != 'MESH': bpy.data.objects.remove(o)
    m.name = name
    return m

# ---------------------------------------------------------------- original rig
evaobjs = imp(eva)
arm = next(o for o in evaobjs if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'; bpy.context.view_layer.update()
J = {b.name: (arm.matrix_world @ b.head_local) for b in arm.data.bones}
kids = {o.name: o.matrix_world.copy() for o in evaobjs if o.parent == arm}

# ---------------------------------------------------------------- chibi body, scaled to H, feet at 0, centred
body = joined_mesh(imp(astro), "ChibiBody")
V = np.array([v.co[:] for v in body.data.vertices])
mn, mx = V.min(0), V.max(0)
s = H / (mx[2] - mn[2])
V = (V - np.array([(mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]])) * s
n = lambda f: f * H       # fractions of height measured on the model (front/side renders + slices)

def centroid(mask):
    return V[mask].mean(0)

x, y, z = V[:, 0], V[:, 1], V[:, 2]
side = {"Left": 1, "Right": -1}   # character faces -Y, so its left is +X (same as the EVA rig)
L = {}
for sd, sg in side.items():
    armv = (sg * x > n(0.16)) & (z > n(0.28)) & (z < n(0.58))
    L[sd + "Hand_c"] = centroid(armv & (z < n(0.37)))
    L[sd + "Wrist"] = centroid(armv & (z > n(0.37)) & (z < n(0.41)))
    L[sd + "Elbow"] = centroid((sg * x > n(0.19)) & (z > n(0.43)) & (z < n(0.47)))
    e = L[sd + "Elbow"]
    L[sd + "Shoulder"] = np.array([sg * n(0.16), e[1], n(0.55)])
    legv = (sg * x > 0) & (z < n(0.25))
    L[sd + "Knee"] = centroid(legv & (z > n(0.17)) & (z < n(0.20)))
    L[sd + "Ankle"] = centroid(legv & (z > n(0.085)) & (z < n(0.105)))
    k = L[sd + "Knee"]
    L[sd + "Hip"] = np.array([sg * n(0.085), k[1], n(0.29)])
for j in ("Hand_c", "Wrist", "Elbow", "Shoulder", "Knee", "Ankle", "Hip"):   # left/right symmetric
    a, b = L["Left" + j], L["Right" + j]
    m = np.array([(a[0] - b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2])
    L["Left" + j] = m; L["Right" + j] = m * [-1, 1, 1]
ty = (L["LeftHip"][1] + L["RightHip"][1]) / 2
T = {"Hips": [0, ty, n(0.305)], "Spine": [0, ty, n(0.36)], "Chest": [0, ty, n(0.44)], "Neck": [0, ty, n(0.59)], "Head": [0, ty, n(0.625)]}
T = {k: np.array(v, float) for k, v in T.items()}
print("LANDMARKS", {k: np.round(v, 3).tolist() for k, v in L.items()})

# ---------------------------------------------------------------- weights (distance to bone segments, by region)
def seg_d(P, a, b):
    ab = b - a; t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)

segs = {"Hips": (T["Hips"], T["Spine"]), "Spine": (T["Spine"], T["Chest"]), "Chest": (T["Chest"], T["Neck"]),
        "Neck": (T["Neck"], T["Head"]), "Head": (T["Head"], T["Head"] + [0, 0, n(0.35)])}
for sd in side:
    segs[sd + "UpperArm"] = (L[sd + "Shoulder"], L[sd + "Elbow"])
    segs[sd + "LowerArm"] = (L[sd + "Elbow"], L[sd + "Wrist"])
    segs[sd + "Hand"] = (L[sd + "Wrist"], 2 * L[sd + "Hand_c"] - L[sd + "Wrist"])
    segs[sd + "UpperLeg"] = (L[sd + "Hip"], L[sd + "Knee"])
    segs[sd + "LowerLeg"] = (L[sd + "Knee"], L[sd + "Ankle"])
    segs[sd + "Foot"] = (L[sd + "Ankle"], L[sd + "Ankle"] + [0, -n(0.08), -n(0.09)])
names = list(segs)
D = np.stack([seg_d(V, *segs[b]) for b in names], 1)
allow = np.zeros_like(D, bool)
bi = {b: i for i, b in enumerate(names)}
torso = ["Hips", "Spine", "Chest", "Neck"]
for sd, sg in side.items():
    armv = (sg * x > n(0.16)) & (z > n(0.27)) & (z < n(0.57))
    legv = (sg * x >= 0) & (z < n(0.30))
    for b in ("UpperArm", "LowerArm", "Hand"): allow[armv, bi[sd + b]] = True
    allow[armv, bi["Chest"]] = True
    for b in ("UpperLeg", "LowerLeg", "Foot"): allow[legv, bi[sd + b]] = True
    allow[legv & (z > n(0.22)), bi["Hips"]] = True
armany = allow[:, [bi[s + "Hand"] for s in side]].any(1)
legany = allow[:, [bi[s + "Foot"] for s in side]].any(1)
rest = ~armany & ~legany
helmet = rest & (z > n(0.61))
for b in torso: allow[rest & ~helmet, bi[b]] = True
allow[rest & (z > n(0.56)), bi["Head"]] = True
allow[helmet, :] = False; allow[helmet, bi["Head"]] = True        # helmet is rigid
W = np.where(allow, 1.0 / (D ** 2 + 1e-5) ** 2, 0.0)
# blend only with a bone next to the nearest one (a hand half-tied to the chest stretches into a spike when the arm moves)
pairs = [("Hips", "Spine"), ("Spine", "Chest"), ("Chest", "Neck"), ("Neck", "Head")]
for sd in side:
    pairs += [("Chest", sd + "UpperArm"), (sd + "UpperArm", sd + "LowerArm"), (sd + "LowerArm", sd + "Hand"),
              ("Hips", sd + "UpperLeg"), (sd + "UpperLeg", sd + "LowerLeg"), (sd + "LowerLeg", sd + "Foot")]
adj = np.eye(len(names), dtype=bool)
for a, b in pairs: adj[bi[a], bi[b]] = adj[bi[b], bi[a]] = True
best = W.argmax(1)
W = np.where(adj[best], W, 0.0)
top2 = np.argsort(-W, 1)[:, :2]
W2 = np.zeros_like(W); r = np.arange(len(W))[:, None]
W2[r, top2] = W[r, top2]; W = W2 / W2.sum(1, keepdims=True)

# cut the faces that weld parts which are not neighbours in the skeleton (the AI mesh fuses the hands to the thighs/belt;
# those faces would stretch into spikes as soon as an arm moves). They only sit where the parts touch, so no hole shows.
import bmesh
best = W.argmax(1)
bm = bmesh.new(); bm.from_mesh(body.data); bm.faces.ensure_lookup_table()
bad = [f for f in bm.faces if not all(adj[best[a.index], best[b.index]] for a in f.verts for b in f.verts)]
bmesh.ops.delete(bm, geom=bad, context='FACES_ONLY')
bm.to_mesh(body.data); bm.free(); body.data.update()
print("BRIDGE_FACES_CUT", len(bad))

# ---------------------------------------------------------------- pose limbs to the EVA rest directions (linear blend skinning)
def rot_between(a, b):
    a = Vector(a).normalized(); b = Vector(b).normalized()
    return np.array(a.rotation_difference(b).to_matrix())

Xf = {b: (np.eye(3), np.zeros(3)) for b in names}   # v' = R v + t
F = {k: v.copy() for k, v in T.items()}
for sd in side:
    for a, bname, c, j0, j1, j2 in (("UpperArm", "LowerArm", "Hand", "Shoulder", "Elbow", "Wrist"),
                                    ("UpperLeg", "LowerLeg", "Foot", "Hip", "Knee", "Ankle")):
        P0, P1, P2 = L[sd + j0], L[sd + j1], L[sd + j2]
        A0, A1, A2 = (np.array(J[sd + a]), np.array(J[sd + bname]), np.array(J[sd + c]))
        R1 = rot_between(P1 - P0, A1 - A0)
        Xf[sd + a] = (R1, P0 - R1 @ P0)
        P1n = R1 @ (P1 - P0) + P0
        R2 = rot_between(R1 @ (P2 - P1), A2 - A1) @ R1
        Xf[sd + bname] = (R2, P1n - R2 @ P1)
        Xf[sd + c] = Xf[sd + bname]
        F[sd + a] = P0; F[sd + bname] = P1n; F[sd + c] = R2 @ P2 + Xf[sd + bname][1]
Vp = np.zeros_like(V)
for i, b in enumerate(names):
    R, t = Xf[b]
    Vp += W[:, i:i + 1] * (V @ R.T + t)
for i, v in enumerate(body.data.vertices): v.co = Vector(Vp[i])
body.data.update()

# ---------------------------------------------------------------- move the EVA joints onto the chibi body (same bone orientations)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
inv = arm.matrix_world.inverted()
for eb in arm.data.edit_bones:
    d = eb.tail - eb.head
    eb.head = inv @ Vector(F[eb.name]); eb.tail = eb.head + d
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()

# hips translation keys were authored for a 0.9 m pelvis
hr = F["Hips"][2] / J["Hips"].z
for act in bpy.data.actions:
    fcs = list(getattr(act, "fcurves", []) or [])
    for l in getattr(act, "layers", []):
        for st in l.strips:
            for cb in st.channelbags: fcs += list(cb.fcurves)
    for fc in fcs:
        if fc.data_path == 'pose.bones["Hips"].location':
            for kp in fc.keyframe_points:
                kp.co[1] *= hr; kp.handle_left[1] *= hr; kp.handle_right[1] *= hr

# ---------------------------------------------------------------- body skin
body.parent = arm
for b in names:
    vg = body.vertex_groups.new(name=b)
    col = W[:, bi[b]]
    for i in np.nonzero(col > 1e-4)[0]: vg.add([int(i)], float(col[i]), 'REPLACE')
mod = body.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
old = next(o for o in evaobjs if o.name.startswith("GLTF.") and o.type == 'MESH')
bpy.data.objects.remove(old)

# ---------------------------------------------------------------- sockets and suit parts
Vb = Vp
front = lambda zlo, zhi: Vb[(Vb[:, 2] > zlo) & (Vb[:, 2] < zhi) & (np.abs(Vb[:, 0]) < n(0.08)), 1].min()
back = lambda zlo, zhi: Vb[(Vb[:, 2] > zlo) & (Vb[:, 2] < zhi) & (np.abs(Vb[:, 0]) < n(0.08)), 1].max()
hz = n(0.80)                                              # helmet centre height
hw = Vb[Vb[:, 2] > n(0.62), 0].max()                      # helmet half width
def place(name, pos):
    o = bpy.data.objects[name]; m = o.matrix_world.copy(); m.translation = Vector(pos); o.matrix_world = m
place("Socket_Helmet", (0, front(n(0.7), n(0.9)) * 0.9, n(0.88)))
place("Socket_Hold", (0, front(n(0.38), n(0.48)) - 0.14, n(0.40)))
place("Socket_Back", (0, back(n(0.42), n(0.55)) + 0.12, n(0.47)))
for sd in side:
    o = f"Socket_Hand{sd[0]}"
    off = kids[o].translation - J[sd + "Hand"]
    place(o, Vector(F[sd + "Hand"]) + off * 0.6)

def as_part(name, glb, size, centre, yaw):
    """Give suit node `name` (kept for the game's hinge / helmet code) the mesh from `glb`, `size` m tall, centred at `centre`."""
    node = bpy.data.objects[name]
    m = joined_mesh(imp(glb), name + "_src")
    P = np.array([v.co[:] for v in m.data.vertices]); lo, hi = P.min(0), P.max(0)
    k = size / (hi[2] - lo[2]); c = (lo + hi) / 2
    R = np.array(Matrix.Rotation(np.radians(yaw), 3, 'Z'))
    Pw = ((P - c) * k) @ R.T + np.array(centre)
    ninv = node.matrix_world.inverted()
    for i, v in enumerate(m.data.vertices): v.co = ninv @ Vector(Pw[i])
    m.data.update(); old = node.data; node.data = m.data
    bpy.data.objects.remove(m)
    if old and old.users == 0: bpy.data.meshes.remove(old)

pz = n(0.465); pd = n(0.13)
as_part("EVA_Pack", backpack, n(0.30), (0, back(n(0.40), n(0.55)) + pd / 2 - 0.02, pz), 180)   # tanks face backwards
# helmet + hatch frame live in the chibi mesh now: keep the nodes (game code moves them) but drop their old meshes
for nm in ("EVA_Helmet", "EVA_HatchFrame"):
    o = bpy.data.objects[nm]; e = bpy.data.objects.new(nm + "_tmp", None)
    e.parent = o.parent; e.parent_type = o.parent_type; e.parent_bone = o.parent_bone; e.matrix_world = o.matrix_world
    bpy.context.scene.collection.objects.link(e); bpy.data.objects.remove(o); e.name = nm
# helmet camera on the right side of the helmet (character right = -X), on the Head bone
cam = joined_mesh(imp(camera), "HelmetCamera")
P = np.array([v.co[:] for v in cam.data.vertices]); lo, hi = P.min(0), P.max(0); k = n(0.07) / (hi[2] - lo[2])
c = np.array([-(hw + n(0.02)), 0, hz + n(0.04)])
for i, v in enumerate(cam.data.vertices): v.co = Vector((P[i] - (lo + hi) / 2) * k + c)
cam.data.update()
mw = cam.matrix_world.copy(); cam.parent = arm; cam.parent_type = 'BONE'; cam.parent_bone = "Head"; cam.matrix_world = mw

arm.data.pose_position = 'POSE'
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_animations=True, export_image_format='JPEG')
print("RIGGED", out, "hips ratio", round(hr, 3))
