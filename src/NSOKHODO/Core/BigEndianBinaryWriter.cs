using System;
using System.IO;
using System.Text;

namespace NSOKHODO.Core
{
    public class BigEndianBinaryWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        public BigEndianBinaryWriter(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        public Stream BaseStream { get { return _stream; } }

        public void WriteByte(byte value)
        {
            _stream.WriteByte(value);
        }

        public void WriteSignedByte(sbyte value)
        {
            _stream.WriteByte((byte)value);
        }

        public void WriteBoolean(bool value)
        {
            _stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteShort(short value)
        {
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }

        public void WriteUnsignedShort(ushort value)
        {
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }

        public void WriteInt(int value)
        {
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }

        public void WriteLong(long value)
        {
            _stream.WriteByte((byte)(value >> 56));
            _stream.WriteByte((byte)(value >> 48));
            _stream.WriteByte((byte)(value >> 40));
            _stream.WriteByte((byte)(value >> 32));
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }

        public void WriteUTF(string value)
        {
            if (value == null) value = string.Empty;
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            if (utf8.Length > 65535)
                throw new InvalidOperationException("UTF string too long: " + utf8.Length);
            WriteUnsignedShort((ushort)utf8.Length);
            _stream.Write(utf8, 0, utf8.Length);
        }

        public void WriteBytes(byte[] data)
        {
            if (data != null && data.Length > 0)
                _stream.Write(data, 0, data.Length);
        }

        public void Flush()
        {
            _stream.Flush();
        }

        public void Dispose()
        {
            if (!_leaveOpen)
                _stream.Dispose();
        }
    }
}
