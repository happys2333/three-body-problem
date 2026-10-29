"""Collect seamless CC0 materials through Poly Haven's public files API."""
from concurrent.futures import ThreadPoolExecutor
from hashlib import sha256
import json
from pathlib import Path
from urllib.parse import urlparse

from fetch_environment_assets import fetch

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/ThirdParty/EnvironmentReady/PolyHaven"
CACHE = ROOT / "Logs/asset-downloads/environment-ready"
ASSETS = ("rock_face", "thatch_roof_angled", "pine_bark")


def collect(asset):
    data = {k.lower(): v for k, v in json.loads(fetch("https://api.polyhaven.com/files/" + asset, CACHE / (asset + ".json")).read_text()).items()}
    files = []
    for kind in ("diff", "nor_gl", "rough", "ao"):
        variant = data.get("diffuse" if kind == "diff" else kind, {}).get("2k", {}).get("jpg")
        if not variant:
            raise ValueError("Missing seamless map: " + asset + "/" + kind)
        name = Path(urlparse(variant["url"]).path).name
        path = fetch(variant["url"], DEST / asset / name, variant.get("md5"))
        files.append({"kind": kind, "path": str(path.relative_to(ROOT)), "sha256": sha256(path.read_bytes()).hexdigest(), "url": variant["url"]})
    (DEST / asset / "SOURCE.txt").write_text("Source: https://polyhaven.com/a/" + asset + "\nLicense: CC0 1.0 https://polyhaven.com/license\nRetrieved: 2026-10-28\nPurpose: seamless surface material; 2K albedo, normal, roughness and AO.\n")
    print(asset + ": four 2K maps verified", flush=True)
    return {"id": asset, "source": "https://polyhaven.com/a/" + asset, "license": "CC0-1.0", "files": files}


if __name__ == "__main__":
    with ThreadPoolExecutor(max_workers=3) as pool:
        assets = list(pool.map(collect, ASSETS))
    target = ROOT / "Documentation/EnvironmentProduction/Ready/surface-sources.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps({"date": "2026-10-28", "assets": assets}, indent=2) + "\n")
