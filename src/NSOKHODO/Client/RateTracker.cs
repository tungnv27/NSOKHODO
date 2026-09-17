using System;
using System.Collections.Generic;

namespace NSOKHODO.Client
{
    /// <summary>
    /// Bo dem TOC DO cho tung tai khoan: Yen/h va EXP%/h. Mot noi duy nhat cho ca ban PC lan
    /// ban Android (dat NGOAI <c>UI/</c> nen csproj cua APK tu nap - KHONG duoc cham
    /// System.Drawing / System.Windows.Forms / Android).
    ///
    /// Thay cho hai ban cu tach roi: <c>MainForm.YenStat</c> (PC) va <c>AccountGrid.YenTracker</c>
    /// (Android) - hai ban do cung cong thuc nhung sai cung mot kieu (xem §Dem lai cho chuan).
    ///
    /// ==================== MOC DEM DAT/XOA O DAU (user chot) ====================
    ///   Bat Auto            -> Clear() roi chot lai ngay khi acc vao duoc game
    ///   Mo Game (auto san)  -> chot luc vao game
    ///   Dung Auto           -> Clear()   (cot hien "---")
    ///   Tat Game (OFFLINE)  -> Clear()   (mo lai la dem lai tu dau)
    ///   Chet -> hoi sinh    -> GIU nguyen moc
    ///   Rot mang -> vao lai -> GIU nguyen moc (dong ho chay suot qua khoang rot)
    ///
    /// 🔑 KHONG duoc neo moc vao <c>NsoClient.StartAutoSystems()</c>: ham do bi goi lai MOI LAN
    /// nhan vat chet roi hoi sinh (NsoClient.cs, StartAutoSystems(REVIVE_STARTUP_DELAY_MS)), ma
    /// san TATL thi chet lien tuc -> dem lai tu dau lien tuc. Vi vay moc song theo DONG TAI KHOAN
    /// (AccountConfig.RateKey), khong song theo vong doi client/mode.
    ///
    /// KHOA la RateKey chu KHONG phai Username: accounts.txt cho phep mot tai khoan nam tren
    /// NHIEU dong; hai dong cung username thi dong nay Clear() cai dong kia vua Sample(), moi
    /// giay mot lan -> dong ho ket cung o "0s" (loi that, user bao 2026-09-09).
    ///
    /// ==================== DEM LAI CHO CHUAN (2 loi cua ban cu) ====================
    ///   1. Tat Game roi Mo lai  -> ban cu GIU moc cu, tuc dong ho tinh ca khoang tool dung khong
    ///                              -> Yen/h bi chia loang, nhin tuong bai farm kem.
    ///   2. Dung Auto            -> ban cu van chay tiep va van hien so.
    /// Ca hai gio deu Clear().
    ///
    /// ==================== YEN KIEM DUOC vs YEN RONG ====================
    /// <see cref="Reading.NetYen"/>  = yen hien tai - yen luc chot moc (co tru luc tieu tien:
    ///                                 dap do, mua binh, gui yen). Day la so hien tren LUOI.
    /// <see cref="Reading.EarnedYen"/> = chi cong nhung nhip yen TANG -> do dung toc do farm cua
    ///                                 bai, khong bi cu dap do keo am. Hien o tab "Tong quan".
    /// Vi "kiem duoc" phai cong don tung nhip nen <see cref="Sample"/> BAT BUOC duoc goi deu
    /// (PC 1s tu _refreshTimer, Android 5s tu BotService de con dem duoc khi dong man hinh).
    /// Sai so da biet: yen len roi xuong TRONG CUNG mot nhip thi chi thay phan rong.
    /// </summary>
    public static class RateTracker
    {
        /// <summary>Duoi nguong nay chua du mau de chia -> hien "..." (giu nguyen quy uoc cu).</summary>
        public const int MIN_SECONDS = 20;

        private class Stat
        {
            // --- moc yen: chot khi da co ten nhan vat (tranh chot luc yen chua ve, = 0) ---
            public bool HasYen;
            public DateTime YenAt;
            public long BaseYen, LastYen, EarnedYen;

            // --- moc exp: chot RIENG vi bang exps[] ve tu DataSync, co the muon hon ten nhan vat.
            // Gop chung mot moc thi server nao khong gui exps[] se lam mat luon ca Yen/h.
            public bool HasExp;
            public DateTime ExpAt;
            public double BasePts, LastPts, EarnedPts;
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Stat> _map =
            new Dictionary<string, Stat>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Xoa moc: goi khi Dung Auto, hoac khi acc ve OFFLINE (Tat Game).</summary>
        public static void Clear(string user)
        {
            if (string.IsNullOrEmpty(user)) return;
            lock (_lock) { _map.Remove(user); }
        }

        /// <summary>
        /// Lay mau. CHI goi khi acc dang InGame VA auto dang bat - dung the moi giu duoc dung
        /// bang moc o phan chu thich dau lop.
        /// </summary>
        /// <param name="charName">Rong = char info chua ve -> chua chot moc yen.</param>
        /// <param name="totalExp">cEXP (exp TICH LUY), khong phai exp trong cap.</param>
        public static void Sample(string user, string charName, long yen, long totalExp)
        {
            if (string.IsNullOrEmpty(user)) return;
            double pts;
            bool hasPts = GameData.ExpTable.TryGetProgressPoints(totalExp, out pts);
            DateTime now = DateTime.UtcNow;

            lock (_lock)
            {
                Stat st;
                if (!_map.TryGetValue(user, out st)) { st = new Stat(); _map[user] = st; }

                if (!st.HasYen)
                {
                    if (!string.IsNullOrEmpty(charName))
                    {
                        st.HasYen = true;
                        st.YenAt = now;
                        st.BaseYen = st.LastYen = yen;
                        st.EarnedYen = 0;
                    }
                }
                else
                {
                    if (yen > st.LastYen) st.EarnedYen += yen - st.LastYen;
                    st.LastYen = yen;
                }

                if (hasPts)
                {
                    if (!st.HasExp)
                    {
                        st.HasExp = true;
                        st.ExpAt = now;
                        st.BasePts = st.LastPts = pts;
                        st.EarnedPts = 0;
                    }
                    else
                    {
                        if (pts > st.LastPts) st.EarnedPts += pts - st.LastPts;
                        st.LastPts = pts;
                    }
                }
            }
        }

        /// <summary>Ket qua doc ra. Moi ty le deu la "tren gio". NaN = chua du du lieu de tinh.</summary>
        public class Reading
        {
            /// <summary>Da dem duoc bao nhieu giay (tinh tu moc yen).</summary>
            public double Seconds;
            /// <summary>Da du <see cref="MIN_SECONDS"/> de con so co nghia.</summary>
            public bool Ready;

            public long NetYen;        // co tru luc tieu tien
            public long EarnedYen;     // chi phan tang
            public double NetYenPerHour, EarnedYenPerHour;

            public bool HasExp;
            public double NetPct;      // % (1 cap = 100) - co the AM khi PK am
            public double EarnedPct;
            public double NetPctPerHour, EarnedPctPerHour;

            /// <summary>"1h23'45" - do dai khoang da dem, cho tab Tong quan.</summary>
            public string Elapsed;
        }

        /// <summary>
        /// Doc so lieu da chot. Tra false khi CHUA co moc (chua Bat Auto / chua vao game / vua
        /// Clear) -> cho goi hien "---". Co moc nhung chua du 20s thi tra true voi Ready=false.
        /// </summary>
        public static bool TryRead(string user, out Reading r)
        {
            r = null;
            if (string.IsNullOrEmpty(user)) return false;
            lock (_lock)
            {
                Stat st;
                if (!_map.TryGetValue(user, out st) || !st.HasYen) return false;

                var now = DateTime.UtcNow;
                double sec = (now - st.YenAt).TotalSeconds;
                if (sec < 0) sec = 0;   // dong ho may bi chinh lui

                r = new Reading();
                r.Seconds = sec;
                r.Ready = sec >= MIN_SECONDS;
                r.Elapsed = FormatElapsed(sec);

                r.NetYen = st.LastYen - st.BaseYen;
                r.EarnedYen = st.EarnedYen;
                double h = sec / 3600.0;
                r.NetYenPerHour = h > 0 ? r.NetYen / h : double.NaN;
                r.EarnedYenPerHour = h > 0 ? r.EarnedYen / h : double.NaN;

                if (st.HasExp)
                {
                    double esec = (now - st.ExpAt).TotalSeconds;
                    if (esec < 0) esec = 0;
                    double eh = esec / 3600.0;
                    r.HasExp = true;
                    r.NetPct = st.LastPts - st.BasePts;
                    r.EarnedPct = st.EarnedPts;
                    r.NetPctPerHour = eh > 0 ? r.NetPct / eh : double.NaN;
                    r.EarnedPctPerHour = eh > 0 ? r.EarnedPct / eh : double.NaN;
                    // Moc exp chot sau moc yen (bang exps ve muon) -> chua du 20s thi cung chua tin duoc.
                    if (esec < MIN_SECONDS) r.HasExp = false;
                }
                return true;
            }
        }

        /// <summary>"1h23'45" / "23'45" / "45s" - gon de nhet vao tooltip va tab Tong quan.</summary>
        public static string FormatElapsed(double seconds)
        {
            if (seconds < 0) seconds = 0;
            long total = (long)seconds;
            long h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            if (h > 0) return h + "h" + (m < 10 ? "0" + m : m.ToString()) + "'" + (s < 10 ? "0" + s : s.ToString());
            if (m > 0) return m + "'" + (s < 10 ? "0" + s : s.ToString());
            return s + "s";
        }
    }
}
