"""Standard-library gateway adapter for isolated MLX and ONNX model processes."""
import atexit
import base64
import json
from pathlib import Path
import select
import subprocess
import threading
import time

CACHE=Path.home()/'.cache/three-body-voice'
TOOLS=Path(__file__).resolve().parent


class ModelWorker:
    def __init__(self,engine,threads=1):
        self.engine=engine;self.threads=threads;self.process=None;self.lock=threading.Lock();self.ready={}
        atexit.register(self.close)

    def _read(self,timeout):
        if not select.select([self.process.stdout],[],[],timeout)[0]:raise TimeoutError(self.engine+' deadline')
        line=self.process.stdout.readline()
        if not line:raise RuntimeError(self.engine+' worker exited')
        value=json.loads(line)
        if value.get('error'):raise RuntimeError(self.engine+': '+value['error'])
        return value

    def start(self):
        if self.process is not None and self.process.poll() is None:return self.ready
        log_dir=CACHE/'logs';log_dir.mkdir(parents=True,exist_ok=True)
        with (log_dir/(self.engine+'.log')).open('a') as log:
            self.process=subprocess.Popen([str(CACHE/'local-venv/bin/python'),str(TOOLS/'local_voice_worker.py'),
                '--engine',self.engine,'--threads',str(self.threads)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,
                stderr=log,text=True,bufsize=1)
        self.ready=self._read(45)
        if not self.ready.get('ready'):raise RuntimeError(self.engine+' initialization failed')
        return self.ready

    def request(self,value,timeout=28):
        if not self.lock.acquire(timeout=timeout):raise TimeoutError(self.engine+' is busy')
        try:
            self.start();self.process.stdin.write(json.dumps(value,ensure_ascii=False)+'\n');self.process.stdin.flush()
            return self._read(timeout)
        except Exception:
            self.close();raise
        finally:self.lock.release()

    def close(self):
        process=self.process;self.process=None
        if process is None:return
        if process.stdin:
            try:process.stdin.close()
            except OSError:pass
        try:process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            process.terminate()
            try:process.wait(timeout=2)
            except subprocess.TimeoutExpired:process.kill();process.wait()
        if process.stdout:process.stdout.close()


class LocalVoice:
    def __init__(self,threads=1):
        self.asr=ModelWorker('qwen');self.tts=ModelWorker('moss',threads)
        self.profiles=json.loads((TOOLS/'voice_profiles.json').read_text())['profiles']
        self.stats={}

    def start(self):
        self.asr.start();self.tts.start()

    def transcribe(self,wav):
        value=self.asr.request({'audio':base64.b64encode(wav).decode()},timeout=28)
        self.stats['asr']={k:v for k,v in value.items() if k!='text'}
        return value['text']

    def speech(self,text,npc_id='wenwang'):
        if npc_id not in self.profiles:raise ValueError('Unknown voice profile')
        value=self.tts.request({'text':text,**self.profiles[npc_id]},timeout=35)
        self.stats['tts']={k:v for k,v in value.items() if k!='wav'}
        return base64.b64decode(value['wav'],validate=True)

    def close(self):
        self.asr.close();self.tts.close()

    def health(self):
        return {'asrEngine':'qwen3-asr-mlx-8bit','ttsEngine':'moss-tts-nano-onnx-cpu',
            'asrModel':'Qwen3-ASR-0.6B-8bit','ttsModel':'MOSS-TTS-Nano-100M-ONNX',
            'voiceProfiles':{name:p['voice'] for name,p in self.profiles.items()},'lastVoiceTimings':self.stats}
