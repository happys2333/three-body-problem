"""Pre-render six mandatory story lines with the same voice profile as live replies."""
import json
from pathlib import Path
import shutil

from local_voice_bridge import CACHE,LocalVoice

ROOT=Path(__file__).resolve().parents[1]


def main():
    lines=json.loads((ROOT/'Tools/scripted_voice_lines.json').read_text())
    staging=CACHE/'rendered-story';staging.mkdir(parents=True,exist_ok=True)
    backup=ROOT/'Logs/ActOne/before-local-voice';backup.mkdir(parents=True,exist_ok=True)
    service=LocalVoice();rows=[]
    try:
        service.tts.start()
        for name,text in lines.items():
            wav=service.speech(text,'wenwang');(staging/(name+'.wav')).write_bytes(wav)
            target=ROOT/'Assets/_Project/Audio/ActOne'/(name+'.wav')
            if target.exists() and not (backup/target.name).exists():shutil.copy2(target,backup/target.name)
            temporary=target.with_suffix('.wav.tmp');shutil.copy2(staging/target.name,temporary);temporary.replace(target)
            rows.append({'name':name,'text':text,**service.stats['tts']});print(name,service.stats['tts']['seconds'],flush=True)
    finally:service.close()
    (ROOT/'Documentation/ActOne/LocalVoice/scripted-lines.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')


if __name__=='__main__':main()
