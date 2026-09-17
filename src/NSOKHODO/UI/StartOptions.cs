using NSOKHODO.Config;

namespace NSOKHODO
{
    /// <summary>
    /// Cai dat TOAN CUC cho nut "Mo Game" (bat den nhieu dong dang chon).
    ///
    /// LUONG CU (mac dinh, <see cref="BatchEnabled"/> = false): mo LAN LUOT tung account,
    /// cach nhau <see cref="DELAY_MS"/> = 2s (FleetManager.StartStaggered) - khong nghen server.
    ///
    /// LUONG MOI (bat): moi luot mo CUNG LUC <see cref="BatchCount"/> account, cac luot van cach
    /// nhau 2s. Tat = ve nguyen luong cu (batch = 1).
    ///
    /// Persist vao Data/settings.txt qua <see cref="SettingsStore"/> (giu chung file voi
    /// <see cref="AutoCreateChar"/>). Dat o namespace goc NSOKHODO cho .UI/.Fleet dung chung.
    /// </summary>
    public static class StartOptions
    {
        /// <summary>Gian cach giua cac luot mo (giu nguyen mac dinh cu 2s cho ca 2 luong).</summary>
        public const int DELAY_MS = 2000;

        /// <summary>Bat mo NHIEU account cung luc (mac dinh TAT = luong cu tung con mot).</summary>
        public static bool BatchEnabled;

        /// <summary>So account mo cung luc moi luot (chi dung khi <see cref="BatchEnabled"/>).</summary>
        public static int BatchCount = 5;

        /// <summary>So account mo cung luc HIEU LUC: tat -> 1 (luong cu).</summary>
        public static int EffectiveBatch
        {
            get { return BatchEnabled ? (BatchCount > 0 ? BatchCount : 1) : 1; }
        }

        public static void Load()
        {
            var map = SettingsStore.LoadAll();
            BatchEnabled = SettingsStore.GetBool(map, "BatchOpen", false);
            int n = SettingsStore.GetInt(map, "BatchOpenCount", 5);
            if (n >= 1 && n <= 100) BatchCount = n;
        }

        public static void Save()
        {
            SettingsStore.Set(
                "BatchOpen", BatchEnabled ? "1" : "0",
                "BatchOpenCount", BatchCount.ToString());
        }
    }
}
