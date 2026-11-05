"""Local Qwen3-TTS with one Metal job at a time and the game's PCM WAV format."""
from pathlib import Path
import subprocess
import tempfile
import threading
import time

CACHE = Path.home() / '.cache/three-body-voice'
ENGINE = 'qwen3-tts-0.6b-q5-k'
MODEL = 'qwen3-tts-0.6b:q5_k'
BINARY = CACHE / 'speech-cpp-0.10.1' / 'speech'
MODEL_PATH = CACHE / 'speech-cpp-0.10.1' / 'model-path.txt'
SPEAKERS = {
    'wenwang': 'uncle_fu', 'king': 'dylan', 'pope': 'ryan', 'scholar': 'aiden',
    'davinci': 'eric', 'shang_guard': 'eric', 'church_guard': 'ryan', 'citizen': 'serena',
}


class QwenTts:
    cache_version = 'qwen3-tts-0.6b-customvoice-q5-k-speech-0.10.1'

    def __init__(self):
        self.model = Path(MODEL_PATH.read_text().strip())
        if not BINARY.is_file() or not self.model.is_file():
            raise FileNotFoundError('Run Tools/setup_qwen_tts.py first.')
        self.profiles = {role: {'voice': voice, 'seed': 2333 + i}
                         for i, (role, voice) in enumerate(SPEAKERS.items())}
        self.stats = {}
        self.lock = threading.Lock()

    def speech(self, text, npc_id):
        profile = self.profiles[npc_id]
        started = time.monotonic()
        # A client waits 35 seconds for a complete WAV. Share that deadline
        # between the queue and synthesis so canceled lines cannot pile up.
        if not self.lock.acquire(timeout=30):
            raise TimeoutError('Qwen TTS is busy.')
        try:
            with tempfile.TemporaryDirectory(prefix='traveller-qwen-') as directory:
                output = Path(directory) / 'npc.wav'
                subprocess.run(
                    [str(BINARY), 'tts', str(self.model), '-o', str(output), '--device', 'gpu',
                     '--language', 'zh', '--voice', profile['voice'], '--seed', str(profile['seed']),
                     '--max-seconds', '110', '--', text],
                    check=True, capture_output=True, timeout=max(.1, 33 - (time.monotonic() - started)),
                )
                data = output.read_bytes()
            self.stats['tts'] = {'seconds': round(time.monotonic() - started, 3), 'voice': profile['voice']}
            return data
        finally:
            self.lock.release()

    def health(self):
        return {'ttsEngine': ENGINE, 'ttsModel': 'Qwen3-TTS-12Hz-0.6B-CustomVoice-Q5_K',
                'voiceProfiles': SPEAKERS, 'lastTtsTimings': self.stats.get('tts', {})}
