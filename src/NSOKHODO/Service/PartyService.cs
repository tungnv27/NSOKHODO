using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class PartyService
    {
        private readonly NsoSession _session;

        public PartyService(NsoSession session)
        {
            _session = session;
        }

        public void SendCreateParty()
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.CREATE_PARTY);
            _session.QueueMessage(msg);
        }

        public void SendInviteByName(string name)
        {
            var msg = new NsoMessage(Cmd.PARTY_INVITE);
            msg.Writer.WriteUTF(name);
            _session.QueueMessage(msg);
        }

        public void SendAcceptInvite(int charId)
        {
            var msg = new NsoMessage(Cmd.PARTY_ACCEPT_INVITE);
            msg.Writer.WriteInt(charId);
            _session.QueueMessage(msg);
        }

        public void SendCancelInvite(int charId)
        {
            var msg = new NsoMessage(Cmd.PARTY_CANCEL);
            msg.Writer.WriteInt(charId);
            _session.QueueMessage(msg);
        }

        public void SendRequestJoin(string name)
        {
            var msg = new NsoMessage(Cmd.PARTY_REQUEST_JOIN);
            msg.Writer.WriteUTF(name);
            _session.QueueMessage(msg);
        }

        public void SendAcceptJoin(string name)
        {
            var msg = new NsoMessage(Cmd.PARTY_ACCEPT_JOIN);
            msg.Writer.WriteUTF(name);
            _session.QueueMessage(msg);
        }

        public void SendLeaveParty()
        {
            var msg = new NsoMessage(Cmd.PARTY_LEAVE);
            _session.QueueMessage(msg);
        }

        public void SendKickMember(byte index)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.KICK_MEMBER);
            msg.Writer.WriteByte(index);
            _session.QueueMessage(msg);
        }

        public void SendChangeLeader(byte index)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.CHANGE_LEADER);
            msg.Writer.WriteByte(index);
            _session.QueueMessage(msg);
        }

        public void SendLockParty(bool locked)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.LOCK_PARTY);
            msg.Writer.WriteBoolean(locked);
            _session.QueueMessage(msg);
        }
    }
}
