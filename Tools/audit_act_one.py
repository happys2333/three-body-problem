"""Audit current act-one delivery against saved Unity and native-player evidence."""
import hashlib
import json
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[1]
DOC=ROOT/'Documentation/ActOne'


def read(name):return json.loads((DOC/name).read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def transforms(path):
    return {match.group(1):match.group(2) for match in re.finditer(
        r'^--- !u!4 &(\d+)\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S)}


def main():
    if (DOC/'Opening20261030/source-manifest.json').exists():
        from audit_gobi_opening import main as audit_current_opening
        audit_current_opening();return
    baseline=read('baseline.json')
    protected=[name for name in baseline if name.endswith((
        'PalaceSkylight.cs','PendulumMotion.cs','TravellerActor.cs','TravellerPlayer.cs',
        'TravellerCastSettings.cs','TravellerCastDirector.cs','TravellerCrowdMember.cs',
        'ObservationJournal.cs','HeatDistortion.cs','CharacterMotionGallery.cs',
        '02_ChaogeCourt.asset','03_MedievalTribunal.asset','FieldNotesPanel.asset'))]
    unchanged={name:sha(ROOT/name)==baseline[name] for name in protected}
    assert all(unchanged.values()),unchanged
    layout={}
    original_layout=read('layout-baseline.json')
    for name in ('02_ChaogeCourt','03_MedievalTribunal'):
        path=Path('Assets/_Project/Scenes/Traveller')/(name+'.unity')
        after=transforms(ROOT/path)
        digest=hashlib.sha256(json.dumps(after,sort_keys=True,separators=(',',':')).encode()).hexdigest()
        layout[name]={'transforms':len(after),'unchanged':len(after)==original_layout[name]['transforms'] and digest==original_layout[name]['sha256']}
    assert all(value['unchanged'] for value in layout.values()),layout
    assets=[ROOT/'Assets/_Project'/p for p in ('Art/ActOne','Audio/ActOne','Dialogue','Settings/ActOne')]
    assets += [ROOT/'Assets/_Project/Scripts'/p for p in (
        'GobiStorySettings.cs','GobiSequenceController.cs','SunExposureVitality.cs','SunExposureSystem.cs',
        'SunDamageFeedback.cs','VegetationFuel.cs','GobiVegetationFire.cs','TorchBearer.cs','TorchHandIK.cs',
        'TorchFlameBillboard.cs','NpcPersona.cs','NpcIdentity.cs','NpcDialogueSettings.cs',
        'NpcDialogueDirector.cs','VoiceWave.cs','PlayerMicrophoneInput.cs','Development/ActOnePlayerCheck.cs','Development/LocalVoicePlayerCheck.cs')]
    assets += [ROOT/'Assets/_Project/Editor/ActOneProduction.cs',ROOT/'Assets/_Project/Tests/Editor/ActOneMechanicsTests.cs']
    files=[]
    for path in assets:
        files.extend(path.rglob('*') if path.is_dir() else [path])
        if path.is_dir():files.append(path)
    missing=[str(path.relative_to(ROOT)) for path in files if path.suffix!='.meta' and not Path(str(path)+'.meta').exists()]
    assert not missing,missing
    resources=read('saved-resource-audit.json');scenes=resources['scenes']
    assert sum(row['torchBearers'] for row in scenes)==36
    assert sum(row['identities'] for row in scenes)==33
    assert sum(row['validHumanoids'] for row in scenes)==36
    assert sum(row['missingScripts'] for row in scenes)==0
    assert scenes[0]['fuel']==138 and resources['personas']==8 and resources['voiceLines']==6
    assert all(row['microphone'] and row['dialogue'] for row in scenes)
    assert all(len(row['triangles'])==3 and row['triangles'][0]>row['triangles'][1]>row['triangles'][2]>0 and row['missingMaterials']==0 for row in resources['props'])
    native=read('Native/functional/report.json');soak=read('Native/soak/report.json')
    journey=read('Native/journey/report.json');characters=read('Native/characters/report.json')
    edit=read('editmode.json')['result']['summary'];voice=read('voice-gateway-check.json');build=read('build.json')
    assert native['passed'] and native['mechanicsPassed'] and native['performancePassed']
    assert journey['passed'] and characters['passed'] and voice['passed'] and edit['failed']==0
    assert build['result']=='Succeeded' and build['errors']==0 and build['warnings']==0
    # The original soak remains unmodified: its timing passed, but its earlier ASR and
    # performance failures were resolved and retested in the final fast native run.
    timing=soak['realTenMinuteWait'] and soak['shelterWallSeconds']>=599 and soak['sunsetWallSeconds']<3.5
    assert timing
    report={'readyForGameplayReview':True,'build':build,'editMode':edit,
        'nativeChecks':native['checks'],'journeyChecks':journey['checks'],'characterChecks':characters['checks'],
        'fullRealWaitTiming':{'passed':timing,'shelterSeconds':soak['shelterWallSeconds'],
            'sunsetSeconds':soak['sunsetWallSeconds'],'originalCombinedSoakPassed':soak['passed'],
            'note':'Timing from the real 600-second run; later service/render-only fixes passed the final native run. The combined soak was not repeated.'},
        'performance':{'gpu':native['gpu'],'resolution':'1920x1080','msaa':2,'frameRateLimit':native['frameRateLimit'],
            'warmupFramesPerView':180,'samplesPerView':360,'worstFrameP95Ms':max(row['p95Ms'] for row in native['metrics']),
            'passed':native['performancePassed'],'scope':'Six first-scene preview views on this machine; not a whole-game or other-hardware guarantee.'},
        'vegetation':{'ignited':native['ignited'],'ash':native['burned'],'shadedSurvived':native['shaded']},
        'savedResources':resources,'missingMetadata':missing,'protectedFilesUnchanged':unchanged,'laterSceneLayouts':layout,
        'scopeChanges':[name for name,value in baseline.items() if sha(ROOT/name)!=value],
        'limits':['Real DeepSeek generation not validated without a server key.',
            'Hardware microphone and permission UI not activated by automated QA.',
            'Speech recognition requires editable confirmation, especially for names.',
            'Local ASR/TTS gateway must be running; packaged standalone voice service is not included.',
            'Temporary preset model voices and procedural flame need final audio/art direction.']}
    (DOC/'delivery.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'readyForGameplayReview':True,'editModePassed':edit['passed'],'nativeChecks':native['checks'],
        'journeyChecks':journey['checks'],'characterChecks':characters['checks'],'worstFrameP95Ms':report['performance']['worstFrameP95Ms'],
        'missingMetadata':missing,'laterSceneLayouts':layout},ensure_ascii=False))


if __name__=='__main__':main()
