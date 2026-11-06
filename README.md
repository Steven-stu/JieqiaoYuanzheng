# 界桥远征 / Jieqiao Yuanzheng (V0.7)

一个用 **Unity 2022.3.62f2c1** 做的中文单机卡牌对战游戏：252 张卡、8 个阵营、3 前排 + 3 后排棋盘、
进化 / 融合双成长线、本地局域网联机（房间号 + 密码）、大厅 / 图鉴 / 组卡 / 签到 / 商店 / 资料 六个系统页。

> 这个仓库是**分片存档**：为了满足「每个文件夹 ≤ 5 MB」，美术与动画资源被拆成 `p01`/`p02`… 多个分片目录。
> 原始路径与仓库路径的完整对应关系见根目录 **`MANIFEST.csv`**（3255 行，`original_path,repo_path,bytes`）。

---

## 目录结构

```
source/scripts/pNN/          C# 代码（规则核心 Prototype.Core + UI），带 .meta
source/editor/pNN/           编辑器工具（美术导入、图集切片、自动检查）
source/scenes/pNN/           Unity 场景（V06Battle.unity 是主场景）
source/screenshots/pNN/      实机截图
project/ProjectSettings/pNN/ 工程设置
project/Packages/pNN/        包依赖清单
art/cards/pNN/               252 张卡面立绘（384×512 PNG，文件名 = 卡ID）
art/anim/pNN/                卡面人物关键帧：<卡ID>/<卡ID>_NN.png（待机）、<卡ID>/attack/、/hit/、/bloom/
art/ui/pNN/                  GPT 交付的界面美术（V07 皮 + 序列帧动画）
art/chars/pNN/               派系「三姿图」兜底立绘
audio/sfx/pNN/               17 个界面/战斗音效（CC0）
audio/meta/pNN/              音频说明（CREDITS.txt）
audio/bgm_parts/pNN/         4 首 BGM 的 4.4 MB 分片（见下）
docs/                        设计与美术需求文档（中文）
tools/reassemble_bgm.ps1     BGM 分片重组脚本
MANIFEST.csv                 全量路径对照表
```

## 怎么还原成可打开的 Unity 工程

1. Unity Hub 新建一个 **2022.3.62f2c1** 项目（2D 模板即可）。
2. 把仓库里的分片按 `MANIFEST.csv` 的 `original_path` 放回去，等价于：

```text
art/cards/**            → Assets/Resources/Art/units/
art/anim/**             → Assets/Resources/Art/Units/Anim/
art/ui/**               → Assets/Resources/Art/UI/
art/chars/**            → Assets/Resources/Art/chars/
audio/sfx/**            → Assets/Resources/Audio/Sfx/
audio/meta/CREDITS.txt  → Assets/Resources/Audio/
source/scripts/**       → Assets/Scripts/          （Prototype.Core 放 Assets/Scripts/Core/）
source/editor/**        → Assets/Editor/
source/scenes/**        → Assets/Scenes/
project/ProjectSettings/** → ProjectSettings/
project/Packages/**     → Packages/
```

3. BGM 分片先重组：`powershell -ExecutionPolicy Bypass -File tools\reassemble_bgm.ps1`，
   产出 `audio/Bgm_reassembled/*.mp3`，再放进 `Assets/Resources/Audio/Bgm/`。
4. 打开 `Assets/Scenes/V06Battle.unity` → Play。
5. 编辑器菜单 **界桥远征 → 导入卡面立绘设置** 会把 `Art/Units/**` 全部贴图设成 Sprite / Single；
   **界桥远征 → 导入音频设置** 会把 BGM 设成 Streaming、音效设成 ADPCM。

## 架构要点

- **规则核心与表现层分离**：`Prototype.Core` 是无 UnityEngine 依赖的纯 C# 程序集（`V06Engine` / `V06Catalog` / `V06Expansion`），
  引擎是确定性的（同种子 + 同行动序列 → 同结果），因此局域网联机用**锁步**实现，只交换行动。
- **界面全部代码构建**：不依赖 Prefab / TMP / DOTween，坐标在 1600×900 设计空间，根节点按 `min(w/1600, h/900)` 缩放。
- **素材缺失自动降级**：皮肤 / 关键帧缺失时退化成纯色或程序化动画，不会报错。
- **卡面人物动画**：一张卡最多 4 个通道 —— 待机 `idle`、出手 `attack`、受击 `hit`、绽放 `bloom`；
  没有关键帧的卡会走"程序化呼吸 / 前顶 / 泛红"兜底，保证全部 252 张都有反馈。

## 素材授权

| 内容 | 来源 | 授权 |
|---|---|---|
| 4 首 BGM | **Kevin MacLeod (incompetech.com)** — Village Consort / Royal Coupling / The Snow Queen / The Path of the Goblin King | CC BY 4.0，需署名 |
| 17 个音效 | **Juhani Junkala**, The Essential Retro Video Game Sound Collection | CC0（无需署名） |
| 卡面立绘 / 界面美术 / 关键帧 | 本项目自制（AI 辅助生成 + 人工整合） | 随本仓库 |

BGM 署名（转载/发布请保留）：

```text
Music by Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/
```

## 已知状态

- 卡池 332 张（192 基础 + 140 扩展：兽群 44 / 少女 16 / N 系列 80）；兽群 44（14 进化线 / 8 终态 / 8 融合体）+ 少女 16。
- 卡面立绘 332/332 已就位（N61–N140 于 2026-11-06 到位）；人物关键帧 117 套 / 599 帧（108 张卡）。
- 编辑器自动检查：`V06ShellChecks`（存档/卡组/联机/确定性）11 项、`V06ShellAutoTest`（全站 9000+ 次真实点击）0 问题。
- 未完成：交换卡牌页仍是占位；战术/装备类卡只有立绘没有立绘动画。
