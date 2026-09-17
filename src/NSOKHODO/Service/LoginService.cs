using System;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class LoginService
    {
        private readonly NsoSession _session;

        public LoginService(NsoSession session)
        {
            _session = session;
        }

        public void SendHandshake()
        {
            var msg = new NsoMessage(Cmd.HANDSHAKE);
            _session.SendDirect(msg);
        }

        /// <summary>
        /// Ngôn ngữ khai với server trong <c>setClientType</c>: <c>mResources.Lang_VI = 0</c>
        /// (<c>Lang_EN = 1</c>, <c>Lang_CAM = 2</c>). Server sinh thông báo / tên map / tên NV theo byte
        /// này — bot đang khớp chuỗi tiếng Việt ở nhiều chỗ ("Không đủ MP", "vào lại game sau",
        /// "quá nhanh"...) nên PHẢI là tiếng Việt.
        ///
        /// Trước 2026-09-14 ô này bị ghi <c>serverLogin</c> ⇒ acc Sanzu/Tessen (=1) tự khai là client
        /// tiếng Anh, Fukiya (=3) khai một ngôn ngữ không tồn tại. Client gốc tách hai field:
        /// <c>setClientType</c> ghi <c>languageID</c> (NinjaSchool_251_src/Service.cs:242), còn
        /// <c>serverLogin</c> CHỈ nằm ở byte cuối gói <c>login</c> (Service.cs:302) — xem SERVER_FACTS §11.
        /// </summary>
        public const byte LANGUAGE_VI = 0;

        /// <param name="clientType">
        /// Byte 1. Mặc định <c>1</c> (J2ME) — giữ nguyên hành vi cũ của cả dàn.
        /// Client PC gửi <c>0</c> khi zoom = 1 và <c>4</c> khi zoom &gt; 1.
        /// </param>
        /// <param name="graphicsMode">
        /// Byte 2. <b>Đây là MỨC ZOOM (1…4), không phải cờ bật/tắt</b> — SERVER_FACTS §19.1b:
        /// bản PC ghi <c>mGraphics.zoomLevel</c>, bản JAR ghi <c>mGraphics.b</c>, và cả hai đều
        /// dùng chính giá trị đó để ghép đường dẫn tài nguyên <c>"/x" + N</c>.
        ///
        /// Mặc định <c>0</c> — <b>cố ý giữ nguyên cho cả dàn</b>: với 0 thì server không gửi dữ liệu
        /// ảnh, tức là ít gói hơn cho bot. Chỉ công cụ "Đồng bộ ảnh" mới khai khác 0, và chỉ cho
        /// ĐÚNG MỘT account (xem <c>GfxDump</c> ben NSOLITEPRO) — đây là chốt chặn của rủi ro R4
        /// trong features/XEM_GAME_PC.md.
        /// </param>
        public void SendSetClientType(byte clientType = 1, byte graphicsMode = 0)
        {
            var msg = new NsoMessage(Cmd.NOT_LOGIN);
            msg.Writer.WriteSignedByte(SubCmd.NotLogin.SET_CLIENT_TYPE);

            // Exact format from Service.java setClientType()
            msg.Writer.WriteByte(clientType);     // clientType (GameMidlet.e = 1)
            msg.Writer.WriteByte(graphicsMode);   // graphicsMode == zoomLevel (SERVER_FACTS §19.1b)
            msg.Writer.WriteBoolean(false);       // isGPRS
            msg.Writer.WriteInt(320);             // screenWidth (int, not short!)
            msg.Writer.WriteInt(480);             // screenHeight (int, not short!)
            msg.Writer.WriteBoolean(true);        // hasTextField
            msg.Writer.WriteBoolean(false);       // isTouch
            msg.Writer.WriteUTF("Nokia");         // platform
            msg.Writer.WriteByte(0);              // padding byte
            msg.Writer.WriteInt(0);               // padding int
            msg.Writer.WriteByte(LANGUAGE_VI);    // languageID - KHONG phai serverLogin (xem LANGUAGE_VI)
            msg.Writer.WriteInt(0);               // provider
            msg.Writer.WriteUTF("0");             // agent

            _session.QueueMessage(msg);
        }

        public void SendLogin(string username, string password, byte serverLogin)
        {
            username = username.ToLower().Trim();
            password = password.ToLower().Trim();
            string random12 = GenerateRandom12();

            var msg = new NsoMessage(Cmd.NOT_LOGIN);
            msg.Writer.WriteSignedByte(SubCmd.NotLogin.LOGIN);

            // Exact format from Service.java login()
            msg.Writer.WriteUTF(username);
            msg.Writer.WriteUTF(password);
            msg.Writer.WriteUTF("1.8.0");         // version
            msg.Writer.WriteUTF("");               // empty string 1
            msg.Writer.WriteUTF("");               // empty string 2
            msg.Writer.WriteUTF(random12);         // random 12 digits
            msg.Writer.WriteByte(serverLogin);     // serverLogin byte

            _session.QueueMessage(msg);
        }

        public void SendClientOk()
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.CLIENT_OK);
            _session.QueueMessage(msg);
        }

        public void SendSelectChar(string charName)
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.CHAR_LIST);
            msg.Writer.WriteUTF(charName);
            _session.QueueMessage(msg);
        }

        // Tao nhan vat. Wire (verify game goc 251 Service.createChar + MODGAME): sub -125, UTF name,
        // byte gender, byte hair. Param 3 la TOC (hairID), KHONG phai phai/class - phai chon sau trong
        // game qua nhiem vu. NV moi tao la "tan thu" chua co phai.
        public void SendCreateChar(string name, byte gender, byte hair)
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.CREATE_CHAR);
            msg.Writer.WriteUTF(name);
            msg.Writer.WriteByte(gender);
            msg.Writer.WriteByte(hair);
            _session.QueueMessage(msg);
        }

        public static string GenerateRandom12()
        {
            var rng = new Random();
            var sb = new System.Text.StringBuilder(12);
            for (int i = 0; i < 12; i++)
                sb.Append(rng.Next(10));
            return sb.ToString();
        }
    }
}
