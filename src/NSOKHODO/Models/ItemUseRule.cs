namespace NSOKHODO.Models
{
    /// <summary>Ket luan cua <see cref="ItemUseRule.Kiem"/> — tuong ung tung nhanh cua ban goc.</summary>
    public enum ItemUseGate
    {
        /// <summary>Gui thang <c>cmd 11</c> (nhanh <c>else</c> cuoi cung cua ban goc).</summary>
        Duoc,
        /// <summary>Sai gioi tinh — ban goc chan, hien "Giới tính không phù hợp".</summary>
        SaiGioiTinh,
        /// <summary>Chua du cap — ban goc chan, hien "Trình độ của bạn chưa đạt yêu cầu".</summary>
        ChuaDuCap,
        /// <summary>Trang bi / thu cuoi CHUA KHOA — ban goc hoi xac nhan truoc khi gui cmd 11.</summary>
        CanXacNhanKhoa,
        /// <summary>Ve dich chuyen — phai chon diem den roi gui <c>cmd 12</c>, KHONG phai cmd 11.</summary>
        ChonDiemDen,
        /// <summary>Mon co luong rieng ma bot chua lam (thiep chuc Tet) — khong gui gi.</summary>
        KhongHoTro
    }

    /// <summary>
    /// Luat "duoc bam Dùng hay khong" — clone nguyen <c>GameScr.actBagUseItem()</c> cua client goc
    /// (`C:\Users\acer\Desktop\NINJAPC\NinjaSchool_251_src\GameScr.cs:20771-20821`).
    ///
    /// <para>Truoc 2026-09-12 nut "Dùng" cua ta <b>khong co cong loc nao</b>: bam la gui thang
    /// <c>cmd 11</c>. Hau qua thay duoc: mon sai gioi tinh / chua du cap thi server im lang → nguoi
    /// dung thay "bấm mãi không ăn thua"; con <b>ve dich chuyen (id 35/37) thi phai di
    /// <c>cmd 12</c></b>, gui cmd 11 vao do la sai lenh.</para>
    ///
    /// <para>Lop nay la LOGIC THUAN (khong tham chieu WinForms/Android) nen nam o
    /// <c>Models/</c> — ca ban PC lan ban APK deu tu lay theo glob, khong phai chep code.</para>
    /// </summary>
    public static class ItemUseRule
    {
        /// <summary>Hai template ve/bua dich chuyen — ban goc bat theo <b>id</b>, khong theo type.</summary>
        public const short VE_DICH_CHUYEN_A = 35;
        public const short VE_DICH_CHUYEN_B = 37;

        /// <summary>Thiep chuc Tet — ban goc mo hop nhap 2 dong, gui lenh rieng (ta chua lam).</summary>
        public const short THIEP_TET_A = 514;
        public const short THIEP_TET_B = 515;

        /// <summary>
        /// Danh sach diem den cua ve dich chuyen, <b>dung thu tu chi so gui trong cmd 12</b>.
        /// Ghep tu <c>mResources.TELEPORT</c>: 3 muc dau la 3 truong (chi so menu 0..2), 7 muc sau
        /// la lang (ban goc gui <c>menuSelectedItem + 3</c> nen ra 3..9).
        /// Doi chieu nguoc: <c>DapDoRunner.VE_DEST_LANG = 5</c> = "Làng Tone" — khop o dung vi tri 5.
        /// </summary>
        public static readonly string[] DiemDen =
        {
            "Trường Hirosaki",  // 0
            "Trường Haruna",    // 1
            "Trường Ookaza",    // 2
            "Làng Kojin",       // 3
            "Làng Sanzu",       // 4
            "Làng Tone",        // 5
            "Làng chài",        // 6
            "Làng Chakumi",     // 7
            "Làng Echigo",      // 8
            "Làng Oshin"        // 9
        };

        // Chuoi y nguyen ban goc (mResources.cs:1641/1643/1566/1704) - khong tu dat lai loi van.
        public const string MSG_SAI_GIOI_TINH = "Giới tính không phù hợp";
        public const string MSG_CHUA_DU_CAP = "Trình độ của bạn chưa đạt yêu cầu";
        public const string MSG_XAC_NHAN_KHOA = "Sau khi sử dụng vật phẩm sẽ bị khóa. Bạn có muốn sử dụng không?";
        public const string MSG_DA_KHOA = "Vật phẩm đã bị khóa";
        public const string MSG_CHUA_HO_TRO = "Món này cần hộp nhập lời chúc — bản bot chưa làm, hãy dùng bằng client game.";

        /// <summary>
        /// Xet mot mon trong TUI theo dung thu tu nhanh cua <c>actBagUseItem()</c>. Thu tu quan
        /// trong: ban goc kiem gioi tinh TRUOC cap, va kiem ca hai TRUOC khi hoi khoa do.
        /// </summary>
        /// <param name="thongBao">Chuoi de hien cho nguoi dung; rong khi ket qua la <see cref="ItemUseGate.Duoc"/>.</param>
        public static ItemUseGate Kiem(Item it, ItemTemplate tpl, CharacterState c, out string thongBao)
        {
            thongBao = string.Empty;
            if (it == null || it.IsEmpty || tpl == null || c == null) return ItemUseGate.Duoc;

            // 1) gender != 2 (unisex) && gender != gioi tinh nhan vat  -> chan
            if (tpl.Gender != 2 && tpl.Gender != c.Gender)
            {
                thongBao = MSG_SAI_GIOI_TINH;
                return ItemUseGate.SaiGioiTinh;
            }

            // 2) template.level > cap nhan vat -> chan
            if (tpl.Level > c.Level)
            {
                thongBao = MSG_CHUA_DU_CAP;
                return ItemUseGate.ChuaDuCap;
            }

            // 3) trang bi / thu cuoi CHUA khoa -> hoi xac nhan (dung xong khoa vinh vien)
            if ((tpl.IsTypeBody || tpl.IsTypeMount) && !it.IsLock)
            {
                thongBao = MSG_XAC_NHAN_KHOA;
                return ItemUseGate.CanXacNhanKhoa;
            }

            // 4) ve dich chuyen -> chon diem den roi gui cmd 12
            if (tpl.Id == VE_DICH_CHUYEN_A || tpl.Id == VE_DICH_CHUYEN_B)
                return ItemUseGate.ChonDiemDen;

            // 5) thiep chuc Tet -> luong rieng, ta chua lam
            if (tpl.Id == THIEP_TET_A || tpl.Id == THIEP_TET_B)
            {
                thongBao = MSG_CHUA_HO_TRO;
                return ItemUseGate.KhongHoTro;
            }

            return ItemUseGate.Duoc;
        }
    }
}
