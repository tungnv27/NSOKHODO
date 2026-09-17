using System;
using System.IO;

namespace NSOKHODO.Core
{
    public class NsoMessage : IDisposable
    {
        private MemoryStream _ms;

        public sbyte Command { get; private set; }

        // For building outgoing messages
        public NsoMessage(sbyte command)
        {
            Command = command;
            _ms = new MemoryStream();
            Writer = new BigEndianBinaryWriter(_ms, true);
        }

        // For reading incoming messages
        public NsoMessage(sbyte command, byte[] data)
        {
            Command = command;
            _ms = new MemoryStream(data ?? new byte[0]);
            Reader = new BigEndianBinaryReader(_ms, true);
        }

        public BigEndianBinaryReader Reader { get; private set; }
        public BigEndianBinaryWriter Writer { get; private set; }

        public byte[] GetData()
        {
            if (Writer != null)
            {
                Writer.Flush();
                return _ms.ToArray();
            }
            return _ms.ToArray();
        }

        public int DataLength
        {
            get { return (int)_ms.Length; }
        }

        public void Dispose()
        {
            if (Reader != null) Reader.Dispose();
            if (Writer != null) Writer.Dispose();
            if (_ms != null) _ms.Dispose();
        }
    }
}
