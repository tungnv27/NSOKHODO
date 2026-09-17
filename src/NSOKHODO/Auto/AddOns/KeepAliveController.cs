using System;
using System.Threading;
using NSOKHODO.Client;

namespace NSOKHODO.Auto
{
    public class KeepAliveController
    {
        private readonly NsoClient _client;
        private Timer _timer;
        private const int INTERVAL = 60000; // 60 seconds

        // ===== E1: chong bi coi la AFK (hoc ZangVPS `dj_0.java:2136-2160`) =====
        // Zang co HAI co che tach biet, ta truoc day chi co cai thu nhat:
        //   1. Keep-alive thuan: opcode -103 moi 60s (`dj_0.java:1198-1201`) - giu ket noi.
        //   2. "Auto Nhay Tranh Ma": neu TOA DO khong doi trong `cF.gg` (mac dinh 60000ms,
        //      `cF.java:2567`) VA nhan vat khong chet, gui 4 goi di lai roi ve cho cu.
        //      Muc dich khac han: chung minh voi server la con NGUOI DANG CHOI, khong phai
        //      mot phien treo may dung im.
        // Gui lai DUNG toa do cu (viec ta van lam) thoa man (1) nhung khong thoa man (2): voi
        // server, vi tri khong he thay doi.
        //
        // ===== 2026-09-07: QUAY VE DUNG BAN ZANG (truc Y), va chua cho "mu" =====
        // Ban truoc nhich TRUC X 24px, voi ly do ghi lai la: "ta headless, bat mot toa do Y khong co
        // nen la tu tao ra dung cai loi dang phai chua". Ly do do KHONG con dung, vi hai le:
        //   1. Cung dot nay Navigator co cong doi soat 80px (clone `ax.h()`). Ca bon buoc cua Zang
        //      deu <= 52px, tuc LUON duoi nguong - khong buoc nao sinh ra "cu nhay" bi server bac.
        //   2. Cung nhay cua Zang co tong do dich chuyen bang 0: -10, -52, -40, roi VE dung cho cu.
        //      Va ta gui thang qua MovementService, KHONG ghi gi vao MyChar.Cx/Cy, nen khong co con
        //      so bia nao song sot sau do.
        // Zang: `dj_0.java:2136-2160` - `cf-10`, roi -42 (=cf-52), roi +12 (=cf-40), roi +40 (=cf).
        //
        // ===== VA DAY LA LOI THU HAI: chong-AFK KHONG bat duoc acc dong bang =====
        // Chong-AFK so MyChar.Cx/Cy. Do la bien DUNG cho muc dich cua no (acc lanh thi MyChar bam
        // theo dung noi ta vua di), NHUNG voi acc dong bang thi MyChar nhay lien tuc
        // (nhich -> burst -> cmd52 keo ve) nen no KHONG BAO GIO thay "dung im" => barbigz106 nam im
        // 22 phut ma khong co gi danh thuc (log 2026-09-07).
        // => Them mot tin hieu THU HAI, doc lap: server keo ve DUNG MOT diem >= FROZEN_REJECT_COUNT
        //    lan (GameStateManager.TryGetConfirmedPos). Hai tin hieu bat hai lop loi khac nhau,
        //    KHONG duoc gop lam mot - xem khoi ghi chu trong Tick().
        private static readonly int[] AFK_JUMP_DY = { -10, -52, -40, 0 };
        private const int AFK_NUDGE_GAP_MS = 250;   // Zang: 250 + rand(50)
        // So lan server keo ve DUNG MOT diem thi coi la dong bang (khong phai chi la mot cu chinh
        // le te). Acc lanh gan nhu khong bao gio dat 3; barbigz106 dat 78.
        private const int FROZEN_REJECT_COUNT = 3;
        // Dong bang lien tuc bao lau thi thoi cuu tai cho, cat ket noi vao lai. Xem khoi ghi chu o
        // nhanh leo thang trong Tick(). Watchdog chay 60s/lan nen gia tri thuc te se lam tron len
        // boi cua 60: 120 => no o lan kiem tra thu hai. (di4 user chot 2026-09-15: 180 -> 120)
        private const int FROZEN_RELOGIN_SEC = 120;
        // ...VA server phai dang tu choi voi mat do cua mot acc ket that (xem cho dung no).
        private const int FROZEN_RELOGIN_REJECTS = 50;

        private short _lastX, _lastY;
        private bool _hasLastPos;
        // Seed tron them hash cua instance: 150 acc khoi tao trong cung mili giay se cung seed neu
        // chi lay TickCount, va do jitter chong-AFK se giong het nhau o ca dan.
        private readonly Random _rnd;

        public KeepAliveController(NsoClient client)
        {
            _client = client;
            _rnd = new Random(Environment.TickCount ^ (GetHashCode() * 397));
        }

        public void Start()
        {
            Stop();
            _hasLastPos = false;
            _timer = new Timer(Tick, null, INTERVAL, INTERVAL);
            _kietSucTu = DateTime.MinValue;
            _kietSucBaoLuc = DateTime.MinValue;
            _hpTimer = new Timer(TickHp, null, KS_TICK_MS, KS_TICK_MS);
            _hcViewInfoAt = DateTime.MinValue;
            _hanCheTimer = new Timer(TickHanChe, null, HC_TICK_MS, HC_TICK_MS);
        }

        /// <summary>Hai phep do HP dung chung MOT timer: 150 acc ma them luong thu ba chi de lam
        /// mot viec 3 giay/lan la khong dang.</summary>
        private void TickHp(object unused)
        {
            TickKietSuc(null);
            TickSongGia();
            TickImLang();
            TickDocIm();
        }

        // ==================================================================================
        // ===== "DOC IM": SERVER IM QUA LAU -> HOI cmd 93 DE GIU LUONG DOC (2026-09-15 di1) =====
        // ==================================================================================
        // Do tren log barbigz350 2026-09-15 (acc "Chi dung im" canh Tinh Anh, map 55): 26/30 lan dut
        // la `Unable to read data ... did not properly respond` DUNG 120-121 giay sau goi vao khu cuoi
        // = `ReceiveTimeout` 120s cua NsoConnection. Nhan vat dung yen, khong ai danh -> server KHONG
        // gui gi. Hai goi giu ket noi dang co deu MOT CHIEU: cmd 1 (Tick 60s) va tu danh cmd 61
        // (Heartbeat 60s) khong co tin tra loi -> chinh luong doc cua TA nem loi, bot relogin roi an
        // "chi co the vao lai game sau 27 giay".
        // Chua: im >= 40s thi hoi cmd 93 ten chinh minh - server tra loi 944/944 (SERVER_FACTS §28.4).
        // Chi gui khi IM nen tan sat (goi ve lien tuc) khong ton them goi nao. HE NEN: moi mode,
        // khong keo nhan vat di dau.
        private const int DI_NGUONG_MS = 40000;   // 1/3 han doc 120s: con 3 lan hoi truoc khi dut
        private const int DI_LAP_MS = 20000;
        private const int DI_BAO_MS = 300000;     // log 5 phut/lan - 150 acc dung im la 150 dong/40s
        private DateTime _diHoiLuc = DateTime.MinValue;
        private DateTime _diBaoLuc = DateTime.MinValue;
        private int _diTongLan;

        private void TickDocIm()
        {
            try
            {
                if (_client.State != ClientState.InGame) return;

                // BO HAN DOC khi da vao game - MOI mode (user chot 2026-09-15 di3: bot chi duoc tu cat ket
                // noi khi tan sat DANH QUAI ma khong phan hoi; "server im lau" khong phai dieu kien do).
                // Ket noi moi luon mo voi 120s (pha dang nhap giu nguyen) nen vao lai game la nhip 3s ke
                // tiep tu bo. Tan sat mang chet ma van danh quai -> SongGia bat. GIA: mang chet im (proxy
                // mat duong ra nhung van giu TCP) luc KHONG danh quai -> acc nam im, chi loi GUI moi bat duoc.
                if (_client.DatHanDoc(0))
                    _client.Log("[DocIm] Vao game -> bo han doc: server im bao lau bot cung KHONG tu cat ket noi (di3)");

                var gs = _client.GameState;
                var c = gs != null ? gs.MyChar : null;
                if (c == null || string.IsNullOrEmpty(c.Name) || gs.LastRecvAt == default(DateTime)) return;

                DateTime now = DateTime.UtcNow;
                int imMs = (int)(now - gs.LastRecvAt).TotalMilliseconds;
                if (imMs < DI_NGUONG_MS) return;
                if ((now - _diHoiLuc).TotalMilliseconds < DI_LAP_MS) return;

                var misc = _client.Misc;
                if (misc == null) return;
                _diHoiLuc = now;
                misc.SendViewInfoByName(c.Name);
                _diTongLan++;

                if ((now - _diBaoLuc).TotalMilliseconds >= DI_BAO_MS)
                {
                    _diBaoLuc = now;
                    _client.Log(string.Format(
                        "[DocIm] Server im {0}s -> hoi cmd 93 giu ket noi. Da hoi {1} lan tu dau phien.",
                        imMs / 1000, _diTongLan));
                }
            }
            catch { }
        }

        // ==================================================================================
        // ===== "SONG GIA": DANH MA KHONG AI DAP LAI (2026-09-08 dd16) =====================
        // ==================================================================================
        // Dau hieu that su cua cai xac, tim ra tu ca barbigz103 07:41: no KHONG im lang, no van
        // danh 2 lan/giay - chi la server ngung dap lai. Xem khoi ghi chu o
        // GameStateManager.LastAttackAckAt de biet so lieu.
        //
        // Do duoc: server dong bang luc 07:38:58, bi cat luc 07:41:23 => co **145 giay** de bat.
        // Nguong 20 giay la thua som, ma van tren muc mot acc lanh co the roi vao (8/8 acc khoe
        // deu co tin dap lai lien tuc trong cung khoang do).
        //
        // Doan sai thi mat dung mot goi: `cmd -9` gui luc con song bi server tu choi - do 606/606
        // lan - va chot chan bia so o PlayerHandler giu HP khong bi bom khong.
        private const int SG_NGUONG_MS = 20000;
        /// <summary>Don danh phai con "tuoi" moi ket luan: neu ta khong danh gi ca thi day khong
        /// phai phep do nay (do la viec cua TickImLang).</summary>
        private const int SG_DANH_TUOI_MS = 5000;

        private void TickSongGia()
        {
            try
            {
                var gs = _client.GameState;
                var c = gs != null ? gs.MyChar : null;
                if (_client.State != ClientState.InGame || c == null || c.IsDead || gs.IsChangingMap) return;
                // Dang chay mot dang CO QUYEN dung im (Thua loi dai / Cho PK / Buff / Danh Vong, hoac
                // o "Chi dung im"): don danh duy nhat cua no la nhip TU DANH 60 giay/lan, ma tu danh
                // thi KHONG BAO GIO co tin dap lai (server chi bao mau con quai bi danh). Tuc phep do
                // nay se ket luan "xac" ngay sau moi nhip tu danh. Xem NsoClient.DungImHopLe.
                //
                // LUAT (user chot 2026-09-15 di3): bot CHI duoc tu cat ket noi khi DANG TAN SAT va DANH
                // QUAI ma server khong phan hoi. Cho PK / Danh PK / Thua loi dai / Buff / Danh Vong /
                // "Chi dung im" / PK Am chen giua tan sat... KHONG ap dung. Day la duong tu cat DUY NHAT.
                if (!_client.DangTanSat) return;
                if (gs.LastMobAttackAt == default(DateTime)) return;

                DateTime now = DateTime.UtcNow;

                // Phai DANG danh QUAI. Don danh NGUOI khong bao gio co tin dap (server chi bao mau quai),
                // nen do tren LastAttackAt la Danh PK / PK Am bi ket luan "xac" sau 20s (loi truoc di3).
                if ((now - gs.LastMobAttackAt).TotalMilliseconds > SG_DANH_TUOI_MS) return;

                // Chua tung co tin dap lai nao ke tu khi vao game -> chua du can cu.
                if (gs.LastAttackAckAt == default(DateTime)) return;

                int cachMs = (int)(now - gs.LastAttackAckAt).TotalMilliseconds;
                if (cachMs < SG_NGUONG_MS) return;

                _client.VeLangViSongGia(cachMs / 1000);
            }
            catch { }
        }

        // ==================================================================================
        // ===== IM LANG BA DUONG O MAP TRAIN (2026-09-08 dd15) =============================
        // ==================================================================================
        // Bat CA HAI ca ma moi chot khac deu mu:
        //   1. CHET CAM  - server khong gui `cmd -11`, khong gui HP = 0, khong gui sat thuong.
        //      barbigz115 nam 75 giay nhu vay trong khi 14 acc cung map chet 2-8 lan MOI acc.
        //   2. SONG DAY AO - server gui `cmd -10`, ta tuyen bo song roi chay auto, nhung van la xac.
        //      Hoi sinh THAT thi trong 10 giay da co don danh/goi map (do 766 ca); hoi sinh ao thi
        //      khong bao gio co gi -> dong ho chay tiep va no o giay thu 5.
        //
        // ⚠️ PHAI DANG O MAP TRAIN moi tinh gio. Lang, map di duong, map nhiem vu deu co the dung im
        // hop le rat lau. Chot nay cat sach nhom false positive lon nhat ma khong can nguong cao.
        /// <summary>Im ca ba duong bao lau thi ket luan. 5 giay an toan vi o map train, mot nhan vat
        /// SONG khong the vua khong danh, vua khong bi danh, vua khong doi map suot 5 giay lien.</summary>
        private const int IM_LANG_NGUONG_MS = 5000;

        private void TickImLang()
        {
            try
            {
                var gs = _client.GameState;
                var c = gs != null ? gs.MyChar : null;
                if (_client.State != ClientState.InGame || c == null || c.IsDead || gs.IsChangingMap)
                {
                    return;
                }

                // ⚠️ Dang chay mot dang CO QUYEN dung im (Thua loi dai / Cho PK / Buff / Danh Vong,
                // hoac o "Chi dung im" cua Kich Yen) -> im ca ba duong la BINH THUONG, khong phai
                // dau hieu cai xac. Do that 2026-09-08: acc "Thua loi dai" dung o map 72 (trung map
                // dich o cau hinh) an 8 phat `cmd -9` trong 94 giay. Xem NsoClient.DungImHopLe.
                if (_client.DungImHopLe) return;

                // Chi tinh khi DANG O MAP TRAIN (map dich cua cau hinh).
                var map = gs.CurrentMap;
                int mapDich = _client.Config != null ? _client.Config.TargetMapId : -1;
                if (map == null || mapDich < 0 || map.MapId != mapDich)
                {
                    return;
                }

                // Moc hoat dong = cai MOI NHAT trong ba duong.
                DateTime hoatDong = gs.LastDamageAt;
                if (gs.LastAttackAt > hoatDong) hoatDong = gs.LastAttackAt;
                if (gs.LastWorldAt > hoatDong) hoatDong = gs.LastWorldAt;
                if (hoatDong == default(DateTime))
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                if ((now - hoatDong).TotalMilliseconds < IM_LANG_NGUONG_MS)
                {
                    return;
                }

                _client.VeLangViImLang((int)(now - hoatDong).TotalSeconds, map.MapId);
            }
            catch { }
        }

        // ==================================================================================
        // ===== DO "CHET MA KHONG DUOC BAO" (2026-09-08 dd11) ==============================
        // ==================================================================================
        // Ca can bat: server giu HP = 0 nhung KHONG gui `cmd -11`, nen `OnDeath` khong chay, vong
        // gui lai `-9` khong khoi dong, va bot cu gui lenh vao mot cai xac cho toi khi bi cat.
        //
        // ⚠️ VI SAO DAT O DAY MA KHONG O TrainMode.Tick (da thu, that bai 2026-09-08):
        // `Tick` bi CHAN toi ~115 giay ben trong `DoGmNavigation` (vong `maxWpRetry = 15`,
        // Navigator.cs:391-419) dung luc nhan vat ket. Ket qua do duoc: trong 77 giay barbigz114
        // dung im, bo do dat trong Tick KHONG no lan nao; con luc no no duoc thi Tick dang chay ngon
        // = nhan vat DANG SONG. Tuc im dung luc can, keu dung luc khong nen.
        // Controller nay chay tren Timer rieng nen khong the bi mot vong lap nao chan.
        //
        // ⚠️ CHOT SONG CON - `ServerAliveAt`: server nay CHI noi HP khi no bang 0 (do phien
        // 02:21-02:25: bao 0 **193 lan**, bao > 0 dung **15 lan** va ca 15 deu luc DANG NHAP). Nen
        // sau moi lan hoi sinh, `ServerHp` nam lai o 0 vinh vien. Doc tho `ServerHp <= 0` la bao dong
        // tren CA acc dang danh khoe - da do: 14/14 acc deu no. Vi vay goi bao 0 chi duoc tinh khi no
        // MOI HON moc `ServerAliveAt` (luc server xac nhan song bang cmd -10/88 hoac HP > 0).
        private Timer _hpTimer;
        private DateTime _kietSucTu;       // luc dau hieu bat dau, do CHINH TA giu
        private DateTime _kietSucBaoLuc;
        private const int KS_TICK_MS = 3000;
        /// <summary>
        /// Dau hieu phai giu lien tuc bao lau moi ket luan. Chet binh thuong duoc cuu trong **2 giay**
        /// (do: 63/66 ca o phien 02:21-02:25), va luc do `State == Dead` nen nhanh nay da bo qua san.
        /// 15 giay la thua xa khoang do, ma van sam hon nhieu so voi 100 giay barbigz114 dung truoc
        /// khi bi cat.
        /// </summary>
        private const int KS_NGUONG_MS = 15000;
        private const int KS_LAP_MS = 15000;   // con dau hieu thi nhac lai moi 15s

        private void TickKietSuc(object unused)
        {
            try
            {
                var c = _client.State == ClientState.InGame ? _client.GameState.MyChar : null;
                if (c == null || c.IsDead || c.ServerHpAt == default(DateTime) || c.ServerHp > 0)
                {
                    _kietSucTu = DateTime.MinValue;
                    return;
                }

                // Goi bao 0 da bi server LAT bang mot xac nhan song moi hon -> khong phai dau hieu.
                if (c.ServerAliveAt != default(DateTime) && c.ServerAliveAt >= c.ServerHpAt)
                {
                    _kietSucTu = DateTime.MinValue;
                    return;
                }

                // Chot chong parse lech (bai hoc dd5): maxHp la dai luong ON DINH. Khong khop thi ta
                // dang doc lech truong, so `hp` di kem vo nghia - tuyet doi khong ket luan tu no.
                if (c.ServerHpMaxSeen <= 0 || c.MaxHp <= 0 || c.ServerHpMaxSeen != c.MaxHp)
                {
                    _kietSucTu = DateTime.MinValue;
                    return;
                }

                DateTime now = DateTime.UtcNow;
                if (_kietSucTu == DateTime.MinValue)
                {
                    _kietSucTu = now;
                    _kietSucBaoLuc = DateTime.MinValue;
                    return;
                }
                int giu = (int)(now - _kietSucTu).TotalSeconds;
                if ((now - _kietSucTu).TotalMilliseconds < KS_NGUONG_MS) return;
                if (_kietSucBaoLuc != DateTime.MinValue
                    && (now - _kietSucBaoLuc).TotalMilliseconds < KS_LAP_MS) return;
                _kietSucBaoLuc = now;

                _client.Log(string.Format(
                    "[KietSuc] CHET MA KHONG DUOC BAO: server giu HP = 0/{0} lien tuc {1}s, "
                    + "goi bao luc do MOI HON moi xac nhan song gan nhat, va KHONG co cmd -11 nao. "
                    + "Bot dang hien {2}/{0}. Vao luong chet: gui lenh ve lang (cmd -9).",
                    c.MaxHp, giu, c.Hp));

                _client.NoteExhausted(c.ServerHp, c.MaxHp, c.Hp, giu);
                _kietSucTu = DateTime.MinValue;   // da ban giao cho luong chet, dem lai tu dau
            }
            catch { }
        }

        private void Tick(object unused)
        {
            if (_client.State != ClientState.InGame || _client.GameState.IsChangingMap) return;

            var c = _client.GameState.MyChar;
            short x = c.Cx, y = c.Cy;

            // NSOKHODO - SUA CO CHU DICH: dang GIAO DICH thi KHONG nhich chong-AFK (cu nhay len xuong
            // toi 52 px). Chua biet server chinh xu ly di chuyen giua phien the nao; phien dai nhat vai
            // phut nen chi gui goi giu ket noi dung cho cu. Xem docs/NGUON_GOC.md.
            if (_client.DangGiaoDich)
            {
                _hasLastPos = false;   // het phien moi bat dau dem "dung im" lai tu dau
                _client.Movement.SendMoveForce(x, y);
                return;
            }

            // ===== TIN HIEU 1: CHONG-AFK (dung ban Zang) =====
            // Zang so `ax.a().y/cf` - vi tri CUC BO that su cua no. Ban cuc bo cua ta la MyChar:
            // voi acc LANH, MyChar bam theo dung noi ta vua di. Khong doi suot mot nhip 60s = dung
            // im that => can chung to con nguoi dang choi.
            bool afkDungIm = _hasLastPos && x == _lastX && y == _lastY;
            _lastX = x; _lastY = y; _hasLastPos = true;

            // ===== TIN HIEU 2: DONG BANG (khac han tin hieu 1) =====
            // ⚠️ KHONG dung "vi tri server xac nhan khong doi" lam tin hieu: server CHI len tieng khi
            // no TU CHOI; im lang = dong y. Acc dang farm ngon lanh ca phut trong cung mot map cung
            // khong co cmd52/MAP_INFO nao => y het acc dong bang. Tin hieu dung la su LAP LAI:
            // server keo ta ve DUNG MOT diem nhieu lan lien tiep (barbigz106: 78 lan ve (1620,672);
            // acc lanh gan nhu bang 0).
            short sx, sy; DateTime movedAt; int repeat;
            bool dongBang = _client.GameState.TryGetConfirmedPos(out sx, out sy, out movedAt, out repeat)
                            && repeat >= FROZEN_REJECT_COUNT
                            && (DateTime.UtcNow - movedAt).TotalMilliseconds >= INTERVAL;

            if (dongBang)
            {
                int giay = (int)(DateTime.UtcNow - movedAt).TotalSeconds;
                _client.Log(string.Format("[Watchdog] DONG BANG: server keo ve ({0},{1}) {2} lan, {3}s khong nhuc nhich",
                    sx, sy, repeat, giay));

                // In "server gui gi" MOT lan moi dot (dot = cung moc movedAt) - xem InVetDongBang.
                if (movedAt != _dongBangDaInVet)
                {
                    _dongBangDaInVet = movedAt;
                    InVetDongBang("lan dau phat hien", sx, sy, repeat, giay);
                }

                // ===== LEO THANG: het FROZEN_RELOGIN_SEC ma van ket -> vao lai =====
                // Cu "nhay tai cho" ben duoi cuu duoc 0/8 acc tren log 2026-09-07 (barbigz104: 15 lan
                // nhich trong 16 phut, khong thoat). Giu no cho cua so dau (dong bang ngan co the chi
                // la mot cu chinh nhat thoi), nhung qua nguong thi phai cat ket noi - xem chu thich
                // day o NsoClient.ForceRelogin.
                // Nguong 120s (di4; truoc la 180s) CHU DICH thap hon nhieu so voi ~1.000s la luc SERVER tu cat: chu dong
                // vao lai thi khong dinh loi hen "chi co the vao lai game sau 16-21 giay nua" (do
                // duoc o barbigz102/104), va cuu lai ~14 phut chet may moi lan no.
                // Doi HAI dieu kien chu khong chi thoi gian: mot acc DUNG YEN CO CHU DICH (che do Cho
                // PK / Buff) cung co the tich du `repeat` qua nhieu nhip 60s ma khong he bi ket - cat
                // ket noi cua no la lam hong viec dang chay. Acc ket THAT bi tu choi voi mat do khac
                // han: do tren log 2026-09-07, sau 180s barbigz104 da co 364 lan, barbigz102 946 lan,
                // trong khi mot acc chi bi choi le te thi nam o hang don vi.
                if (giay >= FROZEN_RELOGIN_SEC && repeat >= FROZEN_RELOGIN_REJECTS)
                {
                    // di4 (user chot 2026-09-15): KHOI PHUC tu vao lai - NHUNG CHI khi DANG TAN SAT
                    // (NsoClient.DangTanSat). Ly do: cu nhich tai cho cuu duoc 0/8 acc (log 2026-09-07), relogin
                    // cuu barbigz104 trong 2s; acc dong bang khong toi duoc quai nen SongGia khong bat duoc.
                    // Mode cho (Cho PK / Danh PK / Thua loi dai / Buff / Danh Vong / "Chi dung im") giu luat
                    // di3: KHONG tu vao lai, chi log + nhich.
                    if (_client.DangTanSat)
                    {
                        _client.Log(string.Format(
                            "[Watchdog] Dong bang {0}s tai ({1},{2}), server keo ve {3} lan -> VAO LAI (di4)",
                            giay, sx, sy, repeat));
                        // In vet TRUOC khi ngat: sau ForceRelogin doi tuong ket noi bi vut, mat sach vet goi.
                        InVetDongBang("truoc khi vao lai", sx, sy, repeat, giay);
                        _client.ForceRelogin(string.Format(
                            "dong bang {0}s tai ({1},{2}) - nhay tai cho khong go duoc, vao lai", giay, sx, sy));
                        return;
                    }
                    _client.Log("[Watchdog] Dong bang qua nguong nhung KHONG o tan sat -> KHONG tu vao lai (di3), chi nhich tai cho");
                }

                // Nhay tu chinh MOC SERVER, khong phai tu MyChar: xuat phat tu con so bia thi lai
                // dung vao cai loi dang chua.
                if (Nudge(sx, sy, false)) return;   // da gui goi that -> khoi gui keepalive trung toa do
            }
            if (afkDungIm)
            {
                if (Nudge(x, y, true)) return;
            }

            // Keep-alive thuan.
            // PHAI dung SendMoveForce: goi nay CO CHU DICH trung toa do (bao con song bang chinh
            // cho dang dung). Neu di qua chot chong trung cua SendMove thi nhan vat dung yen se
            // khong con goi keepalive nao ra -> server dong ket noi.
            _client.Movement.SendMoveForce(x, y);
        }

        /// <summary>Moc <c>movedAt</c> cua dot dong bang da in vet "lan dau" - moi dot chi in MOT lan.</summary>
        private DateTime _dongBangDaInVet = DateTime.MinValue;

        /// <summary>
        /// In "SERVER GUI GI" luc dong bang (user yeu cau 2026-09-15 di4) - de lan sau tim ra goi gay ket.
        /// Header goi nhan gan nhat lay tu bo dem vet cua NsoConnection (cung nguon voi [SongGia] VET GOI);
        /// chi tiet tung lan server keo ve da co san o dong `[Pos] cmd52: server doi cho ...`.
        /// </summary>
        private void InVetDongBang(string luc, short sx, short sy, int repeat, int giay)
        {
            try
            {
                var gs = _client.GameState;
                var c = gs != null ? gs.MyChar : null;
                var map = gs != null ? gs.CurrentMap : null;
                _client.Log(string.Format(
                    "[Watchdog] VET DONG BANG ({0}): map={1} khu={2} | server giu ({3},{4}) {5} lan / {6}s | "
                    + "bot hien ({7},{8}) HP {9}/{10} chet={11} | GOI NHAN GAN NHAT (cu -> moi, cmd/len, * = goi lon -32): {12}",
                    luc, map != null ? map.MapId : -1, map != null ? (int)map.ZoneId : -1,
                    sx, sy, repeat, giay,
                    c != null ? c.Cx : -1, c != null ? c.Cy : -1, c != null ? c.Hp : -1, c != null ? c.MaxHp : -1,
                    c != null && c.IsDead, _client.VetGoiGanNhat()));
            }
            catch { }
        }

        /// <summary>
        /// Cung nhay tai cho theo truc Y - clone ZangVPS `dj_0.java:2136-2160`:
        /// <c>cf-10</c> -> <c>cf-52</c> -> <c>cf-40</c> -> <c>cf</c>, cach nhau 250 + rand(50) ms.
        /// Tong do dich chuyen bang 0 (ket thuc dung cho xuat phat) va buoc lon nhat 52px, duoi
        /// nguong doi soat 80px cua Navigator - nen khong sinh ra vi tri server chua tung chap nhan.
        /// Tra false neu chua gui duoc goi nao (de nguoi goi rot ve keep-alive thuong).
        ///
        /// Dung SendMoveForce cho CA BON buoc: ban goc gui that ca bon, va buoc cuoi ve dung toa do
        /// xuat phat - qua SendMove thi chot chong trung se nuot mat no.
        /// </summary>
        private bool Nudge(short x, short y, bool theoMyChar)
        {
            try
            {
                bool daGui = false;
                for (int i = 0; i < AFK_JUMP_DY.Length; i++)
                {
                    if (_client.State != ClientState.InGame) return daGui;
                    // NSOKHODO: mode vua di chuyen giua nhip (~800 ms) -> dung han, KHONG keo server ve cho cu (review
                    // 17/09: de bi "qua xa" / di mai khong toi). Vua vao giao dich -> bo cac buoc con lai, ve cho cu ngay.
                    if (DaDoiViTri(x, y, theoMyChar)) return daGui;
                    if (_client.DangGiaoDich) break;
                    int ny = y + AFK_JUMP_DY[i];
                    if (ny < 0) ny = 0;
                    _client.Movement.SendMoveForce(x, (short)ny);
                    daGui = true;
                    if (i < AFK_JUMP_DY.Length - 1) Thread.Sleep(AFK_NUDGE_GAP_MS + _rnd.Next(50));
                }
                if (!daGui || _client.State != ClientState.InGame) return daGui;
                if (DaDoiViTri(x, y, theoMyChar)) return true;   // mode da tu gui vi tri moi (3 goi cua CharBurstMove)
                // NSOKHODO - SUA 2026-09-17 (user: "nhan vat bi bay", Y=216 ma nguoi khac thay 164): buoc cuoi
                // MOT goi thi server chinh KHONG ghi nhan - ~1,2 s sau no phat lai cho nguoi khac diem cao nhat
                // (y-52) va giu luon. Do bang acc thu hai (tools/kiemtra NguoiChoi "theo"/"nhay"): 4 buoc + 1 goi
                // -> ket 164 (lap lai 4/4); them 3 goi ve cho cu kieu CharBurstMove (goi 1-2 cach 20 ms)
                // -> dung 216 (5/5, ke ca khi dang ket san). Gui lai 216 MOT goi sau khi da ket: van ket.
                Thread.Sleep(50);
                _client.Movement.SendMoveForce(x, y);
                Thread.Sleep(20);
                _client.Movement.SendMoveForce(x, y);
                _client.Movement.SendMoveForce(x, y);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// NSOKHODO: (khi nhay theo MyChar) MyChar da doi so voi luc bat dau nhip = mode vua tu di chuyen.
        /// Nhip theo moc server (dong bang) khong so MyChar - MyChar va moc server khac nhau la binh thuong.
        /// </summary>
        private bool DaDoiViTri(short x, short y, bool theoMyChar)
        {
            if (!theoMyChar) return false;
            var c = _client.GameState.MyChar;
            if (c == null) return false;
            if (c.Cx != x) return true;
            // Server co the doi MyChar ve mot buoc cua chinh nhip nay (cmd 1 / 52) - khong phai mode di chuyen.
            foreach (var dy in AFK_JUMP_DY)
                if (c.Cy == Math.Max(0, y + dy)) return false;
            return true;
        }

        // ==================================================================================
        // ===== "HAN CHE 900s" (TrainConfig.HanCheBan) - GIU LUU LUONG HAI CHIEU, clone Auto30 =====
        // ==================================================================================
        // Do tren log 2026-09-11: 132/183 lan dut khi dang choi qua proxy la read-timeout, phan lon ngay
        // sau luc dung im - trong khi keep-alive thuan cua ta 60 s/lan va MOT CHIEU (server khong tra loi
        // cmd 1). Bot Java Auto30 giu duong truyen day ca hai chieu bang hai viec, ta clone nguyen:
        //   (4) `Class_cz.java:311-313` - khong di chuyen > 1 s thi gui lai vi tri.
        //   (5) `Class_cv.java:151-153` - moi 15 s gui cmd 93 (xem thong tin) VOI TEN CHINH MINH -> server
        //       tra goi 93 ve (MiscHandler.HandleCharViewInfo lam moi bang "Thong tin" cua acc).
        // Chay o MOI mode vi khong keo nhan vat di dau (luat HE NEN). O dang tat -> khong gui gi.
        private Timer _hanCheTimer;
        private const int HC_TICK_MS = 250;          // do min cho moc 1 s; 150 acc x 4 lan/giay la re
        private const int HC_IDLE_MS = 1000;         // Auto30: `Class_br.c() - this.ai > 1000L`
        private const int HC_VIEW_INFO_MS = 15000;   // Auto30: `Class_br.c() - var1.q > 15000L`
        private DateTime _hcViewInfoAt = DateTime.MinValue;

        private void TickHanChe(object unused)
        {
            try
            {
                var t = _client.Config != null ? _client.Config.Train : null;
                if (t == null || !t.HanCheBan) return;
                if (_client.State != ClientState.InGame) return;
                var gs = _client.GameState;
                var c = gs != null ? gs.MyChar : null;
                if (c == null || c.IsDead || gs.IsChangingMap) return;

                // (4) Gui lai TOA DO DA GUI GAN NHAT, KHONG phai MyChar.Cx/Cy: MyChar la con so lac quan
                // (CharBurstMove gan = dich ngay khi gui) - gui no giua mot cu di co the keo nhan vat lui.
                var mv = _client.Movement;
                if (mv != null) mv.ResendLastIfIdle(HC_IDLE_MS);

                // (5)
                DateTime now = DateTime.UtcNow;
                if ((now - _hcViewInfoAt).TotalMilliseconds >= HC_VIEW_INFO_MS && !string.IsNullOrEmpty(c.Name))
                {
                    _hcViewInfoAt = now;
                    var misc = _client.Misc;
                    if (misc != null) misc.SendViewInfoByName(c.Name);
                }
            }
            catch { }
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            if (_hpTimer != null)
            {
                _hpTimer.Dispose();
                _hpTimer = null;
            }
            if (_hanCheTimer != null)
            {
                _hanCheTimer.Dispose();
                _hanCheTimer = null;
            }
        }
    }
}
