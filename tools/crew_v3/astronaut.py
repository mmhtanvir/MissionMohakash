from glbkit import *
from common import *
import numpy as np, math

BONES = ['Hips', 'Spine', 'Chest', 'Neck', 'Head',
         'LeftUpperArm', 'LeftLowerArm', 'LeftHand', 'RightUpperArm', 'RightLowerArm', 'RightHand',
         'LeftUpperLeg', 'LeftLowerLeg', 'LeftFoot', 'RightUpperLeg', 'RightLowerLeg', 'RightFoot']
BIND_HIPS_Y = 0.95
FPS = 30

# ------------------------------------------------------------------ suit geometry
def add_suit(m, J, flag=True):
    """Adds the full spacesuit to MeshB m. J(boneName)->joint index (or None for static use)."""
    # pelvis / waist / torso
    m.sph(1, (0, .95, 0), SUIT, sc=(.20, .13, .15), joint=J('Hips'))
    m.cyl(.208, .05, (0, 1.025, 0), ORANGE, joint=J('Hips'))
    m.cyl(.18, .20, (0, 1.12, 0), SUIT, r2=.19, joint=J('Spine'))
    m.box((.52, .34, .30), (0, 1.34, 0), SUIT, r=.09, joint=J('Chest'))
    m.box((.28, .15, .05), (0, 1.33, .165), SUIT_GRAY, r=.015, joint=J('Chest'))
    for x, led in ((-.08, LED_RED), (0, LED_GREEN), (.08, LED_BLUE)):
        m.box((.035, .035, .015), (x, 1.355, .195), led, joint=J('Chest'))
    m.cyl(.025, .02, (-.06, 1.30, .19), STEEL, axis='z', joint=J('Chest'))
    m.cyl(.025, .02, (.06, 1.30, .19), STEEL, axis='z', joint=J('Chest'))
    # life-support backpack (rear face at z=-0.36 -> Socket_Back)
    m.box((.44, .50, .22), (0, 1.33, -.25), HULL, r=.05, joint=J('Chest'))
    m.box((.36, .07, .18), (0, 1.60, -.25), SUIT_GRAY, r=.02, joint=J('Chest'))
    m.box((.30, .05, .01), (0, 1.43, -.145), SUIT_GRAY, joint=J('Chest'))
    m.cyl(.065, .46, (0, 1.05, -.20), ORANGE, axis='x', joint=J('Spine'))
    m.cyl(.008, .28, (.15, 1.74, -.30), STEEL, joint=J('Chest'))
    m.sph(.02, (.15, 1.885, -.30), LED_RED, seg=8, rings=5, joint=J('Chest'))
    # neck / helmet
    m.cyl(.11, .08, (0, 1.52, 0), SUIT_GRAY, joint=J('Neck'))
    m.cyl(.15, .06, (0, 1.575, 0), SUIT_GRAY, r2=.17, joint=J('Head'))
    m.sph(.23, (0, 1.73, 0), SUIT, sc=(1, 1.05, 1.05), seg=28, rings=14, joint=J('Head'))
    m.sph(.18, (0, 1.745, .10), VISOR, sc=(1, .82, 1.0), seg=28, rings=14, joint=J('Head'))
    for sx in (-1, 1):
        m.cyl(.05, .035, (sx * .232, 1.73, 0), SUIT_GRAY, axis='x', joint=J('Head'))
        m.box((.05, .04, .05), (sx * .17, 1.88, .12), LAMP, joint=J('Head'))
    # arms
    for side, sx in (('Left', 1), ('Right', -1)):
        ua, la, hd = J(side + 'UpperArm'), J(side + 'LowerArm'), J(side + 'Hand')
        x = sx * .30
        m.sph(.095, (x, 1.39, 0), SUIT, seg=16, rings=8, joint=ua)
        m.cyl(.066, .29, (x, 1.245, 0), SUIT, r2=.074, joint=ua)
        m.cyl(.078, .05, (x, 1.20, 0), ORANGE, joint=ua)
        m.sph(.07, (x, 1.10, 0), SUIT_GRAY, seg=16, rings=8, joint=ua)
        m.cyl(.057, .26, (x, .97, 0), SUIT, r2=.067, joint=la)
        m.cyl(.071, .04, (x, .855, 0), SUIT_GRAY, joint=la)
        m.sph(.065, (x, .79, 0), GLOVE, sc=(1, 1.35, .9), seg=16, rings=8, joint=hd)
        m.sph(.026, (x - sx * .055, .81, .035), GLOVE, seg=10, rings=6, joint=hd)
        if flag:
            ox = x + sx * .076
            if side == 'Left':   # Bangladesh flag patch
                m.box((.012, .07, .105), (ox, 1.31, 0), GREEN_BD, joint=ua)
                m.cyl(.024, .008, (ox + sx * .006, 1.31, -.006), RED, axis='x', joint=ua)
            else:                # mission patch
                m.cyl(.045, .012, (ox, 1.31, 0), ORANGE, axis='x', joint=ua)
                m.cyl(.028, .014, (ox, 1.31, 0), SUIT, axis='x', joint=ua)
    # legs
    for side, sx in (('Left', 1), ('Right', -1)):
        ul, ll, ft = J(side + 'UpperLeg'), J(side + 'LowerLeg'), J(side + 'Foot')
        x = sx * .12
        m.sph(.105, (x, .89, 0), SUIT, seg=16, rings=8, joint=ul)
        m.cyl(.088, .40, (x, .69, 0), SUIT, r2=.104, joint=ul)
        m.sph(.09, (x, .49, 0), SUIT_GRAY, seg=16, rings=8, joint=ul)
        m.cyl(.068, .37, (x, .305, 0), SUIT, r2=.088, joint=ll)
        m.cyl(.084, .04, (x, .34, 0), ORANGE, joint=ll)
        m.cyl(.078, .09, (x, .11, 0), SUIT_GRAY, joint=ft)
        m.box((.15, .10, .31), (x, .055, .07), SUIT_GRAY, r=.04, joint=ft)
        m.box((.16, .03, .33), (x, .015, .07), RUBBER, r=.012, joint=ft)

# ------------------------------------------------------------------ pose helper
def ss(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

class Pose:
    def __init__(s):
        s.e = {b: np.zeros(3) for b in BONES}; s.hip_t = np.array([0, BIND_HIPS_Y, 0.0])
    def add(s, b, x=0, y=0, z=0): s.e[b] += (x, y, z)
    def quats(s): return {b: qe(*s.e[b]) for b in BONES}

def rest(p):
    p.add('LeftUpperArm', z=7); p.add('RightUpperArm', z=-7)
    p.add('LeftLowerArm', x=-10); p.add('RightLowerArm', x=-10)

# ------------------------------------------------------------------ clips (t in seconds)
def idle(t, T=4.0):
    p = Pose(); rest(p); ph = 2 * math.pi * t / T; br = math.sin(ph)
    p.add('Chest', x=1.0 * br); p.add('Spine', x=1 + .6 * br)
    p.add('Head', x=-1 + 1.5 * math.sin(ph + .9), y=3 * math.sin(ph))
    p.add('LeftUpperArm', z=1.0 * br); p.add('RightUpperArm', z=-1.0 * br)
    p.add('LeftLowerArm', x=-2 * br); p.add('RightLowerArm', x=-2 * br)
    p.add('LeftLowerLeg', x=2); p.add('RightLowerLeg', x=2)
    p.add('LeftUpperLeg', x=-1); p.add('RightUpperLeg', x=-1)
    p.add('Hips', z=1.0 * math.sin(ph)); p.add('Spine', z=-1.0 * math.sin(ph))
    p.hip_t[1] += .004 * br
    return p

def gait(t, T, A, K, B, elb, lean, twist, dip):
    p = Pose(); ph = 2 * math.pi * t / T
    p.add('LeftUpperArm', z=7); p.add('RightUpperArm', z=-7)
    for side, off in (('Left', 0.0), ('Right', math.pi)):
        sw = A * math.sin(ph + off)
        k = K * (0.5 + 0.5 * math.cos(ph + off)) ** 2 + 4
        p.add(side + 'UpperLeg', x=-sw); p.add(side + 'LowerLeg', x=k)
        p.add(side + 'Foot', x=0.7 * (sw - k))
        fwd = -math.sin(ph + off)            # arms oppose legs; +ve = forward
        p.add(side + 'UpperArm', x=-B * fwd)
        p.add(side + 'LowerArm', x=-(elb * (1 + 0.4 * fwd)))
    p.add('Hips', y=twist * math.sin(ph)); p.add('Spine', y=-twist * .6 * math.sin(ph), x=lean)
    p.add('Chest', y=-twist * .4 * math.sin(ph)); p.add('Head', x=-lean * .8, y=twist * .2 * math.sin(ph))
    p.hip_t[:] = (.012 * math.sin(ph), BIND_HIPS_Y - dip * .5 * (1 - math.cos(2 * ph)), 0)
    return p

def walk(t, T=1.0): return gait(t, T, 25, 45, 20, 18, 4, 5, .08)
def run(t, T=0.6):  return gait(t, T, 42, 100, 48, 85, 13, 9, .16)

def wave(t, T=2.4):
    p = Pose(); rest(p); ph = 2 * math.pi * t / 4.0
    e = ss(0, .4, t) - ss(1.9, 2.4, t); osc = math.sin(2 * math.pi * 2.5 * t) * e
    p.add('RightUpperArm', z=-(138 * e), x=-8 * e)
    p.add('RightLowerArm', x=-(25 * e), z=32 * osc); p.add('RightHand', z=14 * osc)
    p.add('Chest', z=-2.5 * e); p.add('Head', z=-6 * e, y=-8 * e)
    p.add('LeftUpperArm', z=1.0 * math.sin(ph)); p.hip_t[1] += .003 * math.sin(ph)
    return p

def interact(t, T=1.6):
    p = Pose(); rest(p)
    e = ss(0, .35, t) - ss(.95, 1.45, t)
    press = math.sin(math.pi * min(1.0, max(0.0, (t - .45) / .35))) * e
    p.add('RightUpperArm', x=-78 * e, z=-6 * e)
    p.add('RightLowerArm', x=-(22 * e + 18 * press)); p.add('RightHand', x=-10 * press)
    p.add('LeftUpperArm', x=-14 * e); p.add('LeftLowerArm', x=-18 * e)
    p.add('Spine', x=10 * e); p.add('Chest', x=6 * e); p.add('Head', x=8 * e); p.add('Hips', x=4 * e)
    for s in ('Left', 'Right'):
        p.add(s + 'UpperLeg', x=-12 * e); p.add(s + 'LowerLeg', x=22 * e); p.add(s + 'Foot', x=-8 * e)
    p.hip_t[1] -= .06 * e
    return p

def death(t, T=2.6):
    p = Pose(); rest(p)
    f = min(1.0, max(0.0, (t - .15) / 1.45)); fall = f * f
    kb = ss(0, .6, t)
    p.add('Hips', x=-88 * fall)
    p.hip_t[:] = (0, BIND_HIPS_Y - (BIND_HIPS_Y - .37) * (fall ** .85), -.38 * fall)
    for s, k in (('Left', 22), ('Right', 0)):
        p.add(s + 'UpperLeg', x=-26 * kb * (1 - fall) + (-6 if s == 'Left' else 4) * fall)
        p.add(s + 'LowerLeg', x=65 * kb * (1 - fall) + k * fall)
        p.add(s + 'Foot', x=-20 * (1 - fall))
    p.add('LeftUpperArm', z=48 * fall, x=-34 * kb * (1 - fall) + 10 * fall)
    p.add('RightUpperArm', z=-58 * fall, x=-22 * kb * (1 - fall) - 12 * fall)
    p.add('LeftLowerArm', x=-30 * kb * (1 - fall) - 8 * fall); p.add('RightLowerArm', x=-20 * kb * (1 - fall) - 25 * fall)
    p.add('Spine', x=-6 * fall + 14 * kb * (1 - fall)); p.add('Head', x=-24 * fall, z=10 * fall)
    if t > 1.6:
        d = t - 1.6; p.hip_t[1] += math.exp(-d * 7) * math.sin(d * 22) * .035
        p.add('LeftUpperArm', z=math.exp(-d * 6) * math.sin(d * 18) * 5)
    return p

CLIPS = [  # name, fn, duration, loop
    ('Idle', idle, 4.0, True), ('Walk', walk, 1.0, True), ('Run', run, 0.6, True),
    ('Wave', wave, 2.4, False), ('Interact', interact, 1.6, False), ('Death', death, 2.6, False)]

def pose_dict(p):
    q = p.quats(); d = {b: (None, q[b]) for b in BONES}
    d['Hips'] = (p.hip_t.copy(), q['Hips']); return d

# ------------------------------------------------------------------ build
def build_astronaut():
    A = Asset('Astronaut')
    arm = Node('Armature'); A.add(arm)
    pos = {'Hips': (0, .95, 0), 'Spine': (0, .12, 0), 'Chest': (0, .18, 0), 'Neck': (0, .25, 0), 'Head': (0, .08, 0)}
    N = {}
    N['Hips'] = arm.add(Node('Hips', t=pos['Hips']))
    N['Spine'] = N['Hips'].add(Node('Spine', t=pos['Spine']))
    N['Chest'] = N['Spine'].add(Node('Chest', t=pos['Chest']))
    N['Neck'] = N['Chest'].add(Node('Neck', t=pos['Neck']))
    N['Head'] = N['Neck'].add(Node('Head', t=pos['Head']))
    for side, sx in (('Left', 1), ('Right', -1)):
        N[side + 'UpperArm'] = N['Chest'].add(Node(side + 'UpperArm', t=(sx * .30, .14, 0)))
        N[side + 'LowerArm'] = N[side + 'UpperArm'].add(Node(side + 'LowerArm', t=(0, -.29, 0)))
        N[side + 'Hand'] = N[side + 'LowerArm'].add(Node(side + 'Hand', t=(0, -.26, 0)))
        N[side + 'UpperLeg'] = N['Hips'].add(Node(side + 'UpperLeg', t=(sx * .12, -.06, 0)))
        N[side + 'LowerLeg'] = N[side + 'UpperLeg'].add(Node(side + 'LowerLeg', t=(0, -.40, 0)))
        N[side + 'Foot'] = N[side + 'LowerLeg'].add(Node(side + 'Foot', t=(0, -.41, 0)))
    # attachment sockets (empty transforms, parented to bones)
    N['Chest'].add(Node('Socket_Back', t=(0, .11, -.36), extras={"note": "Rear face of life-support pack. Seismometer pack mounts here, extends -Z."}))
    N['RightHand'].add(Node('Socket_HandR', t=(0, -.07, 0)))
    N['LeftHand'].add(Node('Socket_HandL', t=(0, -.07, 0)))
    N['Head'].add(Node('Socket_Helmet', t=(0, .15, .2)))
    m = MeshB('AstronautBody')
    jidx = {b: i for i, b in enumerate(BONES)}
    add_suit(m, lambda b: jidx[b])
    mesh = Node('AstronautMesh', mesh=m); A.add(mesh)
    mesh.skin = Skin([N[b] for b in BONES], arm)
    # animations
    for name, fn, T_, loop in CLIPS:
        an = Anim(name); n = int(round(T_ * FPS)); times = np.arange(n + 1) / FPS
        poses = [pose_dict(fn(min(t, T_), T_)) for t in times]
        if loop:  # make last key identical to first so the loop is seamless
            poses[-1] = poses[0]
        for b in BONES:
            an.add(N[b], 'rotation', times, np.stack([ps[b][1] for ps in poses]))
        an.add(N['Hips'], 'translation', times, np.stack([ps['Hips'][0] for ps in poses]))
        A.anims.append(an)
    A.nodes_by_name = N
    return A

if __name__ == '__main__':
    import sys
    sys.path.insert(0, '.')
    from preview import sheet
    A = build_astronaut()
    print('tris', A.tri_count(), 'bounds', A.bounds())
    sheet(A, '/home/claude/mm/prev_astro_static.png', views=((20, 10), (200, 10), (90, 0)), title='astronaut bind pose')
