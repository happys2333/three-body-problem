"""Audit the repaired library and native evidence without changing source archives."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "Documentation/EnvironmentProduction/Ready"


def read(path):
    return json.loads((ROOT / path).read_text())


def main():
    raw = read("Documentation/EnvironmentProduction/hunyuan-environment-2026-10-28.json")
    ready = read("Documentation/EnvironmentProduction/Ready/ready-validation.json")
    errors = []
    for source in raw["assets"]:
        for kind in ("rawFile", "projectFile"):
            path = ROOT / source["files"][kind]
            if hashlib.sha256(path.read_bytes()).hexdigest() != source["files"]["sha256"]:
                errors.append("Original source changed: " + str(path.relative_to(ROOT)))
    base = read("Documentation/CharacterProduction/full-cast-integration.json")["sceneSHA256"]
    scene_hashes = {}
    for name, expected in base.items():
        actual = hashlib.sha256((ROOT / "Assets/_Project/Scenes/Traveller" / name).read_bytes()).hexdigest()
        scene_hashes[name] = actual
        if actual != expected:
            errors.append("Story scene changed: " + name)
    files = [p for p in (ROOT / "Assets/_Project/Art/EnvironmentReady").rglob("*") if p.is_file() and p.suffix != ".meta"]
    missing = [str(p.relative_to(ROOT)) for p in files if not Path(str(p) + ".meta").is_file()]
    errors.extend("Missing metadata: " + p for p in missing)
    if len(ready["assets"]) != 20 or any(not a["passed"] for a in ready["assets"]):
        errors.append("Expected twenty technically valid ready prefabs.")
    native_path = DOCS / "Native/qa.json"
    native = json.loads(native_path.read_text()) if native_path.exists() else None
    if native:
        errors.extend(native["errors"])
        if not native["passed"]:
            errors.append("Native verification did not pass.")
        if len(native["metrics"]) != 20:
            errors.append("Native run did not capture all twenty views.")
        for metric in native["metrics"]:
            if not metric.get("viewClear") or not metric["visibleLods"]:
                errors.append("Native inspection view was obstructed or empty: " + metric["asset"])
            if metric["p95FrameMs"] > 16.67:
                errors.append("Native frame budget exceeded: " + metric["asset"])
        for entry in ready["assets"]:
            if not (DOCS / "Native" / (entry["id"] + ".png")).is_file():
                errors.append("Missing native view: " + entry["id"])
        for field in ("gateClear", "portalClear", "gateWallSolid", "portalWallSolid", "gateWalkThrough", "portalWalkThrough"):
            if not native[field]:
                errors.append("Failed native physics check: " + field)
    else:
        errors.append("Native verification report is missing.")
    edit = read("Documentation/EnvironmentProduction/Ready/editmode.json")
    if edit["result"]["summary"]["passed"] != 36 or edit["result"]["summary"]["failed"]:
        errors.append("Existing editor regression did not pass all 36 checks.")
    build_path = DOCS / "preview-build.json"
    build = json.loads(build_path.read_text()) if build_path.exists() else None
    if not build or build["result"] != "Succeeded" or build["errors"]:
        errors.append("Standalone preview did not build successfully.")
    saved_path = DOCS / "saved-prefab-audit.json"
    saved = json.loads(saved_path.read_text()) if saved_path.exists() else None
    if not saved or not saved["passed"] or len(saved["assets"]) != 20:
        errors.append("Saved prefab references have not passed verification.")
    surfaces = read("Documentation/EnvironmentProduction/Ready/surface-sources.json")
    for asset in surfaces["assets"]:
        for file in asset["files"]:
            if hashlib.sha256((ROOT / file["path"]).read_bytes()).hexdigest() != file["sha256"]:
                errors.append("Surface source changed: " + file["path"])
    packed = read("Documentation/EnvironmentProduction/Ready/packed-surfaces.json")
    if not packed["passed"] or len(packed["textures"]) != 8:
        errors.append("Packed surface validation is incomplete.")
    for texture in packed["textures"]:
        if texture["maxLinearError"] > 1 / 255 + 0.00001:
            errors.append("Packed surface channel changed: " + texture["material"])
        if hashlib.sha256((ROOT / texture["path"]).read_bytes()).hexdigest() != texture["sha256"]:
            errors.append("Packed surface file changed: " + texture["path"])
    summary = {
        "date": "2026-10-28", "readyPrefabs": len(ready["assets"]),
        "scope": "Static environment dressing and exterior architecture; independent first-person art yard.",
        "status": "verified_for_static_scene_dressing" if native and native["passed"] and not errors else "validation_in_progress",
        "trianglesByLodTotal": [sum(a["trianglesByLod"][i] for a in ready["assets"]) for i in range(3)],
        "immutableSourceChecks": 40, "storyScenesUnchanged": all(scene_hashes[n] == h for n, h in base.items()),
        "sceneSHA256": scene_hashes, "unityAssetFiles": len(files), "missingMetadata": missing,
        "editorRegression": edit["result"]["summary"], "native": native, "nativeBuild": build,
        "savedPrefabReferences": saved, "photoSurfaceChecks": 12, "packedSurfaceChecks": packed,
        "gallery": "Assets/_Project/Scenes/Environment/Environment_ArtYard.unity",
        "prefabRoot": "Assets/_Project/Art/EnvironmentReady/Prefabs", "errors": errors,
        "limitations": ["Closed houses are static exterior shells; interiors and operable doors are not included.",
                        "Native performance evidence is for the art yard and this machine, not the unchanged story scenes.",
                        "Caravan supplies retain their original atlas-sensitive mesh and small decorative fasteners."]
    }
    (DOCS / "delivery.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
    with (DOCS / "READY_CATALOG.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["id", "prefab", "width_m", "height_m", "depth_m", "LOD0", "LOD1", "LOD2", "collision_triangles", "usage", "modifications"])
        for a in ready["assets"]:
            writer.writerow([a["id"], a["prefab"], *(round(a[n], 3) for n in ("width", "height", "depth")), *a["trianglesByLod"], a["collisionTriangles"], a["usage"], "; ".join(a["modifications"])])
    print(json.dumps({k: summary[k] for k in ("status", "readyPrefabs", "trianglesByLodTotal", "storyScenesUnchanged", "errors")}, ensure_ascii=False))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
