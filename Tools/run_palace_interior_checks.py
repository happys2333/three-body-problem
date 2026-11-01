"""Run the built macOS player checks; never starts another Unity Editor."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess

ROOT = Path(__file__).resolve().parents[1]
CASES = {'interior': ('Interior', '-palace-interior-check', 180),
         'walk': ('CapitalWalk', '-traveller-capital-check', 150),
         'revival': ('Revival', '-capital-revival-check', 180),
         'gobi': ('GobiOpening', '-gobi-opening-check', 300)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=list(CASES) + ['all'], default='all')
    parser.add_argument('--capture-ritual', action='store_true')
    parser.add_argument('--capture-court', action='store_true')
    parser.add_argument('--check-retinue', action='store_true')
    parser.add_argument('--output-root', type=Path, default=ROOT / 'Documentation/InteriorProduction/2026-11-01/Native')
    parser.add_argument('--app', type=Path, default=ROOT / 'Builds/PalaceInterior20261101/Traveller.app')
    args = parser.parse_args()
    app = args.app.resolve()
    executable = app / 'Contents/MacOS' / plistlib.loads((app / 'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    results = []
    for key, (name, flag, timeout) in CASES.items():
        if args.case not in ('all', key):
            continue
        out = args.output_root.resolve() / name
        out.mkdir(parents=True, exist_ok=True)
        report = out / 'report.json'
        if report.exists():
            report.unlink()
        command = [str(executable), '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
                   '-logFile', str(out / 'player.log'), flag, str(report)]
        if key == 'interior' and args.capture_ritual:
            command.append('-capture-ritual')
        if key == 'interior' and args.capture_court:
            command.append('-capture-court')
        if key == 'interior' and args.check_retinue:
            command.append('-check-retinue')
        print('Running ' + name, flush=True)
        try:
            with (out / 'launcher.log').open('w') as log:
                run = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
            data = json.loads(report.read_text()) if report.exists() else {'passed': False, 'errors': ['No report produced']}
            result = {'case': key, 'exit': run.returncode, **data}
        except subprocess.TimeoutExpired:
            result = {'case': key, 'exit': -1, 'passed': False, 'errors': ['Native player timed out']}
        results.append(result)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    if not all(x['exit'] == 0 and x.get('passed') for x in results):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
