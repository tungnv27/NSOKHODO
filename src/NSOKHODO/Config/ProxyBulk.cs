using System;
using System.Collections.Generic;
using System.Text;
using NSOKHODO.Client;

namespace NSOKHODO.Config
{
    /// <summary>
    /// LOI dung chung cho tinh nang "Chia proxy": doc mot DANH SACH proxy dan vao roi chia cho
    /// nhieu account. Dat NGOAI <c>UI/</c> de ban Android nap chung MOT ban - hai ben chia lech
    /// nhau mot con acc la user khong bao gio tin lai duoc bang xem truoc.
    ///
    /// Boi canh (user 2026-09-12): <i>"dan 1 list 100 proxy cho 600 acc 6acc/1proxy thi rat mat
    /// cong"</i> - hop thoai <c>ProxyAssignForm</c> cu chi gan DUNG MOT proxy cho moi dong dang chon.
    ///
    /// Lop nay KHONG cham UI: no chi tra ra "acc thu i dung proxy thu may" (<see cref="Divide"/>)
    /// de nguoi goi ve bang xem truoc TRUOC khi ghi. Danh sach TEN acc chua duoc gan do UI liet ke
    /// (chi UI biet dong nao la dong nao).
    /// </summary>
    public static class ProxyBulk
    {
        /// <summary>So acc/proxy mac dinh (user chot 2026-09-12 - trung tran mac dinh cua cong IP).</summary>
        public const int DEFAULT_PER_PROXY = 4;

        /// <summary>Mot dong proxy da doc xong.</summary>
        public class Entry
        {
            /// <summary>Dang tron <c>host:port[:user:pass]</c>, KHONG con tien to scheme.</summary>
            public string Bare;

            /// <summary>Dong nay la HTTP (chi co nghia khi <see cref="HasScheme"/>).</summary>
            public bool IsHttp;

            /// <summary>Co tien to scheme ngay tren dong -> loai lay theo DONG, khong theo dropdown.</summary>
            public bool HasScheme;

            /// <summary>Host de hien thi/nhom (bo port va user/pass).</summary>
            public string Host
            {
                get
                {
                    int c = Bare != null ? Bare.IndexOf(':') : -1;
                    return c > 0 ? Bare.Substring(0, c) : (Bare ?? "");
                }
            }
        }

        public class ParseResult
        {
            public readonly List<Entry> Items = new List<Entry>();

            /// <summary>So thu tu dong (tinh tu 1) khong doc duoc - de UI boi do.</summary>
            public readonly List<int> BadLines = new List<int>();

            /// <summary>So dong trung mot proxy da xuat hien truoc. GIU LAI, chi dem de bao.</summary>
            public int Duplicates;

            public int Count { get { return Items.Count; } }
        }

        /// <summary>
        /// Cach chia. (Kieu "vong tron" da co trong ban thiet ke nhung user BO 2026-09-12: tai tren
        /// tung proxy y het <see cref="Even"/>, chi khac thu tu, ma xep lien khoi de doi chieu hon.)
        /// </summary>
        public enum Mode
        {
            /// <summary>N acc lien tiep dung 1 proxy; N do user go.</summary>
            PerProxy = 0,

            /// <summary>Chia deu het danh sach: N tu tinh, so du rai vao cac proxy DAU.</summary>
            Even = 1
        }

        public class Plan
        {
            /// <summary>Chi so proxy cho tung acc (theo dung thu tu acc truyen vao). -1 = KHONG gan.</summary>
            public int[] ProxyOfAcc;

            public int Assigned, NotAssigned;

            /// <summary>So proxy trong danh sach khong duoc dung den.</summary>
            public int UnusedProxies;

            /// <summary>So acc it nhat / nhieu nhat ma mot proxy phai ganh.</summary>
            public int MinPerProxy, MaxPerProxy;

            /// <summary>Tron tru: moi proxy ganh bang nhau va khong acc nao bi bo.</summary>
            public bool Exact;

            /// <summary>Cau canh bao san cho UI (rong = khong co gi phai bao).</summary>
            public string Warning = "";
        }

        // ==================== DOC DANH SACH ====================

        /// <summary>
        /// Doc o van ban nhieu dong thanh danh sach proxy.
        ///
        /// Chap nhan: <c>host:port</c> · <c>host:port:user:pass</c> · <c>user:pass@host:port</c>
        /// (dang hay gap khi copy tu trang ban proxy) · tien to <c>http://</c> <c>https://</c>
        /// <c>socks5://</c> <c>socks://</c>. Bo qua dong trong / bat dau <c>#</c> hoac <c>//</c>.
        ///
        /// KHONG tu xoa dong trung: user co the co y cho hai proxy giong nhau: chi dem vao
        /// <see cref="ParseResult.Duplicates"/> de UI noi ra.
        /// </summary>
        public static ParseResult Parse(string text)
        {
            var res = new ParseResult();
            if (string.IsNullOrEmpty(text)) return res;

            var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = (lines[i] ?? "").Trim();
                raw = raw.TrimEnd(',', ';').Trim();
                if (raw.Length == 0) continue;
                if (raw.StartsWith("#") || raw.StartsWith("//")) continue;

                var e = ParseOne(raw);
                if (e == null) { res.BadLines.Add(i + 1); continue; }

                if (seen.ContainsKey(e.Bare)) res.Duplicates++;
                else seen[e.Bare] = true;
                res.Items.Add(e);
            }
            return res;
        }

        /// <summary>Doc MOT dong. null = khong phai proxy dung dang.</summary>
        private static Entry ParseOne(string raw)
        {
            bool isHttp = false, hasScheme = false;
            int s = raw.IndexOf("://", StringComparison.OrdinalIgnoreCase);
            if (s > 0)
            {
                string scheme = raw.Substring(0, s).ToLowerInvariant();
                if (scheme != "http" && scheme != "https" && scheme != "socks5" && scheme != "socks")
                    return null;                       // scheme la -> khong nhan
                isHttp = scheme == "http" || scheme == "https";
                hasScheme = true;
                raw = raw.Substring(s + 3).Trim();
            }

            // "user:pass@host:port" -> "host:port:user:pass" (dinh dang repo dang dung).
            int at = raw.LastIndexOf('@');
            if (at > 0)
            {
                string cred = raw.Substring(0, at).Trim();
                string hostPart = raw.Substring(at + 1).Trim();
                if (hostPart.Length == 0) return null;
                raw = cred.Length > 0 ? hostPart + ":" + cred : hostPart;
            }

            string[] p = raw.Split(':');
            if (p.Length < 2) return null;

            string host = p[0].Trim();
            if (host.Length == 0) return null;

            int port;
            if (!int.TryParse(p[1].Trim(), out port) || port <= 0 || port > 65535) return null;

            var sb = new StringBuilder();
            sb.Append(host).Append(':').Append(port);
            for (int i = 2; i < p.Length; i++) sb.Append(':').Append(p[i].Trim());

            return new Entry { Bare = sb.ToString(), IsHttp = isHttp, HasScheme = hasScheme };
        }

        // ==================== CHIA ====================

        /// <summary>
        /// So acc/proxy cua kieu <see cref="Mode.Even"/>, lam tron LEN - dung de hien dong
        /// "600 ÷ 100 = 6 acc/proxy" va de dien san vao o nhap N.
        /// </summary>
        public static int EvenPerProxy(int accCount, int proxyCount)
        {
            if (accCount <= 0 || proxyCount <= 0) return 0;
            return (accCount + proxyCount - 1) / proxyCount;
        }

        /// <summary>
        /// Len ke hoach chia. KHONG ghi gi - nguoi goi ve bang xem truoc roi moi ap dung.
        /// </summary>
        /// <param name="accCount">So account (da loc) duoc chia, theo dung thu tu tren luoi.</param>
        /// <param name="proxyCount">So proxy doc duoc.</param>
        /// <param name="perProxy">So acc/proxy - CHI dung cho <see cref="Mode.PerProxy"/>.</param>
        /// <param name="repeat">Het proxy thi quay lai dau danh sach (mac dinh KHONG).</param>
        public static Plan Divide(int accCount, int proxyCount, Mode mode, int perProxy, bool repeat)
        {
            var plan = new Plan { ProxyOfAcc = new int[Math.Max(0, accCount)] };
            for (int i = 0; i < plan.ProxyOfAcc.Length; i++) plan.ProxyOfAcc[i] = -1;

            if (accCount <= 0 || proxyCount <= 0)
            {
                plan.NotAssigned = Math.Max(0, accCount);
                plan.Warning = proxyCount <= 0
                    ? "Chưa đọc được proxy nào từ danh sách."
                    : "Chưa chọn tài khoản nào.";
                return plan;
            }

            if (mode == Mode.Even)
            {
                // Chia deu THAT: chia nguyen roi rai so du vao cac proxy dau, moi con +1.
                // 600 acc / 7 proxy -> 5 proxy nhan 86, 2 proxy nhan 85, KHONG bo acc nao.
                // (Lay ceil roi cat khoi cung nhau thi proxy cuoi ganh le hin va de sinh ra proxy
                // khong duoc dung den.)
                int b = accCount / proxyCount;
                int r = accCount % proxyCount;
                int acc = 0;
                for (int p = 0; p < proxyCount && acc < accCount; p++)
                {
                    int take = b + (p < r ? 1 : 0);
                    for (int k = 0; k < take && acc < accCount; k++) plan.ProxyOfAcc[acc++] = p;
                }
            }
            else
            {
                int n = perProxy > 0 ? perProxy : 1;
                for (int i = 0; i < accCount; i++)
                {
                    int p = i / n;
                    if (p >= proxyCount)
                    {
                        if (!repeat) continue;              // -1 = khong gan
                        p = p % proxyCount;
                    }
                    plan.ProxyOfAcc[i] = p;
                }
            }

            // ===== Thong ke =====
            var load = new int[proxyCount];
            for (int i = 0; i < accCount; i++)
            {
                int p = plan.ProxyOfAcc[i];
                if (p < 0) { plan.NotAssigned++; continue; }
                plan.Assigned++;
                load[p]++;
            }

            plan.MinPerProxy = int.MaxValue;
            for (int p = 0; p < proxyCount; p++)
            {
                if (load[p] == 0) { plan.UnusedProxies++; continue; }
                if (load[p] < plan.MinPerProxy) plan.MinPerProxy = load[p];
                if (load[p] > plan.MaxPerProxy) plan.MaxPerProxy = load[p];
            }
            if (plan.MinPerProxy == int.MaxValue) plan.MinPerProxy = 0;

            plan.Exact = plan.NotAssigned == 0 && plan.UnusedProxies == 0
                         && plan.MinPerProxy == plan.MaxPerProxy;
            plan.Warning = BuildWarning(plan, proxyCount, mode, perProxy, repeat);
            return plan;
        }

        /// <summary>
        /// Cau canh bao (user chot: "neu chia sai thi dua text canh bao va acc nao khong co proxy"
        /// - o day lo phan SO LIEU, danh sach ten acc do UI liet ke).
        /// </summary>
        private static string BuildWarning(Plan plan, int proxyCount, Mode mode, int perProxy, bool repeat)
        {
            var sb = new StringBuilder();

            if (plan.NotAssigned > 0)
            {
                int n = perProxy > 0 ? perProxy : 1;
                sb.Append(string.Format(
                    "THIẾU PROXY: {0} proxy × {1} acc = {2} acc → còn {3} acc KHÔNG được gán "
                    + "(giữ nguyên proxy cũ, KHÔNG bị xóa trắng).",
                    proxyCount, n, proxyCount * n, plan.NotAssigned));
                if (!repeat)
                    sb.Append(" Chữa: bật \"Lặp lại danh sách\" · hoặc giảm số acc/proxy · hoặc dán thêm proxy.");
            }

            if (plan.MaxPerProxy > plan.MinPerProxy && plan.MinPerProxy > 0)
            {
                if (sb.Length > 0) sb.Append(Environment.NewLine);
                sb.Append(string.Format("Chia không đều: có proxy nhận {0} acc, có proxy chỉ nhận {1} acc.",
                    plan.MaxPerProxy, plan.MinPerProxy));
                if (mode == Mode.Even) sb.Append(" Không bỏ acc nào.");
            }

            if (plan.UnusedProxies > 0)
            {
                if (sb.Length > 0) sb.Append(Environment.NewLine);
                sb.Append(string.Format("{0} proxy trong danh sách không được dùng đến.", plan.UnusedProxies));
            }

            return sb.ToString();
        }

        // ==================== AP DUNG ====================

        /// <summary>
        /// Ghi ke hoach vao cac AccountConfig, tra ve so acc THAT SU doi (de log dung so).
        ///
        /// Acc co <c>ProxyOfAcc = -1</c> GIU NGUYEN proxy cu - "chia thieu" khong phai la y muon
        /// bo proxy, xoa trang o day la am tham pha cau hinh cua user.
        /// </summary>
        /// <param name="defaultIsHttp">Loai chon o dropdown - chi dung cho dong KHONG co scheme.</param>
        /// <param name="changedPositions">(tuy chon) nhan lai vi tri (trong <paramref name="accs"/>)
        /// cua nhung acc vua doi, de UI chi ve lai dung may o do.</param>
        public static int Apply(IList<AccountConfig> accs, IList<Entry> proxies, Plan plan,
                                bool defaultIsHttp, IList<int> changedPositions)
        {
            if (accs == null || proxies == null || plan == null || plan.ProxyOfAcc == null) return 0;

            int changed = 0;
            int n = Math.Min(accs.Count, plan.ProxyOfAcc.Length);
            for (int i = 0; i < n; i++)
            {
                int p = plan.ProxyOfAcc[i];
                if (p < 0 || p >= proxies.Count) continue;

                var e = proxies[p];
                string stored = AccountConfig.JoinProxy(e.HasScheme ? e.IsHttp : defaultIsHttp, e.Bare);
                if (string.Equals(accs[i].Proxy ?? "", stored, StringComparison.Ordinal)) continue;

                accs[i].Proxy = stored;
                changed++;
                if (changedPositions != null) changedPositions.Add(i);
            }
            return changed;
        }
    }
}
