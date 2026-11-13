"""re-encode every embedded texture as JPEG, max side N: shrink.py file.glb N"""
import sys, io, pygltflib
from PIL import Image
path, N = sys.argv[1], int(sys.argv[2])
g = pygltflib.GLTF2().load(path); blob = g.binary_blob(); new = bytearray()
for i, bv in enumerate(g.bufferViews):
    data = bytes(blob[(bv.byteOffset or 0):(bv.byteOffset or 0) + bv.byteLength])
    im = next((x for x in g.images if x.bufferView == i), None)
    if im is not None:
        img = Image.open(io.BytesIO(data)); img.thumbnail((N, N), Image.LANCZOS)
        buf = io.BytesIO(); img.convert('RGB').save(buf, 'JPEG', quality=88); data = buf.getvalue(); im.mimeType = 'image/jpeg'
    while len(new) % 4: new.append(0)
    bv.byteOffset = len(new); bv.byteLength = len(data); new.extend(data)
g.buffers[0].byteLength = len(new); g.set_binary_blob(bytes(new)); g.save_binary(path)
