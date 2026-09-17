using System;

namespace NSOKHODO.Models
{
    /// <summary>
    /// Một LẦN RA ĐÒN — đủ để lớp vẽ dựng lại toàn bộ hoạt cảnh: cử động thân người, 3 lớp hiệu
    /// ứng ở người đánh, hiệu ứng lan trên từng mục tiêu, phi tiêu.
    ///
    /// Nguồn đối chiếu (hai nguồn khớp nhau):
    ///   NSOPC   Char.cs:3621-3655 (bắt đầu) · :10749-10904 (chạy khung) · :4070-4115 (hiệu ứng lan)
    ///   MODGAME Controller.java:1589-1646 (cmd 60) · :1647-1706 (cmd 61)
    ///
    /// 🔑 THIẾT KẾ "PHÁT LẠI KHÔNG TRẠNG THÁI": struct này KHÔNG có bộ đếm khung. Nó chỉ ghi
    /// *một lần bấm nút* kèm <see cref="BornUtc"/>; lớp vẽ tự tính
    /// <c>step = (now - BornUtc) / FxTables.TICK_MS</c> rồi suy ra mọi thứ.
    /// Vì sao không mô phỏng <c>indexSkill++</c> như client:
    ///   • thread mạng chỉ ghi, thread vẽ chỉ đọc — không ô nào bị hai bên cùng sửa;
    ///   • thu nhỏ cửa sổ (timer dừng) rồi mở lại không bị kẹt hoạt cảnh dở;
    ///   • đổi nhịp vẽ KHÔNG đổi tốc độ hoạt cảnh — đúng bài học đã ghi ở <see cref="FlyText"/>.
    ///
    /// ⚠️ KHÔNG lưu toạ độ người đánh: client vẽ tại <c>cx/cy</c> HIỆN TẠI, mà nhân vật vẫn nhích
    /// trong 240 ms đó. Chỉ lưu danh tính (<see cref="IsMe"/> / <see cref="CasterId"/>).
    ///
    /// ⚠️ Mục tiêu QUÁI lưu thẳng toạ độ, mục tiêu NGƯỜI lưu id — cố ý lệch client: quái đứng im
    /// vĩnh viễn ở server này (SERVER_FACTS §18.5) nên hai cách cho cùng kết quả, mà lưu toạ độ
    /// thì tránh được bẫy "mobId là chỉ số ô mảng, quái hồi sinh có thể tái dùng ô đó".
    /// </summary>
    public struct SkillFxCast
    {
        /// <summary>Số hoạt cảnh chạy cùng lúc tối đa (mình + người khác trong màn).</summary>
        public const int MAX = 8;

        /// <summary>Trần tuổi thọ (ms) — chuỗi dài nhất là 23 bước × 40 ms = 920 ms.</summary>
        public const int MAX_LIFE_MS = 2000;

        /// <summary>Số mục tiêu tối đa client đọc từ gói (<c>Mob[10]</c> / <c>Char[10]</c>).</summary>
        public const int MAX_TARGET = 10;

        /// <summary>Đang dùng slot này không.</summary>
        public bool Active;

        /// <summary>Người đánh là chính mình. Khi đó bỏ qua <see cref="CasterId"/>.</summary>
        public bool IsMe;

        /// <summary>charId người đánh (khi <see cref="IsMe"/> = false).</summary>
        public int CasterId;

        /// <summary><c>SkillTemplate.Id</c> — tra thẳng vào <c>FxTables.Skill()</c>.</summary>
        public int SkillId;

        /// <summary>0 = đứng đất (<c>skillStand</c>), 1 = trên không (<c>skillfly</c>).</summary>
        public int SType;

        /// <summary>Hướng nhìn: 1 = phải, -1 = trái. Client chốt một lần rồi giữ nguyên cả đòn.</summary>
        public int Dir;

        /// <summary>Số mục tiêu thật trong hai mảng dưới.</summary>
        public int TargetCount;

        /// <summary>Toạ độ mục tiêu QUÁI (game coords). Chỉ <see cref="TargetCount"/> phần tử đầu có nghĩa.</summary>
        public short[] TxArr, TyArr;

        /// <summary>charId mục tiêu NGƯỜI; 0 nghĩa là ô đó là quái (dùng Tx/Ty).</summary>
        public int[] TCharId;

        /// <summary>Mốc sinh, giờ máy UTC. net452: KHÔNG dùng DateTimeOffset.</summary>
        public DateTime BornUtc;
    }

    /// <summary>
    /// Bảng hoạt cảnh đánh của MỘT account. Thread mạng / thread auto ghi (<see cref="Push"/>),
    /// luồng giao diện đọc (<see cref="Snapshot"/>) — nên có khoá.
    ///
    /// 🔑 <see cref="Enabled"/> mặc định TẮT, chỉ cửa sổ "Xem game" bật khi mở. Cùng lý do với
    /// <see cref="FlyTextBoard"/>: không có cổng này thì MỌI account đều cấp phát mảng mục tiêu ở
    /// mỗi đòn đánh — với fleet vài trăm account đang tàn sát là hàng nghìn mảng rác mỗi giây cho
    /// một thứ gần như không ai xem.
    ///
    /// ⚠️ KHÁC <see cref="FlyTextBoard"/> ở một điểm: <see cref="Push"/> tìm theo NGƯỜI ĐÁNH
    /// trước, có rồi thì GHI ĐÈ. Client reset <c>indexSkill = 0</c> trên chính đối tượng
    /// <c>Char</c> đó (<c>Char.cs:3646-3655</c>) — một người chỉ có MỘT hoạt cảnh đang chạy.
    /// Nếu cứ chiếm slot trống thì skill dài (920 ms) chồng lên nhịp đánh (400 ms) sẽ cho hai
    /// hoạt cảnh cùng chạy trên một nhân vật ⇒ thân người nhấp nháy giữa hai khung.
    /// </summary>
    public sealed class SkillFxBoard
    {
        private readonly SkillFxCast[] _slots = new SkillFxCast[SkillFxCast.MAX];
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
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i].Active = false;
                    _slots[i].TxArr = null;
                    _slots[i].TyArr = null;
                    _slots[i].TCharId = null;
                }
        }

        /// <summary>
        /// Ghi một lần ra đòn. Gọi TỪ THREAD MẠNG hoặc THREAD AUTO.
        /// Người gọi phải kiểm <see cref="Enabled"/> TRƯỚC khi dựng mảng để khỏi tốn cấp phát.
        /// </summary>
        /// <param name="isMe">Người đánh là chính mình.</param>
        /// <param name="casterId">charId người đánh (bỏ qua khi <paramref name="isMe"/>).</param>
        /// <param name="skillId">SkillTemplate.Id.</param>
        /// <param name="sType">0 đứng đất, 1 trên không.</param>
        /// <param name="dir">1 phải, -1 trái.</param>
        /// <param name="tx">Toạ độ X mục tiêu quái (null nếu chỉ đánh người).</param>
        /// <param name="ty">Toạ độ Y mục tiêu quái.</param>
        /// <param name="tCharId">charId mục tiêu người; 0 = ô đó là quái.</param>
        /// <param name="count">Số phần tử có nghĩa.</param>
        public void Push(bool isMe, int casterId, int skillId, int sType, int dir,
                         short[] tx, short[] ty, int[] tCharId, int count)
        {
            if (!_enabled) return;
            if (count <= 0) return;
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                int slot = -1;

                // 1) CUNG NGUOI DANH -> ghi de (client reset indexSkill tren chinh Char do)
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].Active) continue;
                    if (_slots[i].IsMe == isMe && (isMe || _slots[i].CasterId == casterId)) { slot = i; break; }
                }
                // 2) o trong hoac o da het doi
                if (slot < 0)
                {
                    for (int i = 0; i < _slots.Length; i++)
                    {
                        if (!_slots[i].Active ||
                            (now - _slots[i].BornUtc).TotalMilliseconds >= SkillFxCast.MAX_LIFE_MS)
                        { slot = i; break; }
                    }
                }
                // 3) day -> BO don moi, dung y client (khong thay the nguoi khac)
                if (slot < 0) return;

                _slots[slot].Active = true;
                _slots[slot].IsMe = isMe;
                _slots[slot].CasterId = casterId;
                _slots[slot].SkillId = skillId;
                _slots[slot].SType = sType;
                _slots[slot].Dir = (dir < 0) ? -1 : 1;
                _slots[slot].TxArr = tx;
                _slots[slot].TyArr = ty;
                _slots[slot].TCharId = tCharId;
                _slots[slot].TargetCount = count;
                _slots[slot].BornUtc = now;
            }
        }

        /// <summary>Bản sao các hoạt cảnh CÒN SỐNG. Gọi từ luồng vẽ, mỗi khung một lần.</summary>
        public SkillFxCast[] Snapshot()
        {
            if (!_enabled) return _empty;
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                // Dem TRUOC roi moi cap phat: ham nay chay ~30 lan/giay, phan lon thoi gian rong.
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (Alive(i, now)) n++;
                if (n == 0) return _empty;

                var outArr = new SkillFxCast[n];
                int k = 0;
                for (int i = 0; i < _slots.Length && k < n; i++)
                    if (Alive(i, now)) outArr[k++] = _slots[i];
                return outArr;
            }
        }

        /// <summary>Slot còn sống không. Gọi TRONG khoá.</summary>
        private bool Alive(int i, DateTime now)
        {
            return _slots[i].Active
                && (now - _slots[i].BornUtc).TotalMilliseconds < SkillFxCast.MAX_LIFE_MS;
        }

        private static readonly SkillFxCast[] _empty = new SkillFxCast[0];
    }
}
