using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using NSOKHODO.Config;
using NSOKHODO.Models;

namespace NSOKHODO.Protocol
{
    /// <summary>
    /// Danh sach may chu - lay tu URL chinh thuc TeaMobi (NJVI.txt), cache xuong Data/servers.txt,
    /// + cong them server NGUOI DUNG tu them (Data/servers_custom.txt).
    ///
    /// Dinh dang moi dong NJVI.txt: <c>Name:IP:Port:serverLogin:type</c> ngan cach boi ','.
    /// - serverLogin (field 4): byte GUI khi login, PHAN BIET cac server trung IP:Port
    ///   (vd Tone:...:0 vs Sanzu:...:1 cung 112.213.94.205:14444). Truoc day bo qua field nay
    ///   (luon 0) -> login nham server. Xem <see cref="ServerInfo.ServerLogin"/>.
    ///
    /// Thu tu uu tien khi nap: cache URL (neu co) > baked default; roi APPEND custom o cuoi.
    /// Account tham chieu server theo TEN (ben voi viec danh sach doi thu tu) -> dung Resolve().
    /// </summary>
    public static class ServerList
    {
        public const string OfficialUrl = "http://teamobi.com/srvips/NJVI.txt";
        public const int DefaultPort = 14444;

        /// <summary>
        /// Thu tu BAKED cu (truoc khi nap tu URL). CHI dung de dich ServerIndex legacy cua account
        /// cu (file accounts.txt khong co ServerName) -> ten server. KHONG dung cho hien thi.
        /// </summary>
        private static readonly string[] LegacyOrder =
        {
            "Daisho(New)", "Tekkan", "Fukiya", "Sensha", "Sanzu", "Tone", "Bokken",
            "Shuriken", "Tessen", "Kunai", "Katana", "Hirosaki", "Haruna(NEW)"
        };

        /// <summary>
        /// Snapshot NJVI.txt (2026-06-16) lam fallback khi vua khong co mang vua chua co cache.
        /// Cap nhat that lay tu URL luc khoi dong. Luu y field 4 (serverLogin): Fukiya=3, Sanzu=1, Tessen=1.
        /// </summary>
        private const string BakedRaw =
            "Bisento(new):27.0.12.11:14444:0:0,Daisho:27.0.12.8:14444:0:0,Tekkan:27.0.12.108:14445:0:0," +
            "Fukiya:112.213.94.205:14444:3:0,Sensha:27.0.14.122:14444:0:0,Sanzu:112.213.94.205:14444:1:0," +
            "Tone:112.213.94.205:14444:0:0,Bokken:112.213.84.18:14444:0:0,Shuriken:27.0.14.73:14444:0:0," +
            "Tessen:27.0.14.73:14444:1:0,Kunai:112.213.94.135:14444:0:0,Katana:112.213.94.161:14444:0:0," +
            "Hirosaki:13.228.143.11:14444:0:1,Haruna (NEW):54.151.133.77:14444:0:1";

        private static readonly object _lock = new object();
        private static List<ServerInfo> _servers = new List<ServerInfo>();
        private static bool _inited;

        /// <summary>Fire khi danh sach thay doi (nap xong / refresh URL / them custom) - UI re-load combo/grid.</summary>
        public static event Action OnListChanged;

        /// <summary>Snapshot mang hien tai (thread-safe, copy ra de lap an toan).</summary>
        public static ServerInfo[] Servers
        {
            get { lock (_lock) return _servers.ToArray(); }
        }

        // ==================== KHOI TAO / NAP ====================

        /// <summary>
        /// Goi 1 lan luc khoi dong (Program.Main, sau AppPaths.EnsureReady). Nap NHANH tu local
        /// (cache servers.txt neu co, khong thi baked) + custom -> danh sach san sang ngay.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_inited) return;
                _inited = true;
            }
            Rebuild();
        }

        /// <summary>
        /// Lay danh sach moi nhat tu URL chinh thuc (BLOCKING, co timeout). Thanh cong -> ghi cache
        /// servers.txt + rebuild + fire OnListChanged. That bai (offline/timeout) -> giu nguyen, nem false.
        /// Nen goi tren thread nen de khong treo UI.
        /// </summary>
        public static bool RefreshFromOfficial(int timeoutMs = 6000)
        {
            string raw = TryDownload(OfficialUrl, timeoutMs);
            if (string.IsNullOrEmpty(raw)) return false;

            // Phai parse duoc it nhat 1 server moi coi la hop le (tranh ghi de cache bang rac).
            var parsed = ParseRaw(raw, false);
            if (parsed.Count == 0) return false;

            try { File.WriteAllText(AppPaths.Servers, raw, Encoding.UTF8); } catch { }
            Rebuild();
            return true;
        }

        /// <summary>
        /// Them 1 server nguoi dung tu nhap. Luu vao servers_custom.txt (append) + rebuild.
        /// Port mac dinh 14444 neu &lt;= 0. Trung ten (khong phan biet hoa thuong) -> bao loi.
        /// </summary>
        public static bool AddCustom(string name, string host, int port, byte serverLogin, out string error)
        {
            error = null;
            name = (name ?? "").Trim();
            host = (host ?? "").Trim();
            if (port <= 0) port = DefaultPort;
            if (name.Length == 0 || host.Length == 0) { error = "Nhap ten va dia chi may chu."; return false; }
            if (name.IndexOf(':') >= 0 || host.IndexOf(':') >= 0) { error = "Ten/dia chi khong duoc chua dau ':'."; return false; }
            if (GetByName(name) != null) { error = "Da co may chu trung ten."; return false; }

            var s = new ServerInfo(0, name, host, port, serverLogin) { IsCustom = true };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ServersCustom));
                File.AppendAllText(AppPaths.ServersCustom, s.ToRawLine() + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) { error = "Khong ghi duoc file: " + ex.Message; return false; }

            Rebuild();
            return true;
        }

        // ==================== TRA CUU ====================

        public static ServerInfo GetByIndex(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _servers.Count) return _servers[index];
                return _servers.Count > 0 ? _servers[0] : null;
            }
        }

        public static ServerInfo GetByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (_lock)
            {
                foreach (var s in _servers)
                    if (s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return s;
            }
            return null;
        }

        /// <summary>
        /// Tra server cho 1 account. Uu tien TEN (ben voi danh sach doi thu tu); thieu ten thi
        /// dich ServerIndex legacy qua bang LegacyOrder; cuoi cung fallback index hien tai / phan tu 0.
        /// </summary>
        public static ServerInfo Resolve(string serverName, int serverIndex)
        {
            var byName = GetByName(serverName);
            if (byName != null) return byName;

            if (serverIndex >= 0 && serverIndex < LegacyOrder.Length)
            {
                var legacy = GetByName(LegacyOrder[serverIndex]);
                if (legacy != null) return legacy;
            }
            return GetByIndex(serverIndex);
        }

        // ==================== NOI BO ====================

        private static void Rebuild()
        {
            // Nguon "chinh thuc": cache URL neu co, khong thi baked snapshot.
            string officialRaw = null;
            try { if (File.Exists(AppPaths.Servers)) officialRaw = File.ReadAllText(AppPaths.Servers, Encoding.UTF8); }
            catch { }
            if (string.IsNullOrWhiteSpace(officialRaw)) officialRaw = BakedRaw;

            var list = ParseRaw(officialRaw, false);
            if (list.Count == 0) list = ParseRaw(BakedRaw, false); // cache hong -> baked

            // Custom append o cuoi (bo qua trung ten voi official de tranh hai dong cung ten).
            string customRaw = null;
            try { if (File.Exists(AppPaths.ServersCustom)) customRaw = File.ReadAllText(AppPaths.ServersCustom, Encoding.UTF8); }
            catch { }
            foreach (var c in ParseRaw(customRaw, true))
            {
                bool dup = false;
                foreach (var s in list)
                    if (s.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (!dup) list.Add(c);
            }

            for (int i = 0; i < list.Count; i++) list[i].Index = i;

            lock (_lock) { _servers = list; }
            var h = OnListChanged;
            if (h != null) h();
        }

        /// <summary>Parse noi dung NJVI.txt: cac entry ngan cach ',' hoac xuong dong; moi entry Name:IP:Port:serverLogin:type.</summary>
        private static List<ServerInfo> ParseRaw(string raw, bool custom)
        {
            var list = new List<ServerInfo>();
            if (string.IsNullOrEmpty(raw)) return list;

            foreach (var entry in raw.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string e = entry.Trim();
                if (e.Length == 0) continue;

                string[] f = e.Split(':');
                if (f.Length < 3) continue; // toi thieu Name:IP:Port

                string name = f[0].Trim();
                string host = f[1].Trim();
                if (name.Length == 0 || host.Length == 0) continue;

                int port; if (!int.TryParse(f[2].Trim(), out port) || port <= 0) port = DefaultPort;
                byte login = 0; if (f.Length > 3) byte.TryParse(f[3].Trim(), out login);
                byte type = 0; if (f.Length > 4) byte.TryParse(f[4].Trim(), out type);

                list.Add(new ServerInfo(0, name, host, port, login) { Type = type, IsCustom = custom });
            }
            return list;
        }

        private static string TryDownload(string url, int timeoutMs)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.UserAgent = "NSOKHODO";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch { return null; }
        }
    }
}
