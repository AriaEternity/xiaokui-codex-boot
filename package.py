# 从通用构建目录生成分发包，不包含个人日志、机器路径、备份或验收原始记录。
from pathlib import Path
import hashlib
import shutil
import tempfile
import zipfile

root = Path(__file__).resolve().parent
version = (root / 'VERSION').read_text(encoding='utf-8').strip()
build = root / 'build/XiaokuiCodexBoot'
dist = root / 'dist'
dist.mkdir(exist_ok=True)
names = ['小葵OS · Codex.exe', '小葵OS · 动画预览.exe', '使用说明.txt']
if not all((build / name).is_file() for name in names):
    raise SystemExit('请先执行 python build.py。')
archive = dist / ('xiaokui-codex-boot-v' + version + '-win-x64.zip')
with tempfile.TemporaryDirectory(prefix='xiaokui-package-') as temp:
    stage = Path(temp)
    for name in names:
        shutil.copy2(build / name, stage / name)
    for name in ['LICENSE', 'ASSETS.md', 'THIRD_PARTY_NOTICES.md']:
        shutil.copy2(root / name, stage / name)
    shutil.copytree(root / 'licenses', stage / 'licenses')
    entries = [p for p in stage.rglob('*') if p.is_file()]
    (stage / 'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(stage).as_posix() + '\n' for p in sorted(entries)), encoding='utf-8')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for p in sorted(stage.rglob('*')):
            if p.is_file():
                z.write(p, 'XiaokuiCodexBoot/' + p.relative_to(stage).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
sha = hashlib.sha256(archive.read_bytes()).hexdigest()
(dist / 'SHA256SUMS.txt').write_text(sha + '  ' + archive.name + '\n', encoding='utf-8')
print(archive)
print('SHA256=' + sha)
