"""Check the local speech path using synthetic audio, without opening a microphone."""
import io
from array import array
import json
from pathlib import Path
import time
import urllib.request
import wave

ROOT=Path(__file__).resolve().parents[1]
URL='http://127.0.0.1:8769'


def request(path,data=None,content_type='application/json'):
    headers={'Content-Type':content_type}
    return urllib.request.urlopen(urllib.request.Request(URL+path,data=data,headers=headers),timeout=32)


def to_asr_wav(audio):
    with wave.open(io.BytesIO(audio),'rb') as sound:
        rate=sound.getframerate();channels=sound.getnchannels();frames=sound.getnframes()
        values=array('h',sound.readframes(frames))
    mono=[sum(values[i*channels:(i+1)*channels])/channels for i in range(frames)]
    converted=array('h')
    for i in range(int(frames*16000/rate)):
        position=i*rate/16000;left=int(position);right=min(left+1,frames-1);fraction=position-left
        converted.append(max(-32768,min(32767,round(mono[left]*(1-fraction)+mono[right]*fraction))))
    output=io.BytesIO()
    with wave.open(output,'wb') as sound:
        sound.setnchannels(1);sound.setsampwidth(2);sound.setframerate(16000);sound.writeframes(converted.tobytes())
    return output.getvalue()


def main():
    report={'hardwareMicrophoneUsed':False,'deepseekUsed':False,'checks':[]}
    with request('/health') as response:report['health']=json.load(response)
    text='你叫什么名字？我想和你一起去朝歌。'
    start=time.monotonic()
    with request('/speech',json.dumps({'text':text,'npcId':'wenwang'},ensure_ascii=False).encode()) as response:audio=response.read()
    with wave.open(io.BytesIO(audio),'rb') as sound:
        report['format']={'channels':sound.getnchannels(),'rate':sound.getframerate(),'sampleBytes':sound.getsampwidth(),'seconds':sound.getnframes()/sound.getframerate()}
        assert sound.getnchannels()==1 and sound.getframerate() in (16000,24000,48000) and sound.getsampwidth()==2
    report['speechSeconds']=round(time.monotonic()-start,3)
    # Keep the exact transcript as evidence; non-empty recognition is a transport/decoder
    # check, not a claim that proper nouns or live microphone accuracy have passed.
    asr_audio=to_asr_wav(audio)
    for index in range(3):
        start=time.monotonic()
        with request('/transcribe',asr_audio,'audio/wav') as response:value=json.load(response)
        elapsed=round(time.monotonic()-start,3)
        assert value.get('text') and elapsed<30
        report['checks'].append({'case':'synthetic Chinese question '+str(index+1),'seconds':elapsed,'expectedSpeech':text,'transcript':value['text']})
    with request('/dialogue',json.dumps({'npcId':'wenwang','utterance':'你是谁？','context':{'phase':'ShelterTalk'}}).encode()) as response:value=json.load(response)
    assert value.get('speech') and value.get('mode')=='scripted'
    report['offlineReply']=value
    report['passed']=True
    path=ROOT/'Documentation/ActOne/voice-gateway-check.json'
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False))


if __name__=='__main__':main()
