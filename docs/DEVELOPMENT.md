# 开发说明（模板）

这份文档写给想改代码的人。只想换素材的话看 [ASSETS.md](ASSETS.md)；只想起步看 [README](../README.md)。

## 代码结构

| 文件 | 内容 |
| --- | --- |
| `src/Core.cs` | 入口、单实例、全局异常；`Native`（Win32 / 分层窗口 / 热键 / 空闲检测）；`Ui`（颜色、字体、圆角、气泡路径、星星）；`Loc`（中英文 UI 文案）；`Phrases`（按状态分类的台词）；`UmiFrames`（动作帧加载）；`Config`；`Autostart`；`MusicWatcher`；`Sound` |
| `src/Pet.cs` | 挂件主窗口 `PetForm`：状态机、动画、气泡、拖拽、命中测试、右键菜单、托盘、热键 |
| `src/Panels.cs` | `PanelForm`（控制面板）、`CardForm`（扩列卡面板）、`CardViewer`（放大窗） |

## 运行流程

```
Program.Main
  └─ new PetForm(...)
       ├─ 加载配置 / 动作帧 / 图片
       ├─ Load: 分层窗口样式、托盘、热键、音乐线程、问候
       └─ Timer(40ms) ──> Render()
                            ├─ 每秒一次 Tick()      // 空闲、提醒、音乐
                            ├─ Schedule() / DemoStep() // 选下一个状态
                            ├─ FrameFor(t)          // 选这一帧
                            ├─ GDI+ 画到 _canvas
                            └─ Push() ── UpdateLayeredWindow
```

窗口是 **WS_EX_LAYERED + 逐像素 alpha**：每帧把 32bpp 的 `_canvas` 推给 `UpdateLayeredWindow`。
`WM_NCHITTEST` 会采样 `_canvas` 的 alpha，透明处返回 `HTTRANSPARENT`，所以透明区域不会挡住桌面。

## 状态机

状态是 `Mood` 枚举：

```csharp
enum Mood { Idle, Thinking, Working, Watching, Searching,
            Waiting, Sleeping, Happy, Error, Listening, Demo }
```

`PetForm` 里相关字段：

| 字段 | 作用 |
| --- | --- |
| `_mood` / `_moodSince` | 当前状态和进入时间（帧序列按 `t - _moodSince` 走） |
| `_moodUntil` | 自动回 Idle 的时间；`double.MaxValue` 表示不自动回 |
| `_nextActivity` | 下一次随机找事做的时间 |
| `_demo` / `_demoIndex` / `_demoNext` | 演示模式 |

关键方法：

- **`SetMood(mood, seconds, say, category)`**：切状态、重置计时、可选冒一句台词。
- **`Schedule(t)`**：非 Idle 且超时 → 回 Idle；Idle 且到点 → 按权重随机挑
  思考 30% / 工作 20% / 盯着 20% / 找资料 15% / 听你说 15%，持续 4–9 秒。
- **`Tick(t)`**：每秒一次。
  - `Native.IdleSeconds` < 3 视为活跃；60 秒没输入视为离开；回来时从打盹/等待里唤醒。
  - 活跃累计 45 分钟 → 起身提醒；90 分钟 → 喝水提醒；23:00–5:00 每小时催睡。
  - 音乐在放 → 设置 `_musicSway`，换歌时有机会冒「♪ 歌名」。
  - 75 秒没输入 → `Waiting`；180 秒 → `Sleeping`。
- **`FrameFor(t)`**：根据 `_mood` 和 `t - _moodSince` 选帧（见下）。
- **`RunDemo()` / `DemoStep(t)`**：每 5.2 秒轮换一个状态，走完一圈自动结束。

## 动作帧

主角色槽位 `assets/umi/`（目录名沿用代码）里是 240×240 的透明 PNG，命名和状态对应：

| 状态 | 帧 |
| --- | --- |
| 发呆 | `idle-open` / `idle-blink` / `idle-wink` |
| 思考 | `think` / `think-blink` |
| 工作 | `work` / `work-blink` |
| 盯着 | `watch` / `watch-left` / `watch-right` / `watch-left-far` / `watch-right-far` / `watch-blink` |
| 找资料 | `search` + `search-video-000…`（每 70ms 一帧循环） |
| 等你 | `wait` / `wait-blink` |
| 打盹 | `rest` |
| 开心 | `happy` / `happy-blink` |
| 出错 | `error` |
| 听你说 | `listen` / `listen-blink` |
| 说话 | `talk-closed` / `talk-small` / `talk-medium` / `talk-wide` / `talk-blink` |

`FrameFor` 的逻辑：

1. 气泡刚出现 1.5 秒内，如果当前是 Idle / Listening / Waiting / Happy，用四种嘴型循环（说话）。
2. 否则按 `_mood` 走各自的帧序列；Idle 每 13 秒一个循环，中间两次眨眼 + 一次 wink。
3. 找不到帧时退回 `idle-open`，再退回备用皮肤的 `pet.png`。

**动作帧没有背景，角色脚底大约在 y=225，所有帧保持同一构图**——换角色帧时尽量沿用这个基线，否则挂件会上下跳。

## 加一个新状态

以「伸懒腰 Stretching」为例：

1. `Core.cs` 的 `Mood` 里加 `Stretching`。
2. 把帧放进主角色槽位 `assets/umi/`（如 `stretch.png` / `stretch-blink.png`）。
3. `Pet.cs` 的 `FrameFor` 里加 case：
   ```csharp
   case Mood.Stretching:
       name = (mt % 2.5) < 2.35 ? "stretch" : "stretch-blink";
       break;
   ```
4. `CategoryFor` 里加 `case Mood.Stretching: return "stretching";`，
   并在 `assets/phrases.zh.json` / `.en.json` 的 `phrases` 里加 `"stretching": ["..."]`。
5. 触发：在 `Schedule` 的随机权重里加一项，或 `DemoStep` 的 `seq` 数组里加。
6. `Loc` 里加 `mood.stretching` 的中英文，面板状态会显示它。

## 加一套皮肤

以「小狗」为例：

1. `Core.cs` 的 `Skin` 加 `Dog`。
2. 放 `assets/dog/`：`pet.png`（约 640×600 透明）、`face.png`、`face.ico`（BMP 多尺寸）、可选 `glow.png` / `shadow.png`。
3. `PetForm.RecomputeSize` 里给 Dog 算宽高比。
4. `FrameFor` 开头：
   ```csharp
   if (_skin == Skin.Dog) return _dogPet;
   ```
5. `FacePath` 里返回 `assets/dog/face.png`。
6. `LoadIcon` 的目录判断里加 Dog。
7. `PanelForm` 的「换装」加一个 `DrawOption` + `Hit`，调用 `_pet.SetSkin(Skin.Dog)`。
8. `Config` 里 `Skin` 的字符串（`"dog"`）和 `PetForm` 构造里的解析。
9. `build.ps1` 的图标回退顺序按需要调整。

## 加一种语言

1. `Lang` 加 `Ja`（示例）。
2. `assets/phrases.ja.json` 复制 `phrases.en.json` 的结构，逐类翻译。
3. `Phrases.Load` 里按 `lang` 选文件。
4. `Loc` 的 `Map` 每个 key 现在只有 `[zh, en]`；扩展成三元素或改成字典，
   并在 `Loc.T` 里按 `Current` 取。
5. `PanelForm` 的语言开关、`PetForm` 菜单、`Config` 的解析都要加分支。

## 面板与命中区

`PanelForm` 没有用 WinForms 控件，全部是 GDI+ 画的，交互靠 `_hits`：

```csharp
_hits.Add(new Hit(new RectangleF(34, 402, 96, 36), delegate { _pet.Pat(); }));
```

`OnPaint` 里画按钮，`OnMouseMove` 找 hover，`OnMouseUp` 执行 `Act()`。
加控件 = 加一个 `Hit` + 在 `OnPaint` 对应位置画出来。

> 注意：`Hit` 的矩形是**窗体坐标**；屏幕坐标 = 窗体坐标 + `Location`。

## 构建与调试

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

调试参数：

| 参数 | 作用 |
| --- | --- |
| `--panel` | 启动后直接打开控制面板 |
| `--card` | 启动后直接打开名片 |
| `--poke` | 启动后先戳一下（看气泡/星星/音效） |

日志：`logs/app.log`（启动、热键注册结果、异常、配置/台词加载失败）。
配置：`data/config.json`，删掉即恢复默认。

常见调试点：

- 动作不对：看 `FrameFor` 的 case 和 `_mood`。
- 气泡不出现：看 `ShowBubble` 是否被 `_bubbleUntil` 覆盖；`DrawBubble` 的 `by` 是否在窗口内。
- 点击穿不过透明区：看 `AlphaAt` 和 `WM_NCHITTEST`。
- 热键注册失败：日志里 `hotkeys U=... C=... P=...`，`False` 表示被占用。

## 性能

- 25 fps（40ms Timer），53 张 240×240 位图常驻内存（约 12 MB）。
- 每帧用 GDI+ 画到一张 400×430 的 32bpp 位图，再 `UpdateLayeredWindow`。
- 在测试机上 CPU 约 8%（单核），内存约 100 MB。
- 加特效用预渲染 PNG（`glow.png` / `shadow.png`），不要在每帧里创建大量 `GraphicsPath` / `Font`。

## 发布检查

- [ ] 占位素材可以自由替换；换上的角色图版权归你，建议写进 NOTICE。
- [ ] `FurryPet.exe` 不进仓库（`.gitignore` 已忽略），用 Releases 发布 `FurryPet-template-win64.zip`。
- [ ] 更新 `CHANGELOG.md` 和 README 截图。
- [ ] 跑一次 `build.ps1`，确认干净目录也能编译。
- [ ] 跑一次无 `assets/umi/` 的副本，确认会退回备用皮肤。
