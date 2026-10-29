"""Archive a complete character FBX obtained through the user's browser."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Documentation/CharacterProduction/crowd-model-sources.json"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("download", type=Path)
    parser.add_argument("--id", required=True)
    parser.add_argument("--title", required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*", args.id):
        raise ValueError("Use a plain character identifier.")
    data = args.download.read_bytes()
    if not data.startswith(b"Kaydara FBX Binary") or len(data) < 100000:
        raise ValueError("Expected a completed binary character FBX.")
    digest = hashlib.sha256(data).hexdigest()
    raw = ROOT / f"ArtSource/Characters/Crowd/Mixamo/{args.id}.fbx"
    unity = ROOT / f"Assets/_Project/Characters/Crowd/Sources/{args.id}/{args.id}.fbx"
    for path in (raw, unity):
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError(f"Different source already exists: {path}")
        shutil.copy2(args.download, path)
    manifest = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else {
        "provider": "Adobe Mixamo", "acquisition": "User-authorized Chrome UI downloads; complete T-pose characters with skin and textures.",
        "license": "Adobe/Mixamo content terms; not CC0.", "models": []}
    entry = {"id": args.id, "sourceTitle": args.title,
             "sourcePage": "https://www.mixamo.com/#/?page=1&type=Character",
             "downloadFilename": args.download.name, "format": "FBX for Unity, T-pose",
             "rawFile": str(raw.relative_to(ROOT)), "unityFile": str(unity.relative_to(ROOT)),
             "sha256": digest, "bytes": len(data), "state": "downloaded_pending_unity_validation"}
    manifest["models"] = [m for m in manifest["models"] if m["id"] != args.id] + [entry]
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(entry, ensure_ascii=False))


if __name__ == "__main__":
    main()
