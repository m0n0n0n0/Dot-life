# Dot Life 资产与场景规范

## 目录分工

| 目录 | 放什么 |
| --- | --- |
| `Assets/_DotLife/Core` | 游戏状态、事件总线 |
| `Assets/_DotLife/Player` | 角色移动、镜头、交互、跌落逻辑与主动画控制器 |
| `Assets/_DotLife/Interactable` | 绘制点、任务绘制、边缘触发和过渡逻辑 |
| `Assets/_DotLife/UI` | 菜单、HUD 脚本及 `Prefabs` |
| `Assets/_DotLife/Audio` | 播放逻辑 |
| `Assets/_DotLife/Scenes` | `MainMenu`、`desk` 场景及烘焙数据 |
| `Assets/Art/Characters/Dot` | 角色模型、动画源文件、材质、贴图 |
| `Assets/Art/Environments/Desk` | 桌面物件的模型、材质、贴图和绘制帧 |
| `Assets/Art/Environments/Room` | 房间背景材质、贴图 |
| `Assets/Art/UI`、`Assets/Art/Fonts` | UI 图片与字体 |
| `Assets/Audio/Music` | 音乐文件 |
| `Assets/Settings`、`Assets/Docs` | 项目配置、协作文档 |

纯视觉资产放 `Art`，带玩法或 UI 职责的预制体放 `_DotLife` 对应模块。不提前创建空目录；以后有待整理的导入资源时，再按需使用 `Art/_Incoming`。

## 命名

| 类型 | 示例 |
| --- | --- |
| 脚本 | `BasicBehaviourScript.cs`，文件名与类名一致 |
| 主动画控制器 | `AC_Dot.controller` |
| UI 预制体 | `PF_UI_TaskStatusItem.prefab` |
| 材质 | `MAT_Diary.mat` |
| 绘制帧 | `T_Diary_Draw_000.png` |
| 过渡帧 | `T_Photo_Transition_001.png` |
| 场景交互点 | `Dot_Diary_01`、`Edge_Diary_01` |
| HUD 元素 | `TaskStatus_Diary`、`TaskDescription`、`InteractionPrompt` |

旧模型文件保留有辨识度的名称和 `_low` 后缀，不批量改 FBX 内部节点、骨骼、动画片段名及贴图通道名。新增材质、预制体和序列帧按上述规则命名；旧贴图来源目录可保留中文含义。音乐文件名目前直接用于歌曲标题显示，不随意改名。

绘制帧按 `Diary`、`YuwenBook`、`Math`、`Photo` 分类，任务内部区分 `Draw` 和 `Transition`。两张纸的替换贴图放 `PaperTransitions`，不因目录排序重新填充 Inspector 数组。

## 场景层级

`desk` 保持四个根节点：

```text
_Systems       GameManager、Main Camera、Lighting、Audio、UI
_Gameplay      Tasks（Diary / YuwenBook / Math / PhotoFrame）、EdgeTriggers
_Environment   Desk、Book、Obstacle、Papers、Background、LevelBounds
Player_Root    原角色整体，保留模型内部层级和脚本挂载位置
```

`MainMenu` 统一归入 `_Systems`，按 `Lighting`、`UI` 分组；菜单管理对象继续留在原 Canvas 内，不改按钮绑定和布局。

- 新建组织节点保持位置、旋转为零，缩放为一；重设父级时保留原世界变换。
- 不直接缩放 `_Systems`、`_Gameplay`、`_Environment` 等组织节点。
- `Book`、`Obstacle` 和模型内部节点保留原有语义；`Erasers` 修正原拼写。
- 背景墙使用 `RoomWall_E`；碰撞边界仍使用 `LevelBounds/Wall_E` 等名称。
- `Player_Root` 暂不拆出 `Visuals`：现有脚本直接获取同根 Animator。
- 不拆解 FBX 实例；不改动画绑定路径、任务消息、任务索引和绘制帧顺序。

## 移动与检查

资产移动优先在 Unity 的 Project 窗口中进行，必须保留对应 `.meta` 和 GUID。移动场景时同步更新 Build Settings，并将 `desk` 烘焙数据目录一起移动；场景名仍为 `MainMenu` 和 `desk`。

整理后检查两个场景能够加载、无新增 Missing Script / Missing 引用，角色和碰撞位置、HUD 布局、任务序列及材质引用未变。目录整理不等同于渲染优化，本轮不调整渲染管线、灯光参数、碰撞体或玩法行为。

本次整理已核对原有 406 个资产文件、两个场景的 229 个物体和 436 个非 Transform 组件；保留资产 GUID、原物体及组件 ID，验证原组件参数、世界位置、UI 布局与引用序列未变，并测试菜单进入 `desk` 的 Play 模式启动与退出。另清除了 5 个旧目录 `.meta` 的合并冲突，保留 Unity 实际使用的 GUID。

## 保留待核实

| 资源 | 后续处理 |
| --- | --- |
| `Player` 内两份 `.cs.bak` | 确认没有恢复用途后再单独删除 |
| `Characters/Dot/Animations` 内测试控制器 | 与主控制器依赖核对后，再决定归档或删除 |
| `Characters/Dot/Models/dot.fbx` | 当前角色使用 `dot - Copy.fbx`，原模型暂不删除 |
| `New Terrain.asset`、`New Terrain 1.asset` | 确认用途和历史需求后再处理 |
| `TutorialInfo`、`Readme.asset` | Unity 模板内容，后续单独清理 |
| URP 配置及 `UniversalRenderPipelineGlobalSettings.asset` | 当前项目使用 Built-in，管线方案确定前保留 |

这些是候选项，不代表可以直接删除。后续画面、渲染、3C、交互和 UI 修改应分批验证，避免与目录迁移混在一起。
