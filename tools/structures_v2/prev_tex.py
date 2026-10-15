import numpy as np, math, sys
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection
from PIL import Image
from glbkit import *; from glbkit import _n, _boxuv
import preview
_cache = {}
def tex(p):
    if p not in _cache: _cache[p] = np.asarray(Image.open(p).convert('RGB')).astype(float) / 255
    return _cache[p]
def collect_tex(asset):
    out = []
    for n in asset.all_nodes():
        if n.mesh is None: continue
        W = n.world()
        for mat, g, j in n.mesh.items:
            P = g.P @ W[:3, :3].T + W[:3, 3]; Nn = g.N @ np.linalg.inv(W[:3, :3])
            I = g.I; col = np.tile(np.array(mat.color[:3]), (len(I), 1))
            if mat.tex:
                uv = (g.UV if g.UV is not None else _boxuv(g.P, g.N)) / mat.tile
                c = uv[I].mean(1); im = tex(mat.tex); h, w = im.shape[:2]
                col = col * im[(c[:, 1] * h).astype(int) % h, (c[:, 0] * w).astype(int) % w]
            out.append((P, Nn, I, col, mat))
    return out
def sheet(asset, path, views=((35, 22), (215, 22)), title=None):
    items = collect_tex(asset); fig, axs = plt.subplots(1, len(views), figsize=(6 * len(views), 6), facecolor='#1c2230')
    for ax, (az, el) in zip(np.atleast_1d(axs), views):
        a = math.radians(az); e = math.radians(el)
        fwd = np.array([math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e)])
        right = _n(np.cross([0, 1, 0], fwd)); up = np.cross(fwd, right); light = _n([0.4, 0.8, 0.5])
        polys = []; cols = []; dep = []
        for P, N, I, col, mat in items:
            tri = P[I]; nr = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); nr /= np.maximum(np.linalg.norm(nr, axis=1, keepdims=True), 1e-12)
            vis = nr @ fwd > -0.05; tri = tri[vis]; nr = nr[vis]; cc = col[vis]
            if not len(tri): continue
            sh = 0.35 + 0.65 * np.clip(nr @ light, 0, 1); c = cc * sh[:, None]
            if mat.emissive is not None: c = np.maximum(c, np.array(mat.emissive) * 0.9)
            polys += list(np.stack([tri @ right, tri @ up], -1)); cols += list(np.clip(c, 0, 1)); dep += list((tri @ fwd).mean(1))
        o = np.argsort(dep)
        ax.add_collection(PolyCollection([polys[i] for i in o], facecolors=[cols[i] for i in o], edgecolors='none'))
        allp = np.concatenate([P for P, *_ in items]); x = allp @ right; y = allp @ up
        cx, cy = (x.min() + x.max()) / 2, (y.min() + y.max()) / 2; r = max(np.ptp(x), np.ptp(y)) / 2 * 1.05
        ax.set_xlim(cx - r, cx + r); ax.set_ylim(cy - r, cy + r); ax.set_aspect('equal'); ax.axis('off'); ax.set_facecolor('#1c2230')
    if title: fig.suptitle(title, color='w')
    fig.tight_layout(); fig.savefig(path, dpi=60, facecolor='#1c2230'); plt.close(fig)
if __name__ == '__main__':
    from structures import JOBS
    d = dict(JOBS)
    for n in sys.argv[1:]: sheet(d[n](), f'prev/T_{n}.png', title=n)
