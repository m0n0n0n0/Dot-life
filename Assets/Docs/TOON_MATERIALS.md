# Dot-life 三渲二材质试作

2026-10-08 已推广到全场。当前使用、全场开关和恢复方式以 [SCENE_STYLE.md](SCENE_STYLE.md) 为准；下文保留 2026-10-07 小范围试作的参数说明与历史验证范围。

更新：2026-10-07。当前是 S3-A 的小范围样例，不是最终美术定稿；本轮加入 bottle_low，并在角色 / 瓶子上试作描边，见 [OUTLINES.md](OUTLINES.md)。光影仍为 `f6d8271` 的 Gamma、原灯光 / 环境光 / 阴影，URP 14 保留。

## 改了什么

| 内容 | 当前范围 |
| --- | --- |
| Shader | `Assets/_DotLife/Rendering/ToonLit.shader`，Inspector 名称 `DotLife/Toon Lit` |
| 角色样例 | `Assets/Art/Characters/Dot/Materials/MAT_Dot_Toon.mat` |
| 墨水盒样例 | `Assets/Art/Environments/Desk/Materials/MAT_Ink_Toon.mat`，覆盖 ink_low 的瓶身与瓶盖 |
| 瓶子样例 | `Assets/Art/Environments/Desk/Materials/MAT_Bottle_Toon.mat`，覆盖 bottle_low 的三个部件；原 MAT_Bottle 与贴图保留 |
| 保留 | 原手绘底图、法线图、原 Lit 材质、模型 / FBX 材质映射和墨水盒预制体；场景位置、碰撞、动画、任务与墨水逻辑不变 |
| 暂不做 | 任务纸张 / 全场材质替换、透明物件、AO、后处理、再次调整灯光；墨水盒暂不描边 |

Shader 是物体表面的绘制规则，Material 是它的参数和贴图。这里将“法线与主光方向得到的连续明暗”分成两段 / 三段，再乘原底图；仍接收主光阴影、环境光和现有辅光，并保留较小的风格化高光。**不需要重新画整套贴图，也不是重写渲染管线。** 原主光偏暖，因此样例亮部仍会受原灯光颜色影响。

## 打开与对照

1. 退出 Play，等待资源导入及编译，再重新打开磁盘上的 `desk.unity`。若 Unity 仍显示旧内存内容，不要直接保存覆盖本轮改动。
2. 从 MainMenu 进入试玩；最近 / 最远镜头和右键绕转观察角色、墨水盒与瓶子。
3. 在编辑模式的 desk 使用 `Tools / Dot Life / Toon Trial / Restore Lit In Open Desk` 切回原材质；使用 `Apply Toon To Open Desk` 再切回样例。
4. Apply / Restore 只切换角色 / InkWell / bottle_low 的已知材质槽，支持 Undo，不改原材质或其他槽，也不自动保存场景。Enable / Disable Outline 会保存样例材质及所需渲染器资产，不自动保存场景。

直接修改三个新材质即可；再次 Apply 不会重置你的样例参数。新拖入的 `PF_InkWell` 仍用原 Lit，在 desk 中使用 Apply 才会切换。当前样例为共享资产，调整时所有使用同一新材质的样例一起变化；瓶子的三个部件共用 MAT_Bottle_Toon。

## 建议先调的参数

| Inspector 参数 | 作用 / 建议 |
| --- | --- |
| Three Bands | 开启三段，关闭两段；先比较哪种更符合手绘风格 |
| Shade Threshold / Light Threshold | 暗部 / 亮部边界，默认 0.3 / 0.7；一次调一项，例如 0.05，亮部阈值应高于暗部 |
| Band Softness | 分界柔和度，默认 0.035；越小越硬，保留少量柔和度减少锯齿感 |
| Shade Tint / Midtone Tint / Lit Tint | 对底图明暗区域乘色；不是直接把底图替换成纯色 |
| Normal Strength | 法线细节，角色 0.25、墨水盒 / 瓶子 0.15；过高会让分段碎裂，零值保留模型几何法线 |
| Highlight Strength / Threshold | 高光强度 / 面积；角色 0.04 / 0.96，墨水盒 0.08 / 0.94，瓶子 0.06 / 0.95；阈值越高面积越小 |
| Main Light / Ambient / Additional Light Strength | 只调该材质对各光源的响应，不改变场景灯；默认 1.2 / 0.55 / 0.45。暗部过黑优先微调 Shade Tint 或 Ambient Strength |

先调整两段 / 三段、分界和暗部色，别一次同时调整全部参数。原手绘细节要可读，不必把所有物件变成同一种质感。角色顶部接缝及原模型细节仍需人工观察，不能用描边掩盖模型问题。

## 兼容范围与限制

- 第一版面向当前 Windows PC / URP Forward 的不透明物件；有投影、深度和深度法线通道。Toon 明暗本身不需 Renderer Feature；本轮描边由独立 Render Objects Feature 调用，仍不开全局 Depth / Opaque Texture。
- 保留 `_BaseMap` / `[MainTexture]`，现有 `material.mainTexture` 更新方式可以继续使用。自由墨迹仍使用独立 `InkStamp.shader`，不强行混入 Toon。
- 不实现完整 URP/Lit 的金属度、粗糙度、反射探针、视差或 Lightmap 烘焙。旧材质中这些数据仍保留，但本样例不读取；这正是减少写实高光的试作，不可直接推广到玻璃 / 金属 / 透明纸面。
- 任务面仍用原 Lit。主贴图接口测试通过不等于任务面 Toon 成品；S3-B 再专门验证绘制、过渡和材质分类。S4-A 仅角色 / 瓶子描边，不把手绘贴图内容当作边缘检测对象。

## 验证与下一步

上一轮截图在 `Logs/ToonTrial/Lit` 与 `Toon`，分别为原 Lit 和新 Toon，同灯光 / 同镜头 / 同姿态。`SpawnFar`、`SpawnNear`、`Orbit90`、`Ending`、`InkWellNear` 覆盖 1080p 与 2560×1440；额外检查角色投影、盖印姿态、两帧实际行走和两段切换，共 26 次原生渲染通过。`InkWellNear` 是专用材质观察站位，不代表人工走到该位置。

正式场景副本的核心回归 878 项、自由盖印回归 2296 项通过；原材质、玩法代码、场景布局与光影恢复点保持一致。日志为 `Logs/ToonTrialPrepare.log`、`ToonTrialVisual.log`、`ToonTrialCore.log`、`ToonTrialStamp.log`。核心日志中的四个 `DotLife expected callback failure` 是故意注入的异常恢复用例，不是新出现的游戏异常。

实际验证结果以 [ROADMAP.md](ROADMAP.md) 的最新记录为准。截图隐藏 HUD、固定动画，不代替人工试玩，也不用于宣称 60 FPS；当前未测目标机性能。一次性验证代码仅在隔离副本中运行，不放进正式 Assets；正式工程继续保留 Core / Stamp 回归与应用 / 回退工具。

本轮瓶子 / 描边的正式场景副本 54 次原生渲染、核心 878 项、自由盖印 2296 项通过；当前 Windows 开发构建为 `Logs/OutlineTrialBuild/DotLife.exe`，构建 0 错误、后台启动 12 秒无新增异常。日志和验证边界见 ROADMAP 最新记录与 OUTLINES。

上一轮 `Logs/ToonTrialBuild/DotLife.exe` 不包含瓶子 / 描边；`LightingL1Build` 是已撤回灯光的旧构建。后台启动只验证初始化，不是构建内人工通关，最新构建仍需人工试玩。

下一步先让你和美术确认三类样例的分段、暗部色和线宽，再决定 S3-B 任务面试作与是否推广。不要同时改全场材质与灯光，否则难以判断效果来源。
