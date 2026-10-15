import math, numpy as np
from glbkit import *
import astronaut as AS
from astronaut import BONES, FPS, Pose, ss, idle, walk, run, wave, interact, death, pose_dict

# ------------------------------------------------------------ materials
def M(n, c, m=0.0, r=0.6, **k): return Mat(n, c, m, r, **k)
SUIT    = M('SuitWhite', (0.92, 0.93, 0.94), 0.0, 0.62)
SUIT2   = M('SuitPanelGray', (0.62, 0.65, 0.70), 0.2, 0.5)
NAVYM   = M('SuitNavy', (0.08, 0.17, 0.55), 0.0, 0.5)
DARKG   = M('SuitDarkGray', (0.12, 0.13, 0.15), 0.3, 0.5)
VISOR   = M('VisorDark', (0.02, 0.025, 0.035), 0.9, 0.06)
AMBER   = M('VisorAmber', (0.95, 0.45, 0.05), 0.2, 0.2, emissive=(0.9, 0.35, 0.03), estr=0.8)
GLOVE   = M('GloveSteel', (0.62, 0.65, 0.70), 0.85, 0.35)
BOOT    = M('BootBlack', (0.07, 0.07, 0.08), 0.0, 0.8)
PACK    = M('PackWhite', (0.84, 0.85, 0.87), 0.1, 0.55)
LEDB    = M('LEDBlue', (0.25, 0.6, 1.0), 0, 0.4, emissive=(0.25, 0.6, 1.0), estr=3.0)
LEDG    = M('LEDGreen', (0.1, 1.0, 0.3), 0, 0.4, emissive=(0.1, 1.0, 0.3), estr=3.0)
LEDR    = M('LEDRed', (1.0, 0.1, 0.05), 0, 0.4, emissive=(1.0, 0.1, 0.05), estr=3.0)
LAMPM   = M('HelmetLamp', (1, 0.97, 0.88), 0, 0.4, emissive=(1, 0.95, 0.85), estr=3.0)
FLAGG   = M('FlagGreen', (0.0, 0.42, 0.31), 0, 0.6)
FLAGR   = M('FlagRed', (0.80, 0.08, 0.06), 0, 0.6)
SKIN    = M('Skin', (0.60, 0.41, 0.29), 0.0, 0.6)
SKIN_D  = M('SkinShade', (0.50, 0.33, 0.23), 0.0, 0.65)
HAIR    = M('Hair', (0.035, 0.03, 0.03), 0.0, 0.7)
EYEW    = M('EyeWhite', (0.93, 0.93, 0.92), 0.0, 0.3)
EYEB    = M('Pupil', (0.04, 0.025, 0.02), 0.0, 0.2)
LIPS    = M('Lips', (0.42, 0.22, 0.18), 0.0, 0.5)
SHIRT   = M('TeeNavy', (0.12, 0.28, 0.55), 0.0, 0.85)
SHIRT_D = M('TeeCollar', (0.08, 0.19, 0.40), 0.0, 0.85)
PANTS   = M('JoggerGray', (0.23, 0.24, 0.28), 0.0, 0.85)
PANTS_D = M('JoggerCuff', (0.15, 0.16, 0.19), 0.0, 0.85)
SHOE    = M('SneakerWhite', (0.92, 0.92, 0.9), 0.0, 0.55)
SOLE    = M('SneakerSole', (0.80, 0.80, 0.78), 0.0, 0.7)
SHOE_AC = M('SneakerAccent', (0.95, 0.42, 0.08), 0.0, 0.5)
WATCH   = M('Watch', (0.06, 0.06, 0.07), 0.5, 0.3)

# ------------------------------------------------------------ skeleton
def skeleton(A, hips_y, spine, chest, neck, head, sh_x, sh_y, hip_x, hip_dy, ua, la, ul, ll, sockets=True):
    arm = A.add(Node('Armature')); N = {}
    N['Hips'] = arm.add(Node('Hips', t=(0, hips_y, 0)))
    N['Spine'] = N['Hips'].add(Node('Spine', t=(0, spine, 0)))
    N['Chest'] = N['Spine'].add(Node('Chest', t=(0, chest, 0)))
    N['Neck'] = N['Chest'].add(Node('Neck', t=(0, neck, 0)))
    N['Head'] = N['Neck'].add(Node('Head', t=(0, head, 0)))
    for side, sx in (('Left', 1), ('Right', -1)):
        N[side + 'UpperArm'] = N['Chest'].add(Node(side + 'UpperArm', t=(sx * sh_x, sh_y, 0)))
        N[side + 'LowerArm'] = N[side + 'UpperArm'].add(Node(side + 'LowerArm', t=(0, -ua, 0)))
        N[side + 'Hand'] = N[side + 'LowerArm'].add(Node(side + 'Hand', t=(0, -la, 0)))
        N[side + 'UpperLeg'] = N['Hips'].add(Node(side + 'UpperLeg', t=(sx * hip_x, hip_dy, 0)))
        N[side + 'LowerLeg'] = N[side + 'UpperLeg'].add(Node(side + 'LowerLeg', t=(0, -ul, 0)))
        N[side + 'Foot'] = N[side + 'LowerLeg'].add(Node(side + 'Foot', t=(0, -ll, 0)))
    return arm, N

# ------------------------------------------------------------ extra clips
def lie_pose(t, T=1.9):
    """standing -> crouch -> long-sit -> reclined on back. Avatar-local: head ends toward -Z, legs toward +Z."""
    p = Pose()
    a = ss(0.0, 0.5, t)            # sit down
    b = ss(0.5, 1.0, t)            # swing legs forward
    c = ss(1.0, 1.9, t)            # recline
    hy = 0.93 * (1 - a) + 0.30 * a
    hy = hy * (1 - b) + 0.13 * b
    hy = hy * (1 - c) + 0.15 * c
    p.hip_t[:] = (0, hy, 0.0)
    for s in ('Left', 'Right'):
        thigh = -(70 * a) * (1 - b) - 92 * b
        thigh = thigh * (1 - c) + -2 * c
        shin = (70 * a) * (1 - b) + 4 * b
        shin = shin * (1 - c) + 2 * c
        p.add(s + 'UpperLeg', x=thigh); p.add(s + 'LowerLeg', x=shin); p.add(s + 'Foot', x=-8 * c + 6 * b * (1 - c))
    p.add('Hips', x=-90 * c)
    p.add('Spine', x=18 * a * (1 - b) + 3 * b * (1 - c)); p.add('Head', x=-6 * c + 8 * a * (1 - c))
    p.add('LeftUpperArm', z=8 + 10 * a, x=-10 * a * (1 - c)); p.add('RightUpperArm', z=-8 - 10 * a, x=-10 * a * (1 - c))
    p.add('LeftLowerArm', x=-12 - 8 * c); p.add('RightLowerArm', x=-12 - 8 * c)
    return p

def sleep_pose(t, T=5.0):
    p = lie_pose(1.9); ph = 2 * math.pi * t / T; br = math.sin(ph)
    p.add('Chest', x=1.6 * br); p.add('Spine', x=0.8 * br); p.add('Head', x=1.0 * math.sin(ph + 0.6), z=1.5 * math.sin(ph * 0.5 + 1))
    p.add('LeftUpperArm', x=-1.2 * br); p.add('RightUpperArm', x=-1.2 * br)
    p.add('LeftLowerArm', x=-6); p.add('RightLowerArm', x=-6)   # arms resting along the body
    p.add('LeftUpperArm', z=4); p.add('RightUpperArm', z=-4)
    p.hip_t[1] += 0.004 * br
    return p

def jump(t, T=3.4):
    p = Pose(); AS.rest(p)
    e = ss(0.0, 0.25, t); fl = ss(0.25, 0.7, t); land = ss(2.7, 3.3, t)
    sway = math.sin(2 * math.pi * t / 2.2)
    p.hip_t[:] = (0, 0.95 - 0.04 * (1 - e) - 0.0 * fl, 0)
    for s, sx in (('Left', 1), ('Right', -1)):
        p.add(s + 'UpperLeg', x=-(6 + 16 * fl + 30 * land) + 4 * sway * sx)
        p.add(s + 'LowerLeg', x=(10 + 28 * fl + 20 * land) + 3 * sway)
        p.add(s + 'Foot', x=-14 * fl)
        p.add(s + 'UpperArm', z=sx * (28 * fl + 22 * land) * -1 * -1, x=-(25 * fl) * (1 - land) + 4 * sway * sx)
        p.add(s + 'LowerArm', x=-(14 + 14 * fl))
    p.add('Spine', x=5 * fl - 8 * land); p.add('Head', x=-4 * fl); p.add('Hips', x=3 * fl, z=2 * sway)
    return p

def reverse(fn, T):
    return lambda t, T_=T: fn(T - t, T)

def add_clips(A, N, extra=True, casual=False):
    clips = [('Idle', AS.idle, 4.0, True), ('Walk', AS.walk, 1.0, True), ('Run', AS.run, 0.6, True),
             ('Jump', jump, 3.4, False), ('Wave', AS.wave, 2.4, False), ('Interact', AS.interact, 1.6, False), ('Death', AS.death, 2.6, False)]
    if casual:
        clips += [('LieDown', lie_pose, 1.9, False), ('Sleep', sleep_pose, 5.0, True), ('GetUp', lambda t, T=1.9: lie_pose(1.9 - t), 1.9, False)]
    for name, fn, T_, loop in clips:
        an = Anim(name); n = int(round(T_ * FPS)); times = np.arange(n + 1) / FPS
        poses = [pose_dict(fn(min(t, T_), T_)) for t in times]
        if loop: poses[-1] = poses[0]
        for b in BONES: an.add(N[b], 'rotation', times, np.stack([ps[b][1] for ps in poses]))
        an.add(N['Hips'], 'translation', times, np.stack([ps['Hips'][0] for ps in poses]))
        A.anims.append(an)

# ============================================================ EVA astronaut (screenshot style)
def build_eva():
    A = Asset('AstronautEVA')
    # same proportions as the original rig so poses match
    arm, N = skeleton(A, 0.95, 0.12, 0.18, 0.25, 0.08, 0.30, 0.14, 0.12, -0.06, 0.29, 0.26, 0.40, 0.41)
    m = MeshB('EVA_Body'); J = {b: i for i, b in enumerate(BONES)}; j = lambda b: J[b]
    # pelvis / torso
    m.sph(1, (0, .95, 0), SUIT, sc=(.21, .14, .16), joint=j('Hips'))
    m.cyl(.215, .05, (0, 1.025, 0), NAVYM, joint=j('Hips'))
    m.cyl(.185, .20, (0, 1.12, 0), SUIT, r2=.195, joint=j('Spine'))
    m.box((.54, .36, .31), (0, 1.34, 0), SUIT, r=.10, joint=j('Chest'))
    m.box((.58, .10, .33), (0, 1.47, 0), SUIT, r=.045, joint=j('Chest'))                 # shoulder yoke
    m.box((.30, .17, .06), (0, 1.33, .17), SUIT2, r=.02, joint=j('Chest'))               # chest control box (as screenshot)
    m.box((.26, .12, .02), (0, 1.335, .205), DARKG, r=.01, joint=j('Chest'))
    for x, led in ((-.08, LEDR), (0, LEDG), (.08, LEDB)): m.box((.035, .03, .015), (x, 1.37, .22), led, joint=j('Chest'))
    m.sph(.022, (0, 1.30, .225), LEDB, seg=10, rings=6, joint=j('Chest'))
    m.cyl(.026, .02, (-.15, 1.28, .19), SUIT2, axis='z', joint=j('Chest')); m.cyl(.026, .02, (.15, 1.28, .19), SUIT2, axis='z', joint=j('Chest'))
    # dark shoulder pads (as screenshot)
    for sx in (-1, 1): m.sph(.115, (sx * .30, 1.40, 0), DARKG, sc=(1, .85, 1), seg=18, rings=9, joint=j('LeftUpperArm' if sx > 0 else 'RightUpperArm'))
    # backpack
    m.box((.46, .52, .24), (0, 1.33, -.255), PACK, r=.06, joint=j('Chest'))
    m.box((.38, .08, .20), (0, 1.62, -.255), SUIT2, r=.02, joint=j('Chest'))
    m.box((.34, .06, .01), (0, 1.44, -.14), NAVYM, joint=j('Chest'))
    m.cyl(.065, .46, (0, 1.05, -.20), NAVYM, axis='x', joint=j('Spine'))
    m.cyl(.008, .30, (.15, 1.75, -.30), GLOVE, joint=j('Chest')); m.sph(.02, (.15, 1.90, -.30), LEDR, seg=8, rings=5, joint=j('Chest'))
    # neck ring + helmet
    m.cyl(.115, .08, (0, 1.52, 0), SUIT2, joint=j('Neck'))
    m.cyl(.165, .07, (0, 1.58, 0), SUIT2, r2=.18, joint=j('Head'))
    m.sph(.235, (0, 1.735, 0), SUIT, sc=(1, 1.05, 1.06), seg=32, rings=16, joint=j('Head'))
    m.sph(.245, (0, 1.845, 0), NAVYM, sc=(1.0, .2, 1.07), seg=32, rings=8, joint=j('Head'))   # navy band across the helmet top
    m.sph(.20, (0, 1.745, .085), VISOR, sc=(1.0, .86, 1.0), seg=32, rings=16, joint=j('Head'))   # dark visor
    m.sph(.205, (0, 1.835, .075), AMBER, sc=(.96, .10, 1.0), seg=24, rings=6, joint=j('Head'))     # amber tint band at visor top
    for sx in (-1, 1):
        m.cyl(.055, .04, (sx * .238, 1.73, 0), SUIT2, axis='x', joint=j('Head'))
        m.box((.05, .04, .05), (sx * .16, 1.90, .13), LAMPM, joint=j('Head'))
    # arms
    for side, sx in (('Left', 1), ('Right', -1)):
        ua, la, hd = j(side + 'UpperArm'), j(side + 'LowerArm'), j(side + 'Hand'); x = sx * .30
        m.cyl(.068, .29, (x, 1.245, 0), SUIT, r2=.076, joint=ua)
        m.cyl(.080, .05, (x, 1.20, 0), NAVYM, joint=ua)
        m.sph(.072, (x, 1.10, 0), SUIT2, seg=16, rings=8, joint=ua)
        m.cyl(.058, .26, (x, .97, 0), SUIT, r2=.068, joint=la)
        m.cyl(.073, .04, (x, .855, 0), SUIT2, joint=la)
        m.sph(.068, (x, .79, 0), GLOVE, sc=(1, 1.35, .9), seg=16, rings=8, joint=hd)
        for f in range(4): m.cyl(.011, .06, (x + (f - 1.5) * .02, .70, .0), GLOVE, seg=6, joint=hd)
        m.sph(.026, (x - sx * .058, .81, .036), GLOVE, seg=10, rings=6, joint=hd)
        if side == 'Left':
            m.box((.095, .12, .11), (x, .97, .0), DARKG, r=.015, rot=(0, 0, 0), joint=la) if False else None
            m.box((.04, .11, .13), (x + sx * .062, .97, .0), DARKG, r=.01, joint=la)       # wrist control pad
            m.box((.012, .08, .09), (x + sx * .084, .97, .0), LEDB, joint=la)
            ox = x + sx * .078
            m.box((.012, .07, .105), (ox, 1.30, 0), FLAGG, joint=ua); m.cyl(.024, .008, (ox + sx * .006, 1.30, -.006), FLAGR, axis='x', joint=ua)
        else:
            m.cyl(.045, .012, (x + sx * .078, 1.30, 0), NAVYM, axis='x', joint=ua); m.cyl(.028, .014, (x + sx * .078, 1.30, 0), SUIT, axis='x', joint=ua)
    # legs
    for side, sx in (('Left', 1), ('Right', -1)):
        ul, ll, ft = j(side + 'UpperLeg'), j(side + 'LowerLeg'), j(side + 'Foot'); x = sx * .12
        m.sph(.108, (x, .89, 0), SUIT, seg=16, rings=8, joint=ul)
        m.cyl(.09, .40, (x, .69, 0), SUIT, r2=.106, joint=ul)
        m.sph(.095, (x, .49, 0), SUIT2, seg=16, rings=8, joint=ul)
        m.cyl(.07, .37, (x, .305, 0), SUIT, r2=.09, joint=ll)
        m.cyl(.087, .045, (x, .40, 0), NAVYM, joint=ll); m.cyl(.0875, .02, (x, .335, 0), NAVYM, joint=ll)   # blue knee stripes
        m.cyl(.080, .09, (x, .11, 0), SUIT2, joint=ft)
        m.box((.155, .10, .32), (x, .055, .07), DARKG, r=.04, joint=ft)
        m.box((.165, .03, .34), (x, .015, .07), BOOT, r=.012, joint=ft)
    arm.children and None
    A.add(Node('x_dummy')) if False else None
    mesh = Node('EVA_Mesh', mesh=m); A.add(mesh); mesh.skin = Skin([N[b] for b in BONES], arm)
    N['Chest'].add(Node('Socket_Back', t=(0, .11, -.38)))
    add_clips(A, N, casual=False)
    return A

# ============================================================ casual crew member
def build_casual():
    A = Asset('CrewCasual')
    arm, N = skeleton(A, 0.93, 0.11, 0.17, 0.24, 0.07, 0.195, 0.13, 0.09, -0.05, 0.27, 0.25, 0.43, 0.37)
    m = MeshB('Crew_Body'); J = {b: i for i, b in enumerate(BONES)}; j = lambda b: J[b]
    # pelvis + torso (tee, belt-less joggers)
    m.sph(1, (0, .93, 0), PANTS, sc=(.165, .115, .115), joint=j('Hips'))
    m.cyl(.150, .12, (0, 1.03, 0), SHIRT, r2=.145, joint=j('Spine'))
    m.cyl(.152, .035, (0, .985, 0), PANTS_D, joint=j('Spine'))                       # waistband
    m.box((.37, .31, .21), (0, 1.27, 0), SHIRT, r=.085, joint=j('Chest'))
    m.box((.36, .09, .205), (0, 1.375, 0), SHIRT, r=.04, joint=j('Chest'))
    m.box((.075, .02, .008), (.10, 1.33, .106), SHIRT_D, joint=j('Chest'))            # chest logo bar
    # arms: short sleeves, bare forearms
    for side, sx in (('Left', 1), ('Right', -1)):
        ua, la, hd = j(side + 'UpperArm'), j(side + 'LowerArm'), j(side + 'Hand'); x = sx * .195
        m.sph(.068, (x, 1.335, 0), SHIRT, seg=14, rings=8, joint=ua)
        m.cyl(.058, .13, (x, 1.265, 0), SHIRT, r2=.062, joint=ua)
        m.cyl(.063, .02, (x, 1.20, 0), SHIRT_D, joint=ua)
        m.cyl(.045, .13, (x, 1.135, 0), SKIN, r2=.045, joint=ua)
        m.sph(.044, (x, 1.065, 0), SKIN, seg=12, rings=6, joint=ua)
        m.cyl(.041, .25, (x, .93, 0), SKIN, r2=.046, joint=la)
        m.sph(.052, (x, .79, 0), SKIN, sc=(.85, 1.35, .62), seg=14, rings=8, joint=hd)
        m.sph(.02, (x - sx * .042, .82, .022), SKIN, seg=8, rings=5, joint=hd)
        if side == 'Left':
            m.cyl(.05, .03, (x, .875, 0), WATCH, joint=la)
            ox = x + sx * .068
            m.box((.01, .06, .09), (ox, 1.27, 0), FLAGG, joint=ua); m.cyl(.02, .006, (ox + sx * .005, 1.27, -.004), FLAGR, axis='x', joint=ua)
    # neck + head
    m.cyl(.048, .09, (0, 1.455, 0), SKIN, joint=j('Neck'))
    m.cyl(.085, .03, (0, 1.425, 0), SHIRT_D, r2=.075, joint=j('Neck'))               # collar
    hy = 1.62
    m.sph(.108, (0, hy, 0), SKIN, sc=(.95, 1.17, 1.02), seg=28, rings=16, joint=j('Head'))
    m.sph(.06, (0, hy - .085, .035), SKIN, sc=(1.1, .8, .95), seg=16, rings=8, joint=j('Head'))   # jaw / chin
    for sx in (-1, 1):
        m.sph(.017, (sx * .040, hy + .02, .093), EYEW, sc=(1.25, .8, .6), seg=10, rings=6, joint=j('Head'))
        m.sph(.0085, (sx * .040, hy + .02, .104), EYEB, seg=8, rings=5, joint=j('Head'))
        m.box((.05, .009, .012), (sx * .042, hy + .052, .098), HAIR, rot=(0, 0, -sx * 6), joint=j('Head'))
        m.sph(.022, (sx * .103, hy, -.005), SKIN_D, sc=(.35, 1.0, .75), seg=10, rings=6, joint=j('Head'))
    m.sph(.019, (0, hy - .018, .104), SKIN, sc=(1, 1.1, 1.0), seg=10, rings=6, joint=j('Head'))
    m.box((.052, .008, .012), (0, hy - .065, .098), LIPS, joint=j('Head'))
    # hair: short dark cut
    m.sph(.114, (0, hy + .018, -.012), HAIR, sc=(.97, 1.0, 1.07), seg=28, rings=10, joint=j('Head'))
    m.box((.205, .05, .07), (0, hy + .075, .075), HAIR, r=.02, joint=j('Head'))
    for sx in (-1, 1): m.box((.018, .06, .06), (sx * .098, hy - .005, .02), HAIR, joint=j('Head'))
    # legs: grey joggers, cuffs, sneakers
    for side, sx in (('Left', 1), ('Right', -1)):
        ul, ll, ft = j(side + 'UpperLeg'), j(side + 'LowerLeg'), j(side + 'Foot'); x = sx * .09
        m.sph(.093, (x, .88, 0), PANTS, seg=14, rings=8, joint=ul)
        m.cyl(.078, .43, (x, .665, 0), PANTS, r2=.092, joint=ul)
        m.sph(.074, (x, .45, 0), PANTS, seg=14, rings=8, joint=ul)
        m.cyl(.060, .37, (x, .265, 0), PANTS, r2=.074, joint=ll)
        m.cyl(.062, .05, (x, .115, 0), PANTS_D, joint=ll)
        m.box((.115, .075, .27), (x, .045, .055), SHOE, r=.035, joint=ft)
        m.box((.12, .028, .285), (x, .014, .055), SOLE, r=.012, joint=ft)
        m.box((.118, .02, .06), (x, .062, .165), SHOE_AC, r=.008, joint=ft)
    mesh = Node('Crew_Mesh', mesh=m); A.add(mesh); mesh.skin = Skin([N[b] for b in BONES], arm)
    add_clips(A, N, casual=True)
    return A

if __name__ == '__main__':
    import preview, sys
    for name, fn in (('AstronautEVA', build_eva), ('CrewCasual', build_casual)):
        A = fn(); lo, hi = A.bounds(); print(name, A.tri_count(), 'tris', (hi - lo).round(2), lo.round(2), hi.round(2), [a.name for a in A.anims])
        sz = A.save(f'out/Assets/Resources/Models/{name}.glb'); print(' size KB', sz // 1024)
        preview.prepare_bind(A); preview.sheet(A, f'prev/{name}.png', views=((20, 8), (160, 8), (90, 2)), title=name)
