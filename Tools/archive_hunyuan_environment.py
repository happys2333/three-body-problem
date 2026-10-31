"""Archive completed environment FBX files downloaded through the authorized Chrome UI."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Documentation/EnvironmentProduction/hunyuan-environment-2026-10-28.json"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("download", type=Path)
    parser.add_argument("--id", required=True)
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text())
    asset = next(a for a in manifest["assets"] if a["id"] == args.id)
    data = args.download.read_bytes()
    if not data.startswith(b"Kaydara FBX Binary") or len(data) < 100000:
        raise ValueError("A complete binary FBX export is required.")
    digest = hashlib.sha256(data).hexdigest()
    date = manifest.get("date", "2026-10-28")
    unity_root = manifest.get("unityRoot", "Assets/_Project/Art/HunyuanEnvironment")
    raw = ROOT / f"ArtSource/Environment/Hunyuan/{date}/{args.id}/{args.id}_PBR.fbx"
    project = ROOT / unity_root / args.id / f"{args.id}_PBR.fbx"
    for destination in (raw, project):
        if destination.exists() and hashlib.sha256(destination.read_bytes()).hexdigest() != digest:
            raise ValueError(f"A different export already exists: {destination}")
        if destination.exists():
            continue
        destination.parent.mkdir(parents=True, exist_ok=True)
        staging = ROOT / "Logs/asset-downloads" / f"{date}_{args.id}.fbx.tmp"
        staging.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(args.download, staging)
        staging.replace(destination)
    asset["files"] = {"sourceDownload": str(args.download), "rawFile": str(raw.relative_to(ROOT)),
                      "projectFile": str(project.relative_to(ROOT)), "sha256": digest, "bytes": len(data)}
    asset["state"] = "pbr_fbx_archived_pending_unity_and_art_review"
    args.manifest.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"id": args.id, **asset["files"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
