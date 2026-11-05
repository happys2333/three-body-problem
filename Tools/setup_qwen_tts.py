"""Install and select the tested Qwen3-TTS backend on Apple Silicon."""
import hashlib
import json
from pathlib import Path
import platform
import subprocess
import urllib.request
import zipfile

from qwen_tts import BINARY, CACHE, ENGINE, MODEL, MODEL_PATH

ARCHIVE = 'speech-0.10.1-macos-arm64-metal.zip'
RELEASE = 'https://github.com/nyosegawa/speech.cpp/releases/download/v0.10.1/'
SHA256 = 'ff24f6f58c9d2dedb01caa88ba043ae845c76eff228e4cbfae5de749174404c1'


def main():
    if platform.system() != 'Darwin' or platform.machine() != 'arm64':
        raise SystemExit('This installer requires Apple Silicon macOS.')
    CACHE.mkdir(parents=True, exist_ok=True)
    folder = BINARY.parent
    folder.mkdir(exist_ok=True)
    if not BINARY.exists() or not (folder / 'libspeech.4.dylib').exists():
        with urllib.request.urlopen(RELEASE + ARCHIVE, timeout=60) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != SHA256:
            raise RuntimeError('speech.cpp archive checksum mismatch.')
        from io import BytesIO
        with zipfile.ZipFile(BytesIO(data)) as archive:
            for name in ('speech', 'libspeech.4.dylib'):
                file = folder / name
                temporary = file.with_suffix(file.suffix + '.tmp')
                temporary.write_bytes(archive.read(name))
                temporary.chmod(0o755)
                temporary.replace(file)
    model_path = subprocess.check_output([str(BINARY), 'pull', MODEL], text=True).strip()
    if not Path(model_path).is_file():
        raise RuntimeError('speech.cpp did not return a model file.')
    MODEL_PATH.write_text(model_path + '\n')
    preference = CACHE / 'active-backends.json'
    settings = json.loads(preference.read_text()) if preference.exists() else {}
    settings['tts'] = ENGINE
    temporary = preference.with_suffix('.tmp')
    temporary.write_text(json.dumps(settings, indent=2) + '\n')
    temporary.replace(preference)
    print('Qwen3-TTS Q5_K selected. Run Tools/play_act_one.py to start the game.')


if __name__ == '__main__':
    main()
