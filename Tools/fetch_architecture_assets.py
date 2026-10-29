#!/usr/bin/env python3
"""Collect selected architecture assets from their publishers, without paid tiers.

CC0 models and 1K maps are copied into Unity. The CS Studio archive has a
no-redistribution licence, so its files stay in a Git-ignored local-use folder.
Signed itch.io download URLs are resolved per run and never saved in the manifest.
"""
from concurrent.futures import ThreadPoolExecutor
from hashlib import sha256
from http.cookiejar import CookieJar
from pathlib import Path, PurePosixPath
from urllib.parse import urlencode, urlparse
from urllib.request import HTTPCookieProcessor, Request, build_opener, urlopen
from zipfile import ZipFile
import html
import json
import re
import tarfile
import time

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "Logs/asset-downloads/architecture"
VENDOR = ROOT / "Assets/ThirdParty/Architecture"
MANIFEST = ROOT / "Documentation/architecture-asset-manifest.json"
UA = {"User-Agent": "ThreeBodyScenePrep/1.0"}
RETRIEVED = "2026-10-26"


def fetch(url, path, expected_hash=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        for attempt in range(3):
            try:
                with urlopen(Request(url, headers=UA), timeout=45) as response:
                    data = response.read()
                temporary = path.with_name(path.name + ".partial")
                temporary.write_bytes(data)
                temporary.replace(path)
                break
            except Exception:
                if attempt == 2:
                    raise
                time.sleep(attempt + 1)
    if expected_hash and sha256(path.read_bytes()).hexdigest() != expected_hash:
        raise ValueError("Source checksum mismatch: " + str(path))
    return path


def free_itch_archive(base, filename_filter, cache_name):
    cached = CACHE / cache_name
    if cached.exists():
        return cached
    opener = build_opener(HTTPCookieProcessor(CookieJar()))

    def request(url, data=None, referer=base):
        headers = dict(UA, Referer=referer)
        if data is not None:
            headers["X-Requested-With"] = "XMLHttpRequest"
        return opener.open(Request(url, urlencode(data).encode() if data is not None else None,
                                   headers=headers), timeout=45).read()

    page = request(base).decode()
    token = html.unescape(re.search(r'name="csrf_token" value="([^"]+)"', page).group(1))
    # This is the same free-download action exposed by the publisher's page.
    generated = json.loads(request(base + "/download_url", {"csrf_token": token}))
    if "url" not in generated:
        raise ValueError("No free download offered by " + base)
    download_page = request(generated["url"]).decode()
    token = html.unescape(re.search(r'name="csrf_token" value="([^"]+)"', download_page).group(1))
    # itch.io varies attribute order; do not assume class precedes title.
    uploads = []
    sections = re.split(r'data-upload_id="(\d+)"', download_page)
    for index in range(1, len(sections), 2):
        title = re.search(r'<strong\b[^>]*\btitle="([^"]+)"', sections[index + 1])
        if title:
            uploads.append((sections[index], title.group(1)))
    selected = [(upload, html.unescape(name)) for upload, name in uploads
                if filename_filter(html.unescape(name))]
    if len(selected) != 1:
        raise ValueError("Choose exactly one free archive from: " + str([name for _, name in uploads]))
    upload_id, filename = selected[0]
    resolved = json.loads(request(base + "/file/" + upload_id,
                                  {"csrf_token": token}, generated["url"]))
    if "url" not in resolved:
        raise ValueError("Publisher did not provide " + filename)
    return fetch(resolved["url"], cached)


def extract_models(archive, folder, model_filter=None):
    copied = []
    with ZipFile(archive) as pack:
        names = pack.namelist()
        has_fbx = any(Path(name).suffix.lower() == ".fbx" for name in names)
        for name in names:
            relative = PurePosixPath(name)
            if relative.is_absolute() or ".." in relative.parts or "__MACOSX" in relative.parts:
                continue
            suffix = relative.suffix.lower()
            keep = suffix == ".fbx" if has_fbx else suffix in (".obj", ".mtl", ".blend")
            if keep and model_filter is not None:
                keep = model_filter(relative)
            # Avoid importing duplicate glTF/OBJ versions of the same texture atlas.
            is_other_format = any(part.lower() in ("gltf", "glb", "obj", "blend", "blender")
                                  for part in relative.parts[:-1])
            keep = keep or (suffix in (".png", ".jpg", ".jpeg", ".tga") and not is_other_format)
            keep = keep or (suffix in (".txt", ".md") and
                            any(term in relative.name.lower() for term in ("license", "readme", "credit")))
            if not keep or name.endswith("/"):
                continue
            target = folder.joinpath(*relative.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(pack.read(name))
            copied.append(str(target.relative_to(ROOT)))
    return copied


def source_notice(folder, author, source):
    path = folder / "SOURCE.txt"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("Author: " + author + "\nSource: " + source +
                    "\nSource page licence: CC0 1.0\nRetrieved: " + RETRIEVED +
                    "\nLicence: https://creativecommons.org/publicdomain/zero/1.0/\n", encoding="utf-8")
    return str(path.relative_to(ROOT))


def collect_pack(name, author, source, archive, model_filter=None):
    folder = VENDOR / name
    files = extract_models(archive, folder, model_filter)
    files.append(source_notice(folder, author, source))
    print(name, "copied", len(files), "files", flush=True)
    return {"name": name, "author": author, "source": source, "license": "CC0-1.0",
            "status": "downloaded", "archive_sha256": sha256(archive.read_bytes()).hexdigest(),
            "files": files}


def collect_polyhaven(asset, model):
    metadata = fetch("https://api.polyhaven.com/files/" + asset, CACHE / (asset + ".json"))
    data = json.loads(metadata.read_text())
    selected = []
    if model:
        item = data["fbx"]["1k"]["fbx"]
        selected.append(item)
        # Include required material maps, plus accessible JPG maps for Standard.
        selected.extend(item.get("include", {}).values())
    for map_name in ("Diffuse", "nor_gl", "Rough", "AO", "Metal"):
        if map_name in data and "1k" in data[map_name]:
            selected.append(data[map_name]["1k"]["jpg"])
    folder = ROOT / "Assets/ThirdParty/PolyHaven" / asset
    files = []
    urls = []
    for item in selected:
        url = item["url"]
        filename = Path(urlparse(url).path).name
        target = folder / ("textures" if not filename.lower().endswith(".fbx") else "") / filename
        fetch(url, target, item.get("sha256"))
        relative = str(target.relative_to(ROOT))
        if relative not in files:
            files.append(relative)
            urls.append(url)
    files.append(source_notice(folder, "Poly Haven contributors", "https://polyhaven.com/a/" + asset))
    print(asset, len(files), "files", flush=True)
    return {"name": asset, "author": "Poly Haven contributors", "license": "CC0-1.0",
            "source": "https://polyhaven.com/a/" + asset, "resolution": "1K",
            "status": "downloaded", "kind": "model" if model else "texture", "files": files,
            "downloads": urls}


def prepare_local_chinese_package(archive):
    folder = ROOT / "Assets/ThirdParty/LocalOnly/CSStudioChineseBuildings"
    files = []
    with tarfile.open(archive) as pack:
        for member in pack.getmembers():
            if not member.name.endswith("/pathname") or not member.isfile():
                continue
            original = PurePosixPath(pack.extractfile(member).read().decode("utf-8"))
            if original.parts[:2] != ("Assets", "AncientChina") or ".." in original.parts:
                raise ValueError("Unexpected pathname in CS Studio package")
            target = folder.joinpath(*original.parts[2:])
            prefix = member.name.rsplit("/", 1)[0]
            names = pack.getnames()
            if prefix + "/asset" in names:
                if target.suffix.lower() not in (".fbx", ".png", ".jpg", ".mat", ".prefab", ".txt"):
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(pack.extractfile(prefix + "/asset").read())
                files.append(str(target.relative_to(ROOT)))
            else:
                target.mkdir(parents=True, exist_ok=True)
            if prefix + "/asset.meta" in names:
                meta = Path(str(target) + ".meta")
                meta.write_bytes(pack.extractfile(prefix + "/asset.meta").read())
    notice = folder / "LOCAL_USE_LICENSE.txt"
    notice.write_text("CS Studio: Low Poly Chinese Style Building Set 1 [FREE]\n"
                      "https://cs-studio.itch.io/cs-building-set1\n"
                      "The publisher permits use and modification in personal and commercial games,\n"
                      "but does not permit asset redistribution or resale.\n"
                      "This entire LocalOnly folder is excluded from Git. Keep it local.\n"
                      "Retrieved: " + RETRIEVED + "\n", encoding="utf-8")
    files.append(str(notice.relative_to(ROOT)))
    return files


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    records = []
    errors = []
    jobs = [
        ("KenneyFantasyTown", "Kenney", "https://kenney.nl/assets/fantasy-town-kit",
         lambda: fetch("https://kenney.nl/media/pages/assets/fantasy-town-kit/efe948d309-1754222374/kenney_fantasy-town-kit_2.0.zip", CACHE / "kenney_fantasy-town-kit_2.0.zip")),
        ("QuaterniusMedieval", "Quaternius", "https://quaternius.itch.io/medieval-village-megakit",
         lambda: free_itch_archive("https://quaternius.itch.io/medieval-village-megakit",
                                   lambda name: "standard" in name.lower() and name.lower().endswith(".zip"),
                                   "MedievalVillageMegaKit-Standard.zip")),
        ("VVayToyekPavilion", "VVayToyek", "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free",
         lambda: free_itch_archive("https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free",
                                   lambda name: name == "Chinese Four-corner Pavilion.zip", "Chinese-Four-corner-Pavilion.zip")),
        ("KayKitMedieval", "Kay Lousberg", "https://kaylousberg.itch.io/kaykit-medieval-builder-pack",
         lambda: free_itch_archive("https://kaylousberg.itch.io/kaykit-medieval-builder-pack",
                                   lambda name: name == "KayKit Medieval Builder Pack 1.0.zip", "KayKit-Medieval-Builder-1.0.zip")),
    ]
    for name, author, source, get_archive in jobs:
        try:
            selection = (lambda path: "objects" in path.parts) if name == "KayKitMedieval" else None
            records.append(collect_pack(name, author, source, get_archive(), selection))
        except Exception as error:
            errors.append({"name": name, "error": str(error)})
            print(name, "FAILED:", str(error), flush=True)
    # This source permits use in games but forbids redistribution of the assets.
    # Retain the package in the ignored cache and unpack only into LocalOnly.
    try:
        archive = free_itch_archive("https://cs-studio.itch.io/cs-building-set1",
                                    lambda name: name == "AncientChina_set_1.unitypackage", "AncientChina_set_1.unitypackage")
        records.append({"name": "CSStudioChineseBuildings", "author": "CS Studio",
                        "source": "https://cs-studio.itch.io/cs-building-set1",
                        "license": "Custom: game use and modification allowed; no asset redistribution",
                        "status": "local_only", "cache": str(archive.relative_to(ROOT)),
                        "archive_sha256": sha256(archive.read_bytes()).hexdigest(), "files": [],
                        "local_files": prepare_local_chinese_package(archive)})
        print("CSStudioChineseBuildings prepared in Git-ignored LocalOnly folder", flush=True)
    except Exception as error:
        errors.append({"name": "CSStudioChineseBuildings", "error": str(error)})
    models = ["chinese_armchair", "chinese_stool", "chinese_screen_panels", "chinese_tea_table",
              "large_castle_door", "modular_fort_01"]
    textures = ["clay_roof_tiles", "clay_plaster", "cobblestone_pavement", "wood_planks", "stone_wall",
                "grey_roof_tiles", "red_plaster_weathered", "reed_roof_03"]
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = [(name, pool.submit(collect_polyhaven, name, name in models)) for name in models + textures]
        for name, future in futures:
            try:
                records.append(future.result())
            except Exception as error:
                errors.append({"name": name, "error": str(error)})
                print(name, "FAILED:", str(error), flush=True)
    for record in records:
        record["retrieved"] = RETRIEVED
        record["sha256"] = {name: sha256((ROOT / name).read_bytes()).hexdigest() for name in record["files"]}
        if record.get("local_files"):
            record["local_sha256"] = {name: sha256((ROOT / name).read_bytes()).hexdigest()
                                       for name in record["local_files"]}
        record["bytes"] = sum((ROOT / name).stat().st_size for name in record["files"])
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps({"records": records, "errors": errors}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Saved", len(records), "records;", len(errors), "errors", flush=True)
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
