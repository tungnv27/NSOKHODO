using System;
using System.Collections.Generic;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    /// <summary>
    /// Ket ban (cmd 59) - clone client goc NinjaSchool_251 `Service.addFriend(name)`:
    /// cmd 59 + writeUTF(ten nhan vat). Khong co ID, khong can dung map, khong can focus.
    ///
    /// Dung de "chao hoi" TRUOC khi tuong tac voi nguoi choi khac (moi nhom / xin vao nhom /
    /// sau nay: giao dich). Xem CommandCodes.FRIEND_ADD cho chieu nhan.
    ///
    /// <para>CHONG SPAM: vong moi nhom chay lai moi 10 giay, neu goi thang thi mot thanh vien
    /// offline se an mot goi ket ban moi 10 giay suot ca phien. <see cref="TryAddFriend"/> chan
    /// theo TEN + moc thoi gian; <see cref="SendAddFriend"/> la ban khong chan (dung khi that su
    /// muon gui ngay).</para>
    /// </summary>
    public class FriendService
    {
        private readonly NsoSession _session;

        // Ten -> lan cuoi da gui ket ban. Bi cham tu thread Auto (vong moi nhom) lan thread MANG
        // (dong y loi moi ket ban den) nen phai khoa.
        private readonly Dictionary<string, DateTime> _lastSent =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        /// <summary>Cung mot ten thi it nhat bao lau moi duoc gui ket ban lai.</summary>
        public const int RESEND_GAP_MS = 60000;

        public FriendService(NsoSession session)
        {
            _session = session;
        }

        /// <summary>Gui ket ban NGAY (khong hoi bo dem chong spam).</summary>
        public void SendAddFriend(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            string key = name.Trim();
            if (key.Length == 0) return;

            lock (_lock) { _lastSent[key] = DateTime.Now; }
            Emit(key);
        }

        /// <summary>
        /// Gui ket ban neu chua gui cho ten nay trong <see cref="RESEND_GAP_MS"/> ms gan day.
        /// Tra ve true khi VUA gui that - caller nen cho mot nhip cho server xu ly xong
        /// truoc khi ban goi tuong tac tiep theo (moi nhom / xin vao nhom).
        /// </summary>
        public bool TryAddFriend(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string key = name.Trim();
            if (key.Length == 0) return false;

            lock (_lock)
            {
                DateTime last;
                if (_lastSent.TryGetValue(key, out last)
                    && (DateTime.Now - last).TotalMilliseconds < RESEND_GAP_MS)
                    return false;
                _lastSent[key] = DateTime.Now;
            }
            Emit(key);
            return true;
        }

        private void Emit(string name)
        {
            var msg = new NsoMessage(Cmd.FRIEND_ADD);
            msg.Writer.WriteUTF(name);
            _session.QueueMessage(msg);
        }
    }
}
