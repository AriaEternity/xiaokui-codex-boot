# 运行隔离的原生窗口验收；不会登录、发送聊天或关闭真实客户端。
from pathlib import Path
import json
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
subprocess.run([sys.executable, str(root / 'tests/build-tests.py')], check=True)
for name, delay in [('existing', 0), ('delayed', 5.5), ('late', 11.8), ('absent', 20)]:
    report = root / 'qa' / (name + '.json')
    subprocess.run([str(root / 'qa/Xiaokui.Handoff.Tests.exe'), str(delay), str(report)], check=True, timeout=25)
    result = json.loads(report.read_text(encoding='utf-8-sig'))
    if not result['passed']:
        raise SystemExit('验收失败：' + name)
    print(name + '：通过，播放 ' + str(round(result['animationSeconds'], 3)) + ' 秒')
