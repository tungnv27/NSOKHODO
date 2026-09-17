using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NSOKHODO.Config;

namespace NSOKHODO.Fleet
{
    /// <summary>
    /// Đếm số lần mỗi tài khoản mất kết nối — yêu cầu riêng của bản này.
    ///
    /// <para><b>Vì sao tách làm BA loại thay vì một con số:</b> ba nguyên nhân dưới đây đòi ba cách
    /// xử lý hoàn toàn khác nhau, gộp lại thành "mất kết nối: 37" thì con số đó không chỉ được về
    /// phía nào cả. Đo trên fleet thật bên NSOLITEPRO (1.643 lần thử login) cho thấy phân bố rất
    /// lệch: proxy từ chối và server chặn chiếm phần lớn, còn "đang chơi thì rớt" mới là thứ ảnh
    /// hưởng tới việc canh khu.</para>
    ///
    /// <list type="table">
    /// <item><term><see cref="Loai.RotKhiDangChoi"/></term><description>đã vào game rồi mới rớt —
    /// <b>đây là "mất kết nối" theo nghĩa người dùng hiểu</b>. Cao bất thường ⇒ nghi mạng/proxy
    /// chập chờn, hoặc bot bị server đá.</description></item>
    /// <item><term><see cref="Loai.ServerChan"/></term><description>nối được TCP nhưng server đóng
    /// ngay (&lt; 500 ms). Không phải lỗi mạng — là server từ chối. Nhiều ⇒ đang bị khoá tạm theo
    /// IP, hoặc dồn quá nhiều acc lên một máy chủ.</description></item>
    /// <item><term><see cref="Loai.LoiDangNhap"/></term><description>đứt giữa lúc đăng nhập (bắt
    /// tay, chờ khoá, chờ danh sách nhân vật). Thường là proxy hỏng.</description></item>
    /// </list>
    ///
    /// <para><b>Khoá là Username</b>, không phải dòng lưới: sống qua cả lần khởi động lại app, mà
    /// thứ người dùng muốn biết là "con acc NÀY hay rớt", không phải "dòng số 12".
    /// ⚠ Hệ quả đã biết: cùng một username nằm trên hai dòng thì hai dòng dùng chung một ô đếm.</para>
    ///
    /// <para>Lưu vào <c>Data/&lt;tên danh sách&gt;_disconnect.txt</c> (TSV), ghi lười: đánh dấu bẩn
    /// rồi <see cref="LuuNeuBan"/> gọi từ timer UI. Không ghi mỗi lần đếm — 150 acc rớt dồn là
    /// 150 lần ghi đĩa trong vài giây.</para>
    /// </summary>
    public static class DisconnectStats
    {
        public enum Loai
        {
            /// <summary>Đã vào game rồi mới rớt.</summary>
            RotKhiDangChoi = 0,
            /// <summary>Server đóng ngay sau khi vừa nối (&lt; 500 ms).</summary>
            ServerChan = 1,
            /// <summary>Đứt giữa lúc đăng nhập.</summary>
            LoiDangNhap = 2,
        }

        public sealed class Ban
        {
            public int RotKhiDangChoi;
            public int ServerChan;
            public int LoiDangNhap;
            /// <summary>Số lần vào game THÀNH CÔNG — mẫu số để con số rớt có ý nghĩa.</summary>
            public int VaoGame;
            /// <summary>Mốc lần rớt gần nhất (giờ máy). <c>MinValue</c> = chưa rớt lần nào.</summary>
            public DateTime RotLuc = DateTime.MinValue;

            public int Tong { get { return RotKhiDangChoi + ServerChan + LoiDangNhap; } }
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Ban> _map =
            new Dictionary<string, Ban>(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _ban;

        private static string Path_ { get { return AppPaths.ThongKeDisconnect; } }

        // ==================== GHI ====================

        public static void Ghi(string username, Loai loai)
        {
            if (string.IsNullOrEmpty(username)) return;
            lock (_lock)
            {
                var b = LayHoacTao(username);
                switch (loai)
                {
                    case Loai.RotKhiDangChoi: b.RotKhiDangChoi++; break;
                    case Loai.ServerChan: b.ServerChan++; break;
                    default: b.LoiDangNhap++; break;
                }
                b.RotLuc = DateTime.Now;
                _ban = true;
            }
        }

        /// <summary>Gọi khi account vào game thành công — mẫu số của tỉ lệ rớt.</summary>
        public static void GhiVaoGame(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            lock (_lock) { LayHoacTao(username).VaoGame++; _ban = true; }
        }

        /// <summary>Bản sao số liệu của một account (không bao giờ trả null).</summary>
        public static Ban Lay(string username)
        {
            if (string.IsNullOrEmpty(username)) return new Ban();
            lock (_lock)
            {
                Ban b;
                if (!_map.TryGetValue(username, out b)) return new Ban();
                return new Ban
                {
                    RotKhiDangChoi = b.RotKhiDangChoi,
                    ServerChan = b.ServerChan,
                    LoiDangNhap = b.LoiDangNhap,
                    VaoGame = b.VaoGame,
                    RotLuc = b.RotLuc,
                };
            }
        }

        /// <summary>Xoá số liệu của một account (nút "Reset đếm" trên lưới).</summary>
        public static void Xoa(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            lock (_lock) { if (_map.Remove(username)) _ban = true; }
        }

        public static void XoaHet()
        {
            lock (_lock) { _map.Clear(); _ban = true; }
        }

        private static Ban LayHoacTao(string username)
        {
            Ban b;
            if (!_map.TryGetValue(username, out b)) { b = new Ban(); _map[username] = b; }
            return b;
        }

        // ==================== ĐĨA ====================

        public static void Nap()
        {
            lock (_lock)
            {
                _map.Clear();
                _ban = false;
                try
                {
                    if (!File.Exists(Path_)) return;
                    foreach (string line in File.ReadAllLines(Path_, Encoding.UTF8))
                    {
                        if (string.IsNullOrEmpty(line) || line[0] == '#') continue;
                        // user \t rot \t chan \t loiLogin \t vaoGame \t rotLuc(ticks)
                        string[] p = line.Split('\t');
                        if (p.Length < 5) continue;
                        var b = new Ban();
                        int.TryParse(p[1], out b.RotKhiDangChoi);
                        int.TryParse(p[2], out b.ServerChan);
                        int.TryParse(p[3], out b.LoiDangNhap);
                        int.TryParse(p[4], out b.VaoGame);
                        long tick;
                        if (p.Length > 5 && long.TryParse(p[5], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out tick) && tick > 0)
                            b.RotLuc = new DateTime(tick);
                        _map[p[0]] = b;
                    }
                }
                catch { /* hong file thong ke KHONG duoc lam chet app - dem lai tu 0 */ }
            }
        }

        /// <summary>Chỉ ghi khi có thay đổi. Gọi từ timer UI + lúc thoát.</summary>
        public static void LuuNeuBan()
        {
            if (!_ban) return;
            var sb = new StringBuilder();
            sb.AppendLine("# NSOKHODO - dem mat ket noi. Cot: user|rotKhiDangChoi|serverChan|loiDangNhap|vaoGame|rotLuc");
            lock (_lock)
            {
                foreach (var kv in _map)
                {
                    var b = kv.Value;
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                        kv.Key, b.RotKhiDangChoi, b.ServerChan, b.LoiDangNhap, b.VaoGame,
                        b.RotLuc == DateTime.MinValue ? 0L : b.RotLuc.Ticks);
                    sb.AppendLine();
                }
                _ban = false;
            }
            try { SharedFile.WriteAtomic(Path_, sb.ToString()); }
            catch { /* khong ghi duoc thi thoi, lan sau ghi lai - _ban da tat nen mat 1 chu ky */ }
        }

        /// <summary>
        /// Tổng số lần rớt của TẤT CẢ account, lấy trong MỘT lần khoá.
        ///
        /// <para>Tồn tại vì quy mô: dòng tổng kết chạy mỗi giây, mà gọi <see cref="Lay"/> cho từng
        /// account là 1.800 lần lấy khoá + 1.800 đối tượng rác MỖI GIÂY. Ở đây chỉ cộng số.</para>
        /// </summary>
        public static int TongTatCa()
        {
            int t = 0;
            lock (_lock)
                foreach (var kv in _map) t += kv.Value.Tong;
            return t;
        }

        /// <summary>Xuất CSV cho người dùng mở bằng Excel.</summary>
        public static string XuatCsv(IEnumerable<string> usernames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tai khoan,Rot khi dang choi,Server chan,Loi dang nhap,Tong rot,Vao game OK,Ti le rot %,Rot gan nhat");
            foreach (string u in usernames)
            {
                var b = Lay(u);
                int mauSo = b.VaoGame + b.Tong;
                string ti = mauSo > 0
                    ? (100.0 * b.Tong / mauSo).ToString("0.0", CultureInfo.InvariantCulture)
                    : "";
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7}",
                    u, b.RotKhiDangChoi, b.ServerChan, b.LoiDangNhap, b.Tong, b.VaoGame, ti,
                    b.RotLuc == DateTime.MinValue ? "" : b.RotLuc.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
