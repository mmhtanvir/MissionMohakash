"""
split_helmet.py - moves the helmet (shell, visor, lamps above the neck ring) of the rigged AstronautEVA.glb out of the
skinned body into its own rigid mesh node 'EVA_Helmet', parented to the Head bone. Astronaut.cs can then lift it off.

    python3 split_helmet.py <rigged_in.glb> <out.glb>
"""
import sys, json, struct, numpy as np
from rig_eva import load, read_acc

NECK_Y = 1.52            # triangles whose centroid is above this belong to the helmet
VISOR_Y = 1.44           # the visor reaches a little lower at the front

def main(src, dst):
    j, bin_ = load(src)
    nodes = j['nodes']
    mesh_node = next(i for i, n in enumerate(nodes) if 'mesh' in n and 'skin' in n)
    skin = j['skins'][nodes[mesh_node]['skin']]
    head = next(i for i in skin['joints'] if nodes[i].get('name') == 'Head')
    k = skin['joints'].index(head)
    ibm = read_acc(j, bin_, skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)[k].astype(float)   # world -> Head local

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
    helm_prims = []; moved = 0
    for p in body_prims:
        mat = j['materials'][p['material']]['name'].lower()
        P = read_acc(j, bin_, p['attributes']['POSITION']).astype(float)
        I = read_acc(j, bin_, p['indices']).reshape(-1, 3).astype(np.int64)
        c = P[I].mean(1)
        hel = (c[:, 1] > NECK_Y) | (('visor' in mat) & (c[:, 1] > VISOR_Y))
        if not hel.any(): continue
        moved += int(hel.sum())
        keep = I[~hel]
        p['indices'] = add_acc(keep.reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963)
        if len(keep) == 0: p['_empty'] = True
        HI = I[hel]; used = np.unique(HI); remap = -np.ones(len(P), np.int64); remap[used] = np.arange(len(used))
        Pl = (ibm @ np.c_[P[used], np.ones(len(used))].T).T[:, :3]
        attrs = {'POSITION': add_acc(Pl.astype(np.float32), 5126, 'VEC3', 34962, True)}
        if 'NORMAL' in p['attributes']:
            N = read_acc(j, bin_, p['attributes']['NORMAL']).astype(float)[used] @ ibm[:3, :3].T
            N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)
            attrs['NORMAL'] = add_acc(N.astype(np.float32), 5126, 'VEC3', 34962)
        if 'TEXCOORD_0' in p['attributes']:
            attrs['TEXCOORD_0'] = add_acc(read_acc(j, bin_, p['attributes']['TEXCOORD_0']).astype(np.float32)[used], 5126, 'VEC2', 34962)
        helm_prims.append({'attributes': attrs, 'indices': add_acc(remap[HI].reshape(-1).astype(np.uint32), 5125, 'SCALAR', 34963),
                           'material': p['material'], 'mode': 4})
    body_prims[:] = [p for p in body_prims if not p.pop('_empty', False)]
    j['meshes'].append({'name': 'EVA_Helmet', 'primitives': helm_prims})
    nodes.append({'name': 'EVA_Helmet', 'mesh': len(j['meshes']) - 1})
    nodes[head].setdefault('children', []).append(len(nodes) - 1)
    j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(bin_) % 4: bin_.append(0)
    out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_))
    out += struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(dst, 'wb').write(out)
    print('helmet triangles moved:', moved, '->', dst, len(out) // 1024, 'KB')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
