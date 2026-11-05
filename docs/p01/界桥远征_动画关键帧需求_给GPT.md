# 界桥远征 —— 动画关键帧需求（给 GPT）

**一句话**：这一批是**延续你已经交付的 `Assets/Resources/Art/UI/V07/` 里的图继续画动画帧**，
不是重画、不是改造型。每套动画的第 00 帧直接拿你交的那张原图用，后面几帧在**同一张画布、同一构图、
同一光源**上继续演变。我这边有自动补帧，所以你**每套只要画 2–3 张关键帧**就够。

---

## 一、总表：每套动画用哪张原图（照这张表交就不会错）

| # | 动画名（落盘文件夹/文件名） | **延续的原图文件名** | 原图尺寸 | 要画几帧 | 播放方式 |
|---|---|---|---|---|---|
| 1 | `anim_entry_battle` | `home_entry_battle.png` | 200×200 | 2 | 循环 |
| 2 | `anim_entry_shop` | `home_entry_shop.png` | 200×200 | 2 | 循环 |
| 3 | `anim_entry_checkin` | `home_entry_checkin.png` | 200×200 | 2 | 循环 |
| 4 | `anim_entry_codex` | `home_entry_codex.png` | 200×200 | 2 | 循环 |
| 5 | `anim_entry_profile` | `home_entry_profile.png` | 200×200 | 2 | 循环 |
| 6 | `anim_page_wipe` | **无原图，新画** | 512×512 透明 | 3 | 单次 |
| 7 | `anim_checkin_ready` | `checkin_cell_ready.png` | 150×150 | 2 | 循环 |
| 8 | `anim_checkin_stamp` | `checkin_stamp_claimed.png` | 120×120 | 3 | 单次 |
| 9 | `anim_banner_victory` | `banner_victory.png` | 1200×260 | 3 | 单次 |
| 10 | `anim_banner_defeat` | `banner_defeat.png` | 1200×260 | 3 | 单次 |
| 11 | `anim_banner_draw` | `banner_draw.png` | 1200×260 | 3 | 单次 |
| 12 | `anim_coin` | `icon_coin.png` | 64×64 | 2 | 循环 |
| 13 | `anim_gem` | `icon_gem.png` | 64×64 | 2 | 循环 |
| 14 | `anim_reward_open` | `reward_glow.png` | 512×512 | 3 | 单次 |
| 15 | `anim_pack_open_starter` | `shop_pack_starter.png` | 384×512 | 3 | 单次 |
| 16 | `anim_pack_open_beast` | `shop_pack_beast.png` | 384×512 | 3 | 单次 |
| 17 | `anim_pack_open_girl` | `shop_pack_girl.png` | 384×512 | 3 | 单次 |
| 18 | `anim_seal_press` | `trade_seal.png` | 140×140 | 3 | 单次 |
| 19 | `anim_envelope_open` | `trade_envelope.png` | 260×180 | 3 | 单次 |
| 20 | `anim_rarity_1` ～ `anim_rarity_4` | `codex_rarity_1.png` ～ `codex_rarity_4.png` | 各 128×171 | 各 2 | 循环 |
| 21 | `anim_levelup` | `icon_levelup.png` | 64×64 | 3 | 单次 |
| 22 | `anim_soldout` | `shop_soldout_stamp.png` | 180×180 | 3 | 单次 |
| 23 | `anim_arrows_flow` | `trade_arrows.png` | 180×80 | 3 | 循环 |
| 24 | `anim_locked_shake` | `codex_lock.png` | 72×72 | 2 | 单次 |

**第 1–14 条是你现在最需要的（P0 + P1 上半），一共只有 33 张图。**
第 15–24 条（P1 下半 + P2）可以晚一批，一共 26 张。

---

## 二、逐套说明（都从原图往下画）

### 1–5. 大厅 5 个入口：`anim_entry_battle / shop / checkin / codex / profile`
- 原图：`home_entry_battle.png`、`home_entry_shop.png`、`home_entry_checkin.png`、`home_entry_codex.png`、`home_entry_profile.png`（都 200×200 透明）
- 帧数：**2 帧**
  - `_00` = 原图原样
  - `_01` = 同一构图，整体提亮约 15%，在主体最亮处（剑锋 / 钱袋口 / 铃铛 / 书页 / 徽章）加一点高光点，边缘出现极细暖金描边
- 不要：位移、放大、换角度。位移和缩放我自己用代码做

### 6. 页面切换扫光：`anim_page_wipe`
- 无原图，新画 **512×512 透明**
- 3 帧：
  - `_00` 全透明
  - `_01` 一条从左上到右下的柔光带（宽约画布 1/4，边缘羽化，暖白偏青，最大不透明度 55%）
  - `_02` 光带移到右下角并淡出

### 7. 签到"今天可领"格：`anim_checkin_ready`
- 原图：`checkin_cell_ready.png`（150×150，九宫格）
- 2 帧：
  - `_00` = 原图
  - `_01` = 边缘向外扩一圈光（亮度 +20%），格内内容不变
- 循环（我另外还会加代码缩放呼吸，所以你只画"外圈光"就行）

### 8. "已领取"印章砸下：`anim_checkin_stamp`
- 原图：`checkin_stamp_claimed.png`（120×120 透明）
- 3 帧（画布始终 120×120，印章本体不要出画布）：
  - `_00` 印章在上方，1.35 倍大小，完全透明
  - `_01` 砸到 0.95 倍、完全不透明，边缘有溅开的墨点
  - `_02` 稳定态，墨迹定住

### 9–11. 对局结算横幅：`anim_banner_victory / defeat / draw`
- 原图：`banner_victory.png`、`banner_defeat.png`、`banner_draw.png`（各 1200×260 透明）
- 每种 3 帧：
  - `_00` 横幅只露中间 20%，两端是收拢的布卷
  - `_01` 展开到 60%，卷边还在
  - `_02` 完全展开 100%，卷边消失
- 三种共用展开节奏，只换主色/纹样

### 12–13. 货币闪光：`anim_coin` / `anim_gem`
- 原图：`icon_coin.png` / `icon_gem.png`（64×64 透明）
- 各 2 帧：
  - `_00` = 原图
  - `_01` 放大到 1.12 倍 + 4 个细小星芒 + 一圈淡金扩散环
- 循环

### 14. 发奖光爆：`anim_reward_open`
- 原图：`reward_glow.png`（512×512 透明）
- 3 帧：
  - `_00` = 原图（很淡的底光）
  - `_01` 光团膨胀到画布 1.3 倍，中心过曝成白
  - `_02` 炸开成 8–12 条放射细光束，外圈变暗
- 全程只画光，不出现物体

### 15–17. 开卡包：`anim_pack_open_starter / beast / girl`
- 原图：`shop_pack_starter.png`、`shop_pack_beast.png`、`shop_pack_girl.png`（各 384×512 透明）
- 各 3 帧：
  - `_00` = 原图（完整卡包）
  - `_01` 封蜡崩开、包体撕开一道口子，裂口透光
  - `_02` 包体化成光点散开（只剩一点残影）

### 18. 火漆压印：`anim_seal_press`
- 原图：`trade_seal.png`（140×140 透明）
- 3 帧：悬空半透明 → 压印到位（边缘挤出一点蜡边）→ 定住

### 19. 信封打开：`anim_envelope_open`
- 原图：`trade_envelope.png`（260×180 透明）
- 3 帧：封口微掀 → 掀开一半、内部透光 → 完全打开停留

### 20. 稀有度流光：`anim_rarity_1` ～ `anim_rarity_4`
- 原图：`codex_rarity_1.png` ～ `codex_rarity_4.png`（各 128×171，九宫格描边）
- 各 2 帧：`_00` = 原图；`_01` = 描边上有一段更亮的流光（其余不变）
- 循环（看起来像边框上有光在走）

### 21. 升级爆发：`anim_levelup`
- 原图：`icon_levelup.png`（64×64 透明）
- 3 帧：`_00` 原图 → `_01` 向上爆出光柱、整体放大 1.2 倍 → `_02` 光柱散开变淡

### 22. 售罄章：`anim_soldout`
- 原图：`shop_soldout_stamp.png`（180×180 透明）
- 3 帧：上方半透明 → 砸中（红色墨点溅开）→ 定住

### 23. 交换箭头流动：`anim_arrows_flow`
- 原图：`trade_arrows.png`（180×80 透明）
- 3 帧：`_00` 原图 → `_01` 光点沿箭头由左向右移动一段 → `_02` 光点继续到右端并淡出（循环）

### 24. 锁头摇动：`anim_locked_shake`
- 原图：`codex_lock.png`（72×72 透明）
- 2 帧：`_00` 原图 → `_01` 锁头整体向右倾 6°（其余不变，位移我来做）

---

## 三、交付格式（二选一，都能自动接管）

**格式 A：逐帧文件夹（推荐，最直观）**
```
Assets/Resources/Art/UI/V07/Anim/<动画名>/<动画名>_00.png
                                      <动画名>_01.png
                                      ...
```
例：`Assets/Resources/Art/UI/V07/Anim/anim_checkin_stamp/anim_checkin_stamp_00.png`

**格式 B：一张横排图集（省文件数）**
```
Assets/Resources/Art/UI/V07/Anim/anim_checkin_stamp_sheet3.png
```
`_sheet3` = 横向 3 格（每格宽 = 图宽 / 3，高 = 图高）；也支持 `_sheet3x2`（3 列 2 行，按行优先）。
我有切图工具，会自动切成 `anim_checkin_stamp_00 … _02`。

---

## 四、硬性要求（违反了我会拿不到/显示错位）

1. **画布尺寸必须和原图一模一样**（见总表），逐帧同画布，不要裁切、不要缩放；
2. 除 `bg_*` 外全部**透明底、四角 alpha=0**；
3. `_00` 帧直接用原图（或原图 + 极轻微提亮），**不要重新构图**；
4. 不改配色、不改造型、不改透视，只做"光 / 开合 / 砸落 / 流光"这类延续变化；
5. 文件名严格照抄（大小写、两位编号），我按名字加载；
6. 不要在图里画文字或数字。

---

## 五、我这边已经做了、**不需要你画**的

| 部分 | 做法 |
|---|---|
| 按钮按下缩放回弹、面板弹入、页面淡入、提示条滑入 | 代码补间 |
| 中间帧补间（你只给 2–3 张关键帧） | 运行时交叉淡入 + 缩放曲线 |
| 伤害飘字、棋盘格抖动/闪白、受击/护卫特效 | 已有 `FX/hit_*`、`FX/guard_*` + `V06HudFx` |
| 进化、融合特效 | 上一批的 `FX/evolve`、`FX/fuse`（8 帧）已够用 |
| 卡牌飞出/缩放/翻面、货币滚动、胜率环、费用曲线 | 代码绘制 |

---

## 六、优先级

1. **第 1–5 条**（5 个入口悬停，10 张）—— 大厅第一眼
2. **第 7–8 条**（签到脉动 + 盖章，5 张）—— 每天都会看到
3. **第 9–11 条**（结算横幅，9 张）—— 每局结束都看
4. **第 6、12–14 条**（扫光 + 货币 + 光爆，9 张）—— 手感
5. **第 15–24 条** —— 等商店/交换功能上线前后再交
