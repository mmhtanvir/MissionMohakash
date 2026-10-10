from glbkit import *
from common import *
import numpy as np, math

# ------------------------------------------------------------ anim helpers
def spin(an, node, axis, T, n=12, deg=360.0, sign=1):
    ts = np.linspace(0, T, n + 1)
    an.add(node, 'rotation', ts, np.stack([qaxis(axis, sign * deg * k / n) for k in range(n + 1)]))

def rot_keys(an, node, axis, pairs):
    an.add(node, 'rotation', [t for t, _ in pairs], np.stack([qaxis(axis, d) for _, d in pairs]))

def rot_keys_q(an, node, pairs):
    an.add(node, 'rotation', [t for t, _ in pairs], np.stack([q for _, q in pairs]))

def pos_keys(an, node, pairs):
    an.add(node, 'translation', [t for t, _ in pairs], np.stack([np.array(v, float) for _, v in pairs]))

def scale_keys(an, node, pairs, interp='LINEAR'):
    an.add(node, 'scale', [t for t, _ in pairs], np.stack([np.array(v, float) for _, v in pairs]), interp)

def blink(an, node, T, period, on=0.35):
    pairs = []; t = 0.0
    while t < T - 1e-6:
        pairs += [(t, (1, 1, 1)), (t + on, (0.001, 0.001, 0.001))]; t += period
    pairs.append((T, (1, 1, 1)))
    scale_keys(an, node, pairs, 'STEP')

def openclose(A, node_rots, T=1.2, node_pos=None, node_scale=None):
    """node_rots: [(node, axis, closed_deg, open_deg)] -> adds 'Open' and 'Close' clips"""
    for nm, a, b in (('Open', 0, 1), ('Close', 1, 0)):
        an = Anim(nm)
        for node, ax, d0, d1 in node_rots:
            vals = (d0, d1)
            rot_keys(an, node, ax, [(0, vals[a]), (T, vals[b])])
        for node, p0, p1 in (node_pos or []):
            v = (p0, p1); pos_keys(an, node, [(0, v[a]), (T, v[b])])
        for node, s0, s1 in (node_scale or []):
            v = (s0, s1); scale_keys(an, node, [(0, v[a]), (T, v[b])])
        A.anims.append(an)

def sn(name, mesh=None, t=(0, 0, 0), r=None, sc=(1, 1, 1)):
    return Node(name, t=t, r=r, sc=sc, mesh=mesh)

def mk(name): return MeshB(name)

def flag(m, pos, h=1.4):
    m.cyl(.025, h, (pos[0], pos[1] + h / 2, pos[2]), STEEL, seg=8)
    m.box((.6, .4, .015), (pos[0] + .32, pos[1] + h - .22, pos[2]), GREEN_BD)
    m.cyl(.13, .018, (pos[0] + .3, pos[1] + h - .22, pos[2]), RED, axis='z', seg=18)
    m.sph(.035, (pos[0], pos[1] + h + .02, pos[2]), STEEL, seg=8, rings=6)

# ============================================================ HABITAT
def build_habitat():
    A = Asset('Habitat_HAB'); root = A.add(Node('HAB'))
    m = mk('HAB_Body'); root.add(sn('HAB_Body', m))
    # platform + saddles
    m.box((10.8, .4, 6.6), (0, .2, 0), CONCRETE, r=.05)
    for x in (-2.6, 2.6): m.box((.7, .6, 3.0), (x, .65, 0), HULL_GRAY, r=.08)
    # pressure hull (axis X)
    m.cyl(2.0, 5.2, (0, 2.6, 0), HULL, axis='x', seg=40)
    for x in (-2.5, 2.5): m.sph(2.0, (x, 2.6, 0), HULL, seg=40, rings=20)
    for x in (-2.4, -1.2, 0, 1.2, 2.4):
        m.cyl(2.045, .14, (x, 2.6, 0), HULL_GRAY, axis='x', seg=40)
    m.cyl(2.05, .5, (-1.8, 2.6, 0), ORANGE, axis='x', seg=40)
    # portholes
    for side in (1, -1):
        for x in (-2.7, -1.6, 2.3, 3.3):
            th = 28
            nrm = np.array([0, math.sin(math.radians(th)), side * math.cos(math.radians(th))])
            pos = np.array([x, 2.6, 0]) + nrm * 2.0
            rot = (-th, 0, 0) if side == 1 else (180 + th, 0, 0)
            m.cyl(.36, .12, pos, STEEL, axis='z', seg=20, rot=rot)
            m.cyl(.28, .14, pos + nrm * .01, GLASS, axis='z', seg=20, rot=rot)
    # roof hatch + vents + antenna
    m.box((1.2, .18, 1.2), (.8, 4.62, 0), HULL_GRAY, r=.04)
    m.cyl(.3, .08, (.8, 4.74, 0), STEEL, seg=16)
    for z in (-.8, .8): m.cyl(.18, .35, (-.6, 4.7, z), DARK, seg=14)
    m.cyl(.03, 1.6, (1.9, 5.4, -.5), STEEL, seg=8); m.sph(.05, (1.9, 6.2, -.5), LED_RED, seg=8, rings=6)
    flag(m, (-1.9, 4.55, .3))
    # entry vestibule (+Z side)
    m.box((2.4, 2.5, 1.5), (.7, 1.75, 2.35), HULL, r=.08)
    m.box((2.5, .14, 1.6), (.7, 3.05, 2.35), HULL_GRAY, r=.04)
    m.box((1.5, 2.1, .06), (.7, 1.55, 3.12), DARK, r=.01)          # door frame (dark)
    m.box((.14, 2.3, .2), (-.12, 1.55, 3.1), HULL_GRAY); m.box((.14, 2.3, .2), (1.52, 1.55, 3.1), HULL_GRAY)
    m.box((1.8, .15, .2), (.7, 2.7, 3.1), HULL_GRAY)
    m.box((.5, .12, .02), (.7, 2.9, 3.12), LED_GREEN)
    m.lathe([(0, 0), (.2, 0), (.2, .12), (0, .12)], (-1.0, 0, 3.7), YELLOW, seg=3) if False else None
    for i in range(3):
        m.box((1.8, .16, .5), (.7, .08 + 0 * i, 3.6 + i * .0), CONCRETE, r=.02) if i == 0 else None
    m.box((1.8, .22, .55), (.7, .11, 3.75), HULL_GRAY, r=.03)
    m.box((.1, .9, .1), (-.15, 3.3, 3.1), YELLOW)
    # lamps
    for x in (-.05, 1.45): m.box((.2, .12, .1), (x, 2.95, 3.13), LAMP)
    # piping along platform
    m.cyl(.09, 9.6, (0, .5, -3.0), STEEL, axis='x', seg=10); m.cyl(.09, 9.6, (0, .5, 3.0), STEEL, axis='x', seg=10) if False else None
    m.cyl(.09, 9.6, (0, .5, -2.9), STEEL, axis='x', seg=10)
    # door
    door = root.add(sn('Door', t=(0, 0, 3.13)))
    dm = mk('HAB_DoorMesh'); door.mesh = dm
    dm.box((1.3, 1.9, .08), (.7, 1.45, 0), HULL, r=.02)
    dm.box((.5, .5, .1), (.7, 1.9, 0), GLASS)
    dm.box((.5, .5, .09), (.7, 1.9, -.0), STEEL) if False else None
    dm.cyl(.05, .3, (1.2, 1.4, .1), STEEL, axis='z', seg=10)
    dm.box((1.3, .12, .09), (.7, .65, 0), YELLOW)
    # beacon
    bk = root.add(sn('Beacon', t=(.7, 3.12, 2.35)))
    bm = mk('HAB_BeaconMesh'); bk.mesh = bm
    bm.cyl(.14, .1, (0, .05, 0), DARK, seg=12); bm.box((.2, .12, .08), (.05, .17, 0), LED_AMBER)
    # animations
    openclose(A, [], 1.2, node_pos=[(door, (0, 0, 3.13), (1.4, 0, 3.13))])
    an = Anim('Operate'); spin(an, bk, 'y', 2.0); A.anims.append(an)
    return A

# ============================================================ AIRLOCK / SUIT STATION
def build_airlock():
    A = Asset('Airlock_SuitStation_SUI'); root = A.add(Node('SUI'))
    m = mk('SUI_Body'); root.add(sn('SUI_Body', m))
    m.box((7.6, .3, 4.2), (0, .15, 0), CONCRETE, r=.04)
    # airlock cylinder (axis Z)
    m.cyl(1.5, 2.8, (-1.6, 1.95, 0), HULL, axis='z', seg=36)
    for z in (-1.2, 0, 1.2): m.cyl(1.54, .12, (-1.6, 1.95, z), HULL_GRAY, axis='z', seg=36)
    m.sph(1.5, (-1.6, 1.95, -1.4), HULL, seg=32, rings=14)
    m.cyl(1.0, .3, (-1.6, 1.95, 1.4), HULL_GRAY, axis='z', seg=30)    # hatch collar
    m.torus(.95, .06, (-1.6, 1.95, 1.56), STEEL, axis='z') if False else None
    m.cyl(.9, .04, (-1.6, 1.95, 1.56), DARK, axis='z', seg=30)
    m.box((.5, .18, .06), (-1.6, 3.35, 1.1), LED_GREEN)
    m.box((.3, .3, .1), (-3.0, 2.2, 1.45), SUIT_GRAY, r=.02); m.box((.12, .12, .03), (-3.0, 2.2, 1.52), LED_GREEN)
    m.cyl(.2, 1.0, (-2.9, .8, 1.1), TANK_BODY, seg=14); m.cyl(.2, 1.0, (-2.9, .8, .6), TANK_BODY, seg=14) if False else None
    m.box((1.4, .12, 1.0), (-1.6, .36, 2.1), HULL_GRAY, r=.02)   # step
    # suit alcove (+X)
    m.box((3.6, .12, 3.2), (3.0, 3.2, 0), HULL_GRAY, r=.03)          # roof
    m.box((.12, 3.0, 3.2), (4.8, 1.7, 0), HULL, r=.02)               # right wall
    m.box((3.6, 3.0, .12), (3.0, 1.7, -1.56), HULL, r=.02)           # back wall
    m.box((.12, 3.0, 3.2), (1.2, 1.7, 0), HULL, r=.02)               # left wall
    m.box((3.0, .06, .5), (3.0, 3.0, -1.2), LAMP)
    m.box((3.4, .1, 1.2), (3.0, .35, -.95), HULL_GRAY)               # bench
    for x in (2.3, 3.7):
        m.cyl(.03, 1.0, (x, 2.2, -1.5), STEEL, seg=8, axis='y')
    # status board
    m.box((.9, .55, .06), (3.0, 2.9, -1.48), DARK, r=.01); m.box((.7, .3, .02), (3.0, 2.9, -1.45), SCREEN)
    # suits hanging
    for x in (2.3, 3.7):
        sm = MeshB('dummy'); sm.base = T(x, .38, -.75)
        from astronaut import add_suit
        add_suit(sm, lambda b: None)
        for mat, g, j in sm.items: m.items.append((mat, g, None))
    m.cyl(.3, .08, (4.55, 1.2, .8), RUBBER, axis='x') if False else None
    # door (hinged on -X side of hatch)
    pivot = root.add(sn('HatchPivot', t=(-2.5, 1.95, 1.62)))
    hm = mk('SUI_HatchMesh'); pivot.mesh = hm
    hm.cyl(.86, .14, (.9, 0, 0), HULL, axis='z', seg=32)
    hm.cyl(.45, .16, (.9, .25, 0), GLASS, axis='z', seg=24)
    hm.torus(.55, .045, (.9, -.15, .1), STEEL, axis='z', seg=24)
    for a in (0, 60, 120): hm.cyl(.03, 1.1, (.9, -.15, .1), STEEL, rot=(0, 0, a), seg=8)
    hm.cyl(.06, .12, (.9, -.15, .1), DARK, axis='z', seg=12)
    openclose(A, [(pivot, 'y', 0, -105)], 1.4)
    return A

# ============================================================ OXYGEN GENERATOR
def build_ogs():
    A = Asset('OxygenGenerator_OGS'); root = A.add(Node('OGS'))
    m = mk('OGS_Body'); root.add(sn('OGS_Body', m))
    m.box((4.2, .25, 3.0), (0, .125, 0), CONCRETE, r=.03)
    m.box((2.4, 1.9, 1.7), (-.5, 1.2, -.2), HULL, r=.08)               # cabinet
    m.box((2.0, .08, 1.3), (-.5, 2.19, -.2), HULL_GRAY)
    m.box((1.2, .7, .08), (-.5, 1.4, .69), DARK, r=.015)                # control panel
    m.box((.8, .3, .02), (-.7, 1.5, .74), SCREEN); m.box((.28, .3, .02), (-.1, 1.5, .74), LED_GREEN)
    for i in range(4): m.box((.12, .06, .02), (-.95 + i * .2, 1.2, .74), (LED_AMBER, LED_BLUE, LED_GREEN, LED_RED)[i])
    for i in range(6): m.box((1.0, .035, .04), (-.5, .55 + i * .08, .68), DARK)       # vent slats
    # O2 cylinders
    for x in (1.35, 1.95):
        m.cyl(.32, 2.6, (x, 1.55, -.3), O2_BLUE, seg=24); m.sph(.32, (x, 2.85, -.3), O2_BLUE, sc=(1, .6, 1), seg=24, rings=8)
        m.cyl(.325, .08, (x, 1.0, -.3), STEEL, seg=24); m.cyl(.325, .08, (x, 2.1, -.3), STEEL, seg=24)
        m.cyl(.06, .2, (x, 3.12, -.3), STEEL, seg=10)
    m.box((1.5, .08, .08), (1.65, 3.2, -.3), STEEL)
    # water feed tank (horizontal)
    m.cyl(.3, 1.6, (-.5, 2.62, -.2), H2O_TEAL, axis='x', seg=24)
    for x in (-1.05, .05): m.sph(.3, (x + (-.2 if x < 0 else .2), 2.62, -.2), H2O_TEAL, sc=(.5, 1, 1), seg=16, rings=8) if False else None
    m.sph(.3, (-1.3, 2.62, -.2), H2O_TEAL, seg=16, rings=8); m.sph(.3, (.3, 2.62, -.2), H2O_TEAL, seg=16, rings=8)
    for x in (-1.0, -.1): m.box((.08, .1, .8), (x, 2.38, -.2), STEEL)
    # pipes
    m.between((.7, 2.4, -.3), (1.35, 3.0, -.3), .05, STEEL)
    m.cyl(.06, 1.4, (1.65, .35, .3), STEEL, axis='x', seg=10); m.cyl(.06, .8, (1.35, .6, .0), STEEL, axis='z', seg=10)
    m.box((.35, .35, .12), (1.65, 1.0, .55), YELLOW, r=.02)
    # fan housing + fan
    m.cyl(.45, .15, (-1.1, 2.3, -.25), DARK, seg=20)
    fan = root.add(sn('Fan', t=(-1.1, 2.4, -.25)))
    fm = mk('OGS_FanMesh'); fan.mesh = fm
    for k in range(4): fm.box((.7, .02, .18), (0, 0, 0), STEEL, rot=(0, k * 45, 0)) if k < 2 else None
    for k in range(3): fm.box((.8, .015, .16), (0, 0, 0), STEEL, rot=(0, k * 60, 6))
    fm.cyl(.07, .04, (0, 0, 0), DARK, seg=10)
    led = root.add(sn('StatusLED', t=(-1.5, 2.12, .72))); lm = mk('OGS_LED'); led.mesh = lm
    lm.sph(.05, (0, 0, 0), LED_GREEN, seg=10, rings=6)
    an = Anim('Operate'); spin(an, fan, 'y', 0.6, 12); blink(an, led, 0.6, 0.6, .3) if False else None
    blink(an, led, 0.6, 0.6, .3) if False else None
    A.anims.append(an)
    return A

# ============================================================ STORAGE MODULE
def build_storage():
    A = Asset('StorageModule_STO'); root = A.add(Node('STO'))
    m = mk('STO_Body'); root.add(sn('STO_Body', m))
    m.box((3.0, .25, 6.4), (0, .125, 0), CONCRETE, r=.03)
    w, h, d, t = 1.3, 2.6, 3.0, .1; y0 = .25
    m.box((2 * w, t, 2 * d), (0, y0 + t / 2, 0), HULL_GRAY)                    # floor
    m.box((2 * w, t, 2 * d), (0, y0 + h - t / 2, 0), HULL, r=.03)               # roof
    m.box((t, h, 2 * d), (-w + t / 2, y0 + h / 2, 0), HULL); m.box((t, h, 2 * d), (w - t / 2, y0 + h / 2, 0), HULL)
    m.box((2 * w, h, t), (0, y0 + h / 2, -d + t / 2), HULL)
    for z in np.arange(-2.7, 2.71, .3):
        for sx in (-1, 1): m.box((.05, 2.2, .1), (sx * (w + .02), y0 + h / 2, z), HULL_GRAY)
    for x in np.arange(-1.0, 1.01, .5): m.box((.35, .05, 5.6), (x, y0 + h + .02, 0), HULL_GRAY) if False else None
    for sx in (-1, 1):
        for sz in (-1, 1):
            for yy in (y0 + .1, y0 + h - .1): m.box((.22, .22, .22), (sx * (w - .02), yy, sz * (d - .02)), DARK)
    m.box((2.2, .2, .02), (0, y0 + h - .35, -d - .0), ORANGE)
    # interior
    m.box((.1, .05, 4.6), (-1.0, y0 + 1.0, -.2), DARK)
    for yy in (.9, 1.6, 2.3): m.box((.5, .05, 4.6), (-1.0, y0 + yy - .3, -.2), STEEL)
    for i, z in enumerate((-2.3, -1.2, -.1, 1.0)):
        m.box((.45, .35, .8), (-1.0, y0 + .6 + .05, z), (MIL_GREEN, ORANGE, MIL_GREEN, STEEL)[i], r=.02)
        m.box((.4, .3, .7), (-1.0, y0 + 1.15, z), (STEEL, MIL_GREEN, ORANGE, MIL_GREEN)[i], r=.02)
    m.box((1.0, .7, 1.0), (.5, y0 + .45, -1.9), MIL_GREEN, r=.03); m.box((.8, .5, .8), (.5, y0 + 1.05, -1.9), ORANGE, r=.03)
    m.box((1.8, .06, .3), (0, y0 + h - .15, 0), LAMP)
    # doors
    dl = root.add(sn('DoorR', t=(w, y0 + h / 2, d + .04))); dr = root.add(sn('DoorL', t=(-w, y0 + h / 2, d + .04)))
    for node, sx in ((dl, -1), (dr, 1)):
        dm = mk(node.name + 'Mesh'); node.mesh = dm
        dm.box((1.26, h - .06, .08), (sx * .63, 0, 0), HULL, r=.02)
        for z in np.arange(-.9, 1.0, .3): dm.box((.05, h - .5, .03), (sx * (.15 + .3 * (z + .9) / .3 * .15 + .1) if False else sx * (.2 + (z + .9) * .45), 0, .06), HULL_GRAY) if False else None
        dm.cyl(.03, 2.0, (sx * .2, 0, .09), STEEL, seg=8); dm.cyl(.03, 2.0, (sx * .35, 0, .09), STEEL, seg=8)
        dm.box((.14, .06, .06), (sx * .27, .1, .1), DARK)
    openclose(A, [(dl, 'y', 0, 105), (dr, 'y', 0, -105)], 1.5)
    return A

# ============================================================ GARAGE
def build_garage():
    A = Asset('Garage_GAR'); root = A.add(Node('GAR'))
    m = mk('GAR_Body'); root.add(sn('GAR_Body', m))
    W, D, H, t = 3.5, 4.0, 4.0, .2
    m.box((2 * W + 1, .2, 2 * D + 1.5), (0, .1, .6), CONCRETE, r=.03)        # pad
    m.box((2 * W, .12, 2 * D), (0, .26, 0), HULL_GRAY)                        # floor
    m.box((t, H, 2 * D), (-W + t / 2, H / 2 + .2, 0), HULL); m.box((t, H, 2 * D), (W - t / 2, H / 2 + .2, 0), HULL)
    m.box((2 * W, H, t), (0, H / 2 + .2, -D + t / 2), HULL)
    m.box((2 * W + .3, .22, 2 * D + .3), (0, H + .3, 0), HULL_GRAY, r=.04)    # roof
    # front wall with opening 4.6 x 3.2
    ow, oh = 2.3, 3.2
    m.box((W - ow, H, t), (-(W + ow) / 2, H / 2 + .2, D - t / 2), HULL)
    m.box((W - ow, H, t), ((W + ow) / 2, H / 2 + .2, D - t / 2), HULL)
    m.box((2 * ow, H - oh - .2 + .0, t), (0, oh + .2 + (H - oh - .2) / 2, D - t / 2), HULL)
    m.box((2 * ow + .3, .2, .3), (0, oh + .3, D + .05), STEEL, r=.03)
    for sx in (-1, 1): m.box((.2, oh + .1, .3), (sx * (ow + .05), .2 + (oh + .1) / 2, D + .05), HULL_GRAY, r=.02)
    m.cyl(.22, 2 * ow, (0, oh + .1, D - .35), DARK, axis='x', seg=16)           # roll drum
    # wall ribs
    for z in np.arange(-3.4, 3.5, 1.1):
        for sx in (-1, 1): m.box((.08, H - .2, .16), (sx * (W + .03), H / 2 + .2, z), HULL_GRAY)
    # windows on sides
    for sx in (-1, 1):
        for z in (-1.8, 1.8): m.box((.06, .8, 1.2), (sx * (W + .02), 2.7, z), GLASS)
    # hazard stripe floor edge
    for i in range(10): m.box((.5, .02, .5), (-2.25 + i * .5, .33, D - .6 if False else 3.4), YELLOW if i % 2 == 0 else DARK)
    # roof gear
    m.box((1.6, .6, 1.2), (-1.8, H + .7, -1.5), DARK, r=.05); m.cyl(.4, .1, (-1.8, H + 1.05, -1.5), STEEL, seg=18)
    m.box((1.4, .12, 1.0), (1.4, H + .46, 1.0), GLASS)
    m.cyl(.04, 2.0, (2.8, H + 1.4, -3.0), STEEL, seg=8); m.sph(.08, (2.8, H + 2.4, -3.0), LED_RED, seg=8, rings=6)
    # interior
    for x in (-2.0, 0, 2.0): m.box((1.4, .06, .4), (x, H - .05, 0), LAMP)
    m.box((.1, 1.6, 4.0), (-3.2, 1.2, -1.5), HULL_GRAY) if False else None
    for yy in (1.0, 1.9, 2.8): m.box((3.2, .06, .5), (0, yy, -D + .5), STEEL)
    for i, x in enumerate((-1.2, -.3, .8, 1.5)):
        m.box((.5, .4, .35), (x, 1.25, -D + .5), (MIL_GREEN, ORANGE, STEEL, MIL_GREEN)[i], r=.02)
    m.box((.6, 1.5, .5), (-2.8, 1.0, 1.0), DARK, r=.04); m.box((.4, .3, .02), (-2.8, 1.5, 1.26), SCREEN)   # charger
    m.cyl(.04, 2.0, (-2.8, .6, 1.5), YELLOW, axis='z', seg=8) if False else None
    m.box((.2, .2, .1), (3.0, 3.2, D - .1), LED_AMBER)
    # door (origin at top of opening, slats hang below; scales to coil when opening)
    door = root.add(sn('RollDoor', t=(0, oh + .2, D - .55)))
    dm = mk('GAR_DoorMesh'); door.mesh = dm
    n = 16; sl = oh / n
    for k in range(n):
        dm.box((2 * ow - .05, sl * .92, .08), (0, -sl / 2 - k * sl, 0), HULL_GRAY if k % 2 else HULL, r=.01)
    dm.box((2 * ow - .05, .18, .1), (0, -oh + .09, 0), YELLOW)
    openclose(A, [], 2.0, node_scale=[(door, (1, 1, 1), (1, .06, 1))])
    return A

# ============================================================ COMMS ANTENNA
def build_comms():
    A = Asset('CommsAntenna_COM'); root = A.add(Node('COM'))
    m = mk('COM_Tower'); root.add(sn('COM_Tower', m))
    m.cyl(1.8, .3, (0, .15, 0), CONCRETE, seg=24)
    m.box((.8, .9, .6), (1.15, .75, .6), DARK, r=.04); m.box((.5, .2, .02), (1.15, .95, .91), SCREEN)    # equipment box
    top = 8.0; r0, r1 = 1.1, .35
    ang = [math.radians(a) for a in (90, 210, 330)]
    def leg(a, y):
        r = r0 + (r1 - r0) * y / top; return np.array([r * math.cos(a), y, r * math.sin(a)])
    for a in ang: m.between(leg(a, .3), leg(a, top), .06, STEEL, seg=8)
    levels = np.arange(.3, top + .01, 1.0)
    for k, y in enumerate(levels):
        for i in range(3):
            m.between(leg(ang[i], y), leg(ang[(i + 1) % 3], y), .035, STEEL, seg=6)
        if k + 1 < len(levels):
            y2 = levels[k + 1]
            for i in range(3):
                j = (i + 1) % 3
                if k % 2 == 0: m.between(leg(ang[i], y), leg(ang[j], y2), .03, HULL_GRAY, seg=6)
                else: m.between(leg(ang[j], y), leg(ang[i], y2), .03, HULL_GRAY, seg=6)
    m.cyl(.5, .14, (0, top + .07, 0), HULL_GRAY, seg=20)
    m.cyl(.1, .5, (0, top + .39, 0), STEEL, seg=12) if False else None
    m.cyl(.025, 1.8, (.3, top + .95, .2), STEEL, seg=8)
    bk = root.add(sn('Beacon', t=(-.3, top + .25, -.1))); bm = mk('COM_Beacon'); bk.mesh = bm
    bm.sph(.09, (0, 0, 0), LED_RED, seg=10, rings=6)
    yaw = root.add(sn('DishYaw', t=(0, top + .14, 0))); ym = mk('COM_DishBase'); yaw.mesh = ym
    ym.cyl(.14, .55, (0, .275, 0), STEEL, seg=14); ym.box((.5, .3, .4), (0, .1, 0), DARK, r=.04)
    pitch = yaw.add(sn('DishPitch', t=(0, .6, 0), r=qaxis('x', -35)))
    pm = mk('COM_Dish'); pitch.mesh = pm
    Rr = 1.6; f = .9
    rs = np.linspace(0, Rr, 9)
    prof = [(r, r * r / (4 * f) - .05) for r in rs] + [(Rr, Rr * Rr / (4 * f))] + [(r, r * r / (4 * f)) for r in rs[::-1]]
    pm.lathe(prof, (0, 0, 0), HULL, seg=40, rot=(90, 0, 0))
    pm.cyl(.2, .35, (0, -.05, -.1), DARK, axis='z', seg=14)
    for a in (0, 120, 240):
        p = np.array([Rr * .95 * math.cos(math.radians(a)), Rr * .95 * math.sin(math.radians(a)), (Rr * .95) ** 2 / (4 * f)])
        pm.between(p, (0, 0, f), .02, STEEL, seg=6)
    pm.cyl(.07, .2, (0, 0, f), DARK, axis='z', seg=12); pm.sph(.06, (0, 0, f + .12), LED_BLUE, seg=10, rings=6)
    an = Anim('Operate'); T_ = 12.0
    rot_keys(an, yaw, 'y', [(0, 0), (3, 45), (9, -45), (12, 0)])
    rot_keys(an, pitch, 'x', [(0, -35), (6, -28), (12, -35)])
    blink(an, bk, T_, 2.0, .3)
    A.anims.append(an)
    return A

# ============================================================ RADIATION MONITOR
def build_radiation():
    A = Asset('RadiationMonitor_RAD'); root = A.add(Node('RAD'))
    m = mk('RAD_Body'); root.add(sn('RAD_Body', m))
    m.cyl(.55, .12, (0, .06, 0), CONCRETE, seg=20)
    for a in (0, 120, 240):
        p = np.array([.5 * math.cos(math.radians(a)), .1, .5 * math.sin(math.radians(a))])
        m.between(p, (0, 1.3, 0), .025, STEEL, seg=6)
    m.cyl(.07, 1.7, (0, .95, 0), STEEL, seg=12)
    m.box((.8, .62, .35), (0, 2.1, 0), HULL, r=.05)
    m.box((.7, .52, .02), (0, 2.1, .18), DARK)
    # trefoil on front
    for k in range(3):
        a0 = 90 + k * 120 - 30
        pts = [(0, 0)] + [(.17 * math.cos(math.radians(a0 + i * 10)), .17 * math.sin(math.radians(a0 + i * 10))) for i in range(7)]
        m.extrude(pts, .02, (0, 2.1, .2), YELLOW)
    m.cyl(.04, .03, (0, 2.1, .21), DARK, axis='z', seg=12) if False else None
    m.cyl(.045, .03, (0, 2.1, .205), RUBBER, axis='z', seg=12)
    m.box((.22, .1, .02), (.25, 1.9, .2), LED_GREEN) if False else None
    m.box((.55, .04, .02), (0, 1.88, .19), SCREEN)
    m.cyl(.03, .5, (.3, 2.65, 0), STEEL, seg=8)
    m.cyl(.16, .18, (0, 2.5, 0), HULL_GRAY, seg=18)
    m.lathe([(0, 0), (.2, 0), (.2, .02), (.19, .12), (.14, .26), (.07, .34), (0, .36)], (0, 2.6, 0), GLASS, seg=20) if False else None
    m.box((.5, .03, .35), (-.5, 1.9, 0), SOLAR, rot=(0, 0, -35)) if False else None
    m.box((.55, .04, .4), (.6, 1.5, 0), SOLAR, rot=(0, 0, 30)); m.box((.04, .5, .04), (.45, 1.25, 0), STEEL) if False else None
    m.between((.38, 1.28, 0), (.5, 1.52, 0), .02, STEEL, seg=6)
    m.between((0, 1.35, 0), (.4, 1.3, 0), .02, STEEL, seg=6)
    sens = root.add(sn('SensorHead', t=(0, 2.69, 0))); smm = mk('RAD_Sensor'); sens.mesh = smm
    smm.cyl(.03, .12, (0, .06, 0), STEEL, seg=8); smm.box((.34, .05, .06), (0, .14, 0), DARK)
    smm.cyl(.05, .04, (.15, .17, 0), LED_AMBER, seg=10); smm.cyl(.05, .04, (-.15, .17, 0), YELLOW, seg=10)
    dome = root.add(sn('SensorDome', t=(0, 2.59, 0))); dm = mk('RAD_Dome'); dome.mesh = dm
    dm.lathe([(0, 0)] + [(.2 * math.cos(math.radians(a)), .26 * math.sin(math.radians(a))) for a in range(0, 91, 15)], (0, 0, 0), GLASS, seg=20, smooth_deg=89)
    lamp = root.add(sn('WarnLight', t=(0, 2.85, .0))); lm = mk('RAD_Warn'); lamp.mesh = lm
    lm.sph(.05, (.25, .0, 0), LED_AMBER, seg=10, rings=6) if False else None
    lm.sph(.05, (0, 0, 0), LED_RED, seg=10, rings=6); lamp.t = np.array([-.3, 2.95, 0])
    an = Anim('Operate'); spin(an, sens, 'y', 4.0, 16); blink(an, lamp, 4.0, 1.0, .3); A.anims.append(an)
    return A
