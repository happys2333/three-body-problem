"""Audit the saved awakening revision; no hardware recording or paid model requests."""
import hashlib
import json
from pathlib import Path
import struct
import wave

ROOT=Path(__file__).resolve().parents[1]
DOC=ROOT/'Documentation/ActOne'
EVIDENCE=DOC/'Opening20261030'


def read(path):return json.loads(path.read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def audit():
    native=read(DOC/'Native/opening-20261030/report.json')
    edit=read(EVIDENCE/'editmode.json')['data']['result']['summary']
    unit=read(EVIDENCE/'gateway-tests.json')
    build=read(DOC/'build.json')
    assert native['passed'] and not native['errors'] and native['microphoneAutoSendVerified']
    assert native['openingAnswers']==3 and native['damageEvents']>0
    assert native['ignited']==native['burned'] and native['burned']>0 and native['shaded']>0
    assert native['walkedMetres']>=35 and 0<native['sunriseSeconds']<3.5 and 0<native['sunsetSeconds']<3.5
    assert native['frameP95Ms']<16.67
    assert not native['hardwareMicrophoneUsed'] and not native['onlineModelUsed']
    assert edit['failed']==0 and edit['passed']>=49
    assert unit['passed'] and unit['tests']>=16
    assert build['result']=='Succeeded' and build['errors']==0 and build['warnings']==0
    assert read(EVIDENCE/'clean-build.json')['sameRuntimeAssemblyAsPassedNativeRun']
    for relative,digest in read(EVIDENCE/'source-manifest.json').items():
        assert sha(ROOT/relative)==digest,'Evidence predates changes to '+relative
    audio=[]
    for name in read(ROOT/'Tools/scripted_voice_lines.json'):
        path=ROOT/'Assets/_Project/Audio/ActOne'/(name+'.wav')
        assert Path(str(path)+'.meta').is_file()
        with wave.open(str(path),'rb') as sound:
            assert (sound.getframerate(),sound.getnchannels(),sound.getsampwidth())==(24000,1,2)
            duration=sound.getnframes()/sound.getframerate();assert duration>.5
        audio.append({'name':name,'seconds':duration,'sha256':sha(path)})
    shots=sorted((DOC/'Native/opening-20261030').glob('*.png'));assert len(shots)>=10
    dimensions=sorted({struct.unpack('>II',path.read_bytes()[16:24]) for path in shots})
    report={'passed':True,'editModePassed':edit['passed'],'gatewayTests':unit['tests'],
            'nativeChecks':native['checks'],'microphoneAutoSendVerified':True,'build':build,
            'nativeFrameP95Ms':native['frameP95Ms'],'captureDimensions':dimensions,
            'requestedResolution':[1920,1080],'voiceLines':audio,
            'vegetation':{'ignited':native['ignited'],'ash':native['burned'],'shadedSurvived':native['shaded']},
            'limits':['The native player requests 1920x1080; macOS fits its window to the display. Use captureDimensions for the actual rendered size.',
                      'Synthetic speech was injected into the real ASR coroutine; no hardware microphone or permission prompt was exercised.',
                      'The live run used scripted fallback. Online decisions were tested with a fake upstream, not a real DeepSeek key.',
                      'Short synthetic speech still has ASR character errors; this is not human voice-quality or recognition-accuracy acceptance.',
                      'One representative Gobi frame sample on this Mac, not a whole-game or other-hardware performance guarantee.']}
    (EVIDENCE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    return report


def main():
    report=audit()
    print(json.dumps({k:report[k] for k in ('passed','editModePassed','gatewayTests','nativeChecks','microphoneAutoSendVerified','nativeFrameP95Ms','captureDimensions')},ensure_ascii=False))


if __name__=='__main__':main()
