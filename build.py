from pathlib import Path
import argparse
import hashlib
import math
import shutil
import struct
import subprocess
import wave
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / 'assets'
parser=argparse.ArgumentParser()
parser.add_argument('--output',type=Path)
options=parser.parse_args()
OUT = options.output.resolve() if options.output else ROOT / 'build' / 'XiaokuiCodexBoot'
ASSETS.mkdir(exist_ok=True)
OUT.mkdir(parents=True,exist_ok=True)
if not (ASSETS / 'logo.png').is_file():
    raise SystemExit('请将自己的透明 PNG Logo 保存为 assets/logo.png。')

def make_sound(name, length, notes, sweep=False):
    rate = 44100
    samples = []
    for i in range(int(rate * length)):
        t = i / rate
        value = 0.0
        for start, duration, freq, volume in notes:
            q = t - start
            if 0 <= q < duration:
                envelope = min(1.0, q / .012) * (1 - q / duration) ** 2
                value += volume * envelope * (math.sin(math.tau * freq * q) + .16 * math.sin(math.tau * freq * 2 * q))
        if sweep:
            envelope = math.sin(math.pi * min(1, t / length)) ** 2
            value += .018 * envelope * math.sin(math.tau * (420 * t + 950 * t * t))
        samples.append(struct.pack('<h', int(max(-.8, min(.8, value)) * 32767)))
    with wave.open(str(ASSETS / (name + '.wav')), 'wb') as audio:
        audio.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        audio.writeframes(b''.join(samples))

make_sound('boot', .8, [(0, .42, 659.25, .085), (.16, .56, 987.77, .065)], True)
make_sound('tick', .14, [(0, .12, 1318.51, .045)])
make_sound('ready', .8, [(0, .6, 659.25, .065), (.12, .58, 830.61, .05), (.25, .5, 987.77, .05)])
make_sound('exit', .65, [(0, .48, 1318.51, .05), (.08, .55, 1975.53, .025)], True)

# 用像素樱花绘制 Windows 方形应用图标。
icon = Image.new('RGBA', (256, 256), (10, 8, 18, 255))
d = ImageDraw.Draw(icon)
d.rounded_rectangle((7, 7, 249, 249), 48, outline=(92, 55, 82), width=3)
petal = Image.new('RGBA', (256, 256))
p = ImageDraw.Draw(petal)
p.rectangle((108, 48, 147, 97), fill='#f49abd')
p.rectangle((98, 58, 157, 87), fill='#f49abd')
for angle in range(0, 360, 72):
    icon.alpha_composite(petal.rotate(angle, resample=Image.Resampling.NEAREST))
d = ImageDraw.Draw(icon)
d.rectangle((117, 117, 138, 138), fill='#fff0f6')
icon.save(ASSETS / 'xiaokui.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])

import os
framework = Path(os.environ.get('WINDIR',r'C:\Windows')) / 'Microsoft.NET/Framework64/v4.0.30319'
compiler = framework / 'csc.exe'
command = [str(compiler), '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
           '/out:' + str(OUT / '小葵OS · Codex.exe'),
           '/win32manifest:' + str(ROOT / 'src' / 'app.manifest'),
           '/win32icon:' + str(ASSETS / 'xiaokui.ico')]
for ref in ['System.dll', 'System.Core.dll', 'System.Xaml.dll']:
    command.append('/reference:' + str(framework / ref))
for ref in ['WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll']:
    command.append('/reference:' + str(framework / 'WPF' / ref))
for name in ['logo.png', 'boot.wav', 'tick.wav', 'ready.wav', 'exit.wav']:
    command.append('/resource:' + str(ASSETS / name) + ',' + name)
command.extend(str(source) for source in sorted((ROOT / 'src').glob('*.cs')))
result = subprocess.run(command, capture_output=True, text=True, errors='replace')
print(result.stdout.strip())
print(result.stderr.strip())
if result.returncode:
    raise SystemExit(result.returncode)
shutil.copy2(OUT / '小葵OS · Codex.exe', OUT / '小葵OS · 动画预览.exe')
(OUT / '使用说明.txt').write_text("""小葵 OS · Codex 启动动画 v1.5.0

解压到固定目录，双击「小葵OS · Codex.exe」；它会同步启动官方 Codex 和动画。
只看动画请双击「小葵OS · 动画预览.exe」。两个 EXE 均内置 Logo 与声音。
可右键启动 EXE 创建自己的桌面快捷方式；官方入口保持原有行为。

需要 Windows 10/11 x64、.NET Framework 4.8，以及官方商店版 Codex。
播放不需要 Python、Node.js、FFmpeg、AI API Key 或管理员权限。

动画为固定 12.5 秒：四句字幕在 0/2.5/5/7.5 秒切换，10.5 秒开始分层退场。
进度在 12.2 秒到 100%，最后 0.3 秒结束；它表示动画交接进度。
窗口未出现、响应较慢或最小化均不延长播放。若 Codex 尚未完成加载，会显示它自己的加载界面。
Windows 定时器有毫秒级调度误差，程序装载时间不计入首次播放后的 12.5 秒。

Esc / Enter / 空格：提前跳过；M 或点击「音效」：切换声音。
当前鼠标所在屏幕居中，播放期间保持置顶并对齐、跟随 Codex 窗口。
后台没有常驻服务，不修改官方安装包、账号或聊天，不记录画面或消息正文。
日志只记录窗口和播放步骤，在程序目录 logs/ 中；分享诊断前自行检查其中的环境信息。

本项目由 AriaEternity 发布，是独立社区项目，与 OpenAI 无隶属关系。
代码和示例素材许可见 LICENSE 与 ASSETS.md，参考来源见 THIRD_PARTY_NOTICES.md。
Logo 由作者使用 ChatGPT 生成，动画为 C#/WPF 实时绘制，电子音由 Python 合成。
鼓励使用自己的 Logo、主题与分镜，详见仓库 docs/CUSTOMIZATION.md 与 docs/CREATIVE-GUIDE.md。
项目：https://github.com/AriaEternity/xiaokui-codex-boot
""",encoding='utf-8')

for file in OUT.glob('*.exe'):
    print(file.name, file.stat().st_size, hashlib.sha256(file.read_bytes()).hexdigest())
print('OUTPUT=' + str(OUT))
