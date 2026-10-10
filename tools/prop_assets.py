from glbkit import *
from common import *
from base_assets import *
import numpy as np, math

# ============================================================ SOLAR ARRAY
def build_solar():
    A = Asset('SolarArray_SOL-S'); root = A.add(Node('SOL_S'))
    m = mk('SOL_Base'); root.add(sn('SOL_Base', m))
    m.box((2.0, .15, 1.6), (0, .075, 0), CONCRETE, r=.03)
    m.cyl(.13, 1.5, (0, .85, 0), STEEL, seg=14)
    m.box((.5, .45, .3), (.9, .3, .3), DARK, r=.03); m.box((.2, .05, .02), (.9, .4, .46), LED_GREEN)
    m.cyl(.03, 1.0, (.5, .1, .4), RUBBER, axis='x', seg=6)
    pivot = root.add(sn('PanelPivot', t=(0, 1.6, 0)))
    pm = mk('SOL_Panels'); pivot.mesh = pm
    pm.cyl(.17, .2, (0, 0, 0), HULL_GRAY, seg=14)
    tilt = RE(30, 0, 0)
    pm.base = tilt
    pm.box((6.7, .1, .12), (0, -.0, 0), STEEL)                       # main torque beam
    for row, zc in enumerate((-1.2, 1.2)):
        pm.box((6.6, .08, .1), (0, 0, zc - 1.1 + 2.2 * (0 if False else 0)), STEEL) if False else None
        for col in range(3):
            xc = (col - 1) * 2.15
            pm.box((2.05, .06, 2.15), (xc, .06, zc), STEEL, r=.015)
            pm.box((1.95, .05, 2.05), (xc, .09, zc), SOLAR, r=.01)
            for i in range(1, 4): pm.box((.02, .06, 2.0), (xc - .97 + i * .485, .09, zc), SOLAR_LN)
            for j in range(1, 6): pm.box((1.92, .06, .015), (xc, .09, zc - 1.0 + j * .333), SOLAR_LN)
    pm.box((.14, .14, 5.0), (-2.15, -.1, 0), STEEL); pm.box((.14, .14, 5.0), (2.15, -.1, 0), STEEL)
    an = Anim('Track'); T_ = 16.0
    rot_keys(an, pivot, 'y', [(0, -25), (8, 25), (16, -25)]); A.anims.append(an)
    return A

# ============================================================ SCOUT ROVER
def build_rover():
    A = Asset('ScoutRover_ROV-SC'); root = A.add(Node('ROV_SC'))
    m = mk('ROV_Body'); root.add(sn('ROV_Body', m))
    m.box((1.1, .3, 2.0), (0, .82, 0), HULL, r=.07)                       # chassis
    m.box((.9, .28, .9), (0, 1.1, .35), HULL, r=.07)                      # cabin
    m.box((.8, .12, .6), (0, 1.12, .45), GLASS) if False else None
    m.box((.7, .2, .04), (0, 1.12, .8), GLASS)
    m.box((1.0, .05, .9), (0, 1.27, -.6), SOLAR, rot=(-12, 0, 0)) ; m.box((1.1, .03, 1.0), (0, 1.24, -.6), STEEL, rot=(-12, 0, 0))
    m.box((1.0, .22, .5), (0, .98, -.95), DARK, r=.04)                  # cargo bin
    m.box((.14, .14, .08), (-.35, .85, 1.02), LAMP); m.box((.14, .14, .08), (.35, .85, 1.02), LAMP)
    m.box((.1, .07, .05), (-.5, .98, 1.0), LED_AMBER); m.box((.1, .07, .05), (.5, .98, 1.0), LED_AMBER)
    m.box((.1, .07, .05), (-.4, .86, -1.02), LED_RED); m.box((.1, .07, .05), (.4, .86, -1.02), LED_RED)
    m.cyl(.015, .9, (.4, 1.75, -1.0), STEEL, seg=6); m.sph(.025, (.4, 2.2, -1.0), LED_RED, seg=6, rings=4)
    m.box((.18, .22, .1), (-.4, 1.35, -.05), DARK, r=.02)
    # little robot arm folded at front
    m.cyl(.05, .12, (0, .7, 1.0), DARK, axis='x', seg=10); m.between((0, .72, 1.0), (0, .62, 1.3), .03, STEEL, seg=8)
    m.between((0, .62, 1.3), (0, .72, 1.12), .025, STEEL, seg=8); m.box((.06, .06, .1), (0, .72, 1.1), ORANGE)
    # mast + camera head
    m.cyl(.035, .65, (.3, 1.6, .55), STEEL, seg=8)
    mast = root.add(sn('CameraHead', t=(.3, 1.95, .55))); cm = mk('ROV_Camera'); mast.mesh = cm
    cm.box((.22, .14, .12), (0, .07, 0), DARK, r=.02)
    for x in (-.06, .06): cm.cyl(.03, .05, (x, .07, .09), LED_BLUE, axis='z', seg=10)
    cm.box((.08, .03, .08), (0, .16, 0), STEEL)
    # suspension (rocker-bogie style simple)
    wheels = {}
    zs = {'F': .78, 'M': 0, 'R': -.78}
    for side, sx in (('L', 1), ('R', -1)):
        m.between((sx * .55, .82, .9), (sx * .78, .37, zs['F']), .035, STEEL, seg=8)
        m.between((sx * .55, .82, .0), (sx * .78, .37, zs['M']), .035, STEEL, seg=8)
        m.between((sx * .55, .82, -.9), (sx * .78, .37, zs['R']), .035, STEEL, seg=8)
        m.box((.05, .05, 1.7), (sx * .8, .62, 0), STEEL) if False else None
        for k in 'FMR':
            piv = root.add(sn(f'Steer_{k}{side}', t=(sx * .78, .37, zs[k])))
            piv_m = mk(f'Strut_{k}{side}'); piv.mesh = piv_m
            piv_m.box((.06, .3, .06), (sx * .1, .0, 0), STEEL) if False else None
            w = piv.add(sn(f'Wheel_{k}{side}'))
            wm = mk(f'WheelMesh_{k}{side}'); w.mesh = wm
            wm.cyl(.45 - .13, .3, (0, 0, 0), RUBBER, axis='x', seg=24, r2=.32) if False else None
            wm.cyl(.32, .24, (0, 0, 0), RUBBER, axis='x', seg=24)
            wm.cyl(.2, .26, (sx * .0, 0, 0), STEEL, axis='x', seg=16)
            wm.cyl(.08, .3, (sx * .03, 0, 0), DARK, axis='x', seg=10)
            for i in range(16):
                a = i * 22.5
                wm.box((.26, .05, .07), (0, .34 * math.cos(math.radians(a)), .34 * math.sin(math.radians(a))), DARK,
                       rot=(a - 90 + 90 if False else a, 0, 0))
            wheels[k + side] = w
    an = Anim('Drive')
    for k, w in wheels.items(): spin(an, w, 'x', 1.0, 12)
    A.anims.append(an)
    an2 = Anim('Operate'); rot_keys(an2, mast, 'y', [(0, 0), (3, 70), (9, -70), (12, 0)]); A.anims.append(an2)
    return A

# ============================================================ LANDER
def build_lander():
    A = Asset('Lander'); root = A.add(Node('Lander'))
    m = mk('Lander_Body'); root.add(sn('Lander_Body', m))
    # descent stage (octagonal, foil wrapped)
    m.cyl(1.9, 1.2, (0, 2.1, 0), GOLD_FOIL, seg=8, rot=(0, 22.5, 0))
    m.cyl(1.95, .12, (0, 2.75, 0), HULL_GRAY, seg=8, rot=(0, 22.5, 0)); m.cyl(1.95, .12, (0, 1.45, 0), HULL_GRAY, seg=8, rot=(0, 22.5, 0))
    for a in range(0, 360, 45):
        r = math.radians(a)
        m.box((.5, 1.0, .1), (1.78 * math.cos(r), 2.1, 1.78 * math.sin(r)), HULL_GRAY, rot=(0, -a + 90, 0)) if a % 90 == 45 else None
    # engine bell
    m.lathe([(.25, 0), (.35, .05), (.55, .5), (.6, .55), (.5, .55), (.3, .12), (.2, .0)], (0, .85, 0), DARK, seg=24)
    m.cyl(.3, .4, (0, 1.4, 0), STEEL, seg=14)
    # ascent stage
    m.box((2.2, 1.5, 2.0), (0, 3.55, 0), HULL, r=.18)
    m.cyl(1.0, .2, (0, 4.4, 0), HULL_GRAY, seg=16)
    m.box((.6, .5, .08), (-.55, 3.75, 1.02), GLASS, rot=(0, 0, 18)); m.box((.6, .5, .08), (.55, 3.75, 1.02), GLASS, rot=(0, 0, -18))
    m.box((.8, .9, .4), (0, 3.4, 1.15), HULL_GRAY, r=.06); m.box((.55, .75, .06), (0, 3.4, 1.37), DARK, r=.02)   # hatch
    m.box((.3, .06, .02), (0, 3.9, 1.38), LED_GREEN)
    for sx in (-1, 1):
        m.box((.6, .6, .5), (sx * 1.35, 3.5, .1), HULL_GRAY, r=.05); m.cyl(.1, .3, (sx * 1.7, 3.5, .1), DARK, axis='x', seg=10)
    m.cyl(.03, 1.2, (-.6, 5.0, -.6), STEEL, seg=8); m.sph(.04, (-.6, 5.6, -.6), LED_RED, seg=8, rings=5)
    m.cyl(.45, .08, (.6, 4.8, -.5), STEEL, axis='y', seg=16, rot=(-30, 0, 0)); m.cyl(.04, .55, (.6, 4.65, -.5), STEEL, seg=8)
    # solar wing stowed panels
    m.box((.06, 1.2, 1.3), (-1.15, 3.6, -1.1), SOLAR) if False else None
    # landing legs
    foot_r = 3.4; hy = 2.0
    for a in range(0, 360, 90):
        r = math.radians(a); c, s_ = math.cos(r), math.sin(r)
        top = np.array([1.4 * c, hy + .3, 1.4 * s_]); foot = np.array([foot_r * c, .2, foot_r * s_])
        m.between((1.9 * c, hy + .5, 1.9 * s_) if False else top, foot, .09, STEEL, seg=10)
        sec = np.array([.7 * c, hy - .45, .7 * s_]) if False else np.array([1.1 * c, hy - .4, 1.1 * s_])
        m.between(sec, (foot + (top - foot) * .45), .06, HULL_GRAY, seg=8)
        m.cyl(.5, .1, (foot_r * c, .05, foot_r * s_), HULL_GRAY, seg=18); m.sph(.12, (foot_r * c, .15, foot_r * s_), STEEL, seg=12, rings=6)
    # ladder on +Z leg
    for sx in (-.22, .22): m.between((sx, 2.35, 2.0), (sx, .2, 3.35), .03, STEEL, seg=8)
    for i in range(9):
        t = (i + .5) / 9; p = np.array([0, 2.35, 2.0]) * (1 - t) + np.array([0, .2, 3.35]) * t
        m.cyl(.025, .46, p, STEEL, axis='x', seg=8)
    m.box((.8, .08, .7), (0, 2.4, 2.15), HULL_GRAY)                    # porch
    m.box((.8, .8, .08), (0, 2.85, 2.5), STEEL) if False else None
    for sx in (-.38, .38): m.between((sx, 2.4, 2.45), (sx, 2.9, 2.45), .025, STEEL, seg=6)
    m.between((-.38, 2.9, 2.45), (.38, 2.9, 2.45), .025, STEEL, seg=6)
    m.box((.4, .2, .25), (-1.2, 2.4, -1.4), DARK, r=.03); m.box((.1, .08, .02), (-1.2, 2.5, -1.27), LED_GREEN)
    return A

# ============================================================ BATTERY
def build_battery():
    A = Asset('Battery_BAT'); root = A.add(Node('BAT'))
    m = mk('BAT_Body'); root.add(sn('BAT_Body', m))
    m.box((.40, .26, .20), (0, .13, 0), HULL_GRAY, r=.035)
    for sx in (-1, 1): m.box((.05, .27, .21), (sx * .18, .135, 0), ORANGE, r=.012)
    m.box((.26, .14, .02), (0, .12, .105), DARK)
    for i in range(4): m.box((.04, .06, .012), (-.075 + i * .05, .13, .113), LED_GREEN)
    m.box((.12, .03, .012), (0, .06, .113), YELLOW)
    for sx in (-1, 1): m.cyl(.015, .12, (sx * .1, .31, 0), STEEL, seg=8) if False else None
    for sx, mat in ((-1, RED), (1, DARK)):
        m.cyl(.03, .05, (sx * .1, .285, -.04), STEEL, seg=12); m.cyl(.022, .03, (sx * .1, .31, -.04), mat, seg=10)
    m.cyl(.012, .26, (0, .42, 0), STEEL, axis='x', seg=8)
    for sx in (-1, 1): m.cyl(.012, .15, (sx * .13, .345, 0), STEEL, seg=8)
    m.box((.22, .02, .03), (0, .01, 0), RUBBER) if False else None
    return A

# ============================================================ SEISMOMETER CRATE
def build_seis_crate():
    A = Asset('SeismometerCrate'); root = A.add(Node('SeisCrate'))
    m = mk('Crate_Body'); root.add(sn('Crate_Body', m))
    W, D, H, t = .8, .56, .36, .03
    m.box((W, t, D), (0, t / 2, 0), MIL_GREEN, r=.01)
    for sx in (-1, 1): m.box((t, H, D), (sx * (W / 2 - t / 2), H / 2, 0), MIL_GREEN, r=.01)
    for sz in (-1, 1): m.box((W, H, t), (0, H / 2, sz * (D / 2 - t / 2)), MIL_GREEN, r=.01)
    m.box((W - .08, .15, D - .08), (0, .11, 0), DARK, r=.01)          # foam
    m.sph(.13, (0, .185, 0), GOLD_FOIL, sc=(1, .7, 1), seg=18, rings=8); m.cyl(.16, .02, (0, .185, 0), STEEL, seg=18)
    m.box((.3, .02, .02), (0, .2, -.2), LED_GREEN) if False else None
    for sx in (-1, 1):
        for sz in (-1, 1): m.box((.06, .06, .06), (sx * (W / 2 - .02), .03, sz * (D / 2 - .02)), STEEL)
        m.box((.012, .1, .06), (sx * (W / 2 + .01), .2, 0), STEEL) if False else None
    m.box((.3, .05, .02), (0, .26, D / 2 + .01), YELLOW) if False else None
    lid = root.add(sn('Lid', t=(0, H, -D / 2))); lm = mk('Crate_Lid'); lid.mesh = lm
    lm.box((W, .04, D), (0, .02, D / 2), MIL_GREEN, r=.012)
    lm.box((W - .08, .02, D - .08), (0, -.005, D / 2), DARK)
    for sx in (-.25, .25): lm.box((.08, .02, .06), (sx, .05, D - .05), STEEL)
    lm.box((.34, .005, .1), (0, .042, D / 2), YELLOW)
    for sx in (-.28, .28): m.box((.07, .05, .04), (sx, H - .03, D / 2 + .01), STEEL)
    m.box((.1, .03, .03), (0, .2, D / 2 + .01), STEEL) if False else None
    openclose(A, [(lid, 'x', 0, -105)], 1.0)
    return A

# ============================================================ SEISMOMETER ON BACK
def build_seis_back():
    """origin = mounting plane on astronaut's life-support pack; unit extends -Z (behind the player)."""
    A = Asset('Seismometer_Backpack'); root = A.add(Node('SeisBack'))
    m = mk('SeisBack_Body'); root.add(sn('SeisBack_Body', m))
    m.box((.40, .50, .03), (0, 0, -.015), STEEL, r=.01)                       # mount plate
    m.box((.34, .44, .02), (0, 0, -.04), DARK, r=.008)
    for sx in (-1, 1): m.box((.05, .56, .02), (sx * .13, 0, .002), ORANGE)       # straps
    # carry frame
    for sx in (-1, 1): m.box((.03, .46, .03), (sx * .17, 0, -.22), STEEL)
    m.box((.34, .03, .03), (0, .23, -.22), STEEL); m.box((.34, .03, .03), (0, -.23, -.22), STEEL)
    for sx in (-1, 1):
        for sy in (-1, 1): m.between((sx * .17, sy * .23, -.22), (sx * .15, sy * .2, -.04), .012, STEEL, seg=6)
    # seismometer unit
    m.cyl(.15, .12, (0, .02, -.12), HULL, axis='z', seg=24)
    m.lathe([(0, 0), (.16, 0), (.17, .02), (.15, .1), (.1, .15), (.0, .17)], (0, .02, -.18), GOLD_FOIL, seg=24, rot=(-90, 0, 0))
    m.cyl(.16, .02, (0, .02, -.175), STEEL, axis='z', seg=24)
    m.box((.2, .1, .04), (0, -.17, -.12), DARK, r=.01); m.box((.12, .02, .015), (0, -.17, -.145), SCREEN)
    m.box((.04, .04, .02), (.1, -.17, -.145), LED_GREEN)
    m.cyl(.012, .22, (.14, .2, -.1), STEEL, axis='y', seg=6); m.sph(.02, (.14, .32, -.1), LED_RED, seg=8, rings=5)
    m.box((.1, .02, .1), (-.1, .2, -.22), SOLAR, rot=(-25, 0, 0))
    m.torus(.04, .008, (-.13, -.22, -.08), RUBBER, axis='z', seg=16, tube=6)
    m.box((.06, .1, .03), (0, .1, -.26) , ORANGE, r=.01) if False else None
    m.box((.2, .03, .03), (0, .25, -.23), ORANGE)                                  # handle
    return A

# ============================================================ DEPLOYED SEISMOMETER
def build_seis_deployed():
    A = Asset('Seismometer_Deployed'); root = A.add(Node('SeisDeployed'))
    m = mk('SeisDep_Body'); root.add(sn('SeisDep_Body', m))
    m.cyl(.42, .04, (0, .02, 0), HULL_GRAY, seg=28)
    for a in (90, 210, 330):
        r = math.radians(a); m.cyl(.04, .1, (.34 * math.cos(r), .09, .34 * math.sin(r)), STEEL, seg=8)
        m.cyl(.06, .02, (.34 * math.cos(r), .01, .34 * math.sin(r)), DARK, seg=10) if False else None
    m.cyl(.34, .06, (0, .07, 0), STEEL, seg=28)
    m.lathe([(0, 0)] + [(.31 * math.cos(math.radians(a)), .30 * math.sin(math.radians(a))) for a in range(0, 91, 10)],
            (0, .1, 0), GOLD_FOIL, seg=32, smooth_deg=89) if False else None
    m.lathe([(.31, 0)] + [(.31 * math.cos(math.radians(a)), .30 * math.sin(math.radians(a))) for a in range(0, 91, 10)],
            (0, .1, 0), GOLD_FOIL, seg=32, smooth_deg=89)
    m.cyl(.32, .03, (0, .115, 0), DARK, seg=28)
    m.box((.2, .08, .1), (.28, .08, -.2) , DARK, r=.015, rot=(0, 30, 0))
    m.cyl(.012, .5, (-.3, .35, -.1), STEEL, seg=6); m.sph(.02, (-.3, .6, -.1), STEEL, seg=8, rings=5)
    # solar panel on a stand
    m.between((.3, .04, .25), (.34, .34, .3), .012, STEEL, seg=6); m.between((.55, .04, .25), (.51, .34, .3), .012, STEEL, seg=6)
    m.box((.3, .02, .22), (.42, .36, .3), SOLAR, rot=(-30, 0, 0)); m.box((.32, .015, .24), (.42, .35, .3), STEEL, rot=(-30, 0, 0))
    # tether coil
    m.torus(.1, .018, (.0, .04, .5), RUBBER, axis='y', seg=20, tube=6)
    m.between((.0, .05, .4), (.0, .05, .3), .015, RUBBER, seg=6)
    led = root.add(sn('StatusLED', t=(.1, .17, .27))); lm = mk('SeisDep_LED'); led.mesh = lm
    lm.sph(.025, (0, 0, 0), LED_GREEN, seg=8, rings=5)
    an = Anim('Operate'); blink(an, led, 2.0, 1.0, .5); A.anims.append(an)
    return A

# ============================================================ TANKS
def build_tank(kind='O'):
    isO = kind == 'O'
    A = Asset('OxygenTank_TNK-O' if isO else 'WaterTank_TNK-W'); root = A.add(Node('TNK_' + kind))
    m = mk('TNK_Body'); root.add(sn('TNK_Body', m))
    band = O2_BLUE if isO else H2O_TEAL
    m.cyl(.7, .12, (0, .06, 0), CONCRETE, seg=20)
    R_ = .55; yb, yt = .75, 2.15
    m.cyl(R_, yt - yb, (0, (yb + yt) / 2, 0), TANK_BODY, seg=32)
    for y0, sg in ((yb, -1), (yt, 1)):
        m.lathe([(R_, 0)] + [(R_ * math.cos(math.radians(a)), sg * .38 * math.sin(math.radians(a)) * (1 if True else 1)) for a in range(0, 91, 15)][1:], (0, y0, 0), TANK_BODY, seg=32, smooth_deg=89) if sg == 1 else \
        m.lathe([(0, -.38)] + [(R_ * math.cos(math.radians(a)), -.38 * math.sin(math.radians(a))) for a in range(75, -1, -15)], (0, y0, 0), TANK_BODY, seg=32, smooth_deg=89)
    m.cyl(R_ + .012, .35, (0, 1.45, 0), band, seg=32)
    for y in (.95, 1.95): m.cyl(R_ + .02, .06, (y * 0 + 0, y, 0), STEEL, seg=32)
    # frame legs
    for a in (45, 165, 285):
        r = math.radians(a); m.between((.6 * math.cos(r), .1, .6 * math.sin(r)), (.52 * math.cos(r), .95, .52 * math.sin(r)), .035, DARK, seg=8)
    m.torus(.56, .03, (0, .9, 0), DARK, seg=32, tube=6) if False else None
    # valve + piping
    m.cyl(.08, .16, (0, 2.62, 0), STEEL, seg=12); m.cyl(.14, .04, (0, 2.54, 0), DARK, seg=14) if False else None
    m.cyl(.05, .25, (0, 2.58, 0), STEEL, seg=10); m.torus(.09, .015, (0, 2.74, 0), band, seg=16, tube=6)
    m.cyl(.015, .18, (0, 2.68, 0), STEEL, seg=6) if False else None
    m.between((0, 2.62, 0), (.0, 2.75, 0), .03, STEEL, seg=6) if False else None
    m.cyl(.04, .7, (.35, 2.52, 0), STEEL, axis='x', seg=10) if False else None
    m.between((0, 2.55, 0), (.45, 2.55, .0), .045, STEEL, seg=10)
    m.cyl(.06, .1, (.5, 2.55, 0), band, axis='x', seg=12)
    m.cyl(.09, .04, (0, 2.0, R_ + .03), DARK, axis='z', seg=18); m.cyl(.07, .045, (0, 2.0, R_ + .035), SCREEN if True else STEEL, axis='z', seg=18)
    m.box((.3, .3, .02), (0, 1.0, R_ + .01), YELLOW, rot=(0, 0, 45), r=.0) if False else None
    m.box((.22, .22, .02), (0, 1.45, R_ + .03), HULL, rot=(0, 0, 45))
    m.cyl(.04, .1, (0, .75 - .45, 0), STEEL, seg=8) if False else None
    m.between((.0, .78, R_ - .05), (.0, .3, .45), .04, STEEL, seg=8)
    m.cyl(.06, .1, (0, .27, .5), band, axis='y', seg=12)
    return A

# ============================================================ ROCKS
def _rock_asset(name, radius, seed, color, squash, sub=2, rough=.35, var=.18, sink=0.0, flat=.55, spots=None):
    A = Asset(name); root = A.add(Node(name))
    mat = Mat(name + '_Mat', color, 0.0, 0.95)
    m = mk(name + '_Mesh'); root.add(sn(name + '_Mesh', m))
    m.add(rock_geo(radius, sub, seed, squash, rough, var, flat, sink), mat)
    if spots:
        sm = Mat(name + '_Clasts', spots, 0.0, 0.8)
        rng = np.random.default_rng(seed + 99)
        for i in range(7):
            d = rng.normal(size=3); d[1] = abs(d[1]) * .7 + .15; d /= np.linalg.norm(d)
            p = d * radius * .78 * np.array(squash) + np.array([0, radius * squash[1] * .45, 0])
            m.add(rock_geo(radius * rng.uniform(.1, .17), 1, seed + i, (1, .7, 1), .2, .1, .9, 0), sm, T(*p))
    return A

def build_sample_rocks():
    return [
        _rock_asset('SampleRock_A', .20, 11, (.20, .20, .22), (1.0, .72, .85), 2, .38, .2),                           # lunar basalt (dark)
        _rock_asset('SampleRock_B', .17, 23, (.72, .72, .70), (1.15, .65, .9), 2, .3, .15, spots=(.28, .28, .30)),     # anorthosite w/ clasts
        _rock_asset('SampleRock_C', .22, 37, (.42, .26, .20), (.9, .8, 1.05), 2, .42, .25, spots=(.65, .6, .5)),       # iron-rich breccia
    ]

def build_moon_rocks():
    out = []
    cfg = [(.18, (1.0, .6, .9), 1, .30), (.35, (1.1, .7, .9), 1, .35), (.55, (1.0, .65, 1.2), 2, .30),
           (.8, (1.2, .6, .9), 2, .32), (1.1, (1.0, .75, 1.0), 2, .30), (.28, (1.3, .5, .8), 1, .28)]
    for i, (r, sq, sub, rg) in enumerate(cfg):
        g = .42 + .05 * (i % 3)
        out.append(_rock_asset(f'MoonRock_{i+1:02d}', r, 100 + i * 7, (g, g, g * 1.02), sq, sub, rg, .16, sink=r * .08))
    return out
