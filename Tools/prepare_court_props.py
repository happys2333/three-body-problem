"""Original feather standard, bamboo petition and hu tablet. Run in Blender."""
import importlib.util
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('court_geometry', ROOT / 'Tools/prepare_era_guard_weapons.py')
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)
g.OUT = ROOT / 'ArtSource/CourtAudience/2026-11-01'
g.SLOTS = ['Wood', 'Metal', 'Feather', 'Tip']


def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def feather(angle, length, layer, index):
    start = Vector((0, .012 * layer, .52))
    axis = Vector((math.sin(angle), 0, math.cos(angle)))
    across = Vector((axis.z, 0, -axis.x))
    points, faces = [], []
    for i in range(15):
        t = i / 14
        width = .068 * math.sin(math.pi * t) ** .65 + .002
        centre = start + axis * (length * t)
        centre.y += .025 * math.sin(math.pi * t)
        for side in (-1, 1):
            points.append(tuple(centre + across * width * side))
        if i:
            j = (i - 1) * 2
            faces.append((j, j + 1, j + 3, j + 2))
    mesh = bpy.data.meshes.new('Layered feather vane')
    mesh.from_pydata(points, [], faces)
    obj = bpy.data.objects.new('Layered feather vane', mesh)
    bpy.context.collection.objects.link(obj)
    g.finish(obj, obj.name, 'Tip' if index % 5 == 0 else 'Feather', smooth=True)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new('Feather thickness', 'SOLIDIFY')
    mod.thickness = .007
    bpy.ops.object.modifier_apply(modifier=mod.name)
    rib = [tuple(start + axis * (length * t) + Vector((0, -.012, .0))) for t in (0, .25, .5, .75, .98)]
    return [obj, g.cord('Feather central quill', rib, .0028, 'Tip')]


def fan():
    reset()
    parts = [g.cylinder('Long hardwood fan staff', (0, 0, .19), .018, 2.36, 'Wood', 48)]
    for z in (-.85, -.13, .02, .43, .58):
        parts.append(g.cylinder('Cast bronze staff collar', (0, 0, z), .024, .026, 'Metal', 32))
    for layer, count in ((0, 25), (1, 17)):
        for i in range(count):
            a = -1.05 + i / (count - 1) * 2.10
            length = (1.46 - .48 * abs(math.sin(a))) * (1 if layer == 0 else .84)
            parts += feather(a, length, layer, i)
    parts.append(g.box('Bronze feather mounting shoe', (0, -.01, .57), (.18, .055, .19), 'Metal', .015))
    g.save('RoyalFeatherFan', parts)


def petition():
    reset()
    parts = []
    for i in range(12):
        x = (i - 5.5) * .035
        parts.append(g.box('Separate bound bamboo slip', (x, 0, .0), (.033, .29, .014), 'Wood', .003))
        for j in range(5):
            # Decorative incisions, not purported readable ancient writing.
            if (i * 7 + j) % 3:
                parts.append(g.box('Incised tally stroke', (x, -.10 + j * .042, .008), (.016, .0015, .001), 'Tip', .0003))
    for y in (-.09, .09):
        parts.append(g.cord('Binding cord around the slips', [(-.216,y,.013),(.216,y,.013),(.216,y,-.013),(-.216,y,-.013),(-.216,y,.013)], .004, 'Tip'))
    parts.append(g.box('Small bronze binding clasp', (.16, -.09, .018), (.025, .024, .008), 'Metal', .003))
    g.save('BambooPetition', parts)


def hu():
    reset()
    parts = [g.box('Curved ceremonial wooden tablet', (0,0,0), (.095,.37,.016), 'Wood', .012)]
    for y in (-.165,.165):
        parts.append(g.box('Tablet bronze end binding', (0,y,.0), (.097,.012,.018), 'Metal', .003))
    g.save('CeremonialHu', parts)


if __name__ == '__main__':
    fan(); petition(); hu()
    (g.OUT / 'provenance.json').write_text(json.dumps({'authoredLocally':True, 'source':'Tools/prepare_court_props.py', 'reference':'Original palace props inspired by long-handled ceremonial fans; no historical Shang reconstruction claimed.'},indent=2)+'\n')
