using System;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class CombatService
    {
        private readonly NsoSession _session;

        /// <summary>Bao "ta vua gui mot don danh" (dd15) - NsoClient noi vao GameStateManager.NoteAttackSent.
        /// Dat o day de giu nguyen hinh dang tang Service (ca 10 service chi nhan NsoSession).</summary>
        public Action OnAttackQueued;

        public CombatService(NsoSession session)
        {
            _session = session;
        }

        /// <summary>Bao "ta vua gui don danh QUAI" (di3) - NsoClient noi vao GameStateManager.NoteMobAttackSent.
        /// Tach khoi <see cref="OnAttackQueued"/> vi SongGia chi duoc do tren don danh quai.</summary>
        public Action OnMobAttackQueued;

        private void BaoDaDanhQuai()
        {
            var h = OnMobAttackQueued;
            if (h != null) { try { h(); } catch { } }
        }

        public void SendAttackMob(byte[] mobIds)
        {
            var msg = new NsoMessage(Cmd.ATTACK_MOB);
            msg.Writer.WriteBytes(mobIds);
            _session.QueueMessage(msg);
            BaoDaDanh();
            BaoDaDanhQuai();
        }

        public void SendAttackChar(int[] charIds)
        {
            var msg = new NsoMessage(Cmd.ATTACK_CHAR);
            foreach (int id in charIds)
                msg.Writer.WriteInt(id);
            _session.QueueMessage(msg);
            BaoDaDanh();
        }

        /// <summary>
        /// Service.acceptInviteTestDun(id) - cmd=99, writeInt(charId). Acc phu CHAP NHAN loi moi
        /// len loi dai (server moi bang chinh cmd 99 kem charID nguoi moi).
        /// ⚠️ TUYET DOI khong dung cmd 106 (acceptInviteTestGT) - do la thach dau GIA TOC.
        /// Port nham opcode nay tung lam acc phu ben MODGAME khong bao gio nhan duoc loi moi
        /// (DANH_VONG.md §O.8.1: "Nghi ngo mapping thi so OPCODE, dung so ten ham").
        /// </summary>
        public void SendAcceptInviteDuel(int charId)
        {
            var msg = new NsoMessage(Cmd.ACCEPT_INVITE_DUEL);
            msg.Writer.WriteInt(charId);
            _session.QueueMessage(msg);
        }

        public void SendAttackMixed(byte[] mobIds, int[] charIds)
        {
            var msg = new NsoMessage(Cmd.PLAYER_ATTACK);
            msg.Writer.WriteByte((byte)mobIds.Length);
            msg.Writer.WriteBytes(mobIds);
            msg.Writer.WriteByte((byte)charIds.Length);
            foreach (int id in charIds)
                msg.Writer.WriteInt(id);
            _session.QueueMessage(msg);
            BaoDaDanh();
            if (mobIds.Length > 0) BaoDaDanhQuai();
        }

        private void BaoDaDanh()
        {
            var h = OnAttackQueued;
            if (h != null) { try { h(); } catch { } }
        }

        /// <summary>
        /// Doi che do PK (clone MODGAME Service.changePk): SUB_COMMAND(-30) + byte sub(-93) + byte typePk.
        /// typePk: 0 = hoa binh (khong PK), 1 = PK chinh, 3 = PK tat ca. PK Am dung 3 de danh nguoi.
        /// </summary>
        public void SendChangePk(int typePk)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.CHANGE_PK);
            msg.Writer.WriteByte((byte)typePk);
            _session.QueueMessage(msg);
        }

        public void SendSelectSkill(short skillId)
        {
            var msg = new NsoMessage(Cmd.SELECT_SKILL);
            msg.Writer.WriteShort(skillId);
            _session.QueueMessage(msg);
        }

        public void SendUseSkillBuff(byte direction)
        {
            var msg = new NsoMessage(Cmd.USE_SKILL_BUFF);
            msg.Writer.WriteByte(direction);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Hoi sinh xa 1 member (phai Quat) - clone MODGAME 251 Service.buffLive: SUB_COMMAND(-30) +
        /// sub BUFF_LIVE(-79) + writeInt(charId). ⚠️ CHUA VERIFY HEX server nay (xem SubCmd.Sub.BUFF_LIVE).
        /// </summary>
        public void SendBuffLive(int charId)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.BUFF_LIVE);
            msg.Writer.WriteInt(charId);
            _session.QueueMessage(msg);
        }
    }
}
