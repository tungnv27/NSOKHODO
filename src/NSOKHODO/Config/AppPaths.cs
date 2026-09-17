using System;
using System.IO;

namespace NSOKHODO.Config
{
    /// <summary>
    /// Nguon chan ly DUY NHAT cho moi duong dan file runtime (file APP GHI).
    /// Gom het vao 1 thu muc "Data/" CANH exe (KHONG dung %AppData% de giu portable:
    /// copy ca cum exe + Data + Maps len VPS la chay).
    ///
    /// - Data/ (MOI): file app ghi -> accounts.txt, presets.txt, wp_cache.txt, nsolite.log...
    /// - Maps/ (giu nguyen canh exe): content chi-doc ship kem ban phan phoi, KHONG phai state.
    ///
    /// Truoc day moi file tu Path.Combine(BaseDirectory,...) rai rac, rieng log dung ten tran
    /// theo CWD (lech khi khoi dong tu cho khac). Tap trung o day -> het lan quy uoc.
    /// </summary>
    public static class AppPaths
    {
        // Mac dinh: canh exe (ban PC portable). Ban Android GHI DE bang UseBaseDir() ngay dau
        // MainActivity/BotService - vi thu muc cai dat cua APK KHONG ghi duoc.
        private static string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Doi goc luu tru (chi ban Android goi, TRUOC EnsureReady). Ban PC khong goi -> giu
        /// nguyen hanh vi portable canh exe.
        /// </summary>
        public static void UseBaseDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            BaseDir = dir;
            _dataDir = null;
            _ready = false;
        }

        private static string _dataDir;

        /// <summary>Thu muc gom moi file app ghi: &lt;exe&gt;/Data/ (PC) hoac FilesDir/Data/ (Android).</summary>
        public static string DataDir
        {
            get { return _dataDir ?? (_dataDir = Path.Combine(BaseDir, "Data")); }
        }

        /// <summary>Ten danh sach dung khi user chua chon gi (file di tru tu accounts.txt cu).</summary>
        public const string DEFAULT_LIST = "Mặc định";

        private static string _listName = DEFAULT_LIST;

        /// <summary>
        /// Thu muc chua CAC danh sach tai khoan. Moi danh sach = 1 file .txt, doi qua lai duoc
        /// (xem docs/features/DANH_SACH_ACC.md). Nguoi dung chi dat TEN, tool tu quan cho luu.
        /// </summary>
        public static string AccountsDir { get { return Path.Combine(DataDir, "Accounts"); } }

        /// <summary>Ten danh sach dang mo.</summary>
        public static string ListName { get { return _listName; } }

        /// <summary>
        /// Doi danh sach dang mo. Goi TRUOC khi nap acc va TRUOC khi mo file log (MainForm mo log
        /// trong BuildUi nen thu tu nay an toan).
        /// </summary>
        public static void UseList(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _listName = SafeName(name);
        }

        /// <summary>
        /// Bo cac ky tu khong dat duoc ten file Windows. Ten danh sach do user go nen khong tin
        /// duoc: mot dau '/' la file rot sang thu muc khac, hoac ghi de file khac.
        /// </summary>
        public static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return DEFAULT_LIST;
            var sb = new System.Text.StringBuilder(name.Length);
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in name)
                sb.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
            string s = sb.ToString().Trim().TrimEnd('.');
            return s.Length == 0 ? DEFAULT_LIST : s;
        }

        /// <summary>File acc cua danh sach DANG MO.</summary>
        public static string Accounts { get { return AccountsOf(_listName); } }

        /// <summary>File acc cua mot danh sach bat ky.</summary>
        public static string AccountsOf(string listName)
        {
            return Path.Combine(AccountsDir, SafeName(listName) + ".txt");
        }

        /// <summary>File khoa danh sach (chua PID cua tien trinh dang mo no).</summary>
        public static string LockOf(string listName)
        {
            return AccountsOf(listName) + ".lock";
        }
        public static string Presets { get { return Path.Combine(DataDir, "presets.txt"); } }
        /// <summary>Cai dat toan cuc (key=value): AutoCreateChar, CreateCharGender...</summary>
        public static string Settings { get { return Path.Combine(DataDir, "settings.txt"); } }
        /// <summary>Cache danh sach may chu lay tu URL chinh thuc (NJVI.txt raw).</summary>
        public static string Servers { get { return Path.Combine(DataDir, "servers.txt"); } }
        /// <summary>May chu nguoi dung tu them (moi dong Name:IP:Port:serverLogin:type).</summary>
        public static string ServersCustom { get { return Path.Combine(DataDir, "servers_custom.txt"); } }
        public static string WpCache { get { return Path.Combine(DataDir, "wp_cache.txt"); } }
        /// <summary>Level quai HOC DUOC theo map (mapId=lv,lv,...) - xem GameData/MapLevels.cs.</summary>
        public static string MapLevelsLearned { get { return Path.Combine(DataDir, "map_levels.txt"); } }

        /// <summary>
        /// Bang dem mat ket noi - THEO TUNG DANH SACH acc (xem <c>Fleet.DisconnectStats</c>).
        /// Gan ten danh sach vao vi mo 3 cua so, moi cua so mot nhom acc, thi 3 bo so lieu do phai
        /// tach nhau; chung mot file la ba tien trinh ghi de len nhau.
        /// </summary>
        public static string ThongKeDisconnect
        {
            get { return Path.Combine(DataDir, "disconnect-" + SafeName(_listName) + ".txt"); }
        }
        // Log tach theo danh sach: mo NHIEU cua so cung luc thi moi cua so mot file, khong hai
        // tien trinh cung ghi de len mot file (xem DANH_SACH_ACC.md §C).
        public static string Log { get { return Path.Combine(DataDir, "baotatl-" + SafeName(_listName) + ".log"); } }
        public static string OldLog { get { return Path.Combine(DataDir, "baotatl-" + SafeName(_listName) + ".old.log"); } }

        private static bool _ready;

        /// <summary>
        /// Goi 1 lan luc khoi dong (Program.Main). Tao thu muc Data/ + di tru file cu tu canh
        /// exe vao Data/ (CHI khi ban moi chua co -> khong de len ban da co). Khong nem loi:
        /// hong di tru thi app van chay (chi mat lich su, khong crash).
        /// </summary>
        public static void EnsureReady()
        {
            if (_ready) return;
            _ready = true;
            try { Directory.CreateDirectory(DataDir); } catch { }
            try { Directory.CreateDirectory(AccountsDir); } catch { }

            // Di tru file cu (NSOKHODO doi truoc khi co Data/) tu canh exe -> Data/.
            MigrateLegacy("accounts.txt", Path.Combine(DataDir, "accounts.txt"));
            MigrateLegacy("wp_cache.txt", WpCache);
            MigrateLegacy("nsolite.log", Log);
            MigrateLegacy("nsolite.old.log", OldLog);

            // Di tru MOT file accounts.txt cu -> danh sach "Mac dinh" trong Accounts/.
            // Chi chuyen khi dich CHUA co: chay ban moi roi quay lai ban cu roi lai len ban moi
            // thi khong de ban cu ghi de ban da dung.
            MigrateLegacy(Path.Combine(DataDir, "accounts.txt"), AccountsOf(DEFAULT_LIST), true);
        }

        /// <param name="absolute"><c>true</c> = <paramref name="legacyName"/> da la duong dan day
        /// du (dung cho buoc accounts.txt trong Data/ -> Accounts/), <c>false</c> = ten file canh exe.</param>
        private static void MigrateLegacy(string legacyName, string newPath, bool absolute = false)
        {
            try
            {
                string legacy = absolute ? legacyName : Path.Combine(BaseDir, legacyName);
                if (File.Exists(legacy) && !File.Exists(newPath))
                    File.Move(legacy, newPath);
            }
            catch { }
        }
    }
}
