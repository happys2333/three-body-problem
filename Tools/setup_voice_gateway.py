"""Prepare the local Chinese speech service in a user-owned cache, preserving project dependencies."""
import hashlib
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request
import zipfile

CACHE=Path.home()/'.cache/three-body-voice'
EXPECTED='3af8b0e7e0f835ae9d414ce5df580237a3cfb08d586c9fbbb0f7ff29ad5b14ba'


def main():
    bundled=Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3'
    python=str(bundled) if bundled.exists() else shutil.which('python3.12') or sys.executable
    executable=CACHE/'venv/bin/python'
    if not executable.exists():subprocess.run([python,'-m','venv',str(CACHE/'venv')],check=True)
    subprocess.run([str(executable),'-m','pip','install','vosk==0.3.44'],check=True)
    models=CACHE/'models';models.mkdir(parents=True,exist_ok=True)
    archive=models/'vosk-model-small-cn-0.22.zip'
    if not archive.exists():
        temporary=archive.with_suffix('.download')
        urllib.request.urlretrieve('https://alphacephei.com/vosk/models/vosk-model-small-cn-0.22.zip',temporary)
        temporary.replace(archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest()!=EXPECTED:raise RuntimeError('Chinese model checksum mismatch')
    if not (models/'vosk-model-small-cn-0.22/am/final.mdl').exists():
        with zipfile.ZipFile(archive) as source:
            for entry in source.infolist():
                if not (models/entry.filename).resolve().is_relative_to(models.resolve()):raise RuntimeError('Unexpected archive path')
            source.extractall(models)
    print('Ready. Start with:',str(executable),str(Path(__file__).with_name('dialogue_gateway.py')))


if __name__=='__main__':main()
