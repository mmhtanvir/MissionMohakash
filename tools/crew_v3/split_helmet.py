"""
split_helmet.py - moves the helmet (shell, visor, lamps above the neck ring) of the rigged AstronautEVA.glb into a rigid
node 'EVA_Helmet' under Head, and the life-support pack into 'EVA_Pack' under Chest with its origin on a vertical side
hinge (rear-entry hatch: Astronaut.cs swings it open to climb in / out).

    python3 split_helmet.py <rigged_in.glb> <out.glb>
"""
import sys, json, struct, numpy as np
from rig_eva import load, read_acc

NECK_Y = 1.52            # triangles whose centroid is above this belong to the helmet
VISOR_Y = 1.44           # the visor reaches a little lower at the front

PACK_Z = -0.14           # life-support pack = everything behind the torso (rear-entry hatch)
PACK_HINGE = (0.25, 1.20, -0.13)   # vertical hinge on one side of the pack (bind world, GLB axes)

def main(src, dst):
    j, bin_ = load(src)
    nodes = j['nodes']
    mesh_node = next(i for i, n in enumerate(nodes) if 'mesh' in n and 'skin' in n)
    skin = j['skins'][nodes[mesh_node]['skin']]
    ibm_all = read_acc(j, bin_, skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1).astype(float)
    def joint(name):
        n = next(i for i in skin['joints'] if nodes[i].get('name') == name); return n, ibm_all[skin['joints'].index(n)]

    def add_bv(data, target=None):
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(data)
        bv = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
        if target: bv['target'] = target
        j['bufferViews'].append(bv); return len(j['bufferViews']) - 1
    def add_acc(arr, ctype, atype, target=None, minmax=False):
        a = {'bufferView': add_bv(arr.tobytes(), target), 'componentType': ctype, 'count': int(arr.shape[0]), 'type': atype}
        if minmax: a['min'] = [float(x) for x in arr.min(0)]; a['max'] = [float(x) for x in arr.max(0)]
        j['accessors'].append(a); return len(j['accessors']) - 1

    body_prims = j['meshes'][nodes[mesh_node]['mesh']]['primitives']
    def extract(part, select, bone, pivot_world=None):
        """move the selected triangles into a rigid mesh node under 'bone' (origin at pivot_world, or the bone itself)"""
        bnode, ibm = joint(bone)
        piv = np.zeros(3) if pivot_world is None else (ibm @ np.r_[pivot_world, 1.0])[:3]
        prims = []; moved = 0
        for p in body_prims:
            mat = j['materials'][p['material']]['name'].lower()
            P = read_acc(j, bin_, p['attributes']['POSITION']).astype(float)
            I = read_acc(j, bin_, p['indices']).reshape(-1, 3).astype(np.int64)
            sel = select(P[I].mean(1), mat)
            if not sel.any(): continue
            moved += int(sel.sum())
            keep = I[~sel]
            p['indices'] = add_acc(keep.reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963)
            if len(keep) == 0: p['_empty'] = True
            HI = I[sel]; used = np.unique(HI); remap = -np.ones(len(P), np.int64); remap[used] = np.arange(len(used))
            Pl = (ibm @ np.c_[P[used], np.ones(len(used))].T).T[:, :3] - piv
            attrs = {'POSITION': add_acc(Pl.astype(np.float32), 5126, 'VEC3', 34962, True)}
            if 'NORMAL' in p['attributes']:
                N = read_acc(j, bin_, p['attributes']['NORMAL']).astype(float)[used] @ ibm[:3, :3].T
                N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)
                attrs['NORMAL'] = add_acc(N.astype(np.float32), 5126, 'VEC3', 34962)
            if 'TEXCOORD_0' in p['attributes']:
                attrs['TEXCOORD_0'] = add_acc(read_acc(j, bin_, p['attributes']['TEXCOORD_0']).astype(np.float32)[used], 5126, 'VEC2', 34962)
            prims.append({'attributes': attrs, 'indices': add_acc(remap[HI].reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963),
                          'material': p['material'], 'mode': 4})
        body_prims[:] = [p for p in body_prims if not p.pop('_empty', False)]
        j['meshes'].append({'name': part, 'primitives': prims})
        nd = {'name': part, 'mesh': len(j['meshes']) - 1}
        if pivot_world is not None: nd['translation'] = [float(x) for x in piv]
        nodes.append(nd); nodes[bnode].setdefault('children', []).append(len(nodes) - 1)
        print(part, 'triangles moved:', moved)

    extract('EVA_Helmet', lambda c, mat: (c[:, 1] > NECK_Y) | (('visor' in mat) & (c[:, 1] > VISOR_Y)), 'Head')
    extract('EVA_Pack', lambda c, mat: (c[:, 2] < PACK_Z) & (np.abs(c[:, 0]) < 0.32) & (c[:, 1] > 0.85) & (c[:, 1] <= NECK_Y), 'Chest', np.array(PACK_HINGE))
    # the open hatch in the suit's back (seen when the pack swings open): dark opening + metal rim, rigid on the chest
    def box(cx, cy, cz, sx, sy, sz):
        V, N, I = [], [], []
        for ax in range(3):
            for sg in (-1, 1):
                n = np.zeros(3); n[ax] = sg; u = np.zeros(3); u[(ax + 1) % 3] = 1; v = np.cross(n, u)
                h = np.array([sx, sy, sz]) / 2; c = np.array([cx, cy, cz]) + n * h
                b = len(V)
                for du, dv in ((-1, -1), (1, -1), (1, 1), (-1, 1)): V.append(c + u * du * h[(ax + 1) % 3] + v * dv * h[(ax + 2) % 3]); N.append(n)
                I += [[b, b + 1, b + 2], [b, b + 2, b + 3]]
        return np.array(V), np.array(N), np.array(I)
    _, ibm_c = joint('Chest')
    parts = {'HatchOpening': ([(0, 1.225, -0.142, 0.32, 0.44, 0.02)], (0.02, 0.022, 0.026), 0.0, 0.9),
             'HatchRim': ([(0, 1.455, -0.15, 0.36, 0.03, 0.03), (0, 0.995, -0.15, 0.36, 0.03, 0.03),
                           (-0.165, 1.225, -0.15, 0.03, 0.46, 0.03), (0.165, 1.225, -0.15, 0.03, 0.46, 0.03)], (0.62, 0.64, 0.68), 0.8, 0.35)}
    prims = []
    for mname, (boxes, col, met, rough) in parts.items():
        j['materials'].append({'name': mname, 'pbrMetallicRoughness': {'baseColorFactor': [c ** 2.2 for c in col] + [1.0], 'metallicFactor': met, 'roughnessFactor': rough}})
        V, N, I = [], [], []
        for bx in boxes:
            v, n, i = box(*bx); I.append(i + sum(len(x) for x in V)); V.append(v); N.append(n)
        V = (ibm_c @ np.c_[np.concatenate(V), np.ones(sum(len(x) for x in V))].T).T[:, :3]; N = np.concatenate(N) @ ibm_c[:3, :3].T
        prims.append({'attributes': {'POSITION': add_acc(V.astype(np.float32), 5126, 'VEC3', 34962, True), 'NORMAL': add_acc(N.astype(np.float32), 5126, 'VEC3', 34962)},
                      'indices': add_acc(np.concatenate(I).reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963), 'material': len(j['materials']) - 1, 'mode': 4})
    j['meshes'].append({'name': 'EVA_HatchFrame', 'primitives': prims})
    nodes.append({'name': 'EVA_HatchFrame', 'mesh': len(j['meshes']) - 1})
    cnode, _ = joint('Chest'); nodes[cnode].setdefault('children', []).append(len(nodes) - 1)

    j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(bin_) % 4: bin_.append(0)
    out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_))
    out += struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(dst, 'wb').write(out)
    print('->', dst, len(out) // 1024, 'KB')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
