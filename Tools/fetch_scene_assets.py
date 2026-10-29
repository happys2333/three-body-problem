#!/usr/bin/env python3
"""Fetch the selected CC0 scene assets and OFL font, retaining source records."""
from concurrent.futures import ThreadPoolExecutor
from hashlib import sha256
from pathlib import Path
from urllib.request import Request, urlopen
from zipfile import ZipFile
import json

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "Logs/asset-downloads"
VENDOR = ROOT / "Assets/ThirdParty"
CACHE.mkdir(parents=True, exist_ok=True)
records = []


def fetch(url, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        with urlopen(Request(url, headers={"User-Agent": "ThreeBodyScenePrep/1.0"}), timeout=90) as response:
            path.write_bytes(response.read())
    return path


PACKS = {
    "KenneyCastle": (
        "castle", "https://kenney.nl/assets/castle-kit",
        "https://kenney.nl/media/pages/assets/castle-kit/a395102d20-1711543616/kenney_castle-kit.zip",
        None,
    ),
    "KenneyNature": (
        "nature", "https://kenney.nl/assets/nature-kit",
        "https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip",
        {"grass", "grass_large", "rock_largeA", "rock_largeB", "rock_largeC", "rock_smallA",
         "rock_smallB", "rock_tallA", "rock_tallB", "tree_oak", "tree_oak_dark", "tree_pineRoundA",
         "plant_bush", "log", "campfire_logs", "tent_detailedOpen"},
    ),
}

for name, (cache_key, source, url, selection) in PACKS.items():
    archive = fetch(url, CACHE / (cache_key + ".zip"))
    extracted = []
    with ZipFile(archive) as pack:
        for member in pack.namelist():
            local = Path(member)
            lower = member.lower()
            if local.name == "License.txt":
                target = VENDOR / name / "License.txt"
            elif "fbx" in lower and local.suffix.lower() == ".fbx" and (selection is None or local.stem in selection):
                target = VENDOR / name / "Models" / local.name
            elif "fbx" in lower and local.suffix.lower() == ".png" and "textures/" in lower:
                target = VENDOR / name / "Textures" / local.name
            else:
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(pack.read(member))
            extracted.append(str(target.relative_to(ROOT)))
    records.append({"name": name, "author": "Kenney", "license": "CC0-1.0", "source": source,
                    "download": url, "archive_sha256": sha256(archive.read_bytes()).hexdigest(), "files": extracted})
    print(name, len(extracted), "files", flush=True)

downloads = []
for cache_key, asset in (("ground", "dry_riverbed_rock"), ("stone", "sandstone_blocks_05"), ("boulder", "namaqualand_boulder_02")):
    metadata = fetch("https://api.polyhaven.com/files/" + asset, CACHE / (cache_key + ".json"))
    data = json.loads(metadata.read_text())
    assets = [data[map_name]["1k"]["jpg"] for map_name in ("Diffuse", "nor_gl", "Rough")]
    if cache_key == "boulder":
        assets.append(data["fbx"]["1k"]["fbx"])
    files = []
    for item in assets:
        target = VENDOR / "PolyHaven" / asset / item["url"].rsplit("/", 1)[-1]
        downloads.append((item["url"], target))
        files.append(str(target.relative_to(ROOT)))
    records.append({"name": asset, "author": "Poly Haven contributors", "license": "CC0-1.0",
                    "source": "https://polyhaven.com/a/" + asset, "resolution": "1K", "files": files})

font_base = "https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/"
downloads += [
    (font_base + "OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf", VENDOR / "NotoSansCJK/NotoSansCJKsc-Regular.otf"),
    (font_base + "LICENSE", VENDOR / "NotoSansCJK/OFL.txt"),
    ("https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt", VENDOR / "PolyHaven/CC0-1.0.txt"),
]
with ThreadPoolExecutor(max_workers=6) as pool:
    for path in pool.map(lambda item: fetch(*item), downloads):
        print(path.relative_to(ROOT), path.stat().st_size, flush=True)
records.append({"name": "Noto Sans CJK SC Regular", "author": "Noto Project contributors",
                "license": "SIL Open Font License 1.1", "source": "https://github.com/notofonts/noto-cjk",
                "files": ["Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf", "Assets/ThirdParty/NotoSansCJK/OFL.txt"]})
for record in records:
    record["retrieved"] = "2026-10-26"
    record["sha256"] = {name: sha256((ROOT / name).read_bytes()).hexdigest() for name in record["files"]}
(ROOT / "Documentation/asset-manifest.json").write_text(json.dumps(records, indent=2, ensure_ascii=False) + "\n")
print("Source manifest saved.", flush=True)
