"""Generic validator/previewer: reads a rigged GLB exactly as Unity will (nodes, skin, animation channels), skins it on the CPU at given clip times, renders sheets."""
import sys, json; sys.path.insert(0, '/home/claude/work')
import numpy as np
from PIL import Image, ImageDraw
import rig_eva as R
from glbkit import qmat
from render import render

def nlerp(a, b, k):
    if a @ b < 0: b = -b
    q = a * (1 - k) + b * k; return q / np.linalg.norm(q)

def setup(path):
    j, b = R.load(path); return j, b

def sample(j, b, clip, t):
    an = next(a for a in j['animations'] if a['name'] == clip); out = {}
    for ch in an['channels']:
        s = an['samplers'][ch['sampler']]; T = R.read_acc(j, b, s['input'])[:, 0]; V = R.read_acc(j, b, s['output'])
        t2 = min(max(t, T[0]), T[-1]); i = max(0, min(len(T) - 2, np.searchsorted(T, t2, 'right') - 1)); k = (t2 - T[i]) / (T[i + 1] - T[i] + 1e-9)
        v = nlerp(V[i], V[i + 1], k) if ch['target']['path'] == 'rotation' else V[i] * (1 - k) + V[i + 1] * k
        out[(ch['target']['node'], ch['target']['path'])] = v
    return out

def world(j, pose):
    nodes = j['nodes']; par = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []): par[c] = i
    cache = {}
    def W(i, use_pose):
        key = (i, use_pose)
        if key in cache: return cache[key]
        n = nodes[i]; t = np.array(n.get('translation', [0, 0, 0]), float); q = np.array(n.get('rotation', [0, 0, 0, 1]), float)
        if use_pose:
            t = pose.get((i, 'translation'), t); q = pose.get((i, 'rotation'), q)
        M = np.eye(4); M[:3, :3] = qmat(q); M[:3, 3] = t
        r = (W(par[i], use_pose) if i in par else np.eye(4)) @ M; cache[key] = r; return r
    return W

def skinned(j, b, clip, t, box=None, hold_name='Socket_Hold'):
    pose = sample(j, b, clip, t) if clip else {}; W = world(j, pose)
    sk = j['skins'][0]; joints = sk['joints']; ibm = R.read_acc(j, b, sk['inverseBindMatrices']).reshape(-1, 4, 4)
    ms = [W(jn, True) @ ibm[i].T for i, jn in enumerate(joints)]
    groups = []
    mnode = next(n for n in j['nodes'] if 'skin' in n)
    for k, p in enumerate(j['meshes'][mnode['mesh']]['primitives']):
        P = R.read_acc(j, b, p['attributes']['POSITION']).astype(float); J = R.read_acc(j, b, p['attributes']['JOINTS_0']).astype(int)
        Wt = R.read_acc(j, b, p['attributes']['WEIGHTS_0']).astype(float); I = R.read_acc(j, b, p['indices']).reshape(-1, 3).astype(int)
        Ph = np.c_[P, np.ones(len(P))]; out = np.zeros_like(P); Ms = np.stack(ms)
        for c in range(4): out += Wt[:, c:c + 1] * np.einsum('nij,nj->ni', Ms[J[:, c]], Ph)[:, :3]
        groups.append((out, I, k))
    if box:
        hi = next(i for i, n in enumerate(j['nodes']) if n.get('name') == hold_name); c = W(hi, True)[:3, 3]; h = box / 2
        V = np.array([[x, y, z] for x in (-h, h) for y in (-h, h) for z in (-h, h)]) + c
        F = [(0,1,3),(0,3,2),(4,6,7),(4,7,5),(0,4,5),(0,5,1),(2,3,7),(2,7,6),(0,2,6),(0,6,4),(1,5,7),(1,7,3)]
        groups.append((V, np.array(F), 2))
    return groups

if __name__ == '__main__':
    path, out = sys.argv[1], sys.argv[2]; j, b = setup(path)
    print(path.split('/')[-1], 'clips:', [(a['name'], round(float(R.read_acc(j, b, a['samplers'][0]['input'])[-1, 0]), 2)) for a in j['animations']])
    tests = [('Walk', .25, None), ('PickUp', .5, None), ('PickUp', .95, .38), ('PickUp', 1.5, .38), ('PickUp', 2.3, .38), ('CarryWalk', .25, .38),
             ('LieDown', 1.0, None), ('LieDown', 1.9, None), ('Sleep', 1.0, None), ('Run', .15, None), ('PutDown', 1.0, .38), ('Death', 2.6, None)]
    ims = []
    for clip, t, box in tests:
        g = skinned(j, b, clip, t, box)
        v1 = render(g, (35, 0), size=380, scale=190, center=np.array([0, .75, 0])); v2 = render(g, (90, 0), size=380, scale=190, center=np.array([0, .75, 0]))
        W = Image.new('RGB', (760, 380)); W.paste(v1, (0, 0)); W.paste(v2, (380, 0)); ImageDraw.Draw(W).text((8, 8), f'{clip} t={t}', fill=(255, 255, 0)); ims.append(W)
    S = Image.new('RGB', (1520, 380 * 6))
    for i, im in enumerate(ims): S.paste(im, ((i % 2) * 760, (i // 2) * 380))
    S.save(out); print('saved', out)
