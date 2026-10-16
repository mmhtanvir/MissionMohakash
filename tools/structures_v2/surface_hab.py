"""
surface_hab.py - SurfaceHabitat.glb: the HAB module in the style of NASA's Artemis Base Camp surface habitat concepts
(Milestone 06). A horizontal pressurised cylinder wrapped in white quilted MLI / beta-cloth, elliptical end bulkheads,
four landing legs with foot pads, a berthing adapter on +X where the SUI airlock tunnel (Connector.glb at x=5.6) docks,
two round windows on +X at z=-3.5 (the interior window's portal view looks out from there), roof radiators, a vertical
solar array mast (the Sun stays near the horizon at the lunar south pole), an S-band dish, handrails, and two suitports
with a porch and ladder on the -Z bulkhead.

    python3 surface_hab.py <out_models_dir>      (writes SurfaceHabitat.glb + prev/SurfaceHabitat.png)
"""
import sys, os, math, numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from structures import *
from textures import fnoise, blur, normal_from_h, save_n, save_a
import preview
from glbkit import R as Rot, T as Tr

R, L, DOME, CY = 2.2, 8.0, 1.05, 2.85         # hull radius, cylinder length (z), bulkhead depth, axis height

def make_mli():
    """white quilted beta-cloth MLI: square quilting every 0.25 m (2 m tile), puffed cells, stitch dashes, soft wrinkles"""
    n = 512; cells = 8; cs = n // cells
    y, x = np.mgrid[0:n, 0:n]
    u = (x % cs) / cs; v = (y % cs) / cs
    puff = np.sin(np.pi * u) ** 0.6 * np.sin(np.pi * v) ** 0.6
    wr = blur(fnoise(n, 2.4, 91), 1.0); wr = (wr - wr.mean()) / (wr.std() + 1e-9)
    edge = (np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v)) < 0.02)
    stitch = edge & (((x + y) // 6) % 2 == 0)
    h = puff * 1.0 + 0.10 * wr - 0.6 * stitch
    shade = 0.84 + 0.12 * puff + 0.025 * wr - 0.18 * stitch
    base = np.array([0.95, 0.94, 0.90])
    a = np.clip(base[None, None] * shade[..., None], 0, 1)
    save_a(TX + 'mli_a.jpg', a); save_n(TX + 'mli_n.png', normal_from_h(h, 2.2))

def build_surface_hab():
    make_mli()
    MLI = Mat('MLIBetaCloth', (1, 1, 1), 0.0, 0.85, tex=TX + 'mli_a.jpg', nrm=TX + 'mli_n.png', tile=2.0, nstr=1.0)
    WARM = Mat('WindowWarm', (1.0, 0.85, 0.6), 0.0, 0.1, emissive=(1.0, 0.80, 0.55), estr=1.6)
    FLAGG = Mat('FlagGreen', (0.0, 0.42, 0.31), 0.0, 0.6); FLAGR = Mat('FlagRed', (0.94, 0.16, 0.25), 0.0, 0.6)
    m = mk('HAB')
    # pressure hull: cylinder + elliptical bulkheads, revolved around Z
    prof = []
    for a in np.linspace(-90, 0, 9): prof.append((R * math.cos(math.radians(a)), -L / 2 + DOME * math.sin(math.radians(a))))
    for a in np.linspace(0, 90, 9)[1:]: prof.append((R * math.cos(math.radians(a)), L / 2 + DOME * math.sin(math.radians(a))))
    prof[0] = (0.0, prof[0][1]); prof[-1] = (0.0, prof[-1][1])
    m.lathe(prof, (0, CY, 0), MLI, seg=48, axis='z', smooth_deg=50)
    # circumferential structural rings + berthing collars
    for z in np.linspace(-L / 2, L / 2, 5): m.torus(R + 0.03, 0.055, (0, CY, z), STEEL, axis='z', seg=56, tube=6)
    # longitudinal handrails (gold/yellow, ISS style) on standoffs
    for ang in (35, 145, 90):
        a = math.radians(ang); x, y = (R + 0.16) * math.cos(a), CY + (R + 0.16) * math.sin(a)
        m.between((x, y, -L / 2 + 0.4), (x, y, L / 2 - 0.4), 0.025, RAILY, seg=8)
        for z in np.linspace(-L / 2 + 0.6, L / 2 - 0.6, 6):
            m.between(((R - 0.02) * math.cos(a), CY + (R - 0.02) * math.sin(a), z), (x, y, z), 0.018, RAILY, seg=6)
    # berthing adapter on +X for the SUI airlock tunnel (tunnel end at x = 2.92, centre y ~1.25)
    m.cyl(1.05, 1.9, (2.0, 1.25, 0), MLI, axis='x', seg=36)
    m.torus(1.08, 0.07, (2.92, 1.25, 0), STEEL, axis='x', seg=40, tube=6)
    m.torus(1.08, 0.07, (1.35, 1.25, 0), STEEL, axis='x', seg=40, tube=6)
    # windows on +X at z = -3.5 (portal view) and two on -X, each with a dark frame and warm cabin glow
    for (sx, z, y) in ((1, -3.5, 2.55), (1, -2.6, 2.55), (-1, -1.5, 2.9), (-1, 1.5, 2.9)):
        dy = y - CY; xs = sx * math.sqrt(R * R - dy * dy); ang = math.degrees(math.atan2(dy, abs(xs)))
        rot = (0, 0, sx * ang)
        m.cyl(0.30, 0.10, (xs + sx * 0.02, y, z), DARK, axis='x', seg=28, rot=rot)
        m.cyl(0.22, 0.12, (xs + sx * 0.03, y, z), WARM, axis='x', seg=28, rot=rot)
    # landing legs: main strut + side brace to a foot pad
    for sx in (-1, 1):
        for sz in (-1, 1):
            hip = np.array([sx * 1.75, CY - 1.35, sz * 3.0]); pad = np.array([sx * 2.85, 0.12, sz * 3.9])
            m.between(hip, pad, 0.10, STEEL, seg=12)
            m.between(np.array([sx * 0.7, CY - 2.05, sz * 2.7]), pad, 0.06, STEEL, seg=10)
            m.between(np.array([sx * 1.9, CY - 1.1, sz * 4.1]), pad, 0.06, STEEL, seg=10)
            m.cyl(0.45, 0.10, tuple(pad - np.array([0, 0.07, 0])), DARK, seg=24)
            m.cyl(0.32, 0.12, tuple(pad), STEEL, seg=20)
            m.sph(0.14, tuple(hip), DARK, seg=10, rings=6)
    # roof: two radiator panels on short masts
    for z in (-2.2, 2.2):
        m.cyl(0.08, 0.6, (0.9, CY + R + 0.25, z), STEEL, seg=10)
        m.box((2.6, 0.06, 1.6), (0.9, CY + R + 0.6, z), WHITEP, r=0.01)
        for k in range(-5, 6): m.box((2.5, 0.075, 0.025), (0.9, CY + R + 0.6, z + k * 0.14), STEEL)
    # vertical solar array mast (VSAT style) with two wings
    mx, mz, top = -1.0, 0.0, CY + R + 6.2
    m.cyl(0.11, top - (CY + R), (mx, (CY + R + top) / 2, mz), STEEL, seg=12)
    for k in np.linspace(CY + R + 0.6, top - 0.4, 9): m.torus(0.14, 0.025, (mx, k, mz), DARK, seg=16, tube=4)
    for sz in (-1, 1):
        m.box((0.06, 3.6, 2.2), (mx, top - 2.0, mz + sz * 1.25), SOLARC, r=0.01)
        m.box((0.08, 3.7, 0.06), (mx, top - 2.0, mz + sz * 2.37), STEEL)
    m.sph(0.12, (mx, top + 0.1, mz), LED_R, seg=8, rings=6)
    # S-band dish on the roof
    m.cyl(0.05, 0.7, (0.6, CY + R + 0.35, 3.6), STEEL, seg=8)
    m.add(dish_geo(0.55, 0.18), WHITEP, M=Tr(0.6, CY + R + 0.75, 3.6) @ Rot('x', -35) @ Rot('y', 30))
    # suitports on the -Z bulkhead: two rear-entry suits docked, porch + ladder
    zb = -L / 2 - DOME * 0.62
    for sx in (-1, 1):
        cx = sx * 0.72
        m.torus(0.42, 0.06, (cx, 1.95, zb - 0.05), STEEL, axis='z', seg=36, tube=6)
        m.cyl(0.38, 0.06, (cx, 1.95, zb - 0.06), DARK, axis='z', seg=32)
        m.box((0.55, 0.65, 0.28), (cx, 1.95, zb - 0.25), TANKW, r=0.06)            # docked suit's life-support pack
        m.box((0.40, 0.08, 0.02), (cx, 2.2, zb - 0.40), LED_G)
    m.box((2.6, 0.08, 1.2), (0, 1.15, zb - 0.75), STEEL, r=0.02)                    # porch deck
    for sx in (-1, 1): m.between((sx * 1.25, 1.15, zb - 1.3), (sx * 1.25, 2.1, zb - 1.3), 0.03, RAILY, seg=6)
    m.between((-1.25, 2.1, zb - 1.3), (1.25, 2.1, zb - 1.3), 0.03, RAILY, seg=6)
    for sx in (-0.35, 0.35): m.between((sx, 0.0, zb - 1.75), (sx, 1.15, zb - 1.35), 0.025, RAILY, seg=6)
    for k in range(1, 5):
        t = k / 5; m.between((-0.35, 1.15 * t, zb - 1.75 + 0.4 * t), (0.35, 1.15 * t, zb - 1.75 + 0.4 * t), 0.018, YELLOW, seg=6)
    # running lights + flag decal on the +X side
    m.sph(0.08, (0, CY + R + 0.05, L / 2 + 0.6), LED_R, seg=8, rings=5)
    m.sph(0.08, (R + 0.05, CY, L / 2 - 0.3), LED_G, seg=8, rings=5)
    fy = CY + 0.95; fx = math.sqrt(R * R - 0.95 ** 2) + 0.012; fa = math.degrees(math.atan2(0.95, fx))
    m.box((0.01, 0.6, 1.0), (fx, fy, 1.6), FLAGG, rot=(0, 0, fa))
    m.cyl(0.18, 0.012, (fx + 0.006, fy, 1.5), FLAGR, axis='x', seg=24, rot=(0, 0, fa))
    return asset('HAB_SurfaceHabitat', m, 'HAB')

if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True); os.makedirs('prev', exist_ok=True)
    A = build_surface_hab(); size = A.save(os.path.join(out, 'SurfaceHabitat.glb'))
    lo, hi = A.bounds(); print('SurfaceHabitat', A.tri_count(), 'tris', size // 1024, 'KB', lo.round(2), hi.round(2))
    preview.sheet(A, 'prev/SurfaceHabitat.png', title='SurfaceHabitat')
