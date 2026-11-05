"""Role-scoped context budgets. Stable role prompt first; current facts close to the new question."""
from functools import lru_cache
import json
from pathlib import Path
import threading

CONTEXT_WINDOWS = (32_000, 128_000, 512_000, 1_000_000)
OUTPUT_RESERVE = 320
REQUEST_LIMIT = 16_000_000
_tokenizer = None
_loaded = False
_lock = threading.Lock()


def context_window(config):
    if config is None: return CONTEXT_WINDOWS[0]
    if not isinstance(config, dict): raise ValueError('Invalid context configuration')
    value = config.get('contextWindowTokens', CONTEXT_WINDOWS[0])
    if isinstance(value, bool) or value not in CONTEXT_WINDOWS: raise ValueError('Unsupported context budget')
    return value


def official_tokenizer():
    global _loaded, _tokenizer
    with _lock:
        if not _loaded:
            _loaded = True
            path = Path.home()/'.cache/three-body-voice/tokenizers/deepseek-v4/tokenizer.json'
            if path.is_file():
                try:
                    from tokenizers import Tokenizer
                    _tokenizer = Tokenizer.from_file(str(path))
                except (ImportError, ValueError, OSError): pass
        return _tokenizer


@lru_cache(maxsize=8192)
def count_text(text, use_deepseek=False):
    tokenizer = official_tokenizer() if use_deepseek else None
    if tokenizer is not None:
        return len(tokenizer.encode(text, add_special_tokens=False).ids)
    # A byte bound is intentionally conservative for an unknown provider's
    # tokenizer. Never pretend this is exact billing or expand its real window.
    return len(text.encode('utf-8'))


def message_cost(message, deepseek):
    return count_text(message['content'], deepseek) + 32


def deepseek_config(config):
    if not isinstance(config, dict): return True  # Legacy environment-based DeepSeek.
    return config.get('mode', 'gateway') in {'gateway', 'deepseek'} or str(config.get('apiUrl', '')).startswith('https://api.deepseek.com/')


def history_turns(history):
    """Keep complete answered turns. Game-produced assistant text is speech, not a new system instruction."""
    if not isinstance(history, list): raise ValueError('Invalid history')
    pending = None
    for item in history:
        if not isinstance(item, dict): continue
        role, content = item.get('role'), item.get('content')
        if role not in {'user', 'assistant'} or not isinstance(content, str) or not content.strip(): continue
        if role == 'user':
            pending = {'role': 'user', 'content': content[:1200]}
        elif pending is not None:
            speech = content[:240]
            yield (pending, {'role': 'assistant', 'content': json.dumps(
                {'speech': speech, 'gesture': 'talk', 'storyIntent': 'continue', 'targetId': ''}, ensure_ascii=False)})
            pending = None
    # Accept a valid trailing user entry from legacy clients; never accept an
    # orphan assistant or client-supplied system/tool roles.
    if pending is not None: yield (pending,)


def assemble_messages(static_prompt, current_facts, history, utterance, config=None):
    window = context_window(config); deepseek = deepseek_config(config)
    first = {'role': 'system', 'content': static_prompt}
    now = {'role': 'system', 'content': current_facts}
    question = {'role': 'user', 'content': utterance}
    reserve = OUTPUT_RESERVE + max(1024, window//100)
    remaining = window - reserve - sum(message_cost(m, deepseek) for m in (first, now, question))
    if remaining < 0: raise ValueError('Current role/facts exceed selected context window')
    turns = list(history_turns(history)); selected = []
    for turn in reversed(turns):
        cost = sum(message_cost(m, deepseek) for m in turn)
        if cost > remaining: break
        remaining -= cost; selected.append(turn)
    messages = [first]
    for turn in reversed(selected): messages.extend(turn)
    messages.extend((now, question))
    return messages
