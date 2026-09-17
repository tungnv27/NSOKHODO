using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    public class ChatHandler
    {
        public GameStateManager State { get; private set; }

        // Luu goi party chat gan nhat de MessageRouter ban event (from,text) cho GroupPlayController.
        public string LastPartyFrom { get; private set; }
        public string LastPartyText { get; private set; }

        // Cung khuon cho CHAT RIENG: MessageRouter ban event (from,text) de NsoClient bat lenh goi
        // "ky ..." cua Kich Yen (xem docs/features/KICH_YEN.md muc C.4).
        public string LastPrivateFrom { get; private set; }
        public string LastPrivateText { get; private set; }

        /// <summary>
        /// Nội dung THÔ của gói vừa xử lý — KHÔNG có tiền tố <c>[Server]</c>/<c>[Info]</c>… mà
        /// <see cref="Handle"/> thêm vào cho khung log.
        ///
        /// Thanh thông báo của game in đúng chuỗi server gửi (<c>readUTF()</c>), không tiền tố.
        /// Dùng bản đã format sẽ ra <c>"[Info] Can not invite players…"</c> — đúng lỗi user báo
        /// 2026-09-09 kèm ảnh chụp.
        /// </summary>
        public string LastRaw { get; private set; }

        public ChatHandler(GameStateManager state)
        {
            State = state;
        }

        public string Handle(sbyte cmd, NsoMessage msg)
        {
            var r = msg.Reader;

            switch (cmd)
            {
                case Cmd.CHAT_PUBLIC:
                    {
                        int charId = r.ReadInt();
                        string text = r.ReadUTF();
                        return string.Format("[Public] {0}: {1}", charId, text);
                    }

                case Cmd.CHAT_PRIVATE:
                    {
                        string from = r.ReadUTF();
                        string text = r.ReadUTF();
                        LastPrivateFrom = from;
                        LastPrivateText = text;
                        return string.Format("[Private] {0}: {1}", from, text);
                    }

                case Cmd.CHAT_GLOBAL:
                    {
                        string name = r.ReadUTF();
                        string text = r.ReadUTF();
                        return string.Format("[Global] {0}: {1}", name, text);
                    }

                case Cmd.CHAT_PARTY:
                    {
                        string from = r.ReadUTF();
                        string text = r.ReadUTF();
                        LastPartyFrom = from;
                        LastPartyText = text;
                        return string.Format("[Party] {0}: {1}", from, text);
                    }

                case Cmd.CHAT_CLAN:
                    {
                        string from = r.ReadUTF();
                        string text = r.ReadUTF();
                        return string.Format("[Clan] {0}: {1}", from, text);
                    }

                case Cmd.SERVER_MSG:
                    {
                        string text = r.ReadUTF();
                        // NSOKHODO: giu cau tho - phien giao dich ghi kem ly do server tu choi (SPEC M18).
                        // Nhanh INFO_MSG cua router chi doc LastRaw khi cmd == -24, nen gan o day vo hai.
                        LastRaw = text;
                        return string.Format("[Server] {0}", text);
                    }

                case Cmd.SYSTEM_CHAT:
                    {
                        string text = r.ReadUTF();
                        return string.Format("[System] {0}", text);
                    }

                case Cmd.INFO_MSG:
                    {
                        string text = r.ReadUTF();
                        LastRaw = text;
                        return string.Format("[Info] {0}", text);
                    }

                default:
                    return "[Chat] unknown";
            }
        }
    }
}
