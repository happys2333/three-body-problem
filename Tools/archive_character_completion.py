"""Archive completed Hunyuan UI downloads; keep source hashes and reviewed variants."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import struct

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT/'Documentation/CharacterProduction/2026-11-02/generation-manifest.json'
UNITY = 'Assets/_Project/Art/CharacterCompletion20261102'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('download', type=Path)
    parser.add_argument('--id', required=True)
    parser.add_argument('--stage', required=True, choices=('concept', 'geometry', 'textured', 'rig'))
    parser.add_argument('--variant', default='')
    parser.add_argument('--reject', help='Preserve a rejected concept without selecting it for production.')
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text())
    planned = next(row for row in manifest['priority'] if row['id'] == args.id)
    if not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]*', args.id) or not re.fullmatch(r'[A-Za-z0-9_]*', args.variant):
        raise ValueError('Invalid asset or variant identifier')
    if args.download.suffix.lower() not in ('.png', '.fbx'):
        raise ValueError('Only completed PNG / FBX downloads are accepted')
    blob = args.download.read_bytes()
    if args.stage == 'concept':
        if not blob.startswith(b'\x89PNG\r\n\x1a\n') or len(blob) < 10000:
            raise ValueError('Incomplete PNG concept')
        width, height = struct.unpack_from('>II', blob, 16)
        if min(width, height) < 128:
            raise ValueError('Concept is too small')
        suffix = '.png'
    else:
        if not blob.startswith(b'Kaydara FBX Binary') or len(blob) < 100000:
            raise ValueError('Incomplete binary FBX download')
        suffix = '.fbx'
    if args.reject and args.stage != 'concept':
        raise ValueError('Only concept rejection is handled by this archive entry')
    name = args.id+'_'+args.stage.capitalize()+('_'+args.variant if args.variant else '')+suffix
    raw = ROOT/'ArtSource/CharacterCompletion/2026-11-02'/args.id/name
    destination = ROOT/(f'Documentation/CharacterProduction/2026-11-02/Concepts/{name}' if suffix == '.png' else f'{UNITY}/{args.id}/{name}')
    digest = hashlib.sha256(blob).hexdigest()
    for path in (raw, destination):
        if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError('Different immutable export already exists: '+str(path))
    staging = ROOT/'Logs/CharacterCompletion20261102/staging'
    staging.mkdir(parents=True, exist_ok=True)
    for path in (raw, destination):
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = staging/name
        shutil.copy2(args.download, temporary)
        temporary.replace(path)
    asset = next((a for a in manifest['assets'] if a['id'] == args.id), None)
    if asset is None:
        asset = {'id':args.id, 'kind':planned['kind'], 'files':{}, 'status':'in_production'}
        manifest['assets'].append(asset)
    entry = {'sourceDownload':str(args.download.resolve()), 'rawFile':str(raw.relative_to(ROOT)),
             'projectFile':str(destination.relative_to(ROOT)), 'sha256':digest, 'bytes':len(blob)}
    if args.reject:
        asset.setdefault('rejectedConcepts', []).append({**entry, 'reason':args.reject})
    else:
        asset['files'][args.stage] = entry
        asset['status'] = args.stage+'_downloaded_pending_unity_and_art_review'
    manifest['unityRoot'] = UNITY
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps({'id':args.id, 'stage':args.stage, **entry}, ensure_ascii=False))


if __name__ == '__main__':
    main()
