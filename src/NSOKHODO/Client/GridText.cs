using System.Globalization;

namespace NSOKHODO.Client
{
    /// <summary>
    /// Dung chuoi cho hai o "LV" va "Yen" cua luoi tai khoan. Dat NGOAI <c>UI/</c> nen ban Android
    /// tu nap - va quan trong hon: PC va APK dung CHUNG mot ban nen khong the ve sau moi ben mot kieu.
    ///
    /// ==================== VI SAO CAN CAN CHU (2026-09-08, user duyet) ====================
    /// Ban dau hai o nay in tu do: <c>80[42.93%] 0.4/h</c> va <c>1535347572 . 96056/h</c>.
    /// Nam cho kho doc:
    ///   1. So can TRAI, do dai moi dong moi khac -> cot so rang cua, khong so sanh doc duoc.
    ///   2. TONG yen (10-13 chu so) chiem gan het be ngang, con TOC DO - thu thuc su phai theo doi
    ///      - bi day ra ria.
    ///   3. EXP/h mot chu so thap phan la qua tho: lv 80+ thi phan lon acc chay duoi 0.05%/h,
    ///      acc DANG CHAY (0.02) va acc CHET DI (0.00) deu hien "0.0" - khong phan biet duoc.
    ///   4. Tong yen viet du chu so lam cot qua rong, keo nho cua so la cot cuoi bi day ra ngoai.
    ///
    /// Cach chua (user chot: "lam theo chuan the gioi" + "chuan ve phan table"):
    ///   - So viet theo LE QUOC TE: ngan nghin ",", thap phan ".", rut gon K / M / B.
    ///   - Cot so CAN PHAI (dat ben giao dien) - le chuan cua moi bang du lieu.
    ///   - TOC DO thi LUON in du so - do la don vi cua moi phep so voi ZangVPS, rut gon la khong
    ///     doc duoc chenh lech vai chuc nghin nua.
    ///
    /// ⚠ DA THU font DEU (Consolas) + dem dau cach bang PadLeft de moi TRUONG ben trong o ghep
    /// cung thang hang. User bac: "font chu lv va yen xau qua, cho lai binh thuong la duoc roi".
    /// Nen gio dung font THUONG cua luoi va KHONG dem dau cach nua: can PHAI lo phan me phai, con
    /// cac truong ben trong thi khong hua thang hang. DUNG THEM PadLeft o day - font ti le thi dau
    /// cach rong khac chu so, dem vao chi lam le tum lum chu khong can duoc gi.
    ///
    /// Giao dien do be rong cot bang <see cref="WidestLevelCell"/> / <see cref="WidestYenCell"/>
    /// - do la chuoi DAI NHAT that su, do bang dung font cua luoi.
    /// </summary>
    public static class GridText
    {
        /// <summary>
        /// Chuoi DAI NHAT o "LV" co the in ra: cap 3 chu so + % am + toc do am hai chu so.
        /// Giao dien do be rong cot BANG chuoi nay (khong doan mot con so cung): be rong that phu
        /// thuoc font co san tren may va muc phong to man hinh (125% / 150%), doan bang mat la may
        /// khac se cat mat chu.
        /// </summary>
        public static string WidestLevelCell
        {
            get { return "120[-35.87%]  -12.34%/h"; }
        }

        /// <summary>Chuoi DAI NHAT o "Yen" co the in ra - xem <see cref="WidestLevelCell"/>.</summary>
        public static string WidestYenCell
        {
            get { return "1,234,567/h  ·  999.99B"; }
        }

        /// <summary>
        /// Dinh dang so kieu QUOC TE: ngan nghin = ",", thap phan = "." (user chot 2026-09-08).
        ///
        /// Dung Invariant chu KHONG dua vao CurrentCulture cua may: VPS va may nguoi dung moi noi
        /// mot vung khac nhau, de mac dinh thi cung mot ban tool moi may in mot kieu.
        /// Va CO Y khong dat CurrentCulture cua tien trinh - doi cai do la keo theo moi cho parse
        /// khac (accounts.txt, TrainConfig...), lop nao dang tin "." la dau thap phan se hong am tham.
        /// </summary>
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>So nguyen du chu so, ngan nghin kieu quoc te: <c>1,535,347,572</c>.</summary>
        public static string Num(long v)
        {
            return v.ToString("N0", Inv);
        }

        /// <summary>
        /// Rut gon kieu quoc te cho o hep: <c>1.54B</c> / <c>235.0M</c> / <c>847.5K</c>.
        /// Duoi 1.000 thi in thang so (nhieu nhat 3 ky tu).
        ///
        /// CHI dung cho TONG yen - toc do luon di qua <see cref="Num"/>.
        ///
        /// Nguong cua B va M la 999.950.000 / 999.950 chu khong phai 1e9 / 1e6: 999.999.999 chia
        /// trieu roi lam tron mot chu so se ra "1000.0M" - vua sai bac, vua tran khoi truong 7 ky
        /// tu va lam lech ca cot. Tren nguong do thi day thang len bac tren ("1.00B").
        /// </summary>
        public static string ShortNum(long v)
        {
            long a = v < 0 ? -v : v;
            if (a >= 999950000L) return (v / 1000000000.0).ToString("0.00", Inv) + "B";
            if (a >= 999950L) return (v / 1000000.0).ToString("0.0", Inv) + "M";
            if (a >= 1000L) return (v / 1000.0).ToString("0.0", Inv) + "K";
            return v.ToString(Inv);
        }

        /// <summary>
        /// Toc do EXP: <c>0.43%/h</c>. Hai chu so thap phan - xem ly do o dau lop, muc 3.
        /// Dung chung cho o "LV" cua luoi va dong toc do trong khung Xem game (hai noi in lech
        /// nhau la user tuong tool dem sai).
        /// </summary>
        public static string PctRate(double pctPerHour)
        {
            return pctPerHour.ToString("0.00", Inv) + "%/h";
        }

        /// <summary>Toc do yen, DU SO: <c>96,056/h</c>. Xem <see cref="PctRate"/> ve ly do dung chung.</summary>
        public static string YenRate(double yenPerHour)
        {
            return Num((long)yenPerHour) + "/h";
        }

        /// <summary>
        /// O "LV (EXP% · %/h)": <c>"  80[42.93%]   0.43%/h"</c>.
        ///
        /// Phan cap + % GIU NGUYEN dang <c>62[77.88%]</c> cua ban cu (user chot 2026-09-08) - chi
        /// le PHAI de dau "]" thang cot giua cac dong.
        ///
        /// Toc do lay 2 chu so thap phan chu khong phai 1 - xem ly do o dau lop, muc 3.
        /// </summary>
        /// <param name="level">Cap hien tai.</param>
        /// <param name="pctText">% trong cap do <see cref="GameData.ExpTable.TryGetPercentText"/>
        /// tra ve (KHONG kem dau %), hoac null khi chua co bang exps.</param>
        /// <param name="r">Ket qua bo dem; null = chua co moc (Auto dang tat) -> de trong phan toc do.</param>
        public static string LevelCell(int level, string pctText, RateTracker.Reading r)
        {
            string lv = pctText != null ? level + "[" + pctText + "%]" : level.ToString();
            string rate;
            if (r == null) rate = "";
            else if (!r.HasExp || !r.Ready) rate = "...";
            else rate = PctRate(r.NetPctPerHour);
            return rate.Length == 0 ? lv : (lv + "  " + rate);
        }

        /// <summary>
        /// O "Yên/h · Tổng": <c>"    96,056/h ·  1.54B"</c>.
        ///
        /// TOC DO DUNG TRUOC, tong yen day ra ria (user chot 2026-09-08). Nho vay hai cot toc do
        /// (%/h cua o LV va Yen/h cua o nay) nam SAT NHAU o giua bang - dung thu tu ma mat quet.
        ///
        /// Ve toc do la yen RONG (co tru luc dap do / mua binh) - user chot; "yen kiem duoc" nam
        /// o tab Tong quan.
        /// </summary>
        public static string YenCell(long yen, RateTracker.Reading r)
        {
            string rate;
            if (r == null) rate = "";
            else if (!r.Ready) rate = "...";
            else rate = YenRate(r.NetYenPerHour);

            string total = ShortNum(yen);
            return rate.Length == 0 ? total : (rate + "  ·  " + total);
        }
    }
}
