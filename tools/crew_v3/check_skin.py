"""CPU-skin the rigged GLB with the clip poses and render contact sheets (no Unity needed)."""
import sys; sys.path.insert(0, '/home/claude/work')
import numpy as np, math
from PIL import Image, ImageDraw
import astronaut as AS, poses_extra as PX, rig_eva as R
from astronaut import BONES
from glbkit import qmat
from render import render

def skinned_groups(path, clip_fn, t, T, hold_box=None, hips_y=0.93):
    j, b = R.load(path); AS.BIND_HIPS_Y = hips_y
    pose = AS.pose_dict(clip_fn(t, T))
    Wb = R.bind_world(); Wp = {}
    for n, (par, tr, q) in R.SK.items():
        M = np.eye(4); trans = pose['Hips'][0] if n == 'Hips' else tr
        M[:3, 3] = trans
        if n in pose: M[:3, :3] = qmat(pose[n][1])
        Wp[n] = (Wp[par] if par else np.eye(4)) @ M
    skin = {n: Wp[n] @ np.linalg.inv(Wb[n]) for n in Wb}
    groups = []
    for k, p in enumerate(j['meshes'][0]['primitives']):
        P = R.read_acc(j, b, p['attributes']['POSITION']).astype(float)
        J = R.read_acc(j, b, p['attributes']['JOINTS_0']).astype(int); Wt = R.read_acc(j, b, p['attributes']['WEIGHTS_0']).astype(float)
        I = R.read_acc(j, b, p['indices']).reshape(-1, 3).astype(int)
        Ph = np.c_[P, np.ones(len(P))]; out = np.zeros_like(P)
        for c in range(4):
            Ms = np.stack([skin[BONES[i]] for i in range(len(BONES))])        # (17,4,4)
            out += Wt[:, c:c + 1] * np.einsum('nij,nj->ni', Ms[J[:, c]], Ph)[:, :3]
        groups.append((out, I, k))
    if hold_box:
        # box centred on Socket_Hold (chest-local), carried with the chest
        c = (Wp['Chest'] @ np.array([*R.SOCK['Socket_Hold'][1], 1.0]))[:3]
        h = hold_box / 2
        V = np.array([[x, y, z] for x in (-h, h) for y in (-h, h) for z in (-h, h)]) + c
        faces = [(0,1,3),(0,3,2),(4,6,7),(4,7,5),(0,4,5),(0,5,1),(2,3,7),(2,7,6),(0,2,6),(0,6,4),(1,5,7),(1,7,3)]
        groups.append((V, np.array(faces), 2))
    return groups

if __name__ == '__main__':
    path, out = sys.argv[1], sys.argv[2]
    lib = PX.Lib(PX.EVA); AS.BIND_HIPS_Y = 0.93
    from crew import lie_pose
    tests = [('Walk .0', AS.walk, .0, 1.0, None), ('Walk .25', AS.walk, .25, 1.0, None), ('Run .15', AS.run, .15, .6, None),
             ('PickUp .5', lib.pickup, .5, 2.3, None), ('PickUp 1.0', lib.pickup, 1.0, 2.3, .38), ('PickUp 1.5', lib.pickup, 1.5, 2.3, .38),
             ('PickUp 2.3', lib.pickup, 2.3, 2.3, .38), ('CarryWalk .25', lib.carry_walk, .25, 1.0, .38), ('CarryRun .15', lib.carry_run, .15, .6, .38),
             ('Wave 1.0', AS.wave, 1.0, 2.4, None), ('Interact .7', AS.interact, .7, 1.6, None), ('Idle', AS.idle, 1.0, 4.0, None)]
    ims = []
    for name, fn, t, T, box in tests:
        g = skinned_groups(path, fn, t, T, box)
        a = render(g, (35, 0), size=420, scale=210, center=np.array([0, .9, 0])); b2 = render(g, (90, 0), size=420, scale=210, center=np.array([0, .9, 0]))
        W = Image.new('RGB', (840, 420)); W.paste(a, (0, 0)); W.paste(b2, (420, 0)); ImageDraw.Draw(W).text((8, 8), name, fill=(255, 255, 0)); ims.append(W)
    cols = 2; rows = (len(ims) + 1) // 2
    S = Image.new('RGB', (840 * cols, 420 * rows))
    for i, im in enumerate(ims): S.paste(im, ((i % cols) * 840, (i // cols) * 420))
    S.save(out); print('saved', out)
