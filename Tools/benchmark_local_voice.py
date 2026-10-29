"""Synthetic speech acceptance set; never opens the hardware microphone or calls an LLM."""
import argparse
import io
import json
from pathlib import Path
import re
import time
import wave

from local_voice_bridge import LocalVoice

ROOT=Path(__file__).resolve().parents[1]
CASES=[
 ('wenwang','平静','一个人在夜里赶路？举着火把，跟我走。'),
 ('wenwang','身份','我是姬昌。我要去朝歌，把推演的历法呈给纣王。'),
 ('wenwang','紧急','太阳不对！别停在亮处，快跟我躲到岩石下面！'),
 ('wenwang','安抚','先别出去。这里的阴影能挡住日光，我们暂且等一等。'),
 ('wenwang','计时','沙漏还在走，太阳却未必守时。'),
 ('wenwang','启程','太阳落下了，跟紧我，趁夜色赶路。'),
 ('king','质询','把你已经看见的证据说清楚。'),
 ('king','历法','就按历法等待。若日照应验，再让人们复苏。'),
 ('pope','怀疑','先说清你的依据，眼前的现象能支持你的解释吗？'),
 ('scholar','观测','一颗飞星和两颗飞星，究竟意味着什么？'),
 ('scholar','反例','恒纪元的日出没有到来，我们应当保留这个反例。'),
 ('davinci','讨论','有意思。把你的证据和尚未弄清的地方都说出来。'),
 ('shang_guard','指引','沿着王道往前走，到金字塔的入口等候。'),
 ('church_guard','警告','保持通道畅通，听见警报就先到安全处。'),
 ('citizen','日常','我们也在等天象转好，你在路上看见了什么？'),
 ('wenwang','专名','朝歌、姬昌、周文王、纣王。'),
 ('scholar','术语','乱纪元、恒纪元、脱水、复苏、三日凌空。'),
 ('wenwang','短问','你叫什么名字？'),
 ('wenwang','连续句','我们先在这里等一会儿。安全以后再继续赶路。'),
 ('scholar','数字混合','我记录了三次日出，第二次比第一次更晚。')]


def normalize(text):return re.sub(r'[^\w]','',text).lower()


def distance(a,b):
    row=list(range(len(b)+1))
    for i,x in enumerate(a,1):
        new=[i]
        for j,y in enumerate(b,1):new.append(min(new[-1]+1,row[j]+1,row[j-1]+(x!=y)))
        row=new
    return row[-1]


def main():
    import numpy as np
    from scipy.signal import resample_poly
    parser=argparse.ArgumentParser();parser.add_argument('--limit',type=int,default=20);parser.add_argument('--threads',type=int,default=1);args=parser.parse_args()
    folder=ROOT/'Documentation/ActOne/LocalVoice';folder.mkdir(parents=True,exist_ok=True)
    service=LocalVoice(args.threads);started=time.perf_counter();service.start()
    report={'syntheticOnly':True,'hardwareMicrophoneUsed':False,'deepseekUsed':False,'threads':args.threads,
        'loadSeconds':time.perf_counter()-started,'cases':[],'health':service.health()}
    try:
        for index,(npc,category,text) in enumerate(CASES[:args.limit],1):
            wav=service.speech(text,npc);tts=dict(service.stats['tts']);name='%02d_%s.wav'%(index,npc);(folder/name).write_bytes(wav)
            with wave.open(io.BytesIO(wav),'rb') as sound:
                samples=np.frombuffer(sound.readframes(sound.getnframes()),dtype='<i2').astype(np.float32).reshape(-1,sound.getnchannels()).mean(axis=1)/32768
                rate=sound.getframerate()
            resampled=resample_poly(samples,16000,rate);encoded=io.BytesIO()
            with wave.open(encoded,'wb') as sound:
                sound.setnchannels(1);sound.setsampwidth(2);sound.setframerate(16000);sound.writeframes(np.round(np.clip(resampled,-1,1)*32767).astype('<i2').tobytes())
            heard=service.transcribe(encoded.getvalue());asr=dict(service.stats['asr'])
            errors=distance(normalize(text),normalize(heard));row={'npc':npc,'category':category,'text':text,'transcript':heard,'characterErrors':errors,'referenceCharacters':len(normalize(text)),
                'audio':name,'tts':tts,'asr':asr,'wavePeak':float(np.max(np.abs(samples))),'waveRms':float(np.sqrt(np.mean(samples*samples)))}
            report['cases'].append(row);print(json.dumps({'case':index,**row},ensure_ascii=False),flush=True)
            (folder/'benchmark.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
        silence=io.BytesIO()
        with wave.open(silence,'wb') as sound:
            sound.setnchannels(1);sound.setsampwidth(2);sound.setframerate(16000);sound.writeframes(b'\0'*32000)
        report['silenceText']=service.transcribe(silence.getvalue())
        report['characterErrorRate']=sum(row['characterErrors'] for row in report['cases'])/sum(row['referenceCharacters'] for row in report['cases'])
        report['passed']=report['silenceText']=='' and all(row['tts']['audioSeconds']>0 and row['waveRms']>.002 and not row['tts']['torchImported'] and row['transcript'] for row in report['cases'])
        report['note']='Transport, non-silence, and decoder checks passed does not establish naturalness, real-microphone accuracy, or Unity coexistence performance.'
        (folder/'benchmark.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    finally:service.close()
    if not report.get('passed'):raise SystemExit(1)


if __name__=='__main__':main()
