using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class NpcService
    {
        private readonly NsoSession _session;

        public NpcService(NsoSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Service.openMenu(npcTemplateId) - cmd=40, writeShort(templateId)
        /// </summary>
        public void SendOpenMenu(short npcTemplateId)
        {
            var msg = new NsoMessage(Cmd.NPC_OPEN_MENU);
            msg.Writer.WriteShort(npcTemplateId);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Service.menu(npcId, menuId, optionId) - cmd=29, writeByte x3.
        /// **NSO 240 protocol** (3 bytes), KHÔNG phải 251 (4 bytes).
        /// Tham chiếu: D:\10\NSOTool\src\Service.java:465 + GameScr.java:18213.
        /// Bit-encode: GameScr.b(npcId, menu, item) → wire = [npcId, menu, item].
        /// </summary>
        public void SendNpcMenu(byte npcId, byte menuId, byte optionId)
        {
            var msg = new NsoMessage(Cmd.NPC_SELECT_MENU);
            msg.Writer.WriteByte(npcId);
            msg.Writer.WriteByte(menuId);
            msg.Writer.WriteByte(optionId);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Service.getTask(npcId, menuId) - cmd=47, writeByte x2. Clone ZangVPS ad_0.m(int,int)
        /// (ad_0.java:2334-2343). Xem ghi chu nguon o <see cref="Cmd.GET_TASK"/>.
        /// </summary>
        public void SendGetTask(byte npcId, byte menuId)
        {
            var msg = new NsoMessage(Cmd.GET_TASK);
            msg.Writer.WriteByte(npcId);
            msg.Writer.WriteByte(menuId);
            _session.QueueMessage(msg);
        }

        public void SendSelectMenu(byte index)
        {
            var msg = new NsoMessage(Cmd.NPC_SELECT_MENU);
            msg.Writer.WriteByte(index);
            _session.QueueMessage(msg);
        }

        public void SendMenuId(byte menuId)
        {
            var msg = new NsoMessage(Cmd.NPC_MENU_ID);
            msg.Writer.WriteByte(menuId);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Service.sendUIConfirmID(id) - cmd=107, writeByte(id). Xac nhan hop thoai server dang mo.
        /// Danh Vong: <b>id = 8 = HUY nhiem vu danh vong</b>, di kem <see cref="SendOpenMenu"/>(2)
        /// va KHONG gui cmd 29 (DANH_VONG.md §B.3).
        /// </summary>
        public void SendUIConfirmId(byte id)
        {
            var msg = new NsoMessage(Cmd.UI_CONFIRM_ID);
            msg.Writer.WriteByte(id);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Service.textBoxId(id, text) - cmd=92, writeShort(id) + writeUTF(text).
        /// Loi dai: id <b>2</b> = ten doi thu, id <b>3</b> = xu cuoc (dang CHUOI thap phan).
        /// Ban goc gui thang khi dang DUNG DUNG toa do NPC 0, khong mo menu truoc.
        /// </summary>
        public void SendTextBoxId(short id, string text)
        {
            var msg = new NsoMessage(Cmd.TEXT_BOX_ID);
            msg.Writer.WriteShort(id);
            msg.Writer.WriteUTF(text ?? "");
            _session.QueueMessage(msg);
        }
    }
}
