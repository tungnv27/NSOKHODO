using System;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;

namespace NSOKHODO.Controller
{
    public class PlayerHandler
    {
        public GameStateManager State { get; private set; }

        public PlayerHandler(GameStateManager state)
        {
            State = state;
        }

        public void HandlePlayerMove(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int charId = r.ReadInt();
                short x = r.ReadShort();
                short y = r.ReadShort();

                if (charId == State.MyChar.CharId)
                {
                    // CHAN DOAN 2026-09-03 (chi ghi log, khong doi hanh vi): tim xem THU GI ghi de
                    // toa do nhan vat giua hai tick. CharBurstMove luon dat Cx/Cy = dich, nen moi
                    // phep do "con cach > 20/40 px" phai dat o tick sau; thuc te khong dat, lap hang
                    // tram lan (ket "Mua thuc an", neo da qua lai, xin doi map bi tu choi).
                    // LUU Y: client goc 2.5.1 KHONG cap nhat vi tri BAN THAN tu cmd 1 - myChar la
                    // truong rieng (Char.cs:618), cmd 1 chi quet vCharInMap (Controller.cs case 1).
                    LogPosOverride("cmd1", State.MyChar.Cx, State.MyChar.Cy, x, y);
                    State.MyChar.Cx = x;
                    State.MyChar.Cy = y;
                }
                else
                {
                    foreach (var p in State.CurrentMap.OtherPlayers)
                    {
                        if (p.CharId == charId)
                        {
                            p.X = x;
                            p.Y = y;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        public void HandlePlayerLeave(NsoMessage msg)
        {
            try
            {
                int charId = msg.Reader.ReadInt();
                State.CurrentMap.OtherPlayers.RemoveAll(p => p.CharId == charId);
            }
            catch { }
        }

        public void HandlePlayerAdd(NsoMessage msg)
        {
            try
            {
                var player = MapHandler.ReadCharInfo(msg.Reader);
                if (player != null)
                {
                    // Remove existing entry if any
                    State.CurrentMap.OtherPlayers.RemoveAll(p => p.CharId == player.CharId);
                    State.CurrentMap.OtherPlayers.Add(player);
                }
            }
            catch { }
        }

        public void HandleServerSetPos(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                short nx = r.ReadShort();
                short ny = r.ReadShort();

                // ===== CONG `bU > 0` CUA ZangVPS (clone `ax.java:10118`) =====
                // Ban goc gac TOAN BO khoi dong bo vi tri (`ax.h()`) sau `if (aZ != 0 && bU > 0)`:
                // khi HP = 0 no KHONG snap, KHONG gui move, KHONG neo lai `dc/dd`. Ta mat cong do vi
                // NsoClient TU PHONG SONG (`IsDead = false; Hp = MaxHp`) ngay khi thay MAP_INFO sau
                // khi chet - tu do moi cong dua tren HP/IsDead deu vo hieu vinh vien.
                //
                // DO TREN LOG THAT (2026-09-07, 15 acc / 22 phut / 1.268 lan chet): co mot goi cmd52
                // ve MUON, chen giua dong `[Death] Died` va goi map moi, mang TOA DO CUA MAP CU
                // (vd barbigz104 19:29:47: `(660,192) -> (468,192)` la toa do map 41 trong khi nhan
                // vat dang duoc dua ve map 72). Doi chieu ca 1.268 cai chet:
                //     cai chet CO cmd52 muon : 134 ca, dong bang vinh vien 7  = 5,2%
                //     cai chet KHONG co      : 1.174 ca, dong bang        2  = 0,2%
                // gap 26 lan, va 7/9 ca dong bang nam o nhom dau. Acc dong bang dung chet cung tai
                // diem hoi sinh cho toi khi server cat ket noi (do duoc: dung 16 phut 41 giay).
                //
                // => Trong cua so [cmd -11 ... MAP_INFO], BO QUA hoan toan goi nay: khong ghi MyChar,
                //    khong neo moc server, khong dong bo `_lastSent`. Goi map ve ngay sau do se dat
                //    lai tat ca cho dung (MapHandler.cs:127) - dung nhu Zang dua vao goi map -18.
                if (State.MyChar.IsDead)
                {
                    LogDeadPosIgnored(nx, ny);
                    return;
                }

                LogPosOverride("cmd52", State.MyChar.Cx, State.MyChar.Cy, nx, ny);
                State.MyChar.Cx = nx;
                State.MyChar.Cy = ny;
                State.RaiseServerSetPosition(nx, ny);   // cxSend = cx (xem MovementService.SyncLastSent)
            }
            catch { }
        }

        // ---- Chan doan: cmd52 bi bo qua vi den trong cua so dang chet (xem HandleServerSetPos) ----
        // Dem KHONG phanh (de biet dung so lan), log CO phanh 5 giay giong LogPosOverride: ca 150 acc
        // cung ghi thi mot ca benh ly se nhan chim file log.
        private int _deadPosIgnored;
        private DateTime _lastDeadPosLogAt = DateTime.MinValue;

        // ===== 2026-09-08 dd12: GIU LAI DAU VET CUA CAC cmd52 BI VUT (chi ghi log) =====
        // Truoc day dong nay chi in toa do CUOI + tong so, lai bi phanh 5 giay - nen ca mot dong
        // thong tin cua server bi vut khong con vet. Do dem 2026-09-08: barbigz104 vut **159** goi,
        // barbigz103 **95**, barbigz110 **67**. Server dang noi gi do suot luc ta chet ma ta khong
        // giu lai gi de doc.
        // Nay giu them: toa do DAU tien, va so toa do KHAC NHAU. Hai so do phan biet duoc hai tinh
        // huong hoan toan khac nhau ma truoc day nhin y het:
        //   - so toa do khac nhau = 1  -> server GHIM ta o dung mot cho (dau hieu cua cai xac)
        //   - so toa do khac nhau lon -> server dang day ta di lung tung (chuyen khac han)
        private short _deadPosFirstX, _deadPosFirstY;
        private short _deadPosLastX, _deadPosLastY;
        private int _deadPosDistinct;
        private bool _deadPosHasFirst;

        private void LogDeadPosIgnored(short nx, short ny)
        {
            _deadPosIgnored++;
            if (!_deadPosHasFirst)
            {
                _deadPosHasFirst = true;
                _deadPosFirstX = nx; _deadPosFirstY = ny;
                _deadPosDistinct = 1;
            }
            else if (nx != _deadPosLastX || ny != _deadPosLastY)
            {
                _deadPosDistinct++;
            }
            _deadPosLastX = nx; _deadPosLastY = ny;

            if ((DateTime.UtcNow - _lastDeadPosLogAt).TotalMilliseconds < POS_OVERRIDE_LOG_GAP_MS) return;
            _lastDeadPosLogAt = DateTime.UtcNow;
            State.RaiseDebugLog(string.Format(
                "[Pos] cmd52 ve TRONG LUC DANG CHET -> bo qua; tong {0} lan, {1} toa do khac nhau "
                + "(dau ({2},{3}) -> nay ({4},{5})). Cho goi map dat lai.",
                _deadPosIgnored, _deadPosDistinct,
                _deadPosFirstX, _deadPosFirstY, nx, ny));
        }

        // ---- Chan doan: server dinh vi lai nhan vat (ghi log, toi da 1 lan/5 giay moi account) ----
        private DateTime _lastPosOverrideLogAt = DateTime.MinValue;
        private const int POS_OVERRIDE_LOG_GAP_MS = 5000;
        private const int POS_OVERRIDE_MIN_DELTA = 24;   // lech duoi nguong nay coi nhu nhieu, khong ghi

        private void LogPosOverride(string src, short oldX, short oldY, short newX, short newY)
        {
            int dx = oldX > newX ? oldX - newX : newX - oldX;
            int dy = oldY > newY ? oldY - newY : newY - oldY;
            if (dx < POS_OVERRIDE_MIN_DELTA && dy < POS_OVERRIDE_MIN_DELTA) return;
            if ((DateTime.UtcNow - _lastPosOverrideLogAt).TotalMilliseconds < POS_OVERRIDE_LOG_GAP_MS) return;
            _lastPosOverrideLogAt = DateTime.UtcNow;
            State.RaiseDebugLog(string.Format("[Pos] {0}: server doi cho ({1},{2}) -> ({3},{4}) lech {5},{6}",
                src, oldX, oldY, newX, newY, dx, dy));
        }

        /// <summary>
        /// cmd=-10: Revive from death (liveFromDead)
        /// cmd=-10 (WAKE_UP): Server confirm revive - empty packet
        /// From Controller.java line 467-476:
        ///   if wdx/wdy set → cx=wdx, cy=wdy, clear wdx/wdy
        ///   liveFromDead()
        /// </summary>
        public void HandleWakeUp(NsoMessage msg)
        {
            var c = State.MyChar;
            // CHAN DOAN 2026-09-07: day la MOT TRONG HAI goi DUY NHAT ma client goc coi la
            // "da song lai" (case -10 va 88 -> liveFromDead). Truoc day khong ai ghi lai luc no ve,
            // nen khong the phan biet "server da hoi sinh that" voi "ta tu phong song tu MAP_INFO"
            // (NsoClient.OnMapInfoReceived). Xem docs/features/DONG_BANG_VI_TRI.md muc D.2/F2.
            State.RaiseDebugLog(string.Format("[Revive] <- cmd -10 WAKE_UP (dang o {0},{1}; wd={2},{3})",
                c.Cx, c.Cy, State.DeathX, State.DeathY));

            // ===== CHOT CHAN BIA SO (2026-09-08 dd14) =====
            // `cmd -10` den khi ta KHONG chet la LOI DAP cho mot `cmd -9` ta gui luc con song - do
            // duoc 171/173 lan. Ban goc 2.5.1 goi `liveFromDead()` vo dieu kien (Controller.cs:3244)
            // vi no KHONG BAO GIO gui `-9` luc con song (menu chet moi co nut do). Ta thi co - va tu
            // dot nay con gui chu dich khi bi ghim. Neu van chay nguyen ban goc thi moi lan gui se tu
            // dat `Hp = MaxHp`, tuc CHE RA dung cai "HP ao" da ton ca tuan de truy.
            // Nhan vat khong he hoi sinh o day: no chua bao gio chet.
            if (!c.IsDead)
            {
                State.RaiseDebugLog(string.Format(
                    "[Revive] cmd -10 den luc DANG SONG (dap lai -9 ta gui) -> BO QUA, khong dat "
                    + "Hp = MaxHp. Bot van giu {0}/{1}.", c.Hp, c.MaxHp));
                return;
            }

            if (State.DeathX != 0 || State.DeathY != 0)
            {
                c.Cx = State.DeathX;
                c.Cy = State.DeathY;
                State.DeathX = 0;
                State.DeathY = 0;
            }
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            c.IsDead = false;
            State.RaiseServerSetPosition(c.Cx, c.Cy);
        }

        /// cmd=88 (PLAYER_REVIVE): Revive with position from server
        /// From source Controller.java line 467-476:
        ///   if wdx/wdy set → cx=wdx, cy=wdy, clear wdx/wdy
        ///   liveFromDead(): HP=MaxHP, MP=MaxMP, statusMe=1
        /// </summary>
        public void HandleRevive(NsoMessage msg)
        {
            var c = State.MyChar;
            // CHAN DOAN 2026-09-07: goi thu HAI (va cuoi) ma client goc coi la da song lai.
            // Xem chu thich o HandleWakeUp + docs/features/DONG_BANG_VI_TRI.md muc D.2/F2.
            State.RaiseDebugLog(string.Format("[Revive] <- cmd 88 REVIVE (dang o {0},{1}; wd={2},{3})",
                c.Cx, c.Cy, State.DeathX, State.DeathY));

            // CHOT CHAN BIA SO - y het HandleWakeUp o tren. Hai cua vao cung mot phong: truoc dd15
            // chi khoa `cmd -10`, con `cmd 88` van vao tu do. Server nay chua tung gui `88` lan nao
            // (do: 0/1638 goi hoi sinh deu la `-10`) nen day la bit truoc, khong phai chua chay.
            if (!c.IsDead)
            {
                State.RaiseDebugLog(string.Format(
                    "[Revive] cmd 88 den luc DANG SONG -> BO QUA, khong dat Hp = MaxHp. Bot van giu {0}/{1}.",
                    c.Hp, c.MaxHp));
                return;
            }

            // Set position to respawn point (from cmd=-11 death packet)
            if (State.DeathX != 0 || State.DeathY != 0)
            {
                c.Cx = State.DeathX;
                c.Cy = State.DeathY;
                State.DeathX = 0;
                State.DeathY = 0;
            }
            // liveFromDead()
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            c.IsDead = false;
            State.RaiseServerSetPosition(c.Cx, c.Cy);
        }
    }
}
