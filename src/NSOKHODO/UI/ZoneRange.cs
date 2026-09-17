using System;
using System.Collections.Generic;

namespace NSOKHODO
{
    /// <summary>
    /// Đọc ô "Dải khu" của "Cài đặt nhanh kích yên": <c>0-29</c> · <c>1,3,5</c> · <c>0-9,20-29</c>.
    ///
    /// Tách ra khỏi <c>KichYenQuickForm</c> (WinForms) để bản Android dùng LẠI đúng luật này —
    /// đặt ở namespace gốc <c>NSOKHODO</c> như <see cref="NameMask"/> nên cả <c>.UI</c> lẫn
    /// <c>.Droid.Ui</c> đều thấy, không cần using.
    /// </summary>
    public static class ZoneRange
    {
        /// <summary>
        /// Bung chuỗi thành danh sách khu. Khu hợp lệ 0..254 (255 để dành cho "Mọi khu",
        /// xem <c>AccountConfig.ANY_ZONE</c>). Bỏ trùng, giữ nguyên thứ tự người dùng gõ.
        /// Sai cú pháp ở BẤT KỲ đoạn nào → trả danh sách RỖNG (không đoán bừa một phần).
        /// </summary>
        public static List<int> Parse(string s)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(s)) return result;
            var seen = new HashSet<int>();

            foreach (string chunkRaw in s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string chunk = chunkRaw.Trim();
                if (chunk.Length == 0) continue;

                int dash = chunk.IndexOf('-');
                if (dash > 0)
                {
                    int lo, hi;
                    if (!int.TryParse(chunk.Substring(0, dash).Trim(), out lo)) return new List<int>();
                    if (!int.TryParse(chunk.Substring(dash + 1).Trim(), out hi)) return new List<int>();
                    if (lo > hi) { int tmp = lo; lo = hi; hi = tmp; }
                    if (lo < 0 || hi > 254) return new List<int>();
                    for (int v = lo; v <= hi; v++) if (seen.Add(v)) result.Add(v);
                }
                else
                {
                    int v;
                    if (!int.TryParse(chunk, out v)) return new List<int>();
                    if (v < 0 || v > 254) return new List<int>();
                    if (seen.Add(v)) result.Add(v);
                }
            }
            return result;
        }

        /// <summary>Dòng xem trước dưới ô nhập — dùng chung để hai bản nói y một câu.</summary>
        public static string Preview(string raw)
        {
            return Preview(raw, "Bỏ trống = không đụng tới khu của tài khoản nào.");
        }

        /// <summary>
        /// Như <see cref="Preview(string)"/> nhưng tự đặt câu hiện khi ô TRỐNG. Cần overload này vì
        /// ô trống mang ý nghĩa khác nhau ở hai chỗ dùng: bên "Cài đặt nhanh kích yên" nghĩa là
        /// "không đụng tới khu của ai", còn ở tab Train nghĩa là "tự chọn khu vắng nhất".
        /// Phần đọc/kiểm dải khu thì DÙNG CHUNG — không được chép luật ra UI.
        /// </summary>
        public static string Preview(string raw, string emptyText)
        {
            var z = Parse(raw);
            if (raw == null || raw.Trim().Length == 0)
                return emptyText;
            if (z.Count == 0) return "Không đọc được dải khu.";

            var names = new string[z.Count];
            for (int i = 0; i < z.Count; i++) names[i] = z[i].ToString();
            string joined = string.Join(", ", names);
            if (joined.Length > 60) joined = joined.Substring(0, 60);
            return string.Format("{0} khu: {1}{2}", z.Count, joined, z.Count > 20 ? "…" : "");
        }
    }
}
