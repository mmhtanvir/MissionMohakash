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
KEYS = [(0.0, None), (0.8, (0.27, 1.62, 0.03)), (1.4, (0.27, 1.84, 0.14)), (1.85, (0.26, 1.78, 0.46)), (2.3, (0.24, 1.38, 0.42))]
GRIP_AT = 0.8
T_OFF = 2.6

def _path(D):
    """per-frame arm angles: the wrists follow a smooth path through KEYS, solved with IK every sample (continuous)"""
    base = Pose(); AS.rest(base); W = PX.fk(D, base)
    rest_w = {s: PX.pos(W, s + 'Hand') for s in ('Left', 'Right')}
    times = np.linspace(0, KEYS[-1][0], 47)
    out = {s: [] for s in ('Left', 'Right')}
    for side, sx in (('Left', 1), ('Right', -1)):
        pts = [rest_w[side]] + [np.array([sx * k[1][0], k[1][1], k[1][2]]) for k in KEYS[1:]]
        kt = [k[0] for k in KEYS]
        g = np.array([0.0, 0.0, sx * 7.0, -10.0])
        for t in times:
            i = min(int(np.searchsorted(kt, t, 'right')) - 1, len(kt) - 2)
            f = AS.ss(kt[i], kt[i + 1], t)
            tgt = pts[i] * (1 - f) + pts[i + 1] * f
            if t == 0: v = g
            else: v, err = PX.solve_arm(D, base, side, tgt, g)
            out[side].append(v); g = v
    return times, {s: np.array(v) for s, v in out.items()}

def clips(D):
    times, A = _path(D)
    def arms(t):
        t = min(t, times[-1]); return {s: np.array([np.interp(t, times, A[s][:, c]) for c in range(4)]) for s in A}
    def helmet_off(t, T=T_OFF):
        p = Pose(); AS.rest(p)
        for side, v in arms(t).items(): PX.set_arm(p, side, v)
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
