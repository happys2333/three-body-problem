"""Prepare metre-scale Shang assets, retaining raw FBX/PBR archives.

Blender --background --python Tools/prepare_shang_capital.py -- [asset IDs]
Inputs are exported from saved Unity candidate prefabs, not guessed FBX axes.
"""
import importlib.util
import json
import math
import statistics
import sys
from pathlib import Path

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'ArtSource/ShangCapital/Prepared/2026-11-01'
spec = importlib.util.spec_from_file_location('base_pipeline', ROOT / 'Tools/prepare_environment_meshes.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
BUDGETS = {
    'ShangWatchtower': (65000, 16000, 3500),
    'ShangCourtyardHall': (75000, 18000, 4000),
    'ShangCastingFurnace': (30000, 7500, 1600),
    'ShangWoodCart': (45000, 11000, 2400),
    'ShangStorageJar': (20000, 5000, 1000),
    'ShangGrainBasket': (28000, 6500, 1400),
    'ShangWoodFootbridge': (60000, 15000, 3200),
    'ShangVillageWell': (35000, 8500, 1800),
}


def rebuild_straight_bridge(source):
    # The generated mesh has raised, fused side masses across its walking axis.
    # Author a straight load-bearing structure rather than burying that mesh.
    bpy.data.objects.remove(source, do_unlink=True)
    parts = []

    def timber(name, location, size):
        bpy.ops.mesh.primitive_cube_add(size=1, location=location)
        obj = bpy.context.object
        obj.name = name
        obj.dimensions = size
        base.activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.materials.append(bpy.data.materials['Wood'])
        bevel = obj.modifiers.new('Worn timber edges', 'BEVEL')
        bevel.width = .012
        bevel.segments = 3
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        parts.append(obj)

    for side in (-1, 1):
        timber('Continuous bearer', (0, side * .66, .055), (7.6, .19, .11))
        for i in range(8):
            x = -3.62 + i * (7.24 / 7)
            timber('Square railing post', (x, side * 1.015, .66), (.135, .135, 1.28))
            for height in (.62, 1.14):
                # Two tight rope turns, retained as real geometry at close range.
                for k in range(36):
                    a = k * math.pi / 9
                    b = (k + 1) * math.pi / 9
                    parts.append(base.cylinder_between(
                        (x + .099 * math.cos(a), side * 1.015 + .099 * math.sin(a), height + k * .0007),
                        (x + .099 * math.cos(b), side * 1.015 + .099 * math.sin(b), height + (k + 1) * .0007),
                        .009, 'Thatch', sides=8))
        for height in (.63, 1.15):
            parts.append(base.cylinder_between((-3.72, side * 1.015, height), (3.72, side * 1.015, height), .047, 'Wood', sides=32))
    for i in range(31):
        timber('Separate deck plank', (-3.68 + i * (7.36 / 30), 0, .125 + .002 * math.sin(i * 17)), (.235, 2.2, .11))
    obj = base.join(parts, 'Straight timber footbridge')
    base.activate(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.008)
    bpy.ops.object.mode_set(mode='OBJECT')
    base.normalize(obj)
    return obj


def rebuild_well_frame(obj):
    lo, hi = base.bounds(obj)
    h, w = hi[2] - lo[2], hi[0] - lo[0]
    posts = []
    for side in (-1, 1):
        points = [v.co for v in obj.data.vertices if .45*h < v.co.z < .78*h and side*v.co.x > .22*w]
        if not points:
            raise ValueError('Cannot locate original well posts')
        posts.append((statistics.median(p.x for p in points), statistics.median(p.y for p in points)))
    base.weld_and_crop(obj, top=.36)
    cx, cy = sum(p[0] for p in posts)/2, sum(p[1] for p in posts)/2
    base.remove_region(obj, lambda p: (p.x-cx)**2 + (p.y-cy)**2 < .65**2 and p.z > .10*h)
    parts = [obj]
    for x, y in posts:
        parts.append(base.cylinder_between((x, y, .18), (x, y, 2.92), .115, 'Wood', sides=64))
        for i in range(4):
            bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=10, location=(x, y, 2.63+i*.022), major_radius=.12, minor_radius=.013)
            ring = bpy.context.object; ring.data.materials.append(bpy.data.materials['Thatch']); parts.append(ring)
    (ax, ay), (bx, by) = posts
    cx, cy = (ax+bx)/2, (ay+by)/2
    parts.append(base.cylinder_between((ax-.22, ay, 2.78), (bx+.22, by, 2.78), .13, 'Wood', sides=64))
    for i in range(5):
        bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=10, location=(cx+(i-2)*.022, cy, 2.78), major_radius=.135, minor_radius=.013, rotation=(0, math.pi/2, 0))
        rope = bpy.context.object; rope.data.materials.append(bpy.data.materials['Thatch']); parts.append(rope)
    parts.append(base.cylinder_between((cx, cy, 2.76), (cx, cy, 1.98), .018, 'Thatch', sides=16))
    bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=.235, depth=.34, location=(cx, cy, 1.47))
    bucket = bpy.context.object; bucket.data.materials.append(bpy.data.materials['Wood'])
    bm = bmesh.new(); bm.from_mesh(bucket.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z > .5], context='FACES')
    bm.to_mesh(bucket.data); bm.free(); base.activate(bucket)
    solid = bucket.modifiers.new('Wood bucket thickness', 'SOLIDIFY'); solid.thickness=.025
    bpy.ops.object.modifier_apply(modifier=solid.name); parts.append(bucket)
    for z in (1.34, 1.58):
        bpy.ops.mesh.primitive_torus_add(major_segments=96, minor_segments=12, location=(cx, cy, z), major_radius=.237, minor_radius=.014)
        hoop = bpy.context.object; hoop.data.materials.append(bpy.data.materials['Thatch']); parts.append(hoop)
    for i in range(16):
        a, b = math.pi*i/16, math.pi*(i+1)/16
        parts.append(base.cylinder_between((cx+.23*math.cos(a), cy, 1.64+.34*math.sin(a)), (cx+.23*math.cos(b), cy, 1.64+.34*math.sin(b)), .014, 'Thatch', sides=12))
    result = base.join(parts, 'Rope suspended wood bucket well')
    base.normalize(result, height=3)
    return result


def process(entry):
    identifier = entry['id']
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    obj = base.read_mesh(WORK / 'Input' / (identifier + '.meshbin'))
    base.normalize(obj)
    base.weld_and_crop(obj)
    for face in obj.data.polygons:
        face.material_index = base.SLOTS['OriginalSupplies']
    changes = ['Original UV atlas/PBR preserved; coincident vertices welded; dimensions in metres.']
    if identifier == 'ShangCourtyardHall':
        # The source consistently invents late-style green curled ridge trim.
        # Remove the entire roof, retain the doors/walls, and build a thick roof.
        base.weld_and_crop(obj, top=.64)
        obj = base.roof_for(obj, {'id': identifier, 'height': 5.0})
        base.normalize(obj, height=5.0)
        changes.append('Generated curved green roof removed; straight pitched timber and thick thatch rebuilt.')
    elif identifier == 'ShangWoodFootbridge':
        obj = rebuild_straight_bridge(obj)
        changes.append('Generated bridge rejected after native visual review: fused raised masses obstructed its walking axis. Rebuilt separate bevelled deck planks, bearers, paired wooden rails, posts and rope lashings; 7.6 x 2.2 x 1.3 m. Raw model retained for provenance.')
    elif identifier == 'ShangVillageWell':
        obj = rebuild_well_frame(obj)
        changes.append('Invented metal crank and entire upper frame/bucket removed. Rebuilt timber supports, rope bindings and hollow wooden bucket, retaining the generated stone well ring.')
    folder = WORK / 'Output' / identifier
    folder.mkdir(parents=True, exist_ok=True)
    base.activate(obj)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / (identifier + '_Working.blend')))
    counts = []
    for level, budget in enumerate(BUDGETS[identifier]):
        lower = obj.copy()
        lower.data = obj.data.copy()
        bpy.context.collection.objects.link(lower)
        base.activate(lower)
        triangles = sum(len(f.vertices) - 2 for f in lower.data.polygons)
        if triangles > budget:
            dec = lower.modifiers.new('UV preserving collapse LOD', 'DECIMATE')
            dec.ratio = budget / triangles
            dec.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=dec.name)
        counts.append(base.write_mesh(lower, folder / ('LOD' + str(level) + '.meshbin')))
        bpy.data.objects.remove(lower, do_unlink=True)
    lo, hi = base.bounds(obj)
    info = {'id': identifier, 'sourcePrefab': entry['sourcePrefab'], 'modifications': changes,
            'materials': base.MATERIALS, 'trianglesByLod': counts,
            'width': float(hi[0] - lo[0]), 'height': float(hi[2] - lo[2]), 'depth': float(hi[1] - lo[1]),
            'usage': 'Static exterior / prop; bridge has a separate tested walk surface.'}
    (folder / 'processing.json').write_text(json.dumps(info, indent=2) + '\n')
    print(json.dumps({'id': identifier, 'triangles': counts}), flush=True)


if __name__ == '__main__':
    sources = json.loads((WORK / 'sources.json').read_text())['assets']
    ids = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    for source in sources:
        if not ids or source['id'] in ids:
            process(source)
