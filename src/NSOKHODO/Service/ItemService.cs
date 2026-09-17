using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    /// <summary>
    /// Gui goi lien quan vat pham — BAN KHO: chi con nhung lenh KHONG lam thay doi ban chat mon do.
    ///
    /// <para>⚠️ <b>CO Y GO BO</b> (so voi NSOLITEPRO/NSOBAOTATL) moi lenh <b>dung / mac / thao / ban /
    /// vut / nang cap / luyen / tach trang bi / mua / dung bua</b>. Ly do da do tren server chinh
    /// (test tay T0, SPEC D38): mon do <b>chi bi khoa khi dem ra dung</b>. Kho giu do cua nguoi khac —
    /// mot lenh dung nham la mon do bi khoa vinh vien, khong giao dich duoc nua.</para>
    ///
    /// <para>Go o TANG GUI thay vi dat co: code nao lo goi se KHONG BUILD DUOC, thay vi chay ngam.
    /// Rieng <c>cmd 22</c> (splitItem) la "TACH TRANG BI" — pha do nang cap ra da
    /// (<c>NINJAPC mResources.cs:1267</c>: "Trang bi da duoc tach, ban nhan duoc ..."), KHONG phai
    /// tach chong. Tach chong la <see cref="SendSplitStack"/> (<c>-28/-85</c>).</para>
    /// </summary>
    public class ItemService
    {
        private readonly NsoSession _session;

        public ItemService(NsoSession session)
        {
            _session = session;
        }

        // typeUI quy uoc theo game 251: 3 = tui (bag), 5 = trang bi (body), 4 = ruong.
        public const byte TYPE_BAG = 3;
        public const byte TYPE_BODY = 5;
        public const byte TYPE_BOX = 4;

        /// <summary>
        /// typeUI cua o THU CUOI (game: <c>arrItemMounts[i].typeUI = 41</c>, Controller.cs:5605).
        /// Chi dung de phan biet kho trong giao dien.
        /// </summary>
        public const byte TYPE_MOUNT = 41;

        /// <summary>cmd 42: xin chi tiet 1 item (expires/option/gia). typeUI=3 tui, 5 trang bi.</summary>
        public void SendRequestItemInfo(byte typeUI, byte indexUI)
        {
            var msg = new NsoMessage(Cmd.REQUEST_ITEM_INFO);
            msg.Writer.WriteByte(typeUI);
            msg.Writer.WriteByte(indexUI);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// TACH CHONG: <c>-28 {sub -85, byte oTui, int soLuong}</c> — client goc
        /// <c>Service.inputNumSplit</c> (NINJAPC Service.cs:2431). Client goc chi cho gui khi
        /// <c>1 &lt;= soLuong &lt; soLuongTrongO</c> (GameCanvas.cs:2255) — nguoi goi phai tu kiem.
        /// Server KHONG co goi tra loi rieng: phan tach ra hien qua cac goi cap nhat tui (8/7/115),
        /// nguoi goi so tui truoc/sau de tim o moi.
        /// </summary>
        public void SendSplitStack(byte slot, int qty)
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.ITEM_SPLIT);
            msg.Writer.WriteByte(slot);
            msg.Writer.WriteInt(qty);
            _session.QueueMessage(msg);
        }

        /// <summary>sub -107: sap xep tui (server gop lai cac chong cung loai).</summary>
        public void SendSortBag()
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.SORT_BAG);
            _session.QueueMessage(msg);
        }

        /// <summary>cmd 16: lay 1 mon tu RUONG ve tui. Phai dung sat Thu kho (NPC 5).</summary>
        public void SendItemBoxToBag(byte boxSlot)
        {
            var msg = new NsoMessage(Cmd.ITEM_BOX_TO_BAG);
            msg.Writer.WriteByte(boxSlot);
            _session.QueueMessage(msg);
        }

        /// <summary>cmd 17: cat 1 mon tu tui vao RUONG. Phai dung sat Thu kho (NPC 5).</summary>
        public void SendItemBagToBox(byte bagSlot)
        {
            var msg = new NsoMessage(Cmd.ITEM_BAG_TO_BOX);
            msg.Writer.WriteByte(bagSlot);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// sub -103 (MODGAME Service.java:510 requestItem): xin danh sach 1 kho item.
        /// typeUI = 4 -> RUONG (server tra ve cmd 31). MODGAME muc "Thu kho" gui dung goi nay sau khi
        /// da dung sat NPC 5 (GameScr.java:14356-14362, :13026-13031).
        /// </summary>
        public void SendRequestItem(byte typeUI)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.REQUEST_ITEM);
            msg.Writer.WriteByte(typeUI);
            _session.QueueMessage(msg);
        }
    }
}
