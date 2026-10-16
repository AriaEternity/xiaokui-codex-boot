# 参考来源、致谢与第三方声明

感谢下列项目公开的设计与工程实践。本项目通过自己的静态 Logo、C#/WPF 绘制代码和合成提示音实现动画；没有打包下列项目的角色图、视频、音频或完整源码文件。

| 来源 | 具体参考 |
|---|---|
| [6shenhonghong9/dsh-startup-screen](https://github.com/6shenhonghong9/dsh-startup-screen) | 最初的启动画面氛围、分阶段叙事和交互方式 |
| [yushua0808-cpu/dragon-codex-boot](https://github.com/yushua0808-cpu/dragon-codex-boot) | 独立启动入口、并行激活、覆盖窗口置顶和真实客户端交接思路 |
| [mytjxs/codex-startup-animation · PR #1](https://github.com/mytjxs/codex-startup-animation/pull/1) | 播放期间窗口覆盖、异步探测与可靠性设计讨论；历史阅读快照 head 8af266dd03143222c96f4e5d84c9f06e3011efdd |

以上为参考与致谢，不表示这些项目作者背书本项目。PR 仅作为设计交流来源，不将该 PR 的无明确许可代码或素材作为本项目内容再分发。Windows API 声明对应操作系统公开接口。

为保留参考来源声明，随附两份已核对的 MIT 文本：

- [Dragon Codex Boot MIT](licenses/dragon-codex-boot-MIT.txt)：Copyright (c) 2026 Dragon Codex Boot contributors。
- [dsh-startup-screen MIT](licenses/dsh-startup-screen-MIT.txt)：Copyright (c) 2026 沈宏宏。

以后如引入这些或其他项目的具体代码、素材或依赖，贡献者应先核对对应版本的许可，并保留要求的版权、许可和修改说明。仅添加仓库链接不能代替许可证义务。

## 构建及系统组件

- Pillow 12.2.0：MIT-CMU，仅构建素材使用，不随成品 EXE 捆绑。项目：https://github.com/python-pillow/Pillow ，许可证：https://github.com/python-pillow/Pillow/blob/main/LICENSE 。
- Windows、.NET Framework、WPF 与系统字体由用户设备提供，不包含在仓库或下载包中。
- Codex 与 OpenAI 名称仅用于说明兼容目标。项目为独立社区作品，与 OpenAI 无隶属关系。
