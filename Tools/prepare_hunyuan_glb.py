"""Preserve a browser-downloaded PBR GLB and derive a Unity FBX without losing maps.

Only consumes a completed local file. No network or browser access.
"""
import argparse
import hashlib
import io
import json
import shutil
import struct
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('download', type=Path)
    parser.add_argument('--id', required=True)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--blender', default='/Applications/Blender.app/Contents/MacOS/Blender')
    args = parser.parse_args()
    blob = args.download.read_bytes()
    magic, version, length = struct.unpack_from('<4sII', blob)
    if magic != b'glTF' or version != 2 or length != len(blob):
        raise ValueError('A complete GLB v2 file is required')
    json_length, json_kind = struct.unpack_from('<II', blob, 12)
    if json_kind != 0x4E4F534A:
        raise ValueError('Missing GLB JSON')
    document = json.loads(blob[20:20 + json_length])
    offset = 20 + json_length
    binary_length, binary_kind = struct.unpack_from('<II', blob, offset)
    if binary_kind != 0x004E4942:
        raise ValueError('Missing GLB binary chunk')
    binary = blob[offset + 8:offset + 8 + binary_length]
    manifest = json.loads(args.manifest.read_text())
    entry = next(a for a in manifest['assets'] if a['id'] == args.id)
    raw_dir = ROOT / 'ArtSource/Environment/Hunyuan' / manifest['date'] / args.id
    project_dir = ROOT / manifest['unityRoot'] / args.id
    maps = project_dir / 'Textures'
    raw_dir.mkdir(parents=True, exist_ok=True)
    maps.mkdir(parents=True, exist_ok=True)
    raw = raw_dir / (args.id + '_PBR.glb')
    if raw.exists() and raw.read_bytes() != blob:
        raise ValueError('Different immutable source already exists')
    if not raw.exists():
        shutil.copy2(args.download, raw)

    def image_for(texture):
        image = document['images'][document['textures'][texture['index']]['source']]
        view = document['bufferViews'][image['bufferView']]
        begin = view.get('byteOffset', 0)
        return Image.open(io.BytesIO(binary[begin:begin + view['byteLength']])).convert('RGB')

    if len(document.get('materials', [])) != 1:
        raise ValueError('Explicit multi-material mapping is required')
    material = document['materials'][0]
    pbr = material['pbrMetallicRoughness']
    image_for(pbr['baseColorTexture']).save(maps / (args.id + '_Albedo.png'))
    image_for(material['normalTexture']).save(maps / (args.id + '_Normal.png'))
    packed = image_for(pbr['metallicRoughnessTexture'])
    packed.getchannel('B').save(maps / (args.id + '_Metallic.png'))
    packed.getchannel('G').save(maps / (args.id + '_Roughness.png'))
    staging = ROOT / 'Logs/EnvironmentReady20261031/glb-conversion'
    staging.mkdir(parents=True, exist_ok=True)
    fbx = staging / (args.id + '_PBR.fbx')
    script = staging / (args.id + '.py')
    script.write_text('import bpy\n'
        'bpy.ops.object.select_all(action="SELECT")\n'
        'bpy.ops.object.delete(use_global=False)\n'
        f'bpy.ops.import_scene.gltf(filepath={str(raw)!r})\n'
        f'bpy.ops.export_scene.fbx(filepath={str(fbx)!r},use_selection=False,object_types={{"MESH"}},'
        'add_leaf_bones=False,bake_anim=False,path_mode="STRIP",embed_textures=False)\n')
    subprocess.run([args.blender, '--background', '--factory-startup', '--threads', '4',
                    '--python-exit-code', '1', '--python', str(script)], check=True)
    destination = project_dir / fbx.name
    temporary = project_dir / (fbx.name + '.tmp')
    shutil.copy2(fbx, temporary)
    temporary.replace(destination)
    shutil.copy2(fbx, raw_dir / (args.id + '_Derived.fbx'))
    entry['files'] = dict(sourceDownload=str(args.download), rawFile=str(raw.relative_to(ROOT)),
        projectFile=str(destination.relative_to(ROOT)), sha256=hashlib.sha256(blob).hexdigest(),
        bytes=len(blob), sourceFormat='GLB', projectSha256=hashlib.sha256(fbx.read_bytes()).hexdigest())
    entry['conversion'] = 'Local Blender GLB to FBX; glTF B metallic/G roughness split losslessly; original GLB retained'
    entry['state'] = 'pbr_glb_archived_derived_fbx_pending_unity_and_art_review'
    args.manifest.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'id': args.id, **entry['files']}))


if __name__ == '__main__':
    main()
