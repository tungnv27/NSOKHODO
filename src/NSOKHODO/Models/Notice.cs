using System;

namespace NSOKHODO.Models
{
    /// <summary>
    /// Một dòng "thông tin nhận" — clone <c>InfoItem</c> của client (MODGAME <c>InfoItem.java:1-20</c>).
    ///
    /// <para><b>Đây là thanh DƯỚI ĐÁY màn hình (<c>InfoMe</c>), không phải thanh trên đỉnh
    /// (<c>Info</c>).</b> Game có HAI thanh chạy chữ riêng biệt và rất dễ nhầm:</para>
    /// <list type="bullet">
    ///   <item><c>InfoMe</c> (đáy) — nhặt item, nhận yên, nhận lượng, nhận EXP lớn, chữ server
    ///         gửi riêng cho mình (cmd -24). <b>Đây là cái ta clone.</b></item>
    ///   <item><c>Info</c> (đỉnh) — thông báo toàn server, chat thế giới. KHÔNG làm.</item>
    /// </list>
    ///
    /// <para>⚠️ <c>Info.hI</c> mà <c>GameScr.g()</c> dùng để đẩy HUD xuống là chiều cao thanh
    /// TRÊN ĐỈNH, không liên quan gì tới thanh này. Ta không vẽ thanh đỉnh nên khối trạng thái
    /// (<c>AUTO_X/AUTO_STEP</c>) giữ nguyên mốc y = 38, đúng với <c>Info.hI == 0</c>.</para>
    /// </summary>
    public struct Notice
    {
        /// <summary>Chữ TRẮNG — mặc định của <c>InfoItem(String)</c>: nhặt item, nhặt yên.</summary>
        public const byte KIND_WHITE = 0;

        /// <summary>Chữ VÀNG — chữ server (cmd -24), nhận lượng, nhận EXP lớn, item type 25.</summary>
        public const byte KIND_YELLOW = 1;

        /// <summary>
        /// Số khung GIỮ mặc định (<c>InfoItem.speed = 20</c>). Ở 25 FPS của client ≈ 0,8 giây.
        /// </summary>
        public const int SPEED_DEFAULT = 20;

        /// <summary>Chữ server gửi (cmd -24) giữ lâu hơn — <c>Controller.java:295</c> dùng 50.</summary>
        public const int SPEED_SERVER = 50;

        public string Text;
        public byte Kind;

        /// <summary>Số khung giữ ở pha HOLD — client đếm khung, xem <see cref="SPEED_DEFAULT"/>.</summary>
        public int Speed;
    }

    /// <summary>
    /// Hàng đợi "thông tin nhận" của MỘT account — clone <c>InfoMe.infoWaitToShow</c>
    /// (MODGAME <c>InfoMe.java:109-113</c>).
    ///
    /// <para><b>Sức chứa 11, đầy thì BỎ PHẦN TỬ CŨ NHẤT</b> — đúng bản gốc
    /// (<c>if (infoWaitToShow.size() &gt; 10) removeElementAt(0)</c>). Với bot đang tàn sát thì
    /// hàng đợi gần như luôn đầy; bỏ cái cũ nhất là đúng ý muốn (thứ vừa nhặt mới đáng xem).</para>
    ///
    /// <para><b>KHÔNG clone cơ chế gộp chuỗi của bản gốc</b> (<c>canMergeString</c>): user chốt
    /// 2026-09-09 *"báo hết, không lọc"*. Tiện thể né luôn một lỗi có thật của bản gốc —
    /// <c>InfoMe.java:153</c> dựng <c>StringBuffer</c> rồi vứt đi, nhánh gộp trả <c>true</c> mà
    /// KHÔNG cập nhật phần tử ⇒ thông báo bị nuốt mất.</para>
    ///
    /// <para>🔑 <see cref="Enabled"/> mặc định TẮT, chỉ cửa sổ "Xem game" bật khi mở VÀ ô tích
    /// "Bật thông báo nhận" đang bật. Cùng triết lý với <see cref="FlyTextBoard"/>: cổng đóng thì
    /// handler thoát trước khi nối chuỗi, chi phí về ĐÚNG 0 — với fleet vài trăm account đang
    /// nhặt đồ liên tục thì đây không phải chuyện nhỏ.</para>
    ///
    /// Thread mạng ghi (<see cref="Push(string,byte,int)"/>), luồng vẽ đọc (<see cref="TryTake"/>) — nên có khoá.
    /// </summary>
    public sealed class NoticeBoard
    {
        /// <summary>Sức chứa — <c>size() &gt; 10</c> của bản gốc, tức giữ tối đa 11.</summary>
        public const int MAX = 11;

        private readonly Notice[] _q = new Notice[MAX];
        private int _head;    // vi tri lay ra
        private int _count;
        private readonly object _lock = new object();

        /// <summary>volatile: bật/tắt từ luồng giao diện, đọc từ thread mạng.</summary>
        private volatile bool _enabled;

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (!value) Clear();   // tat la don sach, khong de dong cu treo lai den lan mo sau
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _q.Length; i++) _q[i].Text = null;
                _head = 0; _count = 0;
            }
        }

        /// <summary>
        /// Xếp một dòng vào cuối hàng đợi. Gọi TỪ THREAD MẠNG.
        ///
        /// Người gọi phải kiểm <see cref="Enabled"/> TRƯỚC khi nối chuỗi — hàm này có kiểm lại
        /// nhưng lúc đó chuỗi đã cấp phát rồi, mất đúng cái ta muốn tiết kiệm.
        /// </summary>
        public void Push(string text, byte kind, int speed)
        {
            if (!_enabled || string.IsNullOrEmpty(text)) return;
            lock (_lock)
            {
                if (_count >= MAX)
                {
                    // Day -> bo phan tu CU NHAT (InfoMe.java:110 removeElementAt(0)).
                    _head = (_head + 1) % MAX;
                    _count--;
                }
                int tail = (_head + _count) % MAX;
                _q[tail].Text = text;
                _q[tail].Kind = kind;
                _q[tail].Speed = speed > 0 ? speed : Notice.SPEED_DEFAULT;
                _count++;
            }
        }

        public void Push(string text) { Push(text, Notice.KIND_WHITE, Notice.SPEED_DEFAULT); }

        /// <summary>
        /// Lấy dòng kế tiếp ra khỏi hàng đợi. Gọi TỪ LUỒNG VẼ, chỉ khi thanh chạy chữ đang rảnh
        /// (clone <c>InfoMe.update()</c> state <c>p1 == 5</c>: lấy <c>firstElement</c> rồi
        /// <c>removeElementAt(0)</c>).
        /// </summary>
        public bool TryTake(out Notice n)
        {
            n = default(Notice);
            if (!_enabled) return false;
            lock (_lock)
            {
                if (_count == 0) return false;
                n = _q[_head];
                _q[_head].Text = null;
                _head = (_head + 1) % MAX;
                _count--;
                return true;
            }
        }
    }
}
