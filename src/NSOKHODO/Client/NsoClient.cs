using System;
using System.Threading;
using NSOKHODO.Auto;
using NSOKHODO.Controller;
using NSOKHODO.Core;
using NSOKHODO.GameData;
using NSOKHODO.Models;
using NSOKHODO.Protocol;
using NSOKHODO.Service;

namespace NSOKHODO.Client
{
    public class NsoClient : IDisposable
    {
        private NsoSession _session;
        private MessageRouter _router;
        private GameStateManager _state;

        /// <summary>
        /// Doi quan dang so huu client nay (FleetManager gan luc StartAccount). Kich Yen dung de
        /// giao loi goi NOI BO giua cac account cung tien trinh - nhanh hon va khong dinh bo loc
        /// chat cua server. null khi client duoc tao ngoai fleet (khong xay ra o duong chay chinh).
        /// </summary>
        public Fleet.FleetManager Fleet { get; set; }

        public LoginService Login { get; private set; }
        public DataSyncService DataSync { get; private set; }
        public MovementService Movement { get; private set; }
        public CombatService Combat { get; private set; }
        public ChatService Chat { get; private set; }
        public PartyService Party { get; private set; }
        public ItemService Items { get; private set; }
        public NpcService Npc { get; private set; }
        public MiscService Misc { get; private set; }
        public CharService Character { get; private set; }
        public FriendService Friends { get; private set; }
        /// <summary>NSOKHODO: chieu GUI cua giao dich (43/44/45/46/56/57).</summary>
        public TradeService TradeSvc { get; private set; }

        /// <summary>
        /// NSOKHODO: trang thai giao dich. MOT doi tuong cho ca doi client (song qua reconnect) - luong
        /// nhan goi ghi, mode kho doc. Xem <see cref="Client.TradeState"/>.
        /// </summary>
        public TradeState Trade { get; private set; }

        private volatile bool _dangGiaoDich;
        /// <summary>
        /// NSOKHODO: mode kho dang chay mot phien giao dich. KeepAliveController doc de tam hoan cu nhich
        /// chong-AFK; mode doc de tam hoan tu danh 60 giay (SPEC §11).
        /// </summary>
        public bool DangGiaoDich { get { return _dangGiaoDich; } set { _dangGiaoDich = value; } }

        private long _vaoKhuLucTicks;
        /// <summary>
        /// NSOKHODO: luc (UTC) nhan MAP_INFO gan nhat = luc DAT CHAN toi khu hien tai. Hoi chieu doi khu
        /// cua game dem tu moc nay (user chot, SPEC D28).
        /// </summary>
        public DateTime VaoKhuLucUtc
        {
            get { return new DateTime(System.Threading.Interlocked.Read(ref _vaoKhuLucTicks), DateTimeKind.Utc); }
        }

        /// <summary>NSOKHODO: chat rieng den (nguoi gui, noi dung THO). Chay tren luong nhan goi.</summary>
        public event Action<NsoClient, string, string> OnPrivateChatReceived;

        /// <summary>
        /// NSOKHODO: moi goi nhan truoc khi xu ly (de ghi hex). Chay tren luong nhan goi - ben nghe tu
        /// loc cmd va tra ve nhanh.
        /// </summary>
        public event Action<NsoClient, sbyte, NsoMessage> OnRawPacket;

        // Che do tu dong DUY NHAT cua ban nay: KhoMode (giu do - xem docs/SPEC.md).
        // Giu kieu IAutoMode de AutoModeBase lo thread/vong lap nhu cu.
        private IAutoMode _activeMode;
        // Add-on chay song song voi mode. Ban nay chi con MOT: giu ket noi.
        private KeepAliveController _keepAlive;

        private volatile bool _stopping;
        private volatile bool _loginInProgress;
        private volatile bool _isReconnect;
        private string _resolvedServerName;   // ten server that sau Resolve - lam khoa SharedGameData
        private string _dataKey;              // khoa (server|dataVersion) cua bo du lieu tinh dang dung
        private int _reconnectCount;
        // So lan LIEN TIEP noi duoc TCP nhung server khong gui khoa ("Timeout: no key").
        // ⚠️ NGUYEN NHAN CHUA XAC DINH. SERVER_FACTS §29 tung khang dinh day la "het slot theo IP" -
        // user bac bo 2026-09-09, muc do da bi go. Bo dem nay chi de GHI CANH BAO trong log tu lan
        // thu 3; no KHONG doi nhip thu lai va KHONG duoc dung lam can cu ket luan gi.
        // Reset ve 0 ngay khi bat tay thanh cong.
        private int _noKeyStreak;
        private const int NO_KEY_STREAK_LIMIT = 3;
        // Hen gio login lai (mot phat). Giu tham chieu de khong bi GC gom truoc khi no ban.
        private Timer _reconnectTimer;
        // Chong lap: chi gui "tao NV" DUNG 1 LAN moi lan ket noi (reset dau DoFullLogin). Tao that bai
        // (trung ten/ten khong hop le) -> server tra lai danh sach rong -> khong tao lai vong lap.
        private volatile bool _createCharAttempted;

        public AccountConfig Config { get; private set; }
        public ClientState State { get; set; }
        // Dang chay mode tu dong (StandMode) - UI dung de hien DANG CANH thay vi ONLINE.
        public bool IsTraining { get { return _activeMode != null; } }
        // Nhan hoat dong hien tai cua mode (vd "Tan sat", "PK am", "Di chuyen") cho cot Trang thai.
        public string ActivityText { get { var m = _activeMode; return m != null ? m.Activity : null; } }
        // O "ONLINE" cua dong status: DA VAO GAME, lam gi cung tinh. Dead PHAI co - luc chet
        // StopAutoSystems() xoa mode va State = Dead suot vong ve lang/hoi sinh; ban cu chi dem InGame
        // nen acc chet roi khoi ca ONLINE lan TRAIN trong khi cot Trang thai van hien "Tàn sát".
        public bool IsOnline { get { var s = State; return s == ClientState.InGame || s == ClientState.Dead; } }
        // O "TRAIN": DANG DANH QUAI o bai that (user chot 2026-09-15). Di chuyen / Doi khu / Di ban do /
        // Dap do / mode cho... chi tinh ONLINE. Ban cu = IsTraining (bat auto la tinh) nen TRAIN == ONLINE.
        public bool IsFarming { get { return State == ClientState.InGame && ActivityText == Auto.AutoModeBase.ACT_DUNG_CANH; } }
        /// <summary>Mode dang chay neu dung kieu T (UI doc trang thai chi tiet). null = khong phai.</summary>
        public T ActiveModeAs<T>() where T : class { return _activeMode as T; }
        public GameStateManager GameState { get { return _state; } }
        public NsoSession Session { get { return _session; } }
        /// <summary>
        /// Gửi LẠI <c>setClientType</c> giữa phiên để đổi mức zoom (<c>graphicsMode</c>) mà
        /// <b>không phải đăng nhập lại</b>.
        ///
        /// Đây là hành vi ĐÃ KIỂM CHỨNG: `SERVER_FACTS.md` §19.1 — gửi lại gói này với
        /// <c>graphicsMode = 1</c> ngay giữa phiên thì 254/255 ảnh quái về trong 40 giây, không đổi
        /// gì khác. Nhờ vậy công cụ dump lấy được nhiều mức zoom trong MỘT lần đăng nhập — quan
        /// trọng vì cùng một tài khoản không thể mở hai phiên song song (phiên sau đá phiên trước),
        /// và nhiều phiên cùng IP thì server khoá tạm (§29).
        ///
        /// ⚠ Chỉ công cụ <c>tools/gfxsync</c> gọi. Bot thường KHÔNG bao giờ gọi.
        /// </summary>
        public void ResendClientType(byte clientType, byte graphicsMode)
        {
            if (Login == null || _session == null) return;
            Login.SendSetClientType(clientType, graphicsMode);
            Log(string.Format("[Login] Gui lai setClientType: clientType={0} graphicsMode={1}",
                clientType, graphicsMode));
        }

        /// <summary>
        /// Số ô trong bảng <c>nj_image</c> SỐNG mà server gửi lúc đăng nhập (0 = chưa nhận được).
        /// Bảng nhúng trong EXE là bản ĐÓNG BĂNG nên con số này mới là sự thật hôm nay.
        /// </summary>
        public int ServerSpriteCount
        {
            get { return _router != null && _router.NotMapHandler != null ? _router.NotMapHandler.NjImageCount : 0; }
        }

        /// <summary>Handler NOT_MAP — để công cụ dump đọc nguyên khối bảng đồ hoạ server gửi.</summary>
        public Controller.NotMapHandler RouterNotMap
        {
            get { return _router != null ? _router.NotMapHandler : null; }
        }

        /// <summary>Số part trong bảng <c>nj_part</c> SỐNG của server (0 = chưa nhận được).</summary>
        public int ServerPartCount
        {
            get { return _router != null && _router.NotMapHandler != null ? _router.NotMapHandler.NjPartCount : 0; }
        }

        public ManualResetEvent MapChangeEvent { get { return _router != null ? _router.MapHandler.MapChangeEvent : null; } }
        public ManualResetEvent NpcMenuEvent { get { return _router != null ? _router.MapHandler.NpcMenuEvent : null; } }
        public System.Collections.Generic.List<string> NpcMenuOptions { get { return _router != null ? _router.MapHandler.NpcMenuOptions : null; } }

        public event Action<string> OnLog;
        public event Action OnStateChanged;
        public event Action<string> OnChatMessage;
        public event Action<int, string> OnPartyInviteReceived;
        public event Action<string> OnPartyJoinRequest;
        public event Action<string, string> OnPartyChat;   // (from, text) - GroupPlayController subscribe

        public NsoClient(AccountConfig config)
        {
            Config = config;
            State = ClientState.Disconnected;
            _state = new GameStateManager();
            Trade = new TradeState();
            InitSession();
        }

        private void InitSession()
        {
            // Dong session + tra handle cua bo cu TRUOC khi thay. Truoc day chi gan de len:
            // socket cu con song thi 2 thread nhan/gui cua no van treo, va 2 ManualResetEvent
            // trong MapHandler cu khong bao gio duoc tra -> ro theo so lan reconnect x so account.
            if (_session != null) { try { _session.Disconnect(); } catch { } }
            if (_router != null) { try { _router.Dispose(); } catch { } }

            _session = new NsoSession();
            _router = new MessageRouter(_state, Trade);
            _router.RawHook = (cmd, m) =>
            {
                var rh = OnRawPacket;
                if (rh != null) rh(this, cmd, m);
            };
            _state.OnDebugLog -= Log;   // _state song qua reconnect (InitSession goi lai) -> tranh sub trung
            _state.OnDebugLog += Log;   // handler -> Client.Log ([EFF]/[Auto] hoc id thuc an)
            _state.OnServerBaoSong -= OnServerBaoSong;   // _state song qua reconnect
            _state.OnServerBaoSong += OnServerBaoSong;

            Login = new LoginService(_session);
            DataSync = new DataSyncService(_session);
            Movement = new MovementService(_session);
            // Server dat lai vi tri (cmd 52 / MAP_INFO / hoi sinh) -> dong bo chot chong trung goi
            // move, dung nhu `cx = cxSend` cua ban goc. Thieu day thi lenh di SUA SAI bi nuot.
            _state.OnServerSetPosition -= OnServerSetPosition;   // _state song qua reconnect
            _state.OnServerSetPosition += OnServerSetPosition;
            Combat = new CombatService(_session);
            // dd15: moi don danh gui di = bang chung "con dang hanh dong" cho bo do im lang.
            Combat.OnAttackQueued = _state.NoteAttackSent;
            // di3: rieng don danh QUAI - SongGia chi duoc do tren loai don nay.
            Combat.OnMobAttackQueued = _state.NoteMobAttackSent;
            Chat = new ChatService(_session);
            Party = new PartyService(_session);
            Items = new ItemService(_session);
            Npc = new NpcService(_session);
            Misc = new MiscService(_session);
            Character = new CharService(_session);
            Friends = new FriendService(_session);
            TradeSvc = new TradeService(_session);

            _session.OnMessageReceived += msg =>
            {
                _state.LastRecvAt = DateTime.UtcNow;   // KeepAliveController.TickDocIm
                try { _router.HandleMessage(msg); }
                catch (Exception ex) { Log("[Error] Handle: " + ex.Message); }
            };
            _session.OnDisconnected += HandleDisconnected;
            _session.OnError += ex => Log("[Error] " + ex.Message);
            _session.OnLog += Log;

            _router.OnLog += Log;
            _router.OnDataVersionReceived += OnDataVersionReceived;
            _router.OnCharListReceived += OnCharListReceived;
            _router.OnCharInfoReceived += OnCharInfoReceived;
            _router.OnMapInfoReceived += OnMapInfoReceived;
            _router.OnDeath += OnDeath;
            _router.OnRevive += OnRevive;
            _router.OnChatReceived += msg =>
            {
                // "TS khi het MP": server bao het MP khi danh -> danh dau de TrainMode tu sat
                // (clone MODGAME Controller: to.equals("Khong du MP de su dung") -> Auto.m = true).
                if (!string.IsNullOrEmpty(msg) && msg.IndexOf("Không đủ MP", StringComparison.Ordinal) >= 0)
                {
                    _state.NotEnoughMpAt = DateTime.UtcNow;
                    // Clone HpMpSync.notEnoughMp() cua MODGAME: server vua noi thang la MP THAT dang
                    // thap hon gia chieu -> ep con so cuc bo xuong ngay. Can vi MyChar.Mp CHI duoc
                    // ghi luc dang nhap (sub -125), sub 115 va luc hoi sinh: trong luc farm no DUNG
                    // YEN, nen nguong "uong binh mana" dang so voi so cu va gan nhu khong bao gio no.
                    var mc0 = _state.MyChar;
                    if (mc0 != null) mc0.Mp = 0;
                }

                // ============ "HP đã đầy" -> ÉP LẠI HP CỤC BỘ VỀ ĐẦY ============
                //
                // ⚠️ SỬA GỐC RỄ MỘT LỖI ĐÃ ĐO ĐƯỢC (2026-09-16), bản NSOLITEPRO KHÔNG có nhánh này.
                //
                // Con số HP cục bộ được TỰ TRỪ mỗi lần trúng đòn ("[Hp] Tu tru tai cho"), còn số
                // THẬT thì chỉ được server gửi khi nó đổi. Khi nhân vật hồi đầy máu lúc đứng yên,
                // server KHÔNG gửi gì cả (với nó thì HP có đổi gì đâu) — nên con số cục bộ kẹt lại
                // ở mức thấp VĨNH VIỄN. Kết quả đo thật: barbigz200 uống bình HP **2 giây một lần
                // không ngừng**, lần nào server cũng đáp "HP đã đầy", đốt sạch bình của người dùng.
                //
                // Bên NSOLITEPRO lỗi này bị che vì nó tàn sát liên tục: mỗi đòn đánh là một gói HP
                // thật về, con số cục bộ được nắn lại ngay. Bot ĐỨNG IM thì không có gì nắn cả.
                //
                // Chính câu trả lời của server là nguồn tin chuẩn xác nhất: nó vừa nói HP đang ĐẦY.
                // Cùng hình mẫu với nhánh "Không đủ MP" ngay trên (clone HpMpSync của MODGAME):
                // server nói thẳng trạng thái thật ⇒ nắn con số cục bộ theo, đừng đoán tiếp.
                if (!string.IsNullOrEmpty(msg) && msg.IndexOf("HP đã đầy", StringComparison.Ordinal) >= 0)
                {
                    var mcHp = _state.MyChar;
                    if (mcHp != null && mcHp.MaxHp > 0 && mcHp.Hp < mcHp.MaxHp)
                    {
                        Log(string.Format("[Hp] Server bao 'HP da day' -> nan so cuc bo {0} ve {1} "
                                        + "(so cuc bo da troi thap, dang lam bot uong binh vo ich)",
                                        mcHp.Hp, mcHp.MaxHp));
                        mcHp.Hp = mcHp.MaxHp;
                    }
                }

                // Server tu choi vao game som (cmd -26): "Ban chi co the vao lai game sau <N> giay nua"
                // (MODGAME Controller c[0]="...vao lai game sau ", c[1]=" giay nua"). Bat so giay -> lan
                // reconnect ke tiep CHO DUNG N+2s (clone MODGAME +2) thay vi backoff dam lai som -> dinh tiep.
                int waitSec;
                if (!string.IsNullOrEmpty(msg) && TryParseReentryWait(msg, out waitSec))
                {
                    // LUAT user chot 2026-09-15 (di8, thay di6): lay so giay game bao + 1s, va HEN NGAY tu luc
                    // nhan tin (UI dem nguoc luon) thay vi doi server tu dong ket noi ~5s sau moi hen.
                    _forcedReconnectDelayMs = (waitSec + 1) * 1000;
                    Log(string.Format("[Reconnect] Server yeu cau cho {0}s truoc khi vao lai -> se cho {1}s", waitSec, waitSec + 1));
                    DatLyDoVaoLai("GAME BẮT CHỜ");   // cot Trang thai (di9)
                    if (State != ClientState.InGame) HenVaoLaiTheoGame();
                }

                var h = OnChatMessage;
                if (h != null) h(string.Format("[{0}] {1}", NameMask.Apply(Config.Username), msg));
            };
            _router.OnPartyChat += (from, text) =>
            {
                var h = OnPartyChat;
                if (h != null) h(from, text);
            };
            // NSOKHODO: chat rieng den -> bo dieu phoi kho (lenh cua Chu kho). Dang ky theo VONG DOI
            // ket noi (router moi moi lan InitSession) nhung ban ra qua su kien cua client - su kien
            // nay song ca doi client nen ben nghe chi can dang ky mot lan.
            _router.OnPrivateChat += (from, text) =>
            {
                var ph = OnPrivateChatReceived;
                if (ph != null) ph(this, from, text);
            };
            _router.OnPartyInviteReceived += (charId, fromName) =>
            {
                Log(string.Format("[Party] Invite from: {0} (id={1})", fromName, charId));
                var h = OnPartyInviteReceived;
                if (h != null) h(charId, fromName);
            };
            _router.OnPartyJoinRequest += name =>
            {
                Log("[Party] Xin vao nhom: " + name);
                var h = OnPartyJoinRequest;
                if (h != null) h(name);
            };
        }

        // ==================== PUBLIC API ====================

        public void Start()
        {
            if (State != ClientState.Disconnected && State != ClientState.Error)
                return;
            _stopping = false;
            _reconnectCount = 0;
            _isReconnect = false;
            _state.Reset();
            BatDauLoginKhiConSlot();
        }

        public void Stop()
        {
            _stopping = true;
            State = ClientState.Disconnected;
            HuyChoSlot();           // dang xep hang login thi bo hang, tra slot cho acc khac
            StopAutoSystems();
            Trade.DatLaiKhiMatKetNoi();
            if (_session != null) _session.Disconnect();
            FireStateChanged();
            Log("[Client] Stopped");
        }

        public void RestartAutoSystems()
        {
            if (State != ClientState.InGame) return;
            Log("[Config] Restarting auto systems...");
            StopAutoSystems();
            StartAutoSystems();
        }

        // ==================== LOGIN FLOW ====================

        private void DoLoginAsync()
        {
            // Prevent multiple login threads
            if (_loginInProgress) return;
            _loginInProgress = true;
            // LUONG RIENG, KHONG dung ThreadPool: DoFullLogin chan toi ~65 giay (4 lan WaitFor
            // 10+15+10+30s). 15 acc cung relogin = 15 worker cua pool bi giam. Pool khoi dau bang
            // dung so core va chi bom them ~1 luong/giay -> KeepAlive (System.Threading.Timer,
            // callback CUNG chay tren pool) cua cac acc DANG KHOE bi tre theo. Do la duong duy nhat
            // acc nay lam hong acc kia trong cung tien trinh (audit 2026-09-03).
            RunOffPool("NSO-Login", () =>
            {
                try { DoFullLogin(); }
                finally
                {
                    _loginInProgress = false;
                    // Tra slot cong dieu tiet NGAY khi pha login ket thuc - du thanh hay bai.
                    // An toan khi acc nay khong he xin slot (login lan dau khong qua cong).
                    NSOKHODO.Fleet.ReloginGate.Exit(this);
                    NSOKHODO.Fleet.LoginGate.Exit(this);
                }
            });
        }

        /// <summary>
        /// Chay mot viec CO CHAN LAU tren luong nen rieng thay vi ThreadPool. Dung cho moi doan
        /// co Thread.Sleep dai / WaitFor: chiem worker cua pool se lam tre viec cua account khac.
        /// </summary>
        private static void RunOffPool(string name, Action work)
        {
            var t = new Thread(() => { try { work(); } catch { } })
            {
                IsBackground = true,
                Name = name
            };
            t.Start();
        }

        private void DoFullLogin()
        {
            if (_stopping) return;

            try
            {
                // Resolve theo TEN (ben voi danh sach doi thu tu), fallback ServerIndex legacy.
                var server = ServerList.Resolve(Config.ServerName, Config.ServerIndex);
                _resolvedServerName = server != null ? server.Name : Config.ServerName;
                if (server == null)
                {
                    Log("[Login] Khong xac dinh duoc may chu");
                    HandleLoginFailed();
                    return;
                }

                State = ClientState.Connecting;
                FireStateChanged();
                Log(string.Format("[Login] Connecting to {0} ({1}:{2}) login={3}...",
                    server.Name, server.Host, server.Port, server.ServerLogin));

                _state.Reset();
                _createCharAttempted = false;   // moi lan ket noi: cho phep tao NV 1 lan neu chua co
                    InitSession();
                _session.ServerLogin = server.ServerLogin;

                // Boc RIENG buoc mo ket noi de dem suc khoe PROXY cho dung: catch chung o cuoi ham
                // om ca loi tang game (sai mat khau, server day ra...), tinh vao proxy la vu oan.
                _noiXongLuc = DateTime.MinValue;
                try
                {
                    _session.Connect(server.Host, server.Port, Config.Proxy);
                    _noiXongLuc = DateTime.UtcNow;   // HandleDisconnected dung de nhan "server chan ngay" (di9)
                    NSOKHODO.Fleet.ProxyHealth.NoteOk(Config.Proxy);
                }
                catch (Exception exConn)
                {
                    NSOKHODO.Fleet.ProxyHealth.NoteFail(Config.Proxy, exConn.Message);
                    DatLyDoVaoLai(string.IsNullOrEmpty(Config.Proxy) ? "NỐI SERVER LỖI" : "PROXY LỖI");
                    throw;
                }

                // Handshake
                State = ClientState.Handshake;
                FireStateChanged();
                Login.SendHandshake();

                if (!WaitFor(() => _session.IsEncryptionReady, 10000))
                {
                    if (DaCoLichVaoLai()) return;   // server dong ket noi, lich vao lai da dat (di6)
                    // Noi TCP duoc nhung server khong he gui khoa -> gan nhu luon la HET SLOT THEO IP
                    // (server cho ~9-10 ket noi/IP; do 2026-09-03: 2 proxy, moi proxy dung dung 9 acc).
                    // Dem chuoi that bai de ScheduleReconnect chuyen sang ngu dai, xem _noKeyStreak.
                    _noKeyStreak++;
                    DatLyDoVaoLai("KHÔNG CÓ KHOÁ");   // cot Trang thai (di9)
                    Log(string.Format("[Login] Timeout: no key (lan {0} lien tiep)", _noKeyStreak));
                    HandleLoginFailed();
                    return;
                }
                _noKeyStreak = 0;
                Log("[Login] Encryption ready");
                Thread.Sleep(500);

                // Login
                State = ClientState.LoggingIn;
                FireStateChanged();
                // clientType=1 (J2ME) / graphicsMode=0 - Y HET NSOLITEPRO dang chay that.
                // ⚠️ KHONG doi hai byte nay sang bo cua ban PC (4 / "2.5.1"): server tra khuon goi
                // khac cho tung phien ban, va cap 1/"1.8.0" la cap DA DUOC KIEM CHUNG tren server
                // nay. Xem docs/SO_SANH_NGUON.md.
                // graphicsMode = 0 con co loi phu: server khong gui du lieu anh -> it goi hon.
                Login.SendSetClientType();
                Thread.Sleep(300);
                Login.SendLogin(Config.Username, Config.Password, server.ServerLogin);
                Log("[Login] Login sent...");

                // Wait data versions
                State = ClientState.DataSync;
                FireStateChanged();
                if (!WaitFor(() => _state.DataSyncVersions != null, 15000))
                {
                    if (DaCoLichVaoLai()) return;   // server dong ket noi, lich vao lai da dat (di6)
                    DatLyDoVaoLai("SERVER KHÔNG ĐÁP");   // cot Trang thai (di9)
                    Log("[Login] No response from server");
                    HandleLoginFailed();
                    return;
                }

                // Wait for DataSync to complete (item templates critical for bag parsing)
                if (!WaitFor(() => _state.IsAllDataSynced, 10000))
                    Log(string.Format("[Login] DataSync partial (items: {0}, skillTpl: {1}, skills: {2})",
                        _state.ItemStore.Count, _state.SkillStore.Count, _state.SkillStore.SkillCount));
                else
                {
                    Log(string.Format("[Login] DataSync done (items: {0}, skillTpl: {1}, skills: {2})",
                        _state.ItemStore.Count, _state.SkillStore.Count, _state.SkillStore.SkillCount));

                    // Nap DU va DAY DU -> cong bo cho cac account sau dung chung (khong tai lai).
                    // Chi cong bo khi that su co du lieu (tranh cong bo ban rong lam acc sau kem data).
                    if (!string.IsNullOrEmpty(_dataKey) && _state.ItemStore.Count > 0
                        && _state.SkillStore.SkillCount > 0)
                    {
                        // Dong bang truoc khi chia se: het ghi -> luot doc bo khoa (600 account
                        // tra skill moi tick se khong tranh chung mot khoa).
                        _state.SkillStore.Freeze();
                        if (SharedGameData.Publish(_dataKey, _state.SnapshotSharedData()))
                            Log("[Login] Da chia se du lieu game cho cac account sau");
                    }
                }

                // ClientOk
                Login.SendClientOk();
                Log("[Login] Waiting for char list...");

                // Wait InGame
                if (!WaitFor(() => State == ClientState.InGame || _stopping, 30000))
                {
                    if (DaCoLichVaoLai()) return;   // server dong ket noi, lich vao lai da dat (di6)
                    DatLyDoVaoLai("KHÔNG VÀO ĐƯỢC GAME");   // cot Trang thai (di9)
                    Log("[Login] Timeout entering game");
                    HandleLoginFailed();
                    return;
                }

                if (!_stopping)
                {
                    _reconnectCount = 0;
                    _forcedReconnectDelayMs = 0; // vao game OK -> bo delay ep con ton (tranh stale)
                    _lyDoVaoLai = null; _lyDoLap = 0;   // vao game OK -> xoa ly do cot Trang thai (di9)
                    Log("[Login] In game!");
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                {
                    Log("[Login] Failed: " + ex.Message);
                    HandleLoginFailed();
                }
            }
        }

        private void HandleLoginFailed()
        {
            if (_stopping) return;
            State = ClientState.Error;
            FireStateChanged();
            try { _session.Disconnect(); } catch { }

            if (Config.AutoReconnect)
                ScheduleReconnect();
        }

        // ==================== EVENT HANDLERS ====================

        private void OnDataVersionReceived()
        {
            // On reconnect: skip DataSync if templates already cached (like game client)
            if (_isReconnect && _state.ItemStore.Count > 0)
            {
                MarkDataSyncDone();
                Log("[Login] DataSync skipped (cached)");
                return;
            }

            // Du lieu tinh (item/skill/mob/map/npc template) GIONG HET nhau giua moi account
            // cung server + cung data version -> dung lai ban account khac da nap thay vi tai
            // lai ~200 KB moi account (600 acc = ~130 MB moi lan mo fleet + RAM trung lap).
            _dataKey = SharedGameData.MakeKey(_resolvedServerName, _state.DataSyncVersions);
            var shared = SharedGameData.Get(_dataKey);
            if (shared != null)
            {
                _state.AttachSharedData(shared);
                MarkDataSyncDone();
                Log("[Login] DataSync dung chung (khong tai lai)");
                return;
            }

            DataSync.SendUpdateItem();
            DataSync.SendUpdateSkill();
            DataSync.SendUpdateMap();
            DataSync.SendUpdateData();
        }

        private void MarkDataSyncDone()
        {
            _state.DataSyncDone[0] = true;
            _state.DataSyncDone[1] = true;
            _state.DataSyncDone[2] = true;
            _state.DataSyncDone[3] = true;
        }

        private void OnCharListReceived(string[] chars, byte[] levels)
        {
            if (chars == null || chars.Length == 0)
            {
                // Chua co NV. Bat "Tu tao nhan vat" (toan cuc) -> tao 1 lan, ten = username tai khoan.
                // Server tu gui lai danh sach NV (cmd -126) sau khi tao -> OnCharListReceived chay lai
                // voi danh sach KHONG rong -> chon NV binh thuong. Chi tao 1 lan/ket noi (_createCharAttempted).
                if (AutoCreateChar.Enabled && !_createCharAttempted)
                {
                    _createCharAttempted = true;
                    string newName = (Config.Username ?? "").Trim();
                    if (string.IsNullOrEmpty(newName))
                    {
                        Log("[Login] Chua co NV nhung username rong -> khong tao");
                        return;
                    }
                    byte g = AutoCreateChar.Gender;
                    Log(string.Format("[Login] Chua co NV -> tu tao '{0}' ({1})", NameMask.Apply(newName), AutoCreateChar.GenderText));
                    Login.SendCreateChar(newName, g, AutoCreateChar.HairFor(g));
                    return;
                }

                Log(_createCharAttempted
                    ? "[Login] Tao NV that bai (ten trung/khong hop le?) - kiem tra thu cong"
                    : "[Login] No characters");
                return;
            }
            string charName = string.IsNullOrEmpty(Config.CharName) ? chars[0] : Config.CharName;

            // Set level from char list for the SELECTED character (not the last one)
            if (levels != null && _state.MyChar != null)
            {
                for (int i = 0; i < chars.Length && i < levels.Length; i++)
                {
                    if (chars[i] == charName)
                    {
                        _state.MyChar.Level = levels[i];
                        break;
                    }
                }
            }

            Log("[Login] Selecting: " + charName);
            Login.SendSelectChar(charName);
        }

        private void OnCharInfoReceived()
        {
            Log(string.Format("[Char] {0} Lv.{1} HP={2}/{3}",
                _state.MyChar.Name, _state.MyChar.Level,
                _state.MyChar.Hp, _state.MyChar.MaxHp));
        }

        private void OnMapInfoReceived()
        {
            System.Threading.Interlocked.Exchange(ref _vaoKhuLucTicks, DateTime.UtcNow.Ticks);
            var map = _state.CurrentMap;
            Log(string.Format("[Map] {0} z={1} ({2},{3})",
                map.MapName, map.ZoneId, _state.MyChar.Cx, _state.MyChar.Cy));

            if (State == ClientState.Dead)
            {
                // ===== MAP_INFO KHONG PHAI GIAY CHUNG SONG (2026-09-08 dd10 - do tren log that) =====
                // Ban cu: thay MAP_INFO la xoa co chet + bia `Hp = MaxHp` + chay tiep auto. Do la
                // SUY DIEN cua ta. Do duoc tren log 02:09-02:12 (15 acc, user xac nhan tan mat 3 nick
                // dang chet o Truong Ookaza):
                //
                //   [02:10:43] [Death] <- cmd -11 (pk=0 wd=516,72)      server: MAY CHET
                //   [02:10:43] [Death] Died
                //   [02:10:43] [Death] <- cmd -11 (pk=0 wd=1620,672)    server: MAY CHET (o lang)
                //   [02:10:43] [Map]   Truong Ookaza z=0 (1620,672)
                //   [02:10:43] [Revive] Returned to town, alive!        <-- TA TU PHONG SONG
                //
                // Server vua noi "may chet" DUNG DONG TRUOC. Lay goi map ke tiep lam giay chung sinh
                // la sai hien nhien. Khoang cach [Death] Died -> "Returned to town": 240 ca 0 giay,
                // 43 ca 1 giay - tuc no LUON xay ra, khong phai ca biet.
                //
                // ⚠️ HAI HAU QUA DAY CHUYEN, ca hai deu do dac duoc:
                // 1. GIET VONG GUI LAI cmd -9. `OnDeath` co vong `RunOffPool("NSO-ReturnTown")` gui
                //    -9 moi 1500ms VO HAN (dung nhu Z.a(true) cua Zang / Auto.a() cua MODGAME), chot
                //    dau vong la `if (State != ClientState.Dead) return;`. Dong tren lat State sang
                //    InGame trong CUNG MOT GIAY nen vong thoat ngay. Bang chung: dong
                //    `[Death] Chua ve duoc lang, da gui N lan` = 0 tren MOI log tu truoc toi nay.
                //    Co che cuu san co chua bao gio chay noi mot vong.
                // 2. CHAN Tick TOI ~115 GIAY. Bot tuong minh song -> TrainMode goi DoGmNavigation ->
                //    di waypoint -> server cmd52 keo ve vi that ra la cai xac -> khong doi duoc map ->
                //    roi vao vong `maxWpRetry = 15` (Navigator.cs:391-419), moi vong 1-5,5s + 0,3s +
                //    WaitOne(3000). Chot thoat cua vong la `_client.State != ClientState.Dead` - ma ta
                //    vua tu phong song, nen no chay du 15 vong. Do duoc: barbigz113 im 58s,
                //    barbigz115 im 91s, barbigz114 im 77s - tat ca deu la MOT lan goi ham nay.
                //    Trong suot khoang do khong the co bat cu chan doan nao vi Tick khong quay.
                //
                // NAY: GIU nguyen State = Dead. Vong gui lai -9 ben duoi chay dung vai tro cua no, va
                // dot chet chi ket thuc bang BANG CHUNG:
                //   - `OnRevive` (cmd -10/88) - server xac nhan hoi sinh; hoac
                //   - `OnServerBaoSong` (HP that > 0 tu sub 115 / cmd 93).
                // Ca ba nguon deu da co san trong code. Neu ca ba cung im thi luoi
                // DEAD_STUCK_RELOGIN_TRIES (40 x 1500ms = 60 giay) se ForceRelogin.
                //
                // NGOAI LE HOP LE DUY NHAT: server DA xac nhan hoi sinh bang -10/88 truoc do
                // (`OnRevive` da xoa co chet), goi map nay chi de lay dung map/vi tri. Thieu nhanh nay
                // thi duong "-10 roi doi map" se ket toi khi luoi 60 giay no - dung loi da tra gia o dd7.
                if (!_state.MyChar.IsDead)
                {
                    Log("[Revive] MAP_INFO sau khi server xac nhan hoi sinh (-10/88) -> chay tiep auto");
                    State = ClientState.InGame;
                    FireStateChanged();
                    _state.HoiSinhLucUtc = DateTime.UtcNow;   // Navigator bo nhip giao cach cho chuyen ve bai
            StartAutoSystems(REVIVE_STARTUP_DELAY_MS);
                    return;
                }

                // Server da doi map cai xac ve diem hoi sinh xong => day chinh la luc no chiu nhan
                // `-9`. Giuc vong gui lai ban ngay thay vi ngu not ~1,5 giay (do duoc: 524/524 lan
                // hoi sinh deu phai doi lenh -9 thu hai).
                _giucVeLang = true;

                // ===== XOA TOA DO CHO CHET (2026-09-08) =====
                // Server VUA dat nhan vat o mot cho khac (diem hoi sinh) - goi MAP_INFO nay da ghi
                // dung toa do do vao MyChar. Nhung `HandleWakeUp`/`HandleRevive` con mot buoc
                // "wdx/wdy -> cx,cy" chep tu client goc (Controller.java:467-476), va no se GHI DE
                // toa do dung bang toa do CHO CHET o map cu.
                //
                // Do duoc tren log 12:48:27 (acc tungnv): server bao dang o (1620,672), bot lai di
                // toi NPC 25 tu (1356,576) => server chan, keo ve, lo nhip, NPC25 fail, cho 2 giay
                // roi tick sau moi lam lai => MOI LAN CHET mat ~3 giay.
                //
                // wdx/wdy cua client goc la cho HOI SINH TAI CHO (cung map, khong co MAP_INFO nao
                // ve). Khi da co MAP_INFO tuc la da doi cho => bo wd di, giu toa do server vua bao.
                if (_state.DeathX != 0 || _state.DeathY != 0)
                {
                    Log(string.Format("[Revive] Server doi cho ve ({0},{1}) -> bo toa do cho chet ({2},{3})",
                        _state.MyChar.Cx, _state.MyChar.Cy, _state.DeathX, _state.DeathY));
                    _state.DeathX = 0;
                    _state.DeathY = 0;
                }
                Log(string.Format("[Revive] MAP_INFO ({0}) sau khi chet - CHUA phai da song. Gui lai -9 NGAY, cho -10/88 hoac HP that > 0.",
                    map.MapName));
                return;
            }

            bool firstTime = (State != ClientState.InGame);
            State = ClientState.InGame;
            FireStateChanged();

            if (firstTime)
            {
                // Mau so cua ti le rot: dem O DAY chu khong o luc gui goi login, vi "gui login"
                // khong co nghia la vao duoc. Dat trong `firstTime` de doi map/khu trong cung mot
                // phien khong bi dem thanh nhieu lan vao game.
                NSOKHODO.Fleet.DisconnectStats.GhiVaoGame(Config.Username);
                if (Config.TargetMapId < 0)
                {
                    Config.TargetMapId = map.MapId;
                    Config.TargetZoneId = map.ZoneId;
                }
                StartAutoSystems();
            }
        }

        /// <summary>
        /// Ta TU KET LUAN nhan vat da chet vi server giu HP = 0 ma khong he gui `cmd -11`.
        /// Goi tu <see cref="KeepAliveController"/> (timer rieng - KHONG duoc dat trong TrainMode.Tick,
        /// ham do bi chan toi ~115 giay trong DoGmNavigation dung luc can do nhat).
        ///
        /// Chi di vao LUONG CHET SAN CO, khong che duong moi: dat co chet -> <see cref="OnDeath"/> ->
        /// <see cref="BatDauVongVeLang"/> gui `cmd -9` moi 1500ms cho toi khi server xac nhan song.
        /// Do la duong da do duoc: 63/66 ca chet binh thuong hoi phuc trong 2 giay.
        /// </summary>
        public void NoteExhausted(int serverHp, int maxHp, int believed, int giuGiay)
        {
            if (_stopping) return;
            if (State != ClientState.InGame) return;   // dang trong luong chet roi thi khong lam lai
            Log(string.Format(
                "[KietSuc] >>> VAO LUONG CHET: server bao {0}/{1} lien tuc {2}s (bot dang hien {3}). "
                + "Khong co cmd -11 nen luong chet chua tung chay - kich hoat tay.",
                serverHp, maxHp, giuGiay, believed));
            var mc = _state.MyChar;
            if (mc != null) { mc.IsDead = true; mc.Hp = 0; }
            OnDeath();
        }

        private void OnDeath()
        {
            if (State == ClientState.Dead) return;

            // Cho mode active kip snapshot trang thai TRUOC khi MyChar.Cx/Cy bi overwrite
            // (vd TrainMode luu diem chet de quay lai). Goi truoc StopAutoSystems.
            if (_activeMode != null)
            {
                try { _activeMode.OnBeforeDeath(); }
                catch (Exception ex) { Log("[Death] OnBeforeDeath loi: " + ex.Message); }
            }

            _giucVeLang = false;   // co cua lan chet TRUOC, khong duoc de no bung sang lan nay
            State = ClientState.Dead;
            FireStateChanged();
            StopAutoSystems();
            int deathSeq = ++_deathSeq;   // moc de luong ve lang cu tu tat khi da co lan chet moi
            Log("[Death] Died");

            if (_stopping) return;

            // ===== HOI SINH TAI CHO BANG LUONG (clone MODGAME `hsl`, Auto.java:224-237) =====
            // Gui cmd -10 wakeUpFromDead thay vi -9 ve lang -> char dung day NGAY TAI BAI, bo han
            // pha di lai nhieu map. Toi da 4 lan/1 lan chet, cach nhau 1000ms; het luot / het luong
            // thi roi ve luong cu ben duoi.
            if (TryReviveInPlace()) return;

            // Ve lang (cmd -9). Ban goc gui NGAY khi nhan tin chet - ta cung vay.
            // TRUOC DAY: Thread.Sleep(200) roi moi gui MOT lan duy nhat. Hai cai gia:
            //   - 200ms chet trong MOI lan chet (do duoc: chet 1 lan/~90s voi tungnv, den 1 lan/~18s
            //     voi acc tu sat het MP - tuc toi 1% thoi gian treo may chi de ngu);
            //   - va van khong an toan: goi -9 do bi server bo qua thi KHONG AI gui lai, acc nam
            //     xac vinh vien (SO_SANH_TRAIN.md §8: ta khong he co watchdog "HP<=0 keo dai").
            // Nay: gui ngay, roi POLL 50ms de thoat som, chua song thi gui lai - KHONG GIOI HAN.
            //
            // 2026-09-06: BO HAN MUC 4 LAN (hoc ZangVPS). Truoc day het 4 luot la luong `return`,
            // khong ai vuc acc day nua. Do duoc tren log that: `barbigz122` chet 20:50:56, gui lai
            // 4 lan trong 6 giay roi buong; server tra loi o giay thu 8 (`cmd 92 len=7172`) thi
            // khong con ai nghe. Acc im 4,5 phut (12 dong log, trong khi acc khac ~300 dong) ma
            // KHONG he sinh ra `Connection closed` - nen moi thong ke dut ket noi deu bao "binh
            // thuong" trong khi acc da chet lam sang.
            //
            // ZangVPS khong co khai niem "het luot": `Z.a(1)` nam o DAU MOI TICK cua moi task
            // (`cl_0.i()`, `bk_0.i()`, `bL.java:199-201`, ...), nen he con chet la tick sau lai gui
            // tiep, ~2,1 s/lan, vinh vien. Khong ton tai bien dem nao trong 247 file.
            // Ta giu nhip 1500ms (nhanh hon Zang mot chut) va dung dieu kien thoat san co:
            // song lai / chet lan khac / dang tat - deu la trang thai CHAC CHAN se toi, nen vong
            // lap nay khong the treo vinh vien.
            BatDauVongVeLang(deathSeq);
        }

        /// <summary>
        /// Vong gui lai <c>cmd -9</c> cho toi khi song lai. Tach RIENG khoi <see cref="OnDeath"/> de
        /// nhanh "hoi sinh tai cho" (<see cref="TryReviveInPlace"/>) cung goi duoc khi het luot.
        ///
        /// ⚠️ LO HONG DA BIT (2026-09-08 dd10): truoc day <c>TryReviveInPlace</c> het 4 luot `-10`
        /// thi gui DUNG MOT lan `-9` roi buong. Con duoc vi MAP_INFO tu phong song se vot. Nay
        /// MAP_INFO khong phong song nua, nen thieu vong nay thi bat "Hoi sinh tai cho" = nam xac
        /// den khi ai do tat bot. Config hien tai `ReviveInPlace=0` o ca 50 acc nen chua ai dam phai,
        /// nhung do la mot cai bay dat san.
        /// </summary>
        private void BatDauVongVeLang(int deathSeq)
        {
            RunOffPool("NSO-ReturnTown", () =>
            {
                // Chi cho phep GIUC MOT LAN trong mot dot chet. Server co the gui nhieu MAP_INFO
                // trong luc con chet; khong chan thi moi goi lai cat mot nhip cho va vong nay bien
                // thanh may ban -9 lien tuc - dung thu da lam mat acc dem 2026-09-08.
                bool daGiuc = false;
                for (int lan = 1; ; lan++)
                {
                    // Da song lai / da chet lan khac (luong moi lo) / dang tat -> nhuong.
                    if (_stopping || State != ClientState.Dead || _deathSeq != deathSeq) return;
                    Movement.SendReturnTown(); // cmd=-9
                    // Ghi ngay o lan DAU: truoc day dong log som nhat la "Chua ve duoc lang" - chi ra
                    // sau 1500ms va CHI khi chua song, nen ca truong hop chay tot thi log khong he cho
                    // thay lenh ve lang da duoc gui. Tu day moi lan chet deu co mot dong dung o thoi diem gui.
                    if (lan == 1)
                        Log("[Death] >>> GUI LENH VE LANG (cmd -9) ngay khi ghi nhan chet");
                    // Server tra MAP_INFO -> OnMapInfoReceived xu ly hoi sinh -> State khac Dead.
                    bool giuc = false;
                    for (int cho = 0; cho < RETURN_TOWN_RETRY_MS; cho += RETURN_TOWN_POLL_MS)
                    {
                        if (_stopping || State != ClientState.Dead || _deathSeq != deathSeq) return;
                        // Server vua dat xac ve diem hoi sinh -> gui lai NGAY (xem _giucVeLang).
                        if (_giucVeLang && !daGiuc) { _giucVeLang = false; daGiuc = true; giuc = true; break; }
                        Thread.Sleep(RETURN_TOWN_POLL_MS);
                    }
                    if (giuc) continue;   // khong ghi log "chua ve duoc lang": day la nhip binh thuong
                    // Thua dan: acc ket ca gio se sinh hang nghin dong neu ghi moi lan. Bon lan dau
                    // ghi day du (do la khoang thuong gap), sau do 20 lan mot (~30 giay).
                    if (lan <= 4 || lan % 20 == 0)
                        Log(string.Format("[Death] Chua ve duoc lang, da gui {0} lan - van dang thu", lan));

                    // ===== LUOI AN TOAN "CHET KHONG HOI SINH -> VAO LAI" (dd6) DA BO - user chot 2026-09-15 di3 =====
                    // Luat: bot CHI duoc tu cat ket noi khi tan sat DANH QUAI ma server khong phan hoi
                    // (KeepAliveController.TickSongGia). Chet thi khong danh gi -> khong ap dung.
                    // Qua nguong 40 x 1500ms = 60 giay: CHI log MOT lan, vong nay van gui -9 moi 1,5s.
                    // GIA (ly do dd6 dat luoi nay): OnDeath() da tat _keepAlive; server khong bao gio tra
                    // HP > 0 thi acc nam xac vinh vien cho toi khi user tat/bat lai.
                    if (lan == DEAD_STUCK_RELOGIN_TRIES)
                    {
                        Log(string.Format("[Death] Da gui {0} lan ve lang trong {1}s ma server VAN chua bao HP > 0 - KHONG tu vao lai (di3), van gui tiep",
                            lan, lan * RETURN_TOWN_RETRY_MS / 1000));
                    }
                }
            });
        }

        /// <summary>
        /// SERVER bao HP that &gt; 0 -> ket thuc dot chet. Day la tin hieu song DUY NHAT, clone
        /// <c>bU &gt; 0</c> cua ZangVPS (<c>Z.b()</c>, Z.java:3564). Khac han ban cu von suy dien
        /// tu MAP_INFO roi tu bia day mau.
        /// </summary>
        private void OnServerBaoSong(int hp)
        {
            if (State != ClientState.Dead) return;
            Log(string.Format("[Revive] SERVER bao HP = {0} -> song THAT, chay tiep auto", hp));
            _state.MyChar.IsDead = false;
            State = ClientState.InGame;
            FireStateChanged();
            _state.HoiSinhLucUtc = DateTime.UtcNow;   // Navigator bo nhip giao cach cho chuyen ve bai
            StartAutoSystems(REVIVE_STARTUP_DELAY_MS);
        }

        /// <summary>Moc dem so lan chet - de luong "ve lang" cua lan chet CU tu tat khi da co lan moi.</summary>
        private volatile int _deathSeq;

        /// <summary>
        /// Server VUA dat cai xac ve diem hoi sinh (MAP_INFO ve trong luc con chet) -> vong gui lai
        /// <c>cmd -9</c> phai ban NGAY, khong ngu het nhip 1500 ms.
        ///
        /// VI SAO: do tren log that 2026-09-08, <b>524/524</b> lan hoi sinh chi xay ra SAU lenh
        /// <c>-9</c> thu HAI (459 ca mat 2 giay, 60 ca mat 1 giay). Lenh <c>-9</c> ban ngay luc ghi
        /// nhan chet LUON bi server bo qua - no chi nhan sau khi da xu ly xong cai chet va doi map
        /// nhan vat ve Truong. Ma "da doi map xong" chinh la goi MAP_INFO nay. Cho het 1500 ms nua
        /// la cho thua: moi lan chet mat khong 1,5-2 giay dung im tai diem hoi sinh.
        /// </summary>
        private volatile bool _giucVeLang;

        // RETURN_TOWN_MAX_TRIES (=4) DA BO 2026-09-06: xem chu thich dai o vong gui lai cmd -9.
        // DUNG THEM LAI han muc - het luot la acc nam xac vinh vien, khong sinh log dut ket noi.
        private const int RETURN_TOWN_RETRY_MS = 1500; // cho bao lau roi gui lai cmd -9
        private const int RETURN_TOWN_POLL_MS = 50;    // nhip poll de thoat SOM (ban goc cung 50ms)
        /// <summary>Gui bao nhieu lan ve lang ma van chua song thi vao lai - xem luoi an toan.</summary>
        private const int DEAD_STUCK_RELOGIN_TRIES = 40;   // 40 x 1500ms = 60 giay

        /// <summary>
        /// Delay truoc tick dau tien khi mode duoc TAO LAI sau hoi sinh (khac lan dau vao game).
        /// 0 = tick ngay, giong NSOTRUNGDUC (vong auto cua no khong he bi huy khi chet nen tick ke
        /// tiep - 100ms sau - da di ve bai). Khong con gi de cho: MAP_INFO da ve truoc khi nhanh
        /// nay chay, con skill/hanh trang thi nam trong GameStateManager suot phien, khong mat.
        /// </summary>
        private const int REVIVE_STARTUP_DELAY_MS = 0;

        // ---- Hoi sinh tai cho: dem so lan da thu trong MOT lan chet (clone Auto.hslTries) ----
        private volatile int _hslTries;
        private volatile bool _hslActive;         // dang chay vong gui -10 (khong phai hoi sinh o lang)
        private const int HSL_MAX_TRIES = 4;      // Auto.java:225
        private const int HSL_INTERVAL_MS = 1000; // Auto.java:226

        /// <summary>
        /// Bat dau vong gui cmd -10. Tra TRUE neu da nhan viec (khong duoc gui -9 nua).
        /// </summary>
        private bool TryReviveInPlace()
        {
            if (!Config.Train.ReviveInPlace) return false;
            // Clone Auto.java:224 (`!AutoNangCap.bat`): CHUYEN DAP DO CAN CHET de ve lang.
            // Hoi sinh tai cho se dung nhan vat day NGAY TAI BAI -> khong bao gio toi duoc Tho
            // ren, nhin y het "dap do khong chay". Ban goc gac bang chinh CONG TAC dap (khong
            // phai co chuyen) - giu nguyen nhu vay.
            if (Config.Train.DapDo) return false;
            // CUNG LY DO, cho CHUYEN DI BAN cua Loc do (docs/features/LOC_DO.md §M): chuyen do cung
            // "can chet de ve lang". Khac dap do o cho no khong co cong tac bat/tat thuong truc -
            // chuyen chi thinh thoang mo - nen gac bang CO CHUYEN DANG CHAY chu khong phai bang
            // cong tac. Thieu dong nay thi bat "Hoi sinh tai cho" = nhan vat dung day NGAY TAI BAI,
            // khong bao gio toi duoc lang, chuyen chay het han 90 s roi bo: mat mot mang + mot it
            // luong ma khong ban duoc gi, va nhin y het "loc do khong chay".
            if (_state.BanDoTripDangChay()) return false;
            var mc = _state.MyChar;
            if (mc == null || mc.Luong <= 0)
            {
                if (mc != null && mc.Luong <= 0)
                    Log("[Revive] Het luong -> ve lang");
                return false;
            }

            _hslTries = 0;
            _hslActive = true;
            // Luong rieng: vong nay ngu toi 4 giay. 30 acc chet ~20 giay/lan = ~6 worker cua pool
            // bi giu thuong truc chi de ngu.
            RunOffPool("NSO-Revive", () =>
            {
                while (!_stopping && State == ClientState.Dead && _hslTries < HSL_MAX_TRIES)
                {
                    var c = _state.MyChar;
                    if (c == null || c.Luong <= 0) break;
                    // MODGAME xoa co "het MP" NGAY TRUOC khi gui (Auto.java:228 `Auto.m = false`):
                    // khong thi char vua dung day day MP da tu sat lai vi co cu con han 3 giay.
                    _state.NotEnoughMpAt = DateTime.MinValue;
                    Movement.SendWakeUp();      // cmd=-10
                    _hslTries++;
                    Log(string.Format("[Revive] Hoi sinh tai cho (lan {0}/{1}, luong={2})",
                        _hslTries, HSL_MAX_TRIES, c.Luong));
                    Thread.Sleep(HSL_INTERVAL_MS);
                }
                // Het luot ma van chua song -> ve lang nhu cu
                if (!_stopping && State == ClientState.Dead)
                {
                    _hslActive = false;   // het luot hoi sinh tai cho - tu day la luong ve lang
                    Log("[Revive] Hoi sinh tai cho that bai -> ve lang");
                    // 2026-09-08 dd10: TRUOC DAY chi gui DUNG MOT lan -9 roi buong - con song duoc
                    // vi MAP_INFO tu phong song se vot. Nay MAP_INFO khong phong song nua, nen phai
                    // vao dung vong gui lai vo han + luoi ForceRelogin 60s nhu nhanh chet thong thuong.
                    BatDauVongVeLang(_deathSeq);
                }
            });
            return true;
        }

        /// <summary>
        /// cmd -10 (WAKE_UP) / cmd 88 (REVIVE) = server XAC NHAN hoi sinh. Day la tin hieu song
        /// co thuc quyen cao nhat - cao hon ca goi HP dinh ky.
        ///
        /// CLONE NGUYEN VAN client goc 2.5.1: ca hai case deu goi NGAY <c>liveFromDead()</c>
        /// (case -10 tai Controller.cs:3062-3071 · case 88 tai :712-733), va ham do
        /// (Char.cs:12382-12390) lam dung ba viec:
        ///     cHP = cMaxHP;  cMP = cMaxMP;  changeStatusStand();   // statusMe = 1 (song)
        /// KHONG cho MAP_INFO, khong dieu kien nao het.
        ///
        /// ⚠️ LOI DA TRA GIA (2026-09-07 dd7): ban cu chi hoan tat hoi sinh khi <c>_hslActive</c>
        /// (tuc chi khi CHINH TA gui -10), con lai thi ghi "waiting for MAP_INFO..." roi cho.
        /// Nhung hoi sinh TAI CHO khong doi map nen MAP_INFO KHONG BAO GIO den. Do tren log 23:18:49:
        /// server tra loi lenh ve lang (-9) bang chinh <c>cmd -10</c>, va CA 15 acc ket o
        /// <c>State = Dead</c> vinh vien - toan bo nick hien "da chet". Truoc dd6 loi nay bi che
        /// khuat vi MAP_INFO tu phong song; khi MAP_INFO thoi lam viec do thi no lo ra ngay.
        /// </summary>
        private void OnRevive()
        {
            if (State != ClientState.Dead) return;

            _hslTries = HSL_MAX_TRIES;   // dung vong gui -10 dang chay
            _hslActive = false;

            // liveFromDead(): ba dong duoi la nguyen van ban goc.
            _state.MyChar.IsDead = false;
            _state.MyChar.Hp = _state.MyChar.MaxHp;
            _state.MyChar.Mp = _state.MyChar.MaxMp;
            // Server VUA XAC NHAN ta song (cmd -10/88). Ghi moc - KHONG gan ServerHp = MaxHp (bia so).
            // Thieu dong nay thi `ServerHp` con dong bang o so 0 cua goi bao chet vua roi, va moi chot
            // doc no se bao dong nham suot phan doi con lai cua acc. Xem CharacterState.ServerAliveAt.
            _state.MyChar.ServerAliveAt = DateTime.UtcNow;

            State = ClientState.InGame;
            FireStateChanged();
            Log("[Revive] Server xac nhan hoi sinh (cmd -10/88) -> song lai, chay tiep auto");
            _state.HoiSinhLucUtc = DateTime.UtcNow;   // Navigator bo nhip giao cach cho chuyen ve bai
            StartAutoSystems(REVIVE_STARTUP_DELAY_MS);
        }

        // ==================================================================================
        // ===== IM LANG BA DUONG O MAP TRAIN -> VE LANG (2026-09-08 dd15) ==================
        // ==================================================================================
        /// <summary>
        /// Gui `cmd -9` khi nhan vat da im ca ba duong o map train. Goi tu
        /// <see cref="KeepAliveController"/> (timer rieng, khong the bi vong lap nao chan).
        ///
        /// VI SAO LA `cmd -9` CHU KHONG PHAI `cmd 93`: lenh nay lam CA HAI viec trong mot goi -
        /// vua hoi vua chua. Do dem 2026-09-08:
        ///   `-9` DUOC chap nhan  : 4/4 lan deu la chet that (2 ca co `cmd -11`, 2 ca CHET CAM -
        ///                          barbigz107 im 32s, barbigz102 im 36s, khong he co goi bao chet)
        ///   `-9` bi tu choi      : 606/606 lan server chi dap `cmd -10`, khong co gi xay ra
        /// Tuc chinh viec no AN da la bang chung da chet, va neu an thi nhan vat ve lang luon.
        ///
        /// ⚠️ PHAI DI KEM chot chan bia so o <see cref="Controller.PlayerHandler.HandleWakeUp"/>:
        /// `cmd -10` dap lai `-9` luc con song tung tu dat `Hp = MaxHp` - chinh la cach "HP ao"
        /// duoc CHE RA. Hai thu nay tach ra la vo.
        /// </summary>
        /// <summary>
        /// "Song gia": ta VAN dang danh nhung server da ngung dap lai. Day la dau hieu that cua cai
        /// xac - xem <see cref="GameStateManager.LastAttackAckAt"/>. Goi tu
        /// <see cref="KeepAliveController"/>.
        /// </summary>
        public void VeLangViSongGia(int cachGiay)
        {
            if (_stopping || State != ClientState.InGame) return;
            DateTime now = DateTime.UtcNow;
            if (_songGiaGuiLuc != DateTime.MinValue
                && (now - _songGiaGuiLuc).TotalMilliseconds < IM_LANG_GAP_MS) return;
            _songGiaGuiLuc = now;
            _songGiaTongLan++;

            var c = _state.MyChar;
            Log(string.Format(
                "[SongGia] Van dang danh nhung server KHONG dap lai {0}s -> GUI cmd -9. "
                + "(bot dang hien {1}/{2}, server bao lan cuoi {3}, wd={4},{5}) Lan thu {6} tu dau phien.",
                cachGiay, c != null ? c.Hp : -1, c != null ? c.MaxHp : -1,
                c != null ? c.ServerHp : -1, _state.DeathX, _state.DeathY, _songGiaTongLan));

            try { Movement.SendReturnTown(); }   // cmd -9
            catch (Exception ex) { Log("[SongGia] Gui -9 loi: " + ex.Message); }

            // ===== NAC LEO THANG (2026-09-10 ty4): LECH LUONG DOC -> PHAI KET NOI LAI =====
            // Vi sao can: do tren dan that 2026-09-10, barbigz112 gui `cmd -9` DUNG 50 LAN trong
            // ~6 phut ma khong doi duoc gi. Ly do: khoa XOR chieu DOC da lech (NsoEncryption._readPos
            // nhich mot nac moi byte giai ma; lech mot byte la lech VINH VIEN, khong co duong ve).
            // Tu do bot khong doc noi gi nua - nen "server khong dap lai" la THAT voi bot, du server
            // van tra loi binh thuong. Bang chung: bot hien map 41 trong khi nhan vat that dang o
            // Truong Ookaza (user nhin bang acc khac), va cac goi doc ra co cmd la kem len khong lo
            // (38.917 / 43.939 byte). Chieu GUI dung `_writePos` rieng nen van tot - vi vay nhan vat
            // van song trong game, chi co bot la mu. Gui `-9` them bao nhieu lan cung vo nghia.
            //
            // Vi sao KHONG dung o day duoc: acc lech luong nam chet cho toi khi user tu nhin thay.
            // Do duoc: 6 phut voi barbigz112, 13 lan dut voi barbigz117.
            //
            // ⚠️ VI SAO NGUONG NAY KHONG THE NO OAN VOI ACC LANH - doc ky truoc khi ha xuong:
            // `TickSongGia` (KeepAliveController) da gac san 5 dieu kien truoc khi goi vao day:
            // dang InGame, con song, khong doi map, khong o dang auto CO QUYEN dung im, va
            // **vua danh trong 5 giay gan day**. Tuc day la trang thai "TA DANG DANH ma 90 giay
            // KHONG mot tin dap lai nao". Acc lanh nhan tin dap lai lien tuc (do duoc: bo dem server
            // cua 8 acc khoe deu bam sat). Acc dung yen hop le da bi loc tu vong ngoai.
            // Nguong 90s = 4,5 lan nguong canh bao 20s cua SG_NGUONG_MS.
            if (cachGiay >= SONGGIA_RELOGIN_SEC)
            {
                string vetGoi = "(khong lay duoc)";
                try { if (_session != null) vetGoi = _session.MoTaGoiGanNhat(); }
                catch { }
                // In vet TRUOC khi ngat: sau ForceRelogin la doi tuong ket noi bi vut, mat sach.
                // Day la thu DUY NHAT giup tim ra goi gay lech o phien sau - hai nghi can dang treo
                // la cmd -31 (SPECIAL_LEN) va cmd -27 (HANDSHAKE giua phien). Xem TRUC_Y.md.
                Log("[SongGia] VET GOI GAN NHAT (cu -> moi, cmd/len, * = goi lon -32): " + vetGoi);
                ForceRelogin(string.Format(
                    "song gia {0}s - da gui -9 {1} lan khong an, nghi LECH KHOA XOR chieu doc "
                    + "(khong the tu khoi phuc) -> ket noi lai",
                    cachGiay, _songGiaTongLan));
            }
        }

        /// <summary>
        /// "Song gia" keo dai qua nguong nay (giay) thi thoi gui `cmd -9`, chuyen sang KET NOI LAI.
        /// Xem khoi ghi chu trong <see cref="VeLangViSongGia"/> ve ly do khong the no oan.
        /// </summary>
        private const int SONGGIA_RELOGIN_SEC = 90;

        private DateTime _songGiaGuiLuc = DateTime.MinValue;
        private int _songGiaTongLan;

        /// <summary>Dang co mode chay khong (<c>_activeMode != null</c>).</summary>
        public bool CoAutoDangChay { get { return _activeMode != null; } }

        /// <summary>
        /// LUON <c>false</c> o ban nay - va do la co y, khong phai chua lam.
        ///
        /// <para>Ben NSOLITEPRO day la "dang TAN SAT that", dieu kien CAN de bo do <b>song gia</b>
        /// cua <see cref="Auto.KeepAliveController"/> duoc phep tu cat ket noi. Bo do do hoi:
        /// "ta vua danh QUAI ma server khong he phan hoi -> ket noi la xac". Bot nay <b>khong bao gio
        /// danh quai</b> (chi tu danh chinh minh de chong kick idle, xem
        /// <see cref="Auto.AutoModeBase.Heartbeat"/>), nen phep do do khong co dau vao va se ket luan
        /// bua. Tra <c>false</c> = tat han no.</para>
        /// </summary>
        public bool DangTanSat { get { return false; } }

        /// <summary>
        /// LUON <c>true</c> o ban nay - cung la co y.
        ///
        /// <para>Ben NSOLITEPRO, dung im lau = nghi da chet, nen bo do "im lang ba duong" ban
        /// <c>cmd -9</c> (ve lang) moi 12 giay. Bot nay dung im la <b>dung viec</b>: no duoc giao
        /// dung mot nhiem vu la cam chot mot khu va nhin. De <c>false</c> thi nhan vat bi keo ve
        /// lang lien tuc va khong con canh duoc khu nao - log that ben kia: mot acc an 8 phat
        /// <c>-9</c> trong 94 giay.</para>
        ///
        /// <para>Doi lai, viec chong kick idle KHONG con duoc phep dua vao bo do do; no do
        /// <see cref="Auto.AutoModeBase.Heartbeat"/> (tu danh chinh minh 60s/lan) + goi giu ket noi
        /// cua KeepAlive dam nhan.</para>
        /// </summary>
        public bool DungImHopLe { get { return true; } }

        /// <summary>
        /// Header cac goi NHAN gan nhat (cu -&gt; moi, <c>cmd/len</c>, * = goi lon -32) - CHI de chan doan.
        /// Xem <see cref="NsoConnection.MoTaGoiGanNhat"/>. Dung cho Watchdog dong bang (di4).
        /// </summary>
        public string VetGoiGanNhat()
        {
            try { var s = _session; return s != null ? s.MoTaGoiGanNhat() : "(chua ket noi)"; }
            catch { return "(khong lay duoc)"; }
        }

        /// <summary>Doi han doc cua ket noi hien tai - xem KeepAliveController.TickDocIm. True = vua doi.</summary>
        public bool DatHanDoc(int ms)
        {
            var s = _session;
            return s != null && s.SetReadTimeout(ms);
        }

        public void VeLangViImLang(int imGiay, int mapId)
        {
            if (_stopping || State != ClientState.InGame) return;
            DateTime now = DateTime.UtcNow;
            if (_imLangGuiLuc != DateTime.MinValue
                && (now - _imLangGuiLuc).TotalMilliseconds < IM_LANG_GAP_MS) return;
            _imLangGuiLuc = now;
            _imLangTongLan++;

            var c = _state.MyChar;
            Log(string.Format(
                "[ImLang] Map train {0}: {1}s khong nhan don, khong danh, khong vao map -> GUI cmd -9. "
                + "(bot dang hien {2}/{3}, server bao lan cuoi {4}, wd={5},{6}) Lan thu {7} tu dau phien.",
                mapId, imGiay, c != null ? c.Hp : -1, c != null ? c.MaxHp : -1,
                c != null ? c.ServerHp : -1, _state.DeathX, _state.DeathY, _imLangTongLan));

            try { Movement.SendReturnTown(); }   // cmd -9
            catch (Exception ex) { Log("[ImLang] Gui -9 loi: " + ex.Message); }
        }

        private DateTime _imLangGuiLuc = DateTime.MinValue;
        private int _imLangTongLan;
        private const int IM_LANG_GAP_MS = 10000;   // ham: khong gui `-9` day hon nhip nay

        /// <summary>Server dat lai vi tri -> MovementService coi do la "goi da gui gan nhat".</summary>
        private void OnServerSetPosition(short x, short y)
        {
            var mv = Movement;
            if (mv != null) mv.SyncLastSent(x, y);
        }

        private void HandleDisconnected()
        {
            if (_stopping || State == ClientState.Disconnected || State == ClientState.Reconnecting) return;

            // PHAN LOAI (di9, hoc ZangVPS H.java:108-112 - loi trong 500ms dau = "connect hong", sau do = "rot
            // that"): server dong NGAY sau khi vua noi xong = bi chan tu dau (khoa tam §29, het slot...), khac
            // han dang choi thi rot. Chi doi CHU LOG + cot Trang thai, KHONG doi hanh vi vao lai.
            var buocTruoc = State;
            DateTime noiXong = _noiXongLuc;
            int msSauNoi = noiXong == DateTime.MinValue ? -1 : (int)(DateTime.UtcNow - noiXong).TotalMilliseconds;
            string vi;
            if (buocTruoc == ClientState.InGame || buocTruoc == ClientState.Dead)
            {
                DatLyDoVaoLai("MẤT KẾT NỐI");
                vi = " - dang choi thi rot";
                // DEM: day moi la "mat ket noi" theo nghia nguoi dung hieu - dang o trong game thi rot.
                // Hai nhanh duoi (server chan / loi giua chung dang nhap) la KHONG VAO DUOC, dem rieng.
                NSOKHODO.Fleet.DisconnectStats.Ghi(Config.Username, NSOKHODO.Fleet.DisconnectStats.Loai.RotKhiDangChoi);
            }
            else if (msSauNoi >= 0 && msSauNoi < SERVER_CHAN_MS)
            {
                DatLyDoVaoLai("SERVER CHẶN");
                vi = string.Format(" - server dong NGAY {0}ms sau khi vua noi (chan tu dau)", msSauNoi);
                NSOKHODO.Fleet.DisconnectStats.Ghi(Config.Username, NSOKHODO.Fleet.DisconnectStats.Loai.ServerChan);
            }
            else
            {
                DatLyDoVaoLai("MẤT KẾT NỐI");
                vi = string.Format(" - giua luc dang nhap (buoc {0}, {1}ms sau khi noi)", buocTruoc, msSauNoi);
                NSOKHODO.Fleet.DisconnectStats.Ghi(Config.Username, NSOKHODO.Fleet.DisconnectStats.Loai.LoiDangNhap);
            }

            StopAutoSystems();
            Trade.DatLaiKhiMatKetNoi();   // server tu huy giao dich khi mot ben thoat (test tay T6)
            State = ClientState.Reconnecting;
            FireStateChanged();
            Log("[Client] Disconnected" + vi);   // GIU tien to "[Client] Disconnected" - cac script doc log dang grep no

            if (Config.AutoReconnect)
                ScheduleReconnect();
        }

        /// <summary>
        /// TU CAT ket noi va vao lai - duong thoat ket cuoi cung khi acc khong con cuu duoc tai cho.
        ///
        /// Vi sao can (do tren log that 2026-09-07): acc bi DONG BANG VI TRI dung chet cung tai diem
        /// hoi sinh, moi lenh di deu bi server keo ve (barbigz102: 4.828 lan / 16 phut). Cu "nhay tai
        /// cho" cua watchdog cuu duoc 0/8 acc - barbigz104 duoc nhich 15 lan trong 16 phut van ket.
        /// Thu DUY NHAT chua duoc, do thang tren log: relogin - barbigz104 vao lai luc 19:47:24 thi
        /// 2 giay sau da doi map binh thuong. ZangVPS cung chi con `ax.clears()` (reset khi dang nhap
        /// lai) lam duong thoat cuoi cho co `ax.bQ` bi ket.
        ///
        /// ⚠️ PHAI di duong nay chu KHONG goi thang <c>Session.Disconnect()</c>: ham do dat
        /// <c>_running = false</c> TRUOC khi dong socket, nen vong doc thoat im lang va su kien
        /// <c>OnDisconnected</c> KHONG he ban ra (NsoSession.cs:96-107) => se khong ai goi
        /// ScheduleReconnect va acc nam im vinh vien - dung cai loi dang di chua.
        /// </summary>
        public void ForceRelogin(string reason)
        {
            if (_stopping || State == ClientState.Disconnected || State == ClientState.Reconnecting) return;

            DatLyDoVaoLai("TOOL NGẮT");   // cot Trang thai (di9)
            Log("[Relogin] " + reason);
            StopAutoSystems();
            Trade.DatLaiKhiMatKetNoi();
            if (_session != null) { try { _session.Disconnect(); } catch { } }
            State = ClientState.Reconnecting;
            FireStateChanged();

            if (Config.AutoReconnect)
                ScheduleReconnect();
            else
                Log("[Relogin] AutoReconnect dang TAT -> acc se nam im cho toi khi bat lai bang tay");
        }

        // ==================== AUTO SYSTEMS ====================

        /// <summary>
        /// Khoi dong lai cac he auto. <paramref name="startupDelayMs"/> = -1 dung mac dinh cua mode
        /// (2000ms, cho du lieu ve sau khi vua vao game); >= 0 la ghi de cho lan Start nay.
        /// Sau HOI SINH thi truyen <see cref="REVIVE_STARTUP_DELAY_MS"/>: du lieu da nam san trong
        /// GameStateManager, cho them la thoi gian chet thuan tuy (do duoc: 2/3 quang chet->di).
        /// </summary>
        private void StartAutoSystems(int startupDelayMs = -1)
        {
            if (_stopping || State != ClientState.InGame) return;

            if (_keepAlive == null)
            {
                _keepAlive = new KeepAliveController(this);
                _keepAlive.Start();
            }

            // Ban nay CHI CO MOT MODE (KhoMode) va MOI acc trong danh sach deu la thanh vien kho - KHONG
            // xet BatBao (bo cuc A khong co nut bat/tat; muon cho mot clone nghi thi "Nha clone").
            // Map/khu/cho dung lay tu CAI DAT KHO, khong lay tu TargetMapId cua tung acc - mot kho la
            // mot bo cai dat chung (docs/SPEC.md §4).
            if (_activeMode == null)
            {
                _activeMode = new Auto.KhoMode(this);
                var modeBase = _activeMode as AutoModeBase;
                if (modeBase != null && startupDelayMs >= 0)
                    modeBase.StartupDelayOverrideMs = startupDelayMs;
                Log("[Mode] Khoi dong: " + _activeMode.Name);
                _activeMode.Start();
            }
        }

        /// <summary>
        /// Co nghe lenh map/khu cua nhom truong khong. HAI o tick deu bat duoc:
        ///  - "Danh theo nhom" (AttackByGroup) - o cu, nghe lenh "ts" de train chung bai;
        ///  - "Bam nhom truong" (FollowLeader) - o moi. Server KHONG BAO GIO cho biet mot thanh vien
        ///    nhom dang o map nao (PartyMember chi co CharId/ClassId/Name), nen loi chat "ts" cua
        ///    truong la duong DUY NHAT de bam xuyen map. Khong mo cong nay thi bat mot minh
        ///    "Bam nhom truong" se chi bam duoc khi tinh co dung chung map - dung y user
        ///    "truong o dau cung bam".
        /// </summary>
        private bool ListensToLeaderOrder
        {
            get
            {
                return Config != null && Config.Train != null
                    && (Config.Train.AttackByGroup || Config.Train.FollowLeader);
            }
        }

        /// <summary>
        /// Map dich HIEU LUC cho mode: nghe lenh truong (<see cref="ListensToLeaderOrder"/> - bat
        /// "Danh theo nhom" HOAC "Bam nhom truong") + co lenh (GameState.GroupOrderMapId)
        /// -> lenh truong THANG map user cai; nguoc lai = Config.TargetMapId.
        /// Lenh truong chi la runtime (khong persist, khong ghi de config) -> tat CA HAI o
        /// la ve ngay map rieng, khoi dong lai app cung ve map rieng.
        /// </summary>
        /// <remarks>
        /// Thu tu uu tien (cao -> thap): <b>lenh goi Kich Yen</b> &gt; lenh truong nhom &gt; config user.
        /// Lenh goi thang lenh truong vi no la viec CO THOI HAN (con elite dang cho, TTL 120s), con
        /// lenh truong la trang thai lau dai - di goi xong xoa CallOrder la tu ve dung cho cu.
        /// </remarks>
        public int EffectiveTargetMapId
        {
            get
            {
                return Config != null ? Config.TargetMapId : -1;
            }
        }

        /// <summary>Khu dich HIEU LUC - di cap voi EffectiveTargetMapId (cung nguon lenh goi/truong/config).</summary>
        public byte EffectiveTargetZoneId
        {
            get
            {
                return Config != null ? Config.TargetZoneId : (byte)0;
            }
        }

        /// <summary>
        /// Ten nhan vat DUNG de so sanh trong Kich Yen (whitelist + dinh tuyen noi bo).
        /// Uu tien ten THAT do server gan (<c>MyChar.Name</c>); <c>Config.CharName</c> chi la o
        /// user go trong accounts.txt va KHONG BAO GIO duoc ghi nguoc tu server, acc 1 nhan vat de
        /// trong o do thi no rong vinh vien. Cuoi cung moi den Username (chi de hien thi).
        /// </summary>
        public string DisplayCharName
        {
            get
            {
                var mc = GameState.MyChar;
                if (mc != null && !string.IsNullOrEmpty(mc.Name)) return mc.Name;
                if (Config != null && !string.IsNullOrEmpty(Config.CharName)) return Config.CharName;
                return Config != null ? Config.Username : null;
            }
        }

        private void StopAutoSystems()
        {
            if (_activeMode != null) { _activeMode.Stop(); _activeMode = null; }
            if (_keepAlive != null) { _keepAlive.Stop(); _keepAlive = null; }
            _dangGiaoDich = false;
        }

        // ============ CONG GIOI HAN LOGIN THEO MAY CHU (mac dinh TAT) ============
        //
        // Xem NSOKHODO.Fleet.LoginGate de biet luat. O day chi lo phan VONG DOI cua mot acc:
        // xin slot, xep hang khong chiem thread, tra slot khi dung.

        private Timer _ipWaitTimer;
        private volatile bool _ipWaiting;
        private static readonly Random _ipRnd = new Random();
        private static readonly object _ipRndLock = new object();

        /// <summary>Dang xep hang o cong gioi han login (UI hien "CHO SLOT").</summary>
        public bool WaitingLoginSlot { get { return _ipWaiting; } }

        /// <summary>
        /// Xin slot cong IP. true = duoc di tiep (ke ca khi cong dang TAT). false = chua toi luot,
        /// nguoi goi phai hen lai ~1 giay. Log DUNG MOT LAN luc bat dau cho - dung log moi nhip,
        /// 30 acc x 1 dong/giay la troi log.
        /// </summary>
        private bool XinSlotLogin()
        {
            if (!NSOKHODO.Fleet.LoginGate.Enabled)
            {
                if (_ipWaiting) { _ipWaiting = false; FireStateChanged(); }
                return true;
            }
            if (_stopping) return false;

            string key = NSOKHODO.Fleet.LoginGate.GroupKey(Config);
            if (NSOKHODO.Fleet.LoginGate.TryEnter(this, key))
            {
                if (_ipWaiting)
                {
                    _ipWaiting = false;
                    Log("[Slot] Toi luot -> vao login (may chu " + key + ")");
                    FireStateChanged();
                }
                return true;
            }

            if (!_ipWaiting)
            {
                _ipWaiting = true;
                Log(string.Format("[Slot] May chu {0} da du {1} acc dang login -> xep hang cho",
                    key, NSOKHODO.Fleet.LoginGate.MaxPerGroup));
                FireStateChanged();
            }
            return false;
        }

        /// <summary>
        /// Login LAN DAU (nut "Mo Game"): chua toi luot thi hen lai 1 giay roi thu, KHONG spawn
        /// thread ngoi doi. Cung ly do da ghi o ScheduleReconnect: DoFullLogin chan toi ~65 giay,
        /// moi acc dang cho ma om mot thread la ca ham chet nghen.
        /// Duong login LAI khong di qua day - no co timer rieng (xem ScheduleReconnect).
        /// </summary>
        private void BatDauLoginKhiConSlot()
        {
            if (_stopping) return;
            if (XinSlotLogin())
            {
                HuyHenXinSlot();
                DoLoginAsync();
                return;
            }
            HenLaiXinSlot();
        }

        /// <summary>Hen thu lai sau ~1 giay (+ nhieu ngau nhien de 30 acc khong tick trung nhau).</summary>
        private void HenLaiXinSlot()
        {
            int delay = 1000;
            lock (_ipRndLock) delay += _ipRnd.Next(0, 400);
            if (_ipWaitTimer == null)
                _ipWaitTimer = new Timer(
                    _ => { try { BatDauLoginKhiConSlot(); } catch { } },
                    null, Timeout.Infinite, Timeout.Infinite);
            try { _ipWaitTimer.Change(delay, Timeout.Infinite); } catch { }
        }

        private void HuyHenXinSlot()
        {
            if (_ipWaitTimer == null) return;
            try { _ipWaitTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
        }

        /// <summary>Stop/Dispose: bo hang cho + tra slot ngay cho acc khac trong nhom.</summary>
        private void HuyChoSlot()
        {
            HuyHenXinSlot();
            _ipWaiting = false;
            NSOKHODO.Fleet.LoginGate.Exit(this);
        }

        // ==================== RECONNECT ====================

        private volatile bool _reconnectPending;

        // Thoi gian cho BAT BUOC (ms) do server chi dinh qua message "vao lai game sau X giay" (cmd -26).
        // >0 = lan reconnect ke tiep dung DUNG gia tri nay (khong backoff). Set o OnChatReceived, tieu thu
        // + clear trong ScheduleReconnect. Clear khi vao game thanh cong (tranh stale). Xem docs/features/RELOGIN.md P3.
        private volatile int _forcedReconnectDelayMs;

        // Marker chuoi server (MODGAME Controller c[0], private server text CO DAU). Xem SERVER_FACTS.md.
        private const string REENTRY_WAIT_MARKER = "vào lại game sau";

        // Moc (UTC) se login lai -> UI dem nguoc so giay con lai (ReconnectRemainingSec). Set trong ScheduleReconnect.
        private DateTime _reconnectResumeAtUtc;

        // ===== TRANG THAI KET NOI HIEN RO TUNG BUOC (di9, hoc ZangVPS o_0.java:89-133) =====
        // Zang ve chuoi buoc "WAIT_TIME_RELOGIN_i/n", "CONNET_RELOGIN"... len man hinh. Ta dat vao cot
        // Trang thai: user nhin la biet acc ket o PROXY, bi SERVER CHAN hay dang XEP HANG - truoc day ca
        // ba deu hien "CONNECTING" / "CHO VAO LAI 5s". Chu CO DAU vi hien tren UI (CLAUDE.md).
        private volatile string _lyDoVaoLai;   // ly do cua lan vao lai dang cho (null = chua ro / da vao game)
        private volatile int _lyDoLap;         // bao nhieu lan LIEN TIEP cung ly do do
        private volatile bool _choLuot;        // hen gio da no nhung dang xep hang ReloginGate
        private DateTime _noiXongLuc = DateTime.MinValue;   // luc _session.Connect vua xong - de nhan "server chan ngay"

        /// <summary>
        /// Server dong ket noi trong khoang nay sau khi vua noi xong = SERVER CHAN tu dau, khong phai rot.
        /// Zang lay 500ms (H.java:108-112). Ta lay 1000ms vi log chi co do phan giai GIAY nen chua do duoc
        /// nguong dung - dong log `[Client] Disconnected` in so ms that de chinh sau.
        /// </summary>
        private const int SERVER_CHAN_MS = 1000;

        private void DatLyDoVaoLai(string lyDo)
        {
            if (lyDo == _lyDoVaoLai) _lyDoLap++;
            else { _lyDoVaoLai = lyDo; _lyDoLap = 1; }
        }

        /// <summary>
        /// Chu cot Trang thai cho pha KET NOI / XEP HANG / CHO VAO LAI - dung chung PC (MainForm), Android
        /// (AccountGrid) va cua so Xem game (GameSnapshot) de ba noi noi y het nhau. Tra null khi khong o
        /// pha do (dang dang nhap, trong game, offline, loi) - noi goi tu xu ly nhu cu.
        /// </summary>
        public string TrangThaiKetNoi()
        {
            if (WaitingLoginSlot) return "CHỜ SLOT";
            switch (State)
            {
                case ClientState.Connecting:
                    return (Config != null && !string.IsNullOrEmpty(Config.Proxy)) ? "NỐI PROXY" : "NỐI SERVER";
                case ClientState.Handshake:
                    return "CHỜ KHOÁ";
                case ClientState.Reconnecting:
                    {
                        int rem = ReconnectRemainingSec();
                        if (rem <= 0) return _choLuot ? "CHỜ LƯỢT" : "ĐANG NỐI";
                        string lyDo = _lyDoVaoLai;
                        if (string.IsNullOrEmpty(lyDo)) return "CHỜ VÀO LẠI " + rem + "s";
                        int n = _lyDoLap;
                        return n > 1 ? string.Format("{0} ×{1} · {2}s", lyDo, n, rem)
                                     : string.Format("{0} · {1}s", lyDo, rem);
                    }
            }
            return null;
        }

        /// <summary>So giay con lai truoc khi login lai (cho UI hien thi dem nguoc). 0 neu khong dang cho.</summary>
        public int ReconnectRemainingSec()
        {
            if (State != ClientState.Reconnecting) return 0;
            double s = (_reconnectResumeAtUtc - DateTime.UtcNow).TotalSeconds;
            return s > 0 ? (int)Math.Ceiling(s) : 0;
        }

        /// <summary>
        /// Server vua tu choi vao game ("vao lai game sau N giay"), <see cref="_forcedReconnectDelayMs"/> da
        /// dat = N+1s (di8). Hen lich NGAY tai day chu khong doi server tu dong ket noi: do tren log
        /// 2026-09-15 server dong sau ~5s (dotay35 14:34:00 -> 14:34:05), hen luc do la cho thua 5s va UI
        /// hien "LOGGING IN" thay vi dem nguoc. Clone MODGAME: nhan tin la dong ngay (RELOGIN.md dong 151).
        ///
        /// Thu tu BAT BUOC: ScheduleReconnect TRUOC (dat State = Reconnecting -> WaitFor trong luong dang
        /// nhap thoat, DaCoLichVaoLai tra true) roi moi dong session. NsoSession.Disconnect() tat _running
        /// truoc khi dong socket nen KHONG ban OnDisconnected - khong co lich thu hai. Dong ket noi o day
        /// KHONG phai "tu ngat" luc dang choi: dang o pha dang nhap va server da tu choi.
        /// </summary>
        private void HenVaoLaiTheoGame()
        {
            if (_stopping || !Config.AutoReconnect) return;
            ScheduleReconnect();
            if (_session != null) { try { _session.Disconnect(); } catch { } }
        }

        private void ScheduleReconnect()
        {
            if (_stopping || _reconnectPending) return;
            _reconnectPending = true;
            _isReconnect = true;

            int delay;
            int forced = _forcedReconnectDelayMs;
            if (forced > 0)
            {
                // Server chi dinh thoi gian cho (vao lai game sau X giay) -> CHO DUNG, khong tinh backoff.
                _forcedReconnectDelayMs = 0;
                _reconnectCount = 0;         // reset: lan sau (neu co) bat dau backoff lai tu dau
                delay = forced;
                Log(string.Format("[Reconnect] Cho {0}s theo yeu cau server roi login lai", delay / 1000));
            }
            else
            {
                _reconnectCount++;

                // LUAT user chot 2026-09-15 (di6): KHONG backoff, KHONG tu len 60s - lan nao cung thu lai
                // sau ReconnectDelay (mac dinh 5s). Truoc day 5/10/15/20/25s roi 60s/lan.
                delay = Config.ReconnectDelay > 0 ? Config.ReconnectDelay : 5000;

                // CHI canh bao, KHONG tu keo dai thoi gian cho. Muon doi nhip thu lai thi user tu quyet.
                // ⚠️ KHONG doan nguyen nhan trong dong log nay. Ban cu viet "kha nang IP da het slot,
                // can them proxy" - do la ket luan cua SERVER_FACTS §29, ma user da bac bo
                // 2026-09-09. Log chi duoc noi CAI DO DUOC (server khong gui khoa), khong duoc noi
                // TAI SAO khi chua biet.
                if (_noKeyStreak >= NO_KEY_STREAK_LIMIT)
                    Log(string.Format("[Reconnect] {0} lan lien tiep noi duoc TCP nhung server khong gui khoa (chua ro nguyen nhan)",
                        _noKeyStreak));

                Log(string.Format("[Reconnect] Retry #{0} in {1}s",
                    _reconnectCount, delay / 1000));
            }

            // PHA DONG BO (D2, hoc ZangVPS o_0.java:82 - cong 0..5s ngau nhien) DA BO 2026-09-15 (di6):
            // user chot "thu lai sau 5s". Van con trai deu nho ReloginGate (toi da 5 acc dang nhap cung
            // luc, cap slot cach nhau >= 1s) + LoginGate.

            _reconnectResumeAtUtc = DateTime.UtcNow.AddMilliseconds(delay); // cho UI dem nguoc
            State = ClientState.Reconnecting;
            FireStateChanged();

            // Timer mot phat thay vi ThreadPool + Thread.Sleep(delay): cho toi 60 giay ma KHONG
            // giu worker nao. Truoc day 12 acc dang quay vong relogin = 12 worker nam ngu thuong
            // truc, lam tre callback KeepAlive (cung chay tren pool) cua cac acc dang khoe.
            if (_reconnectTimer != null) { try { _reconnectTimer.Dispose(); } catch { } }
            _reconnectTimer = new Timer(_ =>
            {
                if (_stopping) { _reconnectPending = false; return; }

                // CONG GIOI HAN LOGIN THEO IP (mac dinh TAT). Dat TRUOC cong fleet co chu dich: xin
                // cong fleet truoc roi moi ket o cong IP la om slot fleet ma khong dung, ca ham phai
                // cho theo. Cung nhip hen lai 1 giay, cung khong tinh vao _reconnectCount.
                if (!XinSlotLogin())
                {
                    try { _reconnectTimer.Change(1000, Timeout.Infinite); } catch { }
                    return;
                }

                // CONG DIEU TIET TOAN FLEET (D1, clone Form1.cs:127-143 cua ZangVPS): toi da 5 acc
                // trong pha login lai cung luc, hai lan cap slot cach nhau >= 1 giay. Chua toi luot
                // thi hen lai sau 1 giay - KHONG bo cuoc, khong tinh vao _reconnectCount (day khong
                // phai mot lan thu that bai, chi la xep hang).
                if (!NSOKHODO.Fleet.ReloginGate.TryEnter(this))
                {
                    if (!_choLuot) { _choLuot = true; FireStateChanged(); }   // cot Trang thai "CHO LUOT" (di9)
                    try { _reconnectTimer.Change(1000, Timeout.Infinite); } catch { }
                    return;
                }
                if (_choLuot) { _choLuot = false; FireStateChanged(); }
                _reconnectPending = false;
                DoLoginAsync();
            }, null, delay, Timeout.Infinite);
        }

        // ==================== HELPERS ====================

        /// <summary>
        /// Tach so giay tu chuoi server "...vao lai game sau &lt;N&gt; giay nua" (cmd -26, clone MODGAME
        /// Controller: substring giua c[0]..c[1]). Robust: tim marker roi doc chu so dau tien theo sau,
        /// khong phu thuoc hau to. Tra false neu khong phai message nay hoac khong co so.
        /// </summary>
        private static bool TryParseReentryWait(string text, out int seconds)
        {
            seconds = 0;
            if (string.IsNullOrEmpty(text)) return false;
            int idx = text.IndexOf(REENTRY_WAIT_MARKER, StringComparison.Ordinal);
            if (idx < 0) return false;

            int i = idx + REENTRY_WAIT_MARKER.Length;
            while (i < text.Length && (text[i] < '0' || text[i] > '9')) i++; // bo ky tu truoc so
            int start = i;
            long val = 0;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                val = val * 10 + (text[i] - '0');
                if (val > 86400) { val = 86400; break; }   // cap 1 ngay (tranh tran/so bat thuong)
                i++;
            }
            if (i == start) return false;                  // co marker nhung khong thay so
            seconds = (int)val;
            return true;
        }

        private bool WaitFor(Func<bool> condition, int timeoutMs)
        {
            int waited = 0;
            // State == Reconnecting giua luc dang nhap = server da dong ket noi va HandleDisconnected DA
            // dat lich vao lai (dung so giay game bao neu co) -> thoi cho, xem DaCoLichVaoLai (di6).
            while (!condition() && waited < timeoutMs && !_stopping && State != ClientState.Reconnecting)
            {
                Thread.Sleep(100);
                waited += 100;
            }
            return condition();
        }

        /// <summary>
        /// Loi DA TRA GIA (log 2026-09-15 dotay35): server bao "vao lai game sau 17 giay" roi dong ket noi
        /// luc 14:34:05 -> HandleDisconnected dat lich 19s. Nhung luong dang nhap VAN ngoi trong WaitFor 30s,
        /// nen luc lich no (14:34:24) DoLoginAsync thay _loginInProgress = true va THOAT - lich bi nuot.
        /// Toi 14:34:34 moi "Timeout entering game" -> Retry #1 5s: mat them ~25s moi lan vao lai.
        /// Nay: WaitFor thoat ngay khi thay Reconnecting; noi goi goi ham nay va return IM LANG (khong
        /// HandleLoginFailed - ham do se ghi de State = Error va lich da dat van dung).
        /// </summary>
        private bool DaCoLichVaoLai()
        {
            if (State != ClientState.Reconnecting) return false;
            Log("[Login] Server dong ket noi giua luc dang nhap -> giu lich vao lai da dat");
            return true;
        }

        public void Log(string text)
        {
            if (!Logging.Logger.Enabled) return;   // cong tac tong: tat -> khong sinh log (tiet kiem CPU treo nhieu acc)
            var h = OnLog;
            if (h != null) h(string.Format("[{0}] {1}", NameMask.Apply(Config.Username), text));
        }

        public void FireStateChanged()
        {
            var h = OnStateChanged;
            if (h != null) h();
        }

        public void Dispose()
        {
            Stop();
            if (_session != null) _session.Dispose();
        }
    }
}
