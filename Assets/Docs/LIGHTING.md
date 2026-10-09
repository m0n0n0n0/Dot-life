# Dot-life 基础光影与调参

**历史记录，当前已撤回。** 2026-10-07 用户要求恢复原光影，已逐项恢复 `f6d8271` 的 desk 光照、URP 阴影、Gamma 色彩空间及 Graphics 光强设置，不回退 URP。下文 Linear 方案、测试与构建只对应过去的 L1 / L2 试验，不是当前项目配置。当前三渲二工作见 [TOON_MATERIALS.md](TOON_MATERIALS.md)。

当前值：Gamma；Skybox 环境光；KeyLight / FillLight 强度均为 0.5、原色及原旋转；Sun Source 空引用；阴影 2048、距离 50、1 级联、Bias 1 / 1、关闭软阴影。旧烘焙 / 反射资源未动。

更新：2026-10-07。第一批只建立照明底座，不实现 Toon、描边、AO 或新后处理。当前保留 Unity 2022.3.62f3c1、URP 14.0.12 和原有 URP/Lit 材质。

## 已撤回方案

采用 **Linear + 偏暖主光 + 弱冷辅光 + 渐变环境光 + 两级联软阴影**。目标是减少整体泛白，让纸面文字、人物体积和物件投影更清晰；不是最终美术定稿，也不是把所有暗部变黑。

| 调整入口 | 本轮参数 | 作用 |
| --- | --- | --- |
| Project Settings / Player / Other Settings / Color Space | Linear，原为 Gamma | 改变光照和颜色的计算方式，不是一个调色滤镜；切换后需重新看菜单、贴图和透明材质 |
| desk / Lighting / KeyLight | Directional、Realtime；Intensity 1.1；RGB 1 / 0.96 / 0.88；旋转 50 / -30 / 0 | 桌面主光；决定主要明暗和投影方向 |
| desk / Lighting / FillLight | Directional、Realtime；Intensity 0.16；RGB 0.78 / 0.84 / 0.95；无阴影；旋转保留 | 轻微提亮背光面，不产生第二套投影 |
| Window / Rendering / Lighting / Environment | Sun Source = KeyLight；Environment Lighting Source = Gradient | 主光显式指定，环境光不再由默认天空盒决定 |
| Gradient 环境光 | Sky：0.50 / 0.54 / 0.60；Equator：0.38 / 0.40 / 0.44；Ground：0.22 / 0.21 / 0.20 | 给不同朝向的暗部保留基础颜色与层次 |
| KeyLight / Shadows | Soft；Strength 0.85；Bias 使用管线设置 | 保留可读投影，同时减轻过硬的黑边 |
| Assets/Settings/URP_DotLife.asset / Shadows | 距离 35；2 Cascades；Depth Bias 0.3；Normal Bias 0.4；Soft Shadows 开启，Medium | 调整阴影覆盖、精度和边缘；主光阴影分辨率保留 2048，近级联分割保留 0.25 |

颜色记录使用 0～1 的 RGB 值。Directional Light 的**旋转**决定入射方向，挪动它的位置不会把光“移动到窗边”。这里的“窗光”是光线方向的美术设计，并未新增窗口模型。

直接光可以理解为“主灯照亮哪个面”；环境光负责“没被主灯直接照到的面还能看见多少”。渐变环境光不是完整的反弹光模拟；当前尚未新增烘焙 GI 或 Light Probe 布局。

Linear 会影响整个项目，MainMenu 也需要看一遍。不要发现变暗就同时加大灯光、环境光、曝光和材质颜色，这样无法判断问题来源。[Unity 色彩空间说明](https://docs.unity3d.com/2022.3/Documentation/Manual/LinearRendering-LinearOrGammaWorkflow.html)

## 怎样微调

每次只动一项，使用同一站位、相机距离和任务阶段比较。

| 看到的问题 | 先调什么 | 不要先做什么 |
| --- | --- | --- |
| 纸面太亮、文字发淡 | KeyLight Intensity 小幅降低，例如每次 0.05～0.1 | 不先调暗整张纸的贴图 |
| 人物背光面或墨水盒太暗 | 先微调环境光 Equator；需要更明确的侧向补光再调 FillLight | 不先加多个带阴影的灯 |
| 暗部偏灰、画面缺少体积 | 适量减弱环境光或 FillLight，保留主辅光差异 | 不用大幅增加主光来补对比 |
| 投影方向不好看 | 调 KeyLight 的 Y 旋转，X 决定光线高低 | 不移动灯的位置；不挪任务物体配合影子 |
| 表面出现阴影条纹 / 自阴影脏点 | 小幅增加 Depth / Normal Bias，再查模型法线 | 不把偏移一次拉到很大 |
| 阴影像离开物体、纸边漏光 | 小幅减小 Bias，并在最近/最远镜头检查 | 不因此修改角色碰撞体或任务点 |

Bias 是阴影比较时的偏移：过小可能出现自阴影条纹，过大可能出现悬空感或漏光。当前 KeyLight 使用管线 Bias，调光源中未启用的局部 Bias 不会改变实际效果。软阴影、级联和更大阴影贴图都有成本，并非越高越好。[URP 14 设置说明](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/universalrp-asset.html)

## 本轮保留的内容

- desk 除环境光、两盏灯及主光旋转外，其余场景对象保持原样；MainMenu 场景文件未改。
- 不修改角色显示高度、物理胶囊、相机、交互、任务、墨水逻辑、材质或贴图导入设置。
- 不开启 Depth/Opaque Texture，不加 Renderer Feature；HDR、Render Scale、抗锯齿和质量档位保留。
- `Scenes/desk/LightingData.asset`、`ReflectionProbe-0.exr` 和原引用保留，未删除、重烘焙或自动生成新探针。

修改前原生检查实际加载的 Lightmap 和 Light Probe 数量均为 0，运行时主光已经是 KeyLight。因此不能把旧烘焙数据或“选错主灯”认定为这次画面问题的根因。显式 Sun Source 是为了让后续加灯时更可控。旧反射资源并不代表已完成 Linear 下的重新校准；这属于后续材质/反射阶段。

## 验证与试玩

本机对照保存在 `Logs/LightingL1`，不随 Git 提交：

| 目录 / 日志 | 内容 |
| --- | --- |
| Gamma/Original | 修改前 Gamma 和原灯光，固定姿态截图 |
| Linear/Original | 只切 Linear，保留原灯光，帮助区分色彩空间与照明的影响 |
| Linear/WindowLight、Gamma/WindowLight | 校准时的候选截图；2026-10-07 精简测试时清理，正式前后对照保留 |
| Linear/Applied | 正式保存参数的原生截图：SpawnFar、SpawnNear、Orbit90、Ending、InkWellNear；各有 1920×1080 和 2560×1440 |
| LightingL1Applied.log | 14 次原生渲染通过；两分辨率 HUD、实际人物投影像素差异、主光与保存参数核对；无错误着色器 |
| LightingL1Core.log / LightingL1Stamp.log / LightingL1Build.log | 本轮核心、自由盖印和 Windows 构建验证；最终结果在 ROADMAP 记录 |

本轮核心 878 项、自由盖印 2296 项通过；Windows 64 位 Mono Development 构建成功、0 个错误，后台启动 12 秒无新增异常。试玩构建位于 `Logs/LightingL1Build/DotLife.exe`；启动检查只验证初始化，不操作菜单或冒充人工通关。

上述截图由本轮独立副本的一次性探测生成；2026-10-07 按用户要求删除了该探测脚本，保留日志、正式前后截图与最新构建。截图暂停动画、隐藏部分 UI 或单独捕获 HUD，**不等于完整游戏运行与人工通关**。绘制与读回截图的耗时也不是游戏 FPS。后续继续保留核心和自由盖印回归，不保留锁定某一批灯光数值的测试脚本。

试玩时：

1. 退出 Play，等待资源导入和编译；若 desk 原本已打开，请重新打开磁盘上保存的场景，避免把旧内存状态保存回去。
2. 从 MainMenu 进入；比较人物、纸面、桌面、墨水盒和相框，不只看出生点。
3. 滚轮拉到最近/最远，右键绕转，观察阴影是否闪烁、脱离物体或漏光。
4. 打完日记、跳下、蘸墨、自由盖印并完成四项任务；确认墨点、任务贴图、浮动 E 和 UI 都正常可读。
5. 分别查看 1080p 与 2560×1440。60 FPS 是目标，仍需在实际目标设备的构建中测 CPU/GPU 帧时间；本轮未宣称已达到。

## 接下来

先确认这一版的明暗与投影方向。之后复核颜色图/数据图的 sRGB、粗糙度/光滑度、高光和旧反射，再试角色与一个物件的 Toon；描边最后小范围加入。当前盒体高光条纹、角色顶部细缝在原方案中也能看到，先留到材质/模型阶段调查，不用灯光或黑描边掩盖。

恢复点为 `f6d8271`（“视觉改造前存档”）。恢复本轮照明时要一起恢复 desk 光照、URP 阴影配置、Player 色彩空间与 Graphics 线性光强设置，不应只把一个灯调回旧值；恢复前仍需保护之后新增的手动修改。
