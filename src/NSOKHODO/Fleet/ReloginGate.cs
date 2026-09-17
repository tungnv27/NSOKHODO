using System;
using System.Collections.Generic;

namespace NSOKHODO.Fleet
{
    /// <summary>
    /// Cong dieu tiet LOGIN LAI dung chung cho ca fleet.
    ///
    /// VI SAO CAN. Khi server da hang loat (hoac mang chop mot cai), toan bo 45 acc cung roi vao
    /// trang thai Reconnecting va cung dem lui theo cung mot cong thuc backoff -> chung dap vao
    /// server GAN NHU CUNG MOT LUC. Do la kich ban da lam user phai noi:
    ///     "nhieu tai khoan vo lai 1 luc gay ra bi ban ip"
    /// Do tren log 2026-09-06 (truoc khi go vong leo thang): 76-92% tong so lan dut ket noi la
    /// server TU CHOI NGAY O BAT TAY (dong `Connection closed` cach `Connecting` <= 2 giay), 13-17
    /// acc ket trong vong `no key` + retry 60s, chuoi toi 29 lan lien tiep.
    ///
    /// CLONE TU DAU. ZangVPS khong dat cong nay trong bot ma trong LAUNCHER C#
    /// (`E:\zangvps\NGHIENCUU\_nguon\net_src\ZNinjaPro_exe\ZNinjaPro\Form1.cs:127-143`):
    ///
    ///     int num3 = 0;
    ///     for (int j = 0; j &lt; apps.Count; j++)
    ///         if (apps[j].isRun &amp;&amp; apps[j].isOK() &amp;&amp; apps[j].isReLogin)
    ///         {
    ///             if (militime() - apps[j].timeLogin &gt;= 1000) { ...send("ACTION:LOGIN"); }
    ///             num3++;
    ///             if (num3 &gt;= 5) break;          // TOI DA 5 ACC MOI LUOT
    ///         }
    ///
    /// Bot Zang chi bao "ACTION:RELOGIN" roi ngoi cho launcher goi ten. Ta chay TAT CA acc trong
    /// MOT tien trinh nen khong co launcher - cong nam ngay day, hieu qua tuong duong.
    ///
    /// KHAC BIET CO CHU DICH so voi Zang: Zang nha slot khi bot bao `STATUS:ONLINE`
    /// (`Form1.cs:2248-2254`). Ta nha slot khi pha login KET THUC, du thanh hay bai - login hong
    /// thi tra slot ngay cho acc khac dung, khong bat ca hang doi cho mot acc dang hap hoi.
    ///
    /// KHONG ap cho lan dang nhap DAU TIEN: FleetManager da co staggered start rieng.
    /// </summary>
    internal static class ReloginGate
    {
        /// <summary>
        /// Tran CU: 5 acc login lai cung luc (Zang: 5). Tu 2026-09-12 day chi con la gia tri dung
        /// khi cong <see cref="LoginGate"/> DANG TAT - xem <see cref="EffectiveMax"/>.
        /// </summary>
        public const int LEGACY_MAX = 5;

        /// <summary>Gian cach CU giua hai lan cap slot (Zang: 1000ms/acc). Xem <see cref="EffectiveGapMs"/>.</summary>
        public const int LEGACY_GAP_MS = 1000;

        /// <summary>
        /// Tran THAT SU dang ap. 0 = khong gioi han.
        ///
        /// <para><b>Vi sao doi (user chot 2026-09-12):</b> tran 5 la cai cua 5 cho cho CA 600 acc
        /// cua mot tien trinh. Mot acc login qua proxy treo giu cho toi 2 phut, nen chi 5 acc xui la
        /// ca tab dung im - dung trieu chung user gap ("tat di bat lai tung tab moi duoc"). Cong
        /// <see cref="LoginGate"/> ham dung cho can ham (4 acc / mot MAY CHU), nen khi no BAT thi
        /// tran toan tien trinh chi con la vat can.</para>
        ///
        /// <para><b>Luoi an toan:</b> cong IP TAT -> quay ve 5/1000ms nhu cu. Khong bao gio co
        /// trang thai "khong con cai gi ham ca" tren duong login lai.</para>
        /// </summary>
        public static int EffectiveMax
        {
            get
            {
                int v = Config.NetOptions.ReloginMax;
                if (v >= 0) return v;                                  // user chi dinh cung
                return LoginGate.Enabled ? 0 : LEGACY_MAX;           // tu dong
            }
        }

        /// <summary>Gian cach THAT SU dang ap (ms). 0 = khong gian cach. Xem <see cref="EffectiveMax"/>.</summary>
        public static int EffectiveGapMs
        {
            get
            {
                int v = Config.NetOptions.ReloginGapMs;
                if (v >= 0) return v;
                return LoginGate.Enabled ? 0 : LEGACY_GAP_MS;
            }
        }

        /// <summary>
        /// Slot tu het han sau khoang nay. Luoi an toan: neu mot cho nao do quen goi Exit (nem
        /// ngoai le la, luong bi giet), slot khong duoc phep giam ca fleet vinh vien.
        /// Nguong phai LON HON thoi gian thu toi da: 10s mo TCP + toi 120s bat tay proxy (khi
        /// ProxyHandshakeSec=0) + ~65s cac buoc WaitFor cua DoFullLogin ≈ 195s. De 120s nhu truoc
        /// la slot het han TRONG LUC login van dang chay -> cong cap qua so.
        /// </summary>
        private static readonly TimeSpan SLOT_TTL = TimeSpan.FromSeconds(240);

        private static readonly object _lock = new object();
        private static readonly Dictionary<object, DateTime> _holders = new Dictionary<object, DateTime>();
        private static readonly Random _rnd = new Random();
        private static DateTime _lastGrantUtc = DateTime.MinValue;

        /// <summary>Xin mot slot. false = hang doi day hoac chua toi luot, cu goi lai sau ~1 giay.</summary>
        public static bool TryEnter(object who)
        {
            if (who == null) return true;
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                // Quet slot qua han truoc khi dem.
                if (_holders.Count > 0)
                {
                    List<object> het = null;
                    foreach (var kv in _holders)
                        if (now >= kv.Value) (het ?? (het = new List<object>())).Add(kv.Key);
                    if (het != null) foreach (var k in het) _holders.Remove(k);
                }

                if (_holders.ContainsKey(who)) return true;           // da giu roi, cho qua

                int max = EffectiveMax;
                if (max > 0 && _holders.Count >= max) return false;

                int gap = EffectiveGapMs;
                if (gap > 0 && (now - _lastGrantUtc).TotalMilliseconds < gap) return false;

                _holders[who] = now + SLOT_TTL;
                _lastGrantUtc = now;
                return true;
            }
        }

        /// <summary>Tra slot. An toan khi goi nhieu lan hoac khi chua he xin.</summary>
        public static void Exit(object who)
        {
            if (who == null) return;
            lock (_lock) { _holders.Remove(who); }
        }

        /// <summary>So acc dang giu slot (cho log/UI).</summary>
        public static int InUse { get { lock (_lock) return _holders.Count; } }

        /// <summary>
        /// Do lech ngau nhien cong them vao thoi gian cho truoc khi login lai (ms).
        ///
        /// Muc dich: PHA DONG BO. Cong thuc backoff cua ta la tat dinh (5s,10s,15s,20s,25s,60s)
        /// nen ca fleet cung dut mot luc se cung day lai mot luc, ke ca khi con tran o tren van
        /// bi mot dot dai xep hang. GIU LAI ke ca khi da bo tran: day la thu re nhat va van co tac
        /// dung that - bo no thi 600 acc cung bat day trong mot khoanh khac. Zang giai bai nay bang `n = 5 + rand(10)` giay
        /// (`o_0.java:82`), cong them `55 + rand(30)` neu khong noi chuyen duoc voi launcher.
        ///
        /// Ta KHONG chep nguyen con so cua Zang: 5-14 giay cua no la TOAN BO thoi gian cho, con
        /// ta da co backoff lam nen. Cong them 5-14s nua chi lam acc day len cham hon ma khong
        /// them tac dung phan tan. Lay 0..5s la du trai deu 45 acc qua 5 nhip cap slot.
        /// </summary>
        public static int JitterMs()
        {
            lock (_lock) return _rnd.Next(0, 5000);
        }
    }
}
