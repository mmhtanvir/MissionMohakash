# usage: python3 trim_ladder.py SurfaceHabitat.glb   (edits in place; run once on a fresh game_pack.py output)
# Degenerate the ragged plate left at the foot of the stowed ladder (stairs node): everything below 0.62 m (CapsuleHatch adds clean rail stubs).
import json, struct, sys, array
p = sys.argv[1]; b = bytearray(open(p, 'rb').read())
jl = struct.unpack_from('<I', b, 12)[0]; g = json.loads(b[20:20 + jl]); bin0 = 20 + jl + 8
node = next(n for n in g['nodes'] if n.get('name') == 'stairs')
prims = g['meshes'][node['mesh']]['primitives']
def view(acc):
    a = g['accessors'][acc]; bv = g['bufferViews'][a['bufferView']]
    return bin0 + bv.get('byteOffset', 0) + a.get('byteOffset', 0), a['count'], a['componentType'], bv.get('byteStride')
cut = 0
for pr in prims:
    po, pc, _, ps = view(pr['attributes']['POSITION']); ps = ps or 12
    P = [struct.unpack_from('<3f', b, po + i * ps) for i in range(pc)]
    io, ic, it, _ = view(pr['indices']); fmt, sz = {5123: ('H', 2), 5125: ('I', 4)}[it]
    I = list(struct.unpack_from('<%d%s' % (ic, fmt), b, io))
    for t in range(0, ic, 3):
        c = [sum(P[I[t + k]][j] for k in range(3)) / 3 for j in range(3)]   # glTF: y up, z = ladder width
        if c[1] < 0.62:
            for k in range(3): struct.pack_into('<' + fmt, b, io + (t + k) * sz, I[t])
            cut += 1
open(p, 'wb').write(b); print('degenerated', cut, 'of', sum(g['accessors'][q['indices']]['count'] // 3 for q in prims))
