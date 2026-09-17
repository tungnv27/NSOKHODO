namespace NSOKHODO.Client
{
    public class AccountConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public int ServerIndex { get; set; }   // Legacy: vi tri trong danh sach cu (dung khi thieu ServerName)
        public string ServerName { get; set; }  // Dinh danh server BEN voi danh sach doi thu tu (uu tien hon ServerIndex)
        public string CharName { get; set; }
        public string Proxy { get; set; } // SOCKS5: host:port:user:pass
        public string ChatTemplate { get; set; } // Chat template, {rnd} = random 000-999

        // ===== CONG TAC AUTO =====
        // Ban nay CHI CO MOT MODE (StandMode: di toi khu -> dung -> bao TA/TL) nen khong co
        // dropdown "Che do" nhu NSOLITEPRO. Mot cong tac bat/tat la du.
        /// <summary>MASTER ENABLE: bat thi acc di toi map/khu dich, dung do va bao TA/TL.</summary>
        public bool BatBao { get; set; }
        public bool AutoReconnect { get; set; }
        public int ReconnectDelay { get; set; }
        // MapDelay DA GO 2026-09-06 -> chuyen thanh TrainConfig.NextMapDelayMs (o "Toc do NextMap"
        // tab Train). Ly do: field cu CO that va Navigator CO doc, nhung khong co o nhap tren giao
        // dien va ConfigManager khong he serialize no => vinh vien ket o 500ms. Dung them lai o day.

        /// <summary>
        /// Gia tri dac biet cua <see cref="TargetZoneId"/>: "MOI KHU" - toi map la train luon, KHONG
        /// doi khu. User nhap -1 tren UI (tab Train, o "Khu") -> luu 255; lenh nhom chat gui -1.
        /// Byte 255 khong phai khu that (server chi co vai chuc khu) nen dung lam co an toan; giu
        /// kieu byte de KHONG pha dinh dang accounts.txt / ConfigCopy cu.
        /// Luu y: khu 0 van giu nghia CU (khong ep doi khu) - khong dung tinh nang nay.
        /// </summary>
        public const byte ANY_ZONE = 255;

        // Target map for AFK
        public int TargetMapId { get; set; }
        public byte TargetZoneId { get; set; }
        public short TargetX { get; set; }
        public short TargetY { get; set; }
        public bool AutoRemap { get; set; }

        // Cau hinh chi tiet (map/khu dich, danh sach acc chinh, nguong binh...).
        // BatBao (tren) la cong tac bat/tat; Train chua tham so.
        public TrainConfig Train { get; set; }

        // ===================== KHOA BO DEM TOC DO (RateTracker) =====================
        private static int _rateSeq;
        private readonly string _rateKey =
            "acc#" + System.Threading.Interlocked.Increment(ref _rateSeq).ToString();

        /// <summary>
        /// Khoa DUY NHAT theo DONG cua bang tai khoan, chi song trong mot lan chay tool.
        ///
        /// <para><b>KHONG duoc dung Username lam khoa <see cref="RateTracker"/>:</b> accounts.txt
        /// cho phep MOT tai khoan nam tren NHIEU dong (user that su co hai dong "barbigz999",
        /// phat hien 2026-09-09). Hai dong do dung chung mot o dem: moi giay luoi duyet ca hai,
        /// dong dang TAT auto (hoac OFFLINE) goi <c>Clear()</c> con dong dang BAT goi
        /// <c>Sample()</c> ngay sau => moc bi dat lai lien tuc => "Da chay: 0s" vinh vien.</para>
        ///
        /// <para>Song theo DOI TUONG nay chu khong theo vong doi client/mode, nen chet - hoi sinh
        /// va rot mang - vao lai deu GIU nguyen moc (dung y do ban dau).</para>
        /// </summary>
        public string RateKey { get { return _rateKey; } }

        public AccountConfig()
        {
            Train = new TrainConfig();
            ServerIndex = 6;
            AutoReconnect = true;
            AutoRemap = false;
            ReconnectDelay = 5000;
            TargetMapId = -1;
        }

        public override string ToString()
        {
            return string.Format("{0}@{1}", Username, ServerIndex);
        }

        // ===== Proxy helpers =====
        // Nguoi dung luon nhap/thay proxy dang tron "host:port:user:pass" + chon loai qua dropdown.
        // Noi bo nho loai bang tien to: HTTP -> "http://" + bare; SOCKS5 (mac dinh) -> luu tron
        // (khong tien to) de tuong thich nguoc voi proxy cu da luu.

        /// <summary>Tach chuoi proxy da luu thanh (isHttp, bare = host:port:user:pass khong tien to).</summary>
        public static void SplitProxy(string stored, out bool isHttp, out string bare)
        {
            isHttp = false;
            bare = (stored ?? "").Trim();
            int i = bare.IndexOf("://", System.StringComparison.OrdinalIgnoreCase);
            if (i > 0)
            {
                string scheme = bare.Substring(0, i).ToLowerInvariant();
                isHttp = scheme == "http" || scheme == "https";
                bare = bare.Substring(i + 3);
            }
        }

        /// <summary>Ghep loai + bare thanh chuoi luu. HTTP -> "http://" + bare; SOCKS5 -> bare tron.</summary>
        public static string JoinProxy(bool isHttp, string bare)
        {
            bare = (bare ?? "").Trim();
            if (bare.Length == 0) return "";
            return isHttp ? "http://" + bare : bare;
        }

        /// <summary>IP/host cua proxy de hien thi (bo tien to + bo port/user/pass). Rong neu khong co proxy.</summary>
        public static string ProxyHost(string stored)
        {
            bool isHttp; string bare;
            SplitProxy(stored, out isHttp, out bare);
            if (bare.Length == 0) return "";
            int c = bare.IndexOf(':');
            return c > 0 ? bare.Substring(0, c) : bare;
        }
    }
}
