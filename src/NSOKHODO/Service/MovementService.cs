using System;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Service
{
    public class MovementService
    {
        private readonly NsoSession _session;

        public MovementService(NsoSession session)
        {
            _session = session;
        }

        // Toa do cua goi move GAN NHAT da gui (clone Service.xx/yy cua client goc 2.5.1).
        // Nhieu thread trong CUNG mot account co the goi (luong auto + timer KeepAlive) -> co khoa.
        private readonly object _lastSentLock = new object();
        private short _lastSentX, _lastSentY;
        private bool _hasLastSent;

        /// <summary>
        /// Gui goi di chuyen, BO QUA neu trung y het toa do lan gui truoc.
        ///
        /// Clone <c>Service.charMove</c> cua client goc 2.5.1 (NinjaSchool_251_src/Service.cs:383-401):
        /// <c>if (xSend - xx != 0 || ySend - yy != 0)</c> moi gui. Ta THIEU chot nay, va no la mot
        /// nua con bao goi: <c>Navigator.CharBurstMove</c> ket thuc bang HAI goi trung y het nhau
        /// (:545 va :558); voi nhip duoi quai 20 lan/giay thi rieng cho do da la ~40 goi/giay/account
        /// - khop con so 31-47 goi/giay do duoc trong log 2026-09-04.
        ///
        /// KHONG dung ham nay cho goi CO CHU DICH phai trung toa do (KeepAlive, confirm vi tri truoc
        /// khi xin doi map / mo menu NPC) - dung <see cref="SendMoveForce"/>.
        /// Xem docs/features/DI_CHUYEN.md muc 2.3 + E.4.
        /// </summary>
        /// <summary>
        /// Dong bo "toa do da gui gan nhat" voi vi tri SERVER vua dat - clone <c>cxSend = cx</c>
        /// cua ban goc (NSOTRUNGDUC Class_by.java:420-425 o MAP_INFO: <c>ec = j</c>, <c>ed = k</c>;
        /// client goc 2.5.1 Controller.cs:735-742 lam y het o cmd 52).
        ///
        /// KHONG CO ham nay thi chot chong trung o duoi so voi mot BIEN CHET: server keo nhan vat
        /// ve cho khac -> bot muon di lai dung cho cu -> goi bi NUOT vi "trung lan truoc" -> nhan
        /// vat KHONG BAO GIO di duoc nua, trong khi Cx/Cy van tu gan ve dich. Do la mot nua cua su
        /// co "dung im, danh trat (Ne)" 2026-09-05.
        /// </summary>
        public void SyncLastSent(short x, short y)
        {
            lock (_lastSentLock)
            {
                _lastSentX = x;
                _lastSentY = y;
                _hasLastSent = true;
            }
        }

        /// <summary>
        /// Chup toa do goi move gan nhat DA GUI (= <c>dc/dd</c> cua ZangVPS `ax.java:10143-10144`).
        /// Sau moi cmd 52 / MAP_INFO / hoi sinh thi <see cref="SyncLastSent"/> dat lai bo nay bang
        /// dung vi tri SERVER vua ap, nen no la ban ghi tot nhat ta co ve "server dang nghi ta o dau".
        /// Navigator dung no de doi soat truoc khi burst - xem CharBurstMove.
        /// </summary>
        public bool TryGetLastSent(out short x, out short y)
        {
            lock (_lastSentLock)
            {
                x = _lastSentX; y = _lastSentY;
                return _hasLastSent;
            }
        }

        public void SendMove(short x, short y)
        {
            lock (_lastSentLock)
            {
                if (_hasLastSent && x == _lastSentX && y == _lastSentY) return;
            }
            SendMoveForce(x, y);
        }

        /// <summary>
        /// Gui goi di chuyen BO QUA chot chong trung. Danh cho cac cho CO CHU DICH gui lai dung
        /// toa do cu: KeepAlive (bao con song bang chinh vi tri dang dung - bi bop la acc bi dis),
        /// va cac lan "confirm vi tri voi server" truoc khi xin doi map hoac mo menu NPC.
        /// </summary>
        public void SendMoveForce(short x, short y)
        {
            var msg = new NsoMessage(Cmd.PLAYER_MOVE);
            msg.Writer.WriteShort(x);
            msg.Writer.WriteShort(y);
            // Xep goi TRONG khoa (2026-09-13): thu tu goi tren hang doi PHAI trung thu tu cap nhat
            // _lastSent, de ResendLastIfIdle (luong timer) khong chen mot toa do cu vao giua mot cu
            // burst cua luong auto. QueueMessage chi Enqueue + Set, khong chan.
            lock (_lastSentLock)
            {
                _lastSentX = x;
                _lastSentY = y;
                _hasLastSent = true;
                _lastSendTick = Environment.TickCount;
                _session.QueueMessage(msg);
            }
        }

        // Moc lan gui goi move gan nhat (Environment.TickCount) - cho ResendLastIfIdle.
        // SyncLastSent (server dat vi tri) KHONG cham vao: do la server noi, khong phai ta gui.
        private int _lastSendTick = Environment.TickCount;

        /// <summary>
        /// "Han che 900s" (<c>TrainConfig.HanCheBan</c>) - clone Auto30 `Class_cz.java:311-313`:
        /// khong gui goi move nao trong <paramref name="idleMs"/> thi gui lai DUNG toa do da gui gan nhat.
        /// Kiem "im" + xep goi trong CUNG khoa voi <see cref="SendMoveForce"/>. Tra true neu da gui.
        /// </summary>
        public bool ResendLastIfIdle(int idleMs)
        {
            lock (_lastSentLock)
            {
                if (!_hasLastSent) return false;
                if (unchecked(Environment.TickCount - _lastSendTick) < idleMs) return false;
                var msg = new NsoMessage(Cmd.PLAYER_MOVE);
                msg.Writer.WriteShort(_lastSentX);
                msg.Writer.WriteShort(_lastSentY);
                _lastSendTick = Environment.TickCount;
                _session.QueueMessage(msg);
                return true;
            }
        }

        public void SendRequestChangeMap()
        {
            var msg = new NsoMessage(Cmd.REQUEST_CHANGE_MAP);
            _session.QueueMessage(msg);
        }

        public void SendChangeZone(byte zoneId, byte indexUI)
        {
            var msg = new NsoMessage(Cmd.CHANGE_ZONE);
            msg.Writer.WriteByte(zoneId);
            msg.Writer.WriteByte(indexUI);
            _session.QueueMessage(msg);
        }

        public void SendOpenZoneList()
        {
            var msg = new NsoMessage(Cmd.OPEN_ZONE_LIST);
            _session.QueueMessage(msg);
        }

        public void SendReturnTown()
        {
            var msg = new NsoMessage(Cmd.RETURN_TOWN);
            _session.QueueMessage(msg);
        }

        public void SendWakeUp()
        {
            var msg = new NsoMessage(Cmd.WAKE_UP);
            _session.QueueMessage(msg);
        }
    }
}
