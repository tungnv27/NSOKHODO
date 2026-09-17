using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NSOKHODO.Config;
using NSOKHODO.Models;

namespace NSOKHODO.GameData
{
    /// <summary>
    /// Level quái theo map — dùng cho hộp thoại "Bản đồ" (docs/features/BAN_DO.md).
    /// KHÔNG hiện ở dropdown tab Train (user chốt 2026-09-09: nhãn dài quá), chỉ hiện trong
    /// hộp thoại bản đồ.
    ///
    /// Gồm HAI tầng, tra theo thứ tự hợp nhất:
    ///
    /// 1. <b>Bảng tĩnh</b> — chép NGUYÊN VĂN 2 chuỗi <c>TABLE</c> + <c>TABLE_MQ</c> của
    ///    <c>NSOTRUNGDUC\src\ModUpLv.java:76-100</c> (bản chưa commit, sửa 2026-08-27 — MỚI hơn
    ///    <c>MODLV.xlsx</c> đã commit, mà file xlsx đó còn tính nhầm boss thành quái thường ở 3 map).
    ///    Gốc dữ liệu là <c>database.sql</c> của server NSOKISS (bảng <c>monster</c> nối cột
    ///    <c>map.monster</c>), ĐÃ LỌC SẴN: bỏ boss, bỏ quái lv 0, bỏ quái lạc bầy.
    ///
    /// 2. <b>Bảng tự học</b> — mỗi lần bot vào map thì ghi lại level quái NHÌN THẤY THẬT trên
    ///    server này (<see cref="Observe"/>, gọi từ <c>MapHandler</c>), lưu ở
    ///    <c>Data/map_levels.txt</c>. Vì sao cần: bảng tĩnh là của server KHÁC và thiếu 8 map hang
    ///    cấp cao vào bằng NPC (91, 94, 105, 114, 125, 157-159).
    ///
    /// <para>Đã kiểm chứng bảng tĩnh với chính server này 2/2:
    /// map 55 → Lv 92 (khớp log <c>tpl=136 lv=92</c>), map 41 → Lv 51, 52 (khớp log
    /// <c>tpl=51 lv=51</c>, <c>tpl=52 lv=52</c>). Và 71/71 tên map khớp <c>UI/MapNames.cs</c>,
    /// nên id hai bên cùng một hệ.</para>
    ///
    /// <para>HỢP NHẤT chứ không ghi đè: kết quả là HỢP của hai tầng. Lý do — một lần vào map chỉ
    /// thấy được đám quái đang sống, nếu để bản học ghi đè thì map có 3 loại quái mà lúc đó chết
    /// mất 1 loại sẽ bị xén mất level. Hợp lại thì chỉ có thêm, không bao giờ mất.</para>
    /// </summary>
    public static class MapLevels
    {
        /// <summary>
        /// Bảng map LƯỜNG THƯỜNG. Định dạng <c>mapId:lv,lv,...:TênMap</c>, ngăn bằng <c>;</c>.
        /// Phần tên map CHỈ để đối chiếu nguồn — code KHÔNG BAO GIỜ đọc nó (tên map lấy ở
        /// <c>UI/MapNames.cs</c>). Giữ nguyên để sau này diff được với file gốc.
        /// </summary>
        private const string TABLE =
            "21:3,5,6:Đồi Fumimen;23:3,5,8:Vách Ichidai;69:3,5,6:Vách Ainodake;2:5,6,8:Khu luyện tập;"
            + "6:5,6,8:Thác Kitajima;25:5,6,8:Đồi Kokoro;70:5,6,8:Thung lũng chết;71:5,6,8:Rừng già;"
            + "20:6,8:Chân thác Kitajima;26:6,8:Cánh đồng Fuki;3:10:Đồng Hachi;28:10:Ký túc xá Haruna;"
            + "39:10,11,12:Sông băng Yamato;60:10,11:Cửa hang Aka;4:11,12:Rừng đào Sakura;46:12:Hồ Stuki;"
            + "5:13,14:Rừng trúc Utra;29:13,14:Hang Aka;40:15,17:Cánh đồng Hiya;7:16,17,19:Rừng Mishima;"
            + "30:16,18:Suối Akagi;65:17,19:Mũi Nuranura;31:18,19,20:Bờ biển Oura;"
            + "9:19,20,21,22:Nghĩa địa Izuko;8:20,21:Sông Watamaro;63:23,28:Hang Ha;47:24,25:Hẻm núi Takana;"
            + "11:25,27:Miếu Kamo;33:25,26,27:Rừng Moshio;61:25,26:Cửa biển Kawaguchi;50:26,27:Rừng Kanashii;"
            + "12:28,30:Miếu Oboko;49:28,29:Đền Amaterasu;51:30,32:Rừng Toge;34:31,32:Đảo Hebi;"
            + "57:31,32:Sân sau Miếu Oboko;35:32,33:Hang Meiro;13:34,37:Rừng gỗ Kouji;"
            + "66:34,36:Khe núi Chorochoro;52:35,38:Rừng Kappa;64:39,40:Hang Kugyou;14:41,42:Rừng Aokigahara;"
            + "15:43,44:Vách núi Ito;67:45,46:Núi Ontake;16:47,50:Thung lũng Taira;68:48,49:Núi Anzen;"
            + "41:51,52:Khu đá đỏ Akai;42:53,55:Khu đá đỏ Aiko;62:56,57:Hang Chi;44:58,59:Đỉnh Okama;"
            + "18:60,61:Sân đền Orochi;24:62,65:Đỉnh Ichidai;59:63,64:Mũi Hone;45:66,67:Hang núi Kurai;"
            + "53:66,67,68:Động Tamatamo;19:71:Ngôi đền Orochi;36:74,77:Đồng Kisei;54:80:Đền Harumoto;"
            + "37:84,88:Núi Hashigoto;55:92:Phong ấn Ounio;58:96,100:Sân sau đền Orochi";

        /// <summary>Bảng map VÙNG ĐẤT MA QUỶ (hành lang 139 → 148). Cùng định dạng.</summary>
        private const string TABLE_MQ =
            "139:64,68:Quỷ Sơn;140:74,77:Sơn Hải Vực;141:83,85,88:Đoạn Sơn;142:85,92,96:Đảo Quỷ;"
            + "143:103,107:Sinh Tử Kiều;144:114,117:Nhân Duyên Lộ;145:123,126:Hoang Trấn;"
            + "146:131,137:Mài Tâm Lộ;147:142,148:Bát Thụ Hoang Lâm;148:153,159:Cửu Mộc Hoàng Kiều";

        private static readonly object _lock = new object();

        /// <summary>Bảng tĩnh, dựng một lần lúc nạp lớp. Sau đó CHỈ ĐỌC.</summary>
        private static readonly Dictionary<int, int[]> _table = new Dictionary<int, int[]>();

        /// <summary>Bảng tự học. Đọc/ghi luôn trong <see cref="_lock"/>.</summary>
        private static readonly Dictionary<int, List<int>> _learned = new Dictionary<int, List<int>>();

        private static bool _learnedLoaded;
        private static bool _dirty;

        static MapLevels()
        {
            Parse(TABLE);
            Parse(TABLE_MQ);
        }

        private static void Parse(string raw)
        {
            string[] recs = raw.Split(';');
            for (int i = 0; i < recs.Length; i++)
            {
                string rec = recs[i];
                if (string.IsNullOrEmpty(rec)) continue;

                int c1 = rec.IndexOf(':');
                if (c1 <= 0) continue;
                int c2 = rec.IndexOf(':', c1 + 1);
                string lvPart = c2 > c1 ? rec.Substring(c1 + 1, c2 - c1 - 1) : rec.Substring(c1 + 1);

                int mapId;
                if (!int.TryParse(rec.Substring(0, c1), out mapId)) continue;

                string[] parts = lvPart.Split(',');
                var lvs = new List<int>(parts.Length);
                for (int k = 0; k < parts.Length; k++)
                {
                    int lv;
                    if (int.TryParse(parts[k].Trim(), out lv) && lv > 0 && !lvs.Contains(lv))
                        lvs.Add(lv);
                }
                if (lvs.Count == 0) continue;
                lvs.Sort();
                _table[mapId] = lvs.ToArray();
            }
        }

        /// <summary>Số map bảng tĩnh phủ được (dùng cho nhãn chẩn đoán).</summary>
        public static int StaticCount { get { return _table.Count; } }

        /// <summary>
        /// Các mức level quái của map, tăng dần — HỢP của bảng tĩnh và bảng tự học.
        /// Trả về <c>null</c> khi chưa biết gì về map này.
        /// </summary>
        public static int[] Get(int mapId)
        {
            int[] fixedLv;
            _table.TryGetValue(mapId, out fixedLv);

            lock (_lock)
            {
                EnsureLoaded();

                List<int> seen;
                if (!_learned.TryGetValue(mapId, out seen) || seen.Count == 0)
                    return fixedLv;

                var all = new List<int>(seen);
                if (fixedLv != null)
                {
                    for (int i = 0; i < fixedLv.Length; i++)
                        if (!all.Contains(fixedLv[i])) all.Add(fixedLv[i]);
                }
                all.Sort();
                return all.ToArray();
            }
        }

        /// <summary>
        /// Chuỗi hiển thị: <c>"Lv 92"</c>, <c>"Lv 51 - Lv 52"</c>, hoặc chuỗi rỗng khi chưa biết.
        /// </summary>
        public static string Text(int mapId)
        {
            int[] lv = Get(mapId);
            if (lv == null || lv.Length == 0) return string.Empty;

            var sb = new StringBuilder(lv.Length * 8);
            for (int i = 0; i < lv.Length; i++)
            {
                if (i > 0) sb.Append(" - ");
                sb.Append("Lv ").Append(lv[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Ghi nhận đám quái vừa nhìn thấy khi vào map. Gọi từ <c>MapHandler.HandleMapInfo</c>.
        ///
        /// LỌC y hệt cách bảng gốc được lọc, nếu không nhãn sẽ đầy rác:
        ///  - bỏ boss thật (<c>IsBoss</c>) và Tinh Anh / Thủ Lĩnh (<c>LevelBoss != 0</c>);
        ///  - bỏ level 0 (Hộp bí ẩn, Kền kền nhặt xác);
        ///  - bỏ quái lạc bầy: cao hơn con thấp nhất của map quá 15 level.
        ///
        /// Rẻ và im lặng: không thấy gì mới thì không khoá lâu, không chạm đĩa.
        /// </summary>
        public static void Observe(int mapId, MobState[] mobs)
        {
            if (mapId < 0 || mobs == null || mobs.Length == 0) return;

            // Gom + loc TRUOC khi khoa - vong nay chay tren luong doc socket cua 600 account.
            List<int> found = null;
            int min = int.MaxValue;
            for (int i = 0; i < mobs.Length; i++)
            {
                var m = mobs[i];
                if (m == null || m.IsBoss || m.LevelBoss != 0) continue;
                int lv = m.Level;
                if (lv <= 0) continue;
                if (found == null) found = new List<int>(4);
                if (!found.Contains(lv)) found.Add(lv);
                if (lv < min) min = lv;
            }
            if (found == null) return;

            for (int i = found.Count - 1; i >= 0; i--)
                if (found[i] - min > 15) found.RemoveAt(i);   // quai lac bay
            if (found.Count == 0) return;

            bool changed = false;
            lock (_lock)
            {
                EnsureLoaded();

                List<int> seen;
                if (!_learned.TryGetValue(mapId, out seen))
                {
                    seen = new List<int>(found.Count);
                    _learned[mapId] = seen;
                }
                for (int i = 0; i < found.Count; i++)
                {
                    if (seen.Contains(found[i])) continue;
                    seen.Add(found[i]);
                    changed = true;
                }
                if (!changed) return;
                seen.Sort();
                _dirty = true;
            }

            Save();
        }

        // ===================== luu tru =====================

        private static void EnsureLoaded()
        {
            if (_learnedLoaded) return;
            _learnedLoaded = true;   // dat TRUOC: hong doc thi cung khong thu lai moi lan vao map

            try
            {
                string path = AppPaths.MapLevelsLearned;
                if (!File.Exists(path)) return;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i];
                    if (string.IsNullOrEmpty(ln) || ln[0] == '#') continue;

                    int eq = ln.IndexOf('=');
                    if (eq <= 0) continue;

                    int mapId;
                    if (!int.TryParse(ln.Substring(0, eq).Trim(), out mapId)) continue;

                    string[] parts = ln.Substring(eq + 1).Split(',');
                    var lvs = new List<int>(parts.Length);
                    for (int k = 0; k < parts.Length; k++)
                    {
                        int lv;
                        if (int.TryParse(parts[k].Trim(), out lv) && lv > 0 && !lvs.Contains(lv))
                            lvs.Add(lv);
                    }
                    if (lvs.Count == 0) continue;
                    lvs.Sort();
                    _learned[mapId] = lvs;
                }
            }
            catch { /* hong file hoc duoc thi coi nhu chua hoc gi - khong duoc lam chet bot */ }
        }

        /// <summary>
        /// Ghi <c>Data/map_levels.txt</c>. Chỉ gọi khi thật sự có level MỚI, mà mỗi map chỉ mới
        /// đúng một lần trong đời nên số lần chạm đĩa gần như bằng số map — không cần hẹn giờ.
        /// </summary>
        private static void Save()
        {
            try
            {
                var sb = new StringBuilder(1024);
                sb.AppendLine("# Level quai HOC DUOC tu server nay - NSOKHODO tu ghi, dung sua tay.");
                sb.AppendLine("# Dinh dang: mapId=lv,lv,...   (hop voi bang tinh trong GameData/MapLevels.cs)");

                lock (_lock)
                {
                    if (!_dirty) return;
                    var ids = new List<int>(_learned.Keys);
                    ids.Sort();
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var lvs = _learned[ids[i]];
                        if (lvs == null || lvs.Count == 0) continue;
                        sb.Append(ids[i]).Append('=');
                        for (int k = 0; k < lvs.Count; k++)
                        {
                            if (k > 0) sb.Append(',');
                            sb.Append(lvs[k]);
                        }
                        sb.AppendLine();
                    }
                    _dirty = false;
                }

                // SharedFile: map_levels dung CHUNG cho moi cua so tool -> phai chan giua cac
                // TIEN TRINH (xem docs/features/DANH_SACH_ACC.md §C).
                SharedFile.WriteAtomic(AppPaths.MapLevelsLearned, sb.ToString());
            }
            catch { /* het cho dia / file khoa -> lan sau hoc lai, khong duoc lam chet bot */ }
        }
    }
}
