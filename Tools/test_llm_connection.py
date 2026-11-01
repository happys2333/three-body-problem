"""Connection configuration, real local HTTP routing and failure behaviour; no paid calls."""
import io
import json
import os
import threading
import unittest
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from unittest.mock import patch

import dialogue_gateway as gateway
import llm_connection as llm


class ConnectionTests(unittest.TestCase):
    def config(self, **changes):
        return {'mode': 'custom', 'apiUrl': 'https://example.test/v1', 'apiKey': 'test-session-key',
                'model': 'chosen-model', 'timeoutSeconds': 20, **changes}

    def reply(self, value):
        return io.BytesIO(json.dumps({'choices': [{'message': {'content': json.dumps(value)}}]}).encode())

    def test_base_full_and_deepseek_endpoints(self):
        for url, expected in [('https://api.deepseek.com/', 'https://api.deepseek.com/chat/completions'),
                              ('https://example.test', 'https://example.test/v1/chat/completions'),
                              ('http://localhost:11434/v1/', 'http://localhost:11434/v1/chat/completions'),
                              ('https://example.test/proxy/v1/chat/completions', 'https://example.test/proxy/v1/chat/completions')]:
            self.assertEqual(llm.endpoint(url), expected)

    def test_ui_connection_is_an_explicit_opt_in_without_server_online_flag(self):
        captured = {}
        def fake(request, timeout):
            captured.update(url=request.full_url, body=json.loads(request.data), auth=request.get_header('Authorization'), timeout=timeout)
            return self.reply({'speech': '先靠近火光。', 'gesture': 'think', 'storyIntent': 'continue'})
        payload = {'npcId': 'wenwang', 'utterance': '这里安全吗？', 'context': {'phase': 'OpeningTalk'}, 'connection': self.config()}
        with patch.dict(os.environ, {'DEEPSEEK_API_KEY': 'different-server-key'}):
            result = gateway.dialogue(payload, online=False, opener=fake)
        self.assertEqual(result['mode'], 'online'); self.assertEqual(result['speech'], '先靠近火光。')
        self.assertEqual(captured['url'], 'https://example.test/v1/chat/completions')
        self.assertEqual(captured['body']['model'], 'chosen-model')
        self.assertEqual(captured['auth'], 'Bearer test-session-key'); self.assertEqual(captured['timeout'], 20)
        self.assertNotIn('thinking', captured['body'])
        self.assertNotIn('test-session-key', json.dumps(captured['body']))
        self.assertNotIn('test-session-key', repr(llm.resolve_connection(self.config())))

    def test_offline_choice_never_contacts_an_upstream_even_with_server_key(self):
        def fail(*args, **kwargs): self.fail('Offline mode made a network request')
        payload = {'npcId': 'king', 'utterance': '请看表章。', 'context': {}, 'connection': {'mode': 'scripted'}}
        with patch.dict(os.environ, {'DEEPSEEK_API_KEY': 'server-key'}):
            self.assertEqual(gateway.dialogue(payload, online=True, opener=fail)['mode'], 'scripted')
            self.assertEqual(llm.connection_test({'mode': 'scripted'}, True, fail)['mode'], 'scripted')

    def test_connection_test_requires_a_real_valid_upstream_reply(self):
        result = llm.connection_test(self.config(), opener=lambda *_args, **_kw: self.reply({'ok': True}))
        self.assertTrue(result['ok']); self.assertEqual(result['mode'], 'online')
        result = llm.connection_test(self.config(), opener=lambda *_args, **_kw: self.reply({'speech': 'not a connection test'}))
        self.assertFalse(result['ok'])

    def test_connection_failure_is_not_reported_as_success_and_never_echoes_key(self):
        for error in [TimeoutError('test-session-key'), urllib.error.HTTPError('test-session-key', 401, 'test-session-key', {}, None),
                      ValueError('test-session-key')]:
            def fail(*_args, **_kwargs): raise error
            result = llm.connection_test(self.config(), opener=fail)
            self.assertFalse(result['ok']); self.assertNotIn('test-session-key', json.dumps(result))
            if hasattr(error, 'close'): error.close()

    def test_invalid_config_does_not_become_a_header_or_file_request(self):
        for changes in [{'apiUrl': 'file:///tmp/credentials'}, {'apiUrl': 'https://user:password@example.test/v1'},
                        {'apiUrl': 'https://example.test/v1?api_key=secret'}, {'apiKey': 'x\r\nInjected: header'},
                        {'model': ''}, {'timeoutSeconds': 1000}, {'timeoutSeconds': True}, {'mode': 'unexpected'}]:
            self.assertFalse(llm.connection_test(self.config(**changes))['ok'])

    def test_missing_deepseek_key_fails_test_but_dialogue_keeps_the_story_playable(self):
        config = self.config(mode='deepseek', apiKey='')
        self.assertFalse(llm.connection_test(config)['ok'])
        payload = {'npcId': 'king', 'utterance': '大王。', 'context': {}, 'connection': config}
        self.assertEqual(gateway.dialogue(payload)['mode'], 'scripted')

    def test_redirect_does_not_forward_the_authorization_header(self):
        request = urllib.request.Request('https://example.test/v1', headers={'Authorization': 'Bearer test-session-key'})
        with self.assertRaises(urllib.error.HTTPError) as rejected:
            llm.NoRedirect().redirect_request(request, None, 302, 'redirect', {}, 'https://elsewhere.test/steal')
        rejected.exception.close()


class LocalHttpRoutingTests(unittest.TestCase):
    def test_real_gateway_forwards_custom_settings_and_tests_without_changing_server_mode(self):
        captured = []
        class Upstream(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def do_POST(self):
                body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
                captured.append((self.path, body['model'], self.headers.get('Authorization')))
                value = {'ok': True} if body['max_tokens'] == 32 else {'speech': '寡人先看表章。', 'storyIntent': 'kill_player'}
                data = json.dumps({'choices': [{'message': {'content': json.dumps(value)}}]}).encode()
                self.send_response(200); self.send_header('Content-Length', str(len(data))); self.end_headers(); self.wfile.write(data)
        class LocalGateway(gateway.Handler): online = False
        upstream = ThreadingHTTPServer(('127.0.0.1', 0), Upstream)
        local = ThreadingHTTPServer(('127.0.0.1', 0), LocalGateway)
        for server in (upstream, local): threading.Thread(target=server.serve_forever, daemon=True).start()
        config = {'mode': 'custom', 'apiUrl': 'http://127.0.0.1:%d/v1' % upstream.server_port,
                  'model': 'actual-selected-model', 'apiKey': 'test-session-key', 'timeoutSeconds': 5}
        def post(path, payload, **headers):
            req = urllib.request.Request('http://127.0.0.1:%d%s' % (local.server_port, path),
                                         json.dumps(payload).encode(), {'Content-Type': 'application/json', **headers})
            with urllib.request.urlopen(req, timeout=5) as response: return json.load(response)
        try:
            self.assertTrue(post('/connection-test', {'connection': config})['ok'])
            reply = post('/dialogue', {'npcId': 'king', 'utterance': '请看表章。', 'context': {}, 'connection': config})
            self.assertEqual(reply['mode'], 'online'); self.assertEqual(reply['storyIntent'], 'continue')
            self.assertEqual(captured, [('/v1/chat/completions', 'actual-selected-model', 'Bearer test-session-key')] * 2)
            self.assertFalse(LocalGateway.online)
            with self.assertRaises(urllib.error.HTTPError) as rejected:
                post('/connection-test', {'connection': config}, Origin='https://foreign.test')
            self.assertEqual(rejected.exception.code, 403)
            rejected.exception.close()
        finally:
            for server in (local, upstream): server.shutdown(); server.server_close()


if __name__ == '__main__': unittest.main()
