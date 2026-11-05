"""Prepare textured Hunyuan GLB LODs for Unity without a second model download."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
import trimesh


def prepare(source, lod_directory, destination, identifier, dimensions, albedo_size):
    source_scene = trimesh.load(source, force='scene')
    source_mesh = source_scene.to_mesh()
    if len(source_scene.geometry) != 1 or source_mesh.visual.kind != 'texture':
        raise ValueError('Review multi-material or untextured sources before preparing them.')
    bounds = source_mesh.bounds
    origin = np.array([bounds.mean(axis=0)[0], bounds[0, 1], bounds.mean(axis=0)[2]])
    scale = np.asarray(dimensions) / source_mesh.extents
    transform = np.eye(4)
    transform[:3, :3] = np.diag(scale)
    transform[:3, 3] = -origin * scale
    destination.mkdir(parents=True, exist_ok=True)
    maps = destination / 'Textures'
    maps.mkdir(exist_ok=True)
    material = source_mesh.visual.material
    albedo = material.baseColorTexture.convert('RGB').resize((albedo_size, albedo_size), Image.Resampling.LANCZOS)
    albedo.save(maps / 'Albedo.png')
    normal = material.normalTexture.convert('RGB').resize((1024, 1024), Image.Resampling.LANCZOS)
    vectors = np.asarray(normal, dtype=np.float32) / 127.5 - 1
    vectors /= np.maximum(np.linalg.norm(vectors, axis=2, keepdims=True), 1e-6)
    Image.fromarray(np.clip((vectors + 1) * 127.5, 0, 255).astype(np.uint8)).save(maps / 'Normal.png')
    packed = material.metallicRoughnessTexture.convert('RGB').resize((512, 512), Image.Resampling.LANCZOS)
    red = packed.getchannel('B')
    zero = Image.new('L', red.size, 0)
    smoothness = Image.fromarray(255 - np.asarray(packed.getchannel('G')))
    Image.merge('RGBA', (red, zero, zero, smoothness)).save(maps / 'MetallicSmoothness.png')
    rows = []
    for level in range(3):
        path = lod_directory / f'{identifier}_LOD{level}.glb'
        mesh = trimesh.load(path, force='scene').to_mesh()
        mesh.apply_transform(transform)
        if not np.isfinite(mesh.vertices).all() or len(mesh.visual.uv) != len(mesh.vertices):
            raise ValueError('Invalid geometry or UVs: ' + str(path))
        text = trimesh.exchange.obj.export_obj(mesh, include_normals=True, include_texture=True,
                                              include_color=False, write_texture=False)
        text = '\n'.join(line for line in text.splitlines() if not line.startswith(('mtllib ', 'usemtl '))).rstrip() + '\n'
        output = destination / f'LOD{level}.obj'
        output.write_text(text)
        rows.append({'level': level, 'triangles': len(mesh.faces), 'vertices': len(mesh.vertices),
                     'bytes': output.stat().st_size, 'bounds': mesh.bounds.tolist()})
    result = {'id': identifier, 'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
              'sourceBytes': source.stat().st_size, 'sourceTriangles': len(source_mesh.faces),
              'dimensions': list(dimensions), 'textureSizes': {'albedo': albedo_size, 'normal': 1024, 'mask': 512}, 'lods': rows,
              'assetBytes': sum(p.stat().st_size for p in [*(destination.glob('LOD*.obj')), *(maps.glob('*.png'))])}
    (destination / 'prepared.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('lod_directory', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--id', required=True)
    parser.add_argument('--dimensions', nargs=3, type=float, required=True)
    parser.add_argument('--albedo-size', type=int, default=1024, choices=[1024, 2048])
    args = parser.parse_args()
    prepare(args.source, args.lod_directory, args.destination, args.id, args.dimensions, args.albedo_size)
