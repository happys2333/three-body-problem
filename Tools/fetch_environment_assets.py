#!/usr/bin/env python3
"""Fetch the CC0 photographic environment sources used by the two outdoor scenes.

Only model geometry and material maps are imported. No third-party scripts,
render-pipeline packages, account actions or paid downloads are involved.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import date
from hashlib import md5, sha256
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import Request, urlopen
import json
import time

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/ThirdParty/Environment/PolyHaven"
CACHE = ROOT / "Logs/asset-downloads/environment"
MODELS = ("rock_face_01", "rock_face_02", "namaqualand_boulders_01",
          "shrub_03", "grass_medium_01", "tree_small_02")
SURFACES = ("aerial_rocks_02", "aerial_grass_rock", "brown_mud_dry", "rocky_terrain_02")
HEADERS = {"User-Agent": "ThreeBodyScenePrep/1.0"}


def fetch(url, path, checksum=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        for attempt in range(3):
            try:
                with urlopen(Request(url, headers=HEADERS), timeout=60) as response:
                    data = response.read()
                temp = path.with_suffix(path.suffix + ".partial")
                temp.write_bytes(data)
                temp.replace(path)
                break
            except Exception:
                if attempt == 2:
                    raise
                time.sleep(attempt + 1)
    if checksum and md5(path.read_bytes()).hexdigest() != checksum:
        raise ValueError("Source checksum mismatch: " + str(path))
    return path


def collect(asset):
    metadata = fetch("https://api.polyhaven.com/files/" + asset, CACHE / (asset + ".json"))
    data = json.loads(metadata.read_text())
    chosen = []
    if asset in MODELS:
        chosen.append(data["fbx"]["1k"]["fbx"])
    # Multiple material regions, e.g. bark, branch and leaf atlases, are kept.
    for name, resolutions in data.items():
        if not any(term in name.lower() for term in ("diff", "nor_gl", "alpha", "rough", "ao")):
            continue
        if not isinstance(resolutions, dict):
            continue
        variants = resolutions.get("2k", resolutions.get("1k", {}))
        if not isinstance(variants, dict):
            continue
        item = variants.get("png" if "alpha" in name.lower() else "jpg")
        if item:
            chosen.append(item)
    files = []
    for item in chosen:
        filename = Path(urlparse(item["url"]).path).name
        target = DEST / asset / (filename if filename.endswith(".fbx") else "textures/" + filename)
        fetch(item["url"], target, item.get("md5"))
        files.append({"path": str(target.relative_to(ROOT)), "url": item["url"],
                      "bytes": target.stat().st_size, "sha256": sha256(target.read_bytes()).hexdigest()})
    notice = DEST / asset / "SOURCE.txt"
    notice.write_text("Source: https://polyhaven.com/a/" + asset +
                      "\nLicense: CC0 1.0 https://polyhaven.com/license\nRetrieved: " + str(date.today()) +
                      "\nGeometry: original FBX; maps: 2K. Materials adapted to Unity Built-in.\n")
    print(asset + ": " + str(len(files)) + " files", flush=True)
    return {"id": asset, "source": "https://polyhaven.com/a/" + asset,
            "license": "CC0-1.0", "kind": "model" if asset in MODELS else "terrain texture",
            "resolution": "2K", "files": files}


def main():
    with ThreadPoolExecutor(max_workers=4) as pool:
        assets = list(pool.map(collect, MODELS + SURFACES))
    path = ROOT / "Documentation/environment-asset-manifest.json"
    path.write_text(json.dumps({"retrieved": str(date.today()), "assets": assets,
                               "bytes": sum(f["bytes"] for a in assets for f in a["files"])},
                              indent=2, ensure_ascii=False) + "\n")
    print("Saved " + str(path), flush=True)


if __name__ == "__main__":
    main()
