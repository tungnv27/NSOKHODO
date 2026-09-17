using NSOKHODO.Config;

namespace NSOKHODO
{
    /// <summary>
    /// Cai dat TOAN CUC cho tinh nang "Tu tao nhan vat neu chua co" (bat/tat + gioi tinh).
    /// Toggle toan cuc (nut tren toolbar) giong <see cref="NameMask"/> nen moi noi (MainForm,
    /// NsoClient) dung chung 1 trang thai. Dat o namespace goc NSOKHODO -> ca .UI lan .Client thay.
    ///
    /// Luc tao NV chi chon TEN + GIOI TINH + TOC (phai chon sau trong game qua nhiem vu, KHONG phai
    /// luc tao). Ten = username tai khoan (user chot). Toc = mac dinh theo gioi tinh (hairID[gender][0]
    /// tu MODGAME CreateCharScr: nam=2, nu=11).
    ///
    /// Persist nhe vao Data/settings.txt (key=value) de song qua restart (khac NameMask/log runtime-only).
    /// </summary>
    public static class AutoCreateChar
    {
        /// <summary>Bat tu dong tao NV khi server tra danh sach NV rong (mac dinh TAT).</summary>
        public static bool Enabled;

        /// <summary>Gioi tinh NV tao moi: 1 = Nam (mac dinh game), 0 = Nu.</summary>
        public static byte Gender = 1;

        /// <summary>Toc mac dinh theo gioi tinh = hairID[gender][0] (MODGAME CreateCharScr): nam=2, nu=11.</summary>
        public static byte HairFor(byte gender) { return gender == 0 ? (byte)11 : (byte)2; }

        public static string GenderText { get { return Gender == 0 ? "Nữ" : "Nam"; } }

        public static void Load()
        {
            var map = SettingsStore.LoadAll();
            Enabled = SettingsStore.GetBool(map, "AutoCreateChar", false);
            int g = SettingsStore.GetInt(map, "CreateCharGender", 1);
            if (g == 0 || g == 1) Gender = (byte)g;
        }

        public static void Save()
        {
            SettingsStore.Set(
                "AutoCreateChar", Enabled ? "1" : "0",
                "CreateCharGender", Gender.ToString());
        }
    }
}
