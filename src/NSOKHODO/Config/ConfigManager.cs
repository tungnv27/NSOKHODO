using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NSOKHODO.Client;

namespace NSOKHODO.Config
{
    public static class ConfigManager
    {
        // Duong dan qua AppPaths (gom vao Data/). EnsureReady() goi o Program.Main da di tru
        // accounts.txt cu canh exe vao Data/ truoc khi den day.
        private static string ConfigPath { get { return AppPaths.Accounts; } }

        /// <summary>
        /// Khac <c>null</c> = lan <see cref="Load"/> gan nhat DOC KHONG DUOC file (giai ma hong,
        /// file cut...). <see cref="Save"/> se TU CHOI ghi khi co nay bat - neu khong, mot lan doc
        /// hong se bien thanh ghi de danh sach RONG len file that, mat sach tai khoan.
        /// </summary>
        public static string LoadError { get; private set; }

        // ============================ DINH DANG accounts.txt ============================
        // GIU NGUYEN 23 cot pipe-delimited cua NSOLITEPRO - co chu dich:
        // user co san hang tram acc da cau hinh ben do, chep accounts.txt sang la chay duoc ngay.
        //
        // Nhung cot cua tinh nang ban nay KHONG CO (nhat do: nhom, nhat do, PK Am, dropdown Che do)
        // van duoc GIU CHO: doc thi bo qua, ghi thi ghi "0". Nho vay file di lai duoc CA HAI CHIEU
        // ma khong ben nao lam hong cau hinh cua ben kia.
        //
        // ⚠️ Them field moi thi APPEND O CUOI (cot 23+), dung chen giua - se xe dich toan bo.
        // Cai dat rieng cua tinh nang bao -> them vao TrainConfig (cot 19, dang "key=value;...").
        public static AppConfig Load()
        {
            var config = new AppConfig();
            LoadError = null;
            if (!File.Exists(ConfigPath))
                return config;

            string[] lines;
            try
            {
                string raw = File.ReadAllText(ConfigPath, Encoding.UTF8);
                if (AccountCrypto.IsEncrypted(raw))
                {
                    string plain = AccountCrypto.Decrypt(raw);
                    if (plain == null)
                    {
                        // KHONG tra config rong am tham: buoc goi tiep theo thuong la ghi de.
                        LoadError = "Không giải mã được " + Path.GetFileName(ConfigPath)
                                  + " (file hỏng hoặc do bản tool khác tạo).";
                        return config;
                    }
                    lines = plain.Replace("\r\n", "\n").Split('\n');
                }
                else
                {
                    // File plaintext ban cu -> van doc binh thuong. Lan Save ke tiep tu ma hoa lai.
                    lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                LoadError = "Không đọc được " + Path.GetFileName(ConfigPath) + ": " + ex.Message;
                return config;
            }

            try
            {
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    string[] p = line.Split('|');
                    if (p.Length < 3) continue;

                    var a = new AccountConfig();
                    a.Username = p[0].Trim();
                    a.Password = p[1].Trim();
                    int idx;
                    if (int.TryParse(p[2].Trim(), out idx)) a.ServerIndex = idx;
                    if (p.Length > 3) a.CharName = p[3].Trim();
                    if (p.Length > 4) a.AutoReconnect = p[4].Trim() == "1";
                    // p[5] = legacy autoAttack · p[6] = legacy autoPickItem - bo qua (xem chu thich dau ham)
                    if (p.Length > 7) a.AutoRemap = p[7].Trim() == "1";
                    if (p.Length > 8) { int m; if (int.TryParse(p[8].Trim(), out m)) a.TargetMapId = m; }
                    // Khu: luu 255 = ANY_ZONE ("moi khu"). Nhan them "-1" cho file user sua tay.
                    if (p.Length > 9)
                    {
                        int z;
                        if (int.TryParse(p[9].Trim(), out z))
                        {
                            if (z < 0) a.TargetZoneId = AccountConfig.ANY_ZONE;
                            else if (z <= 255) a.TargetZoneId = (byte)z;
                        }
                    }
                    if (p.Length > 10) { short x; if (short.TryParse(p[10].Trim(), out x)) a.TargetX = x; }
                    if (p.Length > 11) { short y; if (short.TryParse(p[11].Trim(), out y)) a.TargetY = y; }
                    // p[12..15] = nhom (party) - ban nay khong co auto nhom, bo qua
                    if (p.Length > 16) a.Proxy = p[16].Trim();
                    // p[17] = legacy autoAFK (da bo) - bo qua
                    // p[18]: ben NSOLITEPRO la "autoTanSat"; o day doc thanh BatBao. Cung mot y
                    // nghia (cong tac bat/tat he auto) nen accounts.txt dung chung duoc ca hai ben.
                    if (p.Length > 18) a.BatBao = p[18].Trim() == "1";
                    // Field 19 = TrainConfig (1 field, dang "key=value;..."). Them field Train sau
                    // KHONG lam vo format: file cu thieu field nay -> dung default TrainConfig.
                    if (p.Length > 19 && !string.IsNullOrEmpty(p[19].Trim()))
                        a.Train = TrainConfig.Parse(p[19].Trim());
                    // Field 20 = AutoPkAm ben NSOLITEPRO - ban nay khong co PK Am, bo qua.
                    // Field 21 = ServerName (dinh danh ben). File cu thieu -> dich tu ServerIndex legacy
                    // o lan Resolve dau (giu nguyen, khong ep o day de tranh phu thuoc ServerList khi load).
                    if (p.Length > 21) a.ServerName = p[21].Trim();
                    // Field 22 = AutoMode (dropdown "Che do") ben NSOLITEPRO. Ban nay CHI CO MOT
                    // mode nen bo qua - nhung VAN GIU CHO TRONG khi ghi (xem Save) de file di lai
                    // duoc giua hai tool.

                    config.Accounts.Add(a);
                }
            }
            catch { }
            return config;
        }

        public static void Save(AppConfig config)
        {
            // Doc hong ma van ghi = ghi de danh sach RONG len file that. Tha khong luu con hon.
            if (LoadError != null) return;

            var sb = new StringBuilder();
            sb.AppendLine("# NSOKHODO Config");
            // Cot 5 (attack) va 17 (autoAFK) la legacy (da bo) - giu "0" de khong xe dich vi tri cot.
            sb.AppendLine("# user|pass|server|char|reconnect|_legacy0|_legacy0|remap|mapId|zone|x|y|_legacy0|_legacy0|_legacy|_legacy|proxy|_legacy0|batBao|trainCfg|_legacy0|serverName|_legacy0");

            foreach (var a in config.Accounts)
            {
                // Field 19: TrainConfig dang "key=value;...". Khong chua '|' -> an toan voi pipe.
                string trainJson = (a.Train ?? new TrainConfig()).Serialize();

                sb.AppendFormat("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}|{19}|{20}|{21}|{22}",
                    a.Username, a.Password, a.ServerIndex, a.CharName ?? "",
                    a.AutoReconnect ? "1" : "0",
                    "0", // legacy attack
                    "0", // legacy pickItem
                    a.AutoRemap ? "1" : "0",
                    a.TargetMapId, a.TargetZoneId,
                    a.TargetX, a.TargetY,
                    "0", "0", "", "",   // legacy: 4 cot nhom (party/leader/leaderName/members)
                    a.Proxy ?? "",
                    "0", // legacy autoAFK
                    a.BatBao ? "1" : "0",
                    trainJson,
                    "0", // legacy autoPkAm
                    a.ServerName ?? "",
                    "0"); // legacy autoMode
                sb.AppendLine();
            }

            // Ghi ATOMIC: ra file .tmp roi thay the -> crash/mat dien GIUA luc ghi khong cat cut
            // accounts.txt (mat sach vai tram acc). File.Replace giu ban cu thanh .bak.
            try { Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)); } catch { }
            string tmp = ConfigPath + ".tmp";
            string bak = ConfigPath + ".bak";
            // LUON ma hoa khi ghi -> ban .bak sinh ra tu File.Replace cung la ban ma hoa, khong con
            // mat khau tho nam lai tren dia (xem docs/features/MA_HOA_PASS.md).
            File.WriteAllText(tmp, AccountCrypto.Encrypt(sb.ToString()), Encoding.UTF8);
            if (File.Exists(ConfigPath))
            {
                // Replace = doi cho nguyen tu (atomic tren cung volume) + sao luu ban cu sang .bak.
                File.Replace(tmp, ConfigPath, bak);

                // Lan dau chuyen tu file CHU THO sang ma hoa, ban .bak vua sinh ra VAN la chu tho
                // -> mat khau con nguyen mot ban tren dia, dung cai ta muon diet. Ma hoa lai no
                // (giu duoc duong lui, khong con plaintext). Xem docs/features/MA_HOA_PASS.md.
                try
                {
                    if (File.Exists(bak))
                    {
                        string old = File.ReadAllText(bak, Encoding.UTF8);
                        if (!AccountCrypto.IsEncrypted(old))
                            File.WriteAllText(bak, AccountCrypto.Encrypt(old), Encoding.UTF8);
                    }
                }
                catch { }
            }
            else
            {
                File.Move(tmp, ConfigPath);
            }
        }
    }
}
