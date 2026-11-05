"""Exercise required-key checks through the actual local HTTP service."""
import io
import json
import os
from pathlib import Path
import subprocess
import socket
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
import wave
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from dialogue_gateway import Handler
from package_windows_playtest import validate_player


class PlayerPackageTests(unittest.TestCase):
    def test_compressed_and_uncompressed_players_are_accepted(self):
        for payload in ('globalgamemanagers', 'data.unity3d'):
            with self.subTest(payload=payload), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for name in ('ThreeBodyTraveller.exe', 'UnityPlayer.dll',
                             'ThreeBodyTraveller_Data/Managed/ThreeBodyProblem.Runtime.dll',
                             'ThreeBodyTraveller_Data/' + payload):
                    path = root / name; path.parent.mkdir(parents=True, exist_ok=True); path.touch()
                validate_player(root)

    def test_missing_scene_data_cannot_be_shipped(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ('ThreeBodyTraveller.exe', 'UnityPlayer.dll', 'ThreeBodyTraveller_Data/Managed/ThreeBodyProblem.Runtime.dll'):
                path = root / name; path.parent.mkdir(parents=True, exist_ok=True); path.touch()
            with self.assertRaisesRegex(ValueError, 'scene data'):
                validate_player(root)


class Provider(BaseHTTPRequestHandler):
    calls = 0

    def log_message(self, *_):
        pass

    def do_POST(self):
        type(self).calls += 1
        payload = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        if self.headers.get('Authorization') != 'Bearer test-only-key':
            self.send_response(401); self.end_headers(); return
        system = payload['messages'][0]['content']
        if 'Connection test.' in system:
            answer = {'ok': True}
        elif '角色：姬昌' in system:
            answer = {'speech': '先跟紧火光，别独自在戈壁里乱走。', 'gesture': 'talk', 'storyIntent': 'continue'}
        else:
            self.send_response(400); self.end_headers(); return
        data = json.dumps({'choices': [{'message': {'content': json.dumps(answer, ensure_ascii=False)}}]}, ensure_ascii=False).encode('utf-8')
        self.send_response(200); self.send_header('Content-Length', str(len(data))); self.end_headers(); self.wfile.write(data)


class PlaytestAccessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        required = type('RequiredPlayerHandler', (Handler,), {'require_online': True})
        cls.gateway = ThreadingHTTPServer(('127.0.0.1', 0), required)
        cls.provider = ThreadingHTTPServer(('127.0.0.1', 0), Provider)
        for server in (cls.gateway, cls.provider):
            threading.Thread(target=server.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls):
        for server in (cls.gateway, cls.provider):
            server.shutdown(); server.server_close()

    def request(self, path, connection, payload=None):
        data = json.dumps({**(payload or {}), 'connection': connection}).encode()
        request = urllib.request.Request(f'http://127.0.0.1:{self.gateway.server_port}' + path, data=data,
                                         headers={'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(request, timeout=5) as response:
                return response.status, json.load(response)
        except urllib.error.HTTPError as error:
            with error:
                return error.code, json.load(error)

    def connection(self, key='test-only-key'):
        return {'mode': 'custom', 'apiUrl': f'http://127.0.0.1:{self.provider.server_port}/v1',
                'apiKey': key, 'model': 'test-model', 'timeoutSeconds': 5}

    def test_missing_keys_and_offline_modes_never_reach_the_provider(self):
        before = Provider.calls
        for config in (None, {'mode': 'scripted'}, {'mode': 'gateway'}, self.connection(''), self.connection('  ')):
            with self.subTest(config=config):
                status, result = self.request('/connection-test', config)
                self.assertEqual(status, 200); self.assertFalse(result['ok'])
                status, _ = self.request('/dialogue', config)
                self.assertEqual(status, 400)
        self.assertEqual(Provider.calls, before)

    def test_invalid_key_cannot_pass_connection_test(self):
        status, result = self.request('/connection-test', self.connection('wrong-key'))
        self.assertEqual(status, 200); self.assertFalse(result['ok'])
        self.assertIn('Key', result['message'])

    def test_success_requires_a_real_provider_reply(self):
        before = Provider.calls
        status, result = self.request('/connection-test', self.connection())
        self.assertEqual(status, 200); self.assertTrue(result['ok']); self.assertEqual(result['mode'], 'online')
        self.assertEqual(Provider.calls, before + 1)

    def test_actual_npc_dialogue_reads_chinese_prompts_and_replies(self):
        status, result = self.request('/dialogue', self.connection(),
            {'npcId': 'wenwang', 'utterance': '这里安全吗？', 'history': [],
             'context': {'phase': 'OpeningTalk', 'allowedStoryIntents': ['continue'], 'openingAnswers': 0}})
        self.assertEqual(status, 200)
        self.assertEqual(result['mode'], 'online')
        self.assertEqual(result['speech'], '先跟紧火光，别独自在戈壁里乱走。')

    def test_chinese_prompts_work_with_non_utf8_system_encoding(self):
        tools = Path(__file__).resolve().parent
        code = ("import sys,json;sys.path.insert(0," + repr(str(tools)) + ");"
                "from dialogue_gateway import model_messages;"
                "p={'npcId':'wenwang','utterance':'hello','context':{'phase':'OpeningTalk'},'history':[]};"
                "s=model_messages(p)[3][0]['content'];"
                "print(json.dumps({'chinese': '\\u89d2\\u8272\\uff1a\\u59ec\\u660c' in s}))")
        result = subprocess.run([sys.executable, '-X', 'utf8=0', '-c', code], capture_output=True,
            env={**os.environ, 'LC_ALL': 'C', 'PYTHONCOERCECLOCALE': '0', 'PYTHONUTF8': '0'}, timeout=20)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors='replace'))
        self.assertTrue(json.loads(result.stdout)['chinese'])


@unittest.skipUnless(sys.platform == 'win32', 'Windows speech integration')
class WindowsVoiceTests(unittest.TestCase):
    def test_service_exits_when_the_game_process_ends(self):
        with socket.socket() as listener:
            listener.bind(('127.0.0.1', 0)); port = listener.getsockname()[1]
        parent = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(3)'], creationflags=subprocess.CREATE_NO_WINDOW)
        service = subprocess.Popen([sys.executable, str(Path(__file__).with_name('dialogue_gateway.py')),
            '--require-online', '--parent-pid', str(parent.pid), '--port', str(port)],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            parent.wait(timeout=8)
            stdout, stderr = service.communicate(timeout=12)
            self.assertEqual(service.returncode, 0, stderr.decode(errors='replace'))
            self.assertIn(b'API key required: True', stdout)
        finally:
            for process in (parent, service):
                if process.poll() is None: process.kill(); process.wait()

    def test_powershell_script_parses_in_windows_powershell(self):
        script = str(Path(__file__).with_name('windows_voice.ps1')).replace("'", "''")
        result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
            "$errors=$null; [void][System.Management.Automation.Language.Parser]::ParseFile('" + script +
            "',[ref]$null,[ref]$errors); if ($errors.Count) { $errors | ForEach-Object { $_.Message }; exit 1 }"], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_system_speech_produces_game_compatible_audio(self):
        from windows_voice import speech_wav
        with wave.open(io.BytesIO(speech_wav('Hello traveler.', 'wenwang'))) as sound:
            self.assertEqual((sound.getnchannels(), sound.getsampwidth(), sound.getframerate()), (1, 2, 16000))
            self.assertGreater(sound.getnframes(), 1600)


if __name__ == '__main__':
    unittest.main()
