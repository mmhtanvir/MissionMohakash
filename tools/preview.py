import numpy as np, math
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection
from glbkit import *
from glbkit import _n

def collect(asset, pose=None):
    """returns list of (P(world), N(world), I, rgb) for every mesh item. pose: {nodename: (t|None, q|None)}"""
    saved = {}
    if pose:
        for n in asset.all_nodes():
            if n.name in pose:
                saved[id(n)] = (n.t.copy(), n.r.copy())
                t, q = pose[n.name]
                if t is not None: n.t = np.array(t, float)
                if q is not None: n.r = np.array(q, float)
    out = []
    for n in asset.all_nodes():
        if n.mesh is None: continue
        if n.skin is None:
            W = n.world()
            for mat, g, j in n.mesh.items:
                out.append((g.P @ W[:3, :3].T + W[:3, 3], g.N @ np.linalg.inv(W[:3, :3]), g.I, mat))
        else:
            jw = [jn.world() @ np.linalg.inv(_bind[id(jn)]) for jn in n.skin.joints]
            for mat, g, j in n.mesh.items:
                M = jw[j]
                out.append((g.P @ M[:3, :3].T + M[:3, 3], g.N @ np.linalg.inv(M[:3, :3]), g.I, mat))
    for n in asset.all_nodes():
        if id(n) in saved: n.t, n.r = saved[id(n)]
    return out

_bind = {}
def prepare_bind(asset):
    for n in asset.all_nodes(): _bind[id(n)] = n.world()

def draw(ax, items, az, el, lim=None):
    a = math.radians(az); e = math.radians(el)
    # camera basis
    fwd = np.array([math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e)])  # from target to camera
    right = _n(np.cross([0, 1, 0], fwd)); up = np.cross(fwd, right)
    light = _n([0.4, 0.8, 0.5])
    polys = []; cols = []; depth = []
    for P, N, I, mat in items:
        tri = P[I]; nrm = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
        vis = nrm @ fwd > -0.05
        tri = tri[vis]; nrm = nrm[vis]
        if len(tri) == 0: continue
        sh = 0.35 + 0.65 * np.clip(nrm @ light, 0, 1)
        base = np.array(mat.color[:3]); em = mat.emissive
        c = base[None] * sh[:, None]
        if em is not None: c = np.maximum(c, np.array(em)[None] * 0.9)
        c = np.clip(c, 0, 1)
        x = tri @ right; y = tri @ up; d = tri @ fwd
        polys += list(np.stack([x, y], -1)); cols += list(c); depth += list(d.mean(1))
    order = np.argsort(depth)
    pc = PolyCollection([polys[i] for i in order], facecolors=[cols[i] for i in order], edgecolors='none', linewidths=0)
    ax.add_collection(pc)
    allp = np.concatenate([P for P, _, _, _ in items])
    x = allp @ right; y = allp @ up
    cx, cy = (x.min() + x.max()) / 2, (y.min() + y.max()) / 2
    r = max(x.max() - x.min(), y.max() - y.min()) / 2 * 1.05
    ax.set_xlim(cx - r, cx + r); ax.set_ylim(cy - r, cy + r); ax.set_aspect('equal'); ax.axis('off')
    ax.set_facecolor('#1c2230')

def sheet(asset, path, views=((35, 22), (215, 22), (90, 5)), pose=None, title=None):
    prepare_bind(asset)
    items = collect(asset, pose)
    fig, axs = plt.subplots(1, len(views), figsize=(5 * len(views), 5), facecolor='#1c2230')
    axs = np.atleast_1d(axs)
    for ax, (az, el) in zip(axs, views): draw(ax, items, az, el)
    if title: fig.suptitle(title, color='w')
    fig.tight_layout(); fig.savefig(path, dpi=70, facecolor='#1c2230'); plt.close(fig)
