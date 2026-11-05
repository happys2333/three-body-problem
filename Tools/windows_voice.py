"""Windows system speech adapter; no Python packages or model downloads required."""
import ctypes
import json
from pathlib import Path
import subprocess
import tempfile

SCRIPT = Path(__file__).with_name('windows_voice.ps1')


def run(operation, source, target=None):
    command = ['powershell.exe', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
               '-File', str(SCRIPT), '-Operation', operation, '-InputFile', str(source)]
    if target is not None:
        command.extend(['-OutputFile', str(target)])
    return subprocess.run(command, check=True, capture_output=True, timeout=30,
                          creationflags=subprocess.CREATE_NO_WINDOW)


def speech_wav(text, npc_id):
    with tempfile.TemporaryDirectory(prefix='traveller-speech-') as folder:
        source, target = Path(folder) / 'speech.json', Path(folder) / 'speech.wav'
        source.write_text(json.dumps({'text': text, 'npcId': npc_id}, ensure_ascii=False), encoding='utf-8')
        run('speak', source, target)
        return target.read_bytes()


def transcribe(wav):
    with tempfile.TemporaryDirectory(prefix='traveller-listen-') as folder:
        source = Path(folder) / 'input.wav'
        source.write_bytes(wav)
        return json.loads(run('recognize', source).stdout.decode('utf-8-sig'))['text']


def watch_parent(pid, server):
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
    kernel.OpenProcess.restype = ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    handle = kernel.OpenProcess(0x00100000, False, pid)
    if handle:
        kernel.WaitForSingleObject(handle, 0xffffffff)
        kernel.CloseHandle(handle)
    server.shutdown()
