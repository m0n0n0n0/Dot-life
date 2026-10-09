# Dot-life 全场三渲二与描边

更新：2026-10-08。当前 `desk` 的 135 个 Renderer、33 种材质已覆盖；角色、瓶子的已确认参数保留，墨水盒也加入描边。保留原 Gamma、灯光、阴影、手绘贴图和原 Lit 材质，不改场景摆放、碰撞、3C、任务或墨水逻辑。菜单场景没有 3D Renderer，原 UI 不换材质。

## 开关与恢复

退出 Play，等导入与编译完成，**重新打开磁盘上的 desk**，避免旧内存场景保存时覆盖新材质引用。使用 `Tools / Dot Life / Scene Style`，不要用旧 `Toon Trial` 菜单控制全场。

| 菜单 | 效果 |
| --- | --- |
| Apply Toon And Outlines | 全场 Toon + 描边；新放入的原 Lit 物件也在这时转换 |
| Apply Toon Only | 保留 Toon，关闭两层描边及两个 Renderer Feature |
| Restore Original Lit | 恢复全场原 Lit 引用，关闭两个 Feature；不删除 Toon 资产 |
| Enable / Disable Outlines | 不切换表面材质，只切换描边 |

菜单会保存共享材质 / Renderer 配置，场景材质切换后仍需保存 desk。共享 `_Toon` 材质的描边开关会统一设置，手动逐材质关闭后再次 Enable 会重新开启。重复 Apply 不重置已调好的颜色、分段或线宽。

原 FBX 材质映射和 `PF_InkWell` 不改，因此新拖入的模型默认仍是原 Lit；运行时自由墨迹使用独立 Shader，不参与转换。没有用于本场景的材质不批量改造。

## 在哪里调

| 对象 / 配置 | 当前初值与位置 |
| --- | --- |
| 角色、瓶子、墨水盒 | `Art` 对应目录的 `MAT_Dot_Toon` / `MAT_Bottle_Toon` / `MAT_Ink_Toon`；外壳线宽 2.5 |
| 普通塑料、文具 | 对应 `MAT_*_Toon`；外壳线宽 2.0、较弱高光 |
| 金属类、手机 | 对应 `MAT_*_Toon`；外壳线宽 1.6、适量高光 |
| 书本、任务面、纸 | 对应 `MAT_*_Toon`；线宽 1.1、较亮暗部、减弱法线，优先文字可读性 |
| 桌面 / 房间 | 桌面外壳线宽 0.85；房间外壳线宽 0，仅用淡色几何边线，避免单面背景背面遮屏 |
| 原默认白材质 | `Art/Environments/Desk/Materials/MAT_DefaultSurface_Toon`，保留对包内原 Lit 的恢复映射 |
| 全场淡边线 | `Settings/MAT_SceneOutline.mat`，Shader 为 `DotLife/Scene Outline` |

各物件材质的 **Toon Bands** 调明暗分段，**Lighting** 调暗部与高光，**Outline** 调外壳 / 透明轮廓。参数基础见 [TOON_MATERIALS.md](TOON_MATERIALS.md)。不需要重画现有贴图；亮暗分界与高光属于 Shader 效果。

全场淡边线参数：

| 参数 | 默认值 / 作用 |
| --- | --- |
| Line Color | 深灰、Alpha 0.45；Alpha 可降低线条存在感 |
| Width | 1.15，按 1080p 参考像素计算，1440p 等比例放大 |
| Depth Threshold | 0.012；增大可减少深度突变产生的线 |
| Normal Threshold | 0.25；增大可减少几何转折产生的线 |
| Edge Softness | 0.12，边缘过渡宽度 |

先调线色透明度与线宽，再改阈值。单个材质的 Outline Enabled **不能排除全场淡边线**；要隔离比较，关闭 Renderer 的 `DotLife_SceneEdges`。设置 Width 为 0 只隐藏线，不等于关闭 Feature 的渲染开销。

## 原理与边界

采用两层：不透明文具延续反向外壳；`DotLife_SceneEdges` 用深度与**几何法线**补薄纸、开放网格和墙面边线，不检测底图颜色或法线贴图中的笔画。房间单面大网格不使用外壳，避免背面遮屏。透明纸使用 Alpha 内侧轮廓，不写深度、不填镂空，不参与不透明屏幕空间找边。UI / 浮动 E / 墨水条和自由墨迹保持独立绘制。

`URP_DotLife_Renderer` 中两项分别是 `DotLife_ToonOutline`（Render Objects）和 `DotLife_SceneEdges`（内置 Full Screen Pass）。后者在透明物件之前绘制，要求 Depth + Normal，**会触发深度 / 法线预通道并增加开销**；不复制屏幕颜色。URP Asset 的全局 Depth / Opaque Texture 开关仍关闭，但不能据此判断没有深度开销。[URP 14 全屏 Feature](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-features/renderer-feature-full-screen-pass.html)

本轮针对 Windows / D3D11 / 当前 Forward 配置验证，不承诺任意未来平台或渲染器配置直接通用。硬边接缝、薄片背面、透明排序和运动时细线闪烁仍需人工观察；没有同时增加抗锯齿、AO、后处理或再次修改光影。下一步优先试玩校准和测量两层描边各自成本，目标 1080p / 1440p、60 FPS **尚未性能验收**。

## 验证记录

切换、重复应用及 Lit 恢复已通过原生检查；原材质、非渲染组件与原光影保留。98 次原生渲染覆盖全景 / 近景 / 缩放 / 绕转 / 结尾、桌边四方向、任务绘制与完成过渡、动画、透明孔洞、UI 隔离，图像位于 `Logs/SceneToon`。核心 878 项、墨水 2296 项回归通过，Windows 开发构建 0 错误，后台启动 12 秒无新增异常（仅初始化检查）。559 个资产 GUID 无重复。

最新试玩构建：`Logs/SceneToonBuild/DotLife.exe`；修改前的场景 / Renderer / 三份样例材质备份位于 `Logs/SceneToonBaseline`。一次性验证工程已清理，正式项目不留新探测脚本。完整记录见 [ROADMAP.md](ROADMAP.md) 末尾；自动检查不等于人工走完整条关卡或目标机性能达标。
