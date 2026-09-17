using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    /// <summary>
    /// Cac lenh TIEN HOA NHAN VAT: cong diem tiem nang, hoc/nang ky nang.
    ///
    /// ⚠️ CA HAI LENH DEU KHONG HOAN TAC DUOC tren server. Moi cho goi PHAI hoi xac nhan truoc
    /// (xem InventoryForm) - day chi la tang gui gap, no khong tu bao ve.
    ///
    /// Wire chot bang HAI NGUON DOC LAP, khop tung byte:
    ///   NinjaSchool_251_src/Service.cs:536-570  +  MODGAME/src/Service.java:228-252
    /// </summary>
    public class CharService
    {
        private readonly NsoSession _session;

        public CharService(NsoSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Cong diem tiem nang: <c>cmd -30 · byte -109 · byte index · short point</c>.
        /// <paramref name="index"/>: 0 = Suc manh, 1 = Than phap, 2 = The luc, 3 = Chakra.
        /// Server tra loi bang chinh sub -109 (xem SubCommandHandler.HandlePotentialUp).
        /// </summary>
        public void SendUpPotential(byte index, short point)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.POTENTIAL_UP);
            msg.Writer.WriteByte(index);
            msg.Writer.WriteShort(point);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Hoc / nang ky nang: <c>cmd -30 · byte -108 · short skillTemplateId · byte point</c>.
        ///
        /// ⚠️ Gui <b>template.Id</b>, KHONG phai per-level <c>skillId</c> - dung cai bay da ghi o
        /// SERVER_FACTS §8 cho cmd 41 (gui nham thi server lang im hoac lam sai chieu).
        /// Server tra loi bang sub -125 (xem SubCommandHandler.HandleLoadSkill).
        /// </summary>
        public void SendUpSkill(short skillTemplateId, byte point)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.SKILL_UP);
            msg.Writer.WriteShort(skillTemplateId);
            msg.Writer.WriteByte(point);
            _session.QueueMessage(msg);
        }
    }
}
