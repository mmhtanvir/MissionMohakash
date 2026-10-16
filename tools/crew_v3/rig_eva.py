"""
rig_eva.py - turns the owner's STATIC AstronautEVA.glb (textured suit, no skeleton, no animation) into a rigged,
animated character, keeping every mesh, material and texture byte-for-byte.

  python3 rig_eva.py <static_in.glb> <rigged_out.glb>

Adds: 17-bone skeleton (same names as CrewCasual.glb: Hips Spine Chest Neck Head Left/Right UpperArm LowerArm Hand
UpperLeg LowerLeg Foot), sockets (Socket_Hold, Socket_HandL/R, Socket_Back, Socket_Helmet), skin weights, and clips:
Idle Walk Run Jump Wave Interact Death LieDown Sleep GetUp PickUp PutDown CarryIdle CarryWalk CarryRun.
The static model is in an A-pose; bones are aligned to the limbs and clips are authored for a hanging-arm skeleton,
so the A-pose only exists as the bind pose.
"""
import sys, json, struct, math
import numpy as np
import astronaut as AS
from astronaut import BONES, FPS, Pose, ss
import poses_extra as PX
from glbkit import qe, qaxis, qmul, qmat

CT = {5121: np.uint8, 5126: np.float32, 5123: np.uint16, 5125: np.uint32}

def load(path):
    b = open(path, 'rb').read()
    L = struct.unpack('<I', b[12:16])[0]; j = json.loads(b[20:20 + L])
    off = 20 + L; bl = struct.unpack('<I', b[off:off + 4])[0]
    return j, bytearray(b[off + 8:off + 8 + bl])

def read_acc(j, bin_, i):
    a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
    n = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
    dt = np.dtype(CT[a['componentType']]); start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    return np.frombuffer(bytes(bin_), dtype=dt, count=a['count'] * n, offset=start).reshape(a['count'], n)

# ------------------------------------------------------------------ skeleton (EVA, bind = the model's A-pose)
ARM_ANG = 21.0     # deg, A-pose arm angle from vertical
LEG_ANG = 5.0
D = PX.EVA
SK = {  # name: (parent, local translation, bind rotation quat)
    'Hips':  (None,   (0, D['hips_y'], 0), None),
    'Spine': ('Hips', (0, D['spine'], 0), None),
    'Chest': ('Spine', (0, D['chest'], 0), None),
    'Neck':  ('Chest', (0, D['neck'], 0), None),
    'Head':  ('Neck', (0, D['head'], 0), None),
}
for side, sx in (('Left', 1), ('Right', -1)):
    SK[side + 'UpperArm'] = ('Chest', (sx * D['sh_x'], D['sh_y'], 0), qaxis('z', sx * ARM_ANG))
    SK[side + 'LowerArm'] = (side + 'UpperArm', (0, -D['ua'], 0), None)
    SK[side + 'Hand'] = (side + 'LowerArm', (0, -D['la'], 0), None)
    SK[side + 'UpperLeg'] = ('Hips', (sx * D['hip_x'], D['hip_dy'], 0), qaxis('z', sx * LEG_ANG))
    SK[side + 'LowerLeg'] = (side + 'UpperLeg', (0, -D['ul'], 0), None)
    SK[side + 'Foot'] = (side + 'LowerLeg', (0, -D['ll'], 0), qaxis('z', -sx * LEG_ANG))   # foot back to level
SOCK = {  # name: (parent, local t)
    'Socket_Hold': ('Chest', D['hold']), 'Socket_Back': ('Chest', (0, 0.0, -0.45)),
    'Socket_HandL': ('LeftHand', (0, -0.08, 0)), 'Socket_HandR': ('RightHand', (0, -0.08, 0)),
    'Socket_Helmet': ('Head', (0, 0.18, 0.22)),
}

def bind_world():
    W = {}
    def rec(n):
        par, t, q = SK[n]; M = np.eye(4); M[:3, 3] = t
        if q is not None: M[:3, :3] = qmat(q)
        W[n] = (W[par] if par else np.eye(4)) @ M
    for n in SK:   # dict order is parent-first
        rec(n)
    return W

# ------------------------------------------------------------------ skin weights
def seg_dist(P, a, b):
    ab = b - a; t = np.clip(((P - a) @ ab) / (ab @ ab + 1e-12), 0, 1)
    c = a + t[:, None] * ab; return np.linalg.norm(P - c, axis=1)

def compute_weights(P, prim_k):
    W = bind_world(); pos = {n: W[n][:3, 3] for n in W}
    def dirv(n, L): return (W[n][:3, :3] @ np.array([0, -1.0, 0])) * L
    segs = {
        'Hips': (np.array([0, .80, 0]), np.array([0, .98, 0]), .22),
        'Spine': (np.array([0, 1.0, 0]), np.array([0, 1.20, 0]), .22),
        'Chest': (np.array([0, 1.20, 0]), np.array([0, 1.42, 0]), .28),
        'Neck': (np.array([0, 1.42, 0]), np.array([0, 1.52, 0]), .12),
        'Head': (np.array([0, 1.52, 0]), np.array([0, 1.75, 0]), .27),
    }
    for s in ('Left', 'Right'):
        segs[s + 'UpperArm'] = (pos[s + 'UpperArm'], pos[s + 'LowerArm'], .105)
        segs[s + 'LowerArm'] = (pos[s + 'LowerArm'], pos[s + 'Hand'], .095)
        segs[s + 'Hand'] = (pos[s + 'Hand'], pos[s + 'Hand'] + dirv(s + 'UpperArm', .17), .10)
        segs[s + 'UpperLeg'] = (pos[s + 'UpperLeg'], pos[s + 'LowerLeg'], .15)
        segs[s + 'LowerLeg'] = (pos[s + 'LowerLeg'], pos[s + 'Foot'], .125)
        segs[s + 'Foot'] = (pos[s + 'Foot'], pos[s + 'Foot'] + np.array([0, -.10, .13]), .14)
    n = len(P); nb = len(BONES); ND = np.full((n, nb), 99.0)
    for i, b in enumerate(BONES):
        a, c, r = segs[b]; d = seg_dist(P, a, c) / r
        if b.startswith('Left'): d = np.where(P[:, 0] < -0.04, 99.0, d)
        if b.startswith('Right'): d = np.where(P[:, 0] > 0.04, 99.0, d)
        ND[:, i] = d
    nmin = ND.min(1, keepdims=True)
    w = np.exp(-np.square((ND - nmin) / 0.30)); w[ND > 50] = 0
    # hard rules: pack/helmet/visor/lamps are rigid
    J = {b: i for i, b in enumerate(BONES)}
    hard = np.zeros((n, nb), bool)
    pack = (P[:, 2] < -0.10) & (np.abs(P[:, 0]) < 0.30) & (P[:, 1] > 0.92) & (P[:, 1] < 1.52)
    helm = (P[:, 1] > 1.54)
    for mask, bone in ((pack, 'Chest'), (helm, 'Head')):
        w[mask] = 0; w[mask, J[bone]] = 1
    if prim_k in (5, 6): w[:] = 0; w[:, J['Head']] = 1
    # keep the 4 strongest, normalise
    idx = np.argsort(-w, axis=1)[:, :4]; ww = np.take_along_axis(w, idx, 1); ww /= ww.sum(1, keepdims=True)
    return idx.astype(np.uint8), ww.astype(np.float32)

# ------------------------------------------------------------------ extra clips (EVA flavour)

def clip_table(lib):
    from crew import lie_pose, sleep_pose, jump
    def lie(t, T=1.9):
        p = lie_pose(t, T); p.hip_t[1] += 0.27; return p          # raised so the life-support pack clears the mattress
    def sleep(t, T=5.0):
        p = sleep_pose(t, T); p.hip_t[1] += 0.27; return p
    clips = [('Idle', AS.idle, 4.0, True), ('Walk', AS.walk, 1.0, True), ('Run', AS.run, 0.6, True),
             ('Jump', jump, 3.4, False), ('Wave', AS.wave, 2.4, False), ('Interact', AS.interact, 1.6, False),
             ('Death', AS.death, 2.6, False),
             ('LieDown', lie, 1.9, False), ('Sleep', sleep, 5.0, True), ('GetUp', lambda t, T=1.9: lie(1.9 - t), 1.9, False)]
    import suit_clips
    return clips + lib.clips() + suit_clips.clips(D)

# ------------------------------------------------------------------ GLB patch
def main(src, dst):
    j, bin_ = load(src)
    assert 'skins' not in j, 'already rigged'
    AS.BIND_HIPS_Y = D['hips_y']
    lib = PX.Lib(D)
    nodes = j['nodes']; mesh_node = next(i for i, n in enumerate(nodes) if 'mesh' in n)
    root = 0
    # ---- skeleton nodes
    arm_idx = len(nodes); nodes.append({'name': 'Armature'})
    idx = {}
    for n, (par, t, q) in SK.items():
        nd = {'name': n, 'translation': [float(x) for x in t]}
        if q is not None: nd['rotation'] = [float(x) for x in q]
        idx[n] = len(nodes); nodes.append(nd)
    for n, (par, t, q) in SK.items():
        tgt = nodes[arm_idx] if par is None else nodes[idx[par]]
        tgt.setdefault('children', []).append(idx[n])
    for n, (par, t) in SOCK.items():
        idx[n] = len(nodes); nodes.append({'name': n, 'translation': [float(x) for x in t]})
        nodes[idx[par]].setdefault('children', []).append(idx[n])
    nodes[root].setdefault('children', []).append(arm_idx)
    # ---- helpers to append binary data
    def add_bv(data, target=None):
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(data)
        bv = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
        if target: bv['target'] = target
        j['bufferViews'].append(bv); return len(j['bufferViews']) - 1
    def add_acc(arr, ctype, atype, target=None, minmax=False):
        bvi = add_bv(arr.tobytes(), target)
        a = {'bufferView': bvi, 'componentType': ctype, 'count': int(arr.shape[0]), 'type': atype}
        if minmax: a['min'] = [float(x) for x in arr.min(0)]; a['max'] = [float(x) for x in arr.max(0)]
        j['accessors'].append(a); return len(j['accessors']) - 1
    # ---- skin weights per primitive
    prims = j['meshes'][nodes[mesh_node]['mesh']]['primitives']
    for k, p in enumerate(prims):
        P = read_acc(j, bin_, p['attributes']['POSITION']).astype(float)
        jn, wt = compute_weights(P, k)
        jn4 = np.zeros((len(P), 4), np.uint8); jn4[:] = jn
        p['attributes']['JOINTS_0'] = add_acc(jn4, 5121, 'VEC4', 34962)
        p['attributes']['WEIGHTS_0'] = add_acc(wt.astype(np.float32), 5126, 'VEC4', 34962)
    # ---- skin
    W = bind_world(); ibm = np.stack([np.linalg.inv(W[b]).T for b in BONES]).astype(np.float32)   # column-major
    ibm_acc = add_acc(ibm.reshape(len(BONES), 16), 5126, 'MAT4')
    j.setdefault('skins', []).append({'name': 'AstronautSkin', 'joints': [idx[b] for b in BONES], 'skeleton': arm_idx,
                                      'inverseBindMatrices': ibm_acc})
    nodes[mesh_node]['skin'] = 0
    # ---- animations
    j['animations'] = []
    for name, fn, T_, loop in clip_table(lib):
        n = int(round(T_ * FPS)); times = (np.arange(n + 1) / FPS).astype(np.float32)
        poses = [AS.pose_dict(fn(min(float(t), T_), T_)) for t in times]
        if loop: poses[-1] = poses[0]
        tacc = add_acc(times.reshape(-1, 1), 5126, 'SCALAR', None, True)
        j['accessors'][tacc]['min'] = [float(times.min())]; j['accessors'][tacc]['max'] = [float(times.max())]
        samplers, channels = [], []
        for b in BONES:
            q = np.stack([ps[b][1] for ps in poses]).astype(np.float32)
            # keep quaternion hemisphere continuous so LINEAR interpolation never takes the long way round
            for i in range(1, len(q)):
                if float(q[i] @ q[i - 1]) < 0: q[i] = -q[i]
            qa = add_acc(q, 5126, 'VEC4')
            samplers.append({'input': tacc, 'output': qa, 'interpolation': 'LINEAR'})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': idx[b], 'path': 'rotation'}})
        ta = add_acc(np.stack([ps['Hips'][0] for ps in poses]).astype(np.float32), 5126, 'VEC3')
        samplers.append({'input': tacc, 'output': ta, 'interpolation': 'LINEAR'})
        channels.append({'sampler': len(samplers) - 1, 'target': {'node': idx['Hips'], 'path': 'translation'}})
        j['animations'].append({'name': name, 'samplers': samplers, 'channels': channels})
    j['asset']['generator'] = j['asset'].get('generator', '') + ' + Mission Mohakash rig_eva.py'
    j['buffers'][0]['byteLength'] = len(bin_)
    # ---- write GLB
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(bin_) % 4: bin_.append(0)
    out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_))
    out += struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(dst, 'wb').write(out)
    print('wrote', dst, len(out) // 1024, 'KB; clips:', [a['name'] for a in j['animations']])

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
