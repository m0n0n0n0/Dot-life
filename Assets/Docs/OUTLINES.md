# Dot-life 描边试作

2026-10-08 已推广到全场，墨水盒也开启描边，新增淡色深度 / 几何法线补边。当前开关和调参以 [SCENE_STYLE.md](SCENE_STYLE.md) 为准；下文是 2026-10-07 小范围外壳试作的历史记录，不再代表当前全场范围。

更新：2026-10-07。S4-A 小范围样例，**角色 + bottle_low 为 Toon 与描边，ink_low 仅 Toon**。原光影、布局、碰撞、动画和玩法保持；没有全场描边或屏幕空间后处理。

## 在哪里调

| 材质 / 资产 | 用途 |
| --- | --- |
| `Assets/Art/Characters/Dot/Materials/MAT_Dot_Toon.mat` | 角色明暗与线条 |
| `Assets/Art/Environments/Desk/Materials/MAT_Bottle_Toon.mat` | 瓶子三个部件的明暗与线条 |
| `Assets/Art/Environments/Desk/Materials/MAT_Ink_Toon.mat` | 墨水盒，默认不描边 |
| `Assets/Settings/URP_DotLife_Renderer.asset` | Renderer Features 中的 `DotLife_ToonOutline`，调用描边通道 |

退出 Play，等待导入完成后重新打开磁盘上的 desk。原 Lit 材质和原 FBX 材质映射没有改；场景仅使用复制材质。不要用 Unity 内存中的旧场景直接覆盖磁盘改动。

在样例材质 Inspector 的 **Outline** 区域调节：

| 参数 | 默认值 / 作用 |
| --- | --- |
| Outline Enabled | 角色 / 瓶子开启；关闭后只保留 Toon |
| Outline Width (1080p Pixels) | 2.5；以 1080p 为基准，1440p 约 3.33 像素，缩放镜头不随距离缩细。是目标扩张宽度，模型朝向 / 接缝 / 遮挡仍会影响可见线宽 |
| Outline Color | 接近黑色、稍带冷色；Alpha 控制透明度 |

建议先保持 2.5；想更明显可试 3～3.5，避免一下加到很粗。瓶子和人物可使用不同线宽。Toon 明暗、高光与法线强度的说明见 [TOON_MATERIALS.md](TOON_MATERIALS.md)，无需重画贴图。

## 对照与关闭

菜单路径：`Tools / Dot Life / Toon Trial`。

| 菜单 / 操作 | 效果 |
| --- | --- |
| Disable Character And Bottle Outline | 关掉两个样例的线条，保留 Toon，墨水盒参数不变 |
| Enable Character And Bottle Outline | 应用三类 Toon 材质，开启角色 / 瓶子描边；已有材质参数不重置，Feature 不重复创建 |
| Restore Lit In Open Desk | 三类样例切回原 Lit；不删除试作资产或修改原材质 |
| Apply Toon To Open Desk | 从原 Lit 切回已有 Toon；不强制开启描边 |
| 关闭 Renderer Feature 的勾选框 | 全局停止这一描边 pass；与材质开关独立，再用 Enable 菜单会重新开启 |

材质 / Renderer 的开关操作会保存资产；场景材质切换支持 Undo，不自动保存场景。仅改材质开关不等于零渲染成本：该 Feature 仍提交包含描边通道的可见 Toon 物件，包括默认关闭的墨水盒；完全比较描边成本应关闭 Feature。

## 原理与边界

**这次确实涉及 Shader。** Toon 的表面通道负责明暗分段；新增 `ToonOutline` 通道将模型顶点沿几何法线的屏幕投影向外扩一点，只绘制背面，形成反向外壳。不是复制场景物体，也不是对整幅画面、手绘线条或 UI 找边。

URP 自带的 Render Objects Feature 只选择 `LightMode = DotLifeOutline`，在天空盒之后、不透明深度已存在时绘制，不覆盖原材质、相机、深度或模板设置。描边自身使用深度测试、小幅深度偏移及关闭深度写入，避免瓶盖共面黑线，正常被前景实体遮挡；不读 Depth / Opaque Texture、不占用 UI 模板位、不改变 ShadowCaster 的原形状。

目前验证的是本项目 Windows / D3D11 / URP 14 Forward。不自动保证换成 Deferred、开启深度预处理、其他平台或全场透明对象后仍等价；那些变化应单独复测。

- 描边依赖几何法线；硬边、断开的顶点、开放 / 很薄的网格、凹面或脚下接触处，可能出现断线或局部内轮廓。原模型法线 / 拓扑问题不在这轮强行修改。
- 它不会自动画出全部材质接缝，透明材质和纸张不直接套用；透明遮挡物也不能按实体深度遮住描边。
- 顶点扩张不改 CPU 模型包围盒；贴屏边 / 远处小物体极端情况下仍需看裁剪表现。
- 当前没有新增抗锯齿。固定姿态与两帧动画对照通过，不等于连续运动完全无锯齿 / 闪烁；在实际相机移动中确认后，再决定抗锯齿方式。
- 会增加几何绘制和像素成本；SRP Batcher 可用不等于没有额外开销。60 FPS 需在 1080p 与 2560×1440 的目标构建中测量。

资料按当前版本看：[URP 14 Pass 标签](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/urp-shaders/urp-shaderlab-pass-tags.html)、[URP 14 Render Objects](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-features/renderer-feature-render-objects.html)。暂不照搬 Unity 6 的渲染扩展接口。

## 验证与下一步

正式场景副本 **54 次原生渲染、核心 878 项、自由盖印 2296 项通过**；Windows 开发构建 0 错误、后台启动 12 秒无新增异常，试玩入口 `Logs/OutlineTrialBuild/DotLife.exe`。详细记录以 [ROADMAP.md](ROADMAP.md) 末行为准。

截图在 `Logs/OutlineTrial`：`ToonOnly` / `Outlined` 是同材质同镜头，只切描边；`BottleLit` 是瓶子的原 Lit。`BottleFull` 展示完整瓶子，其他瓶子近景专看瓶盖、把手与接缝；这些观察视角不代表玩家已走到该处。

本轮一次性视觉验证脚本只放在隔离副本，不进入正式 Assets。截图隐藏 HUD、固定时间 / 动画；UI 排版由原有 Core 回归另行检查。自动检查不替代人工通关、风格确认或性能测量。

先试玩最近 / 最远镜头、右键绕转、行走 / 打点及蘸墨提示，确认人物和瓶子的线宽、暗部色与接缝。满意后保存阶段版本，再决定任务面材质、其他物件分类推广和抗锯齿，不同时重改灯光或机制。
