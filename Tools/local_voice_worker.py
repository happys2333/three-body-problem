"""Resident model workers. Audio stays in memory; stdout is a JSON-lines protocol."""
import argparse
import base64
import io
import json
import os
from pathlib import Path
import re
import sys
import time
import wave

CACHE=Path.home()/'.cache/three-body-voice'
HOTWORDS=['朝歌','姬昌','周文王','纣王','乱纪元','恒纪元','沙漏','历法','飞星','三日凌空','脱水','复苏']


def pcm_input(encoded):
    import numpy as np
    data=base64.b64decode(encoded,validate=True)
    if len(data)>2_000_000:raise ValueError('Audio too large')
    with wave.open(io.BytesIO(data),'rb') as sound:
        if (sound.getnchannels(),sound.getsampwidth(),sound.getframerate())!=(1,2,16000):raise ValueError('Expected mono 16 kHz PCM16')
        if sound.getnframes()>26*16000:raise ValueError('Audio too long')
        return np.frombuffer(sound.readframes(sound.getnframes()),dtype='<i2').astype(np.float32)/32768


class QwenAsr:
    def __init__(self):
        import mlx.core as mx
        from mlx_audio.stt import load
        self.mx=mx;mx.set_cache_limit(256*1024**2)
        self.model=load(str(CACHE/'models/Qwen3-ASR-0.6B-8bit'))

    def run(self,request):
        import numpy as np
        audio=pcm_input(request['audio']);started=time.perf_counter()
        if len(audio)<1600 or float(np.sqrt(np.mean(audio*audio)))<.001:
            return {'text':'','seconds':time.perf_counter()-started,'silence':True}
        self.mx.reset_peak_memory()
        result=self.model.generate(audio,language='Chinese',max_tokens=384,batch_size=1,
            temperature=0,hotwords=HOTWORDS,verbose=False)
        answer={'text':result.text.strip(),'seconds':time.perf_counter()-started,
            'metalPeakBytes':self.mx.get_peak_memory(),'silence':False}
        self.mx.clear_cache()
        return answer


class MossTts:
    def __init__(self,threads):
        import onnxruntime as ort
        import sentencepiece as spm
        sys.path.insert(0,str(CACHE/'src/MOSS-TTS-Nano'))
        from ort_cpu_runtime import OrtCpuRuntime

        class Runtime(OrtCpuRuntime):
            def _session(self,path):
                options=ort.SessionOptions();options.intra_op_num_threads=threads;options.inter_op_num_threads=1
                options.graph_optimization_level=ort.GraphOptimizationLevel.ORT_ENABLE_ALL
                options.add_session_config_entry('session.intra_op.allow_spinning','0')
                options.add_session_config_entry('session.inter_op.allow_spinning','0')
                return ort.InferenceSession(str(path),sess_options=options,providers=['CPUExecutionProvider'])

        self.runtime=Runtime(CACHE/'models',thread_count=threads,max_new_frames=600,sample_mode='fixed')
        self.tokenizer=spm.SentencePieceProcessor(model_file=str(CACHE/'models/MOSS-TTS-Nano-100M-ONNX/tokenizer.model'))
        self.voices={v['voice']:v for v in self.runtime.list_builtin_voices()}
        self.sample_rate=int(self.runtime.codec_meta['codec_config']['sample_rate'])

    def split(self,text):
        parts=[]
        for piece in re.findall(r'[^。！？!?；;]+[。！？!?；;]*',text):
            while len(self.tokenizer.encode(piece))>70:
                cut=min(40,len(piece));parts.append(piece[:cut]);piece=piece[cut:]
            # Short multi-sentence requests can emit EOS after the first sentence.
            # Generate each sentence independently so no later sentence silently disappears.
            if piece.strip():parts.append(piece)
        return parts

    def run(self,request):
        import numpy as np
        from scipy.signal import resample_poly
        text=str(request.get('text','')).strip()
        if not text or len(text)>240:raise ValueError('Speech must contain 1 to 240 characters')
        voice=str(request.get('voice','Weiguo'))
        if voice not in self.voices:raise ValueError('Unknown voice')
        self.runtime.rng=np.random.default_rng(int(request.get('seed',1234)))
        started=time.perf_counter();first=None;chunks=[];frame_count=0
        for text_part in self.split(text):
            tokens=self.tokenizer.encode(text_part,out_type=int)
            rows=self.runtime.build_voice_clone_request_rows(self.voices[voice]['prompt_audio_codes'],tokens)
            pending=[];self.runtime.codec_streaming_session.reset()

            def decode(force=False):
                nonlocal first
                budget=1 if first is None else 8
                if not pending or (not force and len(pending)<budget):return
                decoded=self.runtime.codec_streaming_session.run_frames(pending[:]);pending.clear()
                if decoded is None:return
                audio,length=decoded
                if length<=0:return
                if first is None:first=time.perf_counter()-started
                chunks.append(np.asarray(audio[0,:,:length],dtype=np.float32).mean(axis=0))

            def on_frame(_all,_index,frame):
                pending.append(frame);decode()

            frames=self.runtime.generate_audio_frames(rows,on_frame=on_frame);decode(True)
            self.runtime.codec_streaming_session.reset();frame_count+=len(frames)
        if not chunks:raise RuntimeError('No synthesized audio')
        audio=np.concatenate(chunks)
        target_rate=24000
        audio=resample_poly(audio,target_rate,self.sample_rate)
        output=io.BytesIO()
        with wave.open(output,'wb') as sound:
            sound.setnchannels(1);sound.setsampwidth(2);sound.setframerate(target_rate)
            sound.writeframes(np.round(np.clip(audio,-1,1)*32767).astype('<i2').tobytes())
        return {'wav':base64.b64encode(output.getvalue()).decode(),'seconds':time.perf_counter()-started,
            'firstPcmSeconds':first,'audioSeconds':len(audio)/target_rate,'voice':voice,'frames':frame_count,
            'torchImported':'torch' in sys.modules}


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--engine',choices=['qwen','moss'],required=True)
    parser.add_argument('--threads',type=int,default=1);args=parser.parse_args()
    os.environ['HF_HUB_OFFLINE']='1'
    started=time.perf_counter()
    engine=QwenAsr() if args.engine=='qwen' else MossTts(max(1,min(args.threads,2)))
    print(json.dumps({'ready':True,'engine':args.engine,'loadSeconds':time.perf_counter()-started,
        'voices':list(engine.voices) if args.engine=='moss' else []}),flush=True)
    for line in sys.stdin:
        try:result=engine.run(json.loads(line))
        except Exception as error:
            print(type(error).__name__+': '+str(error),file=sys.stderr,flush=True)
            result={'error':type(error).__name__}
        print(json.dumps(result,ensure_ascii=False),flush=True);line=''


if __name__=='__main__':main()
