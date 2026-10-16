from pathlib import Path
import subprocess
import os

root=Path(__file__).resolve().parents[1]
framework=Path(os.environ.get('WINDIR',r'C:\Windows'))/'Microsoft.NET/Framework64/v4.0.30319'
out=root/'qa'/'Xiaokui.Handoff.Tests.exe'
out.parent.mkdir(parents=True,exist_ok=True)
args=[str(framework/'csc.exe'),'/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',
      '/main:XiaokuiOS.HandoffTests','/out:'+str(out),'/win32manifest:'+str(root/'src'/'app.manifest')]
for ref in ['System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll']:
    args.append('/reference:'+str(framework/ref))
for ref in ['WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll']:
    args.append('/reference:'+str(framework/'WPF'/ref))
for name in ['logo.png','boot.wav','tick.wav','ready.wav','exit.wav']:
    args.append('/resource:'+str(root/'assets'/name)+','+name)
args.extend(str(p) for p in sorted((root/'src').glob('*.cs')))
args.append(str(root/'tests'/'HandoffTests.cs'))
result=subprocess.run(args,capture_output=True,text=True,errors='replace')
print(result.stdout);print(result.stderr)
raise SystemExit(result.returncode)
