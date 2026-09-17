using System;
using System.Collections.Generic;
using NSOKHODO.Client;
using NSOKHODO.Config;
using NSOKHODO.Protocol;

namespace NSOKHODO.Fleet
{
    /// <summary>
    /// Cong GIOI HAN LOGIN THEO MAY CHU: moi IP may chu game chi cho toi da
    /// <see cref="MaxPerGroup"/> account dang trong pha dang nhap cung luc; con lai xep hang cho.
    ///
    /// ==================== VI SAO LA MAY CHU CHU KHONG PHAI PROXY ====================
    /// Ban dau (2026-09-12 sang) cong nay khoa theo "host proxy + IP may chu". User do duoc mot su
    /// that dat hon vao buoi chieu: <i>"1 IP + gioi han 4 thi khong sao; gan them proxy thi lai
    /// khong vao duoc"</i>. Ly do: khi MOI acc chung mot IP thi khoa nhom gom tat ca vao MOT nhom
    /// => cong hoat dong nhu mot TRAN TONG = 4. Gan 300 proxy vao thi cung con so 4 do tach thanh
    /// 300 nhom => toi 1.200 acc login cung luc - tran bien mat dung luc can nhat.
    ///
    /// Thu that su khan hiem la SO ACC DAP VAO MOT MAY CHU CUNG LUC, khong phai proxy. Bang chung
    /// phu: log 2026-09-06 (chep trong <see cref="ReloginGate"/>) do duoc 76-92% so lan dut la
    /// server TU CHOI NGAY O BAT TAY - dau van tay cua "don qua nhieu ket noi mot luc".
    /// Con mot duong khuech dai nam ngay trong code ta: acc nao bat dau DataSync TRUOC khi acc dau
    /// tien kip cong bo <c>SharedGameData</c> thi tu tai ~200 KB ban rieng => 300 acc login cung
    /// luc = ~60 MB don mot cuc (xem <c>NsoClient.OnDataVersionReceived</c>).
    ///
    /// KHOA NHOM = <b>IP may chu game</b>, BO PORT: <c>Data/servers.txt</c> co nhieu server dung
    /// chung mot IP:port va chi khac byte serverLogin (Fukiya/Sanzu/Tone = 112.213.94.205;
    /// Shuriken/Tessen = 27.0.14.73) - gom theo IP moi dung nghia "dung don vao MOT may chu".
    /// Chay ca ham tren mot server => dung mot bo dem, tuc la mot tran tong.
    ///
    /// PROXY KHONG CON LA MOT CHIEU DO NAO (user chot 2026-09-12: "bo han").
    ///
    /// ==================== DEM THEO TUNG TIEN TRINH ====================
    /// Day la lop <c>static</c> nen bo dem song trong MOT tien trinh. Chay 3 tab = 3 bo dem doc
    /// lap = tran that su gap 3. User chot chap nhan (nhan tren giao dien ghi ro "moi tab");
    /// muon con so dung nghia tuyet doi thi phai dem qua file chung - de lai cho sau.
    ///
    /// MAC DINH TAT: tat -> <see cref="TryEnter"/> luon tra true, khong doi mot ti hanh vi nao.
    /// </summary>
    public static class LoginGate
    {
        /// <summary>So acc duoc login cung luc vao MOT may chu (user chot: 4).</summary>
        public const int DEFAULT_MAX = 4;

        /// <summary>Bat/tat toan cuc. Mac dinh TAT.</summary>
        public static bool Enabled;

        /// <summary>So acc login cung luc toi da tren mot may chu.</summary>
        public static int MaxPerGroup = DEFAULT_MAX;

        /// <summary>Hai lan cap slot trong CUNG mot nhom phai cach nhau it nhat bay nhieu.</summary>
        private const int MIN_GAP_MS = 1000;

        /// <summary>
        /// Slot tu het han sau khoang nay - luoi an toan: neu mot cho nao do quen goi
        /// <see cref="Exit"/> (nem ngoai le la, luong bi giet) thi slot khong duoc phep giam ca
        /// nhom vinh vien. Nguong phai LON HON thoi gian thu toi da: 10s mo TCP + toi 120s bat tay
        /// proxy (khi ProxyHandshakeSec=0) + ~65s cac buoc WaitFor cua DoFullLogin ≈ 195s.
        /// </summary>
        private static readonly TimeSpan SLOT_TTL = TimeSpan.FromSeconds(240);

        /// <summary>
        /// Nguoi xep hang ma qua lau khong hoi lai thi coi nhu da bo cuoc (bi Stop, Dispose...).
        /// BAT BUOC phai co: thu tu cap slot la FIFO, mot ke da chet ma con dung dau hang se chan
        /// CA NHOM vinh vien. Nhip hoi lai ~1 giay nen 15 giay la thua rong rai.
        /// </summary>
        private static readonly TimeSpan WAITER_TTL = TimeSpan.FromSeconds(15);

        private class Waiter
        {
            public DateTime LastAsk;
            public bool Bo;        // da duoc cap slot / da roi hang -> bo qua khi gap trong hang doi
        }

        /// <summary>
        /// Trang thai cua MOT nhom (mot IP may chu).
        ///
        /// <para><b>Vi sao co hang doi rieng tung nhom thay vi quet ca so:</b> ban dau moi lan xin
        /// slot phai duyet TOAN BO bang nguoi cho de tim ai doi lau nhat. Voi 600 acc cung xep hang
        /// va moi acc hoi lai 1 lan/giay thi do la ~360.000 vong lap moi giay duoi CUNG mot khoa -
        /// tra gia CPU dung luc ca ham dang chay chat vat nhat. Nay dung hang doi FIFO: nguoi den
        /// truoc dung dau, moi lan xin chi nhin DAU hang -> O(1).</para>
        /// </summary>
        private class Group
        {
            public int InUse;
            public DateTime LastGrantUtc;
            public readonly Queue<object> Order = new Queue<object>();
            public readonly Dictionary<object, Waiter> Waiters = new Dictionary<object, Waiter>();
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Group> _groups =
            new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Ai dang giu slot cua nhom nao + het han luc nao.</summary>
        private static readonly Dictionary<object, KeyValuePair<string, DateTime>> _holders =
            new Dictionary<object, KeyValuePair<string, DateTime>>();

        // ==================== KHOA NHOM ====================

        /// <summary>
        /// Khoa nhom cua mot account = <b>IP may chu game</b>. Resolve y het luc login
        /// (<c>ServerList.Resolve(ServerName, ServerIndex)</c>) de hai cho khong bao gio lech nhau;
        /// khong resolve duoc thi dung "(?)" - van gom duoc, chi la gom vao mot nhom rieng.
        /// </summary>
        public static string GroupKey(AccountConfig cfg)
        {
            if (cfg == null) return "(?)";
            try
            {
                var sv = ServerList.Resolve(cfg.ServerName, cfg.ServerIndex);
                if (sv != null && !string.IsNullOrEmpty(sv.Host)) return sv.Host;
            }
            catch { }
            return "(?)";
        }

        // ==================== CONG ====================

        /// <summary>
        /// Xin mot slot cho nhom <paramref name="key"/>. false = chua toi luot, cu goi lai sau ~1s.
        /// Goi lai nhieu lan la an toan: da giu slot roi thi tra true ngay.
        /// </summary>
        public static bool TryEnter(object who, string key)
        {
            if (!Enabled || who == null) return true;
            if (string.IsNullOrEmpty(key)) key = "(?)";

            var now = DateTime.UtcNow;
            lock (_lock)
            {
                PurgeHolders(now);

                KeyValuePair<string, DateTime> cur;
                if (_holders.TryGetValue(who, out cur))
                {
                    // Da giu slot. Doi server giua chung thi tra slot cu roi xin lai o nhom moi.
                    if (string.Equals(cur.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        _holders[who] = new KeyValuePair<string, DateTime>(key, now + SLOT_TTL);
                        return true;
                    }
                    NhaSlot(who, cur.Key);
                }

                Group g;
                if (!_groups.TryGetValue(key, out g)) { g = new Group(); _groups[key] = g; }

                // Ghi nhan dang xep hang (lan dau thi vao cuoi hang).
                Waiter w;
                if (!g.Waiters.TryGetValue(who, out w))
                {
                    w = new Waiter();
                    g.Waiters[who] = w;
                    g.Order.Enqueue(who);
                }
                w.LastAsk = now;
                w.Bo = false;

                int max = MaxPerGroup > 0 ? MaxPerGroup : DEFAULT_MAX;
                if (g.InUse >= max) return false;
                if ((now - g.LastGrantUtc).TotalMilliseconds < MIN_GAP_MS) return false;

                // FIFO: bo cac dau hang da chet/da di roi, rot cuoc phai la CHINH MINH dung dau.
                DonDauHang(g, now);
                if (g.Order.Count == 0 || !ReferenceEquals(g.Order.Peek(), who)) return false;

                g.Order.Dequeue();
                g.Waiters.Remove(who);
                g.InUse++;
                g.LastGrantUtc = now;
                _holders[who] = new KeyValuePair<string, DateTime>(key, now + SLOT_TTL);
                return true;
            }
        }

        /// <summary>
        /// Tra slot + roi khoi hang cho. An toan khi goi nhieu lan hoac khi chua he xin.
        /// Goi khi pha login KET THUC (du thanh hay bai) va khi account bi Stop/Dispose.
        /// </summary>
        public static void Exit(object who)
        {
            if (who == null) return;
            lock (_lock)
            {
                KeyValuePair<string, DateTime> cur;
                if (_holders.TryGetValue(who, out cur)) NhaSlot(who, cur.Key);

                // Con dang xep hang o dau do thi danh dau bo - hang doi tu don o lan xin sau.
                foreach (var kv in _groups)
                {
                    Waiter w;
                    if (kv.Value.Waiters.TryGetValue(who, out w)) { w.Bo = true; kv.Value.Waiters.Remove(who); }
                }
            }
        }

        /// <summary>So acc dang trong pha login cua mot nhom (cho log/UI).</summary>
        public static int InUse(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            lock (_lock)
            {
                PurgeHolders(DateTime.UtcNow);
                Group g;
                return _groups.TryGetValue(key, out g) ? g.InUse : 0;
            }
        }

        /// <summary>So acc dang xep hang cua mot nhom (cho log/UI).</summary>
        public static int Waiting(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            lock (_lock)
            {
                Group g;
                return _groups.TryGetValue(key, out g) ? g.Waiters.Count : 0;
            }
        }

        // ==================== NOI BO (GOI TRONG lock) ====================

        private static void NhaSlot(object who, string key)
        {
            _holders.Remove(who);
            Group g;
            if (_groups.TryGetValue(key, out g) && g.InUse > 0) g.InUse--;
        }

        /// <summary>Slot qua han (chu giu quen tra) - luoi an toan cuoi cung.</summary>
        private static void PurgeHolders(DateTime now)
        {
            if (_holders.Count == 0) return;
            List<object> het = null;
            foreach (var kv in _holders)
                if (now >= kv.Value.Value) (het ?? (het = new List<object>())).Add(kv.Key);
            if (het == null) return;
            foreach (var k in het)
            {
                KeyValuePair<string, DateTime> cur;
                if (_holders.TryGetValue(k, out cur)) NhaSlot(k, cur.Key);
            }
        }

        /// <summary>Bo dau hang: nguoi da roi hang, hoac qua lau khong hoi lai (coi nhu bo cuoc).</summary>
        private static void DonDauHang(Group g, DateTime now)
        {
            while (g.Order.Count > 0)
            {
                object head = g.Order.Peek();
                Waiter w;
                if (!g.Waiters.TryGetValue(head, out w) || w.Bo || now - w.LastAsk > WAITER_TTL)
                {
                    g.Order.Dequeue();
                    g.Waiters.Remove(head);
                    continue;
                }
                break;
            }
        }

        // ==================== LUU / NAP ====================
        // GIU NGUYEN ten key cu (IpLoginLimit / IpLoginLimitCount) du cong da doi nghia: user dang
        // co san con so trong Data/settings.txt, doi ten key la con so do bien mat khong bao gi.

        public static void Load()
        {
            var map = SettingsStore.LoadAll();
            Enabled = SettingsStore.GetBool(map, "IpLoginLimit", false);
            int n = SettingsStore.GetInt(map, "IpLoginLimitCount", DEFAULT_MAX);
            if (n >= 1 && n <= 100) MaxPerGroup = n;
        }

        public static void Save()
        {
            SettingsStore.Set(
                "IpLoginLimit", Enabled ? "1" : "0",
                "IpLoginLimitCount", MaxPerGroup.ToString());
        }
    }
}
