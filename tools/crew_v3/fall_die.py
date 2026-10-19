"""Fall and Dying clips, appended to an already rigged GLB (meshes, skin and existing clips untouched).

    python3 fall_die.py ../../Assets/Resources/Models/AstronautEVA.glb ../../Assets/Resources/Models/CrewCasual.glb

Fall   (3.2 s): trip, pitch forward, catch on both hands, lie a moment, push back up to standing.
Dying  (4.0 s): hands to the helmet, stagger, sink to the knees, topple forward, lie still.
Conventions (astronaut.py): Hips x+ pitches forward, UpperArm/UpperLeg x- swings forward, LowerLeg x+ bends the knee.
Re-running replaces clips with the same name.
"""
import sys, json, struct, math
import numpy as np
import astronaut as AS
from astronaut import BONES, FPS, Pose, ss, rest


def _prone(p, f):
    """f = 0 standing .. 1 lying face down, propped on both hands."""
    p.add('Hips', x=62 * f)                                                     # chest stays propped up on the arms
    p.hip_t[1] = AS.BIND_HIPS_Y - (AS.BIND_HIPS_Y - .26) * f
    p.hip_t[2] = .55 * f
    for s in ('Left', 'Right'):
        p.add(s + 'UpperArm', x=-42 * f, z=(10 if s == 'Left' else -10) * f)   # hands down under the shoulders
        p.add(s + 'LowerArm', x=-38 * f)
        p.add(s + 'Hand', x=30 * f)
        p.add(s + 'UpperLeg', x=24 * f)                                         # legs lie flat behind
        p.add(s + 'LowerLeg', x=6 * f); p.add(s + 'Foot', x=25 * f)
    p.add('Head', x=-35 * f)                                                    # look ahead, not into the ground


def fall(t, T=3.2):
    p = Pose(); rest(p)
    trip = ss(0, .3, t) * (1 - ss(.5, .8, t))
    p.add('RightUpperLeg', x=28 * trip); p.add('RightLowerLeg', x=40 * trip)   # foot caught behind
    p.add('LeftUpperLeg', x=-22 * trip)
    for s in ('Left', 'Right'): p.add(s + 'UpperArm', x=-55 * trip)            # arms fly forward
    down = min(1.0, max(0.0, (t - .15) / .65)) ** 2                              # accelerating fall
    up = ss(1.55, 3.0, t)                                                        # push back up
    f = down * (1 - up)
    _prone(p, f)
    p.add('LeftLowerLeg', x=70 * down * (1 - down)); p.add('LeftUpperLeg', x=-25 * down * (1 - down))   # standing knee gives way
    if 1.55 < t < 3.0:                                                           # knees tuck while getting up
        k = math.sin(math.pi * (t - 1.55) / 1.45)
        for s in ('Left', 'Right'): p.add(s + 'UpperLeg', x=-70 * k); p.add(s + 'LowerLeg', x=95 * k)
        p.hip_t[1] -= .03 * k
    if .8 < t < 1.55:                                                            # impact bounce, then breathing
        d = t - .8; p.hip_t[1] += math.exp(-d * 9) * math.sin(d * 26) * .03
        p.add('Chest', x=2 * math.sin(d * 6))
    return p


def dying(t, T=4.0):
    p = Pose(); rest(p)
    grab = ss(0, .5, t) * (1 - ss(1.6, 2.2, t))                                 # hands to the helmet
    for s, sz in (('Left', 1), ('Right', -1)):
        p.add(s + 'UpperArm', x=-95 * grab, z=sz * 28 * grab)
        p.add(s + 'LowerArm', x=-115 * grab)
    sway = math.sin(t * 5.5) * ss(.2, .6, t) * (1 - ss(1.4, 1.9, t))
    p.add('Head', y=12 * sway, x=-10 * grab); p.add('Hips', z=4 * sway); p.add('Spine', x=10 * grab)
    p.hip_t[0] = .04 * sway
    kneel = ss(1.1, 2.0, t)                                                      # sink to the knees
    for s in ('Left', 'Right'):
        p.add(s + 'UpperLeg', x=-8 * kneel); p.add(s + 'LowerLeg', x=95 * kneel); p.add(s + 'Foot', x=35 * kneel)
    p.hip_t[1] = AS.BIND_HIPS_Y - .43 * kneel
    topple = min(1.0, max(0.0, (t - 2.0) / .8)) ** 2                             # fall forward from the knees
    p.add('Hips', x=80 * topple, z=6 * topple)
    p.hip_t[1] -= (AS.BIND_HIPS_Y - .43 - .17) * topple
    p.hip_t[2] = .35 * topple
    for s, sz in (('Left', 1), ('Right', -1)):
        p.add(s + 'UpperLeg', x=8 * topple); p.add(s + 'LowerLeg', x=-80 * topple); p.add(s + 'Foot', x=-20 * topple)   # legs straighten out behind
        p.add(s + 'UpperArm', x=20 * topple, z=sz * 12 * topple)               # arms limp along the body
        p.add(s + 'LowerArm', x=-10 * topple)
    p.add('Head', y=55 * topple, x=-15 * topple)                                 # face turned to the side
    if t > 2.8:
        d = t - 2.8; p.hip_t[1] += math.exp(-d * 8) * math.sin(d * 24) * .03    # impact, then still
    return p


CLIPS = [('Fall', fall, 3.2, False), ('Dying', dying, 4.0, False)]


def load(path):
    b = open(path, 'rb').read()
    jl = struct.unpack('<I', b[12:16])[0]
    j = json.loads(b[20:20 + jl])
    bl = struct.unpack('<I', b[20 + jl:24 + jl])[0]
    return j, bytearray(b[28 + jl:28 + jl + bl])


def save(j, bin_, path):
    j['buffers'][0]['byteLength'] = len(bin_)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(bin_) % 4: bin_.append(0)
    out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_))
    out += struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bin_), 0x004E4942) + bytes(bin_)
    open(path, 'wb').write(out)


def add_clips(path):
    j, bin_ = load(path)
    idx = {n['name']: i for i, n in enumerate(j['nodes']) if 'name' in n}
    AS.BIND_HIPS_Y = float(j['nodes'][idx['Hips']]['translation'][1])   # same rest height the existing clips use

    def add_acc(arr, atype):
        while len(bin_) % 4: bin_.append(0)
        j['bufferViews'].append({'buffer': 0, 'byteOffset': len(bin_), 'byteLength': arr.nbytes})
        bin_.extend(arr.tobytes())
        j['accessors'].append({'bufferView': len(j['bufferViews']) - 1, 'componentType': 5126, 'count': int(arr.shape[0]), 'type': atype})
        return len(j['accessors']) - 1

    names = {c[0] for c in CLIPS}
    j['animations'] = [a for a in j.get('animations', []) if a['name'] not in names]
    for name, fn, T_, loop in CLIPS:
        n = int(round(T_ * FPS)); times = (np.arange(n + 1) / FPS).astype(np.float32)
        poses = [AS.pose_dict(fn(min(float(t), T_), T_)) for t in times]
        tacc = add_acc(times.reshape(-1, 1), 'SCALAR')
        j['accessors'][tacc]['min'] = [float(times.min())]; j['accessors'][tacc]['max'] = [float(times.max())]
        samplers, channels = [], []
        for b in BONES:
            q = np.stack([ps[b][1] for ps in poses]).astype(np.float32)
            for i in range(1, len(q)):
                if float(q[i] @ q[i - 1]) < 0: q[i] = -q[i]
            samplers.append({'input': tacc, 'output': add_acc(q, 'VEC4'), 'interpolation': 'LINEAR'})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': idx[b], 'path': 'rotation'}})
        ta = np.stack([ps['Hips'][0] for ps in poses]).astype(np.float32)
        samplers.append({'input': tacc, 'output': add_acc(ta, 'VEC3'), 'interpolation': 'LINEAR'})
        channels.append({'sampler': len(samplers) - 1, 'target': {'node': idx['Hips'], 'path': 'translation'}})
        j['animations'].append({'name': name, 'samplers': samplers, 'channels': channels})
    save(j, bin_, path)
    print(path.split('/')[-1], 'clips:', [a['name'] for a in j['animations']])


if __name__ == '__main__':
    for p in sys.argv[1:]: add_clips(p)
