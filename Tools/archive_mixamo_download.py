"""Archive an FBX/ZIP downloaded through Chrome; no network access."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Documentation/CharacterProduction/mixamo-assets.json"
RAW = ROOT / "ArtSource/Characters/Mixamo"
UNITY = ROOT / "Assets/_Project/Characters/Mixamo/Animations"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("download", type=Path)
    parser.add_argument("--source-page", required=True)
    parser.add_argument("--id", help="Explicit ID for a single FBX, e.g. MX_TalkCalm")
    parser.add_argument("--source-name", help="Exact UI title of a single selected animation")
    parser.add_argument("--prefix", default="", help="Disambiguate a pack's repeated take names")
    args = parser.parse_args()
    path = args.download.resolve(strict=True)
    if path.suffix.lower() not in (".fbx", ".zip"):
        parser.error("Only FBX or ZIP exports are supported.")
    if args.id and not re.fullmatch(r"MX_[A-Za-z0-9]+", args.id):
        parser.error("ID must be MX_ followed by letters and digits.")
    if args.prefix and not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", args.prefix):
        parser.error("Prefix must contain letters and digits.")
    UNITY.mkdir(parents=True, exist_ok=True)
    (RAW / "Downloads").mkdir(parents=True, exist_ok=True)
    (RAW / "Motions").mkdir(parents=True, exist_ok=True)
    manifest = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else {
        "service": "Mixamo", "acquisition": "User-authorized Chrome UI downloads",
        "sourceCharacter": "Y Bot", "format": "FBX for Unity(.fbx)", "fps": 30,
        "keyframeReduction": "none", "characterMesh": "No Character / Without Skin",
        "archives": [], "clips": [],
    }
    archive_data = path.read_bytes()
    archive_hash = digest(archive_data)
    archived = RAW / "Downloads" / path.name
    if archived.exists() and digest(archived.read_bytes()) != archive_hash:
        archived = archived.with_name(archived.stem + "_" + archive_hash[:8] + archived.suffix)
    if not archived.exists():
        shutil.copy2(path, archived)
    if not any(a["sha256"] == archive_hash for a in manifest["archives"]):
        manifest["archives"].append({"file": str(archived.relative_to(ROOT)), "sourceDownload": str(path),
            "sourcePage": args.source_page, "sha256": archive_hash, "bytes": len(archive_data)})
    if path.suffix.lower() == ".zip":
        with zipfile.ZipFile(path) as archive:
            entries = [i for i in archive.infolist() if not i.is_dir() and Path(i.filename).suffix.lower() == ".fbx"]
            if not entries or sum(i.file_size for i in entries) > 250_000_000 or any(i.file_size > 50_000_000 for i in entries):
                raise ValueError("Unexpected animation archive contents or size.")
            payloads = [(Path(i.filename).name, archive.read(i)) for i in entries]
    else:
        payloads = [(path.name, archive_data)]
    added = []
    for filename, data in payloads:
        if not data.startswith(b"Kaydara FBX Binary") and b"FBXHeaderExtension" not in data[:4096]:
            raise ValueError("Not an FBX export: " + filename)
        clip_id = args.id if len(payloads) == 1 and args.id else "MX_" + args.prefix + "".join(
            token[:1].upper() + token[1:].lower() for token in re.findall(r"[A-Za-z0-9]+", Path(filename).stem))
        sha = digest(data)
        existing = next((c for c in manifest["clips"] if c["id"] == clip_id), None)
        if existing and existing["sha256"] != sha:
            # Keep distinct exports instead of silently overwriting a similarly named motion.
            clip_id += sha[:8].upper()
            existing = next((c for c in manifest["clips"] if c["id"] == clip_id), None)
        if existing:
            continue
        unity_file = UNITY / (clip_id + ".fbx")
        raw_file = RAW / "Motions" / (clip_id + ".fbx")
        if unity_file.exists() or raw_file.exists():
            raise FileExistsError(clip_id)
        unity_file.write_bytes(data)
        raw_file.write_bytes(data)
        manifest["clips"].append({"id": clip_id, "originalFilename": filename,
            "sourceName": args.source_name if len(payloads) == 1 and args.source_name else Path(filename).stem,
            "sourceArchive": str(archived.relative_to(ROOT)), "sourcePage": args.source_page,
            "unityFile": str(unity_file.relative_to(ROOT)), "rawFile": str(raw_file.relative_to(ROOT)),
            "sha256": sha, "bytes": len(data), "state": "downloaded_unity_validation_pending"})
        added.append(clip_id)
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"added": added, "totalClips": len(manifest["clips"])}))


if __name__ == "__main__":
    main()
