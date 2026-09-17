using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using NSOKHODO.Config;

namespace NSOKHODO.Kho
{
    /// <summary>
    /// LOG DAY DU THEO NGAY (SPEC §8.1, D26):
    /// <c>Logs/&lt;danh sach&gt;/yyyy-MM-dd/{app.log, giaodich.csv, chat.log, lenh.csv, hex.log}</c>.
    ///
    /// <para><b>Khong dung lai FileLog cua loi</b>: ban do mac dinh TAT, mot file, qua 5 MB thi
    /// ghi de file cu — trai voi "day du".</para>
    ///
    /// <para>Ghi qua hang doi, mot luong nen gom moi giay. Loi dia KHONG duoc lam chet app: ghi hong
    /// thi bo lo do, lan sau thu lai. Them cap thu muc <c>&lt;danh sach&gt;</c> vi mo nhieu kho (nhieu
    /// danh sach) tren mot may thi cac tien trinh khong duoc ghi chung mot file.</para>
    /// </summary>
    public static class NhatKy
    {
        public const string APP = "app.log";
        public const string GIAO_DICH = "giaodich.csv";
        public const string CHAT = "chat.log";
        public const string LENH = "lenh.csv";
        public const string HEX = "hex.log";

        private const long MOT_PHAN_TOI_DA = 50L * 1024 * 1024;

        private static readonly Dictionary<string, string> _tieuDe = new Dictionary<string, string>
        {
            { GIAO_DICH, "BatDau,KetThuc,Vai,AccBot,DoiPhuong,DoiPhuongId,LaChuKho,MonDua,MonNhan,XuDua,XuNhan,KetQua,LyDo,TinServer,SoLenh,Lech" },
            { LENH, "SoLenh,TaoLuc,KetThucLuc,Nguon,ChuKho,NguoiNhan,Mon,SoXin,SoDaGiao,CacClone,KetQua,LyDo" },
        };

        private static readonly ConcurrentQueue<KeyValuePair<string, string>> _hang = new ConcurrentQueue<KeyValuePair<string, string>>();
        private static Thread _luong;
        private static volatile bool _bat = true;
        private static string _goc;
        private static readonly object _lk = new object();

        /// <summary>Cong tac ghi file (cai dat <c>LogFile</c>). Tat thi hang doi van rut nhung khong ghi.</summary>
        public static bool Bat { get { return _bat; } set { _bat = value; } }

        public static string ThuMucGoc
        {
            get
            {
                if (_goc == null)
                    _goc = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"),
                                        AppPaths.SafeName(AppPaths.ListName));
                return _goc;
            }
        }

        public static string ThuMucHomNay { get { return Path.Combine(ThuMucGoc, DateTime.Now.ToString("yyyy-MM-dd")); } }

        /// <summary>Doi thu muc goc (bo kiem tra dung de ghi vao thu muc tam).</summary>
        public static void DatThuMucGoc(string duongDan) { _goc = duongDan; }

        /// <summary>Khoi dong luong ghi. Goi mot lan luc mo app. Kem don thu muc cu neu co cai dat.</summary>
        public static void KhoiDong(int giuNgay)
        {
            lock (_lk)
            {
                if (_luong != null) return;
                _luong = new Thread(VongGhi) { IsBackground = true, Name = "NSO-NhatKy" };
                _luong.Start();
            }
            if (giuNgay > 0) DonNgayCu(giuNgay);
        }

        // ================= API ghi (goi tu moi luong) =================

        public static void App(string acc, string nhom, string noiDung)
        {
            Day(APP, string.Format("{0} [{1}] [{2}] {3}", Gio(), acc ?? "-", nhom ?? "-", noiDung));
        }

        /// <summary>Dong app.log da dinh dang san (vd log gop cua fleet da co [acc] o dau).</summary>
        public static void AppTho(string dong)
        {
            Day(APP, Gio() + " " + dong);
        }

        public static void Chat(string acc, bool congDong, bool vao, string nguoi, string noiDung)
        {
            Day(CHAT, string.Format("{0} [{1}] [{2}] [{3}] <{4}> {5}", DateTime.Now.ToString("HH:mm:ss"),
                acc ?? "-", congDong ? "CONGDONG" : "RIENG", vao ? "VAO" : "RA", nguoi ?? "", noiDung));
        }

        public static void Hex(string acc, bool vao, sbyte cmd, byte[] data)
        {
            var sb = new StringBuilder();
            if (data != null)
                for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("X2")).Append(' ');
            Day(HEX, string.Format("{0} [{1}] {2} cmd={3} len={4} {5}", Gio(), acc ?? "-", vao ? "S->C" : "C->S",
                cmd, data == null ? 0 : data.Length, sb.ToString().TrimEnd()));
        }

        public static void Csv(string tep, params object[] cot)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cot.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(CsvO(cot[i]));
            }
            Day(tep, sb.ToString());
        }

        public static string CsvO(object o)
        {
            string s;
            if (o == null) s = "";
            else if (o is DateTime) s = ((DateTime)o).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            else s = Convert.ToString(o, CultureInfo.InvariantCulture);
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                s = "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
            return s;
        }

        /// <summary>Ghi ngay moi thu dang cho (goi luc thoat app va trong bo kiem tra).</summary>
        public static void XaNgay()
        {
            GhiHang();
        }

        // ================= ben trong =================

        private static string Gio() { return DateTime.Now.ToString("HH:mm:ss.fff"); }

        private static void Day(string tep, string dong)
        {
            if (!_bat) return;
            // Tran hang doi: dia hong ca dem thi hang nay phinh vo han. Qua tran thi bo dong MOI.
            if (_hang.Count > 200000) return;
            _hang.Enqueue(new KeyValuePair<string, string>(tep, dong));
        }

        private static void VongGhi()
        {
            while (true)
            {
                Thread.Sleep(1000);
                try { GhiHang(); } catch { }
            }
        }

        private static void GhiHang()
        {
            if (_hang.IsEmpty) return;
            lock (_lk)
            {
                var gom = new Dictionary<string, StringBuilder>();
                KeyValuePair<string, string> kv;
                int n = 0;
                while (n < 100000 && _hang.TryDequeue(out kv))
                {
                    StringBuilder sb;
                    if (!gom.TryGetValue(kv.Key, out sb)) { sb = new StringBuilder(); gom[kv.Key] = sb; }
                    sb.Append(kv.Value).Append("\r\n");
                    n++;
                }
                if (!_bat) return;
                string ngay = ThuMucHomNay;
                foreach (var g in gom)
                {
                    try
                    {
                        Directory.CreateDirectory(ngay);
                        string p = PhanHienTai(ngay, g.Key);
                        bool moi = !File.Exists(p);
                        string tieuDe;
                        bool laCsv = g.Key.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                        // CSV co BOM de Excel doc dung tieng Viet; log thuong khong BOM.
                        var enc = new UTF8Encoding(laCsv && moi);
                        using (var w = new StreamWriter(p, true, enc))
                        {
                            if (moi && _tieuDe.TryGetValue(g.Key, out tieuDe)) w.Write(tieuDe + "\r\n");
                            w.Write(g.Value.ToString());
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary><c>app.log</c> vuot 50 MB thi mo <c>app_2.log</c>, <c>app_3.log</c>... — khong ghi de.</summary>
        private static string PhanHienTai(string thuMuc, string tep)
        {
            string ten = Path.GetFileNameWithoutExtension(tep);
            string duoi = Path.GetExtension(tep);
            string p = Path.Combine(thuMuc, tep);
            int phan = 1;
            while (File.Exists(p) && new FileInfo(p).Length >= MOT_PHAN_TOI_DA)
            {
                phan++;
                p = Path.Combine(thuMuc, ten + "_" + phan + duoi);
            }
            return p;
        }

        private static void DonNgayCu(int giuNgay)
        {
            try
            {
                if (!Directory.Exists(ThuMucGoc)) return;
                DateTime moc = DateTime.Now.Date.AddDays(-giuNgay);
                foreach (var d in Directory.GetDirectories(ThuMucGoc))
                {
                    DateTime ngay;
                    if (DateTime.TryParseExact(Path.GetFileName(d), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                               DateTimeStyles.None, out ngay) && ngay < moc)
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
