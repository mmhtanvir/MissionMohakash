"""
crew_hq.py - high-quality CrewCasual.glb (Milestone 06): smooth lofted body, sculpted head with eyes/ears/hair, fingered hands,
sneakers, procedural textures (knit tee, fleece joggers, skin pores, painted face, hair strands, shoe), smooth skin weights.

Same skeleton (bone names + CASUAL dimensions), sockets and 15 clips as crew.build_casual, so no C# changes are needed.
Upper arms are bound in an A-pose (bind rotation only; every clip sets absolute rotations, as in rig_eva.py).

    python3 crew_hq.py <out.glb> [texture_dir]
"""
import sys, os, math, numpy as np
from PIL import Image, ImageDraw, ImageFont
from glbkit import Asset, Node, Skin, MeshB, Mat, Geo, qaxis, qmat
import astronaut as AS
from astronaut import BONES
import crew, poses_extra as PX

D = PX.CASUAL
ARM = 14.0                      # deg, A-pose bind angle of the upper arms
BI = {b: i for i, b in enumerate(BONES)}
TAU = 2 * math.pi

def ss(a, b, x):
    t = np.clip((np.asarray(x, float) - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def gauss(x, mu, sig): return np.exp(-0.5 * ((np.asarray(x, float) - mu) / sig) ** 2)

def interp(x, keys):            # piecewise-linear profile, keys = [(x, value), ...] in any order
    xs, vs = zip(*sorted(keys)); return np.interp(x, xs, vs)

# =====================================================================================================  textures
def pnoise(h, w, sy, sx=None, seed=0):
    """periodic gaussian-filtered noise, std 1. sy/sx = blur sigma in px"""
    sx = sy if sx is None else sx
    r = np.random.default_rng(seed).standard_normal((h, w))
    fy = np.fft.fftfreq(h)[:, None]; fx = np.fft.fftfreq(w)[None, :]
    F = np.fft.fft2(r) * np.exp(-2 * math.pi ** 2 * ((fy * sy) ** 2 + (fx * sx) ** 2))
    o = np.real(np.fft.ifft2(F)); return (o - o.mean()) / (o.std() + 1e-9)

def fbm(h, w, base, oct=4, seed=0, aniso=1.0):
    o = np.zeros((h, w)); a = 1.0; t = 0
    for k in range(oct):
        o += a * pnoise(h, w, base / 2 ** k, base / 2 ** k * aniso, seed + k); t += a; a *= 0.5
    return o / t

def normal_map(hgt, strength):
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5 * strength
    dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5 * strength
    n = np.stack([-dx, dy, np.ones_like(hgt)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)

def save(img, path, q=90):
    a = np.clip(img, 0, 255).astype(np.uint8)
    Image.fromarray(a).save(path, quality=q) if path.endswith('.jpg') else Image.fromarray(a).save(path)
    return path

def col(rgb, shade):            # sRGB base colour * per-pixel shade -> HxWx3
    return np.asarray(rgb, float)[None, None, :] * shade[..., None]

def tex_knit(d, name, rgb, rib=False):
    H = W = 512; y, x = np.mgrid[0:H, 0:W]
    cw, ch = 8, 10                                   # stitch cell (px); 64 columns per tile
    a = (x % cw) / cw; b = (y % ch) / ch
    if rib:                                          # 1x1 rib: raised wales along v
        h = np.cos(TAU * x / cw) * 0.8 + 0.2 * np.cos(TAU * (b + np.abs(a - .5)))
    else:                                            # jersey: interlocking V loops
        h = np.sin(math.pi * a) ** 2 * (0.6 + 0.4 * np.cos(TAU * (b + np.abs(a - 0.5) * 1.2)))
    low = fbm(H, W, 40, 3, seed=11)
    shade = 0.9 + 0.12 * h + 0.035 * low + 0.02 * pnoise(H, W, 1.0, seed=3)
    return (save(col(rgb, shade), f'{d}/{name}.jpg'), save(normal_map(h * 1.2 + 0.4 * low, 1.6), f'{d}/{name}_n.jpg'))

def tex_fleece(d, name, rgb, rib=False):
    H = W = 512; y, x = np.mgrid[0:H, 0:W]
    fib = pnoise(H, W, 0.6, 3.5, seed=21)            # fibres streak along v (down the leg)
    mel = pnoise(H, W, 1.2, 1.2, seed=22)
    low = fbm(H, W, 50, 3, seed=23)
    h = 0.5 * fib + 0.3 * mel
    if rib: h = h * 0.4 + np.cos(TAU * x / 10) * 0.9
    shade = 0.93 + 0.07 * fib + 0.08 * np.where(mel > 1.4, 1, 0) * (mel - 1.4) + 0.03 * low + (0.06 * np.cos(TAU * x / 10) if rib else 0)
    return (save(col(rgb, shade), f'{d}/{name}.jpg'), save(normal_map(h + 0.3 * low, 1.3), f'{d}/{name}_n.jpg'))

def tex_skin(d):
    H = W = 512
    pores = pnoise(H, W, 0.9, seed=31); pores = np.where(pores < -1.6, pores + 1.6, 0)
    mott = fbm(H, W, 30, 4, seed=32); red = fbm(H, W, 45, 2, seed=33)
    shade = 0.975 + 0.018 * mott
    c = col(SKIN_RGB, shade); c[..., 0] += 2 * red; c[..., 2] -= 1 * red
    h = 0.25 * pnoise(H, W, 1.5, seed=34) + 2.0 * pores + 0.15 * mott
    return (save(c, f'{d}/skin.jpg'), save(normal_map(h, 1.0), f'{d}/skin_n.jpg'))

def tex_hair(d):
    H = W = 512
    st = pnoise(H, W, 9, 0.7, seed=41) * 0.7 + pnoise(H, W, 3, 0.5, seed=42) * 0.5       # streaks along v
    clump = fbm(H, W, 25, 3, seed=43)
    shade = 0.75 + 0.22 * st + 0.12 * clump
    c = col(HAIR_RGB, np.clip(shade, 0.4, 1.4))
    return (save(c, f'{d}/hair.jpg'), save(normal_map(st + 0.6 * clump, 2.0), f'{d}/hair_n.jpg'))

def tex_eye(d):
    H = W = 256; y, x = np.mgrid[0:H, 0:W]; u = x / W; v = y / H        # v = polar angle / pi (0 = cornea centre)
    pol = v * math.pi
    sclera = np.array([236, 230, 222.]); iris_c = np.array([72, 44, 26.]); iris_l = np.array([128, 86, 48.])
    streak = pnoise(H, W, 0.8, 0.6, seed=51) * 0.5 + 0.5
    radial = np.clip(pol / 0.48, 0, 1)
    iris = iris_c[None, None] * (1 - radial[..., None] * 0.3) + (iris_l - iris_c)[None, None] * (streak[..., None] * (1 - radial[..., None]) * 0.6)
    c = np.where((pol < 0.48)[..., None], iris, sclera[None, None] * (1 - 0.06 * ss(1.0, 1.8, pol))[..., None])
    c = np.where((pol < 0.50)[..., None] & (pol > 0.44)[..., None], c * 0.45, c)        # limbal ring
    c = np.where((pol < 0.19)[..., None], np.array([10, 8, 8.])[None, None], c)         # pupil
    veins = np.clip(pnoise(H, W, 1.2, seed=52) - 1.8, 0, 1) * ss(0.9, 1.5, pol)
    c[..., 1] -= 60 * veins; c[..., 2] -= 60 * veins
    return save(c, f'{d}/eye.jpg')

def tex_shoe(d):
    H = W = 512; y, x = np.mgrid[0:H, 0:W]; u = x / W; v = y / H     # u: around (0 = bottom centre), v: heel(0) -> toe(1)
    mesh = (np.sin(TAU * x / 6) * np.sin(TAU * y / 6))
    base = np.array([238, 238, 234.])
    shade = 0.95 + 0.03 * mesh + 0.02 * fbm(H, W, 30, 2, seed=61)
    c = col(base, shade)
    # accent swoosh on both sides (u ~ 0.25 and 0.75), rising from heel to mid-foot
    for uc in (0.25, 0.75):
        side = 1 if uc < 0.5 else -1
        curve = uc - side * (0.07 * np.sin(math.pi * np.clip(v * 1.4 - .1, 0, 1)) - 0.02)
        band = (np.abs(u - curve) < 0.018 * (1.2 - v)) & (v > 0.12) & (v < 0.78)
        c[band] = np.array([232, 98, 30.])
    # toe cap seam + heel tab
    c[(np.abs(v - 0.80) < 0.006)] *= 0.8
    c[(v < 0.06) & (np.abs(u - 0.5) < 0.08)] = np.array([40, 52, 92.])
    stitch = ((x % 6) < 3) & ((np.abs(v - 0.795) < 0.002) | (np.abs(v - 0.13) < 0.002))
    c[stitch] *= 0.75
    return (save(c, f'{d}/shoe.jpg'), save(normal_map(mesh * 0.6 + 0.2 * fbm(H, W, 20, 2, seed=62), 1.2), f'{d}/shoe_n.jpg'))

def tex_flag(d):
    W, H = 240, 150; im = Image.new('RGB', (W, H), (0, 106, 78)); dr = ImageDraw.Draw(im)
    r = 34; cx = int(W * 0.45); dr.ellipse((cx - r, H // 2 - r, cx + r, H // 2 + r), fill=(244, 42, 65))
    dr.rectangle((1, 1, W - 2, H - 2), outline=(230, 225, 210), width=5)
    for i in range(0, W, 8): dr.line((i, 3, i + 4, 3), fill=(180, 175, 160))
    im.save(f'{d}/flag.png'); return f'{d}/flag.png'

def tex_patch(d):
    S = 256; im = Image.new('RGB', (S, S), (238, 238, 234)); dr = ImageDraw.Draw(im)
    dr.ellipse((6, 6, S - 6, S - 6), fill=(18, 30, 70), outline=(232, 98, 30), width=10)
    dr.ellipse((70, 70, 186, 186), fill=(200, 200, 205))                  # Moon
    for (cx, cy, r) in ((105, 110, 14), (150, 140, 10), (125, 160, 8), (155, 95, 7)): dr.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(160, 160, 168))
    dr.polygon([(40, 200), (128, 52), (216, 200)], outline=(232, 98, 30), width=6)   # trajectory chevron
    try: f = ImageFont.load_default(size=22)
    except TypeError: f = ImageFont.load_default()
    for txt, yy in (('MOHAKASH', 24), ('OUTPOST ZERO', 210)):
        w = dr.textlength(txt, font=f); dr.text(((S - w) / 2, yy), txt, fill=(255, 255, 255), font=f)
    im.save(f'{d}/patch.png'); return f'{d}/patch.png'

SKIN_RGB = (150, 102, 72)
HAIR_RGB = (30, 24, 21)

# =====================================================================================================  head texture (painted in head UV)
HEAD_EYE_Y = 1.615                  # world y of the eye line
HS0, HS1 = -0.122, 0.118            # head slice range (s = y - eye line)
EYE_X = 0.0315
EYE_A = math.asin((EYE_X / 0.0735) ** 1.25)   # angle of the eye centre on the head (superellipse p=2.5)

def head_uv(th, s):                 # th: angle from +Z (front) toward +X, s: height relative to eye line
    return (th / TAU + 0.5) % 1.0, (HS1 - s) / (HS1 - HS0)

def hair_mask(th, s, sideburns=True, soft=0.005):
    """1 = hair, 0 = skin; smooth edge. th from front toward +X, s relative to eye line."""
    a = np.abs(np.angle(np.exp(1j * th)))            # 0 front .. pi back
    line = interp(a, [(0, .080), (0.35, .078), (0.6, .068), (0.9, .052), (1.22, .032), (1.62, .038), (2.1, -.014), (2.6, -.042), (np.pi, -.050)])
    line = line + 0.003 * np.sin(th * 9) * ss(0.8, 0.0, a)
    sideburn = gauss(a, 1.33, 0.025) * ss(-0.024, -0.010, s)
    m = ss(-soft, soft, s - line)
    return np.maximum(m, sideburn) if sideburns else m

def tex_head(d, hair_rgb=HAIR_RGB):
    H = W = 1024; y, x = np.mgrid[0:H, 0:W]
    th = (x / W - 0.5) * TAU; s = HS1 - y / H * (HS1 - HS0)
    a = np.abs(th)
    mott = fbm(H, W, 40, 4, seed=71); fine = pnoise(H, W, 1.0, seed=72)
    c = col(SKIN_RGB, 0.97 + 0.03 * mott + 0.012 * fine)
    def tint(mask, rgb, k):
        nonlocal c; c = c * (1 - k * mask[..., None]) + np.asarray(rgb, float)[None, None] * (k * mask[..., None])
    # warm cheeks, nose and ears; cooler/darker under eyes
    tint(gauss(a, 0.62, 0.22) * gauss(s, -0.030, 0.020), (176, 96, 76), 0.22)
    tint(gauss(a, 0.0, 0.10) * gauss(s, -0.030, 0.014), (172, 98, 80), 0.18)
    tint(gauss(a, EYE_A, 0.13) * gauss(s, -0.012, 0.006), (98, 64, 52), 0.25)
    # beard shadow / stubble on jaw, chin and upper lip
    stub = (ss(-0.030, -0.050, s) * ss(1.45, 1.25, a) * (1 - gauss(a, 0, 0.16) * gauss(s, -0.064, 0.009)))
    stub = np.clip(stub, 0, 1) * (0.65 + 0.35 * (pnoise(H, W, 0.7, seed=73) > 0.3))
    tint(stub, (88, 64, 52), 0.22)
    # lips
    lipmask = gauss(a, 0, 0.125) ** 1.5 * (gauss(s, -0.0575, 0.0045) + gauss(s, -0.0685, 0.0055))
    lipmask = np.clip(lipmask * 1.4, 0, 1)
    tint(lipmask, (128, 66, 58), 0.75)
    c *= (1 - 0.35 * gauss(a, 0, 0.12) * gauss(s, -0.0632, 0.0011))[..., None]        # mouth line
    # eyebrows: arched dense strokes
    for side in (1, -1):
        t = np.clip((side * th - (EYE_A - 0.24)) / 0.50, 0, 1)
        arch = 0.0245 + 0.006 * np.sin(math.pi * np.clip(t * 1.1, 0, 1)) - 0.002 * t
        inside = ((side * th) > EYE_A - 0.24) & ((side * th) < EYE_A + 0.30)
        thick = 0.0045 * (1.15 - 0.6 * t)
        strokes = pnoise(H, W, 0.4, 2.5, seed=74 + side) > -0.2
        m = inside & (np.abs(s - arch) < thick) & strokes
        c[m] = c[m] * 0.25 + np.asarray(hair_rgb, float) * 0.75
        # eyelash line (upper lid edge) + soft lower lid shadow
        lid = (np.abs(side * th - EYE_A) < 0.15) & (np.abs(s - (0.0085 - 0.25 * (side * th - EYE_A) ** 2)) < 0.0012)
        c[lid] = np.asarray(hair_rgb, float) * 0.9
    # scalp under the hair (so the shell's thin edge never shows bright skin) + ear inner shading
    hm = hair_mask(th, s + 0.0125 + 0.0018 * pnoise(H, W, 1.2, 4, seed=76), soft=0.0025)
    tint(hm, np.asarray(hair_rgb) * 0.85, 1.0)
    hgt = 0.35 * fine + 0.2 * mott + 1.2 * np.clip(pnoise(H, W, 0.8, seed=75) - 1.7, 0, 1)
    hgt += lipmask * 0.6 * np.sin(TAU * x / 5)                                          # vertical lip lines
    return (save(c, f'{d}/head.jpg', 92), save(normal_map(hgt, 0.9), f'{d}/head_n.jpg'))

# =====================================================================================================  geometry
def grid_normals(P, closed=True):
    """P (K, n, 3) ring grid -> smooth normals via central differences (wraps around the ring)."""
    if closed: du = np.roll(P, -1, 1) - np.roll(P, 1, 1)
    else: du = np.gradient(P, axis=1)
    dv = np.zeros_like(P); dv[1:-1] = P[2:] - P[:-2]; dv[0] = P[1] - P[0]; dv[-1] = P[-1] - P[-2]
    N = np.cross(du, dv)
    ln = np.linalg.norm(N, axis=-1, keepdims=True)
    # collapsed rings (poles): use the loft direction
    pole = ln[..., 0] < 1e-9
    if pole.any():
        axis = np.zeros_like(P); axis[0] = P[0].mean(0) - P[1].mean(0); axis[-1] = P[-1].mean(0) - P[-2].mean(0)
        N = np.where(pole[..., None], axis, N); ln = np.linalg.norm(N, axis=-1, keepdims=True)
    N = N / np.maximum(ln, 1e-12)
    c = P.mean(1, keepdims=True)
    if ((P - c) * N).sum() < 0: N = -N
    return N

def grid_geo(P, uv_u, uv_v, N=None, seam=True):
    """P (K, n, 3); uv arrays (K, n+1) / broadcastable. Duplicates the seam column for UVs."""
    K, n, _ = P.shape
    if N is None: N = grid_normals(P, closed=seam)
    if seam:
        P = np.concatenate([P, P[:, :1]], 1); N = np.concatenate([N, N[:, :1]], 1); n1 = n + 1
    else: n1 = n
    idx = np.arange(K * n1).reshape(K, n1)
    a, b, c_, d = idx[:-1, :-1], idx[:-1, 1:], idx[1:, :-1], idx[1:, 1:]
    I = np.stack([np.stack([a, c_, b], -1), np.stack([b, c_, d], -1)], 2).reshape(-1, 3)
    g = Geo(P.reshape(-1, 3), N.reshape(-1, 3), I)
    U = np.broadcast_to(uv_u, (K, n1)); V = np.broadcast_to(uv_v, (K, n1))
    g.UV = np.stack([U.reshape(-1), V.reshape(-1)], 1).astype(float)
    return g

def ring_angles(n, start=math.pi):   # start at the back so the UV seam is hidden
    return start + TAU * np.arange(n) / n

def tube(C, rx, rz, n, X=None, Z=None, off=None, p=2.0, u_rep=1.0, v_per_m=1.0, th0=math.pi):
    """loft rings around centre path C (K,3). rx/rz: (K,) or (K,n). off: (K,n) radial offset. Cross-section is a
    superellipse of exponent p (scalar or (K,1)). Angle 0 = +Z (front), +pi/2 = +X."""
    C = np.asarray(C, float); K = len(C); th = ring_angles(n, th0)
    st, ct = np.sin(th)[None], np.cos(th)[None]
    pp = np.broadcast_to(np.asarray(p, float).reshape(-1, 1) if np.ndim(p) else np.full((1, 1), p), (K, 1))
    e = 2.0 / pp
    sx = np.sign(st) * np.abs(st) ** e; sz = np.sign(ct) * np.abs(ct) ** e
    rx = np.broadcast_to(np.asarray(rx, float).reshape(K, -1), (K, n)); rz = np.broadcast_to(np.asarray(rz, float).reshape(K, -1), (K, n))
    o = 0 if off is None else off
    if X is None:   # rings perpendicular to the path, X kept close to world +X (parallel transport)
        T_ = np.gradient(C, axis=0); T_ /= np.maximum(np.linalg.norm(T_, axis=1, keepdims=True), 1e-12)
        Xs = np.zeros((K, 3)); x = np.array([1.0, 0, 0])
        if abs(T_[0] @ x) > 0.9: x = np.array([0, 0, 1.0])
        for k in range(K):
            x = x - T_[k] * (x @ T_[k]); x /= np.linalg.norm(x); Xs[k] = x
        X = Xs; Z = np.cross(X, -T_)        # T points along the path; for a downward path Z = +Z (front)
        Z /= np.linalg.norm(Z, axis=1, keepdims=True)
        if Z[:, 2].mean() < -1e-6: Z = -Z     # keep 'front' toward +Z for upward paths too
    X = np.broadcast_to(np.asarray(X, float).reshape(-1, 3), (K, 3)); Z = np.broadcast_to(np.asarray(Z, float).reshape(-1, 3), (K, 3))
    P = C[:, None] + X[:, None] * ((rx + o) * sx)[..., None] + Z[:, None] * ((rz + o) * sz)[..., None]
    L = np.r_[0, np.cumsum(np.linalg.norm(np.diff(C, axis=0), axis=1))]
    u = (np.arange(n + 1) / n * u_rep)[None]; v = (L * v_per_m)[:, None]
    return grid_geo(P, u, v), P, th

def weights_from(dense):
    """dense (M,17) -> top-4 (joints uint16, weights float32)"""
    idx = np.argsort(-dense, axis=1)[:, :4]; w = np.take_along_axis(dense, idx, 1)
    w = w / np.maximum(w.sum(1, keepdims=True), 1e-9); return idx.astype(np.uint16), w.astype(np.float32)

def keyed(t, keys):
    """t (M,) -> dense (M,17); keys = [(t, {bone: w}), ...] sorted by t, linear blend between neighbours"""
    t = np.asarray(t, float); out = np.zeros((len(t), len(BONES)))
    ts = np.array([k[0] for k in keys])
    order = np.argsort(ts); ts = ts[order]; ks = [keys[i][1] for i in order]
    i1 = np.clip(np.searchsorted(ts, t), 1, len(ts) - 1); i0 = i1 - 1
    f = np.clip((t - ts[i0]) / np.maximum(ts[i1] - ts[i0], 1e-9), 0, 1)
    for k in range(len(ts)):
        for b, w in ks[k].items():
            out[:, BI[b]] += w * ((i0 == k) * (1 - f) + (i1 == k) * f)
    return out

def mirror_bones(dense):
    out = dense.copy()
    for b in BONES:
        if b.startswith('Left'): out[:, BI[b]], out[:, BI['Right' + b[4:]]] = dense[:, BI['Right' + b[4:]]], dense[:, BI[b]]
    return out

def T4(t): m = np.eye(4); m[:3, 3] = t; return m
def R4(q): m = np.eye(4); m[:3, :3] = qmat(q); return m
def S4(x, y, z): return np.diag([x, y, z, 1.0])

class Body:
    """collects geos + dense weights; adds them to a MeshB at the end"""
    def __init__(s): s.parts = []
    head = False                     # parts added while True go to the separate Crew_Head mesh
    def add(s, g, mat, dense, M=None):
        if M is not None: g = g.xf(M)
        g.JW = weights_from(dense); s.parts.append((g, mat, s.head))
    def into(s, m, head=False):
        for g, mat, h in s.parts:
            if h == head: m.items.append((mat, g, None))

# ------------------------------------------------------------------------------------------------- torso (tee) + neck
def build_torso(B):
    ys = np.r_[np.linspace(0.895, 0.905, 3), np.linspace(0.91, 1.40, 40), np.linspace(1.405, 1.462, 8)]
    K = len(ys); n = 60
    W = interp(ys, [(0.895, .175), (0.93, .171), (1.00, .163), (1.10, .158), (1.22, .170), (1.30, .178), (1.36, .181),
                    (1.40, .174), (1.42, .156), (1.44, .122), (1.452, .093), (1.462, .072)])
    F = interp(ys, [(0.895, .113), (1.00, .107), (1.12, .107), (1.24, .118), (1.30, .118), (1.36, .108), (1.40, .095),
                    (1.43, .080), (1.452, .068), (1.462, .061)])
    Bk = interp(ys, [(0.895, .123), (0.95, .113), (1.00, .105), (1.12, .101), (1.26, .108), (1.34, .108), (1.40, .098), (1.43, .085),
                     (1.452, .072), (1.462, .064)])
    pexp = interp(ys, [(0.9, 2.2), (1.2, 2.5), (1.38, 2.6), (1.44, 2.0)])
    th = ring_angles(n); front = np.cos(th) > 0
    rz = np.where(front[None], F[:, None], Bk[:, None])
    # cloth folds: soft bunching above the waistband, diagonal drag lines under the arms, gentle overall noise
    Y = ys[:, None]; T_ = th[None]
    rng = np.random.default_rng(5); ph = rng.uniform(0, TAU, 6)
    off = 0.0035 * np.sin(TAU * Y / 0.045 + 1.3 * np.sin(3 * T_ + ph[0])) * ss(1.06, 0.95, Y) * ss(0.90, 0.93, Y)
    off += 0.0025 * np.sin(TAU * (Y * 9 + np.abs(np.sin(T_)) * 3.0) + ph[1]) * gauss(np.abs(np.sin(T_)), 1, 0.25) * gauss(Y, 1.20, 0.07)
    off += 0.0015 * np.sin(5 * T_ + ph[2] + Y * 20) * ss(1.38, 1.30, Y)
    off += -0.003 * gauss(Y, 0.897, 0.003)                                       # hem fold
    g, P, _ = tube(np.c_[np.zeros(K), ys, np.zeros(K)], W, rz, n, off=off, p=pexp[:, None], u_rep=10, v_per_m=10)
    Pf = g.P
    dense = keyed(Pf[:, 1], [(0.90, {'Hips': 1}), (0.97, {'Hips': .7, 'Spine': .3}), (1.06, {'Spine': 1}), (1.18, {'Chest': .6, 'Spine': .4}), (1.26, {'Chest': 1})])
    for sx, side in ((1, 'Left'), (-1, 'Right')):
        wa = ss(0.12, 0.19, sx * Pf[:, 0]) * ss(1.27, 1.36, Pf[:, 1]) * 0.6
        dense = dense * (1 - wa[:, None]); dense[:, BI[side + 'UpperArm']] += wa
    B.add(g, TEE, dense)
    # collar rib: a rolled band around the neck hole
    cy = np.linspace(-1, 1, 7); ang = cy * math.pi / 2
    C = np.c_[np.zeros(7), 1.462 + 0.006 * np.sin(ang), np.zeros(7)]
    bulge = 0.006 * np.cos(ang)
    g, P, _ = tube(C, (0.072 + bulge)[:, None] * np.ones((1, n)), np.where(front[None], 0.061 + bulge[:, None], 0.064 + bulge[:, None]), n, u_rep=24, v_per_m=40)
    B.add(g, TEE_RIB, keyed(g.P[:, 1], [(1.40, {'Chest': 1}), (1.50, {'Chest': 1})]))
    # neck
    ys = np.linspace(1.40, 1.575, 16); K = len(ys); n = 40
    rx = interp(ys, [(1.40, .066), (1.47, .061), (1.52, .059), (1.575, .060)])
    rzf = interp(ys, [(1.40, .058), (1.47, .056), (1.52, .052), (1.575, .046)])
    th = ring_angles(n); off = 0.004 * gauss(np.cos(th), 1, 0.08)[None] * gauss(ys, 1.505, 0.012)[:, None]   # adam's apple
    off += 0.003 * gauss(np.abs(np.sin(th)), 0.75, 0.12)[None] * ss(1.44, 1.52, ys)[:, None]                 # sternomastoid
    C = np.c_[np.zeros(K), ys, interp(ys, [(1.40, 0), (1.575, -0.012)])]
    g, P, _ = tube(C, rx, rzf, n, off=off, u_rep=3, v_per_m=10)
    B.add(g, SKIN, keyed(g.P[:, 1], [(1.40, {'Chest': 1}), (1.455, {'Neck': 1}), (1.52, {'Neck': .5, 'Head': .5}), (1.56, {'Head': 1})]))

# ------------------------------------------------------------------------------------------------- head
def head_profile(s, th):
    """radius field of the head: returns P (…,3) relative to the head centre (0, HEAD_EYE_Y, 0)."""
    a = np.abs(np.angle(np.exp(1j * th))); ct = np.cos(th); st = np.sin(th)
    W = interp(s, [(-.122, .012), (-.118, .026), (-.11, .040), (-.095, .052), (-.075, .061), (-.05, .067), (-.025, .071),
                   (0, .0735), (.03, .0750), (.06, .0745), (.08, .069), (.10, .054), (.11, .040), (.116, .022), (.118, .002)])
    F = interp(s, [(-.122, .040), (-.118, .062), (-.11, .082), (-.10, .088), (-.08, .089), (-.06, .091), (-.04, .090),
                   (-.02, .089), (0, .088), (.02, .094), (.04, .092), (.07, .084), (.09, .072), (.105, .052), (.115, .028), (.118, .002)])
    Bk = interp(s, [(-.122, .030), (-.11, .042), (-.09, .056), (-.07, .068), (-.05, .080), (-.02, .098), (.01, .106),
                    (.04, .107), (.07, .098), (.09, .084), (.105, .062), (.115, .032), (.118, .002)])
    depth = np.where(ct > 0, F, Bk)
    p = np.where(ct > 0, interp(s, [(-.12, 2.0), (-.09, 2.3), (-.04, 2.6), (0, 2.5), (.05, 2.3), (.10, 2.0)]), 2.1)
    e = 2.0 / p
    x = W * np.sign(st) * np.abs(st) ** e; z = depth * np.sign(ct) * np.abs(ct) ** e
    # features: radial displacement along the horizontal outward direction
    fw = ct.clip(0, 1)
    d = np.zeros_like(x)
    nose_w = 0.085 + 0.075 * ss(0.012, -0.036, s)
    d += (0.004 + 0.010 * ss(0.012, -0.032, s)) * gauss(a, 0, nose_w) * ss(-0.051, -0.036, s) * ss(0.026, 0.008, s)
    d += 0.005 * gauss(a, 0, 0.13) * gauss(s, -0.036, 0.006)                              # rounded tip   # bridge -> tip
    d += 0.006 * gauss(a, 0.12, 0.05) * gauss(s, -0.040, 0.0045)                          # alae (nostril wings)
    d -= 0.003 * gauss(a, 0.09, 0.025) * gauss(s, -0.044, 0.002)                          # nostril crease
    d -= 0.011 * gauss(a, EYE_A, 0.16) * gauss(s, 0.002, 0.009)                           # eye sockets
    d += 0.0035 * gauss(a, EYE_A, 0.13) * gauss(s, 0.0105, 0.003)                         # upper eyelid
    d += 0.0015 * gauss(a, EYE_A, 0.11) * gauss(s, -0.0085, 0.0025)                       # lower eyelid
    d += 0.005 * gauss(a, 0.38, 0.28) * gauss(s, 0.024, 0.007)                           # brow ridge
    d += 0.005 * gauss(a, 0.78, 0.18) * gauss(s, -0.024, 0.014)                          # cheekbones
    d += 0.0055 * gauss(a, 0, 0.14) * gauss(s, -0.0575, 0.0042)                          # upper lip
    d += 0.0065 * gauss(a, 0, 0.13) * gauss(s, -0.0690, 0.0050)                          # lower lip
    d -= 0.0028 * gauss(a, 0, 0.14) * gauss(s, -0.0632, 0.0012)                          # mouth line
    d -= 0.0018 * gauss(a, 0, 0.035) * gauss(s, -0.050, 0.004)                           # philtrum
    d -= 0.0035 * gauss(a, 0, 0.20) * gauss(s, -0.080, 0.005)                            # chin crease
    d += 0.0065 * gauss(a, 0, 0.24) * gauss(s, -0.099, 0.010)                            # chin
    d -= 0.002 * gauss(a, 0.36, 0.06) * gauss(s, -0.052, 0.010)                          # nasolabial fold
    d *= fw ** 0.5
    ln = np.sqrt(x * x + z * z) + 1e-9
    return np.stack([x + d * x / ln, np.broadcast_to(s, x.shape) * 1.0, z + d * z / ln], -1)

def build_head(B):
    s = np.unique(np.r_[np.linspace(HS0, -0.085, 8), np.linspace(-0.085, 0.032, 70), np.linspace(0.032, HS1, 20)])
    n = 84; th = ring_angles(n, start=math.pi)              # seam at the back (under the hair)
    S_, T_ = np.meshgrid(s, th, indexing='ij')
    P = head_profile(S_, T_) + np.array([0, HEAD_EYE_Y, 0])
    u, v = head_uv(np.r_[th, th[:1] + TAU], s[:, None])
    u = np.broadcast_to(np.r_[(th / TAU + 0.5) % 1.0, 1.0][None], (len(s), n + 1)).copy(); u[:, 0] = 0.0
    g = grid_geo(P, u, np.broadcast_to(v, (len(s), n + 1)))
    B.add(g, FACE, keyed(g.P[:, 1], [(1.4, {'Head': 1}), (1.8, {'Head': 1})]))
    # hair shell: offset along the normal by thickness * mask; keep only hair faces
    N = grid_normals(P)
    m = hair_mask(T_, S_, sideburns=False, soft=0.012) ** 1.5
    a = np.abs(np.angle(np.exp(1j * T_)))
    thick = interp(a, [(0, .016), (0.8, .012), (1.3, .006), (2.0, .008), (np.pi, .009)]) * (1 + 0.6 * ss(0.06, 0.11, S_))
    clump = 0.18 * np.sin(T_ * 23 + S_ * 160) * np.sin(T_ * 7 - S_ * 90)
    Ph = P + N * (thick * (1 + clump) * m)[..., None] + N * 0.0006
    keep = m > 0.03
    gh = grid_geo(Ph, np.broadcast_to(np.r_[np.arange(n) / n, 1.0][None] * 6, (len(s), n + 1)), np.broadcast_to((s[:, None] - HS0) * 30, (len(s), n + 1)))
    # drop faces with any vertex outside the hair
    keep1 = np.concatenate([keep, keep[:, :1]], 1).reshape(-1)
    gh.I = gh.I[keep1[gh.I].all(1)]
    B.add(gh, HAIR, keyed(gh.P[:, 1], [(1.4, {'Head': 1}), (1.8, {'Head': 1})]))
    # eyes: textured spheres in the sockets, cornea bulge at the front
    for sx in (1, -1):
        ang = np.linspace(0, math.pi, 22); r = 0.0122 * (1 + 0.06 * gauss(ang, 0, 0.30))
        C = np.c_[np.zeros(22), np.zeros(22), r * np.cos(ang)]; rad = r * np.sin(ang)
        ge, _, _ = tube(C, rad, rad, 28, X=(1, 0, 0), Z=(0, 1, 0), th0=0)
        ge.UV = np.c_[ge.UV[:, 0], np.repeat(ang / math.pi, 29)]
        zs = head_profile(np.array(0.002), np.array(EYE_A))[2]
        M = T4((sx * EYE_X, HEAD_EYE_Y + 0.002, zs - 0.0122 + 0.0065)) @ R4(qaxis('y', sx * 4.0))
        B.add(ge, EYE, keyed(np.zeros(len(ge.P)), [(0, {'Head': 1}), (1, {'Head': 1})]), M)
    # ears: cupped shell with a rolled rim
    for sx in (1, -1):
        k = np.linspace(0, 1, 14); n = 30; th = ring_angles(n, 0)
        rad = 0.030 * np.sqrt(np.clip(1 - (1 - k) ** 2, 0, 1)) * (1 + 0.12 * gauss(k, 1, 0.12))
        dep = 0.006 + 0.008 * (1 - k) ** 2 - 0.003 * gauss(k, 0.92, 0.05)
        Pk = np.zeros((14, n, 3))
        shape = 1 + 0.22 * np.cos(th - 0.3) - 0.10 * np.cos(2 * th)
        Pk[..., 1] = (rad[:, None] * np.cos(th)[None] * 1.0) * shape * 1.05            # up
        Pk[..., 2] = (rad[:, None] * np.sin(th)[None] * 0.62) * shape                  # front/back
        Pk[..., 0] = dep[:, None] - 0.008 * (1 - k[:, None])
        back = Pk.copy(); back[..., 0] = -0.006 + 0.0 * back[..., 0]
        P2 = np.concatenate([Pk, back[::-1][1:]], 0)
        ge = grid_geo(P2, np.broadcast_to(np.r_[np.arange(n) / n, 1][None], (len(P2), n + 1)), np.broadcast_to(np.linspace(0, 1, len(P2))[:, None], (len(P2), n + 1)))
        x0 = 0.0725
        M = T4((sx * x0, HEAD_EYE_Y - 0.010, -0.010)) @ R4(qaxis('y', sx * -12)) @ R4(qaxis('x', -12)) @ S4(sx, 1, 1)
        B.add(ge, SKIN, keyed(np.zeros(len(ge.P)), [(0, {'Head': 1}), (1, {'Head': 1})]), M)

# ------------------------------------------------------------------------------------------------- arms (built for the LEFT arm in arm space: origin = shoulder joint, -Y down the arm, +X outward, +Z front)
UA, LA = D['ua'], D['la']

def build_arm(B, side, sx, flag):
    watch = flag
    parts = []
    # short sleeve with a domed cap over the shoulder
    ys = np.r_[np.linspace(0.052, 0.03, 6), np.linspace(0.02, -0.120, 14), np.linspace(-0.123, -0.131, 3)]
    K = len(ys); n = 40
    cap = np.sqrt(np.clip(1 - ((ys - 0.02) / 0.036).clip(0) ** 2, 0, 1))
    r = interp(ys, [(-0.131, .0645), (-0.127, .0685), (-0.123, .069), (-0.10, .068), (-0.04, .066), (0.0, .064), (0.02, .062)]) * np.where(ys > 0.02, cap, 1)
    r = np.maximum(r, 0.004)
    th = ring_angles(n)
    off = 0.0025 * np.sin(TAU * (ys[:, None] / 0.05) + 2 * np.sin(th[None] * 2)) * ss(-0.02, -0.11, ys)[:, None]   # sleeve folds
    off = off * ss(0.008, 0.016, r[:, None])
    C = np.c_[np.where(ys > 0, -0.012 * ss(0, 0.07, ys), 0), ys, np.zeros(K)]
    g, P, _ = tube(C, r * 1.0, r * 1.06, n, off=off, u_rep=4, v_per_m=10)
    parts.append((g, TEE, keyed(g.P[:, 1], [(0.05, {'Chest': .45, side + 'UpperArm': .55}), (0.0, {side + 'UpperArm': 1}), (-0.2, {side + 'UpperArm': 1})])))
    # skin: upper arm -> elbow -> forearm -> wrist
    ys = np.linspace(-0.09, -0.535, 42); K = len(ys); n = 28
    rx = interp(ys, [(-0.09, .054), (-0.16, .056), (-0.23, .048), (-0.27, .043), (-0.31, .050), (-0.36, .048), (-0.45, .036), (-0.51, .028), (-0.535, .027)])
    rz = interp(ys, [(-0.09, .057), (-0.16, .060), (-0.23, .050), (-0.27, .045), (-0.31, .048), (-0.36, .045), (-0.45, .036), (-0.51, .033), (-0.535, .032)])
    th = ring_angles(n)
    off = 0.004 * gauss(np.cos(th), 1, 0.35)[None] * gauss(ys, -0.165, 0.035)[:, None]                 # biceps
    off += 0.003 * gauss(np.cos(th), -1, 0.4)[None] * gauss(ys, -0.13, 0.05)[:, None]                  # triceps
    off += 0.003 * gauss(np.cos(th), -1, 0.15)[None] * gauss(ys, -0.272, 0.008)[:, None]               # elbow point
    C = np.c_[np.zeros(K), ys, np.zeros(K)]
    g, P, _ = tube(C, rx, rz, n, off=off, u_rep=2, v_per_m=12)
    parts.append((g, SKIN, keyed(g.P[:, 1], [(-0.09, {side + 'UpperArm': 1}), (-0.245, {side + 'UpperArm': 1}), (-0.295, {side + 'LowerArm': 1}),
                                             (-0.50, {side + 'LowerArm': 1}), (-0.535, {side + 'Hand': 1})])))
    # hand: palm faces the body (-X), fingers relaxed and slightly curled
    yw = -(UA + LA)
    hk = [(yw + 0.02, {side + 'LowerArm': .6, side + 'Hand': .4}), (yw - 0.01, {side + 'Hand': 1}), (yw - 0.3, {side + 'Hand': 1})]
    ys = np.linspace(yw + 0.012, yw - 0.098, 18); K = len(ys)
    pw = interp(ys, [(yw + .012, .030), (yw - .02, .038), (yw - .07, .045), (yw - .098, .041)])        # half width along Z
    pt = interp(ys, [(yw + .012, .019), (yw - .02, .017), (yw - .07, .014), (yw - .098, .012)])        # half thickness along X
    C = np.c_[interp(ys, [(yw, 0), (yw - .1, -0.004)]), ys, np.zeros(K) + 0.002]
    g, P, _ = tube(C, pt, pw, 28, p=3.0, u_rep=1, v_per_m=12)
    parts.append((g, SKIN, keyed(g.P[:, 1], hk)))
    # fingers
    fy = yw - 0.094
    for zc, ln, rr, curl in ((0.028, .074, .0095, 1.0), (0.0095, .082, .0098, 1.1), (-0.0095, .077, .0092, 1.15), (-0.027, .062, .0080, 1.25)):
        segs = np.array([0.42, 0.33, 0.25]) * ln; angs = np.radians([12, 22, 18]) * curl
        pts = [np.array([-0.002, fy + (0.006 if abs(zc) > 0.02 else 0), zc])]; dirv = np.array([0, -1.0, 0]); a = 0
        for L_, an in zip(segs, angs):
            a += an
            for k in range(1, 5):
                d = np.array([-math.sin(a), -math.cos(a), 0.0])
                pts.append(pts[-1] + d * L_ / 4)
        pts = np.array(pts); K = len(pts)
        t = np.linspace(0, 1, K)
        rad = rr * interp(t, [(0, 1.0), (0.4, 0.92), (0.43, 0.97), (0.75, 0.84), (0.78, 0.88), (0.94, 0.78), (1.0, 0.0)])
        rad[-1] = 0; rad[-2] *= 0.75
        g, P, _ = tube(pts, rad * 0.92, rad, 12, u_rep=1, v_per_m=12)
        parts.append((g, SKIN, keyed(np.zeros(len(g.P)), [(0, {side + 'Hand': 1}), (1, {side + 'Hand': 1})])))
        # nail on the back (+X) of the fingertip
        tip = pts[int(K * 0.86)]; dt = pts[-2] - pts[int(K * 0.72)]; dt /= np.linalg.norm(dt)
        gn, _, _ = tube(np.array([tip - dt * 0.007, tip + dt * 0.0065]), np.array([.0058, .0050]), np.array([.0058, .0050]) * 0.35, 10,
                        X=np.cross(dt, (1, 0, 0)) / max(np.linalg.norm(np.cross(dt, (1, 0, 0))), 1e-6), Z=(1, 0, 0))
        gn = gn.xf(T4((rr * 0.62, 0, 0)))
        parts.append((gn, NAIL, keyed(np.zeros(len(gn.P)), [(0, {side + 'Hand': 1}), (1, {side + 'Hand': 1})])))
    # thumb: from the palm base toward the front, curled in
    base = np.array([-0.008, yw - 0.030, 0.026]); pts = [base]; a = 0.0
    for L_, an in ((0.040, 0.0), (0.032, 0.25), (0.026, 0.30)):
        a += an
        for k in range(1, 6):
            d = np.array([-0.30 - 0.7 * a, -0.85, 0.42 - 0.6 * a]); d /= np.linalg.norm(d)
            pts.append(pts[-1] + d * L_ / 5)
    pts = np.array(pts); K = len(pts); t = np.linspace(0, 1, K)
    rad = interp(t, [(0, .016), (0.3, .0125), (0.62, .0108), (0.9, .0095), (1, 0)]); rad[-2] *= 0.75
    g, P, _ = tube(pts, rad, rad * 0.9, 14, u_rep=1, v_per_m=12)
    parts.append((g, SKIN, keyed(np.zeros(len(g.P)), [(0, {side + 'Hand': 1}), (1, {side + 'Hand': 1})])))
    # left wrist: watch (band + case + glass) on the back of the wrist
    if watch:
        yb = yw + 0.035
        gb, _, _ = tube(np.array([[0, yb + 0.010, 0], [0, yb + 0.011, 0], [0, yb - 0.011, 0], [0, yb - 0.010, 0]]),
                        np.array([.0295, .0325, .0325, .0295]), np.array([.0350, .0380, .0380, .0350]), 32, u_rep=1, v_per_m=10)
        parts.append((gb, WATCH, keyed(np.zeros(len(gb.P)), [(0, {side + 'LowerArm': 1}), (1, {side + 'LowerArm': 1})])))
        gc, _, _ = tube(np.array([[0.0325, 0, 0], [0.0335, 0, 0], [0.0385, 0, 0], [0.0395, 0, 0], [0.0396, 0, 0]]),
                        np.array([.012, .0145, .0145, .012, .0]), np.array([.012, .0145, .0145, .012, .0]), 24, X=(0, 1, 0), Z=(0, 0, 1))
        parts.append((gc.xf(T4((0, yb, 0))), STEEL, keyed(np.zeros(len(gc.P)), [(0, {side + 'LowerArm': 1}), (1, {side + 'LowerArm': 1})])))
        gg, _, _ = tube(np.array([[0.0396, 0, 0], [0.0402, 0, 0], [0.0403, 0, 0]]), np.array([.0118, .009, 0]), np.array([.0118, .009, 0]), 24, X=(0, 1, 0), Z=(0, 0, 1))
        parts.append((gg.xf(T4((0, yb, 0))), GLASS, keyed(np.zeros(len(gg.P)), [(0, {side + 'LowerArm': 1}), (1, {side + 'LowerArm': 1})])))
    # sleeve patch on the outer side
    ang = np.linspace(-0.55, 0.55, 12) + math.pi / 2; yy = np.linspace(-0.035, -0.085, 8) if flag else np.linspace(-0.032, -0.088, 10)
    if flag: ang = np.linspace(-0.62, 0.62, 12) + math.pi / 2
    Rr = interp(yy, [(-0.10, .068), (-0.04, .066), (0.0, .064)]) + 0.0042
    PP = np.stack([np.stack([np.sin(ang)[None] * Rr[:, None], np.broadcast_to(yy[:, None], (len(yy), len(ang))), np.cos(ang)[None] * Rr[:, None] * 1.06], -1)], 0)[0]
    PP = PP[:, ::-1]
    uu = np.linspace(0, 1, len(ang))[None]; vv = np.linspace(0, 1, len(yy))[:, None]
    gp = grid_geo(PP, np.broadcast_to(uu, PP.shape[:2]), np.broadcast_to(vv, PP.shape[:2]), N=None, seam=False)
    parts.append((gp, FLAG if flag else PATCH, keyed(np.zeros(len(gp.P)), [(0, {side + 'UpperArm': 1}), (1, {side + 'UpperArm': 1})])))
    # to world: shoulder joint, A-pose, mirror for the right arm
    sh = np.array([sx * D['sh_x'], D['hips_y'] + D['spine'] + D['chest'] + D['sh_y'], 0.0])
    M = T4(sh) @ R4(qaxis('z', sx * ARM)) @ S4(sx, 1, 1)
    for g, mat, dense in parts:
        B.add(g, mat, dense if sx > 0 else dense, M)

# ------------------------------------------------------------------------------------------------- joggers + legs + shoes
def build_legs(B):
    # pelvis/seat
    ys = np.r_[np.linspace(0.765, 0.80, 8), np.linspace(0.81, 0.99, 22)]; K = len(ys); n = 64
    k = np.sqrt(np.clip(1 - ((0.86 - ys) / 0.075).clip(0) ** 2, 0, 1))
    W = interp(ys, [(0.765, .150), (0.80, .158), (0.86, .168), (0.93, .160), (0.99, .150)]) * np.maximum(k, 0.05)
    F = interp(ys, [(0.765, .030), (0.80, .050), (0.85, .080), (0.90, .094), (0.99, .098)]) * np.maximum(k, 0.05)
    Bk = interp(ys, [(0.765, .090), (0.80, .108), (0.86, .118), (0.93, .108), (0.99, .096)]) * np.maximum(k, 0.05)
    th = ring_angles(n); rz = np.where(np.cos(th)[None] > 0, F[:, None], Bk[:, None])
    g, P, _ = tube(np.c_[np.zeros(K), ys, np.zeros(K)], W, rz, n, p=2.3, u_rep=8, v_per_m=8)
    dense = keyed(g.P[:, 1], [(0.7, {'Hips': 1}), (1.0, {'Hips': 1})])
    for sx, side in ((1, 'Left'), (-1, 'Right')):
        wl = ss(0.03, 0.10, sx * g.P[:, 0]) * ss(0.88, 0.79, g.P[:, 1]) * 0.55
        dense = dense * (1 - wl[:, None]); dense[:, BI[side + 'UpperLeg']] += wl
    B.add(g, JOG, dense)
    # drawstrings hanging below the tee hem
    for sx in (1, -1):
        pts = np.array([[sx * 0.012, 0.93, 0.101], [sx * 0.014, 0.905, 0.102], [sx * 0.016, 0.88, 0.102], [sx * 0.017, 0.86, 0.101], [sx * 0.018, 0.845, 0.100]])
        rr = np.array([0.0028] * 4 + [0.0])
        g, _, _ = tube(pts, rr, rr, 8)
        B.add(g, CORD, keyed(np.zeros(len(g.P)), [(0, {'Hips': 1}), (1, {'Hips': 1})]))
        ga, _, _ = tube(np.array([[sx * 0.0175, 0.856, 0.1005], [sx * 0.018, 0.840, 0.100], [sx * 0.018, 0.839, 0.100]]), np.array([.0034, .0034, 0]), np.array([.0034, .0034, 0]), 8)
        B.add(ga, STEEL, keyed(np.zeros(len(ga.P)), [(0, {'Hips': 1}), (1, {'Hips': 1})]))
    UL, LL = D['ul'], D['ll']
    for sx, side in ((1, 'Left'), (-1, 'Right')):
        hip = np.array([sx * D['hip_x'], D['hips_y'] + D['hip_dy'], 0])
        ys = np.linspace(0.02, -(UL + LL) - 0.015, 64); K = len(ys); n = 40
        rx = interp(ys, [(0.02, .074), (0.0, .084), (-0.12, .084), (-0.28, .074), (-0.43, .064), (-0.55, .066), (-0.66, .058), (-0.71, .056), (-0.74, .054), (-0.76, .045), (-0.78, .042)])
        rz = interp(ys, [(0.02, .080), (0.0, .092), (-0.12, .089), (-0.28, .078), (-0.43, .066), (-0.55, .070), (-0.66, .060), (-0.71, .058), (-0.74, .055), (-0.76, .046), (-0.78, .043)])
        th = ring_angles(n); Y = ys[:, None]; T_ = th[None]
        rng = np.random.default_rng(10 + sx)
        off = 0.0035 * np.sin(TAU * Y / 0.022 + 1.5 * np.sin(2 * T_ + rng.uniform(0, 6))) * gauss(Y, -0.70, 0.03)      # stacked folds above the cuff
        off += 0.003 * np.sin(TAU * (Y / 0.03) + 3 * T_) * gauss(np.cos(T_), -1, 0.35) * gauss(Y, -0.42, 0.03)      # behind the knee
        off += 0.002 * np.sin(4 * T_ + Y * 25 + rng.uniform(0, 6)) * ss(0.0, -0.3, Y) * ss(-0.68, -0.5, Y)
        off -= 0.002 * gauss(np.cos(T_), 1, 0.05) * ss(-0.05, -0.2, Y) * ss(-0.6, -0.45, Y) * -1                     # front crease (raised)
        off *= ss(-0.752, -0.742, Y)
        cuff = ys < -0.755
        C = np.c_[np.zeros(K), ys, interp(ys, [(0, 0), (-0.43, 0.004), (-0.8, -0.004)])]
        g, P, _ = tube(C, rx, rz, n, off=off, u_rep=4, v_per_m=8)
        dense = keyed(g.P[:, 1], [(0.02, {'Hips': .55, side + 'UpperLeg': .45}), (-0.05, {side + 'UpperLeg': 1}), (-0.40, {side + 'UpperLeg': 1}),
                                  (-0.465, {side + 'LowerLeg': 1}), (-0.76, {side + 'LowerLeg': 1}), (-0.80, {side + 'LowerLeg': .4, side + 'Foot': .6})])
        # split: jogger fabric above the cuff, rib below
        Kc = int(np.argmax(cuff)); n1 = n + 1
        for (k0, k1, mat) in ((0, Kc + 1, JOG), (Kc, K, JOG_RIB)):
            sub = Geo.__new__(Geo)
            vi = np.arange(k0 * n1, k1 * n1)
            remap = -np.ones(K * n1, int); remap[vi] = np.arange(len(vi))
            I = g.I[(remap[g.I] >= 0).all(1)]
            sub.P = g.P[vi]; sub.N = g.N[vi]; sub.I = remap[I]; sub.C = None; sub.UV = g.UV[vi] * (np.array([3, 1]) if mat is JOG_RIB else 1)
            B.add(sub, mat, dense[vi], T4(hip))
        # sneaker (foot space: origin at the ankle joint)
        ank = hip + np.array([0, -(UL + LL), 0])
        build_shoe(B, side, sx, ank)

def build_shoe(B, side, sx, ank):
    yb = -ank[1]                                # foot-space y of the ground
    zs = np.r_[np.linspace(-0.080, -0.06, 6), np.linspace(-0.055, 0.17, 30), np.linspace(0.175, 0.200, 8)]; K = len(zs); n = 36
    tip = np.sqrt(np.clip(1 - ((zs - 0.165) / 0.035).clip(0) ** 2, 0, 1)); heel = np.sqrt(np.clip(1 - ((-0.058 - zs) / 0.022).clip(0) ** 2, 0, 1))
    k = np.maximum(tip * heel, 0.04)
    hw = interp(zs, [(-0.08, .036), (-0.04, .040), (0.03, .043), (0.10, .050), (0.15, .047), (0.2, .040)]) * k     # half width
    top = interp(zs, [(-0.08, .092), (-0.05, .100), (-0.02, .095), (0.02, .085), (0.08, .066), (0.13, .052), (0.17, .045), (0.2, .03)])
    bot = 0.024
    hh = (top - bot) / 2 * k; yc = yb + bot + (top - bot) / 2
    th = ring_angles(n, 0.0)                    # 0 = bottom centre (u=0), sides at u=.25/.75
    pp = np.where(np.cos(th)[None] > 0, 4.0, 2.4)                       # flat bottom, rounded top
    st, ct = np.sin(th)[None], -np.cos(th)[None]
    e = 2.0 / pp
    X = hw[:, None] * np.sign(st) * np.abs(st) ** e; Yv = hh[:, None] * np.sign(ct) * np.abs(ct) ** e
    # ankle collar dip: lower the top around the heel opening
    collar = gauss(zs, -0.03, 0.035)[:, None] * gauss(np.cos(th), -1, 0.5)[None] * 0.012
    P = np.stack([X, yc[:, None] + Yv - collar, np.broadcast_to(zs[:, None], X.shape)], -1)
    # open the collar: push the topmost rear ring inward as a padded rim (visual only; leg goes inside)
    u = np.broadcast_to(np.r_[np.arange(n) / n, 1.0][None], (K, n + 1)); v = np.broadcast_to(((zs + 0.08) / 0.28)[:, None], (K, n + 1))
    g = grid_geo(P, u, v)
    M = T4(ank) @ S4(sx, 1, 1)
    B.add(g, SHOE, keyed(np.zeros(len(g.P)), [(0, {side + 'Foot': 1}), (1, {side + 'Foot': 1})]), M)
    # sole: midsole (white) + outsole (grey), slightly wider than the upper
    for (y0, y1, grow, mat) in ((0.007, 0.030, 0.0055, SOLE), (0.0, 0.008, 0.0060, OUTSOLE)):
        ys = np.linspace(y0, y1, 5); n = 48
        outline_z = np.linspace(-0.084, 0.204, 60)
        tipk = np.sqrt(np.clip(1 - ((outline_z - 0.165) / 0.039).clip(0) ** 2, 0, 1)); heelk = np.sqrt(np.clip(1 - ((-0.058 - outline_z) / 0.026).clip(0) ** 2, 0, 1))
        w = (interp(outline_z, [(-0.084, .037), (-0.04, .041), (0.03, .043), (0.10, .051), (0.15, .048), (0.2, .041)]) + grow) * np.maximum(tipk * heelk, 0.0)
        # build as a stack of horizontal outlines -> closed slab via tube along y with a custom outline
        ring = np.r_[np.c_[w, outline_z], np.c_[-w[::-1], outline_z[::-1]]][:-1]
        # resample ring to n points
        seg = np.r_[0, np.cumsum(np.linalg.norm(np.diff(np.r_[ring, ring[:1]], axis=0), axis=1))]
        tt = np.linspace(0, seg[-1], n, endpoint=False)
        rx_ = np.interp(tt, seg, np.r_[ring[:, 0], ring[0, 0]]); rz_ = np.interp(tt, seg, np.r_[ring[:, 1], ring[0, 1]])
        cz = rz_.mean()
        Ps = []
        rows = [(y0, 0.0), (y0, 0.94), (y0 + 0.001, 1.0)] + [(y, 1.0) for y in ys[1:-1]] + [(y1 - 0.001, 1.0), (y1, 0.94), (y1, 0.0)]
        for y, sc in rows:
            Ps.append(np.c_[rx_ * sc, np.full(n, yb + y), cz + (rz_ - cz) * sc])
        Ps = np.array(Ps)
        g = grid_geo(Ps, np.broadcast_to(np.r_[np.arange(n) / n, 1][None] * 8, (len(rows), n + 1)), np.broadcast_to(np.linspace(0, 0.3, len(rows))[:, None], (len(rows), n + 1)))
        B.add(g, mat, keyed(np.zeros(len(g.P)), [(0, {side + 'Foot': 1}), (1, {side + 'Foot': 1})]), M)
    # tongue + laces across the instep
    for i, z in enumerate(np.linspace(0.025, 0.105, 5)):
        ytop = yb + np.interp(z, [-0.08, -0.05, -0.02, 0.02, 0.08, 0.13], [.092, .100, .095, .085, .066, .052]) + 0.002
        wl = 0.022 - 0.002 * i
        pts = np.array([[-wl, ytop - 0.004, z], [-wl * 0.5, ytop + 0.001, z + 0.002], [0, ytop + 0.002, z + 0.002], [wl * 0.5, ytop + 0.001, z + 0.002], [wl, ytop - 0.004, z]])
        rr = np.full(5, 0.0026)
        g, _, _ = tube(pts, rr, rr, 8)
        B.add(g, LACE, keyed(np.zeros(len(g.P)), [(0, {side + 'Foot': 1}), (1, {side + 'Foot': 1})]), M)

# =====================================================================================================  materials (set in build)
TEE = TEE_RIB = JOG = JOG_RIB = SKIN = FACE = HAIR = EYE = NAIL = SHOE = SOLE = OUTSOLE = LACE = CORD = WATCH = STEEL = GLASS = FLAG = PATCH = None

def make_materials(d):
    global TEE, TEE_RIB, JOG, JOG_RIB, SKIN, FACE, HAIR, EYE, NAIL, SHOE, SOLE, OUTSOLE, LACE, CORD, WATCH, STEEL, GLASS, FLAG, PATCH
    os.makedirs(d, exist_ok=True)
    W = (1, 1, 1)
    t, tn = tex_knit(d, 'tee', (30, 52, 98)); TEE = Mat('TeeNavy', W, 0.0, 0.92, tex=t, nrm=tn, tile=1.0)
    t, tn = tex_knit(d, 'tee_rib', (24, 42, 82), rib=True); TEE_RIB = Mat('TeeRib', W, 0.0, 0.92, tex=t, nrm=tn, tile=1.0)
    t, tn = tex_fleece(d, 'jog', (96, 98, 106)); JOG = Mat('JoggerFleece', W, 0.0, 0.96, tex=t, nrm=tn, tile=1.0)
    t, tn = tex_fleece(d, 'jog_rib', (84, 86, 94), rib=True); JOG_RIB = Mat('JoggerRib', W, 0.0, 0.95, tex=t, nrm=tn, tile=1.0)
    t, tn = tex_skin(d); SKIN = Mat('Skin', W, 0.0, 0.55, tex=t, nrm=tn, tile=1.0)
    t, tn = tex_head(d); FACE = Mat('Face', W, 0.0, 0.52, tex=t, nrm=tn, tile=1.0)
    t, tn = tex_hair(d); HAIR = Mat('Hair', W, 0.0, 0.62, tex=t, nrm=tn, tile=1.0)
    EYE = Mat('Eye', W, 0.0, 0.08, tex=tex_eye(d), tile=1.0)
    NAIL = Mat('Nail', (0.80, 0.62, 0.55), 0.0, 0.35)
    t, tn = tex_shoe(d); SHOE = Mat('SneakerUpper', W, 0.0, 0.7, tex=t, nrm=tn, tile=1.0)
    SOLE = Mat('SneakerMidsole', (0.93, 0.93, 0.91), 0.0, 0.6)
    OUTSOLE = Mat('SneakerOutsole', (0.32, 0.33, 0.35), 0.0, 0.85)
    LACE = Mat('Laces', (0.95, 0.95, 0.94), 0.0, 0.8)
    CORD = Mat('Drawstring', (0.90, 0.90, 0.88), 0.0, 0.8)
    WATCH = Mat('WatchBand', (0.07, 0.07, 0.08), 0.0, 0.5)
    STEEL = Mat('Steel', (0.75, 0.76, 0.78), 1.0, 0.25)
    GLASS = Mat('WatchGlass', (0.02, 0.03, 0.04), 0.3, 0.05)
    FLAG = Mat('FlagPatch', W, 0.0, 0.8, tex=tex_flag(d), tile=1.0)
    PATCH = Mat('MissionPatch', W, 0.0, 0.8, tex=tex_patch(d), tile=1.0)

# =====================================================================================================  assemble
def build(texdir):
    make_materials(texdir)
    A = Asset('CrewCasual')
    arm, N = crew.skeleton(A, D['hips_y'], D['spine'], D['chest'], D['neck'], D['head'], D['sh_x'], D['sh_y'],
                           D['hip_x'], D['hip_dy'], D['ua'], D['la'], D['ul'], D['ll'])
    for side, sx in (('Left', 1), ('Right', -1)): N[side + 'UpperArm'].r = qaxis('z', sx * ARM)   # A-pose bind
    B = Body()
    build_torso(B); B.head = True; build_head(B); B.head = False
    build_arm(B, 'Left', 1, flag=True)
    # right arm: build as left, then remap the bone weights Left->Right
    n0 = len(B.parts); build_arm(B, 'Left', -1, flag=False)
    for i in range(n0, len(B.parts)):
        g, mat, h = B.parts[i]; j, w = g.JW
        jj = j.copy()
        for b in BONES:
            if b.startswith('Left'): jj[j == BI[b]] = BI['Right' + b[4:]]
        g.JW = (jj, w)
    build_legs(B)
    sk = Skin([N[b] for b in BONES], arm)
    m = MeshB('Crew_Body'); B.into(m)
    mesh = Node('Crew_Mesh', mesh=m); A.add(mesh); mesh.skin = sk
    mh = MeshB('Crew_Head'); B.into(mh, head=True)
    hn = Node('Crew_Head', mesh=mh); A.add(hn); hn.skin = sk
    AS.BIND_HIPS_Y = D['hips_y']
    N['Chest'].add(Node('Socket_Hold', t=D['hold']))
    N['Chest'].add(Node('Socket_Back', t=(0, 0.0, -0.20)))
    N['RightHand'].add(Node('Socket_HandR', t=(0, -.06, 0))); N['LeftHand'].add(Node('Socket_HandL', t=(0, -.06, 0)))
    import suit_clips
    crew.add_clips(A, N, casual=True, more=PX.Lib(D).clips() + suit_clips.clips(D))
    return A

if __name__ == '__main__':
    out = sys.argv[1]; texdir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(out)), 'crew_hq_tex')
    A = build(texdir)
    lo, hi = A.bounds(); print('tris', A.tri_count(), 'bounds', lo.round(3), hi.round(3), 'clips', [a.name for a in A.anims])
    print('KB', A.save(out) // 1024)
