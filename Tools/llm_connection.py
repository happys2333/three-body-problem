"""Per-game OpenAI-compatible connections. Never persist or log credentials."""
from dataclasses import dataclass, field
import json
import os
import socket
import urllib.error
import urllib.parse
import urllib.request
from dialogue_context import context_window


class ConnectionConfigError(ValueError):
    pass


class ProviderRejectedError(ValueError):
    """An upstream content check rejected the input; no NPC decision was made."""
    pass


class ToolCallingUnsupported(ValueError):
    """The configured provider rejected the optional tools request."""


def provider_response(data):
    try:
        value = json.loads(data)
    except (ValueError, UnicodeDecodeError):
        # Discovery can return HTTP 200 + an SSE error even with stream=False.
        # Recognize that error without treating it as an NPC reply or retrying it.
        for line in data.decode('utf-8', errors='replace').splitlines()[:4]:
            if not line.startswith('data:'):
                continue
            try:
                value = json.loads(line[5:].strip())
            except ValueError:
                continue
            if isinstance(value, dict) and isinstance(value.get('error'), dict):
                if value['error'].get('code') == 'content_check_rejected':
                    raise ProviderRejectedError('Upstream content check rejected the request') from None
        raise ValueError('Expected JSON service response') from None
    if isinstance(value, dict) and isinstance(value.get('error'), dict):
        if value['error'].get('code') == 'content_check_rejected':
            raise ProviderRejectedError('Upstream content check rejected the request')
    return value


@dataclass(frozen=True)
class Connection:
    api_url: str
    model: str
    key: str = field(repr=False)
    timeout: int
    deepseek: bool


def endpoint(value):
    if not isinstance(value, str) or len(value) > 2048:
        raise ConnectionConfigError('API 地址格式不正确。')
    try:
        url = urllib.parse.urlsplit(value.strip())
        if (url.scheme not in {'https', 'http'} or not url.hostname or
                url.username or url.password or url.query or url.fragment):
            raise ValueError()
        url.port  # Validate the port without echoing the supplied URL.
        path = url.path.rstrip('/')
        if not path.endswith('/chat/completions'):
            if not path and url.hostname.lower() != 'api.deepseek.com':
                path = '/v1'
            path += '/chat/completions'
        return urllib.parse.urlunsplit((url.scheme, url.netloc, path, '', ''))
    except ValueError:
        raise ConnectionConfigError('请输入有效的 http / https API 地址，Key 请单独填写。') from None


def resolve_connection(config=None, online=False):
    if config is not None and not isinstance(config, dict):
        raise ConnectionConfigError('对话配置格式不正确。')
    config = config or {}
    context_window(config)
    timeout = config.get('timeoutSeconds', 8)
    if isinstance(timeout, bool) or not isinstance(timeout, int) or not 5 <= timeout <= 600:
        raise ConnectionConfigError('等待时间需在 5–600 秒之间。')
    mode = config.get('mode', 'gateway')
    if mode == 'scripted':
        return None
    if mode == 'gateway':
        key = os.environ.get('DEEPSEEK_API_KEY', '')
        if not online or not key:
            return None
        return Connection(endpoint(os.environ.get('DEEPSEEK_API_URL', 'https://api.deepseek.com/chat/completions')),
                          os.environ.get('DEEPSEEK_MODEL', 'deepseek-flash'), key, timeout, True)
    if mode not in {'deepseek', 'custom'}:
        raise ConnectionConfigError('请选择有效的对话模式。')
    model = config.get('model', '')
    key = config.get('apiKey', '')
    timeout = config.get('timeoutSeconds', 20)
    if not isinstance(model, str) or not model.strip() or len(model) > 160 or '\n' in model or '\r' in model:
        raise ConnectionConfigError('请填写服务商提供的模型名称。')
    if not isinstance(key, str) or len(key) > 4096 or '\n' in key or '\r' in key:
        raise ConnectionConfigError('API Key 格式不正确。')
    if isinstance(timeout, bool) or not isinstance(timeout, int) or not 5 <= timeout <= 600:
        raise ConnectionConfigError('等待时间需在 5–600 秒之间。')
    if mode == 'deepseek' and not key.strip():
        raise ConnectionConfigError('请填写 DeepSeek API Key。')
    url = endpoint(config.get('apiUrl', ''))
    is_deepseek = mode == 'deepseek' or urllib.parse.urlsplit(url).hostname.lower() == 'api.deepseek.com'
    return Connection(url, model.strip(), key.strip(), timeout, is_deepseek)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        # Do not forward a player's Authorization header to a redirected host.
        raise urllib.error.HTTPError(request.full_url, code, 'API redirect', headers, fp)


def chat_completion_choice(connection, messages, max_tokens=320, opener=None, tools=None):
    body = {'model': connection.model, 'messages': messages,
            'response_format': {'type': 'json_object'}, 'max_tokens': max_tokens,
            'temperature': .7, 'stream': False}
    if tools:
        body.update(tools=tools, tool_choice='auto')
    # This endpoint's Atria model counts default reasoning against max_tokens.
    # NPC JSON was truncated within the existing 320-token output budget. The
    # live non-thinking profile returns complete short replies within that budget.
    atria = (urllib.parse.urlsplit(connection.api_url).hostname == 'discovery-api.intern-ai.org.cn'
             and connection.model.casefold().startswith('atria-dawn'))
    if connection.deepseek or atria:
        body['thinking'] = {'type': 'disabled'}
    headers = {'Content-Type': 'application/json'}
    if connection.key:
        headers['Authorization'] = 'Bearer ' + connection.key
    request = urllib.request.Request(connection.api_url, data=json.dumps(body, ensure_ascii=False).encode(), headers=headers)
    open_request = opener or urllib.request.build_opener(NoRedirect()).open
    try:
        with open_request(request, timeout=connection.timeout) as response:
            raw = provider_response(response.read())
    except urllib.error.HTTPError as error:
        unsupported = False
        if tools and error.code in {400, 422}:
            detail = error.read(4096).decode('utf-8', errors='replace').lower()
            unsupported = 'tool' in detail and any(word in detail for word in
                ('unsupported', 'not support', 'not allowed', 'unknown field', 'extra_forbidden'))
        error.close()
        if unsupported:
            raise ToolCallingUnsupported() from None
        raise
    return raw['choices'][0]


def chat_completion(connection, messages, max_tokens=320, opener=None):
    choice = chat_completion_choice(connection, messages, max_tokens=max_tokens, opener=opener)
    return json_reply(choice)


def json_reply(choice):
    value = json.loads(choice['message']['content'])
    if not isinstance(value, dict):
        raise ValueError('Expected JSON object')
    return value


def require_player_connection(config):
    if not isinstance(config, dict) or config.get('mode') not in {'deepseek', 'custom'}:
        raise ConnectionConfigError('请配置在线 AI 模型；试玩版不提供离线游玩。')
    if not isinstance(config.get('apiKey'), str) or not config['apiKey'].strip():
        raise ConnectionConfigError('请填写 API Key。')
    return resolve_connection(config)


def connection_test(config=None, online=False, opener=None, require_online=False):
    try:
        connection = require_player_connection(config) if require_online else resolve_connection(config, online)
        if connection is None:
            return {'ok': True, 'mode': 'scripted', 'message': '本地服务可用；当前使用离线剧情对白。'}
        value = chat_completion(connection, [
            {'role': 'system', 'content': 'Connection test. Reply only with the JSON object {"ok":true}.'},
            {'role': 'user', 'content': 'Return the JSON connection-test result.'}
        ], max_tokens=32, opener=opener)
        if value.get('ok') is not True:
            return {'ok': False, 'message': '模型可以访问，但未返回所需的 JSON 格式，请检查兼容接口与模型。'}
        return {'ok': True, 'mode': 'online', 'message': '连接成功，所选模型已返回有效响应。'}
    except ConnectionConfigError as error:
        return {'ok': False, 'message': str(error)}
    except ProviderRejectedError:
        return {'ok': False, 'message': '服务商拒绝了这次输入；未获得模型回答。'}
    except urllib.error.HTTPError as error:
        messages = {401: '认证失败，请检查 API Key。', 403: '接口拒绝访问，请检查 Key 的权限。',
                    404: '接口或模型未找到，请检查 API 地址与模型名称。',
                    429: '请求受限或额度不足，请检查服务商额度。'}
        return {'ok': False, 'message': messages.get(error.code, '模型服务返回错误，请检查地址与服务状态。')}
    except (TimeoutError, socket.timeout):
        return {'ok': False, 'message': '连接超时，请检查网络或增加回复等待时间。'}
    except urllib.error.URLError:
        return {'ok': False, 'message': '无法访问模型接口，请检查网络与 API 地址。'}
    except Exception:
        return {'ok': False, 'message': '接口响应格式不兼容，请使用 Chat Completions 兼容接口。'}
