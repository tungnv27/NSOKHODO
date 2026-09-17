using System;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    /// <summary>
    /// Chieu NHAN cua giao dich (37, 43, 45, 46, 57, 58). Chi PARSE roi ghi vao <see cref="TradeState"/>;
    /// quyet dinh la viec cua mode kho.
    ///
    /// <para>Wire: NINJAPC Controller.cs (43 :1193, 37 :1884, 45 :1474, 46 :1466, 57 :531, 58 :536),
    /// khop bytecode NSOTRUNGDUC 217. Xem docs/GIAO_DICH.md §3.2.</para>
    /// </summary>
    public class TradeHandler
    {
        private readonly GameStateManager _state;
        private readonly TradeState _trade;

        /// <summary>So mon toi da mot lan dat. Client goc tao mang 12 o va tran neu n &gt; 12.</summary>
        public const int MAX_MON = 12;

        public TradeHandler(GameStateManager state, TradeState trade)
        {
            _state = state;
            _trade = trade;
        }

        public void Handle(sbyte cmd, NsoMessage msg)
        {
            var r = msg.Reader;
            switch (cmd)
            {
                case Cmd.TRADE_INVITE:
                    _trade.GhiLoiMoi(r.ReadInt());
                    break;

                case Cmd.TRADE_OPEN_UI:
                    {
                        string ten = "";
                        try { ten = r.ReadUTF(); } catch { }
                        _trade.GhiMoKhung(ten);
                    }
                    break;

                case Cmd.TRADE_LOCK_ITEM:
                    {
                        int xu = r.ReadInt();
                        int n = r.ReadByte() & 0xFF;
                        if (n > MAX_MON)
                            throw new InvalidOperationException("goi 45 co " + n + " mon (> " + MAX_MON + ") - coi la goi hong");
                        var mon = new MonGiaoDich[n];
                        for (int i = 0; i < n; i++)
                        {
                            var m = new MonGiaoDich();
                            m.TemplateId = r.ReadShort();
                            // Luat byte nang cap cua goi 45 GIONG goi 8/31: chi body (0..15) va ngoc kham (34).
                            // KHAC sub -127/115 (co them thu cuoi) - gop hai luat la lech 1 byte, vo ca goi.
                            var tpl = _state.ItemStore != null ? _state.ItemStore.Get(m.TemplateId) : null;
                            if (tpl != null && (tpl.IsTypeBody || tpl.IsTypeNgocKham))
                                m.Upgrade = r.ReadByte();
                            m.IsExpires = r.ReadBoolean();
                            m.Quantity = r.ReadShort();
                            if (m.Quantity <= 0) m.Quantity = 1;
                            mon[i] = m;
                        }
                        _trade.GhiDoiPhuongKhoa(xu, mon);
                    }
                    break;

                case Cmd.TRADE_ACCEPT:
                    _trade.GhiDoiPhuongDongY();
                    break;

                case Cmd.TRADE_CANCEL:
                    _trade.GhiHuy();
                    break;

                case Cmd.TRADE_OK:
                    {
                        int xu = r.ReadInt();
                        var c = _state.MyChar;
                        if (c != null) c.Xu = xu;   // client goc: Char.xu = readInt() (Controller.cs:545)
                        _trade.GhiXong(xu);
                    }
                    break;
            }
        }
    }
}
