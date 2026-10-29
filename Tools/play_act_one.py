"""Start the local offline voice gateway and open the current macOS demo build."""
import json
import os
from pathlib import Path
import subprocess
import signal
import time
import urllib.request

ROOT=Path(__file__).resolve().parents[1]


def health():
    try:
        with urllib.request.urlopen('http://127.0.0.1:8769/health',timeout=1) as response:
            return json.load(response)
    except Exception:return {}


def ready():return health().get('ready',False)


def refresh_owned_gateway_if_needed():
    preference=Path.home()/'.cache/three-body-voice/active-backends.json'
    desired=json.loads(preference.read_text()).get('useLocalModels',False) if preference.exists() else False
    current=health()
    if not current or (current.get('asrEngine')=='qwen3-asr-mlx-8bit')==desired:return
    record=ROOT/'Logs/ActOne/voice-gateway.pid'
    if not record.exists():raise SystemExit('8769 端口上已有另一项语音服务，请先关闭它再启动试玩。')
    pid=int(record.read_text())
    result=subprocess.run(['ps','-p',str(pid),'-o','args='],capture_output=True,text=True)
    if str(ROOT/'Tools/dialogue_gateway.py') not in result.stdout:raise SystemExit('语音进程与本项目不匹配，未自动关闭。')
    os.kill(pid,signal.SIGTERM)
    until=time.monotonic()+8
    while time.monotonic()<until and ready():time.sleep(.2)
    if ready():raise SystemExit('旧语音服务仍在退出，请稍后重新启动。')


def main():
    app=ROOT/'Builds/ActOneTraveller.app'
    if not app.exists():raise SystemExit('请先在 Unity 运行 ActOneProduction.BuildPlayer() 构建试玩包。')
    python=Path.home()/'.cache/three-body-voice/venv/bin/python'
    if not python.exists():subprocess.run(['python3',str(ROOT/'Tools/setup_voice_gateway.py')],check=True,cwd=ROOT)
    refresh_owned_gateway_if_needed()
    if not ready():
        folder=ROOT/'Logs/ActOne';folder.mkdir(parents=True,exist_ok=True)
        with (folder/'gateway.log').open('a') as log:
            process=subprocess.Popen([str(python),str(ROOT/'Tools/dialogue_gateway.py')],cwd=ROOT,
                stdin=subprocess.DEVNULL,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
        (folder/'voice-gateway.pid').write_text(str(process.pid))
        print('正在准备本机语音识别……',flush=True)
        until=time.monotonic()+35
        while time.monotonic()<until and not ready():
            if process.poll() is not None:break
            time.sleep(.3)
    print('本机语音已就绪。' if ready() else '语音暂未就绪，游戏仍可使用文字与离线剧情。',flush=True)
    subprocess.run(['open',str(app)],check=True)


if __name__=='__main__':main()
