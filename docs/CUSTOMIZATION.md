# 制作自己的 Logo 与动画

## 先换图片

将自己的透明 PNG 保存为 `assets/logo.png`，运行 `python build.py`，再双击 `build/XiaokuiCodexBoot/小葵OS · 动画预览.exe`。

建议 Logo 轮廓完整、四周不过度留白，不带烘焙背景；横向画布中的当前布局适合宽幅 Logo。更换长宽比或改为圆形徽章后，需要检查它与字幕、进度条的距离，必要时调整布局。

图片、音效在构建时嵌入 EXE。构建后只修改 PNG 不会改变已经生成的 EXE，需要重新构建。

## 再改风格

| 想改的内容 | 代码位置 |
|---|---|
| 粉色、亮色、文字和底色 | src/XiaokuiBoot.cs 的 SakuraScene 颜色字段与 OnRender 中绘制用色 |
| 四句中文、英文、副标题 | SakuraScene.OnRender 的 zh、en、note 数组 |
| Logo 尺寸、中心、扫描线和圆弧 | SakuraScene.OnRender 的 logoWidth、lx/ly、扫描与 Arc 绘制段 |
| 像素汇聚的颗粒和运动 | SakuraScene 构造函数的采样 step、Tile 偏移，以及 OnRender 的汇聚缓动 |
| 背景、网格、粒子 | SakuraScene.OnRender 的对应绘制段 |
| 字幕切换点 | StageAt、CaptionAlphaAt |
| 各层收尾顺序 | DecorationAlphaAt、LogoAlphaAt、BackgroundAlphaAt、ProgressAlphaAt |
| 开场、阶段和结束提示音 | build.py 的 make_sound 调用；BootWindow.Update 中的音效触发节点 |
| 窗口初始比例与居中大小 | Native.Centered；逻辑画布为 1600×1000，布局需要一起校对 |

## 修改总时长时要一起改什么

本版基准为 Duration=12.5、FadeStart=10.5、ProgressComplete=12.2。修改时需要同时检查：

1. StageAt 与 CaptionAlphaAt 的字幕节点。
2. 五个图层透明度函数中的开始时间和持续时间。
3. BootWindow.Update 的提示音时刻，确保 WAV 能在退出前播放完。
4. Program.Main 的 `--render` 采样时间和相关测试期望。

不要只改 Duration；否则字幕、进度条、声音或收尾可能先结束。StageAt 当前对应四段字幕，也不是外部配置读取器。

## 改完怎样检查

先预览，再使用专用测试窗口检查移动、缩放、最大化、还原、最小化和延迟出现。真实 Codex 的冷启动、已有窗口和多个显示缩放要分别观察。

`python tests/run-tests.py` 会显示测试窗口，不发送聊天、不调用模型、不关闭真实 Codex。测试报告保存在被 Git 忽略的 qa/。改变时长后，应同时更新 HandoffTests.cs 的对应时间合同。

现有版本只提供 Windows Codex 独立启动入口。修改本项目不能替换 Windows 登录前的系统引导动画。
