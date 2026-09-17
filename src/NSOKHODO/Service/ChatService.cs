using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class ChatService
    {
        private readonly NsoSession _session;

        public ChatService(NsoSession session)
        {
            _session = session;
        }

        public void SendPublicChat(string text)
        {
            var msg = new NsoMessage(Cmd.CHAT_PUBLIC);
            msg.Writer.WriteUTF(text);
            _session.QueueMessage(msg);
        }

        public void SendPrivateChat(string toName, string text)
        {
            var msg = new NsoMessage(Cmd.CHAT_PRIVATE);
            msg.Writer.WriteUTF(toName);
            msg.Writer.WriteUTF(text);
            _session.QueueMessage(msg);
        }

        public void SendGlobalChat(string text)
        {
            var msg = new NsoMessage(Cmd.CHAT_GLOBAL);
            msg.Writer.WriteUTF(text);
            _session.QueueMessage(msg);
        }

        public void SendPartyChat(string text)
        {
            var msg = new NsoMessage(Cmd.CHAT_PARTY);
            msg.Writer.WriteUTF(text);
            _session.QueueMessage(msg);
        }

        public void SendClanChat(string text)
        {
            var msg = new NsoMessage(Cmd.CHAT_CLAN);
            msg.Writer.WriteUTF(text);
            _session.QueueMessage(msg);
        }
    }
}
