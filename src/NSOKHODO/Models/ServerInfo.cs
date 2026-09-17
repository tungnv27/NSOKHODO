namespace NSOKHODO.Models
{
    public class ServerInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        /// <summary>
        /// "serverLogin" - field thu 4 trong NJVI.txt (Name:IP:Port:serverLogin:type).
        /// Phan biet cac server DUNG CHUNG IP:Port (vd Shuriken=0/Tessen=1 cung 27.0.14.73:14444,
        /// Tone=0/Sanzu=1 cung 112.213.94.205:14444). Byte nay GUI khi login (setClientType +
        /// login) -> sai gia tri = vao nham server. Truoc day ten la "Language" (luon 0 -> bug).
        /// </summary>
        public byte ServerLogin { get; set; }

        /// <summary>Field thu 5 trong NJVI.txt (type/region flag). Giu de tuong lai, khong dung khi login.</summary>
        public byte Type { get; set; }

        /// <summary>True neu nguoi dung tu them (luu rieng servers_custom.txt, khong bi refresh URL ghi de).</summary>
        public bool IsCustom { get; set; }

        public ServerInfo(int index, string name, string host, int port, byte serverLogin)
        {
            Index = index;
            Name = name;
            Host = host;
            Port = port;
            ServerLogin = serverLogin;
        }

        /// <summary>Dong NJVI.txt: Name:IP:Port:serverLogin:type</summary>
        public string ToRawLine()
        {
            return string.Format("{0}:{1}:{2}:{3}:{4}", Name, Host, Port, ServerLogin, Type);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
