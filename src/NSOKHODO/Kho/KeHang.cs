using System;
using NSOKHODO.Models;

namespace NSOKHODO.Kho
{
    /// <summary>
    /// KE HANG (SPEC §6.1, D32). Moi clone thuoc mot ke; moi mon thuoc mot nhom -> mon di ve clone
    /// cung ke, nen rut do thuong chi can mot clone va do cung loai don ve mot cho (cong don duoc).
    ///
    /// <para>Chi DOAN theo ba khoang <c>ItemTemplate.Type</c> co nguon (0–15 trang bi, 29–33 thu cuoi,
    /// 34 ngoc kham) va co <c>IsUpToUp</c> (xep chong). Ngoai ra ve "Khac" — dung doan them.</para>
    /// </summary>
    public static class KeHang
    {
        public const string TRANG_BI = "TrangBi";
        public const string THU_NGOC = "ThuNgoc";
        public const string CHONG = "Chong";
        public const string RAC = "Rac";
        public const string KHAC = "Khac";

        public static readonly string[] TatCa = { TRANG_BI, THU_NGOC, CHONG, RAC, KHAC };

        public static string TenHienThi(string ke)
        {
            switch (ke)
            {
                case TRANG_BI: return "Trang bị";
                case THU_NGOC: return "Thú cưỡi & ngọc";
                case CHONG: return "Xếp chồng";
                case RAC: return "Rác";
                case KHAC: return "Khác";
                default: return string.IsNullOrEmpty(ke) ? "(chưa gán)" : ke;
            }
        }

        public static string TuTenHienThi(string ten)
        {
            foreach (var k in TatCa)
                if (string.Equals(TenHienThi(k), ten, StringComparison.OrdinalIgnoreCase)) return k;
            return ten;
        }

        /// <summary>
        /// Nhom cua mot template. Thu tu uu tien: danh dau RAC &gt; ke user gan tay &gt; doan theo Type.
        /// </summary>
        public static string NhomCuaMon(short tpl, ItemTemplate t, KhoConfig cfg)
        {
            if (cfg != null && cfg.LaRac(tpl)) return RAC;
            string tay = cfg != null ? cfg.KeCuaMon(tpl) : null;
            if (!string.IsNullOrEmpty(tay)) return tay;
            if (t == null) return KHAC;
            if (t.IsTypeBody) return TRANG_BI;
            if (t.IsTypeMount || t.IsTypeNgocKham) return THU_NGOC;
            if (t.IsUpToUp) return CHONG;
            return KHAC;
        }

        /// <summary>Ke cua mot acc; chua gan thi coi la "Khac" (nhan moi thu con lai).</summary>
        public static string KeCuaAcc(string username, KhoConfig cfg)
        {
            string k = cfg != null ? cfg.KeCuaAcc(username) : null;
            return string.IsNullOrEmpty(k) ? KHAC : k;
        }
    }
}
