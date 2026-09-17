using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    /// <summary>
    /// Chieu GUI cua giao dich giua hai nguoi choi. Wire: NINJAPC Service.cs (tradeInvite :1535,
    /// acceptInviteTrade :1072, tradeItemLock :1221, tradeAccept :1203, cancelInviteTrade :1167,
    /// cancelTrade :1185), khop NSOTRUNGDUC Class_fp tung byte. Xem docs/GIAO_DICH.md §3.1.
    ///
    /// <para>Lop nay CHI dong goi. Thu tu goi, cho doi, kiem tra do <c>Kho.PhienGiaoDich</c> lo.</para>
    /// </summary>
    public class TradeService
    {
        private readonly NsoSession _session;

        public TradeService(NsoSession session)
        {
            _session = session;
        }

        /// <summary>43: moi <paramref name="charId"/> giao dich. Server khoa loi moi 31 giay (test tay T4).</summary>
        public void SendInvite(int charId)
        {
            var msg = new NsoMessage(Cmd.TRADE_INVITE);
            msg.Writer.WriteInt(charId);
            _session.QueueMessage(msg);
        }

        /// <summary>44: nhan loi moi cua <paramref name="inviterId"/>.</summary>
        public void SendAcceptInvite(int inviterId)
        {
            var msg = new NsoMessage(Cmd.TRADE_INVITE_ACCEPT);
            msg.Writer.WriteInt(inviterId);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// 45: dat do + xu roi KHOA, mot goi duy nhat. <paramref name="bagSlots"/> la chi so o tui,
        /// moi o di NGUYEN CHONG (khong co truong so luong). Toi da 12 o. Khoa rong = xu 0, n 0.
        /// </summary>
        public void SendLock(int xu, byte[] bagSlots)
        {
            int n = bagSlots == null ? 0 : bagSlots.Length;
            var msg = new NsoMessage(Cmd.TRADE_LOCK_ITEM);
            msg.Writer.WriteInt(xu < 0 ? 0 : xu);
            msg.Writer.WriteByte((byte)n);
            for (int i = 0; i < n; i++) msg.Writer.WriteByte(bagSlots[i]);
            _session.QueueMessage(msg);
        }

        /// <summary>46: dong y chot giao dich (goi rong).</summary>
        public void SendAccept()
        {
            _session.QueueMessage(new NsoMessage(Cmd.TRADE_ACCEPT));
        }

        /// <summary>56: tu choi loi moi (goi rong).</summary>
        public void SendRejectInvite()
        {
            _session.QueueMessage(new NsoMessage(Cmd.TRADE_INVITE_CANCEL));
        }

        /// <summary>57: huy giao dich / don phien sau khi xong (goi rong).</summary>
        public void SendCancel()
        {
            _session.QueueMessage(new NsoMessage(Cmd.TRADE_CANCEL));
        }
    }
}
