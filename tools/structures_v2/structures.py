import math, numpy as np
from glbkit import *
from glbkit import _icosphere, _n

import os
TX = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tex') + '/'
# ---------------- materials (sRGB tints; textures supply detail)
HULL   = Mat('HullPanels',  (1, 1, 1), 0.15, 0.55, tex=TX + 'hull_a.jpg', nrm=TX + 'hull_n.png', tile=2.0, nstr=1.0)
HULL2  = Mat('HullPanelsWarm', (0.95, 0.93, 0.88), 0.15, 0.6, tex=TX + 'hull_a.jpg', nrm=TX + 'hull_n.png', tile=2.0)
DARK   = Mat('DarkPlate',   (1, 1, 1), 0.65, 0.5, tex=TX + 'dark_a.jpg', nrm=TX + 'dark_n.png', tile=2.0)
GOLDA  = Mat('GoldFoilA',   (1.0, 0.95, 0.85), 0.95, 0.32, tex=TX + 'gold_a.jpg', nrm=TX + 'gold_n.png', tile=2.0, nstr=0.9)
GOLDB  = Mat('GoldFoilB',   (0.92, 0.80, 0.62), 0.95, 0.34, tex=TX + 'gold_a.jpg', nrm=TX + 'gold_n.png', tile=2.0, nstr=0.9)
GOLDC  = Mat('GoldFoilC',   (1.0, 0.86, 0.66), 0.95, 0.30, tex=TX + 'gold_a.jpg', nrm=TX + 'gold_n.png', tile=2.0, nstr=0.9)
SOLARC = Mat('SolarCells',  (1, 1, 1), 0.7, 0.25, tex=TX + 'solar_a.jpg', nrm=TX + 'solar_n.png', tile=2.0)
NAVY   = Mat('NavyPaint',   (0.10, 0.20, 0.55), 0.1, 0.5)
STEEL  = Mat('Steel',       (0.62, 0.65, 0.70), 0.9, 0.35)
BLACK  = Mat('BlackRubber', (0.04, 0.04, 0.05), 0.0, 0.85, double=True)
YELLOW = Mat('HazardYellow', (0.95, 0.74, 0.05), 0.0, 0.5, double=True)
RAILY  = Mat('RailYellow',  (0.90, 0.62, 0.05), 0.2, 0.45)
WHITEP = Mat('WhitePaint',  (0.85, 0.86, 0.87), 0.1, 0.5)
GLASSE = Mat('WindowGlow',  (0.35, 0.65, 1.0), 0.0, 0.2, emissive=(0.25, 0.60, 1.0), estr=2.5)
LED_B  = Mat('NeonBlue',    (0.2, 0.55, 1.0), 0.0, 0.4, emissive=(0.2, 0.55, 1.0), estr=4.0)
LED_G  = Mat('LEDGreen',    (0.1, 1.0, 0.3), 0.0, 0.4, emissive=(0.1, 1.0, 0.3), estr=3.0)
LED_R  = Mat('LEDRed',      (1.0, 0.1, 0.05), 0.0, 0.4, emissive=(1.0, 0.1, 0.05), estr=4.0)
LED_A  = Mat('LEDAmber',    (1.0, 0.62, 0.05), 0.0, 0.4, emissive=(1.0, 0.62, 0.05), estr=3.0)
LAMP   = Mat('LampWarm',    (1.0, 0.95, 0.80), 0.0, 0.4, emissive=(1.0, 0.92, 0.75), estr=4.0)
DUSTM  = Mat('Dust',        (1, 1, 1), 0.0, 0.95, tex=TX + 'dust_a.jpg', nrm=TX + 'dust_n.png', tile=2.0)
TANKW  = Mat('TankWhite',   (0.88, 0.90, 0.92), 0.5, 0.35)
O2B    = Mat('TankBand',    (0.10, 0.40, 0.90), 0.3, 0.4)

def mk(name): return MeshB(name)
def asset(name, mesh, root='Root'):
    A = Asset(name); r = A.add(Node(root)); r.add(Node(name + '_Mesh', mesh=mesh)); return A

def tri_panel(a, b, c, out=0.05, center=(0, 0, 0)):
    a, b, c = map(np.asarray, (a, b, c))
    n = _n(np.cross(b - a, c - a))
    if n @ ((a + b + c) / 3 - np.asarray(center)) < 0: n = -n
    top = [p + n * out for p in (a, b, c)]
    P = []; N = []; I = []
    P += top; N += [n] * 3; I.append([0, 1, 2])
    for i in range(3):
        j = (i + 1) % 3
        e = _n(top[j] - top[i]); sn = _n(np.cross(e, n))
        if sn @ ((a + b + c) / 3 - (np.asarray([a, b, c][i]) + np.asarray([a, b, c][j])) / 2) > 0: sn = -sn
        k = len(P); P += [[a, b, c][i], [a, b, c][j], top[j], top[i]]; N += [sn] * 4
        I += [[k, k + 1, k + 2], [k, k + 2, k + 3]]
    return Geo(P, N, I)

def dish_geo(r=1.0, depth=0.3, seg=28, rings=6, thick=0.03):
    """parabolic dish, opening +Y, origin at vertex."""
    prof = [(0.0, 0.0)] + [(r * k / rings, depth * (k / rings) ** 2) for k in range(1, rings + 1)]
    prof += [(r, depth + thick)] + [(r * k / rings, depth * (k / rings) ** 2 - thick) for k in range(rings - 1, -1, -1)]
    pr = [(x, y) for x, y in prof]
    return lathe([(0.0, -thick)] + [(x, y - thick) for x, y in prof[1:rings + 1]] + [(r, depth), (r, depth - thick)], seg, 60)

def lattice(m, base, top, rb, rt, levels, leg_r, mat, nleg=3, rot0=0.0, ring=True):
    """lattice mast: nleg legs, tapering, zig-zag bracing"""
    def pt(k, t):
        r = rb + (rt - rb) * t; ang = math.radians(rot0 + 360.0 * k / nleg)
        return np.array([r * math.cos(ang), base + (top - base) * t, r * math.sin(ang)])
    for k in range(nleg):
        m.between(pt(k, 0), pt(k, 1), leg_r, mat, r2=leg_r * 0.8, seg=8)
    for i in range(levels):
        t0 = i / levels; t1 = (i + 1) / levels
        for k in range(nleg):
            k2 = (k + 1) % nleg
            if ring and i > 0: m.between(pt(k, t0), pt(k2, t0), leg_r * 0.6, mat, seg=6)
            if i % 2 == 0: m.between(pt(k, t0), pt(k2, t1), leg_r * 0.5, mat, seg=6)
            else: m.between(pt(k2, t0), pt(k, t1), leg_r * 0.5, mat, seg=6)

def radial(n, r, y0=0.0, off=0.0):
    return [(r * math.cos(math.radians(off + 360 * i / n)), y0, r * math.sin(math.radians(off + 360 * i / n))) for i in range(n)]

# =====================================================================  HAB  (GeodesicDome.glb)
def build_dome():
    m = mk('HAB'); R = 4.0; H = 5.0
    V, F = _icosphere(2)
    top = np.array([0, 1, (1 + 5 ** 0.5) / 2]); top = top / np.linalg.norm(top)
    Rm = rot_between(top, [0, 1, 0])[:3, :3]
    V = V @ Rm.T
    Vs = V * np.array([R, H, R]); Vs[:, 1] = np.maximum(Vs[:, 1], 0)
    cen = np.array([0, H * 0.35, 0])
    rng = np.random.default_rng(8)
    glow = set()
    low = [i for i, (a, b, c) in enumerate(F) if V[[a, b, c]][:, 1].mean() > 0.04 and V[[a, b, c]][:, 1].mean() < 0.33]
    for i in low[::max(1, len(low) // 9)][:9]: glow.add(i)
    golds = [GOLDA, GOLDB, GOLDC]
    # dark core dome (visible through gaps) + frame
    m.add(lathe([(0, 0.05)] + [(R * 0.985 * math.cos(math.radians(a)), (H * 0.985) * math.sin(math.radians(a))) for a in np.linspace(0, 90, 14)][:-1] + [(0, H * 0.985)], 40, 89), DARK)
    edges = set()
    for fi, (a, b, c) in enumerate(F):
        pa, pb, pc = Vs[a], Vs[b], Vs[c]
        if (pa[1] + pb[1] + pc[1]) / 3 < 0.25: continue
        ctr = (pa + pb + pc) / 3
        k = 0.88
        qa, qb, qc = ctr + (pa - ctr) * k, ctr + (pb - ctr) * k, ctr + (pc - ctr) * k
        if fi in glow: m.add(tri_panel(qa, qb, qc, 0.03, cen), GLASSE)
        else: m.add(tri_panel(qa, qb, qc, 0.07, cen), golds[int(rng.integers(0, 3))])
        for e in ((a, b), (b, c), (c, a)): edges.add((min(e), max(e)))
    for (a, b) in edges:
        pa, pb = Vs[a], Vs[b]
        if pa[1] < 0.05 and pb[1] < 0.05: continue
        m.between(pa * 1.005, pb * 1.005, 0.055, HULL, seg=6)
    for v in {i for e in edges for i in e}:
        if Vs[v][1] > 0.05: m.sph(0.12, Vs[v] * 1.005, STEEL, seg=8, rings=5)
    # foundation ring + skirt
    m.lathe([(0, 0), (R + 0.45, 0), (R + 0.45, 0.62), (R + 0.30, 0.78), (0, 0.78)], (0, 0, 0), HULL, seg=40, smooth_deg=30)
    m.torus(R + 0.46, 0.05, (0, 0.3, 0), LED_B, seg=48, tube=6)
    for k, (x, y, z) in enumerate(radial(16, R + 0.45)):
        ang = math.degrees(math.atan2(-z, x))
        m.box((0.55, 0.9, 0.45), (x * 1.0, 0.45, z * 1.0), DARK, r=0.06, rot=(0, ang, 0))
    # roof: beacon mast
    m.cyl(0.05, 0.9, (0, H + 0.4, 0), STEEL, seg=8); m.sph(0.1, (0, H + 0.9, 0), LED_R, seg=8, rings=6)
    return asset('HAB_GeodesicDome', m, 'HAB')

# =====================================================================  SUI airlock  (Connector.glb)
def build_connector():
    """SUI airlock: a walk-in chamber. Outer doorway at local x=-0.68 (the hinged outer hatch is a runtime object,
    HingedHatch.cs), inner doorway at local x=2.45 toward the HAB (runtime SlidingDoor). Interior floor y=1.22,
    ceiling y=3.45, half-width 0.95. Effects (vents, beacon, pressure panel) are added at runtime by AirlockChamber.cs."""
    m = mk('SUI')
    x0, x1, xi = -0.68, 2.68, 2.45         # outer end, HAB end, inner door plane
    yf, yc, hw, t = 1.22, 3.45, 0.95, 0.12  # floor, ceiling, interior half-width, wall thickness
    dz, dy1 = 0.55, 3.10                   # doorway half-width / top
    L = x1 - x0; cx = (x0 + x1) / 2
    # outer shell: side walls, roof, floor slab (each a slab so the inside faces are real walls)
    for s_ in (-1, 1): m.box((L, yc + t - (yf - 0.12), t), (cx, (yc + t + yf - 0.12) / 2, s_ * (hw + t / 2)), HULL, r=0.04)
    m.box((L, t, 2 * hw + 2 * t), (cx, yc + t / 2, 0), HULL, r=0.05)
    m.box((L, 0.12, 2 * hw + 2 * t), (cx, yf - 0.06, 0), DARK)
    # end walls with doorways
    for xe, name in ((x0, 'outer'), (xi, 'inner')):
        for s_ in (-1, 1): m.box((t, yc - yf, hw - dz), (xe, (yf + yc) / 2, s_ * (dz + (hw - dz) / 2)), HULL, r=0.02)
        m.box((t, yc - dy1, 2 * dz), (xe, (dy1 + yc) / 2, 0), HULL, r=0.02)
        m.box((t + 0.06, 0.08, 2 * dz + 0.16), (xe, dy1 + 0.04, 0), DARK)
        for s_ in (-1, 1): m.box((t + 0.06, dy1 - yf, 0.08), (xe, (yf + dy1) / 2, s_ * (dz + 0.04)), DARK)
    m.box((x1 - xi, yc - yf, 2 * hw), ((x1 + xi) / 2, (yf + yc) / 2, 0), DARK)          # behind the inner door: berthing tunnel (dark)
    # interior: wall panels, floor grille, ceiling light strips, handrails, vent grilles, pipes
    xa, xb = x0 + t, xi - t / 2
    for s_ in (-1, 1):
        m.box((xb - xa, yc - yf - 0.3, 0.02), ((xa + xb) / 2, (yf + yc) / 2, s_ * (hw - 0.01)), WHITEP)
        m.box((xb - xa, 0.04, 0.04), ((xa + xb) / 2, yf + 0.95, s_ * (hw - 0.06)), RAILY)              # handrail
        for xh in np.linspace(xa + 0.3, xb - 0.3, 3): m.box((0.03, 0.03, 0.06), (xh, yf + 0.95, s_ * (hw - 0.03)), STEEL)
        m.box((1.1, 0.5, 0.03), ((xa + xb) / 2, yf + 1.15, s_ * (hw - 0.03)), STEEL)                 # vent grille
        for k in range(5): m.box((1.05, 0.02, 0.035), ((xa + xb) / 2, yf + 0.95 + k * 0.1, s_ * (hw - 0.04)), DARK)
        m.cyl(0.04, xb - xa, ((xa + xb) / 2, yc - 0.12, s_ * (hw - 0.1)), STEEL, axis='x', seg=10)   # piping along the ceiling edge
    m.box((xb - xa - 0.1, 0.02, 2 * hw - 0.3), ((xa + xb) / 2, yf + 0.01, 0), STEEL)                  # floor grille
    for k in range(9): m.box((0.03, 0.025, 2 * hw - 0.35), (xa + 0.2 + k * (xb - xa - 0.4) / 8, yf + 0.03, 0), DARK)
    for xl in (xa + 0.55, xb - 0.55): m.box((0.7, 0.03, 0.3), (xl, yc - 0.02, 0), LAMP)
    m.box((0.6, 0.04, 0.6), ((xa + xb) / 2, yc - 0.03, 0), STEEL)                                      # air shower nozzle plate
    # outside: frame stripe, ribs, portholes, LED strips, step plate, feet, roof gear
    m.box((0.18, 0.1, 1.5), (x0 - 0.06, dy1 + 0.17, 0), YELLOW)
    for x in (0.55, 1.45, 2.25):                                   # structural hoops OUTSIDE the shell (the chamber stays clear)
        ow = hw + t + 0.04
        for s_ in (-1, 1): m.box((0.10, yc - yf + 0.5, 0.06), (x, (yf + yc) / 2, s_ * ow), DARK, r=0.02)
        m.box((0.10, 0.06, 2 * ow + 0.06), (x, yc + t + 0.03, 0), DARK, r=0.02)
    for s_ in (-1, 1):
        m.box((L - 0.4, 0.06, 0.06), (cx, 1.62, s_ * (hw + t + 0.01)), LED_B)
        m.cyl(0.25, 0.08, (1.0, yf + 1.3, s_ * (hw + t + 0.01)), DARK, axis='z', seg=24)
        m.cyl(0.19, 0.09, (1.0, yf + 1.3, s_ * (hw + t + 0.02)), GLASSE, axis='z', seg=24)
    m.box((L - 0.1, 0.12, 2 * hw + 2 * t - 0.1), (cx, 1.10, 0), DARK)
    # the chamber is raised 0.57 m (ground = local y 0.65 once placed): stair with 3 rises up to the doorway + handrails
    g = 0.65; rise = (yf - g) / 3
    for k in range(2):
        top = yf - rise * (k + 1); xa_, xb_ = x0 - 0.02 - 0.32 * k, x0 - 0.02 - 0.32 * (k + 1)
        m.box((xa_ - xb_, top - (g - 0.3), 1.9), ((xa_ + xb_) / 2, (top + g - 0.3) / 2, 0), DARK)
        m.box((xa_ - xb_, 0.03, 1.9), ((xa_ + xb_) / 2, top + 0.015, 0), STEEL)
        m.box((0.04, 0.035, 1.9), (xb_ + 0.02, top + 0.02, 0), YELLOW)                          # nosing stripe
    xs0, xs1 = x0 - 0.02, x0 - 0.02 - 0.64 - 0.3
    for s_ in (-1, 1):
        zr = s_ * 0.98                                             # rails outside the hatch's swing (it opens over z 0.63..0.88)
        for xp, yp in ((xs0 - 0.05, yf), (xs1 + 0.1, g)):
            m.between((xp, yp - 0.05, zr), (xp, yp + 0.95, zr), 0.025, RAILY, seg=8)
        m.between((xs0 - 0.05, yf + 0.95, zr), (xs1 + 0.1, g + 0.95, zr), 0.025, RAILY, seg=8)
        m.between((xs0 - 0.05, yf + 0.5, zr), (xs1 + 0.1, g + 0.5, zr), 0.018, RAILY, seg=8)
    for x in (-0.3, 1.1, 2.3):                                     # support legs from the floor slab down to the regolith
        for s_ in (-1, 1):
            m.box((0.16, 1.10 - g + 0.25, 0.16), (x, (1.10 + g - 0.25) / 2, s_ * 0.8), STEEL, r=0.03)
            m.box((0.4, 0.06, 0.4), (x, g + 0.02, s_ * 0.8), DARK)
    m.box((0.7, 0.3, 0.5), (1.0, yc + t + 0.15, 0.3), DARK, r=0.04)
    m.cyl(0.22, 0.28, (1.9, yc + t + 0.14, -0.35), STEEL, seg=16)
    m.sph(0.07, (-0.55, yc + t + 0.12, -0.7), LED_A, seg=8, rings=5)
    return asset('SUI_Connector', m, 'SUI')

# =====================================================================  OGS  (HouseLong.glb)
def build_ogs():
    m = mk('OGS'); zc = -2.085; Lz = 7.7
    m.box((3.96, 0.35, Lz), (0, 0.175, zc), DARK, r=0.04)
    m.box((3.76, 2.9, Lz - 0.2), (0, 1.80, zc), HULL, r=0.35)
    m.box((3.80, 0.12, Lz - 0.1), (0, 3.28, zc), HULL2, r=0.04)
    m.box((2.2, 0.48, 5.2), (0, 3.52, zc), HULL, r=0.1)
    for z in (zc + 1.4, zc - 1.4):
        m.cyl(0.74, 0.14, (0, 3.83, z), DARK, seg=28)
        m.lathe([(0, 0), (0.62, 0), (0.62, 0.05), (0.1, 0.12), (0, 0.16)], (0, 3.9, z), STEEL, seg=24)
        for b in range(4): m.box((0.58, 0.03, 0.17), (0.3 * math.cos(math.radians(45 + b * 90)), 3.98, z + 0.3 * math.sin(math.radians(45 + b * 90))), STEEL, rot=(0, -(45 + b * 90), 0))
        m.torus(0.74, 0.04, (0, 3.9, z), STEEL, seg=24, tube=6)
    m.cyl(0.26, 1.0, (0.8, 3.76 + 0.0, zc - 3.1), STEEL, seg=14); m.cyl(0.32, 0.08, (0.8, 4.29, zc - 3.1), DARK, seg=14)
    for s in (-1, 1):
        m.cyl(0.11, Lz - 1.2, (s * 1.2, 3.62, zc), STEEL, axis='z', seg=10)
        m.box((0.12, 0.7, 0.12), (s * 1.2, 3.35, zc + 3.0), STEEL)
        m.box((0.045, 0.08, Lz - 1.2), (s * 1.93, 1.05, zc), LED_B)
        for k in range(3):
            zz = zc - 2.5 + k * 2.4
            m.box((0.08, 1.05, 1.5), (s * 1.9, 1.85, zz), DARK, r=0.02)
            for j in range(5): m.box((0.05, 0.03, 1.3), (s * 1.95, 1.45 + j * 0.2, zz), STEEL)
        m.box((0.07, 1.4, 1.0), (s * 1.9, 1.95, zc + 3.1), DARK, r=0.03)
        m.box((0.04, 1.2, 0.8), (s * 1.94, 1.95, zc + 3.1), GLASSE)
    m.box((3.0, 0.3, 0.05), (0, 2.5, zc + Lz / 2 - 0.05), YELLOW)
    m.box((1.25, 2.1, 0.12), (0, 1.4, 1.74), DARK, r=0.04); m.box((1.0, 1.7, 0.08), (0, 1.45, 1.79), GLASSE)
    m.box((3.0, 1.4, 0.3), (0, 1.9, zc - Lz / 2 + 0.05), DARK, r=0.05)
    for k in range(4): m.cyl(0.12, 1.2, (-1.1 + k * 0.73, 1.9, zc - Lz / 2 - 0.15), STEEL, seg=10)
    for k in range(6): m.box((0.5, 0.05, 0.05), (1.1 + 0.0, 1.0 + k * 0.5, 1.85), STEEL) if False else None
    m.sph(0.07, (1.7, 3.3, 1.7), LED_G, seg=8, rings=5); m.sph(0.07, (-1.7, 3.3, 1.7), LED_A, seg=8, rings=5)
    return asset('OGS_Module', m, 'OGS')

# =====================================================================  STO  (HouseCylinder.glb)
def build_storage():
    m = mk('STO')
    m.lathe([(0, 0), (2.8, 0), (2.8, 0.42), (0, 0.42)], (0, 0, 0), DARK, seg=36)
    m.lathe([(0, 0.42), (2.66, 0.42), (2.66, 3.35), (2.55, 3.55), (1.6, 3.95), (0.0, 4.15)], (0, 0, 0), HULL, seg=36, smooth_deg=35)
    m.torus(2.68, 0.09, (0, 3.35, 0), DARK, seg=36, tube=8)
    m.torus(2.68, 0.035, (0, 1.25, 0), LED_B, seg=48, tube=6)
    for k in range(10):
        a = 36 * k + 18
        x, z = 2.66 * math.cos(math.radians(a)), 2.66 * math.sin(math.radians(a))
        m.box((0.22, 2.9, 0.16), (x, 1.88, z), DARK, r=0.03, rot=(0, -a, 0))
    for a in (60, 150, 240, 330):
        x, z = 2.7 * math.cos(math.radians(a)), 2.7 * math.sin(math.radians(a))
        m.cyl(0.24, 0.08, (x, 2.5, z), DARK, axis='x', seg=20, rot=(0, -a, 0)); m.cyl(0.18, 0.09, (x * 1.005, 2.5, z * 1.005), GLASSE, axis='x', seg=20, rot=(0, -a, 0))
    m.box((1.5, 2.2, 0.2), (0, 1.52, 2.68), DARK, r=0.05); m.box((1.25, 1.95, 0.1), (0, 1.5, 2.77), GLASSE)
    m.box((1.7, 0.14, 0.3), (0, 2.7, 2.72), YELLOW)
    m.box((1.7, 0.12, 1.0), (0, 0.46, 3.0), STEEL, r=0.02)
    m.cyl(0.62, 0.18, (0, 4.1, 0), DARK, seg=24); m.cyl(0.22, 0.5, (1.1, 4.0, 0.6), STEEL, seg=12); m.cyl(0.3, 0.06, (1.1, 4.3, 0.6), DARK, seg=12)
    m.sph(0.08, (0, 4.28, 0), LED_A, seg=8, rings=5)
    return asset('STO_Cylinder', m, 'STO')

# =====================================================================  COM  (RoofAntenna.glb)  ground = local y 4.05
def build_comms():
    m = mk('COM'); m.base = T(0, 4.05, 0)
    m.lathe([(0, 0), (1.9, 0), (1.85, 0.4), (1.25, 3.6), (1.4, 3.75), (1.4, 4.2), (0, 4.2)], (0, 0, 0), HULL, seg=8, smooth_deg=5, rot=(0, 22.5, 0))
    m.torus(1.38, 0.07, (0, 4.2, 0), DARK, seg=8, tube=6, rot=(0, 22.5, 0))
    m.box((0.9, 1.9, 0.12), (0, 1.05, 1.62), DARK, r=0.03, rot=(-8, 0, 0)); m.box((0.7, 1.6, 0.06), (0, 1.05, 1.7), GLASSE, rot=(-8, 0, 0))
    for k in range(8):
        a = 45 * k + 22.5
        if abs(a - 90) < 30: continue
        x, z = 1.5 * math.cos(math.radians(a)), 1.5 * math.sin(math.radians(a))
        m.box((0.4, 1.0, 0.05), (x * 0.92, 2.3, z * 0.92), DARK, rot=(0, -a + 90, 0))
    m.torus(1.9, 0.035, (0, 0.25, 0), LED_B, seg=8, tube=6, rot=(0, 22.5, 0))
    # platform + yellow cage
    m.cyl(1.5, 0.12, (0, 4.26, 0), DARK, seg=24)
    for (x, y, z) in radial(14, 1.45, 4.32):
        m.cyl(0.03, 1.1, (x, 4.87, z), RAILY, seg=6)
    m.torus(1.45, 0.035, (0, 5.4, 0), RAILY, seg=40, tube=6); m.torus(1.45, 0.03, (0, 4.85, 0), RAILY, seg=40, tube=6)
    # tank cluster
    for (x, y, z) in radial(6, 0.62, 4.32, 15) + [(0, 4.32, 0)]:
        m.cyl(0.22, 1.7, (x, 5.2, z), WHITEP, seg=14); m.sph(0.22, (x, 6.05, z), WHITEP, sc=(1, 0.6, 1), seg=14, rings=5)
        m.torus(0.225, 0.02, (x, 5.5, z), GOLDB, seg=14, tube=5)
    m.cyl(0.92, 0.1, (0, 6.2, 0), DARK, seg=24)
    # lattice mast
    lattice(m, 6.25, 13.2, 0.55, 0.2, 8, 0.05, STEEL, nleg=3, rot0=90)
    m.cyl(0.26, 0.2, (0, 6.3, 0), STEEL, seg=12)
    m.cyl(0.05, 1.2, (0, 13.7, 0), STEEL, seg=8); m.sph(0.1, (0, 14.3, 0), LED_R, seg=8, rings=5)
    # dishes
    def dish(y, ang, r, mount):
        mm = T(0, y, 0) @ R('y', ang) @ T(0.45, 0, 0) @ R('z', -90) @ R('x', 0)
        m.add(dish_geo(r, r * 0.3, 28, 6, 0.025), WHITEP, mm)
        m.add(cylinder_geo(0.03, r * 0.9, 6), STEEL, T(0, y, 0) @ R('y', ang) @ T(0.45 + r * 0.45, 0, 0) @ R('z', 90))
        m.add(sphere_geo(0.06, 8, 5), LED_A, T(0, y, 0) @ R('y', ang) @ T(0.45 + r * 0.9, 0, 0))
    dish(10.3, 0, 0.95, 0); dish(8.2, 150, 0.7, 0); dish(12.2, 240, 0.5, 0)
    # guy wires
    for ang in (30, 150, 220, 290):
        c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        top = (0.18 * c, 11.2, 0.18 * s); anc = (6.0 * c, 0.0, 6.0 * s)
        m.between(top, anc, 0.014, STEEL, seg=5)
        m.box((0.5, 0.3, 0.5), (6.0 * c, 0.15, 6.0 * s), DARK, r=0.04, rot=(0, -ang, 0))
        m.between((0.18 * c, 8.2, 0.18 * s), (3.4 * c, 0.2, 3.4 * s), 0.0, STEEL, seg=3) if False else None
    return asset('COM_Tower', m, 'COM')

# =====================================================================  RAD  (RoofRadar.glb)  ground = local y 4.05
def build_radar():
    m = mk('RAD'); m.base = T(0, 4.05, 0)
    m.box((1.35, 0.22, 1.2), (0, 0.11, 0), DARK, r=0.04)
    m.box((0.95, 1.25, 0.7), (-0.05, 0.84, -0.05), HULL, r=0.08)
    m.box((0.5, 0.35, 0.04), (-0.05, 0.9, 0.31), GLASSE); m.sph(0.045, (-0.25, 0.62, 0.32), LED_G, seg=8, rings=5)
    m.sph(0.045, (-0.12, 0.62, 0.32), LED_A, seg=8, rings=5)
    m.box((0.96, 0.08, 0.72), (-0.05, 1.5, -0.05), YELLOW, r=0.02)
    m.cyl(0.06, 1.2, (0.1, 2.1, -0.1), STEEL, seg=10)
    m.cyl(0.14, 0.12, (0.1, 2.75, -0.1), DARK, seg=14)
    m.sph(0.18, (0.1, 2.95, -0.1), GOLDA, seg=18, rings=10)
    m.torus(0.26, 0.025, (0.1, 2.88, -0.1), STEEL, seg=20, tube=6)
    m.add(dish_geo(0.5, 0.15, 24, 5, 0.02), WHITEP, T(0.2, 1.75, 0.3) @ R('z', -60) @ R('y', 0))
    m.cyl(0.02, 0.45, (0.35, 1.9, 0.2), STEEL, seg=6, rot=(0, 0, -40))
    m.box((0.5, 0.03, 0.34), (-0.05, 1.60, -0.05), SOLARC, rot=(-18, 0, 0))
    # radiation trefoil plate
    m.cyl(0.19, 0.02, (0.52, 0.86, 0.0), YELLOW, axis='x', seg=24)
    for k in range(3):
        m.add(lathe([(0.045, 0), (0.17, 0)], 8, 60, 60), BLACK, T(0.535, 0.86, 0.0) @ R('z', -90) @ R('y', 120 * k) @ R('y', 0))
    return asset('RAD_Monitor', m, 'RAD')

# =====================================================================  GAR  (BaseLarge.glb)
def build_garage():
    m = mk('GAR')
    m.box((8.46, 0.4, 8.46), (0, 0.2, 0), DARK, r=0.04)
    m.box((7.4, 3.25, 7.8), (0, 2.0, 0), HULL, r=0.3)
    m.box((7.6, 0.3, 8.0), (0, 3.78, 0), HULL2, r=0.1)
    m.box((7.0, 0.5, 4.2), (0, 4.1, -1.2), HULL, r=0.1)
    # buttress wedges along both sides
    for s in (-1, 1):
        w = extrude_geo([(0, 0), (0.55, 0), (0, 2.9)], 6.8).xf(T(0, 0.4, 0) @ R('y', 90 if s > 0 else -90) @ T(0, 0, 0))
        w = extrude_geo([(0, 0), (0.55, 0), (0, 2.9)], 6.4)
        M = T(s * 3.69, 0.4, 0) @ (R('y', 180) if s < 0 else np.eye(4)) @ R('y', 0)
        # profile lies in XY, extruded along Z: need it to extend in +x for s>0 and -x for s<0, running along Z
        m.add(w, HULL, M)
        m.box((0.05, 0.05, 6.4), (s * 4.05, 0.55, 0), LED_B)
        for k in range(3):
            m.box((0.07, 1.0, 1.4), (s * 3.72, 2.0, -2.3 + k * 2.3), DARK, r=0.02)
            for j in range(4): m.box((0.05, 0.03, 1.2), (s * 3.76, 1.65 + j * 0.22, -2.3 + k * 2.3), STEEL)
    # front door (+Z)
    m.box((5.2, 3.15, 0.3), (0, 1.97, 3.86), DARK, r=0.04)
    m.box((4.6, 2.85, 0.12), (0, 1.82, 3.93), BLACK)
    m.box((4.5, 0.08, 0.2), (0, 3.15, 3.8), LAMP)
    for k in range(3): m.box((0.08, 2.7, 0.08), (-1.5 + k * 1.5, 1.8, 3.8), STEEL) if False else None
    for k in range(7): m.box((4.5, 0.16, 0.14), (0, 3.15 - k * 0.0, 3.9), DARK) if False else None
    m.cyl(0.2, 4.7, (0, 3.5, 3.9), STEEL, axis='x', seg=14)
    for k in range(5): m.box((4.5, 0.12, 0.08), (0, 3.28 - k * 0.12, 3.98), DARK)
    for s in (-1, 1):
        m.box((0.18, 3.15, 0.34), (s * 2.7, 1.97, 3.9), YELLOW)
        for k in range(8): m.box((0.2, 0.18, 0.36), (s * 2.7, 0.55 + k * 0.36, 3.9), BLACK, rot=(0, 0, 15))
    m.box((6.0, 0.1, 0.9), (0, 0.45, 4.1), STEEL, r=0.02)
    m.box((5.2, 0.3, 0.15), (0, 3.7, 3.95), YELLOW); m.box((0.8, 0.5, 0.1), (-2.0, 3.7, 4.05), NAVY)
    # roof: gold solar rail + dish + vents
    for k in range(6):
        m.box((1.05, 0.05, 0.75), (-3.0 + k * 1.2, 4.62, -2.5), GOLDA, rot=(-28, 0, 0))
        m.box((1.08, 0.04, 0.05), (-3.0 + k * 1.2, 4.43, -2.18), DARK)
        m.box((0.06, 0.45, 0.06), (-3.0 + k * 1.2, 4.45, -2.9), STEEL)
    m.box((7.0, 0.1, 0.1), (0, 4.38, -2.95), DARK)
    m.add(dish_geo(0.75, 0.22, 28, 6, 0.025), WHITEP, T(2.6, 4.7, 1.4) @ R('x', -35) @ R('y', 20))
    m.cyl(0.08, 0.7, (2.6, 4.3, 1.4), STEEL, seg=8)
    m.box((0.8, 0.4, 0.6), (-2.4, 4.52, 1.0), DARK, r=0.05); m.cyl(0.25, 0.3, (-1.2, 4.45, 0.4), STEEL, seg=14)
    m.cyl(0.04, 0.9, (-3.2, 4.8, 0.9), STEEL, seg=6); m.sph(0.07, (-3.2, 4.9, 0.9), LED_R, seg=8, rings=5)
    m.sph(0.07, (3.0, 3.7, 3.9), LED_A, seg=8, rings=5); m.sph(0.07, (-3.0, 3.7, 4.0), LED_G, seg=8, rings=5)
    return asset('GAR_Garage', m, 'GAR')

# =====================================================================  SOL-S  (SolarPanelGround.glb)
def build_solar():
    m = mk('SOLS'); tilt = 38.0; w = 1.18; sl = 0.62
    m.box((1.3, 0.1, 0.9), (0, 0.05, 0.0), DARK, r=0.02)
    for s in (-1, 1): m.box((0.08, 0.55, 0.08), (s * 0.4, 0.35, -0.12), STEEL)
    m.box((1.25, 0.07, 0.07), (0, 0.62, -0.12), STEEL)
    for k, off in enumerate((-0.33, 0.33)):
        y = 0.7 + 0.0; 
        C = T(0, 0.64, -0.12) @ R('x', tilt) @ T(0, 0.04, -off + 0.0)
        # panel lies in local XZ plane; tilted so that +Z side faces up/out
        m.add(box_geo(w, 0.035, sl, 0.01), SOLARC, T(0, 0.64, -0.12) @ R('x', tilt) @ T(0, 0.07, off))
        m.add(box_geo(w + 0.06, 0.03, 0.04), STEEL, T(0, 0.64, -0.12) @ R('x', tilt) @ T(0, 0.06, off + sl / 2))
        m.add(box_geo(w + 0.06, 0.03, 0.04), STEEL, T(0, 0.64, -0.12) @ R('x', tilt) @ T(0, 0.06, off - sl / 2))
        for s in (-1, 1): m.add(box_geo(0.04, 0.03, sl), STEEL, T(0, 0.64, -0.12) @ R('x', tilt) @ T(s * (w / 2 + 0.01), 0.06, off))
    m.box((0.1, 0.1, 0.1), (0, 0.12, -0.5), DARK, r=0.02); m.between((0, 0.1, -0.5), (0, 0.6, -0.14), 0.03, STEEL, seg=6)
    m.sph(0.03, (0.5, 0.12, 0.35), LED_G, seg=8, rings=5)
    return asset('SOLS_Array', m, 'SOLS')

# =====================================================================  BAT  (PickupCrate.glb)
def build_battery():
    m = mk('BAT')
    m.box((0.8, 0.62, 0.78), (0, -0.05, 0), DARK, r=0.05)
    m.box((0.82, 0.14, 0.8), (0, -0.30, 0), HULL, r=0.03)
    m.box((0.84, 0.16, 0.82), (0, 0.18, 0), HULL, r=0.04)
    m.box((0.5, 0.05, 0.04), (0, 0.0, 0.405), YELLOW); m.box((0.5, 0.05, 0.04), (0, 0.0, -0.405), YELLOW)
    for k in range(3): m.box((0.08, 0.05, 0.03), (-0.18 + k * 0.18, -0.08, 0.41), LED_G if k < 2 else LED_A)
    for s in (-1, 1):
        m.torus(0.1, 0.018, (s * 0.42, 0.0, 0.0), STEEL, axis='x', seg=16, tube=5, rot=(0, 0, 0))
        m.cyl(0.07, 0.08, (s * 0.2, 0.3, 0.1), STEEL if s > 0 else DARK, seg=10)
        m.cyl(0.035, 0.04, (s * 0.2, 0.35, 0.1), LED_R if s > 0 else BLACK, seg=8)
    m.box((0.28, 0.02, 0.18), (0, 0.265, -0.15), NAVY)
    return asset('BAT_Pack', m, 'BAT')

# =====================================================================  TNK  (PickupJar.glb)  y -0.59..0.43
def build_tank():
    m = mk('TNK'); y0 = -0.59
    m.lathe([(0, y0 + 0.07), (0.3, y0 + 0.07), (0.345, y0 + 0.2), (0.345, y0 + 0.82), (0.3, y0 + 0.95), (0.12, y0 + 1.0), (0.1, y0 + 1.02)], (0, 0, 0), TANKW, seg=28, smooth_deg=50)
    m.cyl(0.352, 0.1, (0, y0 + 0.42, 0), O2B, seg=28); m.cyl(0.352, 0.03, (0, y0 + 0.58, 0), STEEL, seg=28)
    m.cyl(0.07, 0.1, (0, 0.4, 0), STEEL, seg=10); m.cyl(0.02, 0.2, (0, 0.38, 0), STEEL, axis='x', seg=6)
    m.torus(0.1, 0.012, (0, 0.40, 0), DARK, seg=16, tube=5)
    for a in (0, 90, 180, 270):
        x, z = 0.28 * math.cos(math.radians(a)), 0.28 * math.sin(math.radians(a))
        m.box((0.08, 0.1, 0.08), (x, y0 + 0.05, z), DARK, r=0.015)
    m.torus(0.35, 0.02, (0, y0 + 0.25, 0), STEEL, seg=28, tube=5); m.torus(0.35, 0.02, (0, y0 + 0.75, 0), STEEL, seg=28, tube=5)
    return asset('TNK_Tank', m, 'TNK')

# =====================================================================  LANDER  (Spaceship.glb)  centred, feet y=-0.41
def build_lander():
    m = mk('LANDER'); fy = -0.41
    # descent stage
    m.lathe([(0, 0.55), (1.0, 0.55), (2.15, 0.95), (2.15, 2.35), (1.7, 2.5), (0, 2.5)], (0, 0, 0), GOLDA, seg=8, smooth_deg=5, rot=(0, 22.5, 0))
    m.lathe([(0, 0.2), (0.45, 0.2), (0.95, 0.65), (0, 0.65)], (0, 0, 0), DARK, seg=20)
    m.torus(2.17, 0.05, (0, 1.65, 0), DARK, seg=8, tube=6, rot=(0, 22.5, 0))
    # ascent stage
    m.lathe([(0, 2.5), (1.55, 2.5), (1.55, 4.1), (1.15, 4.5), (0, 4.5)], (0, 0, 0), HULL, seg=8, smooth_deg=5, rot=(0, 22.5, 0))
    m.box((0.95, 0.9, 0.12), (0, 3.65, 1.52), DARK, r=0.04)
    m.box((0.7, 0.62, 0.08), (0, 3.65, 1.58), GLASSE)
    for s in (-1, 1): m.box((0.12, 0.7, 0.55), (s * 1.18, 3.7, 1.05), GLASSE, rot=(0, -s * 38, 0))
    m.cyl(0.5, 0.22, (0, 4.6, 0), DARK, seg=16); m.cyl(0.32, 0.1, (0, 4.75, 0), STEEL, seg=16)
    m.add(dish_geo(0.55, 0.16, 24, 5, 0.02), WHITEP, T(-0.9, 4.65, -0.5) @ R('z', 28) @ R('y', -30))
    m.cyl(0.02, 1.0, (0.8, 4.95, -0.6), STEEL, seg=6); m.sph(0.05, (0.8, 5.46, -0.6), LED_R, seg=8, rings=5)
    for (x, z) in ((1.6, 1.6), (-1.6, 1.6), (1.6, -1.6), (-1.6, -1.6)):
        m.box((0.3, 0.3, 0.3), (x * 0.9, 3.2, z * 0.9), DARK, r=0.04, rot=(0, 45, 0))
    m.box((1.2, 0.12, 0.95), (0, 2.45, 1.95), STEEL, r=0.02)
    # legs + pads along axes
    for ang in (0, 90, 180, 270):
        c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        foot = np.array([4.5 * c, fy + 0.06, 4.5 * s]); knee = np.array([1.9 * c, 1.6, 1.9 * s])
        m.between(knee, foot + np.array([0, 0.1, 0]), 0.1, GOLDB, r2=0.07, seg=8)
        for o in (-0.35, 0.35):
            side = np.array([-s, 0, c]) * o
            m.between(np.array([1.4 * c, 2.2, 1.4 * s]) + side * 0.5, foot * 0.7 + np.array([0, 0.7, 0]) * 0.9 + side * 0.4 + np.array([0, 0.0, 0]), 0.045, STEEL, seg=6)
        m.cyl(0.46, 0.07, (foot[0], fy + 0.035, foot[2]), DARK, seg=20); m.lathe([(0, 0.0), (0.46, 0.0), (0.4, 0.1), (0.1, 0.18), (0, 0.18)], (foot[0], fy + 0.04, foot[2]), GOLDC, seg=20)
    # ladder on +Z leg
    for s in (-1, 1): m.between((s * 0.3, 2.45, 1.85), (s * 0.3, fy + 0.2, 4.2), 0.03, STEEL, seg=6)
    for k in range(9): m.box((0.6, 0.03, 0.04), (0, 2.4 - k * 0.3, 1.9 + (4.2 - 1.9) * (k * 0.3) / 2.6), STEEL)
    m.sph(0.1, (0, 2.55, 0.0), LED_B, seg=8, rings=5) if False else None
    return asset('Lander', m, 'Lander')

JOBS = [
    ('GeodesicDome', build_dome), ('Connector', build_connector), ('HouseLong', build_ogs), ('HouseCylinder', build_storage),
    ('RoofAntenna', build_comms), ('RoofRadar', build_radar), ('BaseLarge', build_garage), ('SolarPanelGround', build_solar),
    ('PickupCrate', build_battery), ('PickupJar', build_tank), ('Spaceship', build_lander),
]
