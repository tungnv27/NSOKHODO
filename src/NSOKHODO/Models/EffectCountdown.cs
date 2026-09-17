using System;

namespace NSOKHODO.Models
{
    /// <summary>
    /// Một dòng "đồng hồ hiệu ứng" server gửi qua cmd 117 (gói phụ, sub 2 = TIME_COUNT_DOWN).
    /// Ví dụ: thời gian còn lại của món ăn, của bùa, của sự kiện.
    ///
    /// KHÁC với <see cref="Effect"/>: <see cref="Effect"/> là hiệu ứng buff theo id (không có tên,
    /// không có icon, đến từ gói khác). Dòng này server gửi kèm HẲN chuỗi tên để hiển thị, nên
    /// không gộp chung được — xem docs/reference/SERVER_FACTS.md.
    ///
    /// Wire (NinjaSchool_251 Readmsg.cs:113-140 onCountDown):
    ///   short id | UTF ten | int soGiay | short idIcon | byte kieu
    ///   soGiay &lt; 0            -> BO QUA hoan toan, khong tao dong nao
    ///   kieu == -2            -> xoa dong co id nay
    ///   kieu == 1             -> hien "ten : con lai"
    ///   kieu khac (0, ...)    -> chi hien "ten", KHONG hien so
    /// </summary>
    public class EffectCountdown
    {
        public short Id { get; set; }
        public string Name { get; set; }
        /// <summary>Chỉ số SmallImage của icon. **-1 = không có** — sprite 0 là ô thật (1×1) nên
        /// không được lấy 0 làm "trống".</summary>
        public short IconId { get; set; }

        public EffectCountdown() { IconId = -1; }
        public sbyte Kind { get; set; }

        /// <summary>Mốc hết hạn tính theo giờ máy (UTC). net452: dùng DateTime, KHÔNG dùng DateTimeOffset.</summary>
        public DateTime ExpiresAt { get; set; }

        public int SecondsLeft
        {
            get
            {
                var d = ExpiresAt - DateTime.UtcNow;
                double s = d.TotalSeconds;
                if (s <= 0) return 0;
                if (s > int.MaxValue) return int.MaxValue;
                return (int)s;
            }
        }

        /// <summary>
        /// Thời gian còn lại, GHI RÕ ĐƠN VỊ: <c>2n 3g</c> · <c>3g 05p</c> · <c>10p 28s</c> · <c>28s</c>
        /// (n = ngày, g = giờ, p = phút, s = giây).
        ///
        /// CỐ Ý KHÁC CLIENT. Client (TimecountDown.converSecon2hours) in trần "10:28" và bỏ hẳn
        /// đơn vị — trên màn hình game người chơi đoán được nhờ ngữ cảnh, nhưng ở cửa sổ "Xem game"
        /// nhiều dòng hiệu ứng xếp cạnh nhau thì không phân biệt nổi "10 giờ 28 phút" với
        /// "10 phút 28 giây". Client cũng KHÔNG có bậc ngày: hiệu ứng dài hơn 24h ra "48:30", đọc
        /// như 48 giờ nhưng dễ nhầm là 48 phút. Ở đây tách bậc ngày ra riêng.
        ///
        /// Bậc nhỏ được đệm số 0 (<c>3g 05p</c>) để cột số không nhảy khi đếm lùi.
        /// </summary>
        public static string FormatTime(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int sec = totalSeconds % 60;
            int totalMin = totalSeconds / 60;
            int min = totalMin % 60;
            int totalHour = totalMin / 60;
            int hour = totalHour % 24;
            int day = totalHour / 24;

            if (day > 0) return day + "n " + hour.ToString("00") + "g";
            if (hour > 0) return hour + "g " + min.ToString("00") + "p";
            if (min > 0) return min + "p " + sec.ToString("00") + "s";
            return sec + "s";
        }

        /// <summary>
        /// Thời gian còn lại ĐÚNG ĐỊNH DẠNG CLIENT (<c>NinjaUtil.getTime</c>, MODGAME
        /// <c>NinjaUtil.java:101-145</c>): <c>2d3h</c> · <c>3h05'</c> · <c>01:12</c>.
        /// Dùng cho hàng đồng hồ hiệu ứng trên HUD — user chốt "làm giống hệt game".
        /// Bảng Chi tiết vẫn dùng <see cref="FormatTime"/> có ghi rõ đơn vị.
        ///
        /// MỘT CHỖ CỐ Ý KHÁC: bản gốc so <c>&gt; 60</c> nên đúng giây thứ 60 nó in ra "00:60"
        /// (lỗi thật của game, thấy được mỗi khi đồng hồ chạy qua mốc phút). Ở đây so <c>&gt;= 60</c>.
        /// </summary>
        public static string FormatTimeGame(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int sec = totalSeconds, min = 0, hour = 0, day = 0;
            if (sec >= 60) { min = sec / 60; sec %= 60; }
            if (min >= 60) { hour = min / 60; min %= 60; }
            if (hour >= 24) { day = hour / 24; hour %= 24; }

            if (day > 0) return day + "d" + hour + "h";
            if (hour > 0) return hour + "h" + min + "'";
            return min.ToString("00") + ":" + sec.ToString("00");
        }

        /// <summary>Như <see cref="TimeText"/> nhưng theo định dạng của game.</summary>
        public string GameTimeText()
        {
            if (Kind != 1) return null;
            int left = SecondsLeft;
            if (left <= 0) return ExpiresAt > DateTime.UtcNow ? FormatTimeGame(0) : null;
            return FormatTimeGame(left);
        }

        /// <summary>
        /// CHỈ phần thời gian, hoặc null nếu dòng này không có đếm lùi (kiểu ≠ 1, hoặc đã hết giờ).
        /// Dùng cho kiểu hiển thị "icon + thời gian" — giống client, vốn không in tên bao giờ.
        /// </summary>
        public string TimeText()
        {
            if (Kind != 1) return null;
            int left = SecondsLeft;
            // GIAY CUOI: con 0,4s thi SecondsLeft = 0 nhung dong VAN chua bi loc khoi danh sach
            // (bo loc dung ExpiresAt > now). Tra null o day thi lop ve tuong "khong co dem lui"
            // -> dong dang la "icon + gio" DOT NGOT nhay sang hien TEN khoang 1 giay roi moi bien
            // mat. Tra "0s" de dong giu nguyen hinh dang cho toi luc tat han.
            if (left <= 0) return ExpiresAt > DateTime.UtcNow ? "0s" : null;
            return FormatTime(left);
        }

        /// <summary>Dòng chữ hiển thị, đúng luật client: kiểu 1 mới có số đếm lùi.</summary>
        public string Display()
        {
            string nm = Name ?? string.Empty;
            if (Kind != 1) return nm;
            int left = SecondsLeft;
            if (left <= 0) return nm;
            return nm + " : " + FormatTime(left);
        }

        public override string ToString() { return Display(); }
    }
}
