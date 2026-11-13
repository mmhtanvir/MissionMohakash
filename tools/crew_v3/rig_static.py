"""
rig_static.py - rigs any STATIC character mesh (e.g. a Tripo model: one mesh, metres, feet at y=0, facing +Z) with the
game's 17-bone skeleton, sockets and clips, using rig_eva.py's GLB patcher with measured proportions instead of the
original suit's hard-coded ones.

    python3 rig_static.py eva    <static.glb> <rigged.glb>     # then split_helmet.py + fall_die.py, as for rig_eva.py
    python3 rig_static.py casual <static.glb> <rigged.glb>     # head goes to its own skinned mesh 'Crew_Head'

Proportions: FIT below (measured on front/side renders with a 10 cm grid). Arms may be bound in any direction
(ARM_DIR, left arm; the right is mirrored); clips replace the bind rotations, so only the skin weights see it.
"""
import sys, json, struct, numpy as np
import rig_eva as R
import poses_extra as PX
import astronaut as AS
from astronaut import BONES
from glbkit import qaxis

FIT = {
    # lunar astronaut 3d model.glb at 1.85 m: bulky suit, arms out ~68 deg and ~30 deg forward, pack behind z < -0.13
    'eva': dict(PX.EVA, hips_y=0.90, spine=0.12, chest=0.17, neck=0.31, head=0.10, sh_x=0.22, sh_y=0.21,
                hip_x=0.15, hip_dy=-0.05, ua=0.22, la=0.21, ul=0.40, ll=0.33, ankle_h=0.14,
                arm_dir=(0.80, -0.27, 0.53), leg_ang=4.0, head_r=0.22,
                helmet_y=1.52, pack=dict(z=-0.13, x=0.31, y0=0.90, y1=1.81), pack_hinge=(0.28, 1.35, -0.13)),
    # space explorer 3d model.glb at 1.76 m: casual clothes, arms ~20 deg out of an A-pose
    'casual': dict(PX.CASUAL, hips_y=0.93, sh_x=0.19, sh_y=0.19, ua=0.26, la=0.24, ul=0.43, ll=0.37,
                   arm_dir=(0.34, -0.94, 0.0), leg_ang=3.0, head_r=0.15, head_split_y=1.47),
}

def q_from_to(a, b):
    a = np.asarray(a, float) / np.linalg.norm(a); b = np.asarray(b, float) / np.linalg.norm(b)
    v = np.cross(a, b); c = float(a @ b)
    if c < -0.9999: return np.array([1.0, 0, 0, 0])
    q = np.r_[v, 1.0 + c]; return q / np.linalg.norm(q)   # x, y, z, w

def setup(kind):
    D = FIT[kind]
    R.D = D
    sk = {'Hips': (None, (0, D['hips_y'], 0), None), 'Spine': ('Hips', (0, D['spine'], 0), None),
          'Chest': ('Spine', (0, D['chest'], 0), None), 'Neck': ('Chest', (0, D['neck'], 0), None), 'Head': ('Neck', (0, D['head'], 0), None)}
    for side, sx in (('Left', 1), ('Right', -1)):
        ad = np.array(D['arm_dir']) * [sx, 1, 1]
        sk[side + 'UpperArm'] = ('Chest', (sx * D['sh_x'], D['sh_y'], 0), q_from_to((0, -1, 0), ad))
        sk[side + 'LowerArm'] = (side + 'UpperArm', (0, -D['ua'], 0), None)
        sk[side + 'Hand'] = (side + 'LowerArm', (0, -D['la'], 0), None)
        sk[side + 'UpperLeg'] = ('Hips', (sx * D['hip_x'], D['hip_dy'], 0), qaxis('z', sx * D['leg_ang']))
        sk[side + 'LowerLeg'] = (side + 'UpperLeg', (0, -D['ul'], 0), None)
        sk[side + 'Foot'] = (side + 'LowerLeg', (0, -D['ll'], 0), qaxis('z', -sx * D['leg_ang']))
    R.SK = sk
    R.SOCK = {'Socket_Hold': ('Chest', D['hold']), 'Socket_Back': ('Chest', (0, 0.0, -0.45 if kind == 'eva' else -0.20)),
              'Socket_HandL': ('LeftHand', (0, -0.08, 0)), 'Socket_HandR': ('RightHand', (0, -0.08, 0))}
    if kind == 'eva': R.SOCK['Socket_Helmet'] = ('Head', (0, 0.14, 0.20))
    R.compute_weights = lambda P, k: weights(P, D, kind)
    if kind == 'casual': R.clip_table = casual_clips
    return D

def weights(P, D, kind):
    W = R.bind_world(); pos = {n: W[n][:3, 3] for n in W}
    def dirv(n, L): return (W[n][:3, :3] @ np.array([0, -1.0, 0])) * L
    up = np.array([0, 1.0, 0])
    big = 1.35 if kind == 'eva' else 1.0     # suit limbs are thicker
    segs = {'Hips': (pos['Hips'] - up * 0.12, pos['Spine'], .20 * big), 'Spine': (pos['Spine'], pos['Chest'], .19 * big),
            'Chest': (pos['Chest'], pos['Neck'], .22 * big), 'Neck': (pos['Neck'], pos['Head'], .10 * big),
            'Head': (pos['Head'], pos['Head'] + up * 0.2, D['head_r'])}
    for s in ('Left', 'Right'):
        segs[s + 'UpperArm'] = (pos[s + 'UpperArm'], pos[s + 'LowerArm'], .075 * big)
        segs[s + 'LowerArm'] = (pos[s + 'LowerArm'], pos[s + 'Hand'], .065 * big)
        segs[s + 'Hand'] = (pos[s + 'Hand'], pos[s + 'Hand'] + dirv(s + 'UpperArm', .15), .07 * big)
        segs[s + 'UpperLeg'] = (pos[s + 'UpperLeg'], pos[s + 'LowerLeg'], .10 * big)
        segs[s + 'LowerLeg'] = (pos[s + 'LowerLeg'], pos[s + 'Foot'], .08 * big)
        segs[s + 'Foot'] = (pos[s + 'Foot'], pos[s + 'Foot'] + np.array([0, -.08, .14]), .09 * big)
    n = len(P); nb = len(BONES); ND = np.full((n, nb), 99.0)
    for i, b in enumerate(BONES):
        a, c, r = segs[b]; d = R.seg_dist(P, a, c) / r
        if b.startswith('Left'): d = np.where(P[:, 0] < -0.03, 99.0, d)
        if b.startswith('Right'): d = np.where(P[:, 0] > 0.03, 99.0, d)
        ND[:, i] = d
    nmin = ND.min(1, keepdims=True)
    w = np.exp(-np.square((ND - nmin) / 0.30)); w[ND > 50] = 0
    J = {b: i for i, b in enumerate(BONES)}
    rigid = []
    if kind == 'eva':
        pk = D['pack']
        rigid += [((P[:, 2] < pk['z']) & (np.abs(P[:, 0]) < pk['x']) & (P[:, 1] > pk['y0']) & (P[:, 1] < pk['y1']), 'Chest'),
                  ((P[:, 1] > D['helmet_y']) & (P[:, 2] >= pk['z']), 'Head')]
    else:
        rigid += [(P[:, 1] > D['head_split_y'] + 0.06, 'Head')]
    for mask, bone in rigid:
        w[mask] = 0; w[mask, J[bone]] = 1
    # arm vertices blend only along their own arm and into the chest (hands resting by the thighs would otherwise keep
    # some hip / leg weight and stretch into spikes when the arm lifts)
    dom = w.argmax(1)
    for s in ('Left', 'Right'):
        chain = [J[s + b] for b in ('UpperArm', 'LowerArm', 'Hand')]
        on = np.isin(dom, chain); allowed = np.zeros(nb, bool); allowed[chain + [J['Chest']]] = True
        w[np.ix_(on, ~allowed)] = 0
    idx = np.argsort(-w, axis=1)[:, :4]; ww = np.take_along_axis(w, idx, 1); ww /= ww.sum(1, keepdims=True)
    return idx.astype(np.uint8), ww.astype(np.float32)

def casual_clips(lib):
    from crew import lie_pose, sleep_pose, jump
    import suit_clips
    clips = [('Idle', AS.idle, 4.0, True), ('Walk', AS.walk, 1.0, True), ('Run', AS.run, 0.6, True),
             ('Jump', jump, 3.4, False), ('Wave', AS.wave, 2.4, False), ('Interact', AS.interact, 1.6, False), ('Death', AS.death, 2.6, False),
             ('LieDown', lie_pose, 1.9, False), ('Sleep', sleep_pose, 5.0, True), ('GetUp', lambda t, T=1.9: lie_pose(1.9 - t), 1.9, False)]
    return clips + lib.clips() + suit_clips.clips(R.D)

def split_head(path, y):
    """move the skinned triangles above the neck into their own skinned mesh node 'Crew_Head' (same skin)"""
    j, bin_ = R.load(path)
    nodes = j['nodes']; mi = next(i for i, n in enumerate(nodes) if 'mesh' in n and 'skin' in n)
    def add(arr, ctype, atype, target=None, minmax=False):
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(arr.tobytes())
        bv = {'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}
        if target: bv['target'] = target
        j['bufferViews'].append(bv)
        a = {'bufferView': len(j['bufferViews']) - 1, 'componentType': ctype, 'count': int(arr.shape[0]), 'type': atype}
        if minmax: a['min'] = [float(v) for v in arr.min(0)]; a['max'] = [float(v) for v in arr.max(0)]
        j['accessors'].append(a); return len(j['accessors']) - 1
    head_prims = []
    for p in j['meshes'][nodes[mi]['mesh']]['primitives']:
        P = R.read_acc(j, bin_, p['attributes']['POSITION'])
        I = R.read_acc(j, bin_, p['indices']).reshape(-1, 3).astype(np.int64)
        sel = P[I][:, :, 1].mean(1) > y
        p['indices'] = add(I[~sel].reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963)
        HI = I[sel]; used = np.unique(HI); remap = -np.ones(len(P), np.int64); remap[used] = np.arange(len(used))
        attrs = {}
        for k, a in p['attributes'].items():
            arr = R.read_acc(j, bin_, a)[used]
            ac = j['accessors'][a]
            attrs[k] = add(np.ascontiguousarray(arr), ac['componentType'], ac['type'], 34962, k == 'POSITION')
        head_prims.append({'attributes': attrs, 'indices': add(remap[HI].reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963), 'material': p['material'], 'mode': 4})
        print('Crew_Head triangles:', int(sel.sum()))
    j['meshes'][nodes[mi]['mesh']]['name'] = 'Crew_Body'; nodes[mi]['name'] = 'Crew_Mesh'
    j['meshes'].append({'name': 'Crew_Head', 'primitives': head_prims})
    nodes.append({'name': 'Crew_Head', 'mesh': len(j['meshes']) - 1, 'skin': nodes[mi]['skin']})
    j['scenes'][j.get('scene', 0)]['nodes'].append(len(nodes) - 1)
    j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(bin_) % 4: bin_.append(0)
    out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_))
    out += struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(path, 'wb').write(out)

if __name__ == '__main__':
    kind, src, dst = sys.argv[1:4]
    D = setup(kind)
    R.main(src, dst)
    if kind == 'casual': split_head(dst, D['head_split_y'])

