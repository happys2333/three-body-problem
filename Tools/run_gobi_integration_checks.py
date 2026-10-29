"""Run the Gobi physical walk/performance and existing main-route checks sequentially."""
import argparse
import json
import plistlib
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", choices=["dressing", "journey"])
    args = parser.parse_args()
    app = ROOT / "Builds/GobiDressed.app"
    with (app / "Contents/Info.plist").open("rb") as stream:
        executable = plistlib.load(stream)["CFBundleExecutable"]
    binary = app / "Contents/MacOS" / executable
    for name, flag in (("dressing", "-gobi-dressing-check"), ("journey", "-traveller-entry-check")):
        if args.only and args.only != name:
            continue
        output = ROOT / "Documentation/GobiIntegration/Native" / name
        output.mkdir(parents=True, exist_ok=True)
        report_path = output / "report.json"
        if report_path.exists():
            report_path.unlink()
        command = [str(binary), "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0",
                   "-logFile", str(output / "player.log"), flag, str(report_path)]
        print(f"Running {name} in the native development player", flush=True)
        with (output / "launcher.log").open("w") as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=300)
        report = json.loads(report_path.read_text()) if report_path.exists() else {"passed": False, "errors": ["No native report"]}
        print(json.dumps({"case": name, "exit": result.returncode, **report}, ensure_ascii=False), flush=True)
        if result.returncode or not report.get("passed"):
            raise SystemExit(1)


if __name__ == "__main__":
    main()
