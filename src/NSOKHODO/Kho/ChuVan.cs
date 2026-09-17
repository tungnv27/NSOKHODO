using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace NSOKHODO.Kho
{
    /// <summary>
    /// Xu ly chu cho chat gui vao game.
    ///
    /// <para><b>Hai luat da do tren server chinh</b> (test tay 2026-09-16):</para>
    /// <list type="bullet">
    /// <item><b>T10</b> — chat rieng phai gui <b>tieng Viet KHONG DAU</b> (SPEC D46).</item>
    /// <item><b>T11</b> — co tem <c>@NNN</c> o dau thi chat khu 5 giay/lan khong bi chan; ben
    /// NSOLITEPRO thi server khoa chat rieng khi noi dung lap lai (SPEC D18).</item>
    /// </list>
    /// </summary>
    public static class ChuVan
    {
        // Bo dem tem dung chung TOAN TIEN TRINH (khuon KichYenCaller.TemGoi cua NSOLITEPRO): hai bot
        // gui cung mot cau van ra hai tem khac nhau. Moi bang TickCount de moi lan mo app khong bat dau
        // lai tu 000.
        private static int _tem = Environment.TickCount & 0x3FF;

        /// <summary>Tem tiep theo dang <c>"@NNN "</c> (000–999, xoay vong).</summary>
        public static string TemMoi()
        {
            int n = Interlocked.Increment(ref _tem) & 0x7FFFFFFF;
            return "@" + (n % 1000).ToString("000") + " ";
        }

        /// <summary>
        /// Chuan bi mot tin de gui: bo dau, gan tem, cat do dai. <paramref name="toiDa"/> tinh CA tem.
        /// </summary>
        public static string ChuanBiTinGui(string noiDung, int toiDa)
        {
            string s = TemMoi() + BoDau(noiDung ?? "").Trim();
            if (toiDa > 5 && s.Length > toiDa) s = s.Substring(0, toiDa);
            return s;
        }

        /// <summary>
        /// Bo tem <c>@NNN</c> o dau tin (khoan dung: khong co tem thi tra nguyen). Chap nhan ca
        /// <c>@7</c>, <c>@07</c>, co hoac khong co dau cach theo sau.
        /// </summary>
        public static string BoTem(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            string t = s.TrimStart();
            if (t.Length < 2 || t[0] != '@' || !char.IsDigit(t[1])) return s;
            int i = 1;
            while (i < t.Length && i <= 3 && char.IsDigit(t[i])) i++;
            return t.Substring(i).TrimStart();
        }

        /// <summary>
        /// Tieng Viet -> khong dau: tach dau ra (FormD), bo dau ket hop, <c>đ/Đ</c> -> <c>d/D</c>.
        /// Ky tu ngoai ASCII con sot (ky hieu la, emoji...) -> <c>?</c>. Ky tu dieu khien -> dau cach.
        /// </summary>
        public static string BoDau(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char ch in d)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark
                    || cat == UnicodeCategory.EnclosingMark)
                    continue;
                if (ch == 'đ') { sb.Append('d'); continue; }
                if (ch == 'Đ') { sb.Append('D'); continue; }
                if (ch < 32) { sb.Append(' '); continue; }
                sb.Append(ch < 127 ? ch : '?');
            }
            return sb.ToString();
        }

        /// <summary>Co ky tu nao bi thay bang '?' khi bo dau khong (de ghi log canh bao).</summary>
        public static bool CoKyTuLa(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return BoDau(s).IndexOf('?') >= 0 && s.IndexOf('?') < 0;
        }

        /// <summary>
        /// Chuan hoa de SO KHOP lenh: bo tem, bo dau, chu thuong, gop khoang trang.
        /// </summary>
        public static string ChuanHoa(string s)
        {
            string t = BoDau(BoTem(s ?? "")).ToLowerInvariant();
            var sb = new StringBuilder(t.Length);
            bool cach = false;
            foreach (char ch in t)
            {
                if (char.IsWhiteSpace(ch)) { cach = true; continue; }
                if (cach && sb.Length > 0) sb.Append(' ');
                cach = false;
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>So sanh ten nhan vat: khong phan biet hoa thuong, bo khoang trang hai dau.</summary>
        public static bool CungTen(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>So xu dang de doc: 1.234.567 (dau cham ngan cach, khong dau).</summary>
        public static string SoDep(long n)
        {
            return n.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
        }
    }
}
