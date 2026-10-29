"""Start the local offline voice gateway and open the current macOS demo build."""
import json
from pathlib import Path
import subprocess
import time
import urllib.request

ROOT=Path(__file__).resolve().parents[1]


def ready():
    try:
        with urllib.request.urlopen('http://127.0.0.1:8769/health',timeout=1) as response:
            return json.load(response).get('ready',False)
    except Exception:return False


def main():
    app=ROOT/'Builds/ActOneTraveller.app'
    if not app.exists():raise SystemExit('请先在 Unity 运行 ActOneProduction.BuildPlayer() 构建试玩包。')
    python=Path.home()/'.cache/three-body-voice/venv/bin/python'
    if not python.exists():subprocess.run(['python3',str(ROOT/'Tools/setup_voice_gateway.py')],check=True,cwd=ROOT)
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
