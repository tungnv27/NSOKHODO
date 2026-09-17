using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class MiscService
    {
        private readonly NsoSession _session;

        public MiscService(NsoSession session)
        {
            _session = session;
        }

        public void SendViewInfo(int charId)
        {
            var msg = new NsoMessage(Cmd.SUB_COMMAND);
            msg.Writer.WriteSignedByte(SubCmd.Sub.VIEW_INFO);
            msg.Writer.WriteInt(charId);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Xin bang "Thong tin" nhan vat theo TEN - clone Service.viewInfo (cmd 93 + UTF ten).
        /// Server tra ve cmd 93 (bang chinh) roi cmd 101 (tinh tu / banh). Truyen ten CHINH MINH
        /// de xem thong tin ban than (MODGAME HpMpSync dung dung duong nay, nhip 3s van duoc
        /// server tra loi deu - xem docs/reference/SERVER_FACTS.md muc 16).
        /// </summary>
        public void SendViewInfoByName(string charName)
        {
            if (string.IsNullOrEmpty(charName)) return;
            var msg = new NsoMessage(Cmd.CHAR_VIEW_INFO);
            msg.Writer.WriteUTF(charName);
            // BYTE 0 O CUOI - 2026-09-08. TRUOC DAY THIEU.
            // SERVER_FACTS.md muc 16 ghi wire la `[93][UTF ten]`, chep tu NinjaSchool_251_src. Nhung
            // CA BA client chay THAT deu gui them 1 byte 0:
            //   MODGAME  Service.java:1425-1426   writeUTF(cName); writeByte(0);
            //   ZangVPS  ad_0.java:3992-3993      writeUTF(name);  writeByte(0);
            //   NSOCHIP  cn.java:1775-1777        writeUTF(string); writeByte(0);
            // 3 nguon doc lap thang 1 nguon tai lieu. Truoc doi nay khong ai goi ham nay tu vong auto
            // (chi giao dien goi) nen khong the biet server co tra loi hay khong - log 2026-09-07 co
            // DUNG 0 goi cmd 93. Neu server van im sau khi them byte nay thi xem lai muc 16.
            msg.Writer.WriteByte(0);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Xin server ảnh rời của 1 sprite (clone <c>Service.requestIcon</c>). Dùng cho icon vật
        /// phẩm mà 5 atlas trong EXE không có — xem docs/features/ICON_HANH_TRANG.md §4.
        ///
        /// ⛔ **HIỆN KHÔNG AI GỌI — CỐ Ý.** Đã thử trên server private này ngày 2026-09-07 với cả id
        /// trong bảng (2644) lẫn ngoài bảng (3197, 3420): **server không trả lời cái nào**, kho
        /// `Data/icons/` rỗng, không gói lạ nào chứa PNG. Wire thì đúng (đối chiếu
        /// `NinjaSchool_251_src/Service.cs:175` + `Controller.cs:3499`) ⇒ server đã bỏ tính năng.
        /// Giữ hàm lại để nếu server bật lại thì chỉ việc gọi. Đừng nối lại vào vòng vẽ Hành trang
        /// nếu chưa có bằng chứng server trả lời — xem docs/features/ICON_HANH_TRANG.md §4.5.
        ///
        /// ⚠ CÓ sinh gói tin. Người gọi phải qua <c>IconCache.ShouldRequest</c> ben NSOLITEPRO để
        /// mỗi id chỉ xin một lần và các lần xin đủ giãn cách.
        /// </summary>
        public void SendRequestIcon(int spriteId)
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.REQUEST_ICON);
            msg.Writer.WriteInt(spriteId);
            _session.QueueMessage(msg);
        }

        /// <summary>
        /// Xin server ảnh của 1 loại quái (clone <c>Service.requestModTemplate</c>).
        /// Wire: <c>cmd -28 · sub -108 · byte templateId</c> — <b>byte</b>, không phải short
        /// (SERVER_FACTS §19.2: <c>writeShort</c> của bản 251 không có tác dụng trên server này).
        ///
        /// ⚠ Server CHỈ trả lời khi <c>setClientType</c> đã khai <c>graphicsMode != 0</c>
        /// (§19.1 — đo thật: 510 gói với graphicsMode=0 → 0 trả lời; đổi sang 1 → 254/255 con về
        /// trong 40 giây). Đường khai cờ đó là <c>GfxDump.Arm</c> ben NSOLITEPRO, và nó gắn với
        /// ĐÚNG MỘT account để không đụng cả dàn.
        ///
        /// ⚠ CÓ sinh gói tin. Nhịp đã dùng thật khi dump 254 con là 150 ms/gói, không sự cố.
        /// </summary>
        public void SendRequestMobTemplate(int templateId)
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.MOB_TEMPLATE);
            msg.Writer.WriteByte((byte)templateId);
            _session.QueueMessage(msg);
        }

        public void SendAdminCommand(string text)
        {
            var msg = new NsoMessage(Cmd.NOT_MAP);
            msg.Writer.WriteSignedByte(SubCmd.NotMap.ADMIN_CMD);
            msg.Writer.WriteUTF(text);
            _session.QueueMessage(msg);
        }
    }
}
