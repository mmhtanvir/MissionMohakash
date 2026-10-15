"""
poses_extra.py - extra animation clips for the Mission Mohakash rigs (EVA astronaut + casual crew):
  PickUp   : crouch, grab an object off the ground with both hands, stand up holding it at the chest
  PutDown  : the reverse (lower the object to the ground, let go, stand up)
  CarryIdle / CarryWalk / CarryRun : two-handed carry (arms locked in front, legs do the gait)
All poses are 'absolute local rotations of a hanging-arm skeleton' like the rest of the rig, so the same
functions drive both characters; only the dimension table differs. Forward kinematics + a tiny IK solver
(scipy) place the hands, so the carry pose really holds a box at the Socket_Hold position.
"""
import math, numpy as np
from scipy.optimize import minimize
import astronaut as AS
from astronaut import Pose, ss, BONES
from glbkit import RE

# dimension tables ---------------------------------------------------------------------------
EVA = dict(hips_y=0.93, spine=0.12, chest=0.17, neck=0.25, head=0.08, sh_x=0.33, sh_y=0.18,
           hip_x=0.13, hip_dy=-0.05, ua=0.31, la=0.30, ul=0.362, ll=0.361, ankle_h=0.16,
           hold=(0.0, -0.12, 0.36), hand=(0.19, -0.12, 0.27), rest_z=5.0,
           crouch_cfg=dict(z=0.30, crouch=(105.0, 40.0), lean=(25.0, 20.0, 10.0)))
CASUAL = dict(hips_y=0.93, spine=0.11, chest=0.17, neck=0.24, head=0.07, sh_x=0.195, sh_y=0.13,
              hip_x=0.09, hip_dy=-0.05, ua=0.27, la=0.25, ul=0.43, ll=0.37, ankle_h=0.08,
              hold=(0.0, -0.10, 0.31), hand=(0.15, -0.10, 0.24), rest_z=7.0,
              crouch_cfg=dict(z=0.30, crouch=(105.0, 40.0), lean=(35.0, 20.0, 10.0)))

def T4(x, y, z):
    m = np.eye(4); m[:3, 3] = (x, y, z); return m

def fk(D, p):
    """world 4x4 matrices of the bones we care about for pose p"""
    e = p.e; W = {}
    W['Hips'] = T4(*p.hip_t) @ RE(*e['Hips'])
    W['Spine'] = W['Hips'] @ T4(0, D['spine'], 0) @ RE(*e['Spine'])
    W['Chest'] = W['Spine'] @ T4(0, D['chest'], 0) @ RE(*e['Chest'])
    for s, sx in (('Left', 1), ('Right', -1)):
        W[s + 'UpperArm'] = W['Chest'] @ T4(sx * D['sh_x'], D['sh_y'], 0) @ RE(*e[s + 'UpperArm'])
        W[s + 'LowerArm'] = W[s + 'UpperArm'] @ T4(0, -D['ua'], 0) @ RE(*e[s + 'LowerArm'])
        W[s + 'Hand'] = W[s + 'LowerArm'] @ T4(0, -D['la'], 0) @ RE(*e[s + 'Hand'])
        W[s + 'UpperLeg'] = W['Hips'] @ T4(sx * D['hip_x'], D['hip_dy'], 0) @ RE(*e[s + 'UpperLeg'])
        W[s + 'LowerLeg'] = W[s + 'UpperLeg'] @ T4(0, -D['ul'], 0) @ RE(*e[s + 'LowerLeg'])
        W[s + 'Foot'] = W[s + 'LowerLeg'] @ T4(0, -D['ll'], 0) @ RE(*e[s + 'Foot'])
    return W

def pos(W, b): return W[b][:3, 3]

ARM = ('UpperArm', 'LowerArm')

def set_arm(p, side, v):
    p.e[side + 'UpperArm'][:] = v[:3]; p.e[side + 'LowerArm'][:] = (v[3], 0, 0)

def solve_arm(D, base_pose, side, target_world, guess, w_reg=2e-4):
    sx = 1 if side == 'Left' else -1
    def cost(v):
        p = copy_pose(base_pose); set_arm(p, side, v)
        W = fk(D, p); d = pos(W, side + 'Hand') - target_world
        reg = w_reg * float(np.sum(np.square(v)))
        flex = 0.02 * max(0.0, v[3] + 5.0) ** 2 + 0.02 * max(0.0, -v[3] - 140.0) ** 2   # elbow only flexes forward (x < 0)
        return 1e3 * float(d @ d) + reg + flex
    r = minimize(cost, guess, method='Nelder-Mead', options=dict(xatol=1e-3, fatol=1e-9, maxiter=4000))
    return r.x, math.sqrt(max(0.0, r.fun)) / 31.6

def copy_pose(p):
    q = Pose(); q.hip_t[:] = p.hip_t
    for b in BONES: q.e[b][:] = p.e[b]
    return q

# ---- carry pose: wrists beside the box at the hold socket (chest-local) --------------------------
def carry_arm_angles(D):
    base = Pose(); out = {}
    for side, sx in (('Left', 1), ('Right', -1)):
        W = fk(D, base)
        tgt = (W['Chest'] @ np.array([sx * D['hand'][0], D['hand'][1], D['hand'][2], 1.0]))[:3]
        guess = np.array([-55.0, -sx * 20.0, -sx * 25.0, -80.0])
        v, err = solve_arm(D, base, side, tgt, guess)
        out[side] = v
    return out

# ---- crouch ------------------------------------------------------------------------------------
def crouch_legs(D, p, theta, shin_back, ankle_z=0.0):
    """plant both feet: thigh forward 'theta', shin leaning forward by 'shin_back' (deg). sets hips pos + leg/foot angles."""
    a2 = math.radians(shin_back); th = math.radians(theta)
    L1, L2 = D['ul'], D['ll']
    dy = L1 * math.cos(th) + L2 * math.cos(a2)
    dz = L1 * math.sin(th) - L2 * math.sin(a2)           # ankle is dz in FRONT of the hip joint
    hip_joint_y = D['ankle_h'] + dy
    p.hip_t[:] = (0, hip_joint_y - D['hip_dy'], ankle_z - dz)
    for s in ('Left', 'Right'):
        p.e[s + 'UpperLeg'][:] = (-theta, 0, 0)
        p.e[s + 'LowerLeg'][:] = (theta + shin_back, 0, 0)
        p.e[s + 'Foot'][:] = (-shin_back, 0, 0)

def stand_legs(p, D):
    p.hip_t[:] = (0, D['hips_y'], 0)

def rest_arms(p, D):
    z = D['rest_z']
    p.e['LeftUpperArm'][:] = (0, 0, z); p.e['RightUpperArm'][:] = (0, 0, -z)
    p.e['LeftLowerArm'][:] = (-10, 0, 0); p.e['RightLowerArm'][:] = (-10, 0, 0)

class Lib:
    def __init__(self, D, reach_target_z=None, crouch=None, lean=None):
        d = D.get('crouch_cfg', dict(z=0.30, crouch=(105.0, 40.0), lean=(25.0, 20.0, 10.0)))
        reach_target_z = d['z'] if reach_target_z is None else reach_target_z
        crouch = d['crouch'] if crouch is None else crouch
        lean = d['lean'] if lean is None else lean
        self.D = D; self.carry = carry_arm_angles(D)
        # full-crouch pose used to solve the 'reach to the ground' arm angles
        p = Pose(); rest_arms(p, D); crouch_legs(D, p, crouch[0], crouch[1])
        p.e['Spine'][0], p.e['Chest'][0], p.e['Head'][0] = lean
        self.crouch_pose = copy_pose(p); self.crouch = crouch; self.lean = lean
        self.reach = {}; self.reach_err = {}
        for side, sx in (('Left', 1), ('Right', -1)):
            tgt = np.array([sx * D['hand'][0] * 0.62, D['ankle_h'] + 0.10, reach_target_z])
            v, err = solve_arm(D, p, side, tgt, np.array([-70.0, 0.0, -sx * 8.0, -20.0]))
            self.reach[side] = v; self.reach_err[side] = err
        self.reach_target_z = reach_target_z

    # blend helpers
    def arms_to(self, p, side, a, b, k):
        v = a[side] * (1 - k) + b[side] * k; set_arm(p, side, v)

    def rest_arm_vecs(self):
        z = self.D['rest_z']; return {'Left': np.array([0, 0, z, -10.0]), 'Right': np.array([0, 0, -z, -10.0])}

    def pickup(self, t, T=2.3):
        D = self.D; p = Pose(); rest = self.rest_arm_vecs()
        down = ss(0.0, 0.62, t)                        # crouch down + reach
        up = ss(1.05, 2.0, t)                          # stand up with the object
        c = down * (1 - up)                            # crouch depth 0..1..0
        th, sb = self.crouch
        crouch_legs(D, p, th * c, sb * c)
        # lean forward while low
        s0, c0, h0 = self.lean
        p.e['Spine'][0] = s0 * c; p.e['Chest'][0] = c0 * c; p.e['Head'][0] = h0 * c - 4 * c
        for side in ('Left', 'Right'):
            if t < 1.05: v = rest[side] * (1 - down) + self.reach[side] * down
            else: v = self.reach[side] * (1 - up) + self.carry[side] * up
            set_arm(p, side, v)
        # tiny settle bob at the end
        if t > 1.95: p.hip_t[1] += 0.004 * math.sin((t - 1.95) * 25) * math.exp(-(t - 1.95) * 10)
        return p

    def putdown(self, t, T=2.3):
        return self.pickup(T - t, T)

    def carry_arms(self, p, bob=0.0):
        for side in ('Left', 'Right'):
            v = self.carry[side].copy(); v[0] += bob; set_arm(p, side, v)

    def carry_idle(self, t, T=4.0):
        p = AS.idle(t, T); self.carry_arms(p, 1.0 * math.sin(2 * math.pi * t / T)); p.e['Spine'][0] -= 1.5
        return p

    def carry_walk(self, t, T=1.0):
        p = AS.walk(t, T); self.carry_arms(p, 0.8 * math.sin(4 * math.pi * t / T)); p.e['Spine'][0] -= 2.5; p.e['Chest'][0] -= 1.0
        return p

    def carry_run(self, t, T=0.6):
        p = AS.run(t, T); self.carry_arms(p, 1.6 * math.sin(4 * math.pi * t / T)); p.e['Spine'][0] -= 4.0
        return p

    def clips(self):
        return [('PickUp', self.pickup, 2.3, False), ('PutDown', self.putdown, 2.3, False),
                ('CarryIdle', self.carry_idle, 4.0, True), ('CarryWalk', self.carry_walk, 1.0, True), ('CarryRun', self.carry_run, 0.6, True)]

if __name__ == '__main__':
    for name, D in (('EVA', EVA), ('CASUAL', CASUAL)):
        AS.BIND_HIPS_Y = D['hips_y']
        L = Lib(D)
        print(name, 'carry', {k: v.round(1) for k, v in L.carry.items()}, 'reach', {k: v.round(1) for k, v in L.reach.items()}, 'reach err (m)', {k: round(v, 3) for k, v in L.reach_err.items()})
        p = Pose(); L.carry_arms(p); W = fk(D, p)
        for s in ('Left', 'Right'): print(' wrist', s, (pos(W, s + 'Hand') - pos(W, 'Chest')).round(3))
        W = fk(D, L.pickup(1.0)); print(' crouch hips y', pos(W, 'Hips').round(3), 'feet', pos(W, 'LeftFoot').round(3), pos(W, 'RightFoot').round(3), 'hand', pos(W, 'LeftHand').round(3))
