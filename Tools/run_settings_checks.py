"""Native settings UI -> real local gateway -> fake model. No credentials or paid calls."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import dialogue_gateway as gateway

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT/'Builds/ActOneTraveller.app')
    parser.add_argument('--output', type=Path, default=ROOT/'Documentation/Settings/2026-11-01/Native')
    args = parser.parse_args(); captured = []
    class Upstream(BaseHTTPRequestHandler):
        def log_message(self, *args): pass
        def do_POST(self):
            body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
            auth = self.headers.get('Authorization', '')
            captured.append({'path': self.path, 'model': body['model'], 'hasBearerKey': bool(auth),
                             'credentialMatched': auth == 'Bearer test-session-only-key',
                             'connectionTest': body['max_tokens'] == 32})
            if auth != 'Bearer test-session-only-key':
                self.send_response(401); self.send_header('Content-Length', '0'); self.end_headers(); return
            value = {'ok': True} if body['max_tokens'] == 32 else {'speech': '风很大，先靠近火光。留意天边，我们一起走。', 'gesture': 'think', 'storyIntent': 'continue'}
            raw = json.dumps({'choices': [{'message': {'content': json.dumps(value, ensure_ascii=False)}}]}, ensure_ascii=False).encode()
            self.send_response(200); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(raw))); self.end_headers(); self.wfile.write(raw)
    class LocalGateway(gateway.Handler):
        online = False
        def do_POST(self):
            if self.path == '/transcribe':
                raw = self.rfile.read(int(self.headers['Content-Length']))
                captured.append({'path':'/transcribe','syntheticMicrophone':True,'pcmWav':raw[:4]==b'RIFF','bytes':len(raw)})
                time.sleep(.35)
                self.send({'text':'这里安全吗？'})
                return
            super().do_POST()
    upstream = ThreadingHTTPServer(('127.0.0.1', 0), Upstream)
    local = ThreadingHTTPServer(('127.0.0.1', 0), LocalGateway)
    for server in (upstream, local): threading.Thread(target=server.serve_forever, daemon=True).start()
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    report = output/'report.json'
    if report.exists(): report.unlink()
    app = args.app.resolve()
    executable = app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    command = [str(executable), '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
               '-logFile', str(output/'player.log'), '-settings-player-check', str(report),
               '-settings-test-gateway', 'http://127.0.0.1:%d' % local.server_port,
               '-settings-test-upstream', 'http://127.0.0.1:%d/v1' % upstream.server_port]
    try:
        with (output/'launcher.log').open('w') as log:
            run = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=120)
        data = json.loads(report.read_text()) if report.exists() else {'passed': False, 'errors': ['No native report']}
        evidence = {'realPaidApiCalls': 0, 'gatewayOnlineFlag': False, 'requests': captured}
        (output/'upstream-evidence.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2)+'\n')
        print(json.dumps({'exit': run.returncode, **data}, ensure_ascii=False))
        if run.returncode or not data['passed']: raise SystemExit(1)
    finally:
        for server in (local, upstream): server.shutdown(); server.server_close()


if __name__ == '__main__': main()
