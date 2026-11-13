"""Simplify the big meshes with gltfpack (UV-seam aware), keep small parts as they are, downsize textures, write the game GLBs.
usage: game_pack.py srcdir outdir gltfpack_dir"""
import sys, os, io, subprocess, numpy as np, trimesh, pygltflib
from PIL import Image
src, out, gp = sys.argv[1:4]
TARGET = {'exterior': 70000, 'airlock_body': 45000, 'stairs': 9000}   # triangles; the rest stay as they are
TEX = 2048

def simplify(mesh, name, tris):
    if len(mesh.faces) <= tris: return mesh
    a, b = os.path.join(out, f'_{name}_in.glb'), os.path.join(out, f'_{name}_out.glb')
    mesh.export(a, include_normals=True)
    subprocess.run(['npx', 'gltfpack', '-i', a, '-o', b, '-si', str(tris / len(mesh.faces)), '-noq', '-kn'], cwd=gp, check=True, capture_output=True)
    m = trimesh.load(b, force='mesh'); os.remove(a); os.remove(b)
    print(f'{name}: {len(mesh.faces)} -> {len(m.faces)} tris'); return m

def shrink_textures(path):
    g = pygltflib.GLTF2().load(path); blob = bytearray(g.binary_blob()); new = bytearray()
    views = {im.bufferView for im in g.images}
    # rebuild the buffer: re-encoded images, every other view copied
    for i, bv in enumerate(g.bufferViews):
        data = bytes(blob[bv.byteOffset or 0:(bv.byteOffset or 0) + bv.byteLength])
        im = next((x for x in g.images if x.bufferView == i), None)
        if im is not None:
            img = Image.open(io.BytesIO(data)); img.thumbnail((TEX, TEX), Image.LANCZOS)
            buf = io.BytesIO(); img.convert('RGB').save(buf, 'JPEG', quality=88); data = buf.getvalue(); im.mimeType = 'image/jpeg'
        while len(new) % 4: new.append(0)
        bv.byteOffset = len(new); bv.byteLength = len(data); new.extend(data)
    g.buffers[0].byteLength = len(new); g.set_binary_blob(bytes(new)); g.save_binary(path)

for name in ('SurfaceHabitat',):
    sc = trimesh.load(os.path.join(src, name + '_src.glb'))
    outsc = trimesh.Scene()
    for node in sc.graph.nodes_geometry:
        T, geom = sc.graph[node]
        m = sc.geometry[geom]
        key = 'interior' if name == 'HabitatInterior' else node
        m = simplify(m, key, TARGET.get(key, 70000 if key == 'interior' else 10**9))
        outsc.add_geometry(m, node_name=node, geom_name=node, transform=T)
    p = os.path.join(out, name + '.glb'); outsc.export(p, include_normals=True); shrink_textures(p)
    print(name, os.path.getsize(p) // 1024, 'KB')
