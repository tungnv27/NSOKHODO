using System;
using System.Collections.Generic;
using NSOKHODO.Client;

namespace NSOKHODO.Fleet
{
    /// <summary>
    /// Bo dem SUC KHOE PROXY: gom ket qua mo ket noi cua moi account lai theo host proxy roi bao
    /// MOT dong gon, thay vi de user tu doc 600 dong "[Login] Failed" moi biet proxy nao chet.
    ///
    /// VI SAO CAN (user 2026-09-12): <i>"trước đó tôi 1ip thì vào được gắn thêm proxy thì lại
    /// không"</i>. Khi ca tram proxy chay cung luc, thu user thieu khong phai la log chi tiet hon
    /// ma la cau tra loi "proxy NAO dang hong va hong vi cai gi".
    ///
    /// KHONG tu tat proxy, KHONG tu doi cau hinh - chi bao. Quyet dinh bo proxy nao la cua user.
    /// </summary>
    public static class ProxyHealth
    {
        /// <summary>Bao it nhat bay nhieu lan that bai lien tiep moi keu (tranh keu vi mot cu chop mang).</summary>
        private const int FAIL_STREAK_REPORT = 3;

        /// <summary>Mot proxy khong keu lai qua som: hai lan bao cach nhau it nhat bay nhieu.</summary>
        private static readonly TimeSpan REPORT_GAP = TimeSpan.FromMinutes(2);

        /// <summary>Noi nhan dong bao (FleetManager gan = log gop cua ca ham).</summary>
        public static Action<string> OnLog;

        private class Stat
        {
            public int Ok, Fail, Streak;
            public string LastReason = "";
            public DateTime LastReportUtc;
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Stat> _map =
            new Dictionary<string, Stat>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Mo duoc duong ham qua proxy. Xoa chuoi that bai dang dem.</summary>
        public static void NoteOk(string proxyStored)
        {
            string host = HostOf(proxyStored);
            if (host == null) return;                       // khong dung proxy -> khong theo doi
            lock (_lock)
            {
                var st = Get(host);
                st.Ok++;
                st.Streak = 0;
            }
        }

        /// <summary>
        /// Mo ket noi qua proxy THAT BAI. Tra ve dong can ghi log (null = chua den luc bao).
        /// Nguoi goi tu quyet ghi o dau; <see cref="OnLog"/> cung duoc ban neu co gan.
        /// </summary>
        public static string NoteFail(string proxyStored, string reason)
        {
            string host = HostOf(proxyStored);
            if (host == null) return null;

            string line = null;
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                var st = Get(host);
                st.Fail++;
                st.Streak++;
                st.LastReason = Gon(reason);

                if (st.Streak >= FAIL_STREAK_REPORT && now - st.LastReportUtc >= REPORT_GAP)
                {
                    st.LastReportUtc = now;
                    line = string.Format("[Proxy] {0}: {1} lan loi lien tiep (tong {2} loi / {3} lan mo duoc) - {4}",
                        host, st.Streak, st.Fail, st.Ok, st.LastReason);
                }
            }

            if (line != null)
            {
                var h = OnLog;
                if (h != null) h(line);
            }
            return line;
        }

        /// <summary>
        /// Bang tong ket moi proxy dang hong (cho user bam xem / ghi log khi can).
        /// Sap theo so lan loi lien tiep giam dan.
        /// </summary>
        public static List<string> Report()
        {
            var rows = new List<string>();
            lock (_lock)
            {
                var keys = new List<string>(_map.Keys);
                keys.Sort((a, b) => _map[b].Streak.CompareTo(_map[a].Streak));
                foreach (string k in keys)
                {
                    var st = _map[k];
                    if (st.Fail == 0) continue;
                    rows.Add(string.Format("{0}  ·  loi {1} (lien tiep {2})  ·  mo duoc {3}  ·  {4}",
                        k, st.Fail, st.Streak, st.Ok, st.LastReason));
                }
            }
            return rows;
        }

        /// <summary>Xoa het so lieu (doi danh sach acc / gan lai proxy thi so cu khong con nghia).</summary>
        public static void Reset()
        {
            lock (_lock) _map.Clear();
        }

        // ==================== NOI BO ====================

        /// <summary>GOI TRONG lock.</summary>
        private static Stat Get(string host)
        {
            Stat st;
            if (!_map.TryGetValue(host, out st)) { st = new Stat(); _map[host] = st; }
            return st;
        }

        /// <summary>null = account nay khong dung proxy (khong co gi de theo doi).</summary>
        private static string HostOf(string proxyStored)
        {
            if (string.IsNullOrEmpty(proxyStored)) return null;
            string h = AccountConfig.ProxyHost(proxyStored);
            return h.Length == 0 ? null : h;
        }

        /// <summary>Cat thong diep ngoai le cho dong log khoi tran.</summary>
        private static string Gon(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "(khong ro)";
            reason = reason.Replace("\r", " ").Replace("\n", " ").Trim();
            return reason.Length <= 90 ? reason : reason.Substring(0, 90) + "…";
        }
    }
}
