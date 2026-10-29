"""Archive only completed files obtained through the user-authorized Hunyuan browser UI."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Documentation/CharacterProduction/hunyuan-expansion-2026-10-28.json"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("download", type=Path)
    parser.add_argument("--id", required=True)
    parser.add_argument("--stage", choices=("concept", "geometry", "textured", "rig", "animation"), required=True)
    parser.add_argument("--kind", choices=("character", "prop"), default="character")
    parser.add_argument("--replace-reviewed", help="Reason for superseding a visually rejected export; preserve the old file first.")
    args = parser.parse_args()
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*", args.id):
        raise ValueError("Use a plain asset identifier.")
    data = args.download.read_bytes()
    if args.stage == "concept":
        if not data.startswith(b"\x89PNG\r\n\x1a\n") or len(data) < 10000:
            raise ValueError("Expected a completed PNG concept export.")
        suffix = ".png"
    else:
        if not data.startswith(b"Kaydara FBX Binary") or len(data) < 100000:
            raise ValueError("Expected a completed binary FBX export.")
        suffix = ".fbx"
    digest = hashlib.sha256(data).hexdigest()
    name = args.id + "_" + args.stage.capitalize() + suffix
    raw = ROOT / f"ArtSource/HunyuanExpansion/2026-10-28/{args.id}/{name}"
    if args.stage == "concept":
        unity = ROOT / f"Documentation/CharacterProduction/Concepts/Expansion_{args.id}.png"
    else:
        unity = ROOT / f"Assets/_Project/Art/HunyuanExpansion/{args.id}/{name}"
    manifest = json.loads(MANIFEST.read_text())
    key = "plannedCharacters" if args.kind == "character" else "props"
    records = manifest.setdefault(key, [])
    asset = next((r for r in records if r["id"] == args.id), None)
    if asset is None:
        asset = {"id": args.id, "state": "in_production"}
        records.append(asset)
    previous = asset.get("files", {}).get(args.stage)
    replacing = any(path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() != digest for path in (raw, unity))
    if replacing:
        if not args.replace_reviewed or not previous:
            raise ValueError("A different export exists; review it and use --replace-reviewed with a reason.")
        old_digest = hashlib.sha256(raw.read_bytes()).hexdigest()
        if old_digest != previous["sha256"]:
            raise ValueError("Existing source differs from the recorded export; inspect before changing it.")
        rejected = ROOT / f"ArtSource/HunyuanExpansion/2026-10-28/Rejected/{args.id}_{args.stage}_{old_digest[:12]}{suffix}"
        rejected.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(raw, rejected)
        asset.setdefault("rejectedExports", []).append({**previous, "reason": args.replace_reviewed, "preservedFile": str(rejected.relative_to(ROOT))})
    for path in (raw, unity):
        path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(args.download, path)
    entry = {"stage": args.stage, "sourceDownload": str(args.download), "rawFile": str(raw.relative_to(ROOT)),
             "projectFile": str(unity.relative_to(ROOT)), "sha256": digest, "bytes": len(data)}
    asset.setdefault("files", {})[args.stage] = entry
    asset["state"] = args.stage + "_downloaded_pending_validation"
    manifest.setdefault("events", []).append({"asset": args.id, "event": "archived_export", **entry})
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"id": args.id, **entry}, ensure_ascii=False))


if __name__ == "__main__":
    main()
