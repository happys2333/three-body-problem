"""Recheck installed model hashes and the saved local-voice integration evidence."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import urllib.request
import wave

ROOT=Path(__file__).resolve().parents[1]
DOC=ROOT/'Documentation/ActOne'
CACHE=Path.home()/'.cache/three-body-voice'


def read(path):return json.loads(path.read_text())


def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    from setup_local_voice_models import MODELS,SOURCE_SHA
    manifest=read(CACHE/'local-models.json');models=[]
    assert manifest['sourceRevision']==SOURCE_SHA
    source=subprocess.check_output(['git','-C',str(CACHE/'src/MOSS-TTS-Nano'),'rev-parse','HEAD'],text=True).strip()
    assert source==SOURCE_SHA
    for model in manifest['models']:
        name=Path(model['path']).name
        assert (model['repo'],model['revision'])==MODELS[name]
        for entry in model['files']:
            assert sha(Path(model['path'])/entry['name'])==entry['sha256'],entry['name']
        models.append({'repo':model['repo'],'revision':model['revision'],
            'verifiedFiles':len(model['files']),
            'upstreamLfsChecksums':sum(e['upstreamSha256Checked'] for e in model['files'])})
    assert sum(m['upstreamLfsChecksums'] for m in models)==14
    baseline=read(DOC/'LocalVoice/publication-baseline.json')
    # Compare existing .meta files to the published pre-upgrade snapshot.
    baseline_sha=baseline['remoteHeadVerified']
    lines=read(ROOT/'Tools/scripted_voice_lines.json');audio=[]
    for name in lines:
        path=ROOT/'Assets/_Project/Audio/ActOne'/(name+'.wav');meta=Path(str(path)+'.meta')
        original=subprocess.check_output(['git','show',baseline_sha+':'+str(meta.relative_to(ROOT))])
        assert meta.read_bytes()==original,name+' metadata changed'
        with wave.open(str(path),'rb') as sound:
            assert (sound.getframerate(),sound.getnchannels(),sound.getsampwidth())==(24000,1,2)
            assert sound.getnframes()>2400
            seconds=sound.getnframes()/sound.getframerate()
        audio.append({'name':name,'sha256':sha(path),'seconds':seconds,'guidPreserved':True})
    benchmark=read(DOC/'LocalVoice/benchmark.json');native=read(DOC/'Native/voice/report.json')
    gameplay=read(DOC/'Native/functional/report.json');edit=read(DOC/'LocalVoice/editmode.json')['result']['summary']
    switch=read(DOC/'LocalVoice/launcher-fallback-check.json')
    assert benchmark['passed'] and len(benchmark['cases'])==20 and benchmark['silenceText']==''
    assert all(not c['tts']['torchImported'] for c in benchmark['cases'])
    for case in benchmark['cases']:
        assert (DOC/'LocalVoice'/case['audio']).is_file()
    assert native['passed'] and native['checks']==18 and native['playedSampleRate']==24000
    assert native['asrRequests']>=5 and native['speechRequests']>=5 and not native['hardwareMicrophoneUsed']
    assert gameplay['passed'] and gameplay['checks']==49 and not gameplay['performanceOnly']
    assert all(m['focused'] and m['actualFrameRateLimit']==90 for m in gameplay['metrics'])
    assert edit['passed']==44 and edit['failed']==0 and switch['passed']
    build=read(DOC/'build.json');assert build['result']=='Succeeded' and build['errors']==0 and build['warnings']==0
    unit=subprocess.run([sys.executable,str(ROOT/'Tools/test_dialogue_gateway.py')],capture_output=True,text=True,timeout=30)
    assert unit.returncode==0,unit.stderr
    count=re.search(r'Ran (\d+) tests?',unit.stderr);assert count and int(count[1])==6
    dependencies=subprocess.run([str(CACHE/'local-venv/bin/python'),'-m','pip','check'],capture_output=True,text=True,timeout=30)
    assert dependencies.returncode==0,dependencies.stdout+dependencies.stderr
    with urllib.request.urlopen('http://127.0.0.1:8769/health',timeout=5) as response:health=json.load(response)
    assert health['ready'] and health['asrEngine']=='qwen3-asr-mlx-8bit' and health['ttsEngine']=='moss-tts-nano-onnx-cpu'
    subprocess.run([sys.executable,str(ROOT/'Tools/audit_act_one.py')],check=True,cwd=ROOT)
    evidence=['LocalVoice/benchmark.json','LocalVoice/editmode.json','Native/voice/report.json',
        'Native/functional/report.json','LocalVoice/launcher-fallback-check.json','voice-gateway-check.json']
    report={'passed':True,'publishedBaseline':baseline_sha,'models':models,'runtimeRevision':source,
        'scriptedAudio':audio,'editMode':edit,'gatewayUnitTests':int(count[1]),
        'dependencies':dependencies.stdout.strip(),'summary':read(DOC/'LocalVoice/summary.json'),
        'currentGateway':health,'evidenceSha256':{p:sha(DOC/p) for p in evidence},
        'limits':['Synthetic audio only; real microphone, accents and noise remain untested.',
            'No real DeepSeek key used; default replies are offline scripted dialogue.',
            'Whole-WAV playback; internal first-PCM timing is not audible startup latency.',
            'Eight role profiles reuse three temporary male preset voices.',
            'Foreground first-act timings apply only to this Mac and measured run.',
            'Earlier failed performance and soak reports are preserved; no new 600-second soak.']}
    (DOC/'LocalVoice/delivery.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'passed':True,'upstreamLfsChecksums':14,'scriptedLines':len(audio),
        'gatewayUnitTests':int(count[1]),'editModePassed':edit['passed'],'nativeVoiceChecks':native['checks'],
        'fullActOneChecks':gameplay['checks']},ensure_ascii=False))


if __name__=='__main__':main()
