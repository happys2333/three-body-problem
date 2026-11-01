"""Author closed, bevelled guard equipment; run with Blender --background --python.

No character skeleton or downloaded source is edited. TBM1 outputs use the
project's existing static-mesh interchange format, with Wood/Metal/Leather slots.
"""
import json
import math
import struct
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'ArtSource/Guards/2026-11-01/Equipment'
SLOTS = ['Wood', 'Metal', 'Leather']
MATS = {}


def material(name):
    if name not in MATS:
        MATS[name] = bpy.data.materials.new(name)
    return MATS[name]


def finish(obj, name, slot, bevel=0, smooth=False):
    obj.name = name
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Custom closed blade/shield shells must face outward before beveling.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.materials.append(material(slot))
    if bevel:
        mod = obj.modifiers.new('Rounded worn edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for face in obj.data.polygons:
        face.use_smooth = smooth
    return obj


def box(name, position, size, slot, bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, name, slot, bevel)


def cylinder(name, position, radius, length, slot, vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                      depth=length, location=position)
    return finish(bpy.context.object, name, slot, .002, True)


def cord(name, points, radius, slot='Leather'):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    spline = curve.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for p, co in zip(spline.points, points):
        p.co = (*co, 1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object, name, slot, smooth=True)


def blade(name, outline, thickness=.016):
    n = len(outline)
    verts = [(x, y, z) for y in (-thickness / 2, thickness / 2) for x, z in outline]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, n * 2))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.select_set(True)
    return finish(obj, name, 'Metal', .003)


def polearm(identifier, ge=False):
    parts = [cylinder('Hardwood shaft', (0, 0, 1.01), .018, 2.02, 'Wood')]
    parts.append(cylinder('Leather grip', (0, 0, 1.04), .021, .28, 'Leather'))
    for z in (.035, .89, 1.19, 1.96):
        parts.append(cylinder('Forged binding collar', (0, 0, z), .023, .026, 'Metal'))
    points = []
    for i in range(193):
        t = i / 192
        a = t * math.pi * 2 * 12
        points.append((math.cos(a) * .023, math.sin(a) * .023, .91 + t * .26))
    parts.append(cord('Spiral hand binding', points, .0023))
    if ge:
        parts.append(blade('Transverse bronze ge blade',
                           [(-.13, 1.94), (-.12, 2.035), (.13, 2.065),
                            (.40, 2.015), (.18, 1.95), (.035, 1.92)]))
        parts.append(box('Bronze socket', (0, 0, 1.98), (.052, .048, .16), 'Metal'))
    else:
        parts.append(blade('Leaf spear head',
                           [(-.018, 1.93), (-.072, 2.11), (0, 2.37),
                            (.072, 2.11), (.018, 1.93)]))
        parts.append(cylinder('Tapered spear socket', (0, 0, 1.98), .025, .16, 'Metal'))
    return save(identifier, parts)


def shield(identifier, kite=False):
    parts = []
    if kite:
        outline = [(-.31, .40), (0, .49), (.31, .40), (.28, -.06),
                   (0, -.57), (-.28, -.06)]
        # A solid convex shell with an individually bevelled metal border.
        n = len(outline)
        vertices = [(0, -.065, 0)] + [(x, 0, z) for x, z in outline]
        vertices += [(x, .035, z) for x, z in outline]
        faces = [(0, i + 1, (i + 1) % n + 1) for i in range(n)]
        faces += [tuple(range(n + 1, n * 2 + 1))]
        faces += [(i + 1, i + n + 1, (i + 1) % n + n + 1,
                   (i + 1) % n + 1) for i in range(n)]
        mesh = bpy.data.meshes.new('Kite shield solid shell')
        mesh.from_pydata(vertices, [], faces)
        obj = bpy.data.objects.new('Kite shield solid shell', mesh)
        bpy.context.collection.objects.link(obj)
        parts.append(finish(obj, obj.name, 'Leather', .008))
        edge = [(x, -.006, z) for x, z in outline] + [(outline[0][0], -.006, outline[0][1])]
        parts.append(cord('Iron kite shield edge', edge, .013, 'Metal'))
        parts.append(box('Heraldic vertical cross', (0, -.075, .03), (.046, .018, .61), 'Metal', .004))
        parts.append(box('Heraldic cross arm', (0, -.075, .16), (.32, .018, .046), 'Metal', .004))
    else:
        for i in range(9):
            x = (i - 4) * .066
            y = .045 * (x / .30) ** 2
            parts.append(box('Separate curved wooden plank', (x, y, 0),
                             (.064, .035, .85), 'Wood', .006))
            parts.append(box('Leather plank facing', (x, y - .023, 0),
                             (.064, .012, .83), 'Leather', .004))
        for z in (-.431, .431):
            parts.append(cord('Bronze top and bottom rim',
                              [(x, .045 * (x / .30) ** 2 - .025, z)
                               for x in np.linspace(-.30, .30, 25)], .014, 'Metal'))
        for x in (-.302, .302):
            parts.append(box('Bronze side rim', (x, .017, 0), (.022, .035, .88), 'Metal'))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=(0, -.04, .02))
        boss = bpy.context.object
        boss.scale = (.078, .035, .10)
        parts.append(finish(boss, 'Raised bronze shield boss', 'Metal', smooth=True))
        for x in (-.25, .25):
            for z in np.linspace(-.35, .35, 6):
                bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6,
                    radius=.009, location=(x, -.012, float(z)))
                parts.append(finish(bpy.context.object, 'Riveted shield rim', 'Metal', smooth=True))
    for z in (-.16, .15):
        parts.append(cord('Leather arm strap', [(-.14, .04, z), (-.12, .12, z),
                          (.12, .12, z), (.14, .04, z)], .015))
    parts.append(cord('Rear hand grip', [(0, .035, -.09), (0, .16, -.06),
                      (0, .16, .07), (0, .035, .10)], .021, 'Wood'))
    return save(identifier, parts)


def save(identifier, parts):
    folder = OUT / identifier
    folder.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = identifier
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.008)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / (identifier + '.blend')))
    counts = []
    for level, ratio in enumerate((1, .48, .18)):
        duplicate = obj.copy()
        duplicate.data = obj.data.copy()
        bpy.context.collection.objects.link(duplicate)
        bpy.ops.object.select_all(action='DESELECT')
        duplicate.select_set(True)
        bpy.context.view_layer.objects.active = duplicate
        if ratio < 1:
            modifier = duplicate.modifiers.new('Equipment distance detail', 'DECIMATE')
            modifier.ratio = ratio
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        counts.append(write_mesh(duplicate, folder / (identifier + '_LOD' + str(level) + '.meshbin')))
        bpy.data.objects.remove(duplicate, do_unlink=True)
    bpy.data.objects.remove(obj, do_unlink=True)
    (folder / 'processing.json').write_text(json.dumps({'id': identifier,
        'authoredLocally': True, 'materials': SLOTS, 'trianglesByLod': counts,
        'scope': 'Static equipment meshes; character attachment and contact checked in Unity.'}, indent=2) + '\n')
    print(identifier, counts, flush=True)
    return counts


def write_mesh(obj, path):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    mesh = obj.data
    mesh.update()
    points, normals, uv, indices, surfaces, cache = [], [], [], [], [], {}
    coords = mesh.uv_layers.active.data
    for face in mesh.polygons:
        surfaces.append(SLOTS.index(mesh.materials[face.material_index].name))
        for loop_id in face.loop_indices:
            loop = mesh.loops[loop_id]
            v = mesh.vertices[loop.vertex_index]
            n = v.normal if face.use_smooth else face.normal
            t = coords[loop_id].uv[:]
            key = (loop.vertex_index, tuple(round(x, 6) for x in t),
                   tuple(round(x, 5) for x in n))
            if key not in cache:
                cache[key] = len(points)
                points.append(v.co[:]); normals.append(n[:]); uv.append(t)
            indices.append(cache[key])
    with path.open('wb') as stream:
        stream.write(struct.pack('<4siii', b'TBM1', len(points), len(indices), len(SLOTS)))
        for data, dtype in ((points, '<f4'), (normals, '<f4'), (uv, '<f4'),
                            (indices, '<i4'), (surfaces, '<i4')):
            np.asarray(data, dtype=dtype).tofile(stream)
    return len(indices) // 3


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    polearm('ShangBronzeGe', ge=True)
    polearm('BronzeSpear')
    polearm('MedievalSteelSpear')
    shield('ShangLeatherShield')
    shield('MedievalKiteShield', kite=True)
