"""Package a Windows player with its isolated Python AI service and no credentials."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PYTHON_VERSION = '3.13.17'
PYTHON_SHA256 = '9e35319bffedab8b36ad44d8f377cb4f1733bcec257a0b6d04d94f01486ddb38'
PYTHON_URL = f'https://www.python.org/ftp/python/{PYTHON_VERSION}/python-{PYTHON_VERSION}-embed-amd64.zip'
SERVICE_FILES = ('dialogue_gateway.py', 'llm_connection.py', 'dialogue_context.py',
                 'npc_decision_policy.py', 'windows_voice.py', 'windows_voice.ps1')
README = '''三体 · 旅人 / Windows 64 位试玩版

运行：
1. 将整个 ZIP 解压到一个普通文件夹，保留所有文件及子文件夹。
2. 双击 ThreeBodyTraveller.exe；无需安装 Unity 或 Python。
3. 在“设置 → AI 对话”选择 DeepSeek 或自定义兼容接口，填写 API 地址、模型名称和自己的 API Key。
4. 点击“测试连接”，成功后点击“保存并返回”，再点击“开始旅程”。

本游戏必须配置 AI 才能游玩。未填 Key、连接测试失败或测试后更换模型／Key 时，不能开始旅程。
Key 只在本次运行中使用，关闭游戏后不会保存；下次启动需要重新填写。包内不含开发者 Key。
使用自定义服务时，请填写该服务提供的 Base URL、模型 ID 与对应 Key。

操作：WASD 行走，Shift 快走，鼠标环顾，Enter 文字交谈，F 按住说话，J 观察簿，Esc 菜单。
可在“显示与画质”设置窗口／全屏、分辨率和画质；运行较慢时可选择低画质。
预录剧情语音已随包提供；Windows 动态语音使用系统语音。缺少中文识别组件时请按 Enter 打字交谈。
需要 Windows 10／11 64 位和可访问所填 AI 服务的网络。
'''


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def package(build, output, python_zip):
    build, output = build.resolve(), output.resolve()
    for relative in ('ThreeBodyTraveller.exe', 'UnityPlayer.dll', 'ThreeBodyTraveller_Data/globalgamemanagers',
                     'ThreeBodyTraveller_Data/Managed/ThreeBodyProblem.Runtime.dll'):
        if not (build / relative).is_file():
            raise ValueError('Incomplete Windows player: ' + relative)
    python_zip.parent.mkdir(parents=True, exist_ok=True)
    if not python_zip.exists():
        with urllib.request.urlopen(PYTHON_URL, timeout=60) as source, python_zip.open('wb') as target:
            shutil.copyfileobj(source, target)
    if digest(python_zip) != PYTHON_SHA256:
        raise ValueError('Python runtime checksum mismatch')
    ai = build / 'AI'
    runtime, tools = ai / 'runtime', ai / 'Tools'
    runtime.mkdir(parents=True, exist_ok=True); tools.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(python_zip) as archive:
        archive.extractall(runtime)
    path_file = runtime / 'python313._pth'
    lines = path_file.read_text().splitlines()
    if '../Tools' not in lines:
        path_file.write_text('\n'.join(lines + ['../Tools']) + '\n')
    for name in SERVICE_FILES:
        shutil.copy2(ROOT / 'Tools' / name, tools / name)
    prompts = ai / 'Assets/_Project/Dialogue/Prompts'
    prompts.mkdir(parents=True, exist_ok=True)
    for source in (ROOT / 'Assets/_Project/Dialogue/Prompts').glob('*.txt'):
        shutil.copy2(source, prompts / source.name)
    (build / '试玩说明.txt').write_text(README, encoding='utf-8-sig')
    excluded = ('BackUpThisFolder_ButDontShipItWithYourGame', 'BurstDebugInformation_DoNotShip', '__pycache__')
    files = sorted(p for p in build.rglob('*') if p.is_file() and
                   not any(any(term in part for term in excluded) for part in p.relative_to(build).parts)
                   and p.name != 'playtest-manifest.json' and not p.name.endswith('.build.json'))
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    manifest = {'sourceCommit': commit, 'platform': 'Windows x64', 'aiRequired': True,
                'pythonVersion': PYTHON_VERSION, 'pythonArchiveSha256': PYTHON_SHA256,
                'files': {str(p.relative_to(build)).replace('\\', '/'): {'bytes': p.stat().st_size, 'sha256': digest(p)} for p in files}}
    manifest_file = build / 'playtest-manifest.json'
    manifest_file.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files + [manifest_file]:
            archive.write(path, Path('ThreeBodyTraveller-Windows-x64') / path.relative_to(build))
    checksum = digest(output)
    output.with_name(output.name + '.sha256').write_text(checksum + '  ' + output.name + '\n')
    print(json.dumps({'zip': str(output), 'bytes': output.stat().st_size,
                      'sha256': checksum, 'files': len(files) + 1, 'sourceCommit': commit}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build-dir', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--python-zip', type=Path, default=Path.home() / '.cache/three-body-build/python-3.13.17-embed-amd64.zip')
    args = parser.parse_args()
    package(args.build_dir, args.output, args.python_zip)
