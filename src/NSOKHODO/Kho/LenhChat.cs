using System;
using System.Collections.Generic;

namespace NSOKHODO.Kho
{
    public enum LoaiLenh { Khong = 0, Kho, Nap, Tim, Co, Lay, LayGoi, Goi, Tiep, Huy, Theo, BoTheo, Xa, XaXong, Loi }

    /// <summary>Mot lenh chat rieng cua Chu kho da doc xong (SPEC §9.4).</summary>
    public sealed class LenhChat
    {
        public LoaiLenh Loai;
        public short Tpl = -1;
        public int SoLuong = 1;        // -1 = het
        public int Cap = -1;
        public int Khu = -1;           // -1 = khu chinh (D79)
        public string ChoTen;
        public string TenGoi;
        public string TuKhoa;
        public int SoLenh = -1;
        public int Nguong = -1;
        public string Loi;

        /// <summary>
        /// Doc mot tin. Khong phai lenh (chat thuong) -> <see cref="LoaiLenh.Khong"/>. Sai cu phap ->
        /// <see cref="LoaiLenh.Loi"/> kem cach dung.
        /// </summary>
        public static LenhChat Doc(string tin)
        {
            var r = new LenhChat();
            string goc = ChuVan.BoTem(tin ?? "").Trim();
            string chuan = ChuVan.ChuanHoa(goc);
            if (chuan.Length == 0) return r;

            // Token GOC (giu hoa thuong cho ten nguoi) va token CHUAN (so khop tu khoa) phai cung so luong:
            // bo dau khong doi khoang trang.
            var tg = new List<string>(goc.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            var tc = new List<string>(chuan.Split(' '));
            if (tg.Count != tc.Count) tg = new List<string>(tc);

            switch (tc[0])
            {
                case "kho":
                    r.Loai = tc.Count == 1 ? LoaiLenh.Kho : LoaiLenh.Khong;
                    return r;
                case "nap":
                    r.Loai = tc.Count == 1 ? LoaiLenh.Nap : LoaiLenh.Khong;
                    return r;
                case "goi":
                    r.Loai = tc.Count == 1 ? LoaiLenh.Goi : LoaiLenh.Khong;
                    return r;
                case "xa":
                    // D86: "xa nhanh" - goi clone dung canh Leader truoc khi nap nhieu; `xa xong` = nap xong.
                    if (tc.Count == 1) r.Loai = LoaiLenh.Xa;
                    else if (tc.Count == 2 && tc[1] == "xong") r.Loai = LoaiLenh.XaXong;
                    else return Sai(r, "xa | xa xong");
                    return r;
                case "tim":
                    if (tc.Count < 2) return Sai(r, "tim <tu khoa>");
                    r.Loai = LoaiLenh.Tim;
                    r.TuKhoa = string.Join(" ", tc.GetRange(1, tc.Count - 1).ToArray());
                    return r;
                case "co":
                    if (tc.Count != 2 || !TpL(tc[1], r)) return Sai(r, "co <id>");
                    r.Loai = LoaiLenh.Co;
                    return r;
                case "theo":
                    if (tc.Count < 2 || tc.Count > 3 || !TpL(tc[1], r)) return Sai(r, "theo <id> [N]");
                    if (tc.Count == 3)
                    {
                        int n;
                        if (!int.TryParse(tc[2], out n) || n < 0) return Sai(r, "theo <id> [N]");
                        r.Nguong = n;
                    }
                    r.Loai = LoaiLenh.Theo;
                    return r;
                case "botheo":
                    if (tc.Count != 2 || !TpL(tc[1], r)) return Sai(r, "botheo <id>");
                    r.Loai = LoaiLenh.BoTheo;
                    return r;
                case "tiep":
                case "huy":
                    r.Loai = tc[0] == "tiep" ? LoaiLenh.Tiep : LoaiLenh.Huy;
                    if (tc.Count == 2)
                    {
                        int so;
                        if (!int.TryParse(tc[1].TrimStart('#'), out so) || so <= 0) return Sai(r, tc[0] + " [#so]");
                        r.SoLenh = so;
                    }
                    else if (tc.Count > 2) return Sai(r, tc[0] + " [#so]");
                    return r;
                case "lay":
                    return DocLay(r, tg, tc);
                default:
                    return r;   // chat thuong - khong tra loi
            }
        }

        private static LenhChat DocLay(LenhChat r, List<string> tg, List<string> tc)
        {
            // Mot tin rieng toi da 90 ky tu (KenhChat.DO_DAI_TOI_DA - 10) ke ca "Sai cu phap. Dung: ".
            const string CACH = "lay <id> [sl|het] [+cap] [khu N] [cho ten] | lay goi <ten>";
            const string CACH_GOI = "lay goi <ten> [khu N] [cho <ten>]";
            if (tc.Count < 2) return Sai(r, CACH);
            int i = 1;
            if (tc[1] == "goi")
            {
                if (tc.Count < 3) return Sai(r, CACH_GOI);
                r.Loai = LoaiLenh.LayGoi;
                r.TenGoi = tg[2];
                for (i = 3; i < tc.Count; i++)
                {
                    if (tc[i] == "khu")
                    {
                        if (r.Khu >= 0 || i + 1 >= tc.Count || !DocKhu(tc[i + 1], r)) return Sai(r, CACH_GOI);
                        i++;
                        continue;
                    }
                    if (tc[i] != "cho" || i + 1 >= tc.Count || i + 2 != tc.Count) return Sai(r, CACH_GOI);
                    r.ChoTen = tg[i + 1];
                    break;
                }
                return r;
            }

            if (!TpL(tc[1], r)) return Sai(r, CACH);
            r.Loai = LoaiLenh.Lay;
            bool coSo = false;
            for (i = 2; i < tc.Count; i++)
            {
                string t = tc[i];
                if (t == "cho")
                {
                    if (i + 1 >= tc.Count || i + 2 != tc.Count) return Sai(r, CACH);
                    r.ChoTen = tg[i + 1];
                    break;
                }
                if (t == "khu")
                {
                    if (r.Khu >= 0 || i + 1 >= tc.Count || !DocKhu(tc[i + 1], r)) return Sai(r, CACH);
                    i++;
                    continue;
                }
                if (t == "het")
                {
                    if (coSo) return Sai(r, CACH);
                    r.SoLuong = -1; coSo = true;
                    continue;
                }
                if (t.StartsWith("+"))
                {
                    int cap;
                    if (r.Cap >= 0 || !int.TryParse(t.Substring(1), out cap) || cap < 0 || cap > 16) return Sai(r, CACH);
                    r.Cap = cap;
                    continue;
                }
                int sl;
                if (coSo || !int.TryParse(t, out sl) || sl <= 0 || sl > 100000) return Sai(r, CACH);
                r.SoLuong = sl; coSo = true;
            }
            return r;
        }

        private static bool TpL(string s, LenhChat r)
        {
            short t;
            if (!short.TryParse(s, out t) || t < 0) return false;
            r.Tpl = t;
            return true;
        }

        /// <summary>Khu giao 0..<see cref="KHU_TOI_DA"/>.</summary>
        private static bool DocKhu(string s, LenhChat r)
        {
            int k;
            if (!int.TryParse(s, out k) || k < 0 || k > KHU_TOI_DA) return false;
            r.Khu = k;
            return true;
        }

        /// <summary>So khu lon nhat nhan trong lenh (Lang Tone co 30 khu, 0..29 - log 17/09).</summary>
        public const int KHU_TOI_DA = 99;

        private static LenhChat Sai(LenhChat r, string cach)
        {
            r.Loai = LoaiLenh.Loi;
            r.Loi = "Sai cu phap. Dung: " + cach;
            return r;
        }
    }
}
