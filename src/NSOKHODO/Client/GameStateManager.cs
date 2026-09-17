using System;
using System.Collections.Generic;
using NSOKHODO.GameData;
using NSOKHODO.Models;

namespace NSOKHODO.Client
{
    /// <summary>
    /// Vi tri char chet luc dang TanSat - de bot quay lai cho cu sau revive.
    /// Snapshot tai thoi diem OnDeath, truoc khi MyChar.Cx/Cy bi HandleWakeUp ghi de.
    /// </summary>
    public class DeathSpot
    {
        public int MapId;
        public byte ZoneId;
        public short X;
        public short Y;
        public DateTime At;
    }

    public class GameStateManager
    {
        public CharacterState MyChar { get; private set; }
        public MapState CurrentMap { get; private set; }
        public List<PartyMember> PartyMembers { get; set; }
        public List<Effect> Effects { get; set; }
        public bool IsInGame { get; set; }
        public bool IsInParty { get; set; }
        public short DeathX { get; set; }
        public short DeathY { get; set; }
        public bool PartyLocked { get; set; }

        // ===== Danh sach khu (cmd 36) - xem SERVER_FACTS.md §22 =====
        // Server gui 2 BYTE moi khu: dan so + so nhom. Chi so mang CHINH LA zoneId.
        // Truoc 2026-09-06 parser doc 1 byte/khu -> mang dan xen + mat nua so khu; da sua.
        /// <summary>So NGUOI moi khu. null = chua co so lieu.</summary>
        /// <summary>
        /// Phat CONG THEM vao cong nhip danh (ms) khi server tra loi "qua nhanh" ngay sau mot don.
        /// Chi tang (buoc 50, tran 300), khong tu giam; ve 0 khi Reset() = doi tai khoan/dut ket noi.
        ///
        /// Vi sao o DAY chu khong o TrainMode: mode bi huy + tao MOI moi lan chet
        /// (`NsoClient.StopAutoSystems`/`StartAutoSystems`). De o mode thi acc chet nhieu se do lai
        /// tu dau mai mai - dung cai bay ma co "chu dich dap do" va `TanSatAnchor` da phai tranh.
        /// </summary>
        public int AttackPadPenaltyMs;

        public int[] ZonePlayerCounts { get; set; }
        /// <summary>So NHOM moi khu (cung do dai voi ZonePlayerCounts). Hien chua ai doc - giu de khoi phai parse lai.</summary>
        public int[] ZonePartyCounts { get; set; }
        /// <summary>Luc lay so lieu khu. So lieu qua han thi coi nhu khong co - xem ZoneCountsFresh.</summary>
        public DateTime ZoneCountsAtUtc { get; set; }
        /// <summary>Map luc lay so lieu. Moi map co bang khu rieng nen so lieu map khac la vo nghia.</summary>
        public int ZoneCountsMapId { get; set; }

        // ===== Khu dich do CUM DOI KHU chon (docs/features/DOI_KHU.md) =====
        // VI SAO PHAI CO: ban goc KHONG coi khu dich la hang so. AutoTanSat.java:165 goi
        // `this.a(super.b, super.c, ...)` MOI TICK, va moi lan nhay khu deu GHI DE `super.c`
        // (Auto.b: `this.c = var5`; Auto.c: `this.c = NSOT_MOB.r[...]`). Tuc "khu dich" la BIEN
        // CHAY, khong phai o cau hinh.
        // Bo mat dieu nay (2026-09-06 lan dau) => TrainMode Step 2 thay curZone != Config.TargetZoneId
        // nen KEO NGUOC ve khu cu, danh nhau voi cum doi khu: acc sang khu 10 roi bi tha ve khu 0
        // lien tuc (user bao loi voi anh chup: Khu=0, dai khu 10-15).
        // Song qua CHET (mode bi huy/tao lai moi lan hoi sinh) - cung ly do da chuyen PkAmActive
        // vao day; chi xoa khi Reset() (doi tai khoan / dut ket noi).
        /// <summary>Khu do cum doi khu chon. -1 = chua chon gi (dung o cau hinh).</summary>
        public volatile int HopZoneId = -1;
        /// <summary>Map ma <see cref="HopZoneId"/> thuoc ve - doi map dich thi lua chon cu het hieu luc.</summary>
        public volatile int HopZoneMapId = -1;

        /// <summary>Quen khu da chon (tat tinh nang, user sua o Khu, doi map dich...).</summary>
        public void ClearHopZone()
        {
            HopZoneId = -1;
            HopZoneMapId = -1;
        }

        /// <summary>Ghi nho khu vua nhay toi, de Step 2 khong keo nguoc ve khu cu.</summary>
        public void SetHopZone(int zoneId, int mapId)
        {
            HopZoneMapId = mapId;
            HopZoneId = zoneId;
        }

        /// <summary>So lieu khu con dung duoc khong: dung map + chua qua <paramref name="maxAgeMs"/>.</summary>
        public bool ZoneCountsFresh(int mapId, int maxAgeMs)
        {
            var z = ZonePlayerCounts;
            if (z == null || z.Length == 0) return false;
            if (ZoneCountsMapId != mapId) return false;
            return (DateTime.UtcNow - ZoneCountsAtUtc).TotalMilliseconds <= maxAgeMs;
        }

        /// <summary>
        /// Bang "Thong tin" nhan vat lan xem gan nhat (cmd 93 + 101) - UI tab "Thong tin 1/2" doc.
        /// NULL = chua tung xin. Ghi tu thread mang, doc tu thread UI: doi CA object (khong sua
        /// tai cho) nen doc mot lan ra bien cuc bo la thay anh chup nhat quan.
        /// </summary>
        public volatile Models.CharViewInfo ViewInfo;

        // True when AFK navigation is mid-transition (between SendRequestChangeMap/SendNpcMenu
        // and MAP_INFO arrival). Other Auto controllers must skip sending packets while this is set
        // to avoid acting on stale map context.
        public volatile bool IsChangingMap;

        /// <summary>
        /// Can cu dia cua phe minh ma canh tat Truong -> NPC 25 dua toi: 98 hoac 104. Clone ZangVPS
        /// <c>i_0.aD</c>: khoi tao = 98 (aD = 1, i_0.java:25194), chi doi khi nhan sub -92 cua CHINH
        /// MINH voi cTypePk 4 -> 98, 5 -> 104 (aH.java:1424-1436). CO Y khong xoa trong Reset():
        /// ben Zang no la bien tinh song suot phien, khong ve lai mac dinh khi doi map/ket noi lai.
        /// </summary>
        public volatile int CanCuDiaMap = 98;

        // Thoi diem (UTC) server bao "Khong du MP de su dung" khi dang danh. TrainMode dung cho
        // "TS khi het MP" (clone MODGAME Char.dx + Auto.m). MinValue = chua co.
        public DateTime NotEnoughMpAt = DateTime.MinValue;

        /// <summary>
        /// Moc (UTC) HE NEN vua gui cmd 41 de dung BUFF / PHAN THAN - tuc skill DANH dang chon da bi
        /// de len, mode phai gui lai truoc khi danh tiep.
        ///
        /// <para>Truoc 2026-09-10 buff nam trong TrainMode nen no tu dat <c>_selectedTemplateId = -1</c>
        /// la xong. Nay buff chay o HE NEN cho MOI mode, ma moi mode co bo chon skill rieng
        /// (<c>TrainMode.EnsureSkillSelected</c>, <c>AttackSkillSelector</c>) - nen dung mot MOC THOI
        /// GIAN chung: bo chon nao thay moc nay MOI HON lan chon cuoi cua chinh no thi gui lai.
        /// Khong co co de xoa nen khong co dua luong giua cac bo chon.</para>
        /// </summary>
        public DateTime SkillDanhBiDeAt = DateTime.MinValue;


        // Bo dem phan hoi LUYEN DA (cmd 19/20): ItemHandler tang len moi khi nhan ket qua luyen.
        // TrainMode.TryLuyenDa doc gia tri truoc khi gui roi poll cho seq doi (thay the kieu
        // Class_cl.b() blocking cua MODGAME). volatile: ghi tu thread mang, doc tu thread auto.
        public volatile int LuyenDaSeq;

        // Bo dem phan hoi CONG DIEM - SubCommandHandler tang khi nhan sub -125 (nang chieu) / -109 (cong
        // tiem nang). CongDiemRunner chup truoc khi gui roi so, thay cho Class_cl.u()/w() danh thuc
        // wait(3000) cua MODGAME. Chi de HET CHO SOM - tieu chi thanh cong la diem giam.
        public volatile int SkillUpSeq;
        public volatile int PotentialUpSeq;

        // Bo dem phan hoi DAP DO (cmd 21) - cung khuon LuyenDaSeq o tren: engine doc seq truoc khi
        // gui roi poll cho no doi (thay cho kieu cho blocking cua MODGAME).
        public volatile int UpgradeSeq;
        /// <summary>Byte result cua cmd 21 gan nhat: 1 = LEN cap, 5/6 = kham ngoc, con lai = XIT.</summary>
        public volatile int UpgradeResult;
        /// <summary>Cap +N MOI cua mon sau cmd 21 gan nhat (-1 = chua co).</summary>
        public volatile int UpgradeNewLevel = -1;

        // Bo dem phan hoi RUONG: tang khi nhan cmd 31 (danh sach ruong) HOAC cmd 16/17 (chuyen
        // 1 mon ruong<->tui). Engine dap do poll cai nay thay cho kieu cho blocking cua MODGAME.
        public volatile int BoxSeq;

        // typeUI cua man hinh server vua mo (cmd 30). 10 = man NANG CAP. -1 = chua mo/da thoat.
        // Engine dap do can biet menu Tho ren da mo chua truoc khi gui cmd 21.
        public volatile int LastUiType = -1;
        /// <summary>Thoi diem (UTC ticks) nhan cmd 30 gan nhat - de biet tin hieu con "tuoi" khong.</summary>
        public long LastUiAtTicks;

        // Text server gui gan nhat qua cmd -26 (dialog) / -25 (ticker). Engine dap do doc de bat
        // chuoi "qua nhanh" -> biet goi bi tu choi vi gui day, thoat cho ngay thay vi doi het timeout.
        private string _lastServerText = "";
        private long _lastServerTextAtTicks;
        private readonly object _serverTextLock = new object();

        public void SetLastServerText(string text)
        {
            lock (_serverTextLock)
            {
                _lastServerText = text ?? "";
                _lastServerTextAtTicks = DateTime.UtcNow.Ticks;
            }
        }

        /// <summary>
        /// True neu SAU moc `sinceTicks` server co gui text chua `phrase` (so khong dau, khong phan
        /// biet hoa thuong). Dung cho "qua nhanh" - xem DAP_DO_MODGAME.md D.8.
        /// </summary>
        public bool ServerTextContainsSince(string phrase, long sinceTicks)
        {
            if (string.IsNullOrEmpty(phrase)) return false;
            lock (_serverTextLock)
            {
                if (_lastServerTextAtTicks <= sinceTicks) return false;
                return _lastServerText.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        // Co "dang trong phien PK am" - lUU O DAY (khong phai field cua TrainMode) de SONG SOT qua
        // chet/hoi sinh: chet -> StopAutoSystems null _activeMode -> StartAutoSystems tao TrainMode MOI
        // -> neu co o TrainMode thi mat -> sau 1 cai chet la thoat PK ve train (SAI vs MODGAME giu mode
        // AutoPkAm xuyen qua chet). MODGAME chi dung PK khi no exp tich luy > 15% cap. Reset luc reconnect.
        public bool PkAmActive { get; set; }

        // Moc BAT DAU pha XA DIEM KET PHIEN cua PK Am (UTC; MinValue = khong o trong pha do).
        // PHAI nam o day cung ly do voi PkAmActive: pha nay ket thuc chu yeu BANG CAI CHET (bi nguoi
        // khac danh chet moi tut diem hieu chien), ma chet -> mode bi tao lai -> de o field cua runner
        // thi van thoat 10 phut bi reset moi lan chet => KHONG BAO GIO no. Reset luc reconnect + moi
        // lan ket phien PK.
        public DateTime PkAmXaDiemTuUtc { get; set; }

        // ===== CO CHU DICH cua chuyen DAP DO (clone AutoNangCap.dapDi / dapAt) =====
        // PHAI nam o day: chuyen dap BAT DAU BANG TU SAT, ma chet -> StopAutoSystems() null hoa
        // mode -> StartAutoSystems() tao TrainMode (va DapDoRunner) MOI. De co o runner thi no
        // bay mat dung luc can nhat, va nhan vat hoi sinh o Truong se khong biet minh toi day
        // DE LAM GI. Ben MODGAME class la static nen co song qua cai chet.
        // Doc QUA DapDoRunner.DapBay() (co han 90s tu rung), dung doc thang.
        public bool DapDoTripActive { get; set; }
        /// <summary>
        /// Moc HOI SINH gan nhat (UTC). Navigator doc de BO nhip giao cach giua hai lan doi map
        /// tren chuyen di ngay sau khi chet - user chot 2026-09-08: *"Tôi muốn hồi sinh cái, next
        /// map tiếp theo luôn"*. Chi anh huong chuyen ve bai sau khi chet (co han gio), doi map
        /// binh thuong van giu nhip "Toc do NextMap" cua tab Train.
        /// </summary>
        public DateTime HoiSinhLucUtc { get; set; }

        public DateTime DapDoTripAt { get; set; }

        // ===== CO CHU DICH cua chuyen DI BAN cua LOC DO (docs/features/LOC_DO.md §M) =====
        // CUNG LY DO voi DapDoTripActive ngay tren, khong phai chép cho vui: chuyen ban cung MO DAU
        // BANG TU SAT, ma chet -> StopAutoSystems() null hoa mode -> TrainMode + BanDoRunner bi tao
        // MOI. De co o runner thi nhan vat hoi sinh o lang se khong biet minh toi day DE LAM GI,
        // va han 90s khong bao gio no (dung lop loi dv16/dv17).
        // Doc QUA BanDoRunner.DangDiBan(), dung doc thang.
        public bool BanDoTripActive { get; set; }
        public DateTime BanDoTripAt { get; set; }

        /// <summary>
        /// Truoc moc nay KHONG mo chuyen di ban moi - dat khi mot chuyen HONG (het han / co mon server
        /// khong nhan / bi cat ngang). <c>MinValue</c> = khong nghi. Tach khoi <see cref="BanDoTripAt"/>
        /// (2026-09-14) vi nghi phai tinh tu luc chuyen KET THUC, khong phai luc bat dau: nghi 5 s tinh
        /// tu luc bat dau thi mot chuyen het han 90 s coi nhu khong nghi giay nao.
        /// </summary>
        public DateTime BanDoNghiDenUtc { get; set; }

        /// <summary>
        /// Gia tri cua <see cref="SoMonBanKhongDuoc"/> luc MO chuyen. Ket thuc chuyen ma so do da tang
        /// = trong chuyen co mon server khong nhan ⇒ chuyen HONG, du tui da het mon cho ban (mon do
        /// da bi ghi nho nen khong con duoc dem la "cho ban").
        /// </summary>
        public int BanDoTripMocKhongDuoc { get; set; }

        /// <summary>Han mot chuyen di ban (ms) - qua thi co coi nhu da rung.</summary>
        public const int BAN_DO_TRIP_HAN_MS = 90000;

        // ===== Loai mon ma server KHONG NHAN lenh BAN / VUT (docs/features/LOC_DO.md §V) =====
        // LocDoRunner gui lai toi da 3 lan; mon van nam nguyen trong tui => server khong nhan. Phai
        // nho lai, neu khong thi moi lan runner duoc tao lai (chet, doi mode) mon do lai thanh "cho
        // ban" => mo chuyen di ban => tu sat => lai bi tu choi: vong lap ton mot mang moi vong.
        // Nam o day chu khong phai field cua LocDoRunner vi runner bi tao lai sau MOI lan chet.
        //
        // KHOA = template + co khoa (2026-09-14, truoc day chi template): dong danh sach "chi KHOA /
        // chi thuong" tach duoc hai ban cua cung mot mon (vd da cap 5) - server tu choi ban da 5 KHOA
        // khong co nghia la da 5 thuong cung khong ban duoc.
        private readonly object _khongDuocLock = new object();
        private readonly System.Collections.Generic.HashSet<int> _banKhongDuoc
            = new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.HashSet<int> _vutKhongDuoc
            = new System.Collections.Generic.HashSet<int>();
        private int _soMonBanKhongDuoc;

        private static int KhoaMon(short templateId, bool isLock)
        {
            return templateId * 2 + (isLock ? 1 : 0);
        }

        /// <summary>Loai mon nay (template + khoa/thuong) da bi server tu choi BAN trong phien nay chua.</summary>
        public bool LaBanKhongDuoc(short templateId, bool isLock)
        {
            lock (_khongDuocLock) return _banKhongDuoc.Contains(KhoaMon(templateId, isLock));
        }

        /// <summary>Ghi nhan mot loai mon khong ban duoc. Tra <c>true</c> neu day la lan DAU (de log 1 lan).</summary>
        public bool GhiBanKhongDuoc(short templateId, bool isLock)
        {
            lock (_khongDuocLock)
            {
                if (!_banKhongDuoc.Add(KhoaMon(templateId, isLock))) return false;
                _soMonBanKhongDuoc++;
                return true;
            }
        }

        /// <summary>So loai mon da ghi nho "khong ban duoc" - <c>BanDoRunner</c> so truoc/sau chuyen.</summary>
        public int SoMonBanKhongDuoc
        {
            get { lock (_khongDuocLock) return _soMonBanKhongDuoc; }
        }

        /// <summary>Loai mon nay (template + khoa/thuong) da bi server tu choi VUT trong phien nay chua.</summary>
        public bool LaVutKhongDuoc(short templateId, bool isLock)
        {
            lock (_khongDuocLock) return _vutKhongDuoc.Contains(KhoaMon(templateId, isLock));
        }

        /// <summary>Ghi nhan mot loai mon khong vut duoc. Tra <c>true</c> neu day la lan DAU.</summary>
        public bool GhiVutKhongDuoc(short templateId, bool isLock)
        {
            lock (_khongDuocLock) return _vutKhongDuoc.Add(KhoaMon(templateId, isLock));
        }

        /// <summary>
        /// Chuyen di ban con hieu luc khong (da tinh han). De o day chu khong phai rieng trong
        /// <c>BanDoRunner</c> vi <see cref="NsoClient.TryReviveInPlace"/> cung phai hoi - xem chu
        /// thich o do.
        /// </summary>
        public bool BanDoTripDangChay()
        {
            return BanDoTripActive
                && (DateTime.UtcNow - BanDoTripAt).TotalMilliseconds <= BAN_DO_TRIP_HAN_MS;
        }

        // ===== NO "MAC LAI" cua auto DAP DO (clone AutoNangCap.pendingMac) =====
        // PHAI nam o day, KHONG phai field cua DapDoRunner: runner song trong TrainMode, ma
        // NsoClient.StopAutoSystems() null hoa mode moi lan CHET roi StartAutoSystems() tao
        // TrainMode (va runner) MOI -> de o runner thi mon vua thao ra se KHONG BAO GIO duoc
        // mac lai neu char chet dung luc do. Ben MODGAME class la static nen khong dinh loi nay.
        // Cung ly do voi PkAmActive / TanSatAnchor o tren.
        public Item DapDoPendingItem { get; set; }
        public int DapDoPendingSlot { get; set; }
        public int DapDoPendingTries { get; set; }
        public DateTime DapDoPendingNextAt { get; set; }

        /// <summary>Cac bang shop server da gui (cmd 33), tra theo typeUI.</summary>
        public readonly ShopTables Shops = new ShopTables();

        // Co "thanh vien da xin lenh nhom (tsr) trong ket noi nay" - dat o day (khong phai field cua
        // GroupPlayController) vi controller bi tao lai moi lan chet/restart auto -> flag o controller
        // se lam xin lai moi lan hoi sinh (spam). Reset luc (re)connect: vao lai sau disconnect can xin
        // lai vi truong co the da doi map/khu trong luc minh offline.
        public bool GroupOrderRequested { get; set; }

        // Lenh "Danh theo nhom" nhan tu truong ("ts <map> <khu>"), -1 = chua co lenh. RUNTIME thuan
        // tuy: KHONG bao gio ghi de Config.TargetMapId/Zone user cai (config la cua user, lenh la cua
        // truong). Mode doc dich qua NsoClient.EffectiveTargetMapId/ZoneId: AttackByGroup bat + co lenh
        // -> lenh THANG config; tat checkbox -> ve ngay map rieng. Mat khi reconnect (Reset) - da co
        // co che "tsr" xin lai lenh. Xoa khi roi nhom / duoc don len truong (GroupPlayController).
        public int GroupOrderMapId { get; set; }
        public int GroupOrderZoneId { get; set; }

        // ---- "Bam nhom truong": muc tieu TRUONG dang danh (clone V9_X1 `f.java:1587`) ----
        // Nguon: cmd 60 (NGUOI KHAC dung chieu len QUAI) = `charId int, skillTpl byte, mobId ubyte x N`
        // (SERVER_FACTS.md §2). CombatHandler ghi vao day khi charId == truong nhom.
        // KHONG dung khoa: thread mang CHI GHI, thread mode CHI DOC, va ghi theo thu tu
        // "mang truoc -> moc thoi gian sau" nen ben doc doc "moc sau -> mang truoc" khong bao gio
        // thay moc moi kem mang cu. Doc sai mot nhip chi la danh cham/nham 1 con - vo hai.
        /// <summary>Danh sach mobId truong vua dung chieu len (null = chua co).</summary>
        public byte[] LeaderFocusMobIds { get; set; }
        /// <summary>Moc UTC cua ban ghi tren. Qua han (LEADER_FOCUS_FRESH_MS) thi coi nhu khong co.</summary>
        public DateTime LeaderFocusAt { get; set; }

        // ⚠️ VUNG "KICH YEN - hang cho + may trang thai" cua NSOLITEPRO DA BO O DAY.
        // Ban nay chi co CHIEU GUI (Auto/AddOns/BaoTaTl.cs); khong account nao trong tool NHAN
        // loi goi, nen hang cho 50 cho, dedupe, TTL va may trang thai IDLE/TRAVEL/HUNT deu vo nghia.
        // Can chieu nhan -> dung NSOLITEPRO hoac AngelChip, dung them vao day.

        // (Baseline Yen/h da chuyen sang NsoClient.YenBase de KHONG bi xoa luc reconnect.)

        // Latency in ms of the last successful map change (time between
        // SendRequestChangeMap and MAP_INFO arriving). Used for adaptive timeout.
        public volatile int LastMapChangeMs;

        // Diem chet gan nhat trong luc chay AutoTanSat. Sau revive, controller
        // moi se navigate ve day. Null = khong co diem chet pending.
        // GIU LAI cho tuong thich; TanSat gio dung TanSatAnchor ben duoi.
        public DeathSpot LastTanSatDeathSpot { get; set; }

        /// <summary>
        /// NEO BAI FARM tu hoc (clone MODGAME Auto.e/Auto.f, gan o Auto.java:558-559):
        /// vi tri nhan vat NGAY SAU khi lao toi con mob dang danh. Cap nhat moi lan doi target.
        /// Khac "diem chet": neo la cho DANG farm, khong phai cho bi nga (co the la day vuc
        /// sau khi tu sat het MP). MODGAME khong he snapshot toa do luc chet - no chi giu
        /// map/khu dich + neo nay, chet xong ve map/khu roi keo ve neo (Auto.java:179-182).
        ///
        /// Phai nam o GameState (khong phai TrainMode) vi NsoClient.StopAutoSystems() null hoa
        /// mode khi chet va StartAutoSystems() tao TrainMode MOI -> moi field cua mode bi mat.
        /// </summary>
        public DeathSpot TanSatAnchor { get; set; }

        /// <summary>Neo TRUOC DO (clone Auto.o/Auto.p) - het mob thi lui ve day cho respawn.</summary>
        public DeathSpot TanSatAnchorPrev { get; set; }

        // Data sync tracking
        public byte[] DataSyncVersions { get; set; }
        public bool[] DataSyncDone { get; set; }

        // Tile engine for collision/ground-finding
        public TileEngine Tiles { get; private set; }

        // Ban khi tui/trang bi (BagItems/BodyItems) doi - de UI cap nhat hanh trang NGAY
        // (push, khong poll). Ban tu ItemHandler (cmd 7/8/9/10/18) + luc parse char info login.
        // Chay tren thread engine/session -> UI phai BeginInvoke ve thread giao dien.
        public event Action OnInventoryChanged;
        public void RaiseInventoryChanged()
        {
            var h = OnInventoryChanged;
            if (h != null) h();
        }

        // Ban khi SERVER dat lai vi tri nhan vat (cmd 52, MAP_INFO, hoi sinh). NsoClient noi vao
        // MovementService.SyncLastSent de chot chong trung goi move khong nuot mat lenh di sua sai.
        // Clone `cx = cxSend` cua ban goc - xem MovementService.SyncLastSent.
        public event Action<short, short> OnServerSetPosition;
        // ===== HP THAT (2026-09-07 dd2) =====
        // Cua DUY NHAT de ghi CharacterState.ServerHp. Moi handler nhan HP tu server phai di qua day;
        // khong cho phep bat ky cho nao khac ghi, vi ca gia tri cua no la "khong bao gio bi bia".
        // Doi chieu ZangVPS: `bU` chi doi o cac goi server + `ax.dv()` (cmd -10/88) - khong co duong
        // nao khac. Xem chu thich day o CharacterState.ServerHp.
        //
        // Log CO PHANH 5 giay/account de doc duoc nhip HP that ma khong nhan chim file log; nhung
        // MOC CHUYEN 0 <-> con song thi LUON ghi (do la thoi diem quyet dinh "minh song hay chet").
        private DateTime _lastHpLogAt = DateTime.MinValue;
        private const int HP_LOG_GAP_MS = 5000;

        /// <summary>
        /// Server bao HP > 0 trong khi ta dang coi la CHET -> ket thuc dot chet.
        /// Clone dieu kien song cua ZangVPS: <c>Z.b()</c> = <c>bU &lt;= 0 || E == 14 || E == 5</c>
        /// (Z.java:3564) - tuc "con song" CHI khi HP THAT cua server &gt; 0. MAP_INFO khong phai
        /// giay chung tu: Zang sau khi gui -9 cung chi cho goi map (av_0.q()) roi HOI LAI Z.A().
        /// </summary>
        public event Action<int> OnServerBaoSong;

        public void NoteServerHp(int hp, int maxHp, string src)
        {
            var c = MyChar;
            if (c == null) return;
            int truoc = c.ServerHp;
            c.ServerHp = hp;
            if (maxHp > 0) c.ServerHpMaxSeen = maxHp;
            c.ServerHpAt = DateTime.UtcNow;
            // Server noi mot con so > 0 = no xac nhan ta con song. Xem CharacterState.ServerAliveAt.
            if (hp > 0) c.ServerAliveAt = c.ServerHpAt;

            // ===== GHIM HP VE 0 KHI SERVER BAO CHET (clone ZangVPS aM.java:503-506) =====
            //     if (!(ax.a().bU > 0) || ax.a().E == 14) { ax.a().bU = 0; }
            // Zang KHONG cho bat cu thu gi bia HP len trong luc server con giu nhan vat la xac.
            // Doi chieu them client goc 2.5.1: `cHP = cMaxHP` chi ton tai o case -125 (Controller.cs
            // :4207) va case -109 (:4248), CA HAI deu boc trong `if (statusMe != 14 && statusMe != 5)`.
            // Goi map (-18) KHONG he dung toi cHP - nen ta cung khong duoc dung.
            if (hp <= 0) c.Hp = 0;

            if (hp > 0 && truoc <= 0)
            {
                var hs = OnServerBaoSong;
                if (hs != null) hs(hp);
            }

            bool mocQuanTrong = (truoc <= 0) != (hp <= 0);   // song<->chet
            if (!mocQuanTrong && (DateTime.UtcNow - _lastHpLogAt).TotalMilliseconds < HP_LOG_GAP_MS) return;
            _lastHpLogAt = DateTime.UtcNow;
            RaiseDebugLog(string.Format("[Hp] server bao {0}/{1} (truoc {2}) qua {3}{4} | so ta dang hien: {5}",
                hp, maxHp > 0 ? maxHp : c.MaxHp, truoc, src,
                mocQuanTrong ? (hp <= 0 ? "  <<< CHET" : "  <<< SONG LAI") : "", c.Hp));
        }

        // ===== HP TU TRU TAI CHO (2026-09-07 dd3) =====
        // Dem + log thua (30s/lan) viec tru mau cuc bo, de doi chieu voi con so server gui ve o
        // NoteServerHp. Neu hai ben lech nhieu thi cong thuc tru sai; lech it la binh thuong
        // (buff/giap doi giua hai lan server bao).
        private DateTime _lastLocalHpLogAt = DateTime.MinValue;
        private int _localHpHits;
        private int _localHpTotalDame;

        public void NoteLocalHp(int dame)
        {
            LastDamageAt = DateTime.UtcNow;   // xem khoi "DO IM LANG BA DUONG" ben tren
            _localHpHits++;
            _localHpTotalDame += dame > 0 ? dame : 0;
            if ((DateTime.UtcNow - _lastLocalHpLogAt).TotalMilliseconds < 30000) return;
            _lastLocalHpLogAt = DateTime.UtcNow;
            var c = MyChar;
            RaiseDebugLog(string.Format("[Hp] Tu tru tai cho: {0} don / {1} sat thuong -> con {2}/{3} (server bao lan cuoi: {4})",
                _localHpHits, _localHpTotalDame, c != null ? c.Hp : -1, c != null ? c.MaxHp : -1,
                c != null ? c.ServerHp : -1));
        }

        // ==================================================================================
        // ===== DO IM LANG BA DUONG (2026-09-08 dd15) ======================================
        // ==================================================================================
        // Ca phai bat: nhan vat CHET ma server KHONG gui gi ca - khong `cmd -11`, khong goi HP = 0,
        // khong goi sat thuong. Va ca "song day ao": server gui `cmd -10`, ta tuyen bo song lai roi
        // chay auto, nhung nhan vat van la cai xac.
        //
        // Do tren log 2026-09-08:
        //   barbigz115  nam chet 75 giay: cmd -11 = 0, goi sat thuong = 0, dong farm = 0,
        //               trong khi 14 acc cung map chet 2-8 lan MOI acc. Bot van doc 1025/1629.
        //   barbigz107  im 32 giay -> `cmd -9` DUOC server chap nhan (chuyen ve Truong Ookaza)
        //   barbigz102  im 36 giay -> nt
        // Hai ca sau khong he co `cmd -11`: chet cam, va chinh viec `-9` AN la bang chung da chet.
        //
        // ⚠️ VI SAO KHONG DUNG "TrainMode dung chay": ca phien khong co MOT dong `[ChanDoan]` nao.
        // Tick VAN quay deu - no vao toi phan danh nhau, thay "het muc tieu, cho respawn" roi ngoi
        // doi vinh vien vi cai xac khong duoc cap nhat danh sach quai.
        //
        // ⚠️ VI SAO NGUONG 5 GIAY KHONG DUNG PHAI HOI SINH THAT: do 766 ca hoi sinh, bot danh don
        // dau tien sau 5-10 giay - NHUNG khoang do day ap `MAP_INFO` (di 72->39->40->41, moi map
        // mot goi), va moi goi nap lai `LastWorldAt`. Nen im CA BA duong 5 giay lien la trang thai
        // ma mot nhan vat song trong map train khong the o vao.
        public DateTime LastDamageAt;      // lan cuoi nhan mot don vao nguoi (CombatHandler)
        public DateTime LastAttackAt;      // lan cuoi TA gui mot don danh (CombatService)
        public DateTime LastWorldAt;       // lan cuoi the gioi doi quanh ta (vao map moi)

        /// <summary>Ta vua gui mot don danh - con dang hanh dong.</summary>
        public void NoteAttackSent() { LastAttackAt = DateTime.UtcNow; }

        /// <summary>
        /// Lan cuoi ta gui don danh QUAI (cmd 60 / don hon hop co quai). RIENG cho SongGia: server chi
        /// bao mau QUAI, nen don danh NGUOI (Danh PK, PK Am, Thua loi dai, tu danh chong idle) khong bao
        /// gio co tin dap - dem chung vao <see cref="LastAttackAt"/> la SongGia ket luan "xac" roi tu vao
        /// lai giua tran PK (di3).
        /// </summary>
        public DateTime LastMobAttackAt;

        public void NoteMobAttackSent() { LastMobAttackAt = DateTime.UtcNow; }

        // ==================================================================================
        // ===== "SONG GIA": SERVER NGUNG DAP LAI DON DANH (2026-09-08 dd16) ================
        // ==================================================================================
        // Day moi la dau hieu dung cua cai xac, va no NGUOC HAN voi thu ta di tim ca dem:
        // cai xac KHONG im lang - no van vung kiem deu dan, chi la khong ai dap lai nua.
        //
        // Do tren barbigz103 (log 07:36-07:41), doc tu chinh bo dem cua AoeProbe:
        //     07:36:58   server = 23737   ta = 24246
        //     07:38:58   server = 23793   ta = 25022
        //     07:40:59   server = 23793   ta = 25805     <- server DUNG YEN
        // Ta danh them 783 con giua hai moc cuoi, server bao lai DUNG 0 con. Nhan vat da chet tu
        // ~07:38:58; bot vung kiem vao khong khi them 145 giay roi bi cat o 225 goi/giay.
        //
        // Doi chieu 8 acc KHOE cung luc: bo dem server bam sat bo dem ta o CA 8 (23737->23793,
        // 24164->24180, 24255->24267...). Khong mot acc lanh nao dung yen.
        //
        // ⚠️ VI SAO PHEP DO IM LANG (dd15) MU VOI CA NAY: cai xac dang danh 2 lan/giay nen
        // `LastAttackAt` luon tuoi -> dong ho im lang khong bao gio tich duoc 5 giay. dd15 ban 50
        // lan trong 3 gio, 0 lan bat dung. Phai do CAI DAP LAI, khong phai do su im lang.
        public DateTime LastAttackAckAt;

        /// <summary>Server vua bao mau mot con ta danh - no CON thi hanh lenh cua ta.</summary>
        public void NoteAttackAck() { LastAttackAckAt = DateTime.UtcNow; }

        /// <summary>Vao map moi (MAP_INFO) - server vua dung lai the gioi quanh ta.</summary>
        public void NoteWorldEvent() { LastWorldAt = DateTime.UtcNow; }

        /// <summary>
        /// Lan cuoi nhan BAT KY goi nao tu server (NsoClient ghi truoc khi route). Doc boi
        /// KeepAliveController.TickDocIm: server im lau thi hoi cmd 93 truoc khi han doc 120s cua
        /// NsoConnection tu cat ket noi (log barbigz350 2026-09-15, di1).
        /// </summary>
        public DateTime LastRecvAt;

        public void RaiseServerSetPosition(short x, short y)
        {
            lock (_confirmedLock)
            {
                if (!_hasConfirmed || x != _confirmedX || y != _confirmedY)
                {
                    _confirmedX = x;
                    _confirmedY = y;
                    _confirmedMovedAt = DateTime.UtcNow;
                    _confirmedRepeat = 0;
                }
                else
                {
                    // Server dat lai DUNG cho cu mot lan nua = no vua tu choi them mot lenh di.
                    if (_confirmedRepeat < int.MaxValue) _confirmedRepeat++;
                }
                _hasConfirmed = true;
            }
            var h = OnServerSetPosition;
            if (h != null) h(x, y);
        }

        // ===== Vi tri DA DUOC SERVER XAC NHAN (2026-09-07) =====
        // MyChar.Cx/Cy la toa do LAC QUAN: CharBurstMove tu gan dich vao do truoc khi server dong y
        // (Navigator.cs, cuoi CharBurstMove). Voi mot acc bi dong bang, MyChar.Cx/Cy van nhay lien
        // tuc (nhich lui -> burst -> cmd52 keo ve) trong khi server KHONG he doi mot pixel.
        // => Moi cho can tra loi cau hoi "nhan vat co thuc su nhuc nhich khong" PHAI doc bo nay,
        //    khong duoc doc MyChar. Do la ly do KeepAliveController truoc day mu tit (xem
        //    docs/features/DONG_BANG_VI_TRI.md muc B.2/E6).
        // Nguon ghi: cmd 52, MAP_INFO, hoi sinh - dung 4 cho goi RaiseServerSetPosition.
        //
        // ⚠️ BAY DA DAM PHAI 2026-09-07, DUNG DAM LAI: "toa do nay khong doi" KHONG co nghia la
        // "nhan vat dung im". Server CHI len tieng khi no KHONG dong y; im lang = dong y. Mot acc
        // dang farm ngon lanh trong cung mot map ca phut thi khong co cmd52 lan MAP_INFO nao, nen
        // _confirmedMovedAt cung dung yen y het mot acc dong bang.
        // => Tin hieu phan biet KHONG phai su VANG MAT ma la su LAP LAI: server keo ta ve DUNG MOT
        //    diem nhieu lan lien tiep (_confirmedRepeat). barbigz106 co 78 lan ve dung (1620,672);
        //    acc lanh gan nhu bang 0.
        private readonly object _confirmedLock = new object();
        private short _confirmedX, _confirmedY;
        private DateTime _confirmedMovedAt;
        private int _confirmedRepeat;    // so lan server dat lai DUNG cho cu, ke tu lan doi that gan nhat
        private bool _hasConfirmed;

        /// <summary>
        /// Chup vi tri server xac nhan gan nhat, moc thoi diem no DOI lan cuoi, va so lan server
        /// dat lai DUNG cho do ke tu luc do (= so lenh di bi tu choi lien tiep).
        /// Tra false khi server chua he dat vi tri lan nao (vua vao game, chua co cmd52/MAP_INFO).
        /// </summary>
        public bool TryGetConfirmedPos(out short x, out short y, out DateTime movedAtUtc, out int repeat)
        {
            lock (_confirmedLock)
            {
                x = _confirmedX; y = _confirmedY; movedAtUtc = _confirmedMovedAt; repeat = _confirmedRepeat;
                return _hasConfirmed;
            }
        }

        // Log debug tu handler (khong co Client.Log) -> NsoClient noi vao Client.Log. Dung cho [EFF]/[Auto].
        public event Action<string> OnDebugLog;
        public void RaiseDebugLog(string text)
        {
            var h = OnDebugLog;
            if (h != null) h(text);
        }

        /// <summary>
        /// Con dang co hieu ung EffectId (chua het han cuc bo) khong. Snapshot co lock (Effects bi
        /// ghi tu thread mang, doc tu thread auto). effectId &lt; 0 -> false.
        /// </summary>
        public bool HasActiveEffect(int effectId)
        {
            if (effectId < 0) return false;
            try
            {
                var now = DateTime.UtcNow;
                Effect[] snap;
                lock (Effects) snap = Effects.ToArray();
                for (int i = 0; i < snap.Length; i++)
                {
                    var e = snap[i];
                    if (e != null && e.EffectId == effectId && e.ExpiresAt > now) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Số sát thương bay lên quái (cửa sổ "Xem game"). MẶC ĐỊNH TẮT — chỉ bật khi cửa sổ mở,
        /// xem <see cref="Models.FlyTextBoard.Enabled"/>. Tắt thì handler không cấp phát gì.
        /// </summary>
        public readonly FlyTextBoard FlyTexts = new FlyTextBoard();

        /// <summary>
        /// Hoạt cảnh đánh: cử động nhân vật + hiệu ứng skill + hiệu ứng lan (cửa sổ "Xem game").
        /// MẶC ĐỊNH TẮT — chỉ bật khi cửa sổ mở, xem <see cref="Models.SkillFxBoard.Enabled"/>.
        /// Tắt thì nơi gửi đòn không cấp phát mảng mục tiêu nào.
        /// Đặc tả: docs/features/HIEU_UNG_SKILL.md.
        /// </summary>
        public readonly SkillFxBoard SkillFx = new SkillFxBoard();

        /// <summary>
        /// "Thông tin nhận" — thanh chạy chữ ĐÁY màn hình của cửa sổ "Xem game", clone
        /// <c>InfoMe</c> của client. MẶC ĐỊNH TẮT — chỉ bật khi cửa sổ mở VÀ ô tích
        /// "Bật thông báo nhận" đang bật (xem <see cref="Models.NoticeBoard.Enabled"/>).
        /// Tắt thì handler thoát TRƯỚC khi nối chuỗi, không cấp phát gì.
        /// </summary>
        public readonly NoticeBoard Notices = new NoticeBoard();

        // Bang effTemplate.type theo id (clone MODGAME Effect.effTemplates[id].type) - parse tu duoi
        // block DataSync "data" (NotMapHandler.HandleUpdateData). type 0 = THUC AN (MODGAME Char.java:868).
        // _effectTypes[id] = type, -1 = id khong co trong bang. null = server chua gui bang (fallback).
        // KHONG reset luc reconnect (server-static; datasync chi gui lai khi doi version).
        private int[] _effectTypes;
        public bool HasEffectTypes { get { return _effectTypes != null; } }
        public void SetEffectTypes(int[] typesById) { _effectTypes = typesById; }

        // Ten hieu ung theo id - cung tu block DataSync "data" (NotMapHandler), truoc day doc roi vut.
        // Chi dung de HIEN THI (cua so "Xem game"), khong dung cho logic auto.
        private string[] _effectNames;
        public void SetEffectNames(string[] namesById) { _effectNames = namesById; }

        // Icon hieu ung theo id = chi so SmallImage (EffectTemplate.iconId). Cung nguon voi ten.
        // -1 = khong co. Chi de HIEN THI.
        private int[] _effectIcons;
        public void SetEffectIcons(int[] iconsById) { _effectIcons = iconsById; }
        public int GetEffectIcon(int id)
        {
            var n = _effectIcons;
            if (n != null && id >= 0 && id < n.Length) return n[id];
            return -1;
        }

        public string GetEffectName(int id)
        {
            var n = _effectNames;
            if (n != null && id >= 0 && id < n.Length && !string.IsNullOrEmpty(n[id])) return n[id];
            return null;
        }
        /// <summary>
        /// Nhan hien thi khi KHONG tra duoc ten that. Ten hieu ung do SERVER gui (bang effTemplate
        /// cuoi block DataSync "data" - SERVER_FACTS §13), KHONG co bang tinh nao de tra, nen cho nay
        /// khong duoc bia ten.
        ///
        /// Truoc day moi truong hop deu ra "Hieu ung #31" giong het nhau, che mat SU KHAC BIET quan
        /// trong: server chua gui bang (loi parse DataSync) HAY bang co ma thieu dung id nay. Hai
        /// nguyen nhan nay can hai cach sua khac han.
        /// </summary>
        private string DescribeUnknownEffect(int id)
        {
            int type = GetEffectType(id);
            if (type == 0) return "Thức ăn";              // SERVER_FACTS §13: type 0 = THUC AN
            if (_effectNames == null)
                return "Hiệu ứng #" + id + " (chưa có bảng tên)";
            if (type >= 0)
                return "Hiệu ứng #" + id + " (loại " + type + ")";
            return "Hiệu ứng #" + id + " (không có trong bảng)";
        }

        public int GetEffectType(int id)
        {
            var t = _effectTypes;
            return (t != null && id >= 0 && id < t.Length) ? t[id] : -1;
        }

        /// <summary>Co hieu ung (chua het han) thuoc effTemplate.type == type dang hoat dong khong.</summary>
        public bool HasActiveEffectOfType(int type)
        {
            var t = _effectTypes;
            if (t == null) return false;
            try
            {
                var now = DateTime.UtcNow;
                Effect[] snap;
                lock (Effects) snap = Effects.ToArray();
                for (int i = 0; i < snap.Length; i++)
                {
                    var e = snap[i];
                    if (e == null || e.ExpiresAt <= now) continue;
                    int id = e.EffectId;
                    if (id >= 0 && id < t.Length && t[id] == type) return true;
                }
            }
            catch { }
            return false;
        }

        // Template stores. Chi duoc GHI trong luc DataSync; sau do chi doc -> co the dung chung
        // giua nhieu account cung server+version (xem SharedGameData).
        public ItemTemplateStore ItemStore { get; private set; }
        public SkillTemplateStore SkillStore { get; private set; }
        public MobTemplateStore MobStore { get; private set; }
        public MapTemplateStore MapStore { get; private set; }
        public NpcTemplateStore NpcStore { get; private set; }

        /// <summary>Dung lai bo du lieu tinh da nap san cua account khac (cung server + version).</summary>
        public void AttachSharedData(GameDataSet s)
        {
            if (s == null) return;
            ItemStore = s.ItemStore;
            SkillStore = s.SkillStore;
            MobStore = s.MobStore;
            MapStore = s.MapStore;
            NpcStore = s.NpcStore;
            _effectTypes = s.EffectTypes;
            _effectNames = s.EffectNames;   // thieu 2 dong nay = acc thu 2 tro di mat het icon hieu ung
            _effectIcons = s.EffectIcons;
        }

        /// <summary>Dong goi bo du lieu tinh vua nap xong de cong bo cho cac account sau.</summary>
        public GameDataSet SnapshotSharedData()
        {
            return new GameDataSet
            {
                ItemStore = ItemStore,
                SkillStore = SkillStore,
                MobStore = MobStore,
                MapStore = MapStore,
                NpcStore = NpcStore,
                EffectTypes = _effectTypes,
                EffectNames = _effectNames,
                EffectIcons = _effectIcons
            };
        }

        public GameStateManager()
        {
            MyChar = new CharacterState();
            CurrentMap = new MapState();
            PartyMembers = new List<PartyMember>();
            Effects = new List<Effect>();
            DataSyncDone = new bool[4];
            ZoneCountsMapId = -1;
            GroupOrderMapId = -1;
            GroupOrderZoneId = -1;
            Tiles = new TileEngine();
            ItemStore = new ItemTemplateStore();
            SkillStore = new SkillTemplateStore();
            MobStore = new MobTemplateStore();
            MapStore = new MapTemplateStore();
            NpcStore = new NpcTemplateStore();
        }

        public bool IsAllDataSynced
        {
            get
            {
                return DataSyncDone[0] && DataSyncDone[1] &&
                       DataSyncDone[2] && DataSyncDone[3];
            }
        }

        // ===================== dong ho hieu ung (cmd 117 goi phu, sub 2) =====================
        // Ghi tu thread mang, doc tu thread UI (cua so "Xem game") -> MOI truy cap qua helper duoi.
        // Field private readonly + khong lo list ra ngoai (khuon _kyQueue), KHONG dung
        // "public List<> + lock tren chinh list" nhu MapState.ItemsOnMap.
        private readonly List<EffectCountdown> _countdowns = new List<EffectCountdown>();

        /// <summary>Tran an toan - server hong/lap khong duoc lam phinh bo nho.</summary>
        private const int COUNTDOWN_MAX = 32;

        /// <summary>Them moi hoac gia han dong dem nguoc theo id. Tra ve true neu la dong moi.</summary>
        public bool CountdownUpsert(EffectCountdown c)
        {
            if (c == null) return false;
            lock (_countdowns)
            {
                for (int i = 0; i < _countdowns.Count; i++)
                    if (_countdowns[i].Id == c.Id) { _countdowns[i] = c; return false; }
                if (_countdowns.Count >= COUNTDOWN_MAX) return false;
                _countdowns.Add(c);
                return true;
            }
        }

        /// <summary>Xoa dong theo id (server gui kieu -2).</summary>
        public void CountdownRemove(short id)
        {
            lock (_countdowns) _countdowns.RemoveAll(c => c.Id == id);
        }

        /// <summary>
        /// Bản sao các dòng CÒN HẠN để vẽ. Dòng kiểu 1 hết giờ thì tự rụng (client cũng ẩn khi
        /// số giây về 0); dòng kiểu khác không có giờ nên giữ đến khi server báo xoá.
        /// </summary>
        public EffectCountdown[] SnapshotCountdowns()
        {
            lock (_countdowns)
            {
                if (_countdowns.Count == 0) return new EffectCountdown[0];
                var now = DateTime.UtcNow;
                _countdowns.RemoveAll(c => c.Kind == 1 && c.ExpiresAt <= now);
                return _countdowns.ToArray();
            }
        }

        /// <summary>
        /// Gộp MỌI hiệu ứng đang chạy thành một danh sách để hiển thị, từ HAI nguồn:
        ///
        ///   1. <see cref="Effects"/> — buff theo id, nạp qua gói phụ (SubCommandHandler.UpsertEffect).
        ///      Đây là nguồn CHẮC CHẮN CÓ: bot đã dùng nó cho logic auto từ lâu. Tên lấy từ bảng
        ///      effTemplate của DataSync (trước đây đọc rồi vứt, nay đã giữ).
        ///   2. <see cref="SnapshotCountdowns"/> — đồng hồ cmd 117. Nguồn này CHƯA chắc server có gửi
        ///      (MODGAME không có cơ chế đó — xem SERVER_FACTS §20), nên chỉ là phần cộng thêm.
        ///
        /// Gọi từ thread UI, mỗi khung một lần.
        /// </summary>
        public EffectCountdown[] SnapshotEffectLines()
        {
            var rows = new List<EffectCountdown>();
            var now = DateTime.UtcNow;

            try
            {
                Effect[] snap;
                lock (Effects) snap = Effects.ToArray();
                for (int i = 0; i < snap.Length; i++)
                {
                    var e = snap[i];
                    if (e == null || e.ExpiresAt <= now) continue;
                    string nm = GetEffectName(e.EffectId) ?? DescribeUnknownEffect(e.EffectId);
                    rows.Add(new EffectCountdown
                    {
                        Id = e.EffectId,
                        Name = nm,
                        IconId = (short)GetEffectIcon(e.EffectId),
                        Kind = 1,                 // co dem lui
                        ExpiresAt = e.ExpiresAt
                    });
                }
            }
            catch { }

            try
            {
                var cds = SnapshotCountdowns();
                for (int i = 0; i < cds.Length; i++)
                {
                    var c = cds[i];
                    if (c == null) continue;
                    // tranh trung dong neu ca hai nguon cung noi ve mot thu
                    bool trung = false;
                    for (int k = 0; k < rows.Count; k++)
                        if (rows[k].Name == c.Name) { trung = true; break; }
                    if (!trung) rows.Add(c);
                }
            }
            catch { }

            return rows.ToArray();
        }

        public void Reset()
        {
            MyChar = new CharacterState();
            CurrentMap = new MapState();
            PartyMembers.Clear();
            lock (Effects) Effects.Clear();  // bang effTemplate (type) GIU nguyen: server-static, khong phu thuoc tai khoan
            lock (_countdowns) _countdowns.Clear();   // dong ho cua tai khoan cu phai bien mat
            // Phat nhip danh la do server CU tra loi -> do lai tu dau tren ket noi moi.
            AttackPadPenaltyMs = 0;
            // Vi tri server xac nhan la cua KET NOI CU: giu lai thi watchdog dung im se lay moc
            // thoi gian tu phien truoc va ban ngay mot loat goi "danh thuc" khi vua vao game.
            lock (_confirmedLock) { _hasConfirmed = false; _confirmedX = 0; _confirmedY = 0; _confirmedRepeat = 0; _confirmedMovedAt = default(DateTime); }
            // So lieu khu la cua map/server CU -> phai xoa, khong duoc de song qua reconnect.
            ZonePlayerCounts = null;
            ZonePartyCounts = null;
            ZoneCountsMapId = -1;
            ZoneCountsAtUtc = default(DateTime);
            ClearHopZone();
            IsInGame = false;
            IsInParty = false;
            PartyLocked = false;
            IsChangingMap = false;
            LastMapChangeMs = 0;
            LastTanSatDeathSpot = null;
            TanSatAnchor = null;        // Reset() chi chay khi doi tai khoan/dut ket noi -> bai cu vo nghia
            TanSatAnchorPrev = null;
            PkAmActive = false;
            PkAmXaDiemTuUtc = DateTime.MinValue;
            ViewInfo = null;            // bang "Thong tin" cua char cu -> vo nghia sau khi doi tai khoan
            // Doi tai khoan / dut ket noi -> moi tham chieu Item deu la rac, bo no mac lai.
            DapDoTripActive = false;
            BanDoTripActive = false;
            BanDoTripAt = DateTime.MinValue;
            BanDoNghiDenUtc = DateTime.MinValue;
            BanDoTripMocKhongDuoc = 0;
            lock (_khongDuocLock)
            {
                _banKhongDuoc.Clear();
                _vutKhongDuoc.Clear();
                _soMonBanKhongDuoc = 0;
            }
            DapDoPendingItem = null;
            DapDoPendingSlot = -1;
            DapDoPendingTries = 0;
            DapDoPendingNextAt = DateTime.MinValue;
            Shops.Reset();
            GroupOrderRequested = false;
            GroupOrderMapId = -1;
            GroupOrderZoneId = -1;
            LeaderFocusMobIds = null;
            LeaderFocusAt = DateTime.MinValue;
            DataSyncDone = new bool[4];
        }
    }
}
