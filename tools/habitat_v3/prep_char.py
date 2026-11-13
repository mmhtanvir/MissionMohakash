"""static Tripo character -> game-ready static mesh: ~N tris (gltfpack, UV-seam aware), height H metres, feet y=0, centred
usage: prep_char.py in.glb out.glb height tris gltfpack_dir"""
import sys, os, subprocess, trimesh
src, dst, H, N, gp = sys.argv[1], sys.argv[2], float(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
m = trimesh.load(src, force='mesh')
tmp = dst + '.tmp.glb'
subprocess.run(['npx', 'gltfpack', '-i', os.path.abspath(src), '-o', os.path.abspath(tmp), '-si', str(N / len(m.faces)), '-noq'], cwd=gp, check=True, capture_output=True)
m = trimesh.load(tmp, force='mesh'); os.remove(tmp)
s = H / m.extents[1]; m.apply_scale(s)
lo, hi = m.bounds; m.apply_translation([-(lo[0] + hi[0]) / 2, -lo[1], -(lo[2] + hi[2]) / 2])
m.export(dst, include_normals=True)
print(dst, len(m.faces), 'tris', m.bounds.round(3).tolist())
