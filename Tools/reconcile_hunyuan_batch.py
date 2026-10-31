"""Audit a generated static-asset batch, including honest partial-download status."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def read(path):return json.loads(path.read_text())


def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    parser=argparse.ArgumentParser();parser.add_argument('manifest',type=Path);parser.add_argument('--baseline',type=Path);args=parser.parse_args()
    manifest=read(args.manifest);folder=args.manifest.parent
    imported=read(folder/'unity-validation.json')['assets']
    saved=read(folder/'saved-prefab-audit.json')
    generated=read(folder/'browser-generation-check.json')
    baseline_path=args.baseline or folder/manifest.get('verificationBaselineFile','story-baseline.json')
    baseline=read(baseline_path)
    source_checks=[]
    for asset in manifest['assets']:
        files=asset.get('files')
        if not files:continue
        source=ROOT/files['rawFile'];project=ROOT/files['projectFile']
        passed=sha(source)==sha(project)==files['sha256']
        assert passed,asset['id']+' source mismatch'
        source_checks.append({'id':asset['id'],'sha256':files['sha256'],'bytes':source.stat().st_size,'rawAndProjectIdentical':passed})
    assert len({c['sha256'] for c in source_checks})==len(source_checks),'Duplicate model export'
    checked={a['id'] for a in imported};assert checked=={a['id'] for a in saved['assets']}
    assert all(a['passed'] for a in imported) and saved['passed']
    asset_root=ROOT/manifest['unityRoot']
    paths=[asset_root]+list(asset_root.rglob('*'))
    for asset_id in checked:
        paths.extend((ROOT/'Assets/_Project/Art/GeneratedMeshes').glob('ScanLOD_Hunyuan_'+asset_id+'_*.asset'))
    missing=[str(p.relative_to(ROOT)) for p in paths if p.suffix!='.meta' and not Path(str(p)+'.meta').exists()]
    assert not missing,missing
    scene_checks={name:sha(ROOT/name)==digest for name,digest in baseline.items()}
    assert all(scene_checks.values()),scene_checks
    today=manifest['date'].replace('-','.')
    complete={row['prompt'] for row in generated if row['date']==today and row['completedCandidates']==4}
    assert {a['prompt'] for a in manifest['assets']}<=complete
    edit=read(folder/'editmode.json')['result']['summary']
    assert edit['failed']==0 and edit['passed']==44
    for asset in manifest['assets']:
        row=next((r for r in imported if r['id']==asset['id']),None)
        if row:asset['unityValidation']=row
    downloaded={c['id'] for c in source_checks}
    pending=[a['id'] for a in manifest['assets'] if a['id'] not in downloaded]
    approved=[a['id'] for a in manifest['assets'] if a.get('artReview',{}).get('status')=='approved']
    report={'generationCompleted':True,'allFilesDownloaded':not pending,'allAssetsArtApproved':len(approved)==len(manifest['assets']),
        'generatedGroups':len(manifest['assets']),'downloadedPbrFbx':len(source_checks),'sourceBytes':sum(c['bytes'] for c in source_checks),
        'technicalPrefabsPassed':len(imported),'artApproved':len(approved),'sourceChecks':source_checks,
        'missingMetadata':missing,'storyAndBuildSettingsUnchanged':scene_checks,'baselineFile':str(baseline_path),
        'editMode':edit,'editModeScope':'Historical 44-test run. Current saved-prefab checks are recorded separately; no claim that these tests covered subsequent imports.',
        'pendingExports':pending,'browserContinuation':manifest.get('browserContinuation'),
        'limits':['Technical candidate checks are not art approval. Use the separately repaired ExpansionReady library and its current reports.',
            'No generated asset was integrated into the story scenes.',
            'No new doorway traversal, complete interior or frame-rate validation was performed.',
            'Downloads use the normal platform export menu. No browser security setting was weakened.',
            'A missing automation download event is not evidence of a Chrome block; verify a completed local file.']}
    (folder/'delivery.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    with (folder/'CATALOG.csv').open('w',newline='',encoding='utf-8-sig') as stream:
        writer=csv.writer(stream);writer.writerow(['id','name','category','state','prefab','lod0_triangles','lod1_triangles','lod2_triangles','art_review'])
        for asset in manifest['assets']:
            row=asset.get('unityValidation',{});triangles=row.get('trianglesByLod',['','',''])
            note='已下载原始模型，尚未进行 Unity 外观验收' if asset.get('files') else '尚未下载，不能进行 Unity 外观验收'
            writer.writerow([asset['id'],asset['name'],asset['category'],asset['state'],row.get('prefab',''),*triangles,asset.get('artReview',{}).get('note',note)])
    print(json.dumps({key:report[key] for key in ('generationCompleted','allFilesDownloaded','generatedGroups','downloadedPbrFbx','technicalPrefabsPassed','artApproved')},ensure_ascii=False))


if __name__=='__main__':main()
