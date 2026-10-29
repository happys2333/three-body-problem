"""Reconcile the authorized first-scene integration without rewriting the earlier library audit."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "Documentation/GobiIntegration"


def read(relative):
    return json.loads((DOCS / relative).read_text())


def main():
    files = []
    for path, baseline in read("baseline.json")["files"].items():
        actual = hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
        expected_change = path.endswith("01_GobiJourney.unity")
        files.append({"path": path, "before": baseline, "after": actual,
                      "expectedChange": expected_change, "passed": (actual != baseline) == expected_change})
    layout = read("layout.json")
    dressing = read("Native/dressing/report.json")
    journey = read("Native/journey/report.json")
    scene = read("saved-scene-audit.json")
    editmode = read("editmode.json")["data"]["result"]["summary"]
    build = read("build.json")
    assets = list((ROOT / "Assets/_Project/Art/EnvironmentReady/GobiDressing").glob("*.asset"))
    assets += [ROOT / "Assets/_Project/Editor/GobiEnvironmentDressing.cs",
               ROOT / "Assets/_Project/Scripts/Development/GobiDressingPlayerCheck.cs"]
    missing = [str(path.relative_to(ROOT)) for path in assets if not Path(str(path) + ".meta").exists()]
    errors = []
    if not all(entry["passed"] for entry in files):
        errors.append("Protected file scope differs from the baseline.")
    if missing:
        errors.append("Missing Unity metadata.")
    if not (layout["passed"] and dressing["passed"] and journey["passed"] and editmode["failed"] == 0):
        errors.append("A verification report failed.")
    if not (scene["completeLods"] and scene["completeMaterials"] and scene["firstPersonCamera"] and not scene["dirty"] and not scene["playing"]):
        errors.append("Saved scene state is incomplete.")
    if build["result"] != "Succeeded" or build["errors"]:
        errors.append("Native player build failed.")
    report = {"passed": not errors, "scope": "01_GobiJourney landscape integration", "files": files,
              "placements": layout["instances"], "assetTypes": layout["assetTypes"],
              "editmodePassed": editmode["passed"], "nativeGobiChecks": dressing["checks"],
              "nativeJourneyChecks": journey["checks"], "metresWalked": dressing["metresWalked"],
              "gpu": dressing["gpu"], "worstP95Ms": max(m["p95Ms"] for m in dressing["metrics"]),
              "missingMetadata": missing, "errors": errors}
    (DOCS / "delivery.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
