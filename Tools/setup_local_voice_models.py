"""Install the selected Mac voice models into a user cache, without changing the live gateway."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

CACHE=Path.home()/'.cache/three-body-voice'
SOURCE_SHA='8b7bcc9341b3b4ef3a3a58ba1338a7d85ff133eb'
MODELS={
    'Qwen3-ASR-0.6B-8bit':('mlx-community/Qwen3-ASR-0.6B-8bit','89e96d92ba34aca20b3e29fb10cc284097d1219f'),
    'MOSS-TTS-Nano-100M-ONNX':('OpenMOSS-Team/MOSS-TTS-Nano-100M-ONNX','f52645cb467506d8e18e746ddd59482685b74e58'),
    'MOSS-Audio-Tokenizer-Nano-ONNX':('OpenMOSS-Team/MOSS-Audio-Tokenizer-Nano-ONNX','ceff0d0749bfb3fa2d61149794ec6feef0d1e1ae'),
}


def prepare():
    executable=CACHE/'local-venv/bin/python'
    if not executable.exists():
        bundled=Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3'
        base=str(bundled) if bundled.exists() else shutil.which('python3.12') or sys.executable
        subprocess.run([base,'-m','venv',str(executable.parent.parent)],check=True)
    lock_path=Path(__file__).with_name('requirements-local-voice.lock.txt')
    subprocess.run([str(executable),'-m','pip','install','--no-cache-dir','-r',str(lock_path)],check=True)
    source=CACHE/'src/MOSS-TTS-Nano'
    if not source.exists():
        source.parent.mkdir(parents=True,exist_ok=True)
        subprocess.run(['git','clone','--depth','1','https://github.com/OpenMOSS/MOSS-TTS-Nano.git',str(source)],check=True)
    current=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()
    if current!=SOURCE_SHA:
        subprocess.run(['git','-C',str(source),'fetch','--depth','1','origin',SOURCE_SHA],check=True)
        subprocess.run(['git','-C',str(source),'checkout','--detach',SOURCE_SHA],check=True)
    environment=dict(os.environ);environment['HF_XET_CHUNK_CACHE_SIZE_BYTES']='0'
    subprocess.run([str(executable),str(Path(__file__).resolve()),'--download'],check=True,env=environment)
    lock=subprocess.check_output([str(executable),'-m','pip','freeze'],text=True)
    (CACHE/'local-requirements.lock.txt').write_text(lock)
    if '--activate' in sys.argv:
        (CACHE/'active-backends.json').write_text(json.dumps({'useLocalModels':True,'asr':'qwen3-asr-mlx-8bit','tts':'moss-tts-nano-onnx-cpu','threads':1},indent=2)+'\n')
        print('Local models selected. Run Tools/play_act_one.py to start or refresh the owned gateway.',flush=True)
    else:print('Models prepared. The running game service has not been switched.',flush=True)


def download():
    from huggingface_hub import HfApi,snapshot_download
    records=[]
    for name,(repo,revision) in MODELS.items():
        destination=CACHE/'models'/name
        print('Preparing '+repo+' @ '+revision,flush=True)
        snapshot_download(repo,revision=revision,local_dir=destination,token=False,
            allow_patterns=['*.json','*.safetensors','*.txt','*.model','*.onnx','*.data','README.md'],max_workers=3)
        info=HfApi(token=False).model_info(repo,revision=revision,files_metadata=True)
        files=[]
        for item in info.siblings:
            path=destination/item.rfilename
            if not path.is_file():continue
            with path.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
            expected=getattr(item.lfs,'sha256',None) if item.lfs else None
            if expected and digest!=expected:raise RuntimeError('Checksum mismatch: '+str(path))
            files.append({'name':item.rfilename,'bytes':path.stat().st_size,'sha256':digest,'upstreamSha256Checked':bool(expected)})
        records.append({'repo':repo,'revision':revision,'path':str(destination),'files':files})
    (CACHE/'local-models.json').write_text(json.dumps({'sourceRevision':SOURCE_SHA,'models':records},ensure_ascii=False,indent=2)+'\n')


if __name__=='__main__':
    if '--legacy' in sys.argv:
        CACHE.mkdir(parents=True,exist_ok=True);(CACHE/'active-backends.json').write_text(json.dumps({'useLocalModels':False})+'\n')
        print('Legacy voice selected. Run Tools/play_act_one.py to refresh the owned gateway.')
    elif '--download' in sys.argv:download()
    else:prepare()
