"""Run identical first-act views in a native player; preserve prior reports."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess
ROOT=Path(__file__).resolve().parents[1]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app',type=Path,default=ROOT/'Builds/ActOnePerformance20261102/Traveller.app')
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--probe',default='baseline',choices=['baseline','lod1','no-shadows','no-terrain','no-ui','simple-surface'])
    parser.add_argument('--view',type=int)
    parser.add_argument('--cap',type=int,default=90)
    parser.add_argument('--width',type=int,default=1600);parser.add_argument('--height',type=int,default=900)
    args=parser.parse_args();app=args.app.resolve();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True);report=out/'report.json'
    if report.exists():raise ValueError('Use a fresh output directory to retain existing evidence.')
    exe=app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    cmd=[str(exe),'-screen-fullscreen','0','-screen-width',str(args.width),'-screen-height',str(args.height),'-logFile',str(out/'player.log'),'-act-one-render-benchmark',str(report),'-render-probe',args.probe,'-render-cap',str(args.cap)]
    if args.view is not None:cmd+=['-render-view',str(args.view)]
    with (out/'launcher.log').open('w') as log:result=subprocess.run(cmd,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=240)
    data=json.loads(report.read_text()) if report.exists() else {'passed':False,'errors':['No final report']}
    print(json.dumps({'exit':result.returncode,**data},ensure_ascii=False,indent=2))
    if result.returncode or not data.get('passed'):raise SystemExit(1)
if __name__=='__main__':main()
