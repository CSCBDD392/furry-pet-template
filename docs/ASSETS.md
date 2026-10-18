# 素材制作指南

这是这个模板最重要的文档：**把占位图换成你自己的画**。
代码里的状态机、时间轴、面板、名片都不用动，只要按下面的文件名和尺寸放图。

> 一句话：**240×240 透明 PNG，按帧名放进 `assets/umi/`，脚底对齐，重新 build。**

## 1. 画布规范

| 项目 | 要求 |
| --- | --- |
| 尺寸 | **240 × 240**，所有帧一致 |
| 格式 | PNG，RGBA，透明背景 |
| 角色位置 | 水平居中（中心约 x=120），脚底/最低点约 **y=225–235**，每帧保持一致 |
| 背景 | 透明；不要画阴影/地面（程序会自己画投影和光晕） |
| 边缘 | 建议在 2×/4× 画布上画完再缩小，避免锯齿 |
| 留白 | 允许透明留白；不要裁到角色 |
| 命名 | 严格用下面的文件名（全小写、连字符） |

**为什么要统一脚底**：程序把整张 240×240 按比例缩放后，以「底边」对齐桌面基线。
如果某一帧角色突然高 20px，播放时就会上下跳。

## 2. 帧清单

主角色帧放在 `assets/umi/`。`assets/umi` 这个名字是代码里的历史槽位名，
你可以把它当成「主角色」专用目录（想改名就改 `Core.cs` 里的路径）。

### 2.1 基础状态

| 文件名 | 状态 | 备注 |
| --- | --- | --- |
| `idle-open.png` | 发呆（睁眼） | 默认帧 |
| `idle-blink.png` | 发呆（眨眼） | 与 open 只差眼睛 |
| `idle-wink.png` | 发呆（wink） | 单眼眨 |
| `think.png` | 思考 | 托腮/歪头，可加 `?` |
| `think-blink.png` | 思考眨眼 | |
| `work.png` | 工作 | 敲键盘/忙 |
| `work-blink.png` | 工作眨眼 | |
| `wait.png` | 等你 | 歪头/看过来 |
| `wait-blink.png` | 等你眨眼 | |
| `rest.png` | 打盹 | 闭眼，程序会叠 `z z z` |
| `happy.png` | 开心 | 挥爪/wink |
| `happy-blink.png` | 开心眨眼 | |
| `error.png` | 出错 | `xx` 眼/泄气 |
| `concern.png` | 担心（预留） | 目前不会自动触发，可留可删 |
| `concern-blink.png` | 担心眨眼 | |
| `listen.png` | 听你说 | 侧耳/举爪 |
| `listen-blink.png` | 听你说眨眼 | |

### 2.2 放大镜（盯着）

8 帧循环，每帧 0.42 秒：

```
watch-left → watch-left-far → watch-left → watch
→ watch-right → watch-right-far → watch-right → watch
```

| 文件名 | 备注 |
| --- | --- |
| `watch.png` | 放大镜在中间 |
| `watch-left.png` | 偏左 |
| `watch-left-far.png` | 最左 |
| `watch-right.png` | 偏右 |
| `watch-right-far.png` | 最右 |
| `watch-blink.png` | 中间 + 眨眼（可选，当前序列里未强制使用） |

### 2.3 找资料（连续帧）

| 文件名 | 备注 |
| --- | --- |
| `search.png` | 单帧备用（没有连续帧时用） |
| `search-video-000.png` … `search-video-023.png` | 24 帧循环，70ms/帧 |

连续帧建议做成「脑袋拱动 + 尾巴/纸页轻晃」的 1.5–2 秒循环，
帧数不限于 24，程序会按目录里排好序的数量循环。

### 2.4 说话口型

| 文件名 | 备注 |
| --- | --- |
| `talk-closed.png` | 闭嘴 |
| `talk-small.png` | 小口 |
| `talk-medium.png` | 中口 |
| `talk-wide.png` | 大口 |
| `talk-blink.png` | 说话中眨眼（可选） |

气泡出现后 1.5 秒内，程序按约 9fps 在 closed/small/medium/wide 之间循环。

## 3. 时间轴（程序里的 `FrameFor`）

| 状态 | 循环 | 规则 |
| --- | --- | --- |
| 发呆 | 13.0s | 0–7.5 open；7.5–7.63 blink；7.63–11.5 open；11.5–11.63 blink；11.63–12.9 open；12.9–13.2 wink |
| 思考 | 3.1s | 0–2.95 think；剩余 blink |
| 工作 | 1.0s | 0–0.38 work；0.38–0.5 blink；0.5–1.0 work |
| 盯着 | 0.42s/帧 | 8 帧序列 |
| 找资料 | 0.07s/帧 | `search-video-*` 排序后循环 |
| 等你 | 3.4s | 0–3.25 wait；剩余 blink |
| 打盹 | 静态 | rest |
| 开心 | 1.0s | 0–0.72 happy；0.72–0.9 blink；0.9–1.0 happy |
| 听你说 | 2.4s | 0–2.25 listen；剩余 blink |
| 说话 | 9fps | 气泡出现 1.5 秒内覆盖口型 |
| 演示 | 5.2s/状态 | 思考→工作→盯着→找资料→等你→打盹→开心→出错→听你说 |

## 4. 头像与图标

| 文件 | 规格 |
| --- | --- |
| `assets/umi/face.png` | 128×128，头像/半身，程序会按圆形裁剪 |
| `assets/umi/face.ico` | 多尺寸 ICO：16 / 24 / 32 / 48 / 64 / 128，**BMP 格式**（不要 PNG 压缩的 ICO，资源管理器可能不显示） |
| `assets/blue/face.png` / `face.ico` | 备用皮肤同理 |

头像用在控制面板左上角、名片面板左上角、托盘图标。
图标用在托盘和 exe 图标（`build.ps1` 会优先用 `assets/umi/face.ico`）。

## 5. 名片

| 项目 | 要求 |
| --- | --- |
| 文件 | `assets/card.png` |
| 比例 | 4:3 |
| 建议尺寸 | 2048×1536（或 1600×1200） |
| 内容 | 你的扩列卡/名片；QQ/微信二维码请留 ≥3mm 静区 |
| 显示 | 名片面板 628×471；放大窗约 920×690 |

二维码在 628px 宽的卡片上，单个模块最好 ≥4px，否则手机可能扫不出。
不确定就点一下卡片，用放大窗扫。

## 6. 备用皮肤

| 项目 | 要求 |
| --- | --- |
| `assets/blue/pet.png` | 约 640×600，透明背景，单帧静态图 |
| `assets/blue/face.png` / `face.ico` | 头像/图标 |

备用皮肤不做逐帧动画，程序会给它叠加呼吸、摇晃、工作抖动、打盹变暗等整体变换。
适合放「一张好看的立绘」。

## 7. 台词

`assets/phrases.zh.json` / `phrases.en.json`：

```json
{
  "schemaVersion": 1,
  "rotationMs": 6500,
  "phrases": {
    "idle": ["我在这儿呢", "戳我一下试试"],
    "thinking": ["让我想想…"],
    "music": ["♪ 在听《{0}》"]
  }
}
```

程序用到的分类：

| 分类 | 触发 |
| --- | --- |
| `idle` | 发呆/默认 |
| `thinking` | 思考 |
| `editing` | 工作 |
| `running` | 盯着 |
| `searching` | 找资料 |
| `waiting` | 等你 |
| `success` | 开心/成功 |
| `error` | 出错 |
| `listening` | 听你说 |
| `poke_idle` / `poke_busy` / `poke_waiting` | 戳一下（按当前状态） |
| `wake` | 从打盹/等待被唤醒 |
| `greeting_morning` / `noon` / `afternoon` / `evening` / `night` / `late` | 启动时的问候 |
| `card` | 打开名片 |
| `music` | 放歌（`{0}` 是歌名） |
| `reminder_rest` / `reminder_water` | 45 / 90 分钟提醒 |
| `late_night` | 23 点后 |
| `drag` | 拖动（预留） |
| `demo` | 演示开始 |

想加分类：在 JSON 里加数组，然后在 `Pet.cs` 的 `SetMood`/`ShowBubble` 里用 `Phrases.Say("新分类")`。

界面文字（菜单、面板、状态名）不在这里，在 `src/Core.cs` 的 `Loc` 里，中英各一行。

## 8. 工具与工作流

- **画图**：Aseprite / Photoshop / Krita / Clip Studio 都行；导出 240×240 PNG。
- **对齐**：把 `assets/umi/idle-open.png` 作为参考底层，所有帧叠在上面画，保证脚底和头身位置一致。
- **批量导出**：按帧名命名（`idle-open.png` 等），覆盖进 `assets/umi/`。
- **检查**：`powershell -ExecutionPolicy Bypass -File .\build.ps1`，然后跑 `FurryPet.exe`。
  - `--panel` 启动直接开面板；
  - `--poke` 启动先戳一下；
  - 托盘/右键 →「演示一轮」把所有状态过一遍。
- **看日志**：`logs/app.log` 会写缺失的帧/加载失败。

## 9. 加新状态 / 新皮肤

动作、状态机、面板、语言的扩展点都在 [DEVELOPMENT.md](DEVELOPMENT.md)。
简单说：

- 新状态：`Mood` 加枚举 → 放帧 → `FrameFor` 加 case → `CategoryFor` + JSON 加台词 → `Schedule`/`DemoStep` 触发。
- 新皮肤：`Skin` 加枚举 → `assets/<名字>/` → `RecomputeSize` / `FrameFor` / `FacePath` / `LoadIcon` → 面板加一个按钮。

## 10. 替换检查清单

- [ ] 53 张主角色帧（29 张基础 + 24 张找资料）都在 `assets/umi/`，文件名完全一致
- [ ] 每张都是 240×240、透明背景、脚底对齐
- [ ] `idle-open` / `idle-blink` / `idle-wink` 只差眼睛
- [ ] `think` / `work` / `watch` / `search` / `wait` / `rest` / `happy` / `error` / `listen` 都能在演示里看到
- [ ] `face.png` 是 128×128，`face.ico` 是 BMP 多尺寸
- [ ] `assets/card.png` 是 4:3，二维码能扫
- [ ] `phrases.*.json` 里没有占位台词残留
- [ ] 重新 `build.ps1`，跑一遍 `FurryPet.exe`
- [ ] 更新 `shots/` 截图和 README
