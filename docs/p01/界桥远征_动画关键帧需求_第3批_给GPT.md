# 界桥远征 —— 动画关键帧需求·第 3 批（给 GPT）

> 前两批 **47 套 / 118 帧** 已经全部导入 Unity 并通过自动校验（47/47 套帧尺寸一致、47/47 套能被程序正确取到帧）。
> 第 2 批里 `anim_card_deal / anim_card_flip / anim_turn_banner / anim_flag_flash / anim_slot_ring_evolve /
> anim_slot_ring_fuse / anim_toast_wipe / anim_save_confirm / anim_shelf_hover / anim_rarity_glow_5 /
> anim_bg_dust / anim_bg_embers / anim_avatar_hover / anim_loading_spin / anim_winrate_glow /
> anim_cardback_glow` 这 16 套已经接进实际触发点（部署、进化、融合、摸牌、右键详览、回合切换、旗受伤、
> 保存卡组、提示条、图鉴传说格、大厅浮尘、战场余烬、联机转圈……），实机看得到。

这一批是**第 3 批，量比前两批大**：**61 套 / 164 帧**，分 6 组。
如果一次画不完，请**严格按 P0 → P1 → P2 的顺序交**（P0 只有 20 套，是每局都会看到的战斗演出）。

规则和第 1、2 批完全一致，最重要的一条：

> **凡是"基于原图"的，首帧必须与你已交付的那张原图像素一致**（同画布、同构图、同光源、同描边粗细），
> 后面几帧只是在它上面"接着演"。不要重新设计、不要改风格、不要在图上画文字或数字。

---

## 〇、总表（61 套）

### A 组 · 战斗界面核心演出（20 套 / 57 帧）— P0 必做

| # | 动画名 | 延续的原图（文件名 / 规格） | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| A1 | `anim_deploy_summon` | 新建（同 `arena_iron_court` 的锻造法阵风） | 256×256 透明 | 4 | 单次 |
| A2 | `anim_slot_select_ring` | `card_frame_selected.png` | 96×128 | 2 | 循环 |
| A3 | `anim_slot_target_reticle` | `card_frame_target.png` | 96×128 | 3 | 循环 |
| A4 | `anim_attack_lunge` | `card_frame_normal.png` | 96×128 | 3 | 单次 |
| A5 | `anim_guard_ripple` | `FX/guard_05.png`（V06 现有格挡特效末帧） | 64×64 | 3 | 单次 |
| A6 | `anim_death_dissolve` | `card_frame_normal.png` | 96×128 | 3 | 单次 |
| A7 | `anim_hp_drain` | 新建（汲取/回血用的血珠光点） | 96×96 透明 | 3 | 单次 |
| A8 | `anim_ap_drain` | `icon_cost.png` | 64×64 | 3 | 单次 |
| A9 | `anim_round_tick` | `icon_timer.png` | 64×64 | 2 | 单次 |
| A10 | `anim_flag_break` | 新建（远征旗条裂纹，九宫格左右 24） | 1600×96 | 3 | 单次 |
| A11 | `anim_coin_toss` | `icon_coin.png` | 64×64 | 4 | 单次 |
| A12 | `anim_pressure_warning` | 新建（只画四周暗角红雾，中间全透明） | 1600×900 | 3 | 循环（叠加层） |
| A13 | `anim_fuse_link` | 新建（两卡之间的连线光弧） | 320×160 透明 | 3 | 单次 |
| A14 | `anim_evolve_burst` | 新建（进化时的向上爆发光柱） | 192×192 透明 | 4 | 单次 |
| A15 | `anim_response_pop` | `panel_sub.png` | 256×256 九宫格 32 | 2 | 单次 |
| A16 | `anim_hand_refill` | `cardback_default.png` | 384×512 | 3 | 单次 |
| A17 | `anim_deck_pile_pulse` | `cardback_beast.png` | 384×512 | 2 | 循环 |
| A18 | `anim_discard_pile_pulse` | `cardback_girl.png` | 384×512 | 2 | 循环 |
| A19 | `anim_victory_stars` | 新建（只画星屑，其余全透明） | 1600×900 | 3 | 单次 |
| A20 | `anim_defeat_vignette` | 新建（只画四周压暗与裂纹，中间透明） | 1600×900 | 2 | 循环 |

### B 组 · 大厅 / 通用控件（8 套 / 22 帧）— P1

| # | 动画名 | 延续的原图 | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| B1 | `anim_nav_active_sweep` | `tab_active.png` | 180×56 | 3 | 循环 |
| B2 | `anim_window_open` | `panel_popup.png` | 640×360 九宫格 48 | 3 | 单次 |
| B3 | `anim_window_close` | `panel_popup.png` | 640×360 九宫格 48 | 3 | 单次 |
| B4 | `anim_notice_scroll` | `home_notice_ribbon.png` | 700×44 九宫格 24 | 3 | 循环 |
| B5 | `anim_home_entry_unlock` | `home_entry_battle.png` | 200×200 | 3 | 单次 |
| B6 | `anim_click_ripple` | 新建 | 128×128 透明 | 3 | 单次 |
| B7 | `anim_button_press_glow` | `btn_primary_normal.png` | 320×96 九宫格 24 | 2 | 单次 |
| B8 | `anim_tooltip_in` | `panel_sub.png` | 256×256 九宫格 32 | 2 | 单次 |

### C 组 · 图鉴 / 组卡（9 套 / 23 帧）— P1

| # | 动画名 | 延续的原图 | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| C1 | `anim_codex_unlock` | `codex_cell.png` | 128×171 | 3 | 单次 |
| C2 | `anim_codex_lock_shimmer` | `codex_cell_locked.png` | 128×171 | 2 | 循环 |
| C3 | `anim_codex_progress_fill` | `codex_progress_bar.png` | 600×28 | 3 | 单次 |
| C4 | `anim_deck_row_highlight` | `deck_row.png` | 580×48 九宫格 16 | 2 | 循环 |
| C5 | `anim_deck_valid_stamp` | `checkin_stamp_claimed.png` | 120×120 | 2 | 单次 |
| C6 | `anim_deck_invalid_shake` | `deck_panel_right.png` | 620×760 | 3 | 单次 |
| C7 | `anim_deck_cover_flip` | `deck_cover_01.png` | 96×128 | 3 | 单次 |
| C8 | `anim_reserve_slot_ping` | `deck_slot_reserve.png` | 96×128 | 2 | 循环 |
| C9 | `anim_card_gain_fly` | `cardback_premium.png` | 384×512 | 3 | 单次 |

### D 组 · 商店 / 签到 / 交换（11 套 / 27 帧）— P2

| # | 动画名 | 延续的原图 | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| D1 | `anim_shop_shelf_pulse` | `shop_shelf_frame.png` | 300×380 九宫格 18 | 2 | 循环 |
| D2 | `anim_price_pop` | `shop_price_tag.png` | 168×56 九宫格 16 | 2 | 单次 |
| D3 | `anim_buy_burst` | 新建 | 256×256 透明 | 3 | 单次 |
| D4 | `anim_soldout_stamp` | `shop_soldout_stamp.png` | 180×180 | 2 | 单次 |
| D5 | `anim_daily_pack_glow` | `shop_pack_daily.png` | 384×512 | 2 | 循环 |
| D6 | `anim_checkin_flame_flicker` | `checkin_streak_flame.png` | 64×64 | 3 | 循环 |
| D7 | `anim_checkin_board_glow` | `checkin_board.png` | 1280×420 | 2 | 循环 |
| D8 | `anim_reward_card_reveal` | `reward_popup_frame.png` | 800×600 九宫格 40 | 3 | 单次 |
| D9 | `anim_trade_player_row_glow` | `trade_player_row.png` | 1100×64 九宫格 16 | 2 | 循环 |
| D10 | `anim_trade_seal_press` | `trade_seal.png` | 140×140 | 3 | 单次 |
| D11 | `anim_trade_arrows_flow` | `trade_arrows.png` | 180×80 | 3 | 循环 |

### E 组 · 资料 / 卡框（6 套 / 16 帧）— P2

| # | 动画名 | 延续的原图 | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| E1 | `anim_level_bar_fill` | `profile_level_bar.png` | 420×40 | 3 | 单次 |
| E2 | `anim_badge_shine` | `badge_win10.png` | 96×96 | 2 | 循环 |
| E3 | `anim_winrate_ring_fill` | `profile_winrate_ring.png` | 240×240 | 4 | 单次 |
| E4 | `anim_title_ribbon_glow` | `profile_title_ribbon.png` | 360×56 九宫格 16 | 2 | 循环 |
| E5 | `anim_card_frame_holo` | `card_frame_normal.png`（V06） | 96×128 九宫格 9 | 2 | 循环 |
| E6 | `anim_card_frame_legend` | `card_frame_target.png`（V06） | 96×128 九宫格 9 | 3 | 循环 |

### F 组 · 联机 / 天气氛围叠加层（7 套 / 19 帧）— P2

| # | 动画名 | 延续的原图 | 尺寸 | 帧数 | 播放 |
|---|---|---|---|---|---|
| F1 | `anim_net_linked` | 新建 | 320×160 透明 | 3 | 单次 |
| F2 | `anim_net_sync_check` | 新建 | 96×96 透明 | 2 | 单次 |
| F3 | `anim_bg_stars_twinkle` | 新建（只画星点与微光，其余全透明） | 1600×900 | 3 | 循环（叠加层） |
| F4 | `anim_bg_fog_drift` | 新建（薄雾，低不透明度） | 1600×900 | 3 | 循环（叠加层） |
| F5 | `anim_bg_fireflies` | 新建（暖黄萤火） | 1600×900 | 3 | 循环（叠加层） |
| F6 | `anim_bg_rain` | 新建（细雨斜线） | 1600×900 | 3 | 循环（叠加层） |
| F7 | `anim_loading_mask_breath` | `loading_mask.png` | 64×64 | 2 | 循环 |

**P0 = A 组 20 套 57 帧（必做）**；P1 = B + C 组 17 套 45 帧；P2 = D + E + F 组 24 套 62 帧。

---

## 一、逐套说明

### A 组 · 战斗界面核心演出

**A1 `anim_deploy_summon` —— 部署召唤法阵**
无原图，新画 **256×256 透明**，4 帧：
- `_00` 地面出现一个很淡的金色圆环（不透明度 15%），半径只有画布的 20%
- `_01` 圆环扩到 70%，环内侧出现 6 个刻痕/符文
- `_02` 环最亮（60%）并向上冒出细光柱，符文开始旋转
- `_03` 光柱收束成一点、环淡到 20%（"人已经站定了"）
单次。我会在部署那一刻把它垫在格子下面（格子尺寸 90×120），所以**法阵要居中、别画到画布边缘**。

**A2 `anim_slot_select_ring` —— 格子选中环**
基于 `card_frame_selected.png`（96×128）：
- `_00` = 原图
- `_01` = 原图描边更亮更暖（+四角小三角标记），其余像素不变
循环，2 帧。**只改描边和四角，不要动中间的镂空区**（中间要透出卡面立绘）。

**A3 `anim_slot_target_reticle` —— 可攻击/可选目标准星**
基于 `card_frame_target.png`（96×128），3 帧：
- `_00` = 原图
- `_01` = 描边亮起 + 上下各一个向内指的箭头
- `_02` = 箭头推进到边缘、描边峰值亮度
循环（做成"呼吸"感，3 帧循环即可）。

**A4 `anim_attack_lunge` —— 单位出击的前扑残影**
基于 `card_frame_normal.png`（96×128），3 帧：
- `_00` = 原图
- `_01` = 整体横向拉长到 1.08 倍（像被速度带出去），右侧拖两道速度线
- `_02` = 恢复原比例，边缘留一点余辉
单次。位移我在代码里做，你只画"形变 + 速度线"。

**A5 `anim_guard_ripple` —— 护卫格挡涟漪**
基于 `Assets/Resources/Art/UI/V06/FX/guard_05.png`（64×64，格挡特效末帧）：
- `_00` = 该末帧
- `_01` = 盾形轮廓向外扩一圈、内层出现第二道弧
- `_02` = 整体只留一道很淡的外圈（20%）
单次。这套会套在单位格子上（放大到 90×120），所以中心要对齐。

**A6 `anim_death_dissolve` —— 单位阵亡消散**
基于 `card_frame_normal.png`（96×128），3 帧：
- `_00` = 原图
- `_01` = 卡框碎裂成 4–6 块并向外错开 4–8px，整体不透明度 60%
- `_02` = 只剩零星碎片与灰烬点（不透明度 25%）
单次。

**A7 `anim_hp_drain` —— 生命流失 / 汲取的血珠**
无原图，新画 **96×96 透明**，3 帧：`_00` 3 颗暗红血珠（小，不透明度 60%）→ `_01` 血珠聚拢成一道向上的细流 → `_02` 只剩最上面一点光。单次。

**A8 `anim_ap_drain` —— 行动点被消耗**
基于 `icon_cost.png`（64×64）：`_00` = 原图 → `_01` 图标左半边暗下去、右侧有一道扫光 → `_02` 图标整体变暗并缩到 0.9 倍、外圈一道扩散光。单次。

**A9 `anim_round_tick` —— 轮数跳动**
基于 `icon_timer.png`（64×64）：`_00` = 原图 → `_01` 指针转过一格 + 外圈亮一下。单次。

**A10 `anim_flag_break` —— 远征旗破裂**
无原图，新画 **1600×96**，**九宫格左右各 24px**（我会横向拉伸）：
- `_00` 完全透明
- `_01` 从左到右出现锯齿裂纹（暗红+亮白高光），峰值不透明度 70%
- `_02` 裂纹变宽并溢出一些碎屑，不透明度降到 30%
单次。配合已有的 `anim_flag_flash` 一起用（先闪再裂）。

**A11 `anim_coin_toss` —— 先手掷币**
基于 `icon_coin.png`（64×64），4 帧：`_00` 正面清晰 → `_01` 立起来（压扁到宽度 20%，侧面有厚度）→ `_02` 背面清晰 → `_03` 落定、外圈一圈光。单次。

**A12 `anim_pressure_warning` —— 轮末压力警告**
无原图，新画 **1600×900** 叠加层，**只在四周画暗红雾与警告纹，画面中间 60% 区域必须全透明**（否则看不清棋盘）：
- `_00` 四周暗角 20%
- `_01` 扩大到 40%，四角出现很淡的斜纹
- `_02` 回落到 25%（做呼吸）
循环，3 帧。

**A13 `anim_fuse_link` —— 融合前的双卡连线**
无原图，新画 **320×160 透明**，3 帧：`_00` 左右两端各一个小光点 → `_01` 两点之间拉出一道弧线、中间开始收束 → `_02` 只剩中间的汇聚点（光点最大）。单次。

**A14 `anim_evolve_burst` —— 进化爆发**
无原图，新画 **192×192 透明**，4 帧：`_00` 中心一点亮核 → `_01` 向上冒出光柱、两侧羽翼状光弧展开 → `_02` 光柱最亮、周围 12 个小光点散开 → `_03` 只剩几粒余辉。单次。

**A15 `anim_response_pop` —— 响应窗口弹出**
基于 `panel_sub.png`（256×256 九宫格 32）：`_00` = 原图缩到 0.9 倍、不透明度 0 → `_01` = 恢复 100%、不透明度 100%，边缘有一道亮边。单次。

**A16 `anim_hand_refill` —— 手牌补满**
基于 `cardback_default.png`（384×512），3 帧：`_00` = 卡背铺满（不透明度 100%）→ `_01` 卡背向右倾斜 8° 并滑出画面一半、露出一角卡面 → `_02` 只剩一张卡面（无字，用 U-M04 那张插画的构图即可）。单次。

**A17 `anim_deck_pile_pulse` —— 牌库堆脉动**
基于 `cardback_beast.png`（384×512）：`_00` = 原图 → `_01` = 卡背纹章发出一圈光、整堆叠纸厚了一层（多一道边）。循环。

**A18 `anim_discard_pile_pulse` —— 弃牌堆脉动**
基于 `cardback_girl.png`（384×512）：同上但颜色偏灰紫、光更弱。循环。

**A19 `anim_victory_stars` —— 胜利星屑**
无原图，新画 **1600×900**，只画星屑/光带，其余全透明：`_00` 中央一团金色爆发 → `_01` 星屑向四周扩散到画面 60% → `_02` 稀疏落下（大星屑降到 30% 不透明度）。单次。

**A20 `anim_defeat_vignette` —— 败北暗角**
无原图，新画 **1600×900**，**中间必须完全透明**：`_00` 四周压暗 35% → `_01` 压暗 60% + 远处有几道裂纹。循环（2 帧呼吸）。

### B 组 · 大厅 / 通用控件

**B1 `anim_nav_active_sweep`** 基于 `tab_active.png`（180×56）：原图 → 一道暖光从左划到中间 → 光右移出画布、留下微亮底色。循环 3 帧。
**B2 `anim_window_open`** 基于 `panel_popup.png`（640×360，九宫格 48）：原图缩到 0.92 倍且不透明度 0 → 恢复 100% 并有一圈扩散光 → 稳定原样（可带一点点高光）。单次。
**B3 `anim_window_close`** 同上反向：原图 → 边缘收暗、整体缩到 0.95 倍 → 不透明度 15%。单次。
**B4 `anim_notice_scroll`** 基于 `home_notice_ribbon.png`（700×44，九宫格 24）：一条高光从左向右划过（3 帧覆盖全过程）。循环。
**B5 `anim_home_entry_unlock`** 基于 `home_entry_battle.png`（200×200）：锁形暗影散去 → 描边亮起 + 四角星点 → 完整图带柔光。单次。
**B6 `anim_click_ripple`** 新建 **128×128 透明**：小圆环（50%）→ 放大到 80% 并变淡（30%）→ 贴边消失（5%）。单次。
**B7 `anim_button_press_glow`** 基于 `btn_primary_normal.png`（320×96，九宫格 24）：原图 → 内侧压暗一点 + 外圈亮一线。单次。
**B8 `anim_tooltip_in`** 基于 `panel_sub.png`（256×256，九宫格 32）：缩 0.94 + 不透明度 0 → 原图 + 左侧一道小光。单次。

### C 组 · 图鉴 / 组卡

**C1 `anim_codex_unlock`** 基于 `codex_cell.png`（128×171）：问号遮罩碎开 → 卡面浮现 + 一圈金色扩散 → 稳定。单次（可参考你已交的 `anim_rarity_4`）。
**C2 `anim_codex_lock_shimmer`** 基于 `codex_cell_locked.png`（128×171）：原图 → 锁孔处有一点更暗的呼吸光。循环。
**C3 `anim_codex_progress_fill`** 基于 `codex_progress_bar.png`（600×28）：空条 → 条内 1/3 亮金填充 + 头部有光点 → 填充不变但光点淡出。单次。
**C4 `anim_deck_row_highlight`** 基于 `deck_row.png`（580×48，九宫格 16）：原图 → 左右两端各亮一线、底边多一层暖光。循环。
**C5 `anim_deck_valid_stamp`** 基于 `checkin_stamp_claimed.png`（120×120）：细金线 → 展开成圆章轮廓 → 完整章 + 一圈碎光。单次。
**C6 `anim_deck_invalid_shake`** 基于 `deck_panel_right.png`（620×760）：原图 → 向右偏 6px、边缘泛红 → 回正并偏左 4px、红色更淡。单次（抖动本身我在代码里做，你画"泛红"就够）。
**C7 `anim_deck_cover_flip`** 基于 `deck_cover_01.png`（96×128）：封面 → 压扁到宽度 55% 并加高光 → 展开成另一张封面。单次。
**C8 `anim_reserve_slot_ping`** 基于 `deck_slot_reserve.png`（96×128）：原图 → 描边亮起 + 顶部一个小箭头。循环。
**C9 `anim_card_gain_fly`** 基于 `cardback_premium.png`（384×512）：卡背清晰 → 缩小到 0.7 倍并带残影 → 只剩一点光。单次。

### D 组 · 商店 / 签到 / 交换

**D1 `anim_shop_shelf_pulse`** 基于 `shop_shelf_frame.png`（300×380，九宫格 18）：原图 → 描边与底部暖光同时亮 15%。循环。
**D2 `anim_price_pop`** 基于 `shop_price_tag.png`（168×56，九宫格 16）：原图 → 放大到 1.06 倍并高光 → 回正。单次。
**D3 `anim_buy_burst`** 新建 **256×256 透明**：中心亮核 → 向四周迸出 8 道短光与金币状光点 → 只剩余辉。单次。
**D4 `anim_soldout_stamp`** 基于 `shop_soldout_stamp.png`（180×180）：半透明斜向落下 → 完整盖章 + 一圈碎光。单次。
**D5 `anim_daily_pack_glow`** 基于 `shop_pack_daily.png`（384×512）：原图 → 卡包底部与顶部各亮一线、封口处有流光。循环。
**D6 `anim_checkin_flame_flicker`** 基于 `checkin_streak_flame.png`（64×64）：3 帧火焰左右抖动 + 亮度呼吸。循环。
**D7 `anim_checkin_board_glow`** 基于 `checkin_board.png`（1280×420）：原图 → 板上有一道很淡的横向流光。循环 2 帧。
**D8 `anim_reward_card_reveal`** 基于 `reward_popup_frame.png`（800×600，九宫格 40）：框体缩 0.9 + 不透明度 0 → 恢复并有一道自上而下的光 → 稳定 + 四角星点。单次。
**D9 `anim_trade_player_row_glow`** 基于 `trade_player_row.png`（1100×64，九宫格 16）：原图 → 左侧头像位一圈光、整行右侧有一层淡暖光。循环。
**D10 `anim_trade_seal_press`** 基于 `trade_seal.png`（140×140）：印章自上方落下（带轻微倾斜）→ 盖实 + 一圈碎光 → 稳定。单次。
**D11 `anim_trade_arrows_flow`** 基于 `trade_arrows.png`（180×80）：左箭头亮 → 中间小光点流动 → 右箭头亮。循环 3 帧。

### E 组 · 资料 / 卡框

**E1 `anim_level_bar_fill`** 基于 `profile_level_bar.png`（420×40）：空条 → 填充 60% + 光点在头部 → 填满并淡出光点。单次。
**E2 `anim_badge_shine`** 基于 `badge_win10.png`（96×96）：原图 → 斜向一道高光扫过（只改高光，不动形状）。循环。
**E3 `anim_winrate_ring_fill`** 基于 `profile_winrate_ring.png`（240×240）：空环 → 环上 1/3 亮起 → 2/3 → 满环 + 一圈扩散光。单次，4 帧。
**E4 `anim_title_ribbon_glow`** 基于 `profile_title_ribbon.png`（360×56，九宫格 16）：原图 → 缎带边缘亮一线 + 右端小星点。循环。
**E5 `anim_card_frame_holo`** 基于 `card_frame_normal.png`（96×128，九宫格 9）：原图 → 框上斜向一道彩色流光（很淡，别盖住卡面）。循环。
**E6 `anim_card_frame_legend`** 基于 `card_frame_target.png`（96×128，九宫格 9）：3 帧金色流光自左下向右上扫过 + 四角星点强弱变化。循环。

### F 组 · 联机 / 天气氛围叠加层

**F1 `anim_net_linked`** 新建 **320×160 透明**：左右两个小圆环 → 中间连出一条光桥 → 光桥稳定 + 两端各闪一下。单次。
**F2 `anim_net_sync_check`** 新建 **96×96 透明**：一圈淡环 → 环内画出一道对勾的笔画（分两帧长出）→ 对勾发光。单次。
**F3 `anim_bg_stars_twinkle`** 新建 **1600×900**，只画星点：3 帧里星点亮度与位置略微变化（位移 10–20px）。循环。
**F4 `anim_bg_fog_drift`** 新建 **1600×900**，薄雾（整体不透明度 ≤25%，中间更透）。3 帧缓慢横移。循环。
**F5 `anim_bg_fireflies`** 新建 **1600×900**，暖黄萤火 12–18 个点，3 帧各自漂移。循环。
**F6 `anim_bg_rain`** 新建 **1600×900**，细雨斜线（不透明度 ≤35%）。3 帧向下错位。循环。
**F7 `anim_loading_mask_breath`** 基于 `loading_mask.png`（64×64）：原图 → 不透明度 60%（呼吸）。循环 2 帧。

---

## 二、交付格式（和前两批完全一致，我能自动接管）

```
Assets/Resources/Art/UI/V07/Anim/<动画名>/<动画名>_00.png
Assets/Resources/Art/UI/V07/Anim/<动画名>/<动画名>_01.png
...
```
- 也支持图集：`Assets/Resources/Art/UI/V07/Anim/<动画名>_sheet3.png`（横排 3 格），或 `_sheet3x2`（3 列 2 行）。
- **同一套动画内，逐帧尺寸必须完全一致**（前两批 47/47 套都做到了，继续保持；有 4 帧的就给 `_00`–`_03`）。
- 透明底、四角 alpha = 0（背景叠加层 A12/A19/A20/F3–F6 例外：按要求"中间透明、四周有内容"）。
- **不要在图里画任何文字、数字、图标里的字**——文字全部由代码绘制。
- 九宫格那几套（A10、B2、B3、B4、B7、B8、C4、D1、D2、D8、D9、E4、E5、E6）请在交付说明里再写一次四边像素值，我按它切片。

---

## 三、优先级与分批建议

| 批次 | 内容 | 套数 | 帧数 |
|---|---|---:|---:|
| **P0** | A 组全部（战斗演出） | 20 | 57 |
| **P1** | B 组 + C 组（大厅控件 / 图鉴组卡） | 17 | 45 |
| **P2** | D 组 + E 组 + F 组（商店签到交换 / 资料卡框 / 联机氛围） | 24 | 62 |
| 合计 | 61 套 | 61 | 164 |

如果想让"实机一眼看出变化"，**P0 里的 A1/A4/A5/A6/A12/A14/A19/A20 这 8 套最划算**（部署、出击、格挡、阵亡、压力警告、进化爆发、胜利星屑、败北暗角）。

---

## 四、已经交付、不用再画的 47 套（防重复）

**第 1 批（27 套）**：`anim_entry_battle / shop / checkin / codex / profile`、`anim_page_wipe`、
`anim_checkin_ready`、`anim_checkin_stamp`、`anim_banner_victory / defeat / draw`、`anim_coin`、`anim_gem`、
`anim_reward_open`、`anim_pack_open_starter / beast / girl`、`anim_seal_press`、`anim_envelope_open`、
`anim_rarity_1 / 2 / 3 / 4`、`anim_levelup`、`anim_soldout`、`anim_arrows_flow`、`anim_locked_shake`

**第 2 批（20 套）**：`anim_card_deal`、`anim_card_flip`、`anim_turn_banner`、`anim_flag_flash`、
`anim_slot_ring_evolve`、`anim_slot_ring_fuse`、`anim_keyword_ping`、`anim_shelf_hover`、`anim_refresh_wipe`、
`anim_save_confirm`、`anim_toast_wipe`、`anim_avatar_hover`、`anim_winrate_glow`、`anim_loading_spin`、
`anim_coin_fly`、`anim_trade_sent`、`anim_cardback_glow`、`anim_bg_dust`、`anim_bg_embers`、`anim_rarity_glow_5`

> 注：`anim_soldout`（第 1 批）和本批 D4 `anim_soldout_stamp` 是两个不同用途（前者是货位整体售罄，后者是盖章瞬间），
> 不冲突；如果你觉得重复，可以只画 D4，跳过 D4 我会自动回退用 `anim_soldout`。

---

## 五、还有一件事（不在本批，先提一下）

之前那份「攻击特效」需求（`界桥远征_V0.7_需要GPT处理的卡面清单.txt` 里列出的 16 套攻击特效 / 102 帧）
目前游戏里只有在用 V0.6 的老特效（`hit_00–05`、`guard_00–05` 各 6 帧）。那 16 套一旦交付，战斗打击感会立刻上一个档。
如果这批动画画累了，可以先切过去画那 16 套，我这边都是同一个导入流程，随时接。
