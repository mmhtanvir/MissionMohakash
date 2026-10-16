"""
nasa_structs.py - outpost hardware modelled on NASA references (Milestone 06):

  SolarArrayVSAT.glb   Vertical Solar Array Technology (VSAT, Artemis south-pole power): tripod, ~10 m deployable mast,
                       tall vertical blanket on a 'Tracker' node that the game turns toward the low Sun.
  LunarBattery.glb     surface energy-storage module: MLI-wrapped battery pack on a pallet, radiator fins, power cable.
  OxygenPlant.glb      electrolysis O2 plant (ISS OGS / lunar ISRU style skid): cell stack, water feed tank,
                       O2 accumulator spheres, radiator panel, piping, control box.
  OxygenTank.glb       composite pressure vessel in a cradle, white with green O2 band + label.
  WaterTank.glb        larger potable-water tank, white with blue band + label.
  LogisticsPallet.glb  unpressurised logistics pallet: cargo transfer bags and hard cases under cargo straps.
  CommsTerminal.glb    lunar surface comm terminal: tripod mast, steerable high-gain dish (+Z, tilted up), whip antennas.
  RadiationStation.glb radiation sensor station (HERA-style detector head on a gold-MLI electronics box, solar panel, antenna).
  RoverGarage.glb      arched rover shelter with regolith sandbag shielding, open front (+Z), charging post.

All are authored at real size, origin on the ground, front = +Z.     python3 nasa_structs.py <out_models_dir>
"""
import sys, os, math, numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from structures import *
from glbkit import Geo, R as Rot, T as Tr
import preview

def label_tex(name, text, bg, fg, band=None, w=512, h=256):
    im = Image.new('RGB', (w, h), bg); dr = ImageDraw.Draw(im)
    if band: dr.rectangle((0, int(h * 0.72), w, h), fill=band)
    size = int(h * 0.5)
    while True:                                                    # largest font that fits 88 % of the width
        try: f = ImageFont.load_default(size=size)
        except TypeError: f = ImageFont.load_default(); break
        if dr.textlength(text, font=f) <= w * 0.88 or size < 12: break
        size -= 2
    tw = dr.textlength(text, font=f); dr.text(((w - tw) / 2, (h * 0.72 - size) / 2), text, fill=fg, font=f)
    p = TX + name + '.png'; im.save(p); return p

def decal(m, center, w, h, mat, face='+z'):
    """flat textured quad (u left->right, v top->bottom) just off a face"""
    cx, cy, cz = center; hw, hh = w / 2, h / 2
    if face == '+z': P = [(cx - hw, cy + hh, cz), (cx + hw, cy + hh, cz), (cx - hw, cy - hh, cz), (cx + hw, cy - hh, cz)]; N = (0, 0, 1)
    elif face == '-z': P = [(cx + hw, cy + hh, cz), (cx - hw, cy + hh, cz), (cx + hw, cy - hh, cz), (cx - hw, cy - hh, cz)]; N = (0, 0, -1)
    elif face == '+x': P = [(cx, cy + hh, cz + hw), (cx, cy + hh, cz - hw), (cx, cy - hh, cz + hw), (cx, cy - hh, cz - hw)]; N = (1, 0, 0)
    else: P = [(cx, cy + hh, cz - hw), (cx, cy + hh, cz + hw), (cx, cy - hh, cz - hw), (cx, cy - hh, cz + hw)]; N = (-1, 0, 0)
    g = Geo(P, [N] * 4, [[0, 2, 1], [1, 2, 3]]); g.UV = np.array([[0, 0], [1, 0], [0, 1], [1, 1]], float)
    m.add(g, mat)

def tank_profile(r, L):
    """capsule-shaped pressure vessel along Y (centre at 0)"""
    pr = [(0.0, -L / 2 - r)]
    for a in np.linspace(-90, 0, 7)[1:]: pr.append((r * math.cos(math.radians(a)), -L / 2 + r * math.sin(math.radians(a))))
    for a in np.linspace(0, 90, 7)[:-1]: pr.append((r * math.cos(math.radians(a)), L / 2 + r * math.sin(math.radians(a))))
    pr.append((0.0, L / 2 + r)); return pr

def tripod(m, top, foot_r, mat=STEEL, rad=0.05, n=3, rot0=90):
    for k in range(n):
        a = math.radians(rot0 + k * 360 / n)
        foot = (foot_r * math.cos(a), 0.06, foot_r * math.sin(a))
        m.between(top, foot, rad, mat, seg=10); m.cyl(0.18, 0.06, foot, DARK, seg=16)

# ---------------------------------------------------------------- VSAT solar array
def build_vsat():
    base = mk('VSAT_Base'); H = 10.0
    tripod(base, (0, 1.2, 0), 1.8, rad=0.07)
    base.box((0.7, 0.55, 0.7), (0, 0.95, 0), GOLDA, r=0.04)                       # power electronics in gold MLI
    base.box((0.5, 0.06, 0.5), (0, 1.25, 0), WHITEP)
    base.cyl(0.16, 0.5, (0, 1.5, 0), DARK, seg=16)                                  # mast canister + rotary joint
    base.cyl(0.11, H - 1.75, (0, 1.75 + (H - 1.75) / 2, 0), STEEL, seg=14)          # deployed mast
    for y in np.arange(2.2, H, 0.55): base.torus(0.13, 0.025, (0, y, 0), DARK, seg=14, tube=4)
    base.sph(0.1, (0, H + 0.08, 0), LED_R, seg=8, rings=6)
    tr = mk('VSAT_Blanket')                                                          # vertical blanket, faces +Z (turned to the Sun in game)
    y0, y1 = 2.6, H - 0.35
    for sx in (-1, 1):
        x = sx * 0.95
        tr.box((1.55, y1 - y0, 0.03), (x, (y0 + y1) / 2, 0.12), SOLARC)
        tr.box((0.04, y1 - y0, 0.06), (x + sx * 0.79, (y0 + y1) / 2, 0.12), STEEL)
        for y in np.linspace(y0, y1, 9): tr.box((1.6, 0.025, 0.05), (x, y, 0.13), STEEL)
    tr.box((3.6, 0.08, 0.1), (0, y1 + 0.05, 0.1), STEEL); tr.box((3.6, 0.08, 0.1), (0, y0 - 0.05, 0.1), STEEL)
    for y in (y0 - 0.05, y1 + 0.05): tr.box((0.2, 0.12, 0.2), (0, y, 0.0), DARK)
    A = Asset('SolarArrayVSAT'); r = A.add(Node('VSAT')); r.add(Node('VSAT_Mesh', mesh=base))
    t = r.add(Node('Tracker')); t.add(Node('Blanket_Mesh', mesh=tr))
    return A

# ---------------------------------------------------------------- battery
def build_battery():
    m = mk('BAT'); lab = Mat('BatLabel', (1, 1, 1), 0, 0.6, tex=label_tex('lab_bat', 'BATTERY 10 PWh', (235, 235, 230), (20, 30, 70), (255, 140, 20)), tile=1.0)
    m.box((1.7, 0.12, 1.1), (0, 0.16, 0), STEEL, r=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1): m.box((0.16, 0.14, 0.16), (sx * 0.7, 0.07, sz * 0.42), DARK)
    m.box((1.45, 0.75, 0.9), (0, 0.6, 0), GOLDB, r=0.05)                             # battery pack in gold MLI
    m.box((1.5, 0.05, 0.95), (0, 1.0, 0), WHITEP)
    for k in range(9): m.box((0.04, 0.22, 0.85), (-0.64 + k * 0.16, 1.13, 0), WHITEP)  # radiator fins
    m.box((0.9, 0.38, 0.03), (0, 0.62, 0.465), DARK)
    for k in range(5): m.box((0.1, 0.05, 0.02), (-0.3 + k * 0.15, 0.72, 0.48), LED_G if k < 4 else LED_A)
    decal(m, (0, 0.5, 0.484), 0.7, 0.18, lab)
    for sx in (-1, 1): m.between((sx * 0.5, 0.98, 0.47), (sx * 0.5, 1.1, 0.47), 0.02, RAILY, seg=6)
    m.cyl(0.07, 0.1, (0.78, 0.55, 0), DARK, axis='x', seg=12)                       # power connector + cable to the bus
    m.between((0.84, 0.55, 0), (1.2, 0.05, 0.3), 0.03, BLACK, seg=8)
    m.torus(0.22, 0.03, (1.35, 0.04, 0.45), BLACK, seg=20, tube=5)
    return asset('LunarBattery', m, 'BAT')

# ---------------------------------------------------------------- oxygen plant (electrolysis skid)
def build_oxygen_plant():
    m = mk('OGS'); lab = Mat('OgsLabel', (1, 1, 1), 0, 0.6, tex=label_tex('lab_ogs', 'O2 GENERATOR', (235, 235, 230), (20, 30, 70), (40, 160, 70)), tile=1.0)
    m.box((3.4, 0.2, 1.8), (0, 0.22, 0), DARK)
    for sx in (-1, 1):
        for sz in (-1, 1): m.box((0.2, 0.14, 0.2), (sx * 1.5, 0.07, sz * 0.75), STEEL)
    # electrolyzer cell stack (horizontal, on cradles)
    for k in range(18):
        m.cyl(0.38 if k % 2 else 0.35, 0.05, (-1.25 + 0.06 * k, 0.85, -0.3), DARK if k % 2 else STEEL, axis='x', seg=24)
    m.cyl(0.42, 0.08, (-1.3, 0.85, -0.3), STEEL, axis='x', seg=24); m.cyl(0.42, 0.08, (-0.17, 0.85, -0.3), STEEL, axis='x', seg=24)
    for x in (-1.15, -0.3): m.box((0.12, 0.45, 0.7), (x, 0.5, -0.3), STEEL)
    for k in range(4): m.between((-1.3, 0.85 + 0.3 * math.cos(k * math.pi / 2), -0.3 + 0.3 * math.sin(k * math.pi / 2)),
                                 (-0.17, 0.85 + 0.3 * math.cos(k * math.pi / 2), -0.3 + 0.3 * math.sin(k * math.pi / 2)), 0.02, STEEL, seg=6)   # tie rods
    # water feed tank (vertical) and two O2 accumulator spheres
    m.lathe(tank_profile(0.42, 0.7), (0.55, 1.05, -0.35), WHITEP, seg=28)
    m.cyl(0.43, 0.1, (0.55, 1.05, -0.35), O2B, seg=28)
    for k, z in enumerate((-0.4, 0.4)):
        m.sph(0.33, (1.35, 0.68, z), TANKW, seg=24, rings=12)
        m.torus(0.335, 0.03, (1.35, 0.68, z), Mat('O2Green', (0.1, 0.6, 0.25), 0.2, 0.4), seg=24, tube=5)
        m.cyl(0.18, 0.35, (1.35, 0.42, z), STEEL, seg=14)
    # radiator panel on a post (rejects the stack's waste heat)
    m.cyl(0.05, 1.5, (-1.0, 1.0, 0.6), STEEL, seg=8)
    m.box((1.5, 1.0, 0.04), (-1.0, 1.95, 0.6), WHITEP)
    for k in range(7): m.box((1.45, 0.025, 0.05), (-1.0, 1.5 + k * 0.15, 0.62), STEEL)
    # piping: water tank -> stack, stack -> spheres
    m.between((0.15, 0.95, -0.35), (-0.15, 0.95, -0.3), 0.04, STEEL, seg=8)
    m.between((-0.7, 1.25, -0.3), (-0.7, 1.45, 0.2), 0.035, STEEL, seg=8); m.between((-0.7, 1.45, 0.2), (1.35, 1.45, 0.2), 0.035, STEEL, seg=8)
    for z in (-0.4, 0.4): m.between((1.35, 1.45, 0.2), (1.35, 1.0, z), 0.03, STEEL, seg=8)
    # control box with status lights + label
    m.box((0.6, 0.55, 0.35), (0.35, 0.6, 0.6), WHITEP, r=0.03)
    m.box((0.45, 0.2, 0.02), (0.35, 0.68, 0.78), DARK)
    for k in range(3): m.box((0.06, 0.04, 0.02), (0.22 + k * 0.13, 0.52, 0.785), (LED_G, LED_G, LED_B)[k])
    decal(m, (0.35, 0.82, 0.785), 0.5, 0.1, lab)
    m.cyl(0.06, 0.4, (-1.45, 1.35, -0.3), STEEL, seg=8); m.sph(0.08, (-1.45, 1.58, -0.3), DARK, seg=8, rings=5)   # vent
    return asset('OxygenPlant', m, 'OGS')

# ---------------------------------------------------------------- pressure tanks
def build_tank(name, label, band_col, r, L):
    m = mk(name)
    lab = Mat(name + 'Label', (1, 1, 1), 0, 0.6, tex=label_tex('lab_' + name.lower(), label, (240, 240, 236), (20, 30, 60), tuple(int(c * 255) for c in band_col)), tile=1.0)
    band = Mat(name + 'Band', band_col, 0.2, 0.45)
    cy = r + 0.28
    m.lathe(tank_profile(r, L), (0, cy, 0), TANKW, seg=32, axis='x')
    for x in (-L * 0.25, L * 0.25): m.cyl(r + 0.01, 0.12, (x, cy, 0), band, axis='x', seg=32)
    for x in (-L * 0.32, L * 0.32):                                                 # cradles
        m.box((0.14, 0.26, r * 1.7), (x, 0.13, 0), STEEL, r=0.02)
        m.torus(r + 0.03, 0.03, (x, cy, 0), STEEL, axis='x', seg=24, tube=5)
    m.cyl(0.05, 0.2, (L / 2 + r - 0.05, cy, 0), STEEL, axis='x', seg=10)            # valve + gauge
    m.cyl(0.08, 0.05, (L / 2 + r + 0.07, cy + 0.08, 0), DARK, axis='x', seg=14)
    m.box((0.06, 0.1, 0.06), (L / 2 + r + 0.1, cy, 0), YELLOW)
    decal(m, (0, cy + 0.02, r + 0.012), L * 0.35, L * 0.175, lab)
    decal(m, (0, cy + 0.02, -r - 0.012), L * 0.35, L * 0.175, lab, face='-z')
    return asset(name, m, name)

# ---------------------------------------------------------------- logistics pallet (storage)
def build_logistics():
    m = mk('STO'); ctb = Mat('CTB', (0.84, 0.80, 0.68), 0.0, 0.9); case = Mat('HardCase', (0.9, 0.91, 0.92), 0.1, 0.45)
    lab = Mat('StoLabel', (1, 1, 1), 0, 0.6, tex=label_tex('lab_sto', 'LOGISTICS', (235, 235, 230), (20, 30, 70), (30, 80, 190)), tile=1.0)
    m.box((3.2, 0.22, 2.2), (0, 0.38, 0), STEEL, r=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1): m.box((0.2, 0.3, 0.2), (sx * 1.4, 0.15, sz * 0.9), DARK); m.between((sx * 1.55, 0.49, sz * 1.05), (sx * 1.55, 2.4, sz * 1.05), 0.04, RAILY, seg=8)
    rng = np.random.default_rng(4)
    for ix in range(3):
        for iz in range(2):
            for iy in range(3):
                if iy == 2 and (ix + iz) % 2: continue
                x, z, y = -1.0 + ix * 1.0, -0.5 + iz * 1.0, 0.49 + iy * 0.58
                hard = (ix + iy) % 3 == 0
                m.box((0.92, 0.55, 0.92), (x, y + 0.28, z), case if hard else ctb, r=0.03 if hard else 0.08)
                if hard: m.box((0.94, 0.06, 0.94), (x, y + 0.42, z), O2B)
                else: m.box((0.06, 0.56, 0.94), (x, y + 0.28, z), Mat('CTBStrap', (0.2, 0.3, 0.55), 0.1, 0.6))
    for x in (-0.75, 0.25, 1.15):                                                    # cargo straps over the stack
        m.box((0.06, 0.02, 2.2), (x, 2.24, 0), YELLOW)
        for sz in (-1, 1): m.box((0.06, 1.8, 0.02), (x, 1.34, sz * 1.08), YELLOW)
    decal(m, (0, 0.38, 1.104), 1.2, 0.2, lab)
    return asset('LogisticsPallet', m, 'STO')

# ---------------------------------------------------------------- comms terminal
def build_comms():
    m = mk('COM'); H = 5.0
    tripod(m, (0, 1.4, 0), 2.0, rad=0.07)
    m.box((0.8, 0.6, 0.6), (0, 0.6, -0.9), GOLDA, r=0.04)                           # electronics in gold MLI
    m.between((0, 0.6, -0.6), (0, 1.3, 0), 0.03, BLACK, seg=6)
    m.cyl(0.12, H - 1.4, (0, 1.4 + (H - 1.4) / 2, 0), STEEL, seg=14)
    m.cyl(0.2, 0.3, (0, H, 0), DARK, seg=16)                                         # azimuth drive
    for sx in (-1, 1): m.box((0.08, 0.7, 0.25), (sx * 0.55, H + 0.4, 0), STEEL)      # elevation yoke
    m.box((1.18, 0.1, 0.3), (0, H + 0.08, 0), STEEL)
    tilt = 12.0                                                                       # Earth sits ~7 deg up at the south pole
    D = Tr(0, H + 0.55, 0.05) @ Rot('x', 90 - tilt)
    m.add(dish_geo(0.95, 0.28, seg=36), Mat('DishWhite', (0.9, 0.91, 0.92), 0.1, 0.45, double=True), M=D)
    for k in range(3):                                                                # feed horn on 3 struts
        a = math.radians(90 + k * 120); p = D @ np.array([0.9 * math.cos(a), 0.29, 0.9 * math.sin(a), 1.0])
        f = D @ np.array([0, 0.85, 0, 1.0]); m.between(p[:3], f[:3], 0.015, STEEL, seg=6)
    f = D @ np.array([0, 0.85, 0, 1.0]); m.sph(0.07, tuple(f[:3]), DARK, seg=10, rings=6)
    m.box((1.4, 0.05, 0.05), (0, H - 0.6, 0), STEEL)                                  # whip antennas
    for x in (-0.65, 0.65): m.cyl(0.012, 1.1, (x, H - 0.05, 0), DARK, seg=6); m.sph(0.025, (x, H + 0.5, 0), DARK, seg=6, rings=4)
    m.sph(0.08, (0, H + 1.6, 0), LED_R, seg=8, rings=5); m.cyl(0.015, 0.9, (0, H + 1.15, -0.2), STEEL, seg=6)
    return asset('CommsTerminal', m, 'COM')

# ---------------------------------------------------------------- radiation station
def build_radiation():
    m = mk('RAD')
    lab = Mat('RadLabel', (1, 1, 1), 0, 0.6, tex=label_tex('lab_rad', 'RAD MONITOR', (240, 210, 40), (20, 20, 20)), tile=1.0)
    tripod(m, (0, 1.1, 0), 1.0, rad=0.045)
    m.box((0.5, 0.42, 0.45), (0, 1.3, 0), GOLDA, r=0.03)                             # electronics, gold MLI
    m.box((0.52, 0.04, 0.47), (0, 1.53, 0), WHITEP)
    m.cyl(0.13, 0.22, (0, 1.66, 0), DARK, seg=20)                                    # detector head, looking at the sky
    m.cyl(0.1, 0.02, (0, 1.78, 0), Mat('DetectorWindow', (0.6, 0.65, 0.7), 0.6, 0.2), seg=20)
    m.torus(0.14, 0.02, (0, 1.6, 0), STEEL, seg=20, tube=4)
    m.add(box_geo(0.6, 0.03, 0.4), SOLARC, M=Tr(0, 1.48, -0.42) @ Rot('x', 35))       # small solar panel
    m.cyl(0.01, 0.9, (0.2, 1.95, 0.15), DARK, seg=6)                                  # antenna
    m.box((0.06, 0.06, 0.02), (0.15, 1.36, 0.235), LED_A)
    decal(m, (0, 1.24, 0.233), 0.4, 0.12, lab)
    for k in range(5): m.cyl(0.035, 0.4, (0.75, 0.2 + k * 0.4, 0.4), WHITEP if k % 2 else LED_R, seg=8)   # marker pole
    return asset('RadiationStation', m, 'RAD')

# ---------------------------------------------------------------- rover garage
def build_garage():
    m = mk('GAR'); Rg, Lg = 2.7, 5.5
    prof = [(Rg * math.cos(math.radians(a)), Rg * math.sin(math.radians(a))) for a in np.linspace(0, 180, 17)]
    for k in range(len(prof) - 1):                                                    # arched shell panels along Z
        (x0, y0), (x1, y1) = prof[k], prof[k + 1]
        cxp, cyp = (x0 + x1) / 2, (y0 + y1) / 2; ln = math.hypot(x1 - x0, y1 - y0); ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
        m.box((ln + 0.02, 0.1, Lg), (cxp, cyp, 0), HULL, rot=(0, 0, ang))
    for z in np.linspace(-Lg / 2, Lg / 2, 5): m.torus(Rg + 0.05, 0.07, (0, 0, z), DARK, axis='z', seg=36, tube=5)   # ribs
    # closed back wall, open front with a frame
    for x in np.linspace(-Rg + 0.15, Rg - 0.15, 18):                               # closed back wall (vertical strips under the arch)
        hgt = math.sqrt(max(Rg * Rg - x * x, 0.0)); m.box((2 * Rg / 18 + 0.02, hgt, 0.1), (x, hgt / 2, -Lg / 2), HULL)
    m.torus(Rg + 0.06, 0.1, (0, 0, Lg / 2), YELLOW, axis='z', seg=36, tube=6)
    # regolith sandbag berm along both sides (radiation / micrometeoroid shielding)
    rng = np.random.default_rng(3)
    for sx in (-1, 1):
        for row in range(3):
            for k in range(9):
                z = -Lg / 2 + 0.3 + k * 0.62 + (0.31 if row % 2 else 0)
                if z > Lg / 2 - 0.2: continue
                x = sx * (Rg + 0.35 - row * 0.15)
                m.box((0.55, 0.32, 0.6), (x, 0.17 + row * 0.3, z), DUSTM, r=0.12, rot=(0, float(rng.uniform(-6, 6)), 0))
    m.box((Rg * 2 - 0.2, 0.05, Lg - 0.1), (0, 0.025, 0), STEEL)                       # floor pad
    m.box((Rg * 2 - 0.4, 0.02, 0.12), (0, 0.06, Lg / 2 - 0.15), YELLOW)
    # charging post with cable and status lights
    m.box((0.35, 1.2, 0.3), (-1.6, 0.6, -1.9), DARK, r=0.03)
    m.box((0.25, 0.2, 0.02), (-1.6, 1.0, -1.74), LED_G)
    m.between((-1.6, 0.9, -1.75), (-0.9, 0.4, -1.2), 0.03, BLACK, seg=8)
    m.cyl(0.06, 0.15, (-0.9, 0.4, -1.2), RAILY, seg=10)
    for x in (-1.0, 1.0): m.box((0.6, 0.04, 0.2), (x, Rg - 0.15, 0), LAMP)            # interior lights
    return asset('RoverGarage', m, 'GAR')

JOBS_NASA = [('SolarArrayVSAT', build_vsat), ('LunarBattery', build_battery), ('OxygenPlant', build_oxygen_plant),
             ('OxygenTank', lambda: build_tank('OxygenTank', 'O2', (0.15, 0.62, 0.30), 0.38, 1.3)),
             ('WaterTank', lambda: build_tank('WaterTank', 'H2O', (0.15, 0.40, 0.85), 0.5, 1.5)),
             ('LogisticsPallet', build_logistics), ('CommsTerminal', build_comms), ('RadiationStation', build_radiation), ('RoverGarage', build_garage)]

if __name__ == '__main__':
    out = sys.argv[1]; os.makedirs(out, exist_ok=True); os.makedirs('prev', exist_ok=True)
    for name, fn in JOBS_NASA:
        A = fn(); size = A.save(os.path.join(out, name + '.glb')); lo, hi = A.bounds()
        print(f'{name:18s} {A.tri_count():6d} tris {size // 1024:5d} KB  {lo.round(2)} {hi.round(2)}')
