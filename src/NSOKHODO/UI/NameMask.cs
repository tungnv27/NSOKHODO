namespace NSOKHODO
{
    /// <summary>
    /// Che giau ten tai khoan / nhan vat tren UI. Toggle TOAN CUC (nut "Ẩn tên" tren header)
    /// nen moi noi (MainForm, InventoryForm, CopyConfigForm, NsoClient log...) dung chung 1 trang thai.
    /// Dat o namespace goc NSOKHODO -> ca .UI lan .Client deu thay khong can using.
    /// Chi anh huong HIEN THI - khong doi du lieu luu.
    /// </summary>
    public static class NameMask
    {
        /// <summary>Bat che ten (false = hien binh thuong, mac dinh).</summary>
        public static bool Enabled;

        /// <summary>"barbig1011" -> "ba****11". Giu 2 dau + 2 cuoi. Rong/null hoac dang tat -> nguyen van.</summary>
        public static string Apply(string s)
        {
            if (!Enabled || string.IsNullOrEmpty(s)) return s;
            if (s.Length <= 2) return "****";
            if (s.Length <= 4) return s.Substring(0, 2) + "****";
            return s.Substring(0, 2) + "****" + s.Substring(s.Length - 2);
        }
    }
}
