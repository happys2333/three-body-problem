"""Run opt-in checks in the real macOS development player, one GPU workload at a time."""
import argparse
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CASES = {
    "crowd": "-traveller-crowd-check",
    "characters": "-traveller-character-check",
    "journey": "-traveller-entry-check",
    "capitalWalk": "-traveller-capital-check",
    "performance": "-traveller-benchmark",
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", choices=CASES)
    parser.add_argument("--output", default="Logs/FullCrowdQA/final-native")
    args = parser.parse_args()
    output = ROOT / args.output
    output.mkdir(parents=True, exist_ok=True)
    binary = ROOT / "Builds/macOS/ThreeBodyTraveller.app/Contents/MacOS/Three Body Problem"
    for name, flag in CASES.items():
        if args.only and args.only != name:
            continue
        report_path = output / (name + ".json")
        if report_path.exists():
            report_path.unlink()
        command = [str(binary), "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0",
                   "-logFile", str(output / (name + ".log")), flag, str(report_path)]
        if name == "performance":
            command += ["-traveller-benchmark-fps", "120"]
        print(f"Running {name}", flush=True)
        with (output / (name + "-launcher.log")).open("w") as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=300)
        report = json.loads(report_path.read_text()) if report_path.exists() else {"passed": False, "errors": ["Player produced no report."]}
        print(json.dumps({"case": name, "exit": result.returncode, "passed": report.get("passed"),
                          "checks": report.get("checks", len(report.get("metrics", []))), "errors": report.get("errors", [])}, ensure_ascii=False), flush=True)
        if result.returncode or not report.get("passed"):
            raise SystemExit(1)


if __name__ == "__main__":
    main()
