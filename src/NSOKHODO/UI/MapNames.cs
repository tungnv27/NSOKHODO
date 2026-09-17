namespace NSOKHODO.UI
{
    /// <summary>
    /// Tên map theo id (copy từ NSOTool/NSOManager). Index trong mảng = map id.
    ///
    /// <para><see cref="List"/> là thứ đổ vào combo chọn map, dạng <c>"[55] Phong ấn Ounio"</c>.
    /// User chốt 2026-09-09: dropdown <b>KHÔNG</b> kèm level quái (nhãn dài quá) — level chỉ hiện
    /// trong hộp thoại "Bản đồ", lấy từ <see cref="GameData.MapLevels"/>.</para>
    /// </summary>
    public static class MapNames
    {
        /// <summary>
        /// Nguồn gốc, giữ nguyên dạng cũ <c>"55 - Phong ấn Ounio"</c> để không phải sửa 160 dòng.
        /// KHÔNG dùng trực tiếp để hiển thị — dùng <see cref="List"/> hoặc <see cref="NameOf"/>.
        /// </summary>
        private static readonly string[] Raw = new string[]
        {
            "0 - Nhà thi đấu Haruna",
            "1 - Trường Hirosaki",
            "2 - Khu luyện tập",
            "3 - Đồng Hachi",
            "4 - Rừng đào Sakura",
            "5 - Rừng trúc Utra",
            "6 - Thác Kitajima",
            "7 - Rừng Mishima",
            "8 - Sông Watamaro",
            "9 - Nghĩa địa Izuko",
            "10 - Làng Kojin",
            "11 - Miếu Kamo",
            "12 - Miếu Oboko",
            "13 - Rừng gỗ Kouji",
            "14 - Rừng Aokigahara",
            "15 - Vách núi Ito",
            "16 - Thung lũng Taira",
            "17 - Làng Sanzu",
            "18 - Sân đền Orochi",
            "19 - Ngôi đền Orochi",
            "20 - Chân thác Kitajima",
            "21 - Đồi Fumimen",
            "22 - Làng Tone",
            "23 - Vách Ichidai",
            "24 - Đỉnh Ichidai",
            "25 - Đồi Kokoro",
            "26 - Cánh đồng Fuki",
            "27 - Trường Haruna",
            "28 - Ký túc xá Haruna",
            "29 - Hang Aka",
            "30 - Suối Akagi",
            "31 - Bờ biển Oura",
            "32 - Làng chài",
            "33 - Rừng Moshio",
            "34 - Đão Hebi",
            "35 - Hang Meiro",
            "36 - Đồng Kisei",
            "37 - Núi Hashigoto",
            "38 - Làng Chakumi",
            "39 - Sông băng Yamato",
            "40 - Cánh đồng Hiya",
            "41 - Khu đá đỏ Akai",
            "42 - Khu đá đỏ Aiko",
            "43 - Làng Echigo",
            "44 - Đỉnh Okama",
            "45 - Hang núi Kurai",
            "46 - Hồ Stuki",
            "47 - Hẻm núi Takana",
            "48 - Làng Oshin",
            "49 - Đền Amaterasu",
            "50 - Rừng Kanashii",
            "51 - Rừng Toge",
            "52 - Rừng Kappa",
            "53 - Động Tamatamo",
            "54 - Đền Harumoto",
            "55 - Phong ấn Ounio",
            "56 - Nhà thi đấu Ookaza",
            "57 - Sân sau Miếu Oboko",
            "58 - Sân sau đền Orochi",
            "59 - Mũi Hone",
            "60 - Cửa hang Aka",
            "61 - Cửa biển Kawaguchi",
            "62 - Hang Chi",
            "63 - Hang Ha",
            "64 - Hang Kugyou",
            "65 - Mũi Nuranura",
            "66 - Khe núi Chorochoro",
            "67 - Núi Ontake",
            "68 - Núi Anzen",
            "69 - Vách Ainodake",
            "70 - Thung lũng chết",
            "71 - Rừng già",
            "72 - Trường Ookaza",
            "73 - Nhà thi đấu Hirosaki",
            "74 - Hang Inoshishi",
            "75 - Đấu trường cấp 10",
            "76 - Đấu trường cấp 20",
            "77 - Đấu trường cấp 30",
            "78 - Địa đạo Chikatoya",
            "79 - Đấu trường cấp 40",
            "80 - Cửa Chờ",
            "81 - Cửa Siêu Tốc",
            "82 - Cửa Né Tránh",
            "83 - Cửa Phản Đòn",
            "84 - Cửa Hỏa",
            "85 - Cửa Phong",
            "86 - Cửa Băng",
            "87 - Cửa Sa Mạc",
            "88 - Cửa Đồi Núi",
            "89 - Cửa Đầm Lầy",
            "90 - Cửa Bùa Chú",
            "91 - Động bàn tơ",
            "92 - Hang dơi",
            "93 - Hang thủ lĩnh",
            "94 - Hang tổ ong",
            "95 - Động bọ ngựa",
            "96 - Hang kỳ đà",
            "97 - Thiên Vương Động",
            "98 - Căn cứ địa",
            "99 - Bạch đài",
            "100 - Hành lang giữa",
            "101 - Hành lang trên",
            "102 - Hành lang dưới",
            "103 - Hắc đài",
            "104 - Cắn cứ địa",
            "105 - Tam hợp sơn động",
            "106 - Long xà động",
            "107 - Hoàng xà động",
            "108 - Xích trùng động",
            "109 - Ngân lang động",
            "110 - Khu báo danh",
            "111 - Lôi đài",
            "112 - Thất thú ải",
            "113 - Khu vực chờ",
            "114 - Thạch không vực",
            "115 - Sinh tử vực",
            "116 - Luân hồi kiếp",
            "117 - Khu báo danh",
            "118 - Báo danh gia tộc",
            "119 - Báo danh gia tộc",
            "120 - Sảnh 1",
            "121 - Hành lang 1",
            "122 - Hành lang 2",
            "123 - Hành lang 3",
            "124 - Sảnh 2",
            "125 - Độc phong sơn",
            "126 - Địa trùng sơn",
            "127 - Mộc hỏa vực",
            "128 - Sơn vương trại",
            "129 - Lôi đài",
            "130 - Kẹo chiến",
            "131 - Kẹo trắng",
            "132 - Kẹo đen",
            "133 - Phòng chờ",
            "134 - Núi Doragon",
            "135 - Rừng Majo",
            "136 - Vực Yunikoon",
            "137 - Động Kingu",
            "138 - Làng Fearri",
            "139 - Quỷ Sơn",
            "140 - Sơn Hải Vực",
            "141 - Đoạn Sơn",
            "142 - Đão Quỷ",
            "143 - Sinh Tử Kiều",
            "144 - Nhân Duyên Lộ",
            "145 - Hoang Trấn",
            "146 - Mài Tâm Lộ",
            "147 - Bát Thụ Hoang Lâm",
            "148 - Cửu Mộc Hoàng Kiều",
            "149 - Lôi đài",
            "150 - Chiến trường Hirosaki",
            "151 - Chiến trường Haruna",
            "152 - Chiến trường Ookaza",
            "153 - Khu vực giao lưu",
            "154 - Phòng chờ Hirosaki",
            "155 - Phòng chờ Haruna",
            "156 - Phòng chờ Ookaza",
            "157 - Tam Nhân Quan 1",
            "158 - Tam Nhân Quan 2",
            "159 - Tam Nhân Quan 3"
        };

        /// <summary>
        /// Nhãn cho combo chọn map: <c>"[55] Phong ấn Ounio"</c>. Index = map id.
        /// Dựng một lần lúc nạp lớp từ <see cref="Raw"/>.
        /// </summary>
        public static readonly string[] List = BuildList();

        private static string[] BuildList()
        {
            var list = new string[Raw.Length];
            for (int i = 0; i < Raw.Length; i++)
                list[i] = "[" + i + "] " + NameOf(i);
            return list;
        }

        /// <summary>Tên map theo id (không kèm id), hoặc "map N" nếu ngoài danh sách.</summary>
        public static string NameOf(int mapId)
        {
            if (mapId >= 0 && mapId < Raw.Length)
            {
                string s = Raw[mapId];
                int dash = s.IndexOf(" - ");
                return dash > 0 ? s.Substring(dash + 3) : s;
            }
            return "map " + mapId;
        }

        /// <summary>
        /// Bộ lọc ô "Tìm" của hộp thoại Bản đồ — dùng CHUNG cho bản PC và bản Android.
        ///
        /// Nhận 4 kiểu gõ, tự đoán theo nội dung:
        /// <list type="bullet">
        /// <item><c>ounio</c> — theo tên, <b>không cần dấu</b> và không phân biệt hoa thường
        ///       (gõ <c>phong an</c> vẫn ra "Phong ấn Ounio").</item>
        /// <item><c>55</c> — số: khớp map id 55 <b>hoặc</b> map có quái Lv 55.</item>
        /// <item><c>80-95</c> — dải: mọi map có ít nhất một loại quái nằm trong dải level đó.</item>
        /// <item><c>lv 92</c> — như trên, chữ "lv" ở đầu được bỏ qua.</item>
        /// </list>
        /// </summary>
        public static bool Matches(int mapId, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            string q = Deaccent(query.Trim().ToLowerInvariant());
            if (q.Length == 0) return true;

            if (q.StartsWith("lv")) q = q.Substring(2).Trim();
            if (q.Length == 0) return true;

            // Dai level "80-95" (chi nhan khi CA HAI ve deu la so - "abc-xyz" van tim theo ten).
            int dash = q.IndexOf('-');
            if (dash > 0 && dash < q.Length - 1)
            {
                int lo, hi;
                if (int.TryParse(q.Substring(0, dash).Trim(), out lo)
                    && int.TryParse(q.Substring(dash + 1).Trim(), out hi))
                {
                    if (lo > hi) { int t = lo; lo = hi; hi = t; }
                    int[] lvR = GameData.MapLevels.Get(mapId);
                    if (lvR == null) return false;
                    for (int i = 0; i < lvR.Length; i++)
                        if (lvR[i] >= lo && lvR[i] <= hi) return true;
                    return false;
                }
            }

            int n;
            if (int.TryParse(q, out n))
            {
                if (mapId == n) return true;
                int[] lv = GameData.MapLevels.Get(mapId);
                if (lv != null)
                    for (int i = 0; i < lv.Length; i++)
                        if (lv[i] == n) return true;
                return false;
            }

            return Deaccent(NameOf(mapId).ToLowerInvariant()).IndexOf(q, System.StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Bỏ dấu tiếng Việt để tìm kiếm gõ nhanh. Chữ <c>đ</c> phải xử lý riêng vì nó KHÔNG tách
        /// được bằng <c>FormD</c> (không phải "d + dấu", mà là một chữ cái riêng trong Unicode).
        /// </summary>
        public static string Deaccent(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;

            string norm = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(norm.Length);
            for (int i = 0; i < norm.Length; i++)
            {
                char ch = norm[i];
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                    == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (ch == 'đ' || ch == 'Đ') { sb.Append(ch == 'đ' ? 'd' : 'D'); continue; }
                sb.Append(ch);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        /// <summary>
        /// Nhãn đầy đủ cho hộp thoại "Bản đồ": <c>"[55] Phong ấn Ounio (Lv 92)"</c>.
        /// Map chưa biết level thì bỏ hẳn phần ngoặc.
        /// </summary>
        public static string LabelWithLevel(int mapId)
        {
            string baseText = (mapId >= 0 && mapId < List.Length) ? List[mapId] : "[" + mapId + "] map " + mapId;
            string lv = GameData.MapLevels.Text(mapId);
            return lv.Length == 0 ? baseText : baseText + " (" + lv + ")";
        }
    }
}
