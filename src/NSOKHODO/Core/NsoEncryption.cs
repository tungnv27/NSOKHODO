namespace NSOKHODO.Core
{
    public class NsoEncryption
    {
        private byte[] _key;
        private int _readPos;
        private int _writePos;

        public bool IsReady { get; private set; }

        public void SetKey(byte[] rawKey)
        {
            _key = new byte[rawKey.Length];
            System.Array.Copy(rawKey, _key, rawKey.Length);

            // XOR cascade: key[i+1] ^= key[i]
            for (int i = 0; i < _key.Length - 1; i++)
            {
                _key[i + 1] ^= _key[i];
            }

            _readPos = 0;
            _writePos = 0;
            IsReady = true;
        }

        public byte DecryptByte(byte b)
        {
            byte result = (byte)(b ^ _key[_readPos]);
            _readPos = (_readPos + 1) % _key.Length;
            return result;
        }

        public byte EncryptByte(byte b)
        {
            byte result = (byte)(b ^ _key[_writePos]);
            _writePos = (_writePos + 1) % _key.Length;
            return result;
        }

        public void Reset()
        {
            _key = null;
            _readPos = 0;
            _writePos = 0;
            IsReady = false;
        }
    }
}
