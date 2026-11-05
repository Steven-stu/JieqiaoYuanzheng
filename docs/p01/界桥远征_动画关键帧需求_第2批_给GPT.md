# 界桥远征 —— 动画关键帧需求·第 2 批（给 GPT）

第 1 批 27 套已经全部装进游戏并验证通过（`AnimSetsLoaded=14/14` 全命中、27/27 套内部尺寸一致）。
这一批是**接着做还缺的动画**。规则和第 1 批完全一样：**每套都接着你已交付的那张原图往下画**，
首帧＝原图，同画布、同画风、同光源，不重构图。

---

## 一、总表

| # | 动画名 | **延续的原图** | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| 1 | `anim_card_deal` | 新建（卡背用 `cardback_default`） | 384×512 | 3 | 单次 |
| 2 | `anim_card_flip` | `cardback_default` → 任意卡面 | 384×512 | 3 | 单次 |
| 3 | `anim_turn_banner` | 新建 | 600×120 透明 | 3 | 单次 |
| 4 | `anim_flag_flash` | 新建（远征旗条） | 1600×96 九宫格 | 2 | 循环 |
| 5 | `anim_slot_ring_evolve` | 新建 | 128×128 透明 | 2 | 循环 |
| 6 | `anim_slot_ring_fuse` | 新建 | 128×128 透明 | 2 | 循环 |
| 7 | `anim_keyword_ping` | 新建 | 96×96 透明 | 2 | 单次 |
| 8 | `anim_shelf_hover` | `shop_shelf_frame` | 300×380 九宫格 | 2 | 循环 |
| 9 | `anim_refresh_wipe` | `shop_shelf_frame` | 300×380 | 3 | 单次 |
| 10 | `anim_save_confirm` | 新建 | 240×64 透明 | 2 | 单次 |
| 11 | `anim_toast_wipe` | `notify_toast` | 560×96 九宫格 | 2 | 单次 |
| 12 | `anim_avatar_hover` | `profile_avatar_slot` | 320×320 | 2 | 循环 |
| 13 | `anim_winrate_glow` | `profile_winrate_ring` | 240×240 | 2 | 循环 |
| 14 | `anim_loading_spin` | 新建 | 96×96 透明 | 4 | 循环 |
| 15 | `anim_coin_fly` | `icon_coin` | 48×48 透明 | 3 | 单次 |
| 16 | `anim_trade_sent` | `trade_envelope` | 260×180 | 3 | 单次 |
| 17 | `anim_cardback_glow` | `cardback_default` | 384×512 | 2 | 循环 |
| 18 | `anim_bg_dust` | `bg_home` | 1600×900 | 3 | 循环（叠加层） |
| 19 | `anim_bg_embers` | `bg_home` | 1600×900 | 3 | 循环（叠加层） |
| 20 | `anim_rarity_glow_5` | `codex_rarity_4` | 128×171 | 2 | 循环（传说专用更强流光） |

**优先级**：第 1–7 条是战斗界面（每局都会看到，最值得先画）；8–15 是系统界面手感；16–20 是氛围润色。

---

## 二、逐套说明

### 1. `anim_card_deal` —— 摸牌入场（战斗手牌）
- 无原图，新画 **384×512 透明**（卡背直接用 `cardback_default.png` 的构图往下演）
- 3 帧：
  - `_00` 一团淡金色光点（牌还没成形）
  - `_01` 卡牌半透明、整体缩小到 0.8 倍、牌面还看不清
  - `_02` 卡牌完全成形、正面朝向、边缘还有一点余辉
- 循环：否。我在手牌位置播完立刻换成真正的卡面

### 2. `anim_card_flip` —— 翻牌（开包/图鉴解锁）
- 基于 `cardback_default.png`（384×512）
- 3 帧：
  - `_00` = 卡背原图
  - `_01` 卡背被压扁到宽度的 55%（像侧过来），左右留一点高光
  - `_02` 卡面已经朝前、宽度恢复到 100%，边缘一圈白光
- 循环：否

### 3. `anim_turn_banner` —— 回合切换提示
- 无原图，新画 **600×120 透明**
- 3 帧：`_00` 一条窄光带 → `_01` 向两侧展开成横幅、中间空出来（留给文字）→ `_02` 两端有收口装饰的完整横幅
- 循环：否。文字我用代码画，**不要在图上画字**

### 4. `anim_flag_flash` —— 远征旗受击闪烁
- 无原图，新画 **1600×96**，**九宫格左右各 24px**（我会横向拉伸）
- 2 帧：`_00` 完全透明 → `_01` 从左到右一条暖红/金色的受击光带（峰值不透明度 40%）
- 循环：是（受击瞬间连播两轮）

### 5. `anim_slot_ring_evolve` —— 进化时格子底环
- 无原图，新画 **128×128 透明**
- 2 帧：`_00` 一圈很淡的金环（不透明度 20%）→ `_01` 同位置更亮更粗（不透明度 45%）+ 四个方位的小三角标记
- 循环：是。我会把它放在棋盘格下方，进化时叠上上一批的 `FX/evolve`

### 6. `anim_slot_ring_fuse` —— 融合时格子底环
- 与 5 同规格 **128×128**，但换成青色/紫色、纹样用"两个环交汇"
- 2 帧，循环

### 7. `anim_keyword_ping` —— 关键词触发的一圈光
- 无原图，新画 **96×96 透明**
- 2 帧：`_00` 小圆环（30% 不透明度）→ `_01` 放大到接近画布边缘并淡出（10%）
- 单次，用于"护卫挡下 / 汲取回血 / 余烬爆炸"时套在单位格上

### 8. `anim_shelf_hover` —— 商店货位悬停
- 基于 `shop_shelf_frame.png`（300×380 九宫格）
- 2 帧：`_00` = 原图 → `_01` 描边变亮、底部多一层柔和暖光
- 循环

### 9. `anim_refresh_wipe` —— 刷新商品的光扫
- 基于 `shop_shelf_frame.png`（300×380）
- 3 帧：原图 → 一道斜光扫过货位 → 恢复原图（光扫已离开）
- 单次

### 10. `anim_save_confirm` —— 保存卡组的确认
- 无原图，新画 **240×64 透明**
- 2 帧：`_00` 一条细金线 → `_01` 金线展开成短横幅并在右端收成一个小勾
- 单次（配合已交的 `checkin_stamp` 风格）

### 11. `anim_toast_wipe` —— 顶部提示条的滑入尾迹
- 基于 `notify_toast.png`（560×96 九宫格）
- 2 帧：`_00` 完全透明 → `_01` 提示条本体带一条向右拖尾的浅光
- 单次

### 12. `anim_avatar_hover` —— 头像悬停
- 基于 `profile_avatar_slot.png`（320×320）
- 2 帧：原图 → 外圈描边亮起 + 四角小装饰点亮
- 循环

### 13. `anim_winrate_glow` —— 胜率环流光
- 基于 `profile_winrate_ring.png`（240×240）
- 2 帧：原图 → 环上有一段更亮的流光（其余不变）
- 循环

### 14. `anim_loading_spin` —— 加载转圈
- 无原图，新画 **96×96 透明**
- 4 帧：一段圆弧依次转到 90° / 180° / 270° / 360°，线宽约 8px，暖金色
- 循环

### 15. `anim_coin_fly` —— 金币飞向顶栏的拖尾
- 基于 `icon_coin.png`（48×48）
- 3 帧：`_00` 原图（清晰）→ `_01` 缩小到 0.8 倍并带一点速度残影 → `_02` 只剩一点光点
- 单次；位移我自己做

### 16. `anim_trade_sent` —— 交换请求发出
- 基于 `trade_envelope.png`（260×180）
- 3 帧：原图 → 信封合上并带一道向上的风痕 → 信封缩成一点光
- 单次

### 17. `anim_cardback_glow` —— 卡背流光（牌库/抽牌堆）
- 基于 `cardback_default.png`（384×512）
- 2 帧：原图 → 卡背中央的纹章亮起并有一圈光扩散
- 循环

### 18. `anim_bg_dust` —— 大厅背景浮尘（叠加层）
- 基于 `bg_home.png` 的 **1600×900**，但**只画浮尘/微光点**（不要重画背景，其余全透明）
- 3 帧：光点位置略微错开（像缓慢飘动，位移幅度 20–40px）
- 循环；我会把它铺在 `bg_home` 上面做视差氛围

### 19. `anim_bg_embers` —— 战斗场地余烬（叠加层）
- 同上规格 **1600×900**，只画零星余烬/火星（暖橙），其余全透明
- 3 帧，循环

### 20. `anim_rarity_glow_5` —— 传说级描边（更强流光）
- 基于 `codex_rarity_4.png`（128×171）
- 2 帧：原图 → 流光更亮、金色更饱和 + 四角星点
- 循环

---

## 三、交付格式（和第 1 批一致，都能自动接管）

- 逐帧：`Assets/Resources/Art/UI/V07/Anim/<动画名>/<动画名>_00.png …`
- 或者图集：`Assets/Resources/Art/UI/V07/Anim/<动画名>_sheet3.png`（横排 3 格；`_sheet3x2` = 3 列 2 行）
- 尺寸必须在该动画内**逐帧完全一致**（第 1 批 27/27 套都做到了，继续保持）
- 除背景叠加层（18、19）外，全部透明底、四角 alpha=0
- 不要在图里画文字/数字

---

## 四、参考：第 1 批已验收通过的清单（不用重画）

`anim_entry_battle / shop / checkin / codex / profile`、`anim_page_wipe`、`anim_checkin_ready`、
`anim_checkin_stamp`、`anim_banner_victory / defeat / draw`、`anim_coin`、`anim_gem`、`anim_reward_open`、
`anim_pack_open_starter / beast / girl`、`anim_seal_press`、`anim_envelope_open`、
`anim_rarity_1 / 2 / 3 / 4`、`anim_levelup`、`anim_soldout`、`anim_arrows_flow`、`anim_locked_shake`

> 共 27 套 / 68 帧 / 4.23 MB，已全部导入并接线：大厅 5 个入口在呼吸、页面切换有扫光、签到格脉冲与盖章、
> 结算横幅展开、图鉴/商店/交换的关键帧也都能直接播。
