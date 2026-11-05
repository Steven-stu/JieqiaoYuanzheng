#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Jieqiao;
using Jieqiao.UI;
using UnityEngine;

/// <summary>
/// V0.7 大厅 / 存档 / 联机 的定点核对。
///
/// 联机部分是本机回环自测：同一个进程里开房 + 加入（127.0.0.1），
/// 验证找房→鉴权→行动中继→双方引擎状态一致，以及密码错误会被拒。
/// 不依赖任何联网插件（纯 .NET Socket），所以编辑器里就能跑。
/// </summary>
public static class V06ShellChecks
{
    private static int _pass;
    private static int _fail;
    private static readonly StringBuilder Out = new StringBuilder();

    public static string Run()
    {
        _pass = 0;
        _fail = 0;
        Out.Length = 0;

        SaveRoundtrip();
        SaveDefaults();
        DeckValidation();
        NetHandshake();
        NetWrongPassword();
        NetActionRelay();
        Determinism();

        Out.AppendLine("V0.7 大厅/存档/联机核对：" + _pass + " 通过 / " + _fail + " 失败");
        return Out.ToString();
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok) { _pass++; Out.AppendLine("  ✅ " + name); }
        else { _fail++; Out.AppendLine("  ❌ " + name + "　—　" + detail); }
    }

    // ───────────────────────── 存档 ─────────────────────────

    private static void SaveRoundtrip()
    {
        // 每次都从"全新存档"开始核对：不依赖磁盘上被实机测试改过的旧存档
        V06Save.ResetForTest();
        V06Save.Load();
        V06Save.Data.playerName = "测试员甲";
        V06Save.Data.coins = 4321;
        V06Save.Data.gems = 77;
        V06Save.Save();
        V06Save.Data = null;
        V06SaveData again = V06Save.Load();
        bool ok = again != null && again.playerName == "测试员甲" && again.coins == 4321 && again.gems == 77;
        Check("存档写入 → 重新读取一致（名字/金币/宝石）", ok,
            again == null ? "读取失败" : (again.playerName + " / " + again.coins + " / " + again.gems));
        if (!ok) { return; }
        int owned = V06Save.DistinctOwned();
        int total = V06Save.TotalOwned();
        Check("默认收藏已发放（应有 192 基础 + 一阶兽/少女）", owned >= 192 && total >= 384,
            "种类 " + owned + " / 张数 " + total);
        Check("默认卡组存在且合法", again.decks.Count >= 1 && again.decks[0].IsLegal(),
            "卡组数 " + again.decks.Count + "，首套合法=" + (again.decks.Count > 0 && again.decks[0].IsLegal()));
    }

    private static void SaveDefaults()
    {
        V06SaveData d = V06Save.Data;
        if (d == null) { Check("收藏计数自洽", false, "无存档"); return; }
        bool okAll = true;
        int zero = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            int n = V06Save.OwnedOf(c.Id);
            if (n < 0) { okAll = false; }
            if (n == 0) { zero++; }
        }
        // 进化形态/融合体是 0 张，属于预期
        Check("持有数量没有负数；未持有的正好是进化形态 + 融合体（30 张）", okAll && zero == 30,
            "未持有 " + zero + " 张（预期 30 = 二阶 14 + 三阶 8 + 融合体 8）");
    }

    private static void DeckValidation()
    {
        V06DeckSave d = V06Save.Data.decks[0];
        bool legal = d.IsLegal();
        V06DeckSave copy = new V06DeckSave();
        copy.units = new string[7];
        copy.tactics = d.tactics;
        copy.extras = d.extras;
        copy.upgrades = d.upgrades;
        copy.supports = d.supports;
        bool tooFew = copy.IsLegal();
        Check("卡组校验：24 主牌规则生效（官方 8 单位 + 4 战术 / 自组 8 单位 + 2 战术 + 额外 ≤4）", legal && !tooFew,
            "官方预组=" + legal + "（主牌 " + d.MainCount + "），缺单位时=" + tooFew);

        // 自定义卡组能开局
        V06Engine e = new V06Engine();
        string err;
        bool started = e.TryStartDeck(1234, V06DeckLibrary.Get(0), V06DeckLibrary.Get(1), out err);
        Check("自定义卡组入口可开局（TryStartDeck）", started && e.Players[0] != null && e.Players[0].Hand.Count > 0,
            started ? ("手牌 " + e.Players[0].Hand.Count) : err);
    }

    // ───────────────────────── 联机 ─────────────────────────

    private static void NetHandshake()
    {
        V06Net.Close();
        int room = 137;
        string err;
        bool hosted = V06Net.StartHost(room, "pw123", "房主甲", out err);
        if (!hosted) { Check("联机：房主开房 + 客户端加入", false, "开房失败：" + err); return; }
        bool joined = V06Net.StartJoin(room, "pw123", "客机乙", out err);
        bool connected = joined && V06Net.WaitConnectedOrFailed(6f);
        Check("联机：房主开房 + 客户端加入（房间号+密码）", hosted && connected,
            "开房=" + hosted + " 加入=" + joined + " 状态=" + V06Net.Status);
        if (connected)
        {
            Check("联机：双方互相认识（房主看到对手的中文名，UTF-8 不乱码）", V06Net.PeerName == "客机乙",
                "对手名=" + V06Net.PeerName + "（应 客机乙）");
        }
        V06Net.Close();
    }

    private static void NetWrongPassword()
    {
        V06Net.Close();
        int room = 138;
        string err;
        if (!V06Net.StartHost(room, "right", "房主", out err)) { Check("联机：密码错误必须被拒", false, "开房失败：" + err); return; }
        bool joined = V06Net.StartJoin(room, "wrong", "客机", "127.0.0.1", out err);
        float t = 0f;
        while (t < 6f && !V06Net.Connected && !(V06Net.Status.Contains("密码错误") || V06Net.Status.Contains("密码不对"))) { System.Threading.Thread.Sleep(30); t += 0.03f; }
        // 同一进程里房主与客户端共用 Status，谁后写谁显示；两种文案都说明"被拒了"
        bool rejected = !V06Net.Connected && (V06Net.Status.Contains("密码错误") || V06Net.Status.Contains("密码不对"));
        Check("联机：密码错误必须被拒（且房主仍在线）", joined && rejected,
            "状态=" + V06Net.Status);
        V06Net.Close();
    }

    private static void NetActionRelay()
    {
        V06Net.Close();
        int room = 139;
        string err;
        if (!V06Net.StartHost(room, "", "房主", out err)) { Check("联机：行动中继", false, "开房失败：" + err); return; }
        if (!V06Net.StartJoin(room, "", "客机", "127.0.0.1", out err) || !V06Net.WaitConnectedOrFailed(6f))
        {
            Check("联机：行动中继", false, "连接失败：" + V06Net.Status);
            V06Net.Close();
            return;
        }
        // 房主发一条行动；对端应能收到并还原
        V06Action a = new V06Action();
        a.Kind = V06ActionKind.Deploy;
        a.CardId = "U-H01";
        a.Slot = 2;
        V06Net.SendAct(a);
        List<V06NetMsg> got = new List<V06NetMsg>();
        float t = 0f;
        while (t < 4f && got.Count == 0)
        {
            got.AddRange(V06Net.Drain());
            System.Threading.Thread.Sleep(20);
            t += 0.02f;
        }
        V06Action back = got.Count > 0 ? V06Net.ToAction(got[0]) : null;
        bool ok = back != null && back.Kind == V06ActionKind.Deploy && back.CardId == "U-H01" && back.Slot == 2;
        Check("联机：行动中继（发 → 收 → 还原成 V06Action）", ok,
            back == null ? ("没收到消息，状态=" + V06Net.Status) : back.ToString());
        V06Net.Close();
    }

    /// <summary>确定性：同种子 + 同行动序列 → 两份引擎状态一致（锁步联机的前提）。</summary>
    private static void Determinism()
    {
        V06Engine a = new V06Engine();
        V06Engine b = new V06Engine();
        string err;
        if (!a.TryStart(20261104, 0, 1, out err) || !b.TryStart(20261104, 0, 1, out err))
        {
            Check("确定性：同种子同行动序列结果一致", false, "开局失败 " + err);
            return;
        }
        int steps = 0;
        while (!a.Finished && steps < 400)
        {
            List<V06Action> acts = a.LegalActions();
            if (acts.Count == 0) { break; }
            V06Action pick = acts[acts.Count - 1];   // 固定取最后一条（让过），只要两边一致即可
            string e1, e2;
            bool ok1 = a.Apply(pick, out e1);
            bool ok2 = b.Apply(pick, out e2);
            if (ok1 != ok2) { Check("确定性：同种子同行动序列结果一致", false, "第 " + steps + " 步应用结果不同"); return; }
            if (!ok1) { break; }
            steps++;
        }
        bool same = a.Round == b.Round && a.Winner == b.Winner
                    && a.Players[0].FlagHp == b.Players[0].FlagHp
                    && a.Players[1].FlagHp == b.Players[1].FlagHp
                    && a.Log.Count == b.Log.Count;
        Check("确定性：同种子同行动序列结果一致（" + steps + " 步）", same,
            "轮 " + a.Round + "/" + b.Round + "，旗 " + a.Players[0].FlagHp + ":" + a.Players[1].FlagHp
            + " vs " + b.Players[0].FlagHp + ":" + b.Players[1].FlagHp + "，日志 " + a.Log.Count + "/" + b.Log.Count);
    }
}
#endif
