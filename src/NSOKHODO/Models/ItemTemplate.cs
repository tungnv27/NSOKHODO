namespace NSOKHODO.Models
{
    public class ItemTemplate
    {
        public short Id { get; set; }
        public byte Type { get; set; }
        public byte Gender { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte Level { get; set; }
        public short IconId { get; set; }
        public short Part { get; set; }
        public bool IsUpToUp { get; set; }

        // Exact from game Item.java: isTypeBody() = type >= 0 && type <= 15
        public bool IsTypeBody
        {
            get { return Type >= 0 && Type <= 15; }
        }

        // Exact from game: isTypeMounts() = type >= 29 && type <= 33
        public bool IsTypeMount
        {
            get { return Type >= 29 && Type <= 33; }
        }

        // Exact from game: isTypeNgocKham() = type == 34
        public bool IsTypeNgocKham
        {
            get { return Type == 34; }
        }

        public bool HasUpgrade
        {
            get { return IsTypeBody || IsTypeMount || IsTypeNgocKham; }
        }

        // ===== Phan loai 3 nhom trong dai 0..15 - quyet dinh chon bang mau so / phi khi DAP DO.
        // Giong het o CA HAI nguon (NinjaSchool_251_src Item.cs va MODGAME Item.java).

        /// <summary>Y phuc: non(0) ao(2) gang(4) quan(6) giay(8) -> bang upClothe / coinUpClothes.</summary>
        public bool IsTypeClothe
        {
            get { return Type == 0 || Type == 2 || Type == 4 || Type == 6 || Type == 8; }
        }

        /// <summary>Trang suc: lien(3) nhan(5) ngoc boi(7) phu(9) -> bang upAdorn / coinUpAdorns.</summary>
        public bool IsTypeAdorn
        {
            get { return Type == 3 || Type == 5 || Type == 7 || Type == 9; }
        }

        /// <summary>Vu khi -> bang upWeapon / coinUpWeapons.</summary>
        public bool IsTypeWeapon
        {
            get { return Type == 1; }
        }

        /// <summary>Da nang cap (nguyen lieu dap/luyen).</summary>
        public bool IsTypeCrystal
        {
            get { return Type == 26; }
        }

        /// <summary>
        /// TRAN nang cap cua mon theo Level cua template (game Item.getUpMax()):
        /// 1-19 -> +4 | 20-39 -> +8 | 40-49 -> +12 | 50-59 -> +14 | >=60 -> +16.
        /// </summary>
        public int GetUpMax()
        {
            if (Level > 0 && Level < 20) return 4;
            if (Level >= 20 && Level < 40) return 8;
            if (Level >= 40 && Level < 50) return 12;
            if (Level >= 50 && Level < 60) return 14;
            return 16;
        }
    }
}
