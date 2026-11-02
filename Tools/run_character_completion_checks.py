"""Run the reviewed-asset checks in a native macOS development player."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT/'Builds/CharacterCompletion20261102/Traveller.app')
    parser.add_argument('--output', type=Path, default=ROOT/'Documentation/CharacterProduction/2026-11-02/Native')
    parser.add_argument('--pope-only', action='store_true')
    args = parser.parse_args()
    app, out = args.app.resolve(), args.output.resolve()
    executable = app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    out.mkdir(parents=True, exist_ok=True)
    report = out/'report.json'
    if report.exists():
        raise ValueError('Choose a fresh output directory to retain earlier QA evidence')
    command = [str(executable), '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
               '-logFile', str(out/'player.log'), '-character-completion-check', str(report)]
    if args.pope_only:
        command.append('-completion-pope-only')
    with (out/'launcher.log').open('w') as log:
        result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=240)
    data = json.loads(report.read_text()) if report.exists() else {'passed': False, 'errors': ['No report produced']}
    print(json.dumps({'exit': result.returncode, **data}, ensure_ascii=False))
    if result.returncode or not data.get('passed'):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
