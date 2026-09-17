using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NSOKHODO.Core
{
    public class NsoSession : IDisposable
    {
        private NsoConnection _connection;
        private Thread _receiverThread;
        private Thread _senderThread;
        private readonly ConcurrentQueue<NsoMessage> _sendQueue = new ConcurrentQueue<NsoMessage>();
        private readonly AutoResetEvent _sendSignal = new AutoResetEvent(false);
        private readonly System.Collections.Generic.List<NsoMessage> _sendBatch =
            new System.Collections.Generic.List<NsoMessage>(64);
        private volatile bool _running;

        public string Host { get; private set; }
        public int Port { get; private set; }
        public byte ServerLogin { get; set; }

        public event Action<NsoMessage> OnMessageReceived;
        public event Action OnDisconnected;
        public event Action<Exception> OnError;
        /// <summary>Dong log tu tang session (hien chi dung cho canh bao lu goi).</summary>
        public event Action<string> OnLog;

        // ---- Dong ho do goi gui ra (chong lu goi) ----
        // Dem message thuc su gui moi giay. Vuot nguong -> ghi canh bao, toi da 1 lan/10 giay.
        // CO Y khong tu chan goi: chan giua chung co the pha logic di chuyen (duoi mob hop le van
        // co the burst 20 goi/giay). Muc dich la de lan sau ket vong lap LO NGAY tu dong log dau
        // tien, thay vi phai mo pha phap y nhu su co 2026-09-03 (5837 goi "ve bai" trong 159 giay).
        private const int FLOOD_WARN_MSG_PER_SEC = 25;
        private int _msgSentInWindow;
        private DateTime _floodWindowStart = DateTime.UtcNow;
        private DateTime _lastFloodWarnAt = DateTime.MinValue;

        // ===== PHAN TICH LU GOI THEO OPCODE (2026-09-08 dd12) - CHI GHI LOG =====
        // Truoc day dong [Flood] chi cho biet TOC DO, khong cho biet THANH PHAN. Dem 2026-09-08 mat
        // 3 nick (barbigz110 dut o 116 goi/giay, barbigz103 o 142, barbigz104 o 33) va trong ca ba ca
        // ta chi doan la "goi di chuyen" chu khong co so nao chung minh.
        // Bang dem nay tra loi thang: 142 goi/giay do la cmd 1 (di), cmd 60 (danh), hay -17 (xin doi map).
        //
        // An toan luong: CA HAI cho dung mang nay (cong don trong SenderLoop va doc/xoa trong
        // CheckFloodMeter) deu chay tren DUNG MOT thread - SenderLoop goi CheckFloodMeter o cuoi moi
        // vong. Nen khong can khoa. Mang 256 phan tu = 1KB/session, khong dang ke.
        private readonly int[] _floodByCmd = new int[256];

        /// <summary>Ten cho vai opcode hay gap o CHIEU GUI - de doc log khong phai tra bang.</summary>
        private static string TenLenh(int cmd)
        {
            switch (cmd)
            {
                case 1:   return "di chuyen";
                case 4:   return "danh (PLAYER_ATTACK)";
                case 60:  return "danh quai";
                case 61:  return "danh nguoi";
                case 11:  return "dung do";
                case 28:  return "doi khu";
                case 41:  return "chon skill";
                case 74:  return "buff";
                case 93:  return "xem thong tin";
                case -17: return "xin doi map";
                case -14: return "nhat do";
                case -10: return "hoi sinh tai cho";
                case -9:  return "ve lang";
                case -30: return "sub";
                default:  return null;
            }
        }

        public bool IsConnected
        {
            get { return _connection != null && _connection.Connected && _running; }
        }

        public bool IsEncryptionReady
        {
            get { return _connection != null && _connection.Crypto.IsReady; }
        }

        /// <summary>
        /// Header cua cac goi nhan gan nhat (cu -&gt; moi), dang <c>cmd/len</c>. Chi dung de CHAN DOAN
        /// khi nghi luong doc da lech - xem <see cref="NsoConnection.MoTaGoiGanNhat"/>.
        /// </summary>
        public string MoTaGoiGanNhat()
        {
            var c = _connection;
            return c != null ? c.MoTaGoiGanNhat() : "(chua ket noi)";
        }

        /// <summary>Xem <see cref="NsoConnection.SetReadTimeout"/>.</summary>
        public bool SetReadTimeout(int ms)
        {
            var c = _connection;
            return c != null && c.SetReadTimeout(ms);
        }

        public void Connect(string host, int port, string proxy = null)
        {
            Host = host;
            Port = port;

            _connection = new NsoConnection();
            _connection.Connect(host, port, proxy);
            _running = true;

            // STACK 256 KB thay vi 1 MB mac dinh - BAT BUOC o quy mo cua tool nay.
            // Moi account giu 2 luong nay suot phien. 1.800 account = 3.600 luong; voi stack mac
            // dinh 1 MB do la 3,6 GB VUNG DIA CHI bi giu cho, con 256 KB thi chi ~900 MB.
            // (Bo nho THAT dung it hon nhieu - Windows chi cap phat trang khi cham toi - nhung
            //  vung dia chi bi giu cho thi van la mot tran cung.)
            // 256 KB la thua cho hai vong nay: chung chi doc/ghi socket va goi handler, khong de quy.
            const int STACK_BYTES = 256 * 1024;

            _receiverThread = new Thread(ReceiverLoop, STACK_BYTES)
            {
                IsBackground = true,
                Name = "NSO-Receiver"
            };
            _receiverThread.Start();

            _senderThread = new Thread(SenderLoop, STACK_BYTES)
            {
                IsBackground = true,
                Name = "NSO-Sender"
            };
            _senderThread.Start();
        }

        public void QueueMessage(NsoMessage msg)
        {
            _sendQueue.Enqueue(msg);
            try { _sendSignal.Set(); } catch { }
        }

        public void SendDirect(NsoMessage msg)
        {
            if (_connection != null && _connection.Connected)
                _connection.SendMessage(msg);
        }

        private void ReceiverLoop()
        {
            try
            {
                while (_running)
                {
                    NsoMessage msg = _connection.ReadMessage();
                    var handler = OnMessageReceived;
                    if (handler != null)
                        handler(msg);
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    _running = false;
                    var errHandler = OnError;
                    if (errHandler != null)
                        errHandler(ex);
                    var dcHandler = OnDisconnected;
                    if (dcHandler != null)
                        dcHandler();
                }
            }
        }

        /// <summary>
        /// Rut CA hang doi roi gui trong DUNG 1 lan Write (1 TCP segment neu vua MSS).
        /// Truoc day moi message la 1 lan SendMessage = 4 segment; mot lan burst move
        /// (~60 goi day vao hang doi trong duoi 1ms) tao ra ~240 segment.
        ///
        /// Cho bang AutoResetEvent thay vi Thread.Sleep(10) polling: 600 account x 100
        /// lan thuc/giay = 60.000 wakeup/giay vo ich. Van co timeout 10ms de vong lap
        /// thoat duoc khi _running = false va de gom them goi den sat nhau.
        /// </summary>
        private void SenderLoop()
        {
            try
            {
                while (_running)
                {
                    _sendSignal.WaitOne(10);

                    NsoMessage msg;
                    while (_sendQueue.TryDequeue(out msg))
                        _sendBatch.Add(msg);

                    if (_sendBatch.Count > 0)
                    {
                        try
                        {
                            if (_connection != null && _connection.Connected)
                            {
                                _connection.SendBatch(_sendBatch, _sendBatch.Count);
                                _msgSentInWindow += _sendBatch.Count;
                                // Dem theo opcode de dong [Flood] noi duoc CAI GI dang bom - xem _floodByCmd.
                                for (int i = 0; i < _sendBatch.Count; i++)
                                    _floodByCmd[_sendBatch[i].Command + 128]++;
                            }
                        }
                        finally
                        {
                            _sendBatch.Clear();
                        }
                    }

                    CheckFloodMeter();
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    _running = false;
                    var errHandler = OnError;
                    if (errHandler != null)
                        errHandler(ex);
                    var dcHandler = OnDisconnected;
                    if (dcHandler != null)
                        dcHandler();
                }
            }
        }

        /// <summary>
        /// Chot so goi da gui trong cua so 1 giay vua qua; vuot nguong thi canh bao (1 lan/10 giay).
        /// Goi tu SenderLoop nen khong ton them luong nao.
        /// </summary>
        /// <summary>
        /// Xep 4 opcode gui nhieu nhat trong giay vua roi thanh mot chuoi doc duoc.
        /// Chon lua chon O(n) tren mang 256 thay vi sort: ham nay chay MOI GIAY tren MOI session
        /// (co the 150 session cung luc), khong duoc phep cap phat lung tung.
        /// </summary>
        private string TomTatOpcode()
        {
            var sb = new System.Text.StringBuilder();
            int[] viTri = new int[4];
            int soLay = 0;

            for (int lan = 0; lan < 4; lan++)
            {
                int best = -1, bestVal = 0;
                for (int i = 0; i < _floodByCmd.Length; i++)
                {
                    if (_floodByCmd[i] <= bestVal) continue;
                    bool trung = false;
                    for (int k = 0; k < soLay; k++) if (viTri[k] == i) { trung = true; break; }
                    if (trung) continue;
                    best = i; bestVal = _floodByCmd[i];
                }
                if (best < 0) break;
                viTri[soLay++] = best;

                int cmd = best - 128;
                string ten = TenLenh(cmd);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("cmd ").Append(cmd);
                if (ten != null) sb.Append(" (").Append(ten).Append(')');
                sb.Append(" x").Append(bestVal);
            }
            return sb.Length > 0 ? sb.ToString() : "khong co goi nao";
        }

        private void CheckFloodMeter()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _floodWindowStart).TotalMilliseconds < 1000) return;

            int rate = _msgSentInWindow;
            _msgSentInWindow = 0;
            _floodWindowStart = now;

            // Chup roi XOA bang dem NGAY - phai lam ca khi duoi nguong, neu khong so lieu cua cac giay
            // truoc se don lai va dong log dau tien vuot nguong se bao cao sai thanh phan.
            string chiTiet = TomTatOpcode();
            Array.Clear(_floodByCmd, 0, _floodByCmd.Length);

            if (rate <= FLOOD_WARN_MSG_PER_SEC) return;
            if ((now - _lastFloodWarnAt).TotalSeconds < 10) return;
            _lastFloodWarnAt = now;

            var h = OnLog;
            if (h != null)
                h(string.Format("[Flood] Dang gui {0} goi/giay (nguong {1}) | {2}",
                    rate, FLOOD_WARN_MSG_PER_SEC, chiTiet));
        }

        public void Disconnect()
        {
            _running = false;
            try { _sendSignal.Set(); } catch { } // danh thuc sender de thoat ngay
            if (_connection != null)
            {
                _connection.Disconnect();
                _connection = null;
            }
            // Clear send queue
            NsoMessage dummy;
            while (_sendQueue.TryDequeue(out dummy)) { }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
