using System;
using System.IO;
using System.Text;

namespace NSOKHODO.Core
{
    public class BigEndianBinaryReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        public BigEndianBinaryReader(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        public Stream BaseStream { get { return _stream; } }

        public int Available
        {
            get { return (int)(_stream.Length - _stream.Position); }
        }

        public byte ReadByte()
        {
            int b = _stream.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            return (byte)b;
        }

        public sbyte ReadSignedByte()
        {
            return (sbyte)ReadByte();
        }

        public int ReadUnsignedByte()
        {
            return ReadByte();
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public short ReadShort()
        {
            byte b1 = ReadByte();
            byte b2 = ReadByte();
            return (short)((b1 << 8) | b2);
        }

        public ushort ReadUnsignedShort()
        {
            byte b1 = ReadByte();
            byte b2 = ReadByte();
            return (ushort)((b1 << 8) | b2);
        }

        public int ReadInt()
        {
            byte b1 = ReadByte();
            byte b2 = ReadByte();
            byte b3 = ReadByte();
            byte b4 = ReadByte();
            return (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
        }

        public long ReadLong()
        {
            byte[] buf = ReadFully(8);
            return ((long)buf[0] << 56) | ((long)buf[1] << 48) |
                   ((long)buf[2] << 40) | ((long)buf[3] << 32) |
                   ((long)buf[4] << 24) | ((long)buf[5] << 16) |
                   ((long)buf[6] << 8) | buf[7];
        }

        public string ReadUTF()
        {
            ushort len = ReadUnsignedShort();
            if (len == 0) return string.Empty;
            byte[] buf = ReadFully(len);
            return DecodeModifiedUTF8(buf, len);
        }

        public byte[] ReadFully(int count)
        {
            byte[] buf = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = _stream.Read(buf, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException();
                offset += read;
            }
            return buf;
        }

        public void Skip(int count)
        {
            if (_stream.CanSeek)
            {
                _stream.Seek(count, SeekOrigin.Current);
            }
            else
            {
                ReadFully(count);
            }
        }

        private static string DecodeModifiedUTF8(byte[] buf, int len)
        {
            var sb = new StringBuilder(len);
            int i = 0;
            while (i < len)
            {
                byte c = buf[i++];
                if ((c & 0x80) == 0)
                {
                    sb.Append((char)c);
                }
                else if ((c & 0xE0) == 0xC0)
                {
                    if (i >= len) break;
                    byte c2 = buf[i++];
                    sb.Append((char)(((c & 0x1F) << 6) | (c2 & 0x3F)));
                }
                else if ((c & 0xF0) == 0xE0)
                {
                    if (i + 1 >= len) break;
                    byte c2 = buf[i++];
                    byte c3 = buf[i++];
                    sb.Append((char)(((c & 0x0F) << 12) | ((c2 & 0x3F) << 6) | (c3 & 0x3F)));
                }
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            if (!_leaveOpen)
                _stream.Dispose();
        }
    }
}
