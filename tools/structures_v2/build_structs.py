import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from structures import *
import preview
OUT = sys.argv[1] if len(sys.argv) > 1 else '/home/claude/work/out'
only = set(sys.argv[2:])
os.makedirs(OUT + '/Assets/Resources/Models', exist_ok=True); os.makedirs('prev', exist_ok=True)
for name, fn in JOBS:
    if only and name not in only: continue
    t = time.time(); A = fn(); size = A.save(f'{OUT}/Assets/Resources/Models/{name}.glb')
    lo, hi = A.bounds(); d = hi - lo
    print(f'{name:18s} {A.tri_count():6d} tris {size/1024:7.0f} KB  size {d.round(2)}  min {lo.round(2)} max {hi.round(2)}  {time.time()-t:.1f}s')
    preview.sheet(A, f'prev/{name}.png', title=name)
