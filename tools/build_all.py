import sys, os, shutil, json
sys.path.insert(0, os.path.dirname(__file__))
from glbkit import *
from astronaut import build_astronaut
from base_assets import *
from prop_assets import *

OUT = sys.argv[1] if len(sys.argv) > 1 else '/home/claude/mm/out'
shutil.rmtree(OUT, ignore_errors=True)
M = OUT + '/Assets/Models'
jobs = [
 ('Characters/astronaut', build_astronaut(), 'Astronaut (you), rigged'),
 ('Base/habitat', build_habitat(), 'Habitat (HAB)'),
 ('Base/airlock_suit_station', build_airlock(), 'Airlock / Suit Station (SUI)'),
 ('Base/oxygen_generator', build_ogs(), 'Oxygen Generator module (OGS)'),
 ('Base/storage_module', build_storage(), 'Storage Module (STO)'),
 ('Base/garage', build_garage(), 'Garage (GAR)'),
 ('Base/comms_antenna', build_comms(), 'Comms antenna (COM)'),
 ('Base/radiation_monitor', build_radiation(), 'Radiation Monitor post (RAD)'),
 ('Base/solar_array', build_solar(), 'Solar arrays (SOL-S)'),
 ('Vehicles/scout_rover', build_rover(), 'Scout Rover (ROV-SC)'),
 ('Vehicles/lander', build_lander(), 'Lander'),
 ('Props/battery', build_battery(), 'Battery (BAT)'),
 ('Props/seismometer_crate', build_seis_crate(), 'Seismometer crate'),
 ('Props/seismometer_backpack', build_seis_back(), 'Seismometer on your back'),
 ('Props/seismometer_deployed', build_seis_deployed(), 'Deployed seismometer body'),
 ('Props/tank_oxygen', build_tank('O'), 'Oxygen tank (TNK-O)'),
 ('Props/tank_water', build_tank('W'), 'Water tank (TNK-W)'),
]
for A, nm in zip(build_sample_rocks(), ('a', 'b', 'c')): jobs.append((f'Rocks/sample_rock_{nm}', A, f'Sample rock {nm.upper()}'))
for i, A in enumerate(build_moon_rocks()): jobs.append((f'Rocks/moon_rock_{i+1:02d}', A, f'Scattered moon rock variant {i+1}'))

rows = []
for rel, A, desc in jobs:
    p = f'{M}/{rel}.glb'; size = A.save(p)
    lo, hi = A.bounds(); d = hi - lo
    clips = ', '.join(a.name for a in A.anims) or '-'
    rows.append((rel + '.glb', desc, f'{d[0]:.2f} x {d[1]:.2f} x {d[2]:.2f}', A.tri_count(), size // 1024, clips))
    print(f'{rel:32s} {A.tri_count():6d} tris {size/1024:7.1f} KB  clips: {clips}')
json.dump(rows, open(OUT + '/rows.json', 'w'))
