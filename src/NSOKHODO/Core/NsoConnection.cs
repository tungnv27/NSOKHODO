using System;
using System.IO;
using System.Net.Sockets;

namespace NSOKHODO.Core
{
    public class NsoConnection : IDisposable
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private readonly NsoEncryption _crypto = new NsoEncryption();
        private readonly object _sendLock = new object();

        // ===== VET GOI GAN NHAT (2026-09-10 ty4) - CHI GHI NHAN, KHONG DOI HANH VI =====
        // Bo dem XOR (`NsoEncryption._readPos`) nhich mot nac moi byte giai ma. Chi can MOT byte
        // doc ma khong giai ma (hoac nguoc lai) la khoa lech VINH VIEN - khong co co che nao dua no
        // ve dung. Tu do bot doc ra rac: cmd la, len khong lo, va khong bao gio tu khoi.
        // Do duoc 2026-09-10 (18 acc, 30 phut): DUNG 4 acc co goi rac, va DUNG 4 acc do mat lien lac
        // (barbigz112/114/102/117); 14 acc con lai khong acc nao. Xem docs/features/TRUC_Y.md.
        //
        // Van de: luc phat hien duoc thi goi GAY LECH da troi qua tu lau, khong con vet gi de doc -
        // nhung goi truoc no van "trong hop le" nen khong ai ghi lai. Vong dem nay giu header cua
        // RECENT_CAP goi gan nhat de khi bao dong thi in ra, tim duoc thu pham that o phien sau.
        // Hai nghi can dang treo, ca hai deu o ReadMessage: cmd -31 (SPECIAL_LEN - length KHONG giai
        // ma nhung payload thi CO) va cmd -27 (HANDSHAKE den giua phien luc IsReady da bat).
        private const int RECENT_CAP = 24;
        private readonly sbyte[] _recentCmd = new sbyte[RECENT_CAP];
        private readonly int[] _recentLen = new int[RECENT_CAP];
        private readonly bool[] _recentBig = new bool[RECENT_CAP];   // true = di duong cmd -32
        private readonly object _recentLock = new object();
        private int _recentIdx;      // vi tri ghi tiep theo
        private int _recentCount;    // so phan tu da ghi (toi da RECENT_CAP)

        private void GhiVetGoi(sbyte cmd, int len, bool big)
        {
            lock (_recentLock)
            {
                _recentCmd[_recentIdx] = cmd;
                _recentLen[_recentIdx] = len;
                _recentBig[_recentIdx] = big;
                _recentIdx = (_recentIdx + 1) % RECENT_CAP;
                if (_recentCount < RECENT_CAP) _recentCount++;
            }
        }

        /// <summary>
        /// Chuoi mo ta <see cref="RECENT_CAP"/> goi gan nhat theo thu tu CU -&gt; MOI, dang
        /// <c>cmd/len</c> (them <c>*</c> neu di duong goi lon -32). Dung khi bao dong lech luong.
        /// </summary>
        public string MoTaGoiGanNhat()
        {
            lock (_recentLock)
            {
                if (_recentCount == 0) return "(chua co goi nao)";
                var sb = new System.Text.StringBuilder();
                int start = (_recentIdx - _recentCount + RECENT_CAP) % RECENT_CAP;
                for (int i = 0; i < _recentCount; i++)
                {
                    int p = (start + i) % RECENT_CAP;
                    if (i > 0) sb.Append(' ');
                    sb.Append(_recentCmd[p]).Append('/').Append(_recentLen[p]);
                    if (_recentBig[p]) sb.Append('*');
                }
                return sb.ToString();
            }
        }

        public bool Connected
        {
            get { return _tcp != null && _tcp.Connected; }
        }

        public NsoEncryption Crypto { get { return _crypto; } }

        /// <summary>Han doc cua ket noi game dang choi (xem ghi chu trong <see cref="Connect"/>).</summary>
        public const int GAME_READ_TIMEOUT_MS = 120000;

        /// <summary>
        /// Doi han doc cua ket noi DANG CHOI. 0 = cho vo han. Tra true neu vua doi that.
        /// Goi tu KeepAliveController.TickDocIm: vao game la bo han doc o MOI mode (user chot 2026-09-15
        /// di3 - bot khong tu cat ket noi vi server im lau). KHONG bat loi het han roi doc tiep thay cho viec nay: sau khi recv
        /// het han, Windows coi socket o trang thai khong xac dinh (co the mat byte) - mat MOT byte la
        /// lech khoa XOR vinh vien.
        /// </summary>
        public bool SetReadTimeout(int ms)
        {
            var t = _tcp;
            if (t == null) return false;
            try
            {
                if (t.ReceiveTimeout == ms) return false;
                t.ReceiveTimeout = ms;
                return true;
            }
            catch { return false; }
        }

        public void Connect(string host, int port)
        {
            Connect(host, port, null);
        }

        public void Connect(string host, int port, string proxyString)
        {
            _crypto.Reset();
            _tcp = new TcpClient();
            _tcp.NoDelay = true;
            // BAT BIEN: KeepAlive (60s, KeepAliveController.INTERVAL) < ReceiveTimeout.
            // Truoc day ReceiveTimeout = 30s < 60s: account nao im lang qua 30 giay (khu vang quai,
            // dang cho respawn, cho doi khu) bi nem IOException -> tuong dut ket noi -> relogin oan.
            // Truoc ban va 2026-09-03 loi nay bi CHE KHUAT vi bot ban goi move lien tuc nen luong
            // nhan khong bao gio im; sua het ket vong lap thi account dung yen THAT SU im lang
            // -> khong nang cho nay len la loi se lo ra ngay.
            // 120s = chiu duoc 2 lan lo nhip KeepAlive. Dut ket noi that (peer dong) van phat hien
            // TUC THI qua read <= 0; han nay chi ap cho ca "duong mang chet im" (proxy bien mat).
            // ⚠️ DINH CHINH 2026-09-15 (di1): bat bien tren CHUA DU. KeepAlive cmd 1 va tu danh cmd 61
            // la MOT CHIEU - server khong tra loi - nen acc dung yen van dut dung 120s (log barbigz350:
            // 26/30 ca). Thu giu luong DOC song la KeepAliveController.TickDocIm (im >= 40s -> cmd 93);
            // Vao game roi thi TickDocIm bo han doc ve 0 o MOI mode (di3) - 120s chi con ap cho pha dang nhap.
            _tcp.ReceiveTimeout = GAME_READ_TIMEOUT_MS;
            _tcp.SendTimeout = 30000;

            if (!string.IsNullOrEmpty(proxyString))
            {
                // HAN GIO RIENG CHO PHA BAT TAY PROXY (2026-09-12).
                //
                // Truoc day pha bat tay dung luon ReceiveTimeout 120 giay o tren - ma 120 giay la
                // han cua KET NOI GAME dang choi (phai lon hon nhip KeepAlive 60s, xem ghi chu tren).
                // Hau qua: proxy nhan TCP xong roi im (qua tai / het slot / IP vua xoay) thi luong
                // login nam cho DU 2 PHUT, trong luc do van om slot cua ReloginGate + LoginGate
                // => chi 5 acc xui la ca tien trinh dung hinh, phai tat/bat lai tool moi chay.
                // Proxy song bat tay xong trong vai tram ms, nen 10 giay la rong rai.
                // Dat 0 o Data/settings.txt (ProxyHandshakeSec=0) de quay ve hanh vi cu.
                int hsMs = Config.NetOptions.ProxyHandshakeMs;
                if (hsMs > 0)
                {
                    _tcp.ReceiveTimeout = hsMs;
                    _tcp.SendTimeout = hsMs;
                }
                // Tien to scheme quyet dinh loai proxy. Khong co tien to = SOCKS5 (mac dinh,
                // tuong thich nguoc voi proxy cu da luu dang host:port:user:pass).
                string raw = proxyString.Trim();
                bool isHttp = false;
                int schemeIdx = raw.IndexOf("://", StringComparison.OrdinalIgnoreCase);
                if (schemeIdx > 0)
                {
                    string scheme = raw.Substring(0, schemeIdx).ToLowerInvariant();
                    isHttp = scheme == "http" || scheme == "https";
                    raw = raw.Substring(schemeIdx + 3);
                }

                try
                {
                    if (isHttp)
                        ConnectViaHttp(host, port, raw);
                    else
                        ConnectViaSocks5(host, port, raw);
                }
                finally
                {
                    // Duong ham da thong (hoac da hong) -> tra lai han cua KET NOI GAME. Quen buoc
                    // nay thi moi acc qua proxy se bi nem IOException sau 10 giay im lang trong game.
                    try { _tcp.ReceiveTimeout = GAME_READ_TIMEOUT_MS; _tcp.SendTimeout = 30000; } catch { }
                }
            }
            else
            {
                ConnectWithTimeout(host, port);
            }

            _stream = _tcp.GetStream();
        }

        /// <summary>
        /// Mo ket noi TCP co HAN GIO. TcpClient.Connect tran KHONG co timeout: proxy chet/lo lung
        /// thi SYN treo theo mac dinh cua he dieu hanh (~21 giay). 15 account chung mot proxy hong
        /// = 15 luong login treo cung luc.
        /// </summary>
        private void ConnectWithTimeout(string host, int port, int timeoutMs = 10000)
        {
            var ar = _tcp.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs, false))
            {
                try { _tcp.Close(); } catch { }
                throw new TimeoutException(string.Format(
                    "Khong mo duoc ket noi toi {0}:{1} trong {2}s", host, port, timeoutMs / 1000));
            }
            _tcp.EndConnect(ar);
        }

        /// <summary>
        /// HTTP CONNECT proxy (tunnel). Format (da bo tien to http://): host:port:user:pass.
        /// Auth dung Basic (RFC 7617) neu co user/pass.
        /// </summary>
        private void ConnectViaHttp(string targetHost, int targetPort, string proxyString)
        {
            string[] parts = proxyString.Split(':');
            if (parts.Length < 2)
                throw new ArgumentException("Invalid proxy format. Use host:port:user:pass");

            string proxyHost = parts[0].Trim();
            int proxyPort = int.Parse(parts[1].Trim());
            string proxyUser = parts.Length > 2 ? parts[2].Trim() : null;
            string proxyPass = parts.Length > 3 ? parts[3].Trim() : null;
            bool hasAuth = !string.IsNullOrEmpty(proxyUser);

            ConnectWithTimeout(proxyHost, proxyPort);
            var stream = _tcp.GetStream();

            string hostPort = targetHost + ":" + targetPort;
            var sb = new System.Text.StringBuilder();
            sb.Append("CONNECT ").Append(hostPort).Append(" HTTP/1.1\r\n");
            sb.Append("Host: ").Append(hostPort).Append("\r\n");
            if (hasAuth)
            {
                byte[] cred = System.Text.Encoding.ASCII.GetBytes(proxyUser + ":" + proxyPass);
                sb.Append("Proxy-Authorization: Basic ").Append(Convert.ToBase64String(cred)).Append("\r\n");
            }
            sb.Append("Proxy-Connection: keep-alive\r\n");
            sb.Append("\r\n");

            byte[] req = System.Text.Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(req, 0, req.Length);

            // Doc status line + headers cho den \r\n\r\n (doc tung byte, khong nuot du lieu game).
            string statusLine = ReadHttpResponseHead(stream);
            // "HTTP/1.1 200 Connection established"
            string[] sp = statusLine.Split(' ');
            if (sp.Length < 2 || sp[1] != "200")
                throw new Exception("HTTP proxy CONNECT failed: " + statusLine);
        }

        /// <summary>Doc header HTTP tung byte cho den dau dong trong (\r\n\r\n). Tra ve dong status dau tien.</summary>
        private static string ReadHttpResponseHead(NetworkStream stream)
        {
            var all = new System.Text.StringBuilder();
            int state = 0; // dem chuoi \r\n\r\n
            while (true)
            {
                int b = stream.ReadByte();
                if (b < 0) throw new EndOfStreamException("HTTP proxy closed during handshake");
                all.Append((char)b);
                if (b == '\r') { if (state == 0 || state == 2) state++; else state = 1; }
                else if (b == '\n') { if (state == 1) state = 2; else if (state == 3) break; else state = 0; }
                else state = 0;
            }
            string head = all.ToString();
            int nl = head.IndexOf("\r\n", StringComparison.Ordinal);
            return nl > 0 ? head.Substring(0, nl) : head.Trim();
        }

        /// <summary>
        /// SOCKS5 proxy connection.
        /// Format: host:port:user:pass (e.g. 1.2.3.4:1080:user:pass)
        /// </summary>
        private void ConnectViaSocks5(string targetHost, int targetPort, string proxyString)
        {
            // Parse proxy: host:port:user:pass
            string[] parts = proxyString.Split(':');
            if (parts.Length < 2)
                throw new ArgumentException("Invalid proxy format. Use host:port:user:pass");

            string proxyHost = parts[0].Trim();
            int proxyPort = int.Parse(parts[1].Trim());
            string proxyUser = parts.Length > 2 ? parts[2].Trim() : null;
            string proxyPass = parts.Length > 3 ? parts[3].Trim() : null;
            bool hasAuth = !string.IsNullOrEmpty(proxyUser);

            // Connect to proxy
            ConnectWithTimeout(proxyHost, proxyPort);
            var stream = _tcp.GetStream();

            // SOCKS5 handshake
            if (hasAuth)
            {
                // Offer username/password auth (method 0x02)
                stream.Write(new byte[] { 0x05, 0x01, 0x02 }, 0, 3);
            }
            else
            {
                // No auth (method 0x00)
                stream.Write(new byte[] { 0x05, 0x01, 0x00 }, 0, 3);
            }

            // Read server response
            byte[] resp = new byte[2];
            ReadFull(stream, resp, 2);
            if (resp[0] != 0x05)
                throw new Exception("SOCKS5: Invalid version response");

            if (resp[1] == 0x02 && hasAuth)
            {
                // Username/password auth (RFC 1929)
                byte[] userBytes = System.Text.Encoding.ASCII.GetBytes(proxyUser);
                byte[] passBytes = System.Text.Encoding.ASCII.GetBytes(proxyPass);
                byte[] authReq = new byte[3 + userBytes.Length + passBytes.Length];
                authReq[0] = 0x01; // version
                authReq[1] = (byte)userBytes.Length;
                Array.Copy(userBytes, 0, authReq, 2, userBytes.Length);
                authReq[2 + userBytes.Length] = (byte)passBytes.Length;
                Array.Copy(passBytes, 0, authReq, 3 + userBytes.Length, passBytes.Length);
                stream.Write(authReq, 0, authReq.Length);

                byte[] authResp = new byte[2];
                ReadFull(stream, authResp, 2);
                if (authResp[1] != 0x00)
                    throw new Exception("SOCKS5: Auth failed");
            }
            else if (resp[1] == 0xFF)
            {
                throw new Exception("SOCKS5: No acceptable auth method");
            }

            // Connect request
            byte[] hostBytes = System.Text.Encoding.ASCII.GetBytes(targetHost);
            byte[] connectReq = new byte[7 + hostBytes.Length];
            connectReq[0] = 0x05; // version
            connectReq[1] = 0x01; // connect
            connectReq[2] = 0x00; // reserved
            connectReq[3] = 0x03; // domain name
            connectReq[4] = (byte)hostBytes.Length;
            Array.Copy(hostBytes, 0, connectReq, 5, hostBytes.Length);
            connectReq[5 + hostBytes.Length] = (byte)(targetPort >> 8);
            connectReq[6 + hostBytes.Length] = (byte)targetPort;
            stream.Write(connectReq, 0, connectReq.Length);

            // Read connect response
            byte[] connResp = new byte[4];
            ReadFull(stream, connResp, 4);
            if (connResp[1] != 0x00)
                throw new Exception("SOCKS5: Connect failed, code=" + connResp[1]);

            // Skip bound address
            if (connResp[3] == 0x01) // IPv4
            {
                byte[] skip = new byte[6]; // 4 IP + 2 port
                ReadFull(stream, skip, 6);
            }
            else if (connResp[3] == 0x03) // Domain
            {
                byte[] lenBuf = new byte[1];
                ReadFull(stream, lenBuf, 1);
                byte[] skip = new byte[lenBuf[0] + 2]; // domain + 2 port
                ReadFull(stream, skip, skip.Length);
            }
            else if (connResp[3] == 0x04) // IPv6
            {
                byte[] skip = new byte[18]; // 16 IP + 2 port
                ReadFull(stream, skip, 18);
            }

            // Connection established through proxy!
        }

        private static void ReadFull(NetworkStream stream, byte[] buf, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buf, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException("Proxy connection closed");
                offset += read;
            }
        }

        public NsoMessage ReadMessage()
        {
            // Doc ca header 3 byte trong 1 syscall (moi packet luon co it nhat cmd + 2 byte
            // length; goi lon -32 thi byte[1] la cmd that, byte[2] la byte dau cua length 4B).
            // Thu tu giai ma van tuan tu tung byte nen bo dem XOR khong lech.
            byte[] hdr = ReadStreamFully(3);

            byte cmdByte = hdr[0];
            if (_crypto.IsReady)
                cmdByte = _crypto.DecryptByte(cmdByte);

            sbyte cmd = (sbyte)cmdByte;

            // Handle cmd=-32 (large packet)
            if (cmd == -32)
            {
                byte realCmdByte = hdr[1];
                if (_crypto.IsReady)
                    realCmdByte = _crypto.DecryptByte(realCmdByte);
                cmd = (sbyte)realCmdByte;

                // 4-byte length, each byte decrypted. hdr[2] la byte dau tien.
                byte[] lenBuf = new byte[4];
                lenBuf[0] = hdr[2];
                byte[] rest = ReadStreamFully(3);
                lenBuf[1] = rest[0]; lenBuf[2] = rest[1]; lenBuf[3] = rest[2];
                if (_crypto.IsReady)
                {
                    for (int i = 0; i < 4; i++)
                        lenBuf[i] = _crypto.DecryptByte(lenBuf[i]);
                }
                int length = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];

                byte[] data = ReadStreamFully(length);
                if (_crypto.IsReady)
                {
                    for (int i = 0; i < data.Length; i++)
                        data[i] = _crypto.DecryptByte(data[i]);
                }

                GhiVetGoi(cmd, length, true);
                return new NsoMessage(cmd, data);
            }

            // 2-byte length (da nam san trong header)
            byte[] lenBytes = new byte[] { hdr[1], hdr[2] };

            if (_crypto.IsReady && cmd != -27 && cmd != -31)
            {
                lenBytes[0] = _crypto.DecryptByte(lenBytes[0]);
                lenBytes[1] = _crypto.DecryptByte(lenBytes[1]);
            }

            int dataLen = (lenBytes[0] << 8) | lenBytes[1];

            byte[] payload = null;
            if (dataLen > 0)
            {
                payload = ReadStreamFully(dataLen);
                if (_crypto.IsReady)
                {
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] = _crypto.DecryptByte(payload[i]);
                }
            }

            GhiVetGoi(cmd, dataLen, false);

            // Handle key exchange
            if (cmd == -27 && payload != null && payload.Length > 0)
            {
                int keyLen = payload[0] & 0xFF;
                byte[] rawKey = new byte[keyLen];
                Array.Copy(payload, 1, rawKey, 0, keyLen);
                _crypto.SetKey(rawKey);
                return new NsoMessage(cmd, payload);
            }

            return new NsoMessage(cmd, payload ?? new byte[0]);
        }

        public void SendMessage(NsoMessage msg)
        {
            byte[] data = msg.GetData();   // ToArray() copy mang -> chi goi DUNG 1 lan / message
            lock (_sendLock)
            {
                byte[] buf = new byte[3 + (data != null ? data.Length : 0)];
                int n = EncodeInto(msg.Command, data, buf, 0);
                _stream.Write(buf, 0, n);
            }
        }

        /// <summary>
        /// Gui NHIEU message trong DUNG 1 lan Write xuong socket.
        ///
        /// Vi sao can: NetworkStream khong co bo dem va socket bat NoDelay (Nagle tat) nen
        /// MOI lan Write la 1 TCP segment. Truoc day 1 message game = 4 lan ghi = 4 segment
        /// (7 byte du lieu -> 167 byte tren day). Mot lan CharBurstMove day ~60 goi move vao
        /// hang doi trong duoi 1ms; gop lai thi ca lo nam gon trong 1 segment (~460 byte).
        ///
        /// Bat buoc: NsoEncryption la XOR CO TRANG THAI (bo dem ghi tang dan theo tung byte)
        /// nen thu tu byte di qua bo ma hoa phai giu nguyen y het luc ghi tung goi:
        /// cmd -> lenHi -> lenLo -> data, message nay roi moi toi message ke.
        /// </summary>
        public void SendBatch(System.Collections.Generic.IList<NsoMessage> msgs, int count)
        {
            if (msgs == null || count <= 0) return;

            // Lay du lieu tung message DUNG 1 lan (GetData goi ToArray -> copy mang moi lan).
            byte[][] datas = new byte[count][];
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                datas[i] = msgs[i].GetData();
                total += 3 + (datas[i] != null ? datas[i].Length : 0);
            }

            lock (_sendLock)
            {
                byte[] buf = new byte[total];
                int off = 0;
                for (int i = 0; i < count; i++)
                    off += EncodeInto(msgs[i].Command, datas[i], buf, off);

                _stream.Write(buf, 0, off);
            }
        }

        /// <summary>
        /// Ma hoa 1 message vao <paramref name="buf"/> tai <paramref name="offset"/>.
        /// Tra ve so byte da ghi. PHAI goi trong _sendLock: bo dem XOR ghi la trang thai chay
        /// theo tung byte, sai thu tu la hong toan bo luong.
        /// </summary>
        private int EncodeInto(sbyte cmd, byte[] data, byte[] buf, int offset)
        {
            int len = data != null ? data.Length : 0;
            int p = offset;

            // Command byte
            byte cmdByte = (byte)cmd;
            if (_crypto.IsReady)
                cmdByte = _crypto.EncryptByte(cmdByte);
            buf[p++] = cmdByte;

            // 2-byte length. cmd=-31: length NOT encrypted
            byte lenHi = (byte)(len >> 8);
            byte lenLo = (byte)len;
            if (_crypto.IsReady && cmd != -31)
            {
                lenHi = _crypto.EncryptByte(lenHi);
                lenLo = _crypto.EncryptByte(lenLo);
            }
            buf[p++] = lenHi;
            buf[p++] = lenLo;

            // Data
            if (len > 0)
            {
                if (_crypto.IsReady)
                {
                    for (int i = 0; i < len; i++)
                        buf[p + i] = _crypto.EncryptByte(data[i]);
                }
                else
                {
                    Array.Copy(data, 0, buf, p, len);
                }
                p += len;
            }

            return p - offset;
        }

        private byte[] ReadStreamFully(int count)
        {
            byte[] buf = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = _stream.Read(buf, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException("Connection closed during read");
                offset += read;
            }
            return buf;
        }

        public void Disconnect()
        {
            try
            {
                if (_stream != null) _stream.Close();
                if (_tcp != null) _tcp.Close();
            }
            catch { }
            _stream = null;
            _tcp = null;
            _crypto.Reset();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
