"""
suit_clips.py - suit-dock clips shared by AstronautEVA.glb and CrewCasual.glb (Milestone 06).

  HelmetOff (2.6 s): hands up to the helmet sides (grip at 0.8 s), lift it clear (to 1.5 s), lower it in front of the chest
                     (to 2.2 s), hold. Astronaut.cs detaches the helmet at HelmetGripAt and carries it between the hands.
  HelmetOn  (2.6 s): HelmetOff reversed (helmet seats on the head at 2.6 - 0.8 = 1.8 s).
  WalkBack  (1.0 s): the walk cycle played backwards (stepping back out of / into the docked suit).

Wrist targets are in root space (feet at y=0, facing +Z) for the EVA helmet (centre ~ (0, 1.74, 0.03), radius ~0.235).
"""
import math, numpy as np
import astronaut as AS
from astronaut import Pose
import poses_extra as PX

# wrist waypoints (Left side; Right is mirrored) and the times they are reached
KEYS = [(0.0, None), (0.42, (0.55, 1.36, 0.16)), (0.8, (0.30, 1.80, 0.17)), (1.4, (0.28, 1.90, 0.26)), (1.85, (0.26, 1.72, 0.42)), (2.3, (0.24, 1.38, 0.42))]   # elbows stay bent < ~140 deg
GRIP_AT = 0.8
T_OFF = 2.6

def _euler_xyz(Rm):
    """rotation matrix -> (x, y, z) degrees for qe() (q = qz * qy * qx, i.e. R = Rz Ry Rx)"""
    b = math.asin(max(-1.0, min(1.0, -Rm[2, 0])))
    a = math.atan2(Rm[2, 1], Rm[2, 2]); c = math.atan2(Rm[1, 0], Rm[0, 0])
    return np.degrees([a, b, c])

def _arm_ik(D, side, target):
    """analytic 2-bone IK in the chest frame (rest torso): the elbow is placed on the side the pole vector points to
    (outward, a bit down and back), so the solution is unique and continuous - no flips between frames."""
    sx = 1 if side == 'Left' else -1
    base = Pose(); AS.rest(base); W = PX.fk(D, base)
    S = W['Chest'][:3, :3] @ np.array([sx * D['sh_x'], D['sh_y'], 0.0]) + W['Chest'][:3, 3]
    L1, L2 = D['ua'], D['la']
    d = target - S; dist = np.linalg.norm(d); dist = min(max(dist, abs(L1 - L2) + 1e-3), L1 + L2 - 1e-3); dn = d / np.linalg.norm(d)
    pole = np.array([sx * 0.85, -0.35, -0.45]); pole = pole - dn * (pole @ dn); pole /= np.linalg.norm(pole)
    a = (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist)                 # law of cosines at the shoulder
    E = S + dn * (L1 * a) + pole * (L1 * math.sqrt(max(0.0, 1 - a * a)))
    T = S + dn * dist
    yax = -(E - S); yax /= np.linalg.norm(yax)                               # bones point down their local -Y
    fwd = (T - E); fwd -= yax * (fwd @ yax)                                  # where the forearm leans off the upper-arm line
    ref = np.array([0.0, 0.25, 1.0]); ref -= yax * (ref @ yax)                # stable 'forward' when the arm is nearly straight
    zax = fwd + ref / (np.linalg.norm(ref) + 1e-12) * 0.03
    zax /= np.linalg.norm(zax)                                               # elbow flex (x < 0) swings the forearm toward local +Z
    xax = np.cross(yax, zax); xax /= np.linalg.norm(xax); zax = np.cross(xax, yax)
    ua = _euler_xyz(np.stack([xax, yax, zax], 1))
    inner = math.degrees(math.acos(max(-1.0, min(1.0, ((S - E) @ (T - E)) / (L1 * L2)))))
    v = np.array([ua[0], ua[1], ua[2], -(180.0 - inner)])
    p = Pose(); AS.rest(p); PX.set_arm(p, side, v)
    return v, float(np.linalg.norm(PX.pos(PX.fk(D, p), side + 'Hand') - T))

def _path(D):
    """per-frame arm angles from analytic IK along the smooth wrist path through KEYS (continuous, no flips)"""
    base = Pose(); AS.rest(base); W = PX.fk(D, base)
    rest_w = {s: PX.pos(W, s + 'Hand') for s in ('Left', 'Right')}
    times = np.linspace(0, KEYS[-1][0], 70)
    kt = [k[0] for k in KEYS]
    out = {s: [] for s in ('Left', 'Right')}
    rest_v = {'Left': np.array([0.0, 0.0, 7.0, -10.0]), 'Right': np.array([0.0, 0.0, -7.0, -10.0])}
    for side, sx in (('Left', 1), ('Right', -1)):
        pts = [rest_w[side]] + [np.array([sx * k[1][0], k[1][1], k[1][2]]) for k in KEYS[1:]]
        for t in times:
            i = min(int(np.searchsorted(kt, t, 'right')) - 1, len(kt) - 2)
            f = AS.ss(kt[i], kt[i + 1], t)
            tgt = pts[i] * (1 - f) + pts[i + 1] * f
            v, err = _arm_ik(D, side, tgt)
            if t < 0.25:                                                     # ease out of the exact rest pose
                k = AS.ss(0.0, 0.25, t); v = rest_v[side] * (1 - k) + v * k
            out[side].append(v)
    # wrists: from the grip on, bend the hands inward toward each other so the palms cradle the helmet's sides
    hands = {s: [] for s in ('Left', 'Right')}
    for k, t in enumerate(times):
        p = Pose(); AS.rest(p)
        for side in out: PX.set_arm(p, side, out[side][k])
        Wk = PX.fk(D, p)
        wl, wr = PX.pos(Wk, 'LeftHand'), PX.pos(Wk, 'RightHand')
        w = AS.ss(0.5, 0.8, t)
        for side, other in (('Left', wr), ('Right', wl)):
            Rla = Wk[side + 'LowerArm'][:3, :3]; wrist = PX.pos(Wk, side + 'Hand')
            fore = -Rla[:, 1]                                                   # forearm direction (bones point down -Y)
            inward = other - wrist; inward /= np.linalg.norm(inward) + 1e-9
            d = fore * (1 - 0.5 * w) + inward * (0.5 * w); d /= np.linalg.norm(d)
            y = -d; x = Rla[:, 0] - y * (Rla[:, 0] @ y); x /= np.linalg.norm(x); z = np.cross(x, y)
            hands[side].append(_euler_xyz(Rla.T @ np.stack([x, y, z], 1)))
    return times, {s: np.c_[np.array(out[s]), np.array(hands[s])] for s in out}

def clips(D):
    times, A = _path(D)
    def arms(t):
        t = min(t, times[-1]); return {s: np.array([np.interp(t, times, A[s][:, c]) for c in range(A[s].shape[1])]) for s in A}
    def helmet_off(t, T=T_OFF):
        p = Pose(); AS.rest(p)
        for side, v in arms(t).items(): PX.set_arm(p, side, v[:4]); p.e[side + 'Hand'][:] = v[4:7]
        k1 = AS.ss(0.0, 0.8, t); k2 = AS.ss(0.8, 1.4, t); k3 = AS.ss(1.4, 2.3, t)
        p.add('Head', x=10 * k1 * (1 - k3) - 4 * k2 * (1 - k3))        # duck as the helmet lifts
        p.add('Spine', x=-4 * k2 * (1 - k3) + 5 * k3)                    # lean back, then forward to look at it
        p.add('LeftUpperLeg', x=-1); p.add('RightUpperLeg', x=-1); p.add('LeftLowerLeg', x=2); p.add('RightLowerLeg', x=2)
        return p
    def helmet_on(t, T=T_OFF): return helmet_off(T - t, T)
    def walk_back(t, T=1.0): return AS.walk(T - t, T)
    return [('HelmetOff', helmet_off, T_OFF, False), ('HelmetOn', helmet_on, T_OFF, False), ('WalkBack', walk_back, 1.0, True)]

if __name__ == '__main__':
    for name, D in (('EVA', PX.EVA), ('CASUAL', PX.CASUAL)):
        AS.BIND_HIPS_Y = D['hips_y']; times, A = _path(D)
        for t in (0.8, 1.4, 1.85, 2.3):
            p = Pose(); AS.rest(p)
            for side in A: PX.set_arm(p, side, np.array([np.interp(t, times, A[side][:, c]) for c in range(4)]))
            W = PX.fk(D, p); print(name, t, 'L wrist', PX.pos(W, 'LeftHand').round(3), 'R wrist', PX.pos(W, 'RightHand').round(3))
