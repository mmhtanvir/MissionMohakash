"""
glbkit.py - tiny dependency-free (numpy only) glTF 2.0 / GLB writer used to
procedurally build the Mission Mohakash (OUTPOST ZERO) game assets.

Conventions (match glTF + Unity/glTFast import):
  * metres, +Y up, character/object FRONT faces +Z
  * a character's LEFT is +X, RIGHT is -X (glTF is right-handed; Unity flips X on import)
"""
import json, math, struct, os
import numpy as np

# ----------------------------------------------------------------- math
def _n(v):
    v = np.asarray(v, float); l = np.linalg.norm(v)
    return v / l if l > 1e-12 else v

def T(x=0, y=0, z=0):
    m = np.eye(4); m[:3, 3] = (x, y, z); return m

def S(x=1, y=None, z=None):
    if y is None: y = z = x
    return np.diag([x, y, z, 1.0])

def R(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a); m = np.eye(4)
    if axis == 'x': m[1, 1] = c; m[1, 2] = -s; m[2, 1] = s; m[2, 2] = c
    elif axis == 'y': m[0, 0] = c; m[0, 2] = s; m[2, 0] = -s; m[2, 2] = c
    else: m[0, 0] = c; m[0, 1] = -s; m[1, 0] = s; m[1, 1] = c
    return m

def RE(x=0, y=0, z=0):
    return R('z', z) @ R('y', y) @ R('x', x)

def rot_between(a, b):
    a = _n(a); b = _n(b); c = float(a @ b)
    if c < -0.999999:
        ax = np.cross(a, [1, 0, 0])
        if np.linalg.norm(ax) < 1e-6: ax = np.cross(a, [0, 0, 1])
        ax = _n(ax); K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
        m3 = np.eye(3) + 2 * K @ K
    else:
        v = np.cross(a, b); K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
        m3 = np.eye(3) + K + K @ K / (1 + c)
    m = np.eye(4); m[:3, :3] = m3; return m

def qaxis(axis, deg):
    a = math.radians(deg) / 2; s = math.sin(a); c = math.cos(a)
    return np.array({'x': (s, 0, 0, c), 'y': (0, s, 0, c), 'z': (0, 0, s, c)}[axis], float)

def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return np.array([aw*bx + ax*bw + ay*bz - az*by, aw*by - ax*bz + ay*bw + az*bx,
                     aw*bz + ax*by - ay*bx + az*bw, aw*bw - ax*bx - ay*by - az*bz])

def qe(x=0, y=0, z=0):
    """euler degrees -> quaternion xyzw (X applied first, then Y, then Z)"""
    return qmul(qaxis('z', z), qmul(qaxis('y', y), qaxis('x', x)))

QI = np.array([0, 0, 0, 1.0])

def qmat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                     [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                     [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def trs(t, q, s):
    m = np.eye(4); m[:3, :3] = qmat(q) @ np.diag(s); m[:3, 3] = t; return m

# ----------------------------------------------------------------- materials
class Mat:
    def __init__(s, name, color, metal=0.0, rough=0.6, emissive=None, estr=1.0, alpha=1.0, double=False,
                 tex=None, nrm=None, tile=2.0, nstr=1.0):
        s.name = name; s.color = tuple(color); s.metal = metal; s.rough = rough
        s.emissive = emissive; s.estr = estr; s.alpha = alpha; s.double = double
        s.tex = tex; s.nrm = nrm; s.tile = tile; s.nstr = nstr

def _lin(c): return [float(max(0.0, min(1.0, x)) ** 2.2) for x in c]

# ----------------------------------------------------------------- geometry
class Geo:
    def __init__(s, P, N, I, C=None):
        s.P = np.asarray(P, float).reshape(-1, 3); s.N = np.asarray(N, float).reshape(-1, 3)
        s.I = np.asarray(I, np.int64).reshape(-1, 3)
        s.C = None if C is None else np.asarray(C, float).reshape(-1, 4)
        s.UV = None
        s._fix()

    def _fix(s):
        a, b, c = s.P[s.I[:, 0]], s.P[s.I[:, 1]], s.P[s.I[:, 2]]
        gn = np.cross(b - a, c - a)
        area = np.linalg.norm(gn, axis=1)
        avg = s.N[s.I[:, 0]] + s.N[s.I[:, 1]] + s.N[s.I[:, 2]]
        flip = (gn * avg).sum(1) < 0
        I = s.I.copy(); I[flip] = I[flip][:, [0, 2, 1]]
        s.I = I[area > 1e-10]

    def xf(s, M):
        A = M[:3, :3]
        P = s.P @ A.T + M[:3, 3]
        N = s.N @ np.linalg.inv(A)
        N = N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)
        I = s.I[:, [0, 2, 1]] if np.linalg.det(A) < 0 else s.I
        g = Geo.__new__(Geo); g.P = P; g.N = N; g.I = I; g.C = s.C; g.UV = s.UV; g.JW = getattr(s, 'JW', None)
        return g

def box_geo(sx, sy, sz, r=0.0):
    h = np.array([sx, sy, sz], float) / 2
    r = min(r, h.min() * 0.999)
    P = []; N = []; I = []; off = 0
    if r <= 1e-6:
        for a in range(3):
            for sg in (-1, 1):
                b, c = (a + 1) % 3, (a + 2) % 3
                for (u, v) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    p = np.zeros(3); p[a] = sg * h[a]; p[b] = u * h[b]; p[c] = v * h[c]
                    n = np.zeros(3); n[a] = sg
                    P.append(p); N.append(n)
                I += [[off, off+1, off+2], [off, off+2, off+3]]; off += 4
        return Geo(P, N, I)
    t = math.tan(math.radians(22.5))
    def grid(hh): return [-hh, -hh + r * (1 - t), -hh + r, hh - r, hh - r + r * t, hh]
    lo = -(h - r); hi = h - r
    for a in range(3):
        for sg in (-1, 1):
            b, c = (a + 1) % 3, (a + 2) % 3
            gb, gc = grid(h[b]), grid(h[c])
            for ib, vb in enumerate(gb):
                for ic, vc in enumerate(gc):
                    p = np.zeros(3); p[a] = sg * h[a]; p[b] = vb; p[c] = vc
                    inner = np.clip(p, lo, hi); d = p - inner
                    nn = d / np.linalg.norm(d)
                    P.append(inner + nn * r); N.append(nn)
            for ib in range(5):
                for ic in range(5):
                    i0 = off + ib * 6 + ic
                    I += [[i0, i0 + 1, i0 + 7], [i0, i0 + 7, i0 + 6]]
            off += 36
    return Geo(P, N, I)

def lathe(profile, seg=24, smooth_deg=40, sweep=360.0):
    """profile: [(radius, y), ...] revolved around Y. Order bottom->top along the OUTSIDE."""
    pr = np.asarray(profile, float)
    segs = []
    for i in range(len(pr) - 1):
        dr, dy = pr[i + 1] - pr[i]; l = math.hypot(dr, dy)
        segs.append(None if l < 1e-9 else np.array([dy / l, -dr / l]))
    cs = math.cos(math.radians(smooth_deg))
    ang = np.radians(np.linspace(0, sweep, seg + 1)); ca, sa = np.cos(ang), np.sin(ang)
    P = []; N = []; I = []; off = 0; UVs = []
    cum = np.concatenate([[0.0], np.cumsum(np.hypot(*(np.diff(pr, axis=0)).T))])
    circ = max(2.0, round(2 * math.pi * float(np.abs(pr[:, 0]).max()) / 2.0) * 2.0)
    for i, sn in enumerate(segs):
        if sn is None: continue
        ends = []
        for pi_, nb in ((i, i - 1), (i + 1, i + 1)):
            nn = sn
            if 0 <= nb < len(segs) and segs[nb] is not None and float(segs[nb] @ sn) > cs:
                nn = sn + segs[nb]; nn = nn / np.linalg.norm(nn)
            if abs(pr[pi_][0]) < 1e-9:
                nn = np.array([0.0, 1.0 if nn[1] >= 0 else -1.0])
            ends.append((pr[pi_], nn))
        for (r, y), nn in ends:
            P.append(np.stack([r * ca, np.full_like(ca, y), r * sa], 1))
            N.append(np.stack([nn[0] * ca, np.full_like(ca, nn[1]), nn[0] * sa], 1))
            if abs(nn[1]) > 0.8: UVs.append(np.stack([r * ca, r * sa], 1))
            else:
                k = [j for j in range(len(pr)) if abs(pr[j][0] - r) < 1e-9 and abs(pr[j][1] - y) < 1e-9][0]
                UVs.append(np.stack([ang / (2 * math.pi) * circ, np.full_like(ca, cum[k])], 1))
        a = off; b = off + seg + 1
        for k in range(seg):
            I.append([a + k, b + k, a + k + 1]); I.append([a + k + 1, b + k, b + k + 1])
        off += 2 * (seg + 1)
    g = Geo(np.concatenate(P), np.concatenate(N), I)
    g.UV = np.concatenate(UVs)
    return g

def cylinder_geo(r, h, seg=20, r2=None):
    r2 = r if r2 is None else r2
    return lathe([(0, -h/2), (r, -h/2), (r2, h/2), (0, h/2)], seg)

def sphere_geo(r=1.0, seg=20, rings=10):
    pf = [(r * math.cos(math.radians(a)), r * math.sin(math.radians(a)))
          for a in np.linspace(-90, 90, rings + 1)]
    pf[0] = (0, -r); pf[-1] = (0, r)
    return lathe(pf, seg, smooth_deg=89)

def extrude_geo(poly, depth):
    """star-shaped polygon (list of (x,y)) in XY, extruded along Z, centred."""
    poly = np.asarray(poly, float); n = len(poly); c = poly.mean(0); hz = depth / 2
    P = []; N = []; I = []; off = 0
    for z, nz in ((hz, 1), (-hz, -1)):
        P.append([c[0], c[1], z]); N.append([0, 0, nz])
        for p in poly: P.append([p[0], p[1], z]); N.append([0, 0, nz])
        for k in range(n): I.append([off, off + 1 + k, off + 1 + (k + 1) % n])
        off += n + 1
    for k in range(n):
        p0, p1 = poly[k], poly[(k + 1) % n]; e = p1 - p0
        nn = _n([e[1], -e[0], 0])
        P += [[p0[0], p0[1], hz], [p1[0], p1[1], hz], [p1[0], p1[1], -hz], [p0[0], p0[1], -hz]]
        N += [nn] * 4; I += [[off, off+1, off+2], [off, off+2, off+3]]; off += 4
    return Geo(P, N, I)

def torus_geo(R_, r_, seg=24, tube=10):
    P = []; N = []; I = []
    for i in range(seg + 1):
        u = 2 * math.pi * i / seg
        for j in range(tube + 1):
            v = 2 * math.pi * j / tube
            cx, cz = math.cos(u), math.sin(u)
            P.append([(R_ + r_ * math.cos(v)) * cx, r_ * math.sin(v), (R_ + r_ * math.cos(v)) * cz])
            N.append([math.cos(v) * cx, math.sin(v), math.cos(v) * cz])
    for i in range(seg):
        for j in range(tube):
            a = i * (tube + 1) + j; b = a + tube + 1
            I += [[a, b, a + 1], [a + 1, b, b + 1]]
    return Geo(P, N, I)

def _icosphere(sub):
    t = (1 + 5 ** 0.5) / 2
    V = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    V = [list(_n(v)) for v in V]
    for _ in range(sub):
        cache = {}; NF = []
        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in cache:
                V.append(list(_n((np.array(V[a]) + np.array(V[b])) / 2))); cache[k] = len(V) - 1
            return cache[k]
        for a, b, c in F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            NF += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        F = NF
    return np.array(V), np.array(F)

def rock_geo(radius=0.3, sub=2, seed=0, squash=(1, 0.7, 1), rough=0.35, var=0.18, flat_bottom=0.55, sink=0.0):
    """faceted low-poly rock, flat shaded, vertex-colour variation; origin = ground contact."""
    rng = np.random.default_rng(seed)
    V, F = _icosphere(sub)
    dirs = rng.normal(size=(9, 3)); dirs /= np.linalg.norm(dirs, axis=1, keepdims=True)
    fr = rng.uniform(1.2, 4.5, 9); ph = rng.uniform(0, 6.28, 9); am = rough / fr ** 0.9
    d = (np.sin((V @ dirs.T) * fr + ph) * am).sum(1)
    P = V * (1 + d)[:, None] * np.array(squash) * radius
    ymin = -flat_bottom * radius * squash[1]
    P[:, 1] = np.maximum(P[:, 1], ymin)
    P[:, 1] -= P[:, 1].min() + sink
    Pt = P[F].reshape(-1, 3)
    I = np.arange(len(Pt)).reshape(-1, 3)
    a, b, c = Pt[0::3], Pt[1::3], Pt[2::3]
    fn = np.cross(b - a, c - a); fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)
    # ensure outward
    cen = (a + b + c) / 3 - np.array([0, radius * squash[1] * 0.4, 0])
    flip = (fn * cen).sum(1) < 0
    fn[flip] *= -1
    N = np.repeat(fn, 3, axis=0)
    shade = 1 - var * rng.uniform(0, 1, len(fn)) * 2
    shade = shade * (0.9 + 0.1 * np.clip(fn[:, 1], 0, 1))
    C = np.repeat(np.stack([shade, shade, shade, np.ones_like(shade)], 1), 3, axis=0)
    g = Geo(Pt, N, I, C)
    return g

# ----------------------------------------------------------------- mesh builder
def _xfm(pos=(0, 0, 0), rot=None, sc=None):
    M = T(*pos)
    if rot is not None:
        M = M @ (rot if isinstance(rot, np.ndarray) else RE(*rot))
    if sc is not None:
        M = M @ (S(*sc) if not isinstance(sc, (int, float)) else S(sc))
    return M

_AXROT = {'y': np.eye(4), 'x': R('z', -90), 'z': R('x', 90)}

class MeshB:
    def __init__(s, name=''):
        s.name = name; s.items = []; s.base = np.eye(4)

    def add(s, geo, mat, M=None, joint=None):
        M2 = s.base if M is None else s.base @ M
        s.items.append((mat, geo.xf(M2), joint))
        return s

    def box(s, size, pos, mat, r=0.0, rot=None, joint=None):
        return s.add(box_geo(*size, r=r), mat, _xfm(pos, rot), joint)

    def cyl(s, r, h, pos, mat, axis='y', r2=None, seg=20, rot=None, joint=None):
        M = _xfm(pos, rot) @ _AXROT[axis]
        return s.add(cylinder_geo(r, h, seg, r2), mat, M, joint)

    def sph(s, r, pos, mat, sc=(1, 1, 1), seg=20, rings=10, rot=None, joint=None):
        return s.add(sphere_geo(r, seg, rings), mat, _xfm(pos, rot, sc), joint)

    def lathe(s, profile, pos, mat, seg=24, rot=None, sc=None, joint=None, smooth_deg=40, sweep=360.0, axis='y'):
        M = _xfm(pos, rot, sc) @ _AXROT[axis]
        return s.add(lathe(profile, seg, smooth_deg, sweep), mat, M, joint)

    def torus(s, R_, r_, pos, mat, axis='y', seg=28, tube=10, rot=None, joint=None):
        return s.add(torus_geo(R_, r_, seg, tube), mat, _xfm(pos, rot) @ _AXROT[axis], joint)

    def between(s, p0, p1, r, mat, r2=None, seg=10, joint=None):
        p0 = np.asarray(p0, float); p1 = np.asarray(p1, float); d = p1 - p0; L = np.linalg.norm(d)
        M = T(*((p0 + p1) / 2)) @ rot_between([0, 1, 0], d)
        return s.add(cylinder_geo(r, L, seg, r2), mat, M, joint)

    def beam(s, p0, p1, w, d, mat, r=0.0, joint=None):
        p0 = np.asarray(p0, float); p1 = np.asarray(p1, float); v = p1 - p0; L = np.linalg.norm(v)
        M = T(*((p0 + p1) / 2)) @ rot_between([0, 1, 0], v)
        return s.add(box_geo(w, L, d, r), mat, M, joint)

    def extrude(s, poly, depth, pos, mat, rot=None, sc=None, joint=None):
        return s.add(extrude_geo(poly, depth), mat, _xfm(pos, rot, sc), joint)

    def empty(s): return len(s.items) == 0

# ----------------------------------------------------------------- scene graph
class Node:
    def __init__(s, name, t=(0, 0, 0), r=None, sc=(1, 1, 1), mesh=None, extras=None):
        s.name = name; s.t = np.array(t, float); s.r = QI.copy() if r is None else np.array(r, float)
        s.s = np.array(sc, float); s.mesh = mesh; s.children = []; s.parent = None; s.skin = None; s.extras = extras

    def add(s, n):
        n.parent = s; s.children.append(n); return n

    def local(s): return trs(s.t, s.r, s.s)
    def world(s):
        m = s.local(); p = s.parent
        while p is not None: m = p.local() @ m; p = p.parent
        return m

class Skin:
    def __init__(s, joints, skeleton): s.joints = joints; s.skeleton = skeleton

class Anim:
    def __init__(s, name): s.name = name; s.chans = []
    def add(s, node, path, times, values, interp='LINEAR'):
        s.chans.append((node, path, np.asarray(times, np.float32), np.asarray(values, np.float32), interp))

# ----------------------------------------------------------------- writer
def _boxuv(P, N):
    ax = np.abs(N).argmax(1)
    u = np.where(ax == 0, P[:, 2], P[:, 0]); v = np.where(ax == 1, P[:, 2], P[:, 1])
    return np.stack([u, v], 1)

class Asset:
    def __init__(s, name):
        s.name = name; s.roots = []; s.anims = []

    def add(s, n): s.roots.append(n); return n

    def all_nodes(s):
        out = []
        def rec(n):
            out.append(n)
            for c in n.children: rec(c)
        for r in s.roots: rec(r)
        return out

    def bounds(s):
        lo = np.full(3, 1e9); hi = np.full(3, -1e9)
        for n in s.all_nodes():
            if n.mesh is None: continue
            W = n.world() if n.skin is None else np.eye(4)
            for mat, g, j in n.mesh.items:
                P = g.P @ W[:3, :3].T + W[:3, 3]
                lo = np.minimum(lo, P.min(0)); hi = np.maximum(hi, P.max(0))
        return lo, hi

    def tri_count(s):
        return sum(len(g.I) for n in s.all_nodes() if n.mesh for _, g, _ in n.mesh.items)

    def save(s, path):
        nodes = s.all_nodes()
        idx = {id(n): i for i, n in enumerate(nodes)}
        buf = bytearray(); views = []; accs = []; cache = {}

        def add_acc(arr, ctype, atype, target=None, minmax=False, key=None):
            if key is not None and key in cache: return cache[key]
            raw = arr.tobytes()
            while len(buf) % 4: buf.append(0)
            views.append({"buffer": 0, "byteOffset": len(buf), "byteLength": len(raw), **({"target": target} if target else {})})
            buf.extend(raw)
            a = {"bufferView": len(views) - 1, "componentType": ctype, "count": int(arr.shape[0]), "type": atype}
            if minmax:
                a["min"] = [float(x) for x in np.atleast_1d(arr.min(0))]; a["max"] = [float(x) for x in np.atleast_1d(arr.max(0))]
            accs.append(a)
            if key is not None: cache[key] = len(accs) - 1
            return len(accs) - 1

        mats = []; mat_idx = {}; images = []; textures = []; img_idx = {}
        def tex_id(path):
            if path in img_idx: return img_idx[path]
            raw = open(path, 'rb').read()
            while len(buf) % 4: buf.append(0)
            views.append({"buffer": 0, "byteOffset": len(buf), "byteLength": len(raw)})
            buf.extend(raw)
            images.append({"bufferView": len(views) - 1, "mimeType": "image/png" if path.endswith('.png') else "image/jpeg", "name": os.path.basename(path)})
            textures.append({"sampler": 0, "source": len(images) - 1})
            img_idx[path] = len(textures) - 1
            return img_idx[path]
        def mat_id(m):
            if id(m) in mat_idx: return mat_idx[id(m)]
            d = {"name": m.name, "pbrMetallicRoughness": {"baseColorFactor": _lin(m.color) + [float(m.alpha)],
                 "metallicFactor": float(m.metal), "roughnessFactor": float(m.rough)}}
            if m.tex: d["pbrMetallicRoughness"]["baseColorTexture"] = {"index": tex_id(m.tex)}
            if m.nrm: d["normalTexture"] = {"index": tex_id(m.nrm), "scale": float(m.nstr)}
            if m.emissive is not None:
                d["emissiveFactor"] = _lin(m.emissive)
                if m.estr != 1.0: d.setdefault("extensions", {})["KHR_materials_emissive_strength"] = {"emissiveStrength": float(m.estr)}
            if m.alpha < 1.0: d["alphaMode"] = "BLEND"
            if m.double: d["doubleSided"] = True
            mats.append(d); mat_idx[id(m)] = len(mats) - 1; return mat_idx[id(m)]

        meshes = []; mesh_of = {}
        for n in nodes:
            if n.mesh is None or n.mesh.empty(): continue
            groups = {}
            for mat, g, j in n.mesh.items: groups.setdefault(id(mat), (mat, []))[1].append((g, j))
            prims = []
            skinned = n.skin is not None
            for mat, lst in groups.values():
                P = []; N = []; I = []; C = []; J = []; U = []; off = 0; anyc = any(g.C is not None for g, _ in lst)
                for g, j in lst:
                    P.append(g.P); N.append(g.N); I.append(g.I + off); off += len(g.P)
                    if mat.tex or mat.nrm: U.append(g.UV if g.UV is not None else _boxuv(g.P, g.N))
                    if anyc: C.append(g.C if g.C is not None else np.ones((len(g.P), 4)))
                    if skinned:
                        if getattr(g, 'JW', None) is not None: J.append(g.JW)   # smooth weights: (joints (n,4), weights (n,4))
                        else:
                            jj = np.zeros((len(g.P), 4), np.uint16); jj[:, 0] = 0 if j is None else j
                            ww = np.zeros((len(g.P), 4), np.float32); ww[:, 0] = 1; J.append((jj, ww))
                P = np.concatenate(P).astype(np.float32); N = np.concatenate(N).astype(np.float32)
                I = np.concatenate(I)
                it = np.uint16 if len(P) < 65535 else np.uint32
                attrs = {"POSITION": add_acc(P, 5126, "VEC3", 34962, True), "NORMAL": add_acc(N, 5126, "VEC3", 34962)}
                if U: attrs["TEXCOORD_0"] = add_acc((np.concatenate(U) / mat.tile).astype(np.float32), 5126, "VEC2", 34962)
                if anyc:
                    Cc = np.concatenate(C); Cc = np.concatenate([Cc[:, :3] ** 2.2, Cc[:, 3:]], 1).astype(np.float32)
                    attrs["COLOR_0"] = add_acc(Cc, 5126, "VEC4", 34962)
                if skinned:
                    jj = np.concatenate([x[0] for x in J]).astype(np.uint16); ww = np.concatenate([x[1] for x in J]).astype(np.float32)
                    attrs["JOINTS_0"] = add_acc(jj, 5123, "VEC4", 34962); attrs["WEIGHTS_0"] = add_acc(ww, 5126, "VEC4", 34962)
                ind = add_acc(I.astype(it).reshape(-1), 5123 if it == np.uint16 else 5125, "SCALAR", 34963)
                prims.append({"attributes": attrs, "indices": ind, "material": mat_id(mat), "mode": 4})
            meshes.append({"name": n.mesh.name or n.name, "primitives": prims}); mesh_of[id(n)] = len(meshes) - 1

        skins = []; skin_of = {}
        for n in nodes:
            if n.skin is None or id(n.skin) in skin_of: continue
            ibm = np.stack([np.linalg.inv(j.world()).T for j in n.skin.joints]).astype(np.float32)
            skins.append({"joints": [idx[id(j)] for j in n.skin.joints], "skeleton": idx[id(n.skin.skeleton)],
                          "inverseBindMatrices": add_acc(ibm.reshape(-1, 16), 5126, "MAT4")})
            skin_of[id(n.skin)] = len(skins) - 1

        jn = []
        for n in nodes:
            d = {"name": n.name}
            if n.children: d["children"] = [idx[id(c)] for c in n.children]
            if np.any(n.t != 0): d["translation"] = [float(x) for x in n.t]
            if np.any(n.r != QI): d["rotation"] = [float(x) for x in n.r]
            if np.any(n.s != 1): d["scale"] = [float(x) for x in n.s]
            if id(n) in mesh_of: d["mesh"] = mesh_of[id(n)]
            if n.skin is not None: d["skin"] = skin_of[id(n.skin)]
            if n.extras: d["extras"] = n.extras
            jn.append(d)

        anims = []
        for a in s.anims:
            samplers = []; chans = []
            for node, chpath, times, vals, interp in a.chans:
                v = vals.copy()
                if chpath == "rotation":
                    for k in range(1, len(v)):
                        if np.dot(v[k], v[k - 1]) < 0: v[k] = -v[k]
                    v = v / np.linalg.norm(v, axis=1, keepdims=True)
                ti = add_acc(times.reshape(-1), 5126, "SCALAR", None, True, key=("t", times.tobytes()))
                vi = add_acc(v.astype(np.float32), 5126, "VEC4" if chpath == "rotation" else "VEC3")
                samplers.append({"input": ti, "output": vi, "interpolation": interp})
                chans.append({"sampler": len(samplers) - 1, "target": {"node": idx[id(node)], "path": chpath}})
            anims.append({"name": a.name, "samplers": samplers, "channels": chans})

        js = {"asset": {"version": "2.0", "generator": "MissionMohakash glbkit"},
              "scene": 0, "scenes": [{"name": s.name, "nodes": [idx[id(r)] for r in s.roots]}],
              "nodes": jn, "materials": mats, "meshes": meshes, "accessors": accs, "bufferViews": views,
              "buffers": [{"byteLength": len(buf)}]}
        if images: js["images"] = images; js["textures"] = textures; js["samplers"] = [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]
        if skins: js["skins"] = skins
        if anims: js["animations"] = anims
        if any("extensions" in m for m in mats): js["extensionsUsed"] = ["KHR_materials_emissive_strength"]
        jb = json.dumps(js, separators=(',', ':')).encode()
        jb += b' ' * ((4 - len(jb) % 4) % 4)
        while len(buf) % 4: buf.append(0)
        total = 12 + 8 + len(jb) + 8 + len(buf)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'wb') as f:
            f.write(struct.pack('<4sII', b'glTF', 2, total))
            f.write(struct.pack('<I4s', len(jb), b'JSON')); f.write(jb)
            f.write(struct.pack('<I4s', len(buf), b'BIN\x00')); f.write(buf)
        return total
