"""Install DeepSeek's published V4 tokenizer data in the user's cache, without running bundled code."""
import hashlib
import io
import json
from pathlib import Path
import urllib.request
import zipfile

URL = 'https://cdn.deepseek.com/api-docs/deepseek_v4_tokenizer.zip'
SHA256 = 'e7310d1dafe0a86d8a5629fe78a7c763760f651db9b8682718a1781dcd6fe495'
TARGET = Path.home()/'.cache/three-body-voice/tokenizers/deepseek-v4'


def main():
    TARGET.mkdir(parents=True, exist_ok=True)
    archive = TARGET/'source.zip'
    if archive.exists() and hashlib.sha256(archive.read_bytes()).hexdigest() == SHA256:
        raw = archive.read_bytes()
    else:
        with urllib.request.urlopen(URL, timeout=30) as response: raw = response.read(10_000_001)
        if len(raw) > 10_000_000 or hashlib.sha256(raw).hexdigest() != SHA256:
            raise RuntimeError('Official tokenizer package changed; verify its source before updating the pinned hash.')
        archive.write_bytes(raw)
    with zipfile.ZipFile(io.BytesIO(raw)) as source:
        for name in ('tokenizer.json', 'tokenizer_config.json'):
            data = source.read('deepseek_v4_tokenizer/'+name)
            if len(data) > 15_000_000: raise ValueError('Tokenizer entry too large')
            (TARGET/name).write_bytes(data)
    provenance = {'url': URL, 'sha256': SHA256, 'extracted': ['tokenizer.json', 'tokenizer_config.json'], 'executedBundledCode': False}
    (TARGET/'provenance.json').write_text(json.dumps(provenance, indent=2)+'\n')
    print(json.dumps(provenance))


if __name__ == '__main__': main()
