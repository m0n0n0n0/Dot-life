# 角色音效接入

脚步、点墨、蘸墨补满接口已接到 `desk` 的角色，由场景 `_Systems/Audio/PlayerSfxManager` 统一管理。2026-10-22 检查时，你已导入脚步包并配置了行走 / 点墨素材与音量，本轮保留；新加的蘸墨素材列表为空，补入素材才发声。原 BGM 与渲染、任务规则不变。

## 配置入口

在 Hierarchy 中选中 `_Systems/Audio/PlayerSfxManager`，Inspector 会直接展开 Sound Settings，把导入的 AudioClip 拖入对应数组即可。一个音效也能用，多个会随机变化并尽量避免连续相同；空数组或空元素会跳过，不报错。

Motor / Ink 已指向现有角色，不需要手动修改。Settings 仍引用 `Assets/_DotLife/Audio/SFX_Player.asset`，在物体 Inspector 中修改声音参数会保存到这份配置资产，不是重复的一套参数；Play 中修改这些资产参数也会保留。运行中锁定角色 / 配置引用，但素材、音量和步距仍可调。

| 参数 | 用途 |
| --- | --- |
| Footstep Clips / Volume / Pitch | 脚步素材、音量、随机音高范围；音高填 `(1, 1)` 可关闭变化 |
| Step Distance | 每走多少世界距离响一次，默认 1；调小会更密集 |
| Minimum Step Interval | 两次脚步的最短间隔，默认 0.12 秒，避免短时间重复 |
| Minimum Move Speed | 忽略轻微移动，默认 0.1；撞墙不按输入持续播放 |
| Footstep Mode | 默认 Distance，无需修改动画；Animation Events 仅在接好动画事件后使用 |
| Stamp Clips / Volume / Pitch | 成功点墨时的素材、音量、音高范围 |
| Ink Refill / Refill Clips / Volume / Pitch | 蘸墨成功补满时的素材、独立音量与随机音高；不需要另加动画事件 |
| Output | 可选的 AudioMixerGroup，未来可以接 SFX 总音量；现在留空即可 |

新素材按需放入 `Assets/Audio/SFX`，已导入的脚步包不移动、不删减，不用改 BGM 文件。先用短的单次脚步、墨水接触或蘸墨声，不要用循环行走片段。每类动作只有一个播放声道，快速重复会替换同类旧声音；脚步、点墨、蘸墨各有独立声道，不会互相覆盖。

## 触发规则

- 脚步读取实际水平移动、落地及角色控制状态；停下、交互、跳下、结束、暂停或失焦时不继续发出脚步，传送不会补播一串声音。
- 点墨只接 `PlayerInk.InkChanged` 的实际扣墨通知。日记自动打点、手动任务和自由盖印共用这条路径；手动动作仍在现有接触时刻提交。取消、错序、空墨、盖印失败、蘸墨均不会播放点墨声，最后一笔即使结束游戏也会播放。
- 点墨没有新增动画事件，不要再给动画添加点墨音效事件，否则可能重复。
- 蘸墨接 `PlayerInk.Refilled`，现有动作在接触时刻成功补满才通知；按 E 请求、取消、失去条件、失败不播放。合法的满墨再蘸取也会通知，不依赖“墨量必须增加”；点墨扣墨不会误播蘸墨声。不添加蘸墨动画事件，避免重复。
- 音效默认是 2D，不随相机缩放改变音量；三个 AudioSource 只在运行时生成于 `PlayerSfxManager/_PlayerSfx` 子节点，没有新增 AudioListener。脚步读取所引用角色的位置，不读取 Audio 管理物体的位置。

需要精确脚掌落地同步时，把 Footstep Mode 改为 Animation Events，在行走动画的左右脚接地帧各添加一次 `OnFootstep`（无参数）事件。同 Animator 的 `PlayerSfxEvents` 已接好，负责转发给 Audio 下的管理物体。该模式不再按距离发声；没配事件就不会有脚步声。可先用距离模式调声音，之后再切换。

## 检查与边界

等待编译完成后试玩：移动 / 撞墙 / 跳下、成功与失败盖印、蘸墨接触 / 取消 / 满墨再蘸取、日记最后一点、最后任务、场景重进都要核对。本轮不更换已配置素材；新增蘸墨声尚未指定，实际听感、素材响度与步态同步仍需人工确认。

初版（角色挂载）的历史验收：2026-10-09 独立副本原生接线与原组件不变检查 657 项、音效专项 55 项、核心 878 项、自由盖印 2296 项通过；Windows 64 位 Mono Development 构建成功、0 错误。专项使用运行时生成的静音 AudioClip，检查播放请求、音量 / 音高、空素材、真实位移 / 撞墙、跳下 / 传送、暂停 / 失焦、成功 / 失败 / 最后一笔及订阅清理，不代表实际听感已确认。日志为 `Logs/PlayerSfxSetupVerified.log`、`PlayerSfxRegression.log`、`PlayerSfxCore.log`、`PlayerSfxStamp.log`、`PlayerSfxBuild.log`；当时构建为 `Logs/PlayerSfxBuild/DotLife.exe`，不含后续 Audio 管理物体。一次性探测仅保留于 Logs，不加入正式 Assets。

Audio 管理物体版验收（2026-10-09）：原生迁移 / 配置保留 / Inspector 类型与序列化编辑 Undo 检查 664 项、音效专项 59 项、核心 878 项、自由盖印 2296 项通过；Windows 开发构建 0 错误。确认管理物体不移动时仍读取角色位移、运行声道在 Audio 下、动画事件经角色转发、禁用管理物体不阻断墨水提交、删除后无重复订阅。场景只新增管理物体并替换原角色音效组件为转发组件，原配置资产、BGM、布局、碰撞与渲染保持；566 个资产 GUID 无重复。最新构建 `Logs/PlayerSfxManagerBuild/DotLife.exe`，日志 `Logs/PlayerSfxManager*.log`；专项仍使用静音样本，真实听感未验收。

2026-10-22 新增蘸墨配置与独立声道，保留原脚步 / 点墨素材和参数，两个场景不改。按用户要求已停止额外的成套运行检查与构建，不把中断构建标为完成；本轮没有新的完整构建，历史 `PlayerSfxManagerBuild` 不包含蘸墨音效。后续小改动默认只作必要静态核对，确需运行回归或构建时先确认。蘸墨素材留空，实际听感和试玩由后续配置确认。

可停止 Play 后使用 `Tools / Dot Life / Connect Player SFX` 为 `desk` 重新接线；会迁移旧的角色音效组件并保留配置，重复执行不新增第二个管理物体或转发组件，也不覆盖已经指定的自定义配置。本轮只是给既有管理物体增加蘸墨配置，不需要重新接线或保存场景。落地、任务完成音效与音量设置 UI 不在这轮范围内。
