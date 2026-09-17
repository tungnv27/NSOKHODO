using System;

namespace NSOKHODO.Models
{
    /// <summary>
    /// Một "số sát thương bay lên" — clone <c>GameScr.startFlyText</c> của client.
    ///
    /// Nguồn đối chiếu (hai nguồn khớp nhau):
    ///   MODGAME GameScr.java:5781-5801 (sinh) · :4040-4053 (update) · :4352-4374 (vẽ)
    ///   NSOPC   GameScr.cs:6152-6173    (sinh) · :6175-6190      (update) · :6196-6233 (vẽ)
    ///
    /// Client: tối đa 5 số cùng lúc, ĐẦY thì BỎ số mới (không thay thế); bay thẳng đứng lên
    /// 2 px mỗi tick, sống 15 tick rồi tắt (<c>lh += 2; if (lh &gt; 30) chết</c>) — tức tổng
    /// cộng đi lên đúng 30 px. Không parabol, không mờ dần, không phóng to.
    ///
    /// ⚠️ LỆCH CÓ CHỦ ĐÍCH: client đếm bằng TICK, ta đếm bằng ĐỒNG HỒ THẬT. Cửa sổ "Xem game"
    /// vẽ theo nhịp riêng (và người dùng đổi được nhịp đó), nên "15 tick" ở nhịp 150 ms sẽ
    /// thành 2,25 giây lừ đừ, còn ở nhịp 33 ms lại thành 0,5 giây chớp mắt. Buộc theo giờ thật
    /// thì số bay giống nhau bất kể nhịp vẽ.
    /// </summary>
    public struct FlyText
    {
        /// <summary>Số slot — ĐÚNG như client (mảng cố định 5, đầy thì bỏ số mới).</summary>
        public const int MAX = 5;

        /// <summary>Sống bao lâu (ms). Client: 15 tick.</summary>
        public const int LIFE_MS = 800;

        /// <summary>Tổng quãng bay lên (px). Client: 2 px × 15 tick = 30.</summary>
        public const int RISE_PX = 30;

        public const byte KIND_NORMAL = 0;   // dame thuong len quai  -> cam   (client type 5)
        public const byte KIND_CRIT = 1;     // chi mang              -> vang  (client type 3)
        public const byte KIND_DODGE = 2;    // quai ne don           -> "Ne"  (client type 4)
        public const byte KIND_YEN = 3;      // "+N" yen NHAN duoc    -> vang  (client type 1)
        public const byte KIND_EXP = 4;      // "+N" kinh nghiem      -> xanh la (client type 2)

        /// <summary>
        /// Giá trị <see cref="TemplateId"/> nghĩa là "neo trên đầu NHÂN VẬT MÌNH", không phải quái.
        ///
        /// Client bắn chữ nhận yên/EXP tại <c>myChar.cx, myChar.cy - myChar.ch</c>
        /// (MODGAME <c>Controller.java:505-506</c> cmd -8, <c>:860</c> cmd 5) chứ không neo vào
        /// con quái nào. Lớp vẽ thấy giá trị này thì đo chiều cao bằng ảnh nhân vật đã ghép,
        /// KHÔNG tra bảng chiều cao quái — tra nhầm thì số nằm lẫn vào chân nhân vật.
        /// </summary>
        public const int TPL_MYCHAR = -1;

        /// <summary>Chuỗi hiển thị, vd "-1234". Rỗng với kiểu né (chữ do lớp vẽ quyết định).</summary>
        public string Text;

        /// <summary>Toạ độ GỐC của con quái lúc số phát sinh (toạ độ game, chưa trừ chiều cao ảnh).</summary>
        public int X, Y;

        /// <summary>Template quái — để lớp vẽ tra CHIỀU CAO ảnh, đặt số lên đỉnh đầu như client.</summary>
        public int TemplateId;

        public byte Kind;

        /// <summary>Mốc sinh, giờ máy UTC. net452: KHÔNG dùng DateTimeOffset.</summary>
        public DateTime BornUtc;
    }

    /// <summary>
    /// Bảng 5 slot số bay của MỘT account. Thread mạng ghi (<see cref="Push"/>), luồng giao diện
    /// đọc (<see cref="Snapshot"/>) — nên có khoá.
    ///
    /// 🔑 <see cref="Enabled"/> mặc định TẮT và chỉ được cửa sổ "Xem game" bật lên khi mở.
    /// Lý do (yêu cầu của user: *"tắt Xem game thì tool lại nhẹ như bình thường"*): không có cổng
    /// này thì MỌI account đều nối chuỗi <c>"-" + dame</c> ở mỗi đòn đánh — với fleet vài trăm
    /// account đang tàn sát là hàng nghìn chuỗi rác mỗi giây, cho một thứ gần như không ai xem.
    /// Đóng cửa sổ là cổng đóng lại, chi phí về đúng 0 (một phép so bool trong handler).
    /// </summary>
    public sealed class FlyTextBoard
    {
        private readonly FlyText[] _slots = new FlyText[FlyText.MAX];
        private readonly object _lock = new object();

        /// <summary>volatile: bật/tắt từ luồng giao diện, đọc từ thread mạng.</summary>
        private volatile bool _enabled;

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (!value) Clear();   // tat la don sach, khong de rac treo lai
            }
        }

        public void Clear()
        {
            lock (_lock)
                for (int i = 0; i < _slots.Length; i++) _slots[i].Text = null;
        }

        /// <summary>
        /// Thêm một số. Gọi TỪ THREAD MẠNG. Không làm gì khi cổng đang tắt — người gọi phải
        /// kiểm <see cref="Enabled"/> TRƯỚC khi dựng chuỗi để không tốn cấp phát.
        /// </summary>
        public void Push(string text, int x, int y, int templateId, byte kind)
        {
            if (!_enabled) return;
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    // slot trong, hoac slot da het doi
                    if (_slots[i].Text != null &&
                        (now - _slots[i].BornUtc).TotalMilliseconds < FlyText.LIFE_MS) continue;

                    _slots[i].Text = text;
                    _slots[i].X = x;
                    _slots[i].Y = y;
                    _slots[i].TemplateId = templateId;
                    _slots[i].Kind = kind;
                    _slots[i].BornUtc = now;
                    return;
                }
                // day 5 slot -> BO so moi, dung y client (GameScr.java:5781-5801 khong thay the)
            }
        }

        /// <summary>Bản sao các số CÒN SỐNG. Gọi từ luồng vẽ, mỗi khung một lần.</summary>
        public FlyText[] Snapshot()
        {
            if (!_enabled) return _empty;
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                // Dem TRUOC roi moi cap phat: ham nay chay ~30 lan/giay, ma gan nhu luc nao cung
                // rong (chi co so trong ~0,8 giay sau moi don danh). Cap phat mang tam moi lan goi
                // la rac vo nghia - dung tinh than "mo cua so cung phai nhe".
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (Alive(i, now)) n++;
                if (n == 0) return _empty;

                var outArr = new FlyText[n];
                int k = 0;
                for (int i = 0; i < _slots.Length && k < n; i++)
                    if (Alive(i, now)) outArr[k++] = _slots[i];
                return outArr;
            }
        }

        /// <summary>Slot còn sống không. Gọi TRONG khoá.</summary>
        private bool Alive(int i, DateTime now)
        {
            return _slots[i].Text != null
                && (now - _slots[i].BornUtc).TotalMilliseconds < FlyText.LIFE_MS;
        }

        private static readonly FlyText[] _empty = new FlyText[0];
    }
}
