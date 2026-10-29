"""Reconcile actual downloaded files and Unity checks; never mark a queued model delivered."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "Documentation/EnvironmentProduction"
MANIFEST = DOCS / "hunyuan-environment-2026-10-28.json"


def main():
    data = json.loads(MANIFEST.read_text())
    report_path = DOCS / "hunyuan-environment-unity-validation.json"
    report = json.loads(report_path.read_text()) if report_path.exists() else {"assets": []}
    checks = {a["id"]: a for a in report["assets"]}
    errors, pending, rows = [], [], []
    checked_files = source_bytes = 0
    for asset in data["assets"]:
        files = asset.get("files")
        if not files:
            pending.append(asset["id"])
            asset["state"] = "generated_pbr_pending_browser_export"
        else:
            for key in ("rawFile", "projectFile"):
                path = ROOT / files[key]
                if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != files["sha256"]:
                    errors.append(f"Missing or changed source: {path}")
                checked_files += 1
            source_bytes += files["bytes"]
        check = checks.get(asset["id"])
        if check:
            asset["unityValidation"] = check
            review = asset.get("artReview", {}).get("status", "pending")
            asset["state"] = ("library_technical_checks_passed_art_rework_required" if review == "reviewed_requires_rework" else "library_technical_checks_passed_art_review_pending") if check["passed"] else "unity_import_check_failed"
        rows.append([asset["id"], asset["name"], asset["category"], asset["state"],
                     files.get("projectFile", "") if files else "", check.get("prefab", "") if check else "",
                     *(check.get("trianglesByLod", ["", "", ""]) if check else ["", "", ""]),
                     asset.get("artReview", {}).get("status", "pending")])
    baseline = json.loads((ROOT / "Documentation/CharacterProduction/full-cast-integration.json").read_text())["sceneSHA256"]
    scene_hashes = {}
    for name, expected in baseline.items():
        path = ROOT / "Assets/_Project/Scenes/Traveller" / name
        scene_hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        if scene_hashes[name] != expected:
            errors.append("Story scene changed: " + name)
    preview_groups = []
    for start in range(0, len(report["assets"]), 4):
        image = DOCS / f"Previews/Environment_Unity_{start // 4 + 1:02}.jpg"
        if image.exists():
            preview_groups.append({"image": str(image.relative_to(ROOT)),
                                   "leftToRight": [a["id"] for a in reversed(report["assets"][start:start + 4])]})
    library_files = [p for p in (ROOT / "Assets/_Project/Art/HunyuanEnvironment").rglob("*") if p.is_file() and p.suffix != ".meta"]
    missing_meta = [str(p.relative_to(ROOT)) for p in library_files if not Path(str(p) + ".meta").is_file()]
    errors.extend("Missing Unity metadata: " + p for p in missing_meta)
    summary = {"date": data["date"], "scope": "Generated asset library; no story-scene replacement or new native benchmark.",
               "generationGroupsSubmitted": data["submissions"], "downloadedPbrModels": sum(bool(a.get("files")) for a in data["assets"]),
               "unityPreparedModels": len(checks), "unityTechnicalChecksPassed": sum(bool(a["passed"]) for a in checks.values()),
               "visuallyReviewedModels": sum(a.get("artReview", {}).get("status", "pending") != "pending" for a in data["assets"]),
               "modelsRequiringArtRework": [a["id"] for a in data["assets"] if a.get("artReview", {}).get("status") == "reviewed_requires_rework"],
               "pendingBrowserExports": pending, "sourceFileChecks": checked_files, "sourceBytes": source_bytes,
               "unityAssetFiles": len(library_files), "missingUnityMetadata": missing_meta,
               "sceneSHA256": scene_hashes, "sceneFilesUnchanged": not any("Story scene changed" in e for e in errors),
               "errors": errors, "previews": preview_groups,
               "limitations": ["Technical import checks do not certify artistic style, historical fidelity, seamless tiling, open portals or gameplay collisions.",
                               "These library assets have not been placed in the story scenes or tested in a new native performance run."]}
    data["deliveryReport"] = str((DOCS / "hunyuan-environment-delivery.json").relative_to(ROOT))
    test_path = DOCS / "editmode-20261028.json"
    if test_path.exists():
        test = json.loads(test_path.read_text())
        summary["editModeRegression"] = {"jobId": test["job_id"], **test["result"]["summary"], "report": str(test_path.relative_to(ROOT))}
    audit_path = DOCS / "hunyuan-environment-saved-prefab-audit.json"
    if audit_path.exists():
        summary["savedPrefabAudit"] = json.loads(audit_path.read_text())
    data["status"] = "partial_library_delivery_pending_browser_exports" if pending else "library_delivery_art_rework_required"
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    (DOCS / "hunyuan-environment-delivery.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
    with (DOCS / "ENVIRONMENT_CATALOG.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["id", "name", "scene", "state", "source", "prefab", "LOD0_triangles", "LOD1_triangles", "LOD2_triangles", "art_review"])
        writer.writerows(rows)
    print(json.dumps(summary, ensure_ascii=False))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
