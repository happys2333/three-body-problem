"""Sequential native checks for the revised first act. --soak exercises all 600 seconds."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--soak',action='store_true');parser.add_argument('--journey',action='store_true');parser.add_argument('--characters',action='store_true');parser.add_argument('--voice',action='store_true');parser.add_argument('--perf-only',action='store_true');args=parser.parse_args()
    app=ROOT/'Builds/ActOneTraveller.app'
    with (app/'Contents/Info.plist').open('rb') as stream:executable=plistlib.load(stream)['CFBundleExecutable']
    binary=app/'Contents/MacOS'/executable
    name='performance-probe' if args.perf_only else 'voice' if args.voice else 'characters' if args.characters else 'journey' if args.journey else 'soak' if args.soak else 'functional'
    folder=ROOT/'Documentation/ActOne/Native'/name;folder.mkdir(parents=True,exist_ok=True);report=folder/'report.json'
    if report.exists():report.unlink()
    flag='-traveller-local-voice-check' if args.voice else '-traveller-character-check' if args.characters else '-traveller-entry-check' if args.journey else '-act-one-check'
    command=[str(binary),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',str(folder/'player.log'),flag,str(report)]
    if args.soak:command.append('-act-one-real-wait')
    if args.perf_only:command.append('-act-one-perf-only')
    print('Running '+name,flush=True)
    with (folder/'launcher.log').open('w') as log:result=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=850 if args.soak else 300)
    data=json.loads(report.read_text()) if report.exists() else {'passed':False,'errors':['No native report']}
    print(json.dumps({'exit':result.returncode,**data},ensure_ascii=False),flush=True)
    if result.returncode or not data.get('passed'):raise SystemExit(1)


if __name__=='__main__':main()
