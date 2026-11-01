"""Author deterministic, original palace Foley; no recordings or external services."""
from array import array
import json
import math
from pathlib import Path
import random
import sys
import wave

OUT = Path(__file__).resolve().parents[1] / 'Assets/_Project/Art/PalaceInterior/Audio'
RATE = 22050


def make(name, seconds, kind, seed):
    rng = random.Random(seed)
    values = []
    low = 0.0
    for i in range(int(RATE * seconds)):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        low += .065 * (noise - low)
        if kind == 'sand':
            value = (noise - low) * (.16 + .05 * math.sin(t * 49) ** 2)
        elif kind == 'fire':
            value = low * .35
        elif kind == 'grind':
            envelope = math.sin(math.pi * t / seconds) ** .6
            value = (low * .8 + noise * .08 + math.sin(t * 2 * math.pi * 61) * .04) * envelope
        else:
            value = (low * .8 + noise * .28 + math.sin(t * 2 * math.pi * 87) * .12) * math.exp(-t * 30)
        values.append(value)
    if kind in ('sand', 'fire'):
        for _ in range(int(seconds * (48 if kind == 'sand' else 11))):
            start = rng.randrange(len(values) - 1100)
            amplitude = rng.uniform(.06, .18) if kind == 'sand' else rng.uniform(.10, .45)
            for k in range(400):
                values[start + k] += rng.uniform(-1, 1) * amplitude * math.exp(-k / (40 if kind == 'sand' else 110))
        # Matching zero-valued ends avoid clicks at the loop seam.
        for i in range(256):
            values[i] *= i / 256
            values[-1 - i] *= i / 256
    peak = max(abs(x) for x in values) or 1
    pcm = array('h', (int(max(-1, min(1, x / peak * .72)) * 32767) for x in values))
    if sys.byteorder != 'little':
        pcm.byteswap()
    with wave.open(str(OUT / (name + '.wav')), 'wb') as stream:
        stream.setparams((1, 2, RATE, len(pcm), 'NONE', 'not compressed'))
        stream.writeframes(pcm.tobytes())


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for args in [('SandTrickle', 4, 'sand', 71), ('EmberCrackle', 6, 'fire', 83), ('StoneSlide', 2, 'grind', 97), ('StoneFootstep', .24, 'step', 101)]:
        make(*args)
    (OUT / 'provenance.json').write_text(json.dumps({'authoredLocally': True, 'source': 'Tools/prepare_palace_audio.py', 'sampleRate': RATE, 'channels': 1, 'description': 'Original procedural Foley, not field recordings.'}, indent=2) + '\n')
