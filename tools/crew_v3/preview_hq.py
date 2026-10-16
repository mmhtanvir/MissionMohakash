"""Render a rigged character GLB (textures + CPU skinning of any clip pose) with pyrender.
    python3 preview_hq.py Model.glb out.png [mode]   mode: bind | poses | face"""
import sys, io, json, struct, math, numpy as np
import trimesh, pyrender
from PIL import Image, ImageDraw
from glbkit import qmat
import astronaut as AS, poses_extra as PX, crew

CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
def load(path):
    b = open(path, 'rb').read(); L = struct.unpack('<I', b[12:16])[0]; j = json.loads(b[20:20 + L])
    off = 20 + L; return j, b[off + 8:off + 8 + struct.unpack('<I', b[off:off + 4])[0]]
def acc(j, bin_, i):
    a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
    n = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
    return np.frombuffer(bin_, CT[a['componentType']], a['count'] * n, bv.get('byteOffset', 0) + a.get('byteOffset', 0)).reshape(a['count'], n)

def node_mats(j, override=None):
    nodes = j['nodes']; par = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []): par[c] = i
    W = {}
    def local(i):
        n = nodes[i]; M = np.eye(4)
        t = n.get('translation', [0, 0, 0]); q = n.get('rotation', [0, 0, 0, 1])
        if override and n.get('name') in override:
            tt, qq = override[n['name']]; q = qq if qq is not None else q; t = tt if tt is not None else t
        M[:3, :3] = qmat(q) @ np.diag(n.get('scale', [1, 1, 1])); M[:3, 3] = t; return M
    def world(i):
        if i in W: return W[i]
        W[i] = (world(par[i]) if i in par else np.eye(4)) @ local(i); return W[i]
    for i in range(len(nodes)): world(i)
    return W

def meshes(path, pose=None):
    j, b = load(path); skin = j['skins'][0]; joints = skin['joints']
    ibm = acc(j, b, skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    ov = None
    if pose is not None:
        ov = {name: (None, q) for name, (t, q) in pose.items()}; ov['Hips'] = (pose['Hips'][0], pose['Hips'][1])
    W = node_mats(j, ov); Ms = np.stack([W[jn] @ ibm[k] for k, jn in enumerate(joints)])
    out = []
    for p in j['meshes'][0]['primitives']:
        P = acc(j, b, p['attributes']['POSITION']).astype(float); I = acc(j, b, p['indices']).reshape(-1, 3)
        J = acc(j, b, p['attributes']['JOINTS_0']).astype(int); Wt = acc(j, b, p['attributes']['WEIGHTS_0']).astype(float)
        Ph = np.c_[P, np.ones(len(P))]; Q = np.zeros_like(P)
        for c in range(4): Q += Wt[:, c:c + 1] * np.einsum('nij,nj->ni', Ms[J[:, c]], Ph)[:, :3]
        m = j['materials'][p['material']]; pbr = m['pbrMetallicRoughness']
        fac = pbr.get('baseColorFactor', [1, 1, 1, 1])
        mat = pyrender.MetallicRoughnessMaterial(baseColorFactor=fac, metallicFactor=pbr.get('metallicFactor', 0), roughnessFactor=pbr.get('roughnessFactor', .6), doubleSided=True)
        uv = acc(j, b, p['attributes']['TEXCOORD_0']).astype(float) if 'TEXCOORD_0' in p['attributes'] else None
        if uv is not None: uv = np.c_[uv[:, 0], 1.0 - uv[:, 1]]   # glTF v points down, OpenGL up
        if 'baseColorTexture' in pbr:
            im = j['images'][j['textures'][pbr['baseColorTexture']['index']]['source']]; bv = j['bufferViews'][im['bufferView']]
            mat.baseColorTexture = pyrender.Texture(source=np.array(Image.open(io.BytesIO(b[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']])).convert('RGB')), source_channels='RGB',
                                                    sampler=pyrender.Sampler(wrapS=10497, wrapT=10497))
        if 'normalTexture' in m:
            im = j['images'][j['textures'][m['normalTexture']['index']]['source']]; bv = j['bufferViews'][im['bufferView']]
            mat.normalTexture = pyrender.Texture(source=np.array(Image.open(io.BytesIO(b[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']])).convert('RGB')), source_channels='RGB',
                                                 sampler=pyrender.Sampler(wrapS=10497, wrapT=10497))
        tm = trimesh.Trimesh(Q, I, process=False)
        prim = pyrender.Primitive(positions=Q, normals=tm.vertex_normals, indices=I, texcoord_0=uv, material=mat, mode=4)
        out.append(pyrender.Mesh([prim]))
    return out

R = None
def render(ms, eye, target, size=(600, 800), fov=30):
    global R
    if R is None or (R.viewport_width, R.viewport_height) != size: R = pyrender.OffscreenRenderer(*size)
    sc = pyrender.Scene(bg_color=[0.16, 0.17, 0.2, 1], ambient_light=[0.25, 0.25, 0.27])
    for m in ms: sc.add(m)
    eye = np.array(eye, float); target = np.array(target, float)
    f = target - eye; f /= np.linalg.norm(f); r = np.cross(f, [0, 1, 0]); r /= np.linalg.norm(r); u = np.cross(r, f)
    cam = np.eye(4); cam[:3, 0] = r; cam[:3, 1] = u; cam[:3, 2] = -f; cam[:3, 3] = eye
    sc.add(pyrender.PerspectiveCamera(yfov=math.radians(fov), aspectRatio=size[0] / size[1]), pose=cam)
    def light(dirv, I, c=(1, 1, 1)):
        d = np.array(dirv, float); d /= np.linalg.norm(d); z = -d; x = np.cross([0, 1, 0], z); x /= np.linalg.norm(x); y = np.cross(z, x)
        M = np.eye(4); M[:3, 0] = x; M[:3, 1] = y; M[:3, 2] = z; sc.add(pyrender.DirectionalLight(color=c, intensity=I), pose=M)
    light((-0.5, -0.7, -0.6), 4.0, (1, .97, .92)); light((0.7, -0.2, 0.5), 1.6, (.75, .82, 1)); light((0.2, -0.3, 1.0), 1.8)
    col, _ = R.render(sc); return Image.fromarray(col)

def sheet(ims, cols):
    w, h = ims[0].size; rows = (len(ims) + cols - 1) // cols; S = Image.new('RGB', (w * cols, h * rows))
    for i, im in enumerate(ims): S.paste(im, ((i % cols) * w, (i // cols) * h))
    return S

if __name__ == '__main__':
    path, out = sys.argv[1], sys.argv[2]; mode = sys.argv[3] if len(sys.argv) > 3 else 'bind'
    AS.BIND_HIPS_Y = PX.CASUAL['hips_y']
    if mode == 'bind':
        ms = meshes(path); ims = []
        for az in (20, 90, 180, -40):
            a = math.radians(az); ims.append(render(ms, (3.4 * math.sin(a), 1.1, 3.4 * math.cos(a)), (0, 0.9, 0)))
        sheet(ims, 4).save(out)
    elif mode == 'face':
        ms = meshes(path, AS.pose_dict(AS.idle(0.0, 4.0))); ims = []
        for az in (0, 35, 90, 150):
            a = math.radians(az); ims.append(render(ms, (0.75 * math.sin(a), 1.64, 0.75 * math.cos(a)), (0, 1.60, 0), size=(500, 500), fov=24))
        for az in (25,):
            a = math.radians(az); ims.append(render(ms, (1.5 * math.sin(a), 1.2, 1.5 * math.cos(a)), (0, 1.1, 0), size=(500, 500), fov=34))
        a = math.radians(-30); ims.append(render(ms, (0.5 * math.sin(a) + 0.2, 0.8, 0.5 * math.cos(a)), (0.2, 0.75, 0.0), size=(500, 500), fov=30))
        sheet(ims, 3).save(out)
    else:
        L = PX.Lib(PX.CASUAL)
        tests = [('Idle', AS.idle, 1.0, 4.0), ('Walk', AS.walk, .25, 1.0), ('Run', AS.run, .15, .6), ('Wave', AS.wave, 1.0, 2.4),
                 ('PickUp', L.pickup, 1.0, 2.3), ('CarryWalk', L.carry_walk, .25, 1.0), ('Sleep', crew.sleep_pose, 1.0, 5.0), ('Jump', crew.jump, 1.2, 3.4)]
        ims = []
        for name, fn, t, T in tests:
            ms = meshes(path, AS.pose_dict(fn(t, T)))
            im = render(ms, (2.2, 1.3, 2.6), (0, 0.8, 0), size=(420, 520)); ImageDraw.Draw(im).text((8, 8), name, fill=(255, 255, 0)); ims.append(im)
        sheet(ims, 4).save(out)
    print('saved', out)
