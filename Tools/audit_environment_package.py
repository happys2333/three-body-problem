"""Verify Unity package payloads and GUID closure without importing over a project."""
import argparse
import hashlib
import json
import re
import tarfile
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('package', type=Path)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    groups = {}
    references = set()
    # Streaming avoids repeatedly decompressing a large package for each GUID.
    with tarfile.open(args.package, 'r|gz') as archive:
        for member in archive:
            name = member.name.removeprefix('./')
            match = re.fullmatch(r'([a-f0-9]{32})/(pathname|asset.meta|asset)', name)
            if not member.isfile() or not match:
                continue
            guid, kind = match.groups()
            row = groups.setdefault(guid, {'guid': guid, 'bytes': 0, 'sha256': hashlib.sha256(b'').hexdigest()})
            data = archive.extractfile(member).read()
            if kind == 'pathname':
                path = data.decode().rstrip('\x00\r\n')
                if not path.startswith('Assets/') or '..' in Path(path).parts:
                    raise ValueError('Unexpected asset path: ' + path)
                row['path'] = path
            elif kind == 'asset.meta':
                if b'guid: ' + guid.encode() not in data:
                    raise ValueError('GUID folder and metadata disagree: ' + guid)
                row['metadataVerified'] = True
            else:
                row.update(bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
            if kind == 'asset.meta' or data.startswith(b'%YAML'):
                references.update(m.decode() for m in re.findall(rb'guid:\s*([a-f0-9]{32})', data))
    rows = sorted(groups.values(), key=lambda r: r.get('path', ''))
    if any('path' not in r or not r.get('metadataVerified') for r in rows):
        raise ValueError('Incomplete package entry')
    guids = {r['guid'] for r in rows}
    unresolved = sorted(g for g in references if g not in guids and not g.startswith('0000000000000000'))
    if len(guids) != len(rows):
        raise ValueError('Duplicate package GUID')
    result = {'passed': not unresolved, 'package': str(args.package), 'bytes': args.package.stat().st_size,
              'sha256': hashlib.sha256(args.package.read_bytes()).hexdigest(),
              'entries': len(rows), 'unresolvedGuids': unresolved, 'assets': rows,
              'scope': 'Archive contents and serialized GUID reference closure; not an independent empty-project import test.'}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k: result[k] for k in ('passed', 'entries', 'bytes', 'unresolvedGuids')}))
    if unresolved:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
