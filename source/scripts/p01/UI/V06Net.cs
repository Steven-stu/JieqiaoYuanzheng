using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Jieqiao.UI
{
    /// <summary>联机消息（一条一行 JSON，字段够用就好）。</summary>
    [Serializable]
    public class V06NetMsg
    {
        public string t;            // discover / here / auth / authok / autherr / deck / start / act / bye / chat
        public string pw;
        public string name;
        public string payload;      // 通用字符串（卡组 JSON / 提示）
        public string payload2;     // 第二个字符串（guest 卡组 JSON）
        public int room;
        public int seed;
        public int camp;            // 该玩家坐在哪一方（0/1）
        // 行动字段
        public string act;          // ActionKind 名字
        public string cardId;
        public int slot = -1;
        public int uid = -1;
        public int targetUid = -1;
        public int targetSlot = -1;
        public bool power;
        public bool toFlag;
        public bool fromReserve;
    }

    /// <summary>
    /// 局域网 / 本机联机（房间号 + 密码）。
    ///
    /// 做法：TCP 直连 + UDP 广播找房主，纯 .NET Socket，不依赖任何联网插件。
    ///   · 端口 = 27500 + 房间号（房间号 1–999）
    ///   · 找房：客户端向 27600 广播 {"t":"discover","room":N}，房主回 {"t":"here","room":N}
    ///   · 联机模型是"锁步 + 房主授权"：双方各自跑同一套引擎，只交换行动指令，
    ///     因为引擎是确定性的（同种子同行动 → 同结果），所以两边状态一致。
    /// </summary>
    public static class V06Net
    {
        public const int TcpBasePort = 27500;
        public const int UdpDiscoverPort = 27600;
        public const int MinRoom = 1;
        public const int MaxRoom = 999;

        public enum Role { None, Host, Client }

        public static Role MyRole = Role.None;
        public static bool Connected;
        public static int Room;
        public static string MyName = "远征者";
        public static string PeerName = "对手";
        public static string Status = "未连接";
        public static string LocalIp = "";
        public static bool MatchStarted;
        public static int MyCamp = 0;

        public static int PortOf(int room) { return TcpBasePort + room; }

        private static TcpListener _listener;
        private static TcpClient _peer;
        private static NetworkStream _stream;
        private static Thread _acceptThread;
        private static Thread _readThread;
        private static Thread _writeThread;
        private static Thread _discoverThread;
        private static UdpClient _udp;
        private static volatile bool _running;
        private static readonly ConcurrentQueue<string> Inbound = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<string> Outbound = new ConcurrentQueue<string>();

        // ───────────────────────── 对外接口 ─────────────────────────

        /// <summary>房主：开一个房间并开始监听。返回是否成功。</summary>
        public static bool StartHost(int room, string password, string playerName, out string error)
        {
            error = "";
            if (!ValidRoom(room, ref error)) { return false; }
            Close();
            MyRole = Role.Host;
            Room = room;
            MyName = string.IsNullOrEmpty(playerName) ? "远征者" : playerName;
            PeerName = "对手";
            MatchStarted = false;
            MyCamp = 0;
            string expectedPw = password == null ? "" : password;
            try
            {
                _listener = new TcpListener(IPAddress.Any, PortOf(room));
                _listener.Start();
            }
            catch (Exception ex)
            {
                MyRole = Role.None;
                error = "开房失败（端口被占？）：" + ex.Message;
                Status = error;
                return false;
            }
            _running = true;
            StartDiscoveryResponder(room);
            LocalIp = LocalIpBest();
            Status = "房间 " + room + " 已开启，等待对手加入…（本机 IP " + LocalIp + "）";
            _acceptThread = new Thread(() => AcceptLoop(expectedPw));
            _acceptThread.IsBackground = true;
            _acceptThread.Start();
            return true;
        }

        /// <summary>客户端：按房间号找房主并发起连接（异步，用 Status 看进度）。
        /// hostIpOverride 非空时跳过广播、直连该地址（跨网段/虚拟局域网/VPN 用）。</summary>
        public static bool StartJoin(int room, string password, string playerName, out string error)
        {
            return StartJoin(room, password, playerName, null, out error);
        }

        public static bool StartJoin(int room, string password, string playerName, string hostIpOverride, out string error)
        {
            error = "";
            if (!ValidRoom(room, ref error)) { return false; }
            CloseClientSide();
            MyRole = Role.Client;
            Room = room;
            MyName = string.IsNullOrEmpty(playerName) ? "远征者" : playerName;
            MatchStarted = false;
            MyCamp = 1;
            _running = true;
            Status = "正在搜索房间 " + room + "…";
            string pw = password == null ? "" : password;
            string ip = hostIpOverride == null ? "" : hostIpOverride.Trim();
            _discoverThread = new Thread(() => DiscoverAndConnect(room, pw, ip));
            _discoverThread.IsBackground = true;
            _discoverThread.Start();
            return true;
        }

        public static void Close()
        {
            _running = false;
            try { if (_udp != null) { _udp.Close(); } } catch (Exception) { }
            _udp = null;
            try { if (_listener != null) { _listener.Stop(); } } catch (Exception) { }
            _listener = null;
            CloseClientSide();
            MyRole = Role.None;
            Status = "未连接";
        }

        /// <summary>只清客户端侧（连接/收发线程与队列），不动监听与找房广播。
        /// 这样同一个进程里就能既当房主又当客户端，方便回环自测。</summary>
        private static void CloseClientSide()
        {
            try { if (_stream != null) { _stream.Close(); } } catch (Exception) { }
            _stream = null;
            try { if (_peer != null) { _peer.Close(); } } catch (Exception) { }
            _peer = null;
            Connected = false;
            MatchStarted = false;
            string tmp;
            while (Inbound.TryDequeue(out tmp)) { }
            while (Outbound.TryDequeue(out tmp)) { }
        }

        /// <summary>取走所有收到的消息（主线程调用）。</summary>
        public static List<V06NetMsg> Drain()
        {
            List<V06NetMsg> list = new List<V06NetMsg>();
            string line;
            while (Inbound.TryDequeue(out line))
            {
                if (string.IsNullOrEmpty(line)) { continue; }
                V06NetMsg m = null;
                try { m = JsonUtility.FromJson<V06NetMsg>(line); }
                catch (Exception ex) { Debug.LogWarning("[V06Net] 消息解析失败：" + ex.Message); }
                if (m != null && !string.IsNullOrEmpty(m.t)) { list.Add(m); }
            }
            return list;
        }

        public static void Send(V06NetMsg m)
        {
            if (m == null) { return; }
            string json;
            try { json = JsonUtility.ToJson(m); }
            catch (Exception ex) { Debug.LogWarning("[V06Net] 序列化失败：" + ex.Message); return; }
            Outbound.Enqueue(json);
        }

        /// <summary>把一次行动发给对手。</summary>
        public static void SendAct(V06Action a)
        {
            if (a == null || !Connected) { return; }
            V06NetMsg m = new V06NetMsg();
            m.t = "act";
            m.act = a.Kind.ToString();
            m.cardId = a.CardId;
            m.slot = a.Slot;
            m.uid = a.Uid;
            m.targetUid = a.TargetUid;
            m.targetSlot = a.TargetSlot;
            m.power = a.PowerStrike;
            m.toFlag = a.ToFlag;
            m.fromReserve = a.FromReserve;
            Send(m);
        }

        /// <summary>把收到的 act 消息还原成行动。</summary>
        public static V06Action ToAction(V06NetMsg m)
        {
            if (m == null || m.t != "act") { return null; }
            V06Action a = new V06Action();
            try { a.Kind = (V06ActionKind)Enum.Parse(typeof(V06ActionKind), m.act); }
            catch (Exception) { return null; }
            a.CardId = m.cardId;
            a.Slot = m.slot;
            a.Uid = m.uid;
            a.TargetUid = m.targetUid;
            a.TargetSlot = m.targetSlot;
            a.PowerStrike = m.power;
            a.ToFlag = m.toFlag;
            a.FromReserve = m.fromReserve;
            return a;
        }

        /// <summary>测试/工具：等连接态变化（阻塞，仅编辑器自测用）。</summary>
        public static bool WaitConnectedOrFailed(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (Connected) { return true; }
                if (!string.IsNullOrEmpty(Status) && Status.StartsWith("连接失败")) { return false; }
                Thread.Sleep(20);
                t += 0.02f;
            }
            return Connected;
        }

        // ───────────────────────── 内部：房主 ─────────────────────────

        private static bool ValidRoom(int room, ref string error)
        {
            if (room < MinRoom || room > MaxRoom)
            {
                error = "房间号要在 " + MinRoom + "–" + MaxRoom + " 之间";
                return false;
            }
            return true;
        }

        private static void StartDiscoveryResponder(int room)
        {
            try
            {
                _udp = new UdpClient();
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, UdpDiscoverPort));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[V06Net] 找房广播未开启（端口占用）：" + ex.Message);
                _udp = null;
                return;
            }
            Thread th = new Thread(() =>
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                while (_running && _udp != null)
                {
                    try
                    {
                        byte[] data = _udp.Receive(ref remote);
                        string text = Encoding.UTF8.GetString(data);
                        V06NetMsg m = JsonUtility.FromJson<V06NetMsg>(text);
                        if (m == null || m.t != "discover" || m.room != room) { continue; }
                        V06NetMsg reply = new V06NetMsg();
                        reply.t = "here";
                        reply.room = room;
                        byte[] outData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(reply));
                        _udp.Send(outData, outData.Length, remote);
                    }
                    catch (Exception)
                    {
                        if (!_running) { break; }
                        Thread.Sleep(30);
                    }
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private static void AcceptLoop(string expectedPw)
        {
            try
            {
                while (_running)
                {
                    TcpClient c = _listener.AcceptTcpClient();
                    c.NoDelay = true;
                    NetworkStream s = c.GetStream();
                    string line = ReadLine(s);
                    V06NetMsg hello = null;
                    try { hello = JsonUtility.FromJson<V06NetMsg>(line); } catch (Exception) { }
                    if (hello == null || hello.t != "auth")
                    {
                        SafeWrite(s, "{\"t\":\"autherr\"}");
                        c.Close();
                        continue;
                    }
                    if ((hello.pw ?? "") != expectedPw)
                    {
                        SafeWrite(s, "{\"t\":\"autherr\"}");
                        c.Close();
                        Status = "有人尝试加入但密码不对，房间仍然开着";
                        continue;
                    }
                    _peer = c;
                    _stream = s;
                    PeerName = string.IsNullOrEmpty(hello.name) ? "对手" : hello.name;
                    V06NetMsg ok = new V06NetMsg();
                    ok.t = "authok";
                    ok.name = MyName;
                    SafeWrite(s, JsonUtility.ToJson(ok));
                    Connected = true;
                    Status = PeerName + " 已加入房间 " + Room;
                    StartReader(s);
                    StartWriter(s);
                    return;
                }
            }
            catch (Exception ex)
            {
                if (_running) { Status = "接受连接出错：" + ex.Message; }
            }
        }

        // ───────────────────────── 内部：客户端 ─────────────────────────

        private static void DiscoverAndConnect(int room, string pw, string hostIpOverride)
        {
            string hostIp = null;
            try
            {
                if (!string.IsNullOrEmpty(hostIpOverride))
                {
                    hostIp = hostIpOverride;
                    Status = "直连房主 " + hostIp + "…";
                }
                else
                {
                    Status = "正在搜索房间 " + room + "…";
                    hostIp = Discover(room);
                }
                if (hostIp == null)
                {
                    // 同机自测兜底：没有广播回应时，试一下 127.0.0.1
                    Status = "没搜到广播回应，尝试本机直连…";
                    hostIp = "127.0.0.1";
                }
                Status = "连接房主 " + hostIp + " 的端口 " + PortOf(room) + "…";
                TcpClient c = new TcpClient();
                IAsyncResult ar = c.BeginConnect(hostIp, PortOf(room), null, null);
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
                {
                    Status = "连接失败：房主没有响应（超时）。如果不在同一网段，请让房主把 IP 告诉你、填到「房主 IP」里再试";
                    _running = false;
                    return;
                }
                c.EndConnect(ar);
                c.NoDelay = true;
                NetworkStream s = c.GetStream();
                V06NetMsg auth = new V06NetMsg();
                auth.t = "auth";
                auth.pw = pw;
                auth.name = MyName;
                auth.room = room;
                SafeWrite(s, JsonUtility.ToJson(auth));
                string reply = ReadLine(s);
                V06NetMsg rr = null;
                try { rr = JsonUtility.FromJson<V06NetMsg>(reply); } catch (Exception) { }
                if (rr == null || rr.t != "authok")
                {
                    Status = "连接失败：密码错误";
                    c.Close();
                    _running = false;
                    return;
                }
                _peer = c;
                _stream = s;
                if (!string.IsNullOrEmpty(rr.name)) { PeerName = rr.name; }
                Connected = true;
                Status = "已加入 " + PeerName + " 的房间 " + room;
                StartReader(s);
                StartWriter(s);
            }
            catch (Exception ex)
            {
                Status = "连接失败：" + ex.Message;
                _running = false;
            }
        }

        /// <summary>广播找房：每 0.4 秒发一次，最多 3 秒，命中就返回房主 IP。</summary>
        private static string Discover(int room)
        {
            UdpClient udp = null;
            try
            {
                udp = new UdpClient();
                udp.EnableBroadcast = true;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                V06NetMsg q = new V06NetMsg();
                q.t = "discover";
                q.room = room;
                byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(q));
                float waited = 0f;
                IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
                udp.Client.ReceiveTimeout = 400;
                while (waited < 3f)
                {
                    try
                    {
                        udp.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, UdpDiscoverPort));
                    }
                    catch (Exception) { }
                    try
                    {
                        udp.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, UdpDiscoverPort));
                    }
                    catch (Exception) { }
                    try
                    {
                        byte[] rx = udp.Receive(ref any);
                        V06NetMsg r = JsonUtility.FromJson<V06NetMsg>(Encoding.UTF8.GetString(rx));
                        if (r != null && r.t == "here" && r.room == room)
                        {
                            return any.Address.ToString();
                        }
                    }
                    catch (SocketException)
                    {
                        waited += 0.4f;
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                try { if (udp != null) { udp.Close(); } } catch (Exception) { }
            }
        }

        // ───────────────────────── 内部：读写线程 ─────────────────────────

        private static void StartReader(NetworkStream s)
        {
            _readThread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        string line = ReadLine(s);
                        if (line == null) { break; }
                        if (line.Length > 0) { Inbound.Enqueue(line); }
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
                if (_running)
                {
                    Connected = false;
                    Status = "与对手断开连接";
                }
            });
            _readThread.IsBackground = true;
            _readThread.Start();
        }

        private static void StartWriter(NetworkStream s)
        {
            _writeThread = new Thread(() =>
            {
                while (_running)
                {
                    string line;
                    if (!Outbound.TryDequeue(out line)) { Thread.Sleep(5); continue; }
                    try
                    {
                        byte[] data = Encoding.UTF8.GetBytes(line + "\n");
                        s.Write(data, 0, data.Length);
                        s.Flush();
                    }
                    catch (Exception)
                    {
                        Connected = false;
                        break;
                    }
                }
            });
            _writeThread.IsBackground = true;
            _writeThread.Start();
        }

        private static string ReadLine(NetworkStream s)
        {
            // 必须按 UTF-8 解码：以前一个字节一个字节转 char（等于 Latin-1），
            // 中文玩家名会变成乱码（"客机" → "å®¢æœº"）。
            var ms = new MemoryStream();
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) { break; }
                if (b == (int)'\n') { break; }
                ms.WriteByte((byte)b);
                if (ms.Length > 65536) { return null; }
            }
            if (ms.Length == 0) { return null; }
            return Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\r');
        }

        private static void SafeWrite(NetworkStream s, string text)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(text + "\n");
                s.Write(data, 0, data.Length);
                s.Flush();
            }
            catch (Exception) { }
        }

        /// <summary>取一个能给对方看的本机 IPv4（优先 192.168/10.x 之类私网地址）。</summary>
        public static string LocalIpBest()
        {
            try
            {
                IPAddress[] all = Dns.GetHostAddresses(Dns.GetHostName());
                string fallback = "";
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].AddressFamily != AddressFamily.InterNetwork) { continue; }
                    string s = all[i].ToString();
                    if (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("172.")) { return s; }
                    if (fallback.Length == 0 && !s.StartsWith("127.")) { fallback = s; }
                }
                return fallback.Length > 0 ? fallback : "127.0.0.1";
            }
            catch (Exception) { return "127.0.0.1"; }
        }
    }
}
