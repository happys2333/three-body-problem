"""Check the saved torch in the real first-person macOS player and retain images."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'Builds/TorchVfx20261102/Traveller.app')
    parser.add_argument('--output', type=Path, default=ROOT / 'Documentation/TorchVfx/2026-11-02/NativeFinal')
    args = parser.parse_args()
    app, output = args.app.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = output / 'report.json'
    if report.exists():
        raise ValueError('Use a fresh output directory to preserve earlier evidence.')
    executable = app / 'Contents/MacOS' / plistlib.loads((app / 'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    command = [str(executable), '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
               '-logFile', str(output / 'player.log'), '-torch-vfx-check', str(report)]
    with (output / 'launcher.log').open('w') as log:
        run = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=180)
    data = json.loads(report.read_text()) if report.exists() else {'passed': False, 'errors': ['No final report.']}
    print(json.dumps({'exit': run.returncode, **data}, ensure_ascii=False, indent=2))
    if run.returncode or not data.get('passed'):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
