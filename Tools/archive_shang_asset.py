"""Archive a verified Hunyuan download and update this production batch manifest."""
import argparse
import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / 'Documentation/SceneProduction/2026-11-01'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('id')
    parser.add_argument('kind', choices=['concept', 'textured', 'rig'])
    parser.add_argument('source', type=Path)
    args = parser.parse_args()
    path = DOCS / 'generation-manifest.json'
    manifest = json.loads(path.read_text())
    entry = next(a for a in manifest['assets'] if a['id'] == args.id)
    data = args.source.read_bytes()
    if args.kind == 'concept':
        assert data.startswith(b'\x89PNG\r\n\x1a\n'), 'Not a completed PNG'
        suffix = '_Concept.png'
    else:
        assert data.startswith(b'Kaydara FBX Binary') and len(data) > 100000, 'Not a completed FBX'
        suffix = '_Rig.fbx' if args.kind == 'rig' else '_PBR.fbx'
    target = ROOT / manifest['sourceRoot'] / entry['id'] / (entry['id'] + suffix)
    target.parent.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(data).hexdigest()
    if target.exists():
        assert hashlib.sha256(target.read_bytes()).hexdigest() == digest, 'Immutable archive already exists'
    else:
        shutil.copy2(args.source, target)
    info = {'rawFile': str(target.relative_to(ROOT)), 'sourceDownload': str(args.source),
            'bytes': len(data), 'sha256': digest}
    if args.kind == 'concept':
        entry['concept'] = info
        entry['state'] = 'concept_downloaded'
    else:
        project = ROOT / manifest['unitySourceRoot'] / entry['id'] / target.name
        project.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(target, project)
        info['projectFile'] = str(project.relative_to(ROOT))
        entry.setdefault('files', {})[args.kind] = info
        entry['state'] = args.kind + '_downloaded'
    manifest['updated'] = datetime.now().astimezone().isoformat()
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    imports = []
    for asset in manifest['assets']:
        f = asset.get('files', {}).get('textured', {})
        if asset['kind'] == 'character' or not f.get('projectFile'):
            continue
        imports.append({'id': asset['id'], 'category': asset['kind'], 'sizeAxis': 'height',
                        'targetSizeMetres': asset['targetHeightMetres'], 'yawDegrees': 0,
                        'files': {'projectFile': f['projectFile']}})
    (DOCS / 'unity-import-manifest.json').write_text(json.dumps({'assets': imports}, indent=2) + '\n')
    print(json.dumps({'id': entry['id'], 'kind': args.kind, 'bytes': len(data), 'archive': info['rawFile']}))


if __name__ == '__main__':
    main()
