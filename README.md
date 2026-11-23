# ABYSS ECHO · 深渊回声

> 第一人称科幻心理恐怖探索游戏
> First-person sci-fi psychological horror exploration game

---

## 项目信息

| 项 | 值 |
|---|---|
| 引擎 | Unity 2023.1.1f1c1（中国版） |
| 渲染管线 | Universal Render Pipeline (URP) 15.0.6 |
| 语言 | C# |
| 平台 | Windows PC（64-bit） |
| 模式 | 单机 |
| 版本 | v1.0.0 |

---

## 游戏内容

五个章节，约 3~5 小时流程。

| 章节 | 场景 | 核心机制 |
|---|---|---|
| 第一章 · 深潜 | `Level_Chapter1_Graybox` | 保险丝 / 电力恢复 / 门禁密码 |
| 第二章 · 回声 | `Level_Chapter2_Graybox` | 水泵 / 频率终端 / 录音机 |
| 第三章 · 镜沼 | `Level_Chapter3_Graybox` | 多结局系统 / 长明灯谜题 |
| 第四章 · 心腔 | `Level_Chapter4_Graybox` | 心跳节拍 / 心瓣 / 核心腔 |
| 第五章 · 出口 | `Level_Chapter5_Graybox` | 撤离控制台 / 三结局选择 |

### 核心玩法

- **潜行与躲避** —— 没有武器，只能躲
- **一击必杀** —— 被怪物抓住直接死亡（类似《逃生》《失忆症》）
- **环境叙事** —— 通过纸条、录音带、日志拼凑真相
- **多结局** —— 第三章、第五章各有多个结局分支

### 操作

| 按键 | 功能 |
|---|---|
| W A S D | 行走 |
| 鼠标 | 环视 |
| Shift | 奔跑（消耗体力） |
| Ctrl | 蹲下（更安静） |
| E | 拾取 / 开门 / 使用设备 |
| F | 手电筒 |
| Tab | 物品栏 |
| J | 日志 |
| H | 操作说明 |
| Esc | 暂停菜单 |
| M | 和平模式 |
| 空格 | 跳过结局（结局播放中） |
| R | 死亡后重新开始 |

---

## 开发环境搭建

### 前置要求

- **Unity 2023.1.1f1c1**（或其他 2023.1.x 版本）
  - 需通过 Unity Hub 安装
  - 中国版编辑器下载：[Unity 中国](https://unity.cn/releases)
- **Windows 10 / 11**

### 打开项目

1. 克隆仓库
   ```bash
   git clone <仓库地址>
   cd ABYSS_ECHO/UnityProject
   ```

2. 用 Unity Hub 添加项目
   - 打开 Unity Hub
   - 点击「Add」→ 选择 `UnityProject` 文件夹
   - 用 **2023.1.1f1c1** 打开

3. 首次打开会重建 `Library` 目录
   - 约 2~5 分钟
   - `Library/` 不纳入版本控制（已在 `.gitignore` 中排除）

### 运行游戏

1. 打开 `Assets/_Project/Scenes/MainMenu.unity`
2. 点击 Unity 顶部 **Play** 按钮

或直接打开任意章节场景（`Level_Chapter1_Graybox` 等）按 Play。

---

## 项目结构

```
UnityProject/
├── Assets/
│   ├── _Project/
│   │   ├── Art/            材质、贴图、模型
│   │   ├── Audio/          音频（当前为空）
│   │   ├── Data/           数据资产
│   │   ├── Input/          Input System 配置
│   │   ├── Prefabs/        预制体
│   │   ├── Scenes/         场景文件（启动 + 五章 + 模板）
│   │   ├── Scripts/        全部 C# 代码（535 个文件）
│   │   └── Settings/       URP 管线配置
│   ├── Settings/
│   └── Art/
├── Packages/               Unity 包依赖清单
├── ProjectSettings/        项目设置（版本、图形、输入等）
├── .gitignore
├── .gitattributes
└── README.md
```

### 代码命名空间

| 命名空间 | 职责 |
|---|---|
| `AbyssEcho.Player` | 玩家控制（移动、视角、输入） |
| `AbyssEcho.Interaction` | 交互系统（拾取、门、笔记） |
| `AbyssEcho.Inventory` | 背包 |
| `AbyssEcho.Narrative` | 剧情、日志、结局 |
| `AbyssEcho.Horror` | 恐惧系统、怪物 AI、死亡 |
| `AbyssEcho.Puzzle` | 谜题（键盘、电力、心瓣） |
| `AbyssEcho.UI` | 所有界面 |
| `AbyssEcho.Audio` | 音频管理 |
| `AbyssEcho.Save` | 存档系统 |
| `AbyssEcho.LevelGen` | 灰盒关卡生成 |
| `AbyssEcho.Tools` | 手电筒等工具 |
| `AbyssEcho.Core.EditorTools` | 编辑器工具（不进构建） |
| `AbyssEcho.DebugTools` | 自动化测试脚本 |

---

## 构建

### 打包 Windows 64-bit 正式版

1. Unity 菜单 → `File` → `Build Settings`
2. 确认场景列表（`MainMenu` 必须在序号 0）
3. `Platform` 选 **Windows, Mac, Linux** → `Target Platform` 选 **Windows**
4. `Architecture` 选 **x86_64**
5. 点击 `Build`

或使用内置工具：

```
Unity 菜单 → ABYSS ECHO → 正式构建 Windows64
```

输出到 `Builds/Release/ABYSS_ECHO_Final/`

---

## 已知问题

| 问题 | 影响 | 等级 |
|---|---|---|
| 无音频资源 | 游戏全程静音（不影响玩法） | P3 |
| 第一、二章怪物无 Animator Controller | 模型滑动，无走路动画（AI 正常） | P3 |
| 主菜单标题文字重叠 | 「深渊回声」与章节标签部分重叠 | P3 |

---

## 技术栈

- Unity URP 15.0.6
- Input System 1.6.1（`activeInputHandler = Both`，新旧输入系统共存）
- TextMeshPro 3.0.6
- Unity UI (uGUI)
- Timeline 1.8.2
- AI Navigation 1.1.5

---

## 素材来源

| 素材 | 来源 | 许可 |
|---|---|---|
| ambientCG 贴图集（6 套 2K PBR） | [ambientCG](https://ambientcg.com/) | CC0 |
| 有机组织贴图（FleshWet / Membrane / SlimeBio） | 程序化生成 | 原创 |
| 灰盒网格 | 程序化生成 | 原创 |
| 全部代码与剧情文本 | — | 原创 |

---

## 许可

本项目代码与剧情文本版权归作者所有。

引用的第三方素材遵循其各自许可（见上方「素材来源」）。

---

## 截图

（暂无）