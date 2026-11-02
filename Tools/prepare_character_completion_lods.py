"""Derive two UV-preserving static prop LODs in Blender; source FBX stays immutable."""
import json
from pathlib import Path
import sys

import bpy

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets/_Project/Art/CharacterCompletion20261102'
WORK = ROOT / 'Logs/CharacterCompletion20261102/lods'


def run(identifier):
    source = ASSETS / identifier / (identifier + '_Textured.fbx')
    if identifier not in ('PapalThrone', 'OpenManuscript') or not source.is_file():
        raise ValueError('A completed static prop is required')
    folder = WORK / identifier
    folder.mkdir(parents=True, exist_ok=True)
    result = {'id': identifier, 'source': str(source.relative_to(ROOT)), 'levels': []}
    for level, ratio in ((1, .24), (2, .06)):
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=str(source))
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
        if not meshes:
            raise ValueError('FBX has no meshes')
        for obj in meshes:
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            modifier = obj.modifiers.new('UV preserving distance detail', 'DECIMATE')
            modifier.ratio = ratio
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        path = folder / (identifier + '_LOD' + str(level) + '.fbx')
        bpy.ops.export_scene.fbx(filepath=str(path), object_types={'MESH'}, bake_anim=False,
                                path_mode='STRIP', embed_textures=False, add_leaf_bones=False)
        triangles = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in meshes)
        result['levels'].append({'level': level, 'triangles': triangles, 'stagingFile': str(path)})
    (folder / 'processing.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result), flush=True)


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:]
    for identifier in args:
        run(identifier)
