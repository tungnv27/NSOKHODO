using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class DataSyncService
    {
        private readonly NsoSession _session;

        public DataSyncService(NsoSession session)
        {
            _session = session;
        }

        public void SendUpdateData()
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.UPDATE_DATA);
            _session.QueueMessage(msg);
        }

        public void SendUpdateMap()
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.UPDATE_MAP);
            _session.QueueMessage(msg);
        }

        public void SendUpdateSkill()
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.UPDATE_SKILL);
            _session.QueueMessage(msg);
        }

        public void SendUpdateItem()
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.UPDATE_ITEM);
            _session.QueueMessage(msg);
        }
    }
}
