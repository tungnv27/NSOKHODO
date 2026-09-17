using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NSOKHODO.Client;
using NSOKHODO.Models;

namespace NSOKHODO.Auto
{
    /// <summary>
    /// Shared navigation engine - exact replica of game's TileMap.k() + TileMap.j() + Class_bd + Char.b().
    /// Tach ra tu AutoAFKController de ca AFK lan Train (TanSat) deu dung chung logic di chuyen.
    /// Khong tu chay thread - cac mode goi DoGmNavigation/DoZoneChange/CharBurstMove truc tiep.
    /// </summary>
    public class Navigator
    {
        private readonly NsoClient _client;
        private volatile bool _stopped;
        private readonly Random _rnd = new Random();

        private const int STEP_PX = 50;     // Char.b() step size
        // Nguong doi soat vi tri truoc khi burst - ZangVPS `ax.java:10128` dung dung so 80.
        private const int POS_RESYNC_PX = 80;
        private const int POS_RESYNC_LOG_GAP_MS = 5000;
        private DateTime _lastResyncLogAt = DateTime.MinValue;
        /// <summary>
        /// So goi move duoc ban TU DO truoc khi bat dau phanh - clone NSOTRUNGDUC Class_ba.java:10221
        /// <c>if (++dem &lt;= lllIIIII[56]) continue; Thread.sleep(100L);</c> voi <c>lllIIIII[56] = 50</c>
        /// (giai tu bo khoi tao mang, doi chieu bytecode javap).
        /// LUU Y ngu nghia: ban goc KHONG reset bo dem -> tu goi thu 51 tro di MOI GOI deu ngu 100ms.
        /// (Ban 2026-09-05 doc nham hang so la 20 va con tu them reset -> phanh nhe hon ban goc ~12 lan
        /// tren quang dai.)
        /// </summary>
        /// <summary>
        /// Lech truc Y bao nhieu thi ghi vet cu lao. 200px: cao hon han nhip chinh vat binh thuong
        /// (do duoc: 963/1300 cmd52 lech Y dung 24px, gan het phan con lai duoi 96px) nhung thap hon
        /// nhieu so voi 432px cua hai ca dut ket noi - de con bat duoc ca muc trung gian.
        /// </summary>
        private const int BURST_LOG_DY_PX = 200;
        /// <summary>Phanh log: ham burst nam tren duong nong nhat (MoveToMob goi ~10 lan/giay).</summary>
        private const int BURST_LOG_GAP_MS = 5000;
        private DateTime _lastBurstDyLogAt = DateTime.MinValue;

        private const int BURST_FREE_PACKETS = 50;
        /// <summary>Nghi bao lau - clone Class_ba.java:10223 (`Thread.sleep(100L)`).</summary>
        private const int BURST_REST_MS = 100;
        /// <summary>Khoang giua hai goi dich - clone Class_ba.java:10242 (`Class_gv.a(20L)`).</summary>
        private const int FINAL_MOVE_GAP_MS = 20;

        // ===== 2026-09-13 (ty3.5) DI XUONG THEO DIA HINH - xem CharBurstMove + GameData/FloorPath.cs =====
        /// <summary>
        /// Dich thap hon cho dang dung it nhat bao nhieu thi di theo dia hinh. 48 = 2 o: lech 1 o (24px)
        /// la bac nen nho, nhanh cu (FindGround) van xu ly.
        /// </summary>
        private const int DOWN_MIN_PX = 48;
        /// <summary>
        /// Moi goi luc ROI xuong bao nhieu px. CHUA DO server nhan toi dau - chon 48 (2 o) vi goi di NGANG
        /// 50px/buoc server nhan tu truoc toi nay, con cac lan bi bac deu la 240-480px mot phat
        /// (log 2026-09-13). Client goc khong co hang so buoc: gui vi tri vat ly toi da 250ms/lan (Char.cs:1268).
        /// </summary>
        private const int FALL_STEP_PX = 48;
        /// <summary>
        /// Nho ket qua tim duong bao lau. TrainMode.MoveToMob goi burst ~10 lan/giay khi quai o tang duoi -
        /// khong nho thi moi lan cap mot mang w*h. Khoa theo O (map, o xuat phat, o dich), nen di lech vai px
        /// trong cung o van dung lai; het han de lop o nen server bom (doi khu) khong bi dinh ban cu qua lau.
        /// </summary>
        private const int DUONG_XUONG_CACHE_MS = 3000;
        private sealed class DuongXuongCache
        {
            public int Map, Sc, Sr, Gc, Gr, NenDichY;
            public List<GameData.FloorPath.DiemMoc> Moc;   // null = da tim, khong co duong
            public DateTime HetHan;
        }
        // Thay CA doi tuong mot lan (khong sua tung truong) - HeNen/KichYen co the goi burst tu luong khac.
        private volatile DuongXuongCache _duongXuong;
        private DateTime _lastDuongXuongLogAt = DateTime.MinValue;

        // ===== 2026-09-13 "HAN CHE 900s" (TrainConfig.HanCheBan) - nhip buoc clone bot Java Auto30 =====
        /// <summary>Auto30 `Class_cz.java:1780`: <c>Class_br.b(300L)</c> sau moi cum goi.</summary>
        private const int HAN_CHE_REST_MS = 300;

        /// <summary>Nhip gui cua MOT lan burst: do dai buoc, so goi tu do, nghi bao lau, co dat lai bo dem.</summary>
        private struct NhipBuoc
        {
            public int Step;
            public int TuDo;
            public int NghiMs;
            public bool DatLai;
        }

        /// <summary>
        /// Bat "Han che 900s" + da biet toc do nhan vat -> dung nhip Auto30 (`Class_cz.java:1770-1790`):
        /// <c>buoc = speed &lt;&lt; 3</c>, <c>if (++dem &gt; (int)(speed*1.5)) { dem = 0; sleep(300); }</c>.
        /// Nguoc lai (tat, hoac Speed = 0 vi chua nhan goi toc do) -> nhip cu cua ta, tranh buoc 0 px lap vo han.
        /// </summary>
        private NhipBuoc LayNhipBuoc()
        {
            var t = _client.Config.Train;
            int sp = _client.GameState.MyChar.Speed;
            if (t != null && t.HanCheBan && sp > 0)
                return new NhipBuoc { Step = sp << 3, TuDo = (int)(sp * 1.5), NghiMs = HAN_CHE_REST_MS, DatLai = true };
            return new NhipBuoc { Step = STEP_PX, TuDo = BURST_FREE_PACKETS, NghiMs = BURST_REST_MS, DatLai = false };
        }

        /// <summary>
        /// Dem + phanh sau MOI goi trung gian. Nhip cu: KHONG dat lai bo dem (qua goi 50 thi moi goi deu
        /// ngu - ngu nghia NSOTRUNGDUC, xem BURST_FREE_PACKETS). Nhip Auto30: dat lai sau moi lan nghi.
        /// </summary>
        private static void Phanh(ref int demGoi, NhipBuoc n)
        {
            if (++demGoi > n.TuDo)
            {
                Thread.Sleep(n.NghiMs);
                if (n.DatLai) demGoi = 0;
            }
        }
        /// <summary>
        /// Tran cho gói MAP_INFO ve sau khi da gui yeu cau doi map.
        ///
        /// 2026-09-07: 10000 -> 2000, CLONE THANG ZangVPS `av_0.q()` (`av_0.java:1285-1289`):
        ///     public static void q() { ce = 1; df_0.a("WaitObject").a(2000L); }
        /// Chot cua Zang duoc nha o CUOI ham parse goi map (`aH.java:4191` goi `av_0.x()`) - ta co
        /// dung ngu nghia do: `MapChangeEvent.Set()` nam o cuoi `MapHandler.HandleMapInfo`
        /// (`MapHandler.cs:256`). Hai ben khop nhau nen con so moc thang sang duoc.
        ///
        /// DO TREN LOG THAT (2026-09-07, 7.611 lan doi map qua waypoint THANH CONG, 50 acc):
        ///     p50 = 1s · p90 = 2s · p95 = 2s · p99 = 2s · max = 5s
        ///     chi 4/7.611 (0,05%) lau hon 2 giay.
        /// Tuc 8 trong 10 giay cu la VUT DI: moi chang hong la nhan vat dung im tron 10 giay roi
        /// moi thu lai. Trong 39 phut do duoc 79 `wp fail` + 93 `NPC25 NV fail` = 172 lan.
        ///
        /// DUNG NANG LAI vi ly do "cho chac an": so 2000 nay la cua Zang, va p99 do tren CHINH
        /// server nay da xac nhan no du. Het 2 giay khong co nghia la bo cuoc - vong thu lai san
        /// co se chay tiep, chi la chay som hon 8 giay.
        /// </summary>
        private const int WAIT_MAP = 2000;   // ZangVPS av_0.q(): df_0.a("WaitObject").a(2000L)

        /// <summary>
        /// Sau khi hoi sinh, trong bao lau thi chuyen di duoc mien nhip giao cach giua hai lan doi
        /// map. Day chi la TRAN CUOI: moc that su bi xoa ngay khi <c>TrainMode</c> thay da ve toi map
        /// train, nen binh thuong quyen nay chi song vai giay. Tran 60 giay chi de acc ket giua duong
        /// khong giu quyen mai mai.
        /// </summary>
        private const int REVIVE_FAST_TRIP_MS = 60000;

        /// <summary>Da ghi log "di thang" cho chuyen nay chua - tranh moi chang mot dong giong nhau.</summary>
        private bool _daBaoDiNhanh;

        // Per-instance: maps unreachable via NPC (task requirement fail)
        private readonly Dictionary<int, DateTime> _npcDestBlacklist = new Dictionary<int, DateTime>();
        private DateTime _lastZoneChange = DateTime.MinValue;

        // (_zoneResetCount da go 2026-09-06 cung voi vong leo thang doi zone - xem ghi chu
        //  "DA GO vong leo thang doi zone" phia duoi.)

        // Track unreachable targets to avoid spamming "No path" every 3s
        private readonly Dictionary<string, DateTime> _unreachableTargets = new Dictionary<string, DateTime>();

        // Temporary per-instance waypoint blacklist (expires after 120s)
        private readonly Dictionary<string, DateTime> _wpBlacklist = new Dictionary<string, DateTime>();

        // Shared waypoint cache: "fromMap:wpIdx" -> actual destination mapId
        private static readonly Dictionary<string, int> _wpCache = new Dictionary<string, int>();
        private static readonly HashSet<string> _brokenLinks = new HashSet<string>();

        // "fromMap:wpIdx->toMap" da chung minh la KHONG dan toi dich (di toi noi + xin doi map,
        // server im). Vi sao can: chi so waypoint mac dinh lay tu THU TU trong
        // MapGraph.Connections[] - do la PHONG DOAN, khong phai du lieu map. Doan sai thi
        // FindWaypointForTarget tra ve dung chi so sai do MAI MAI, va nhanh "quet thu waypoint
        // khac" (TryLearnWaypoint) thanh code chet.
        // Su co 2026-09-03: 17 acc ket vinh vien o 48->47, log 83 lan that bai, 0 lan thanh cong.
        //
        // ===================== SUA LON 2026-09-06 - BA THAY DOI =====================
        // Ban cu la HashSet vinh vien, khoa CHI CO "fromMap:idx". Do tren log that (20 phut,
        // 50 acc, 61531 dong): DUNG BA dong danh dau da sinh ra 4669 lan `wp not found`
        // (232/phut), keo ti le vao map xuong 61,8%. Ba dong do la:
        //     21:21:46  wp[1] map 46 (dich 39)   -> 3174 lan hong 46->39
        //     21:24:03  wp[2] map 46 (dich 47)
        //     21:37:19  wp[1] map 72 (dich 39)   -> 1274 lan hong 72->39
        // Doi chieu MapGraph.Connections[46] = {63,39,47}: ca hai chi so bi giet deu DUNG.
        // Waypoint hoan toan lanh, chi la mot lan that bai tam thoi bi ket an chung than.
        //
        // (A3) KHOA NAY GIO CO toMap. Ban cu khong co: giet wp[1] cua map 10 khi tim map 9 la
        //      giet luon tuyen 10->11, du waypoint do lam dung viec cua no.
        // (A1) CO HAN DUNG. ZangVPS khong he co co che danh dau waypoint chet (khong ton tai
        //      cau truc nao nhu vay trong 247 file) - no chi tra chi so tu bang ke roi di.
        //      Ta khong bo han vi guard nay sinh ra de chua su co 48->47 that; nhung han dung
        //      bien no tu "an chung than" thanh "tam dinh chi".
        // (A2) VAN XOA TRANG khi can ung vien - clone `bL.java:278-281` cua Zang:
        //          if (... khong con ung vien nao ...) { ct_0.c.clear(); }
        //      Zang chi ap cho blacklist boss, nhung mau hinh la thu ta thieu: khong bao gio
        //      duoc phep de tap ung vien rong roi tra ve "that su thu 0".
        //
        // 2026-09-13 (S8): DOI TU `static` SANG RIENG TUNG ACC (user chot). Ban cu co y dung chung
        // ("mot acc hoc duoc waypoint hong thi ca fleet khoi ton mot lan cho") nhung cai gia la MOT ACC
        // DAU DOC CA TAB 5 phut - chinh ba dong danh dau o tren da sinh 4669 lan `wp not found`.
        // ZangVPS luu rieng tung nhan vat. Gia moi: moi acc tu mat mot lan cho (2 giay) khi gap cong
        // hong that.
        private readonly Dictionary<string, DateTime> _wpDeadIdx = new Dictionary<string, DateTime>();

        /// <summary>Dau chet song bao lau. 5 phut = bang blacklist canh da co san o duoi.</summary>
        private static readonly TimeSpan WP_DEAD_TTL = TimeSpan.FromMinutes(5);

        private static string WpDeadKey(int fromMap, int idx, int toMap)
        {
            return fromMap + ":" + idx + "->" + toMap;
        }

        private bool IsWpDead(int fromMap, int idx, int toMap)
        {
            string k = WpDeadKey(fromMap, idx, toMap);
            lock (_wpDeadIdx)
            {
                DateTime until;
                if (!_wpDeadIdx.TryGetValue(k, out until)) return false;
                if (DateTime.UtcNow >= until) { _wpDeadIdx.Remove(k); return false; }
                return true;
            }
        }

        private void MarkWpDead(int fromMap, int idx, int toMap)
        {
            string k = WpDeadKey(fromMap, idx, toMap);
            bool added;
            lock (_wpDeadIdx)
            {
                added = !_wpDeadIdx.ContainsKey(k);
                _wpDeadIdx[k] = DateTime.UtcNow + WP_DEAD_TTL;
            }
            if (added)
                _client.Log(string.Format(
                    "[Nav] wp[{0}] cua map {1} KHONG dan toi map {2} - loai {3} phut, lan sau quet waypoint khac",
                    idx, fromMap, toMap, (int)WP_DEAD_TTL.TotalMinutes));
        }

        /// <summary>
        /// VAN AN TOAN (clone y tuong `ct_0.c.clear()` cua Zang, bL.java:278-281): xoa moi dau
        /// chet cua canh fromMap->toMap. Goi khi vong quet khong con gi de thu - tha thu lai mot
        /// waypoint da tung hong con hon dung im bao "khong tim thay" mai mai.
        /// </summary>
        private int ClearWpDeadForEdge(int fromMap, int toMap)
        {
            string suffix = "->" + toMap;
            string prefix = fromMap + ":";
            var bo = new List<string>();
            lock (_wpDeadIdx)
            {
                foreach (var k in _wpDeadIdx.Keys)
                    if (k.StartsWith(prefix) && k.EndsWith(suffix)) bo.Add(k);
                foreach (var k in bo) _wpDeadIdx.Remove(k);
            }
            return bo.Count;
        }
        private static string CacheFile { get { return NSOKHODO.Config.AppPaths.WpCache; } }

        static Navigator() { LoadCache(); }

        public Navigator(NsoClient client) { _client = client; }

        /// <summary>Cho phep nav chay lai sau khi da Stop (mode restart).</summary>
        public void Reset() { _stopped = false; }

        /// <summary>Yeu cau dung moi vong lap di chuyen dang chay.</summary>
        public void Stop() { _stopped = true; }

        /// <summary>
        /// True neu duong from->to vua moi bi danh dau khong di duoc (trong 60s).
        /// Mode dung de skip + sleep dai thay vi retry lien tuc.
        /// </summary>
        public bool IsKnownUnreachable(int fromMap, int toMap)
        {
            DateTime expiry;
            lock (_unreachableTargets)
            {
                _unreachableTargets.TryGetValue(fromMap + ":" + toMap, out expiry);
            }
            return expiry > DateTime.UtcNow;
        }

        // ======================== TileMap.k() - NAVIGATION ========================

        /// <summary>Duoi nguong nay thi khong co diem nao da mo de nhay toi -> giu y nguyen hanh vi cu.</summary>
        private const int MOD_MIN_TASK = 17;
        /// <summary>Toi da so chang di bo mot luot (clone guard 60 cua ModMove).</summary>
        private const int MOD_MAX_HOPS = 60;
        /// <summary>
        /// Dung yen hoan toan sau mot luot thi nghi bang nay roi moi thu lai.
        /// Ban goc de 30000 (ModMove.RETRY_MS); user chot 2026-09-09 la LAU QUA -> ha con 10 giay.
        /// An toan vi luot thu lai KHONG mien phi ma cung khong dat: no thoat gan nhu tuc thi khi
        /// van khong co duong (BFS tren bang tinh), va co tien trien thi moc nghi da bi xoa roi.
        /// </summary>
        private const int MOD_RETRY_MS = 10000;
        /// <summary>
        /// Ngan sach thoi gian MOT LUOT goi. KHAC ban goc, co chu dich - xem chu thich o
        /// <see cref="ModMove"/>.
        /// </summary>
        private const int MOD_BUDGET_MS = 60000;

        /// <summary>Moc "nghi phat" sau mot luot ModMove khong nhuc nhich duoc. Rieng tung account.</summary>
        private DateTime _modRetryAt = DateTime.MinValue;

        /// <summary>
        /// Cua vao DUY NHAT cho moi che do khi can di toi mot map (7 cho goi trong repo).
        ///
        /// Chay hai buoc:
        ///  1. <see cref="DoGmNavigationCore"/> - luat GOC, nguyen ven. Toi noi thi thoi, KHONG doi
        ///     gi so voi truoc day.
        ///  2. Khong toi duoc thi mo them duong "di map khi CHUA LAM NHIEM VU" (<see cref="ModMove"/>).
        /// </summary>
        public bool DoGmNavigation(int targetMap)
        {
            _coreKhongCoDuong = false;
            bool ok = DoGmNavigationCore(targetMap);
            // Chup NGAY: ModMove goi lai DoGmNavigationCore nhieu lan va se ghi de co nay.
            bool coreKhongCoDuong = _coreKhongCoDuong;
            if (_client.GameState.CurrentMap.MapId == targetMap)
            {
                _modRetryAt = DateTime.MinValue;
                return true;
            }

            int mapLucHong = _client.GameState.CurrentMap.MapId;
            int kq;
            bool res = ModMove(targetMap, ok, out kq);

            // S5 (2026-09-13): danh dau "khong toi duoc" SAU khi biet nhanh di dai co ganh duoc khong.
            //  - taskId < 17 (user chot: van bao khong toi duoc) / khong co duong DI BO nao -> 60s nhu cu.
            //  - di dai co chay nhung dung im, hoac dang nghi giua hai luot -> chi toi moc _modRetryAt,
            //    de 6 noi goi IsKnownUnreachable khong chan mat luot thu lai cua di dai.
            //  - co tien trien -> khong danh dau.
            if (coreKhongCoDuong)
            {
                DateTime until = DateTime.MinValue;
                if (kq == MOD_KQ_KHONG_AP_DUNG || kq == MOD_KQ_KHONG_CO_DUONG)
                    until = DateTime.UtcNow.AddSeconds(60);
                else if (kq == MOD_KQ_DUNG_IM || kq == MOD_KQ_DANG_NGHI)
                    until = _modRetryAt;
                if (until > DateTime.UtcNow)
                {
                    lock (_unreachableTargets) { _unreachableTargets[mapLucHong + ":" + targetMap] = until; }
                }
            }
            return res;
        }

        /// <summary>Lan goi DoGmNavigationCore gan nhat bao "khong co duong" (BFS rong). Rieng tung acc.</summary>
        private bool _coreKhongCoDuong;

        /// <summary>Moc het nghi canh tat NPC 25 -> map NV sau mot lan hong (review 2026-09-13, L1). Rieng tung acc.</summary>
        private DateTime _npc25NvHongDen = DateTime.MinValue;
        private const int NPC25_NV_NGHI_MS = 120000;

        // Ket qua mot luot ModMove - DoGmNavigation dung de quyet dinh danh dau "khong toi duoc".
        private const int MOD_KQ_KHONG_CHAY = 0;        // dang dung / chet / chua co nhan vat
        private const int MOD_KQ_KHONG_AP_DUNG = 1;     // taskId < MOD_MIN_TASK
        private const int MOD_KQ_DANG_NGHI = 2;         // chua het moc nghi giua hai luot
        private const int MOD_KQ_KHONG_CO_DUONG = 3;    // khong co duong DI BO nao toi dich
        private const int MOD_KQ_DUNG_IM = 4;           // co chay nhung khong doi duoc map nao
        private const int MOD_KQ_CO_TIEN_TRIEN = 5;     // toi dich, hoac doi duoc it nhat mot map

        /// <summary>
        /// "Di map khi chua lam nhiem vu" - clone <c>ModMove</c> cua MODGAME (hook22/23/23a/24),
        /// LUON BAT NGAM, khong co o tich (user chot 2026-09-09, dung nhu ban goc).
        ///
        /// <para><b>Vi sao lam duoc:</b> server chi chan NPC DICH CHUYEN (mo menu NPC, cmd 29),
        /// KHONG chan DI BO qua cong (cmd -17). Ban dau MODGAME chi "go chot nhiem vu" trong BFS va
        /// SAI: duong di van an gian qua canh NPC, server chan, nhan vat dung chet o map 38. Cach
        /// dung la LAI: nhay toi diem DA MO gan dich nhat, roi di bo not - xuyen qua chinh nhung
        /// map dang bi khoa dich chuyen.</para>
        ///
        /// <para><b>Mot cho co y lam KHAC ban goc:</b> ban goc di toi 60 chang x 5 giay lien tuc.
        /// Ham nay von DA chan Tick toi ~115 giay (canh bao co san o <c>NsoClient</c> va
        /// <c>KeepAliveController</c>), them vong do la co the chan them 5 phut nua - du de lo ca
        /// nhip chet/hoi sinh. Nen co <see cref="MOD_BUDGET_MS"/>: het gio thi tra ve, tick sau di
        /// tiep. KHONG mat tien do - moi chang da di duoc deu giu nguyen, va chinh ban goc cung
        /// dua vao tick sau goi lai. Ngan sach do GIUA cac chang, nen mot chang dang ket van ton
        /// dung bang thoi gian nhu hien nay.</para>
        ///
        /// <para>Nguyen tac cua ban goc duoc giu nguyen: <b>co tien trien thi khong phat</b> - doi
        /// duoc map la xoa moc nghi, chi khi dung im hoan toan moi nghi 30 giay.</para>
        /// </summary>
        private bool ModMove(int targetMap, bool coreResult, out int ketQua)
        {
            ketQua = MOD_KQ_KHONG_CHAY;
            if (_stopped || _client.State == ClientState.Dead) return coreResult;

            var mc = _client.GameState.MyChar;
            if (mc == null) return coreResult;

            // Chua mo duoc diem nao thi khong co cho nao de nhay toi roi di bo tiep -> hanh vi cu.
            // User chot 2026-09-13: taskId < 17 thi VAN bao khong toi duoc, KHONG di duong dai.
            if (mc.IsHuman && mc.TaskId < MOD_MIN_TASK) { ketQua = MOD_KQ_KHONG_AP_DUNG; return coreResult; }
            if (DateTime.UtcNow < _modRetryAt) { ketQua = MOD_KQ_DANG_NGHI; return coreResult; }

            int start = _client.GameState.CurrentMap.MapId;

            // Review 2026-09-13 (F4): dang o Nha thi dau thi di dai VO NGHIA - loi ra duy nhat la noi chuyen voi
            // NPC, dung thao tac luat goc vua lam. Chay tiep chi lap lai no them 1-2 lan moi tick (moi lan di bo
            // + cho 2s). Khong danh dau gi: tick sau luat goc tu thu lai, nhu Zang.
            if (MapGraph.IsArena(start)) return coreResult;

            // === Buoc 1: chon cho de NHAY toi (chot nhiem vu VAN BAT o buoc nay) ===
            // S6 (2026-09-13): bo qua diem NPC vua dich chuyen hong (dang bi chan 120s), ke ca map
            // nhiem vu (NPC 25). Truoc day di dai chon lai dung diem vua hong => moi luot lap lai chang hong.
            var dangChan = NpcDichDangBiChan();
            int curD = MapGraph.WalkHops(start, targetMap);
            int hub = MapGraph.BestTeleHub(targetMap, mc.TaskId, mc.IsHuman, dangChan);
            int hubD = hub >= 0 ? MapGraph.WalkHops(hub, targetMap) : -1;
            int qm = DateTime.UtcNow >= _npc25NvHongDen ? mc.DailyQuestMapId : -1;   // L1: NPC 25 NV vua hong
            int qmD = (qm > 0 && qm < 160 && !dangChan.Contains(qm)) ? MapGraph.WalkHops(qm, targetMap) : -1;

            int tele = -1;
            int bestD = curD;                                   // dung yen tai cho
            if (hubD >= 0 && (bestD < 0 || hubD < bestD)) { bestD = hubD; tele = hub; }
            // Uu tien map nhiem vu khi HOA - giong ban goc (qmD <= hubD).
            if (qmD >= 0 && (bestD < 0 || qmD <= bestD)) { bestD = qmD; tele = qm; }

            if (bestD < 0)
            {
                _client.Log(string.Format(
                    "[Nav] Chua lam NV: khong co duong DI BO nao toi map {0} (dang o {1}, taskId={2}) - nghi {3}s",
                    targetMap, start, mc.TaskId, MOD_RETRY_MS / 1000));
                _modRetryAt = DateTime.UtcNow.AddMilliseconds(MOD_RETRY_MS);
                ketQua = MOD_KQ_KHONG_CO_DUONG;
                return coreResult;
            }

            _client.Log(string.Format("[Nav] Di duong dai toi map {0} (taskId={1}, nhay toi {2}, con {3} chang di bo)",
                targetMap, mc.TaskId, tele >= 0 ? tele.ToString() : "khong nhay", bestD));

            if (tele >= 0 && tele != start)
            {
                DoGmNavigationCore(tele);   // chot nhiem vu VAN BAT
                NghiSauChangDiDai(start, targetMap);
            }

            // === Buoc 2: di bo tung chang, chot nhiem vu TAT ===
            DateTime t0 = DateTime.UtcNow;
            for (int guard = 0; guard < MOD_MAX_HOPS; guard++)
            {
                if (_stopped || _client.State == ClientState.Dead) break;

                int now = _client.GameState.CurrentMap.MapId;
                if (now == targetMap) break;
                if ((DateTime.UtcNow - t0).TotalMilliseconds > MOD_BUDGET_MS)
                {
                    _client.Log(string.Format(
                        "[Nav] Het ngan sach {0}s cho mot luot di duong dai - dang o map {1}, tick sau di tiep",
                        MOD_BUDGET_MS / 1000, now));
                    break;
                }

                int next = MapGraph.NextWalkHop(now, targetMap);
                if (next < 0) break;

                DoGmNavigationCore(next, true);
                if (_client.GameState.CurrentMap.MapId == now) break;   // khong nhuc nhich -> ket
                NghiSauChangDiDai(now, targetMap);
            }

            int end = _client.GameState.CurrentMap.MapId;
            if (end == targetMap)
            {
                _modRetryAt = DateTime.MinValue;
                ketQua = MOD_KQ_CO_TIEN_TRIEN;
                _client.Log("[Nav] Di duong dai XONG - da toi map " + targetMap);
                return true;
            }

            // "Co tien trien thi khong phat": doi duoc map la lan sau cho di tiep ngay.
            if (end != start) { _modRetryAt = DateTime.MinValue; ketQua = MOD_KQ_CO_TIEN_TRIEN; }
            else { _modRetryAt = DateTime.UtcNow.AddMilliseconds(MOD_RETRY_MS); ketQua = MOD_KQ_DUNG_IM; }

            _client.Log(string.Format("[Nav] Di duong dai dung o map {0} (dich {1}){2}",
                end, targetMap, end == start ? " - khong nhuc nhich, nghi " + (MOD_RETRY_MS / 1000) + "s" : ""));
            return false;
        }

        /// <summary>
        /// Exact replica of TileMap.k(targetMapId).
        /// BFS pathfinding + execute each step in a loop.
        /// Game loop: for each step { NPC or WP transition; if(mapID!=target) h(); sleep(1000); }
        ///
        /// <para>Day la LOI GOC. Cua vao that cho ca bot la <see cref="DoGmNavigation"/> - no thu
        /// loi goc truoc, that bai moi mo them duong "di khi chua lam nhiem vu".</para>
        /// </summary>
        private bool DoGmNavigationCore(int targetMap, bool ignoreTaskGate = false)
        {
            int curMap = _client.GameState.CurrentMap.MapId;
            if (curMap == targetMap) return true;

            // BFS pathfinding (with task restrictions + NPC blacklist)
            var myChar = _client.GameState.MyChar;
            // Bom canh tat Truong->questMap qua NPC 25 BAT KE target (clone MODGAME TileMap.k:1563
            // "if (var10 && var8 != null && bc[var8.mapId] ...)" -> chi can co NV hang ngay (var8 != null),
            // KHONG gate theo target). Nho vay train o map CANH map NV van di tat qua NPC 25 roi loi bo
            // 1 chang toi map dich, thay vi chay vong xa. BFS tu chon duong ngan nhat (canh chi them, khong bo).
            // Review 2026-09-13 (L1): NPC 25 vua hong khi di map NV -> trong NPC25_NV_NGHI_MS tim duong COI NHU
            // KHONG CO NV (tat canh tat NPC 25), de cong di bo sat Truong van di duoc. Xem nhanh xu ly hong.
            bool npc25NvDangNghi = DateTime.UtcNow < _npc25NvHongDen;
            int questMapId = (myChar.DailyQuestMapId > 0 && !npc25NvDangNghi) ? myChar.DailyQuestMapId : -1;
            _client.Log(string.Format("[Nav] taskId={0} isHuman={1} questMap={2}{3}", myChar.TaskId, myChar.IsHuman, questMapId,
                npc25NvDangNghi ? " (NPC 25 NV dang nghi)" : ""));
            var path = BuildPath(curMap, targetMap, questMapId, ignoreTaskGate);
            if (path == null || path.Count < 2)
            {
                // Phan biet HAI kieu "khong co duong" - truoc day gop chung mot dong nen bay
                // MOT CHIEU trong bang ke bi che kin (phat hien 2026-09-05 khi doi chieu bh[][] cua
                // NSOTRUNGDUC: Connections[1]/[27]/[72] deu CHUA 157 va co NPC dua vao map 157,
                // nhung KHONG co hang Connections[157] -> vao duoc, ra khong duoc, ket vinh vien).
                if (MapGraph.GetConnections(curMap).Length == 0)
                {
                    _client.Log(string.Format(
                        "[Nav] KET CUNG: map {0} KHONG co canh ra nao trong bang ke - moi dich deu vo vong. " +
                        "Day la LOI DU LIEU MapGraph.Connections, khong phai loi di chuyen.", curMap));
                }
                else
                {
                    _client.Log(string.Format("[Nav] No path {0}->{1} (taskId={2})",
                        curMap, targetMap, myChar.TaskId));
                }
                // 2026-09-13 (S5): KHONG danh dau "khong toi duoc" o day nua. Truoc day danh dau 60s
                // NGAY TAI DAY => 6 noi goi IsKnownUnreachable (TrainMode/DanhVong/SaveSpot/LoiDai) ngu
                // 15s va BO QUA LUON nhanh di dai trong 60s. Nay chi bat co; DoGmNavigation danh dau
                // sau khi biet nhanh di dai co ganh duoc hay khong.
                _coreKhongCoDuong = true;
                return false;
            }

            _client.Log(string.Format("[Nav] Duong: {0}", string.Join("->", path)));

            // === Execute path - exact TileMap.k() loop ===
            // Game: for(i=1; i<path.size() && ag && expectedMap==mapID; i++)
            int expectedMap = curMap;
            for (int i = 1; i < path.Count && !_stopped && expectedMap == _client.GameState.CurrentMap.MapId; i++)
            {
                // Check alive before each step (game checks eh.ag flag)
                if (_client.State == ClientState.Dead) break;

                int fromMap = path[i - 1];
                int toMap = path[i];
                var action = MapGraph.GetTransitionAction(fromMap, toMap, questMapId);

                // Walk to transition point + send map change request ATOMICALLY
                // Game: j() does walk + sleep(10ms) + requestChangeMap in one call
                int oldMap = _client.GameState.CurrentMap.MapId;

                if (action.IsNPC && action.IsArenaExit)
                {
                    // S11 (2026-09-13): roi Nha thi dau bang NPC - xem DoArenaExit.
                    DoArenaExit(oldMap);
                    if (_client.State == ClientState.Dead) break;
                }
                else if (action.IsNPC)
                {
                    DoNpcWalk(action.NpcTemplateId);
                    if (_client.State == ClientState.Dead) break;

                    // 2026-09-13 (S7): DA GO NavigationQueue.Acquire/Release - xem ghi chu o nhanh waypoint.
                    bool menuHong = false;
                    try
                    {
                        _client.MapChangeEvent.Reset();
                        var closest = FindNpc(action.NpcTemplateId);
                        if (closest != null)
                        {
                            // Fix #3: block other Auto controllers from sending packets mid-transition
                            _client.GameState.IsChangingMap = true;

                            if (action.MenuText != null)
                            {
                                // Hang dong (NPC 0) + map nhiem vu (NPC 25): chon THEO CHU nhu ZangVPS.
                                menuHong = !ChonMenuNpcTheoChu(action.NpcTemplateId, action.MenuText);
                            }
                            else
                            {
                                // Clone ZangVPS i_0.a(npc, m1, m2) (i_0.java:15456-15473): openMenu roi menu NGAY,
                                // khong cho server tra. Server "type" != 0 (Hirosaki/Haruna) gui them
                                // menu(25,1,0) truoc chang NPC 25 (cF.aq(), i_0.java:15469-15471).
                                _client.Npc.SendOpenMenu((short)closest.TemplateId);
                                if (action.NpcTemplateId == 25 && ServerTypeKhacKhong())
                                    _client.Npc.SendNpcMenu(25, 1, 0);
                                _client.Npc.SendNpcMenu((byte)action.NpcTemplateId,
                                    (byte)action.MenuParam1, (byte)action.MenuParam2);
                            }
                        }
                        if (!menuHong && _client.GameState.CurrentMap.MapId != toMap)
                            WaitForMapChange(WaitTimeout(), oldMap);
                    }
                    finally
                    {
                        _client.GameState.IsChangingMap = false;
                    }
                }
                else
                {
                    int wpIdx = FindWaypointForTarget(fromMap, toMap);
                    if (wpIdx < 0)
                    {
                        _client.Log(string.Format("[Nav] wp not found {0}->{1}", fromMap, toMap));
                        return false;
                    }
                    // Game TileMap.j(): walk + delay + requestChangeMap ATOMIC (no semaphore between)
                    _client.MapChangeEvent.Reset();
                    DoWaypointWalk(wpIdx);
                    if (_client.State == ClientState.Dead) break;
                    var swMap = System.Diagnostics.Stopwatch.StartNew();

                    // Wait for map change.
                    // 2026-09-13 (S7): DA GO NavigationQueue - hang doi DUNG CHUNG moi acc trong tab (khoa
                    // gian cach 100ms + semaphore 50) => ca tab chi ~10 lan doi map/giay, acc thu 120 cho
                    // ~12s MOI chang. O nhanh nay goi xin doi map DA GUI TRUOC khi vao hang nen no khong gian
                    // cach goi nao, chi bat acc dung cho. ZangVPS khong co gi tuong tu. User chot: bo hang
                    // doi, GIU nhip "Toc do NextMap" (rieng tung acc, xem NhipQuaMap).
                    try
                    {
                        // Fix #3: block other Auto controllers from sending packets mid-transition
                        _client.GameState.IsChangingMap = true;
                        _client.Movement.SendRequestChangeMap();
                        if (_client.GameState.CurrentMap.MapId != toMap)
                            WaitForMapChange(WaitTimeout(), oldMap);
                        // Fix #6: record measured latency for next iterations
                        if (_client.GameState.CurrentMap.MapId != oldMap)
                            _client.GameState.LastMapChangeMs = (int)swMap.ElapsedMilliseconds;
                    }
                    finally { _client.GameState.IsChangingMap = false; }
                }

                // Check alive after map change
                if (_client.State == ClientState.Dead) break;

                // Log arrival
                int arrivedMap = _client.GameState.CurrentMap.MapId;
                if (arrivedMap != oldMap)
                {
                    _client.Log(string.Format("[Nav] -> {0} (map {1})",
                        _client.GameState.CurrentMap.MapName, arrivedMap));
                }

                // Dem GIUA hai lan doi map - o "Toc do NextMap" tab Train
                // (TrainConfig.NextMapDelayMs, mac dinh 1500ms = bang ZangVPS `av_0.java:1111`).
                // 0 = khong nghi them, chi cho su kien map nap.
                // CHANG CUOI thi BO QUA: luc do khong con lan doi map nao de giao cach nua, nguoi
                // goi (TrainMode) chi cho de... danh quai. Do duoc tren log 2026-09-05: quang
                // "chet -> ve toi bai" trung vi 4 giay, dong nay an 500ms trong do o MOI lan chet
                // (53/56 duong di la 1 chang duy nhat 72->41, tuc dem nay LUON la chang cuoi).
                if (i < path.Count - 1)
                {
                    int mapDelay = NhipQuaMap();
                    if (mapDelay > 0) SafeSleep(mapDelay);
                }

                // If didn't arrive at expected map, handle failure
                if (_client.GameState.CurrentMap.MapId != toMap)
                {
                    if (action.IsNPC)
                    {
                        if (action.IsArenaExit)
                        {
                            // KHONG blacklist: dich la Truong (1/27/72) - chan no se chan luon moi canh NPC
                            // toi Truong trong 120s. Zang cung chi de vong sau thu lai.
                            _client.Log(string.Format("[Nav] Roi nha thi dau map {0} khong xong - de tick sau thu lai", fromMap));
                        }
                        else if (action.NpcTemplateId == 25 && toMap == questMapId)
                        {
                            // NPC 25 di MAP NHIEM VU hong: KHONG blacklist kieu "truong:dich". BuildPath chan canh
                            // hub:dest cho MOI hub, ma map NV co the la CONG DI BO sat Truong (2, 3, 26, 28, 71, 39)
                            // => chan la cat luon cong that, acc taskId < 17 mat duong ~2 phut (review 2026-09-13,
                            // L1). Thay bang moc rieng tung acc: trong 120s tim duong COI NHU KHONG CO map NV
                            // (questMapId = -1) -> tat canh tat NPC 25, cong di bo van di duoc.
                            // (Truoc S6 thi khong chan gi ca -> tick sau lap lai dung chang hong.)
                            _npc25NvHongDen = DateTime.UtcNow.AddMilliseconds(NPC25_NV_NGHI_MS);
                            _client.Log(string.Format("[Nav] NPC 25 -> map NV {0} fail - tat canh tat NPC 25 trong {1}s",
                                toMap, NPC25_NV_NGHI_MS / 1000));
                        }
                        else
                        {
                            // 2026-09-13 (S6): moi NPC con lai (ke ca NPC 25 di 98/104/113) blacklist 120s. Cac dich
                            // nay KHONG phai cong di bo cua hub nao, nen chan hub:dest chi tat dung canh NPC.
                            // Canh bom trong BFS nay cung tuan danh sach chan (MapGraph.FindPath); ModMove bo qua
                            // diem dang bi chan.
                            // 2026-09-06: DA GO "reset vi tri bang doi zone" khoi cho nay (xem ghi chu
                            // chung o cuoi ham) - khong gan lai.
                            BlacklistNpcDestination(toMap);
                            _client.Log(string.Format("[Nav] NPC {0} -> map {1} fail, blacklist 120s", action.NpcTemplateId, toMap));
                        }
                    }
                    else
                    {
                        // Re-walk + re-send requestChangeMap — retry đến khi thành công
                        _client.Log(string.Format("[Nav] wp fail {0}->{1}, resend", fromMap, toMap));
                        int wpRetry = 0;
                        int maxWpRetry = 15;
                        int wpIdxUsed = -1;   // chi so waypoint da dung o lan thu cuoi (de danh dau hong)
                        while (!_stopped && _client.State != ClientState.Dead
                            && _client.GameState.CurrentMap.MapId == fromMap
                            && wpRetry < maxWpRetry)
                        {
                            wpRetry++;
                            // Delay tăng dần: 1s, 2s, 3s, ... max 5s + random jitter
                            int delay = Math.Min(wpRetry * 1000, 5000) + _rnd.Next(500);
                            SafeSleep(delay);

                            // 2026-09-07: DA GO "nhich lui 100px roi di lai waypoint".
                            // No tu GHI THANG vao MyChar.Cx mot toa do server chua he chap nhan, roi
                            // burst xuat phat tu do => goi move dau tien cach cho server biet 150px.
                            // Do la nguon sinh ra chuoi "[Pos] cmd52 ... lech 100,0" lap vo tan trong
                            // log 2026-09-07 (barbigz106: 78 dong, 22 phut dung yen mot pixel).
                            // ZangVPS KHONG co buoc nhich nao - chang hong thi no di lai waypoint tu
                            // dung cho dang dung (av_0.i, `av_0.java:787-1119`). Ta lam giong vay;
                            // cong doi soat o dau CharBurstMove lo phan keo ve moc server.
                            Thread.Sleep(300);

                            _client.MapChangeEvent.Reset();
                            int wpIdx2 = FindWaypointForTarget(fromMap, toMap);
                            if (wpIdx2 >= 0) { wpIdxUsed = wpIdx2; DoWaypointWalk(wpIdx2); }
                            _client.Movement.SendRequestChangeMap();

                            _client.MapChangeEvent.WaitOne(3000);

                            if (wpRetry % 5 == 0)
                                _client.Log(string.Format("[Nav] wp {0}->{1} retry #{2}", fromMap, toMap, wpRetry));
                        }
                        if (_client.GameState.CurrentMap.MapId != fromMap)
                        {
                            _client.Log(string.Format("[Nav] wp {0}->{1} OK after {2} retries", fromMap, toMap, wpRetry));
                            expectedMap = _client.GameState.CurrentMap.MapId;
                            continue;
                        }
                        if (wpRetry >= maxWpRetry)
                        {
                            // 15 lan di toi DUNG waypoint do ma server khong doi map => chinh chi so
                            // waypoint la SAI (no suy ra tu thu tu Connections[], khong phai du lieu map).
                            // Ghi lai TRUOC khi thoat: lan navigate sau FindWaypointForTarget se bo qua
                            // no va quet cac waypoint khac. Khong co dong nay thi moi vong lai chon
                            // dung cai sai do -> ket vinh vien (su co 2026-09-03, 17 acc).
                            if (wpIdxUsed >= 0) MarkWpDead(fromMap, wpIdxUsed, toMap);

                            // 2026-09-06: DA GO "reset vi tri bang doi zone" khoi cho nay (xem ghi chu
                            // chung o cuoi ham). Het retry thi blacklist + re-path luon.
                            // Fix #8: blacklist specific waypoint edge for 5 mins, force re-path qua đường khác
                            lock (_wpBlacklist)
                            {
                                _wpBlacklist[fromMap + ":" + toMap] = DateTime.UtcNow.AddMinutes(5);
                            }
                            _client.Log(string.Format("[Nav] wp {0}->{1} GIVE UP after {2} retries, blacklist 5m + re-path", fromMap, toMap, wpRetry));
                        }
                    }
                    break;
                }

                // Success: update expectedMap
                expectedMap = toMap;
            }

            return _client.GameState.CurrentMap.MapId == targetMap;
        }

        // ===================================================================================
        // DA GO vong leo thang doi zone ("RecoverStuckByZoneReset") - 2026-09-06
        // ===================================================================================
        // Y tuong cu: khi doi map that bai lien tiep, doi zone (z -> z^1) de EP server dat lai
        // char ve vi tri hop le tren cung map, roi re-path. Gioi han 2 lan/lan-navigate.
        //
        // VI SAO GO - do tren log that (fleet 45 acc, ~2 gio, Data/nsolite.log):
        //   - 16 lan chay "reset vi tri bang doi zone" -> 15 lan DUT KET NOI trong 5 giay (94%).
        //   - Doi khu qua NPC 13 noi chung thi VO HAI: 650 lan, chi 2% dut. Rieng 28 lan bi
        //     server day nguoc vi tri (cmd 52) ngay sau khi gui thi 57% dut.
        //   => Vong leo thang nay khong cuu duoc acc ket, no chi bien "ket" thanh "mat ket noi",
        //      roi acc phai xin dang nhap lai va thuong bi tu choi o bat tay hang chuc lan.
        //
        // DOI CHIEU ZangVPS (Java 2024, 247 file da go roi): ho KHONG CO gi tuong duong.
        //   3 duong doi khu cua ho (Z.java:735-850 / Z.java:1292-1341 / at_0.java:20-55) deu:
        //   gui opcode 28 -> cho toi da 2s -> XONG. Khong so khu dich, khong dem thanh/bai,
        //   khong thu lai, khong ep server dat lai vi tri. Hong thi im lang, cho het cooldown
        //   (10s, hoac 5s neu co kha di lenh), roi thu khu khac.
        //   Moc cooldown cua ho CHI duoc dong dau sau khi da gui that (Z.java:848) - moi nhanh
        //   bo cuoc deu khong reset moc, nen moi lan thu luon cach nhau du cooldown.
        //
        // User chot 2026-09-06: *"Cu lam giong zang nhe."*
        //
        // DUNG THEM LAI. Duong du phong dung dan da co san va nay chay thang: blacklist canh hong
        // (5 phut) + re-path qua duong khac, hoac MarkWpDead cho chi so waypoint sai.
        // Neu that su can chua "char ket o vi tri xau" thi cach dung la dong bo lai toa do theo
        // server (da co: PlayerHandler cmd 52 -> MyChar.Cx/Cy + SyncLastSent) roi DI BO LAI tu vi
        // tri server xac nhan - clone `ax.h()` cua Zang (ax.java:10118-10145: lech > 80px thi lui
        // ve moc cu roi di lai tung buoc 48px) - CHU KHONG PHAI gui them lenh doi khu.
        // ===================================================================================

        // ======================== TileMap.j() - WAYPOINT STEP ========================

        /// <summary>
        /// TileMap.j() WALK ONLY - calculate entry point and walk there.
        /// The actual requestChangeMap is sent separately (after Reset).
        /// </summary>
        private void DoWaypointWalk(int wpIdx)
        {
            // Chup phan tu ra bien local trong CUNG mot khoa: giua kiem tra Count va truy cap [wpIdx],
            // luong nhan co the Clear() danh sach khi doi map (TOCTOU - quy tac CLAUDE.md).
            var wpSrc = _client.GameState.CurrentMap.Waypoints;
            Waypoint wp;
            lock (wpSrc)
            {
                if (wpIdx < 0 || wpIdx >= wpSrc.Count) return;
                wp = wpSrc[wpIdx];
            }
            if (wp == null) return;
            var tiles = _client.GameState.Tiles;
            int mapW = tiles.IsLoaded ? tiles.MapWidthPx : 2000;
            int mapH = tiles.IsLoaded ? tiles.MapHeightPx : 1000;

            // Exact TileMap.j() entry point calculation
            int x = wp.MinX;
            int y = wp.MinY;

            if (wp.MinY != 0 && wp.MaxY < mapH - 24)
            {
                if (wp.MaxX <= mapW / 2)
                {
                    x = wp.MaxX + 12;
                    y = wp.MaxY;
                }
                else if (wp.MinX >= mapW / 2)
                {
                    x = wp.MinX - 12;
                    y = wp.MaxY;
                }
            }
            else if (wp.MaxY <= mapH / 2)
            {
                x = (wp.MaxX + wp.MinX) / 2;
                y = wp.MaxY + 24;
            }
            else if (wp.MinY >= mapH / 2)
            {
                x = (wp.MaxX + wp.MinX) / 2 + 24;
                y = wp.MaxY - 48;
            }

            _client.Log(string.Format("[Nav] wp[{0}]({1},{2})", wpIdx, x, y));

            // Char.b(x, y) - walk to entry point
            CharBurstMove((short)x, (short)y);

            // Fix #1: Double-send position với 100ms spacing để server confirm vị trí trước requestChangeMap.
            // SendMoveForce: goi nay CO CHU DICH trung toa do voi goi cuoi cua CharBurstMove o tren
            // - di qua chot chong trung cua SendMove la mat han buoc confirm, xin doi map se gui tu
            // vi tri server chua kip ghi nhan. Xem docs/features/DI_CHUYEN.md E.4.
            Thread.Sleep(100);
            _client.Movement.SendMoveForce((short)x, (short)y);
        }

        /// <summary>
        /// GameScr.b() WALK ONLY - find NPC and walk to it.
        /// The actual openMenu+menu is sent separately (after Reset).
        /// </summary>
        private void DoNpcWalk(int npcTemplateId)
        {
            var closest = FindNpc(npcTemplateId);
            if (closest == null)
            {
                _client.Log(string.Format("[Nav] NPC {0} not found. NPCs in map:", npcTemplateId));
                foreach (var n in _client.GameState.CurrentMap.Npcs)
                    _client.Log(string.Format("  tpl={0} ({1},{2})", n.TemplateId, n.X, n.Y));
                return;
            }

            var mc = _client.GameState.MyChar;
            _client.Log(string.Format("[Nav] NPC {0} ({1},{2}) | Me ({3},{4})",
                npcTemplateId, closest.X, closest.Y, mc.Cx, mc.Cy));

            // Char.b(npc.cx, npc.cy)
            CharBurstMove(closest.X, closest.Y);

            // Luôn confirm vị trí với server trước openMenu, kể cả khi char đứng sẵn cạnh NPC.
            // SendMoveForce vi ca hai goi nay CO CHU DICH trung toa do (va trung ca voi goi cuoi cua
            // CharBurstMove) - day chinh la muc dich cua chung. Xem DI_CHUYEN.md E.4.
            Thread.Sleep(100);
            _client.Movement.SendMoveForce(closest.X, closest.Y);
            Thread.Sleep(100);
            _client.Movement.SendMoveForce(closest.X, closest.Y);
        }

        // ======================== NHIP QUA MAP ========================

        /// <summary>
        /// Nhip nghi giua hai lan doi map = o "Toc do NextMap" cua tab Train (RIENG TUNG ACC), tru khi vua
        /// hoi sinh. Dung CHUNG cho luat goc (giua cac chang) va nhanh di duong dai (S9).
        ///
        /// CHUYEN VE BAI NGAY SAU KHI CHET: bo nhip giao cach (user chot 2026-09-08:
        /// *"Tôi muốn hồi sinh cái, next map tiếp theo luôn. Với nhân vật nào đi qua npc
        /// thì đến luôn"*). Duong ve bai thuong 3 chang (72->39->40->41) nen nhip 1500 ms
        /// an mat 3 giay MOI LAN CHET - ma acc train chet vai chuc lan moi gio.
        /// CO HAN GIO (REVIVE_FAST_TRIP_MS) de khong bien thanh "bo nhip vinh vien":
        /// het gio la moi lan doi map lai theo dung o "Toc do NextMap" cua tab Train.
        /// </summary>
        private int NhipQuaMap()
        {
            int mapDelay = _client.Config.Train != null
                ? _client.Config.Train.NextMapDelayMs
                : TrainConfig.DEFAULT_NEXT_MAP_DELAY_MS;

            if (mapDelay > 0 && _client.GameState.HoiSinhLucUtc != DateTime.MinValue
                && (DateTime.UtcNow - _client.GameState.HoiSinhLucUtc).TotalMilliseconds < REVIVE_FAST_TRIP_MS)
            {
                if (!_daBaoDiNhanh)
                {
                    _daBaoDiNhanh = true;
                    _client.Log(string.Format("[Nav] Vua hoi sinh -> di thang khong nghi {0}ms giua cac chang", mapDelay));
                }
                mapDelay = 0;
            }
            return mapDelay;
        }

        /// <summary>
        /// S9 (2026-09-13): nghi "Toc do NextMap" sau mot chang cua nhanh di duong dai neu da doi duoc map
        /// ma chua toi dich. Luat goc bo nhip o CHANG CUOI cua duong, ma moi chang di bo cua ModMove la
        /// mot duong MOT chang => truoc day di dai 9 chang = 9 lan xin doi map lien tuc khong nghi.
        /// ZangVPS nghi sau MOI lan doi map (av_0.java:1101-1111).
        /// </summary>
        private void NghiSauChangDiDai(int mapTruoc, int targetMap)
        {
            int now = _client.GameState.CurrentMap.MapId;
            if (_stopped || _client.State == ClientState.Dead || now == mapTruoc || now == targetMap) return;
            int d = NhipQuaMap();
            if (d > 0) SafeSleep(d);
        }

        /// <summary>Cac map dich dang bi chan NPC (con han). Snapshot - an toan de lap.</summary>
        private HashSet<int> NpcDichDangBiChan()
        {
            var s = new HashSet<int>();
            var now = DateTime.UtcNow;
            lock (_npcDestBlacklist)
            {
                foreach (var kv in _npcDestBlacklist)
                    if (kv.Value > now) s.Add(kv.Key);
            }
            return s;
        }

        // ======================== NPC: CHON MENU THEO CHU (clone ZangVPS cF.a) ========================

        /// <summary>Cho server tra goi 40 (menu NPC) - ZangVPS cF.java:1920 `GetVecTabNpc.a(2000L)`.</summary>
        private const int NPC_MENU_WAIT_MS = 2000;

        /// <summary>Da ghi log chon menu (khop / khong khop) cho truong hop nao - moi truong hop chi ghi MOT lan/acc.</summary>
        private readonly HashSet<string> _daLogMenu = new HashSet<string>();

        /// <summary>
        /// Chon menu NPC theo CHU "cap1$cap2" - clone ZangVPS <c>cF.a(int, String)</c> (cF.java:1836-1945).
        ///
        /// <para>Menu client goc hien ra = [cac dong SERVER gui trong goi 40] + [tieu de menu TEMPLATE
        /// cua NPC] (NINJAPC Controller.cs:1587-1615). Chon mot muc (GameCanvas.cs:2115-2148):</para>
        /// <list type="bullet">
        /// <item>dong server thu i -> <c>menu(npc, i, 0)</c></item>
        /// <item>muc template t KHONG co menu con -> <c>menu(npc, soDongServer + t, 0)</c></item>
        /// <item>muc template t CO menu con, chon muc con j -> <c>menu(npc, soDongServer + t, j)</c>
        /// (menu con mo TAI CHO, khong hoi server)</item>
        /// </list>
        /// <para>Zang quet ca danh sach theo dung thu tu do, muc dau tien khop la muc duoc chon.</para>
        ///
        /// <para>Tra false (= chang hong) khi server khong tra menu hoac khong khop chu. Zang KHONG lui ve
        /// chi so co dinh, ta cung vay.</para>
        /// </summary>
        private bool ChonMenuNpcTheoChu(int npcTpl, string chuoi)
        {
            string[] phan = chuoi.Split('$');
            var ev = _client.NpcMenuEvent;
            var dsServer = _client.NpcMenuOptions;
            if (ev == null || dsServer == null) return false;

            ev.Reset();
            _client.Npc.SendOpenMenu((short)npcTpl);
            if (!ev.WaitOne(NPC_MENU_WAIT_MS))
            {
                _client.Log(string.Format("[Nav] NPC {0}: server khong tra menu trong {1}ms - bo chang", npcTpl, NPC_MENU_WAIT_MS));
                return false;
            }

            List<string> dong;
            lock (dsServer) { dong = new List<string>(dsServer); }
            var tpl = _client.GameState.NpcStore != null ? _client.GameState.NpcStore.Get(npcTpl) : null;
            var menuTpl = tpl != null ? tpl.Menu : null;

            // --- Cap 1 ---
            int chiSo = -1;
            string[] menuCon = null;
            for (int i = 0; i < dong.Count; i++)
                if (KhopMenu(phan[0], dong[i])) { chiSo = i; break; }
            if (chiSo < 0 && menuTpl != null)
            {
                for (int t = 0; t < menuTpl.Count; t++)
                {
                    var m = menuTpl[t];
                    if (m != null && m.Length > 0 && KhopMenu(phan[0], m[0]))
                    {
                        chiSo = dong.Count + t;
                        menuCon = m;
                        break;
                    }
                }
            }
            if (chiSo < 0)
            {
                var ds = new List<string>(dong);
                if (menuTpl != null)
                    foreach (var m in menuTpl)
                        if (m != null && m.Length > 0) ds.Add("[tpl] " + m[0]);
                LogMenuHong(npcTpl, chuoi, phan[0], ds);
                return false;
            }

            if (phan.Length < 2)
            {
                LogMenuChon(npcTpl, chuoi, chiSo, 0);
                _client.Npc.SendNpcMenu((byte)npcTpl, (byte)chiSo, 0);
                return true;
            }

            // --- Cap 2, muc template CO menu con: menu con mo TAI CHO, khong hoi server (GameCanvas.cs:2123-2131) ---
            if (menuCon != null && menuCon.Length > 1)
            {
                for (int k = 1; k < menuCon.Length; k++)
                {
                    if (KhopMenu(phan[1], menuCon[k]))
                    {
                        LogMenuChon(npcTpl, chuoi, chiSo, k - 1);
                        _client.Npc.SendNpcMenu((byte)npcTpl, (byte)chiSo, (byte)(k - 1));
                        return true;
                    }
                }
                var dsCon = new List<string>();
                for (int k = 1; k < menuCon.Length; k++) dsCon.Add(menuCon[k]);
                LogMenuHong(npcTpl, chuoi, phan[1], dsCon);
                return false;
            }

            // --- Cap 2, muc cap 1 la dong SERVER hoac muc template KHONG co menu con: client gui menu(npc, chiSo, 0)
            // (GameCanvas.cs:2132-2135, 2146-2148) roi server tra menu MOI (goi 40), client lai ghep
            // [dong server moi] + [menu template] (Controller.cs:1598-1612) - Zang tim cap 2 tren ca danh sach do.
            // Review 2026-09-13 (L2/L3): ban truoc bo qua muc template khong co menu con (tra hong ma khong gui
            // goi nao) va chi tim cap 2 trong dong server.
            ev.Reset();
            _client.Npc.SendNpcMenu((byte)npcTpl, (byte)chiSo, 0);
            if (!ev.WaitOne(NPC_MENU_WAIT_MS))
            {
                _client.Log(string.Format("[Nav] NPC {0}: server khong tra menu cap 2 trong {1}ms - bo chang", npcTpl, NPC_MENU_WAIT_MS));
                return false;
            }
            lock (dsServer) { dong = new List<string>(dsServer); }
            for (int i = 0; i < dong.Count; i++)
            {
                if (KhopMenu(phan[1], dong[i]))
                {
                    LogMenuChon(npcTpl, chuoi, i, 0);
                    _client.Npc.SendNpcMenu((byte)npcTpl, (byte)i, 0);
                    return true;
                }
            }
            if (menuTpl != null)
            {
                for (int t = 0; t < menuTpl.Count; t++)
                {
                    var m = menuTpl[t];
                    if (m == null || m.Length == 0 || !KhopMenu(phan[1], m[0])) continue;
                    if (m.Length > 1) break;   // muc nay lai co menu con - chuoi 2 phan khong du de chon toi
                    LogMenuChon(npcTpl, chuoi, dong.Count + t, 0);
                    _client.Npc.SendNpcMenu((byte)npcTpl, (byte)(dong.Count + t), 0);
                    return true;
                }
            }
            var ds2 = new List<string>(dong);
            if (menuTpl != null)
                foreach (var m in menuTpl)
                    if (m != null && m.Length > 0) ds2.Add("[tpl] " + m[0]);
            LogMenuHong(npcTpl, chuoi, phan[1], ds2);
            return false;
        }

        /// <summary>
        /// So chu menu - clone ZangVPS <c>cF.a(bl_0, String)</c> (cF.java:1985-2006): mau ket thuc "@" thi
        /// so CHUA; con lai so BANG (khong phan biet hoa thuong, bo khoang trang dau cuoi); "nhiệm vụ"
        /// trong mau duoc coi bang "nv". Them chuan hoa Unicode dang C o ca hai ben de hai cach go dau
        /// cung mot chu khong lech nhau (Zang so hash chuoi tho).
        /// </summary>
        private static bool KhopMenu(string mau, string dong)
        {
            if (mau == null || dong == null) return false;
            string d = ChuanHoaChu(dong);
            string m = ChuanHoaChu(mau);
            if (mau.EndsWith("@") && d.Contains(ChuanHoaChu(mau.Replace("@", "")))) return true;
            if (m == d) return true;
            if (m.Contains("nhiệm vụ") && d.Contains("nv") && m.Replace("nhiệm vụ", "nv") == d) return true;
            return false;
        }

        private static string ChuanHoaChu(string s)
        {
            return s.Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim();
        }

        private void LogMenuHong(int npcTpl, string chuoi, string canTim, List<string> ds)
        {
            string key = "hong|" + npcTpl + "|" + chuoi + "|" + canTim;
            lock (_daLogMenu) { if (!_daLogMenu.Add(key)) return; }
            _client.Log(string.Format("[Nav] NPC {0}: KHONG khop chu '{1}' (chuoi '{2}') - bo chang. Menu thay duoc: [{3}]",
                npcTpl, canTim, chuoi, string.Join(" | ", ds.ToArray())));
        }

        /// <summary>
        /// F3 (review 2026-09-13): khop chu THANH CONG thi ghi MOT lan/acc chi so da chon - de doi chieu voi loi so
        /// cu (NPC 25 NV truoc day chay that bang menu (1,3)). Thieu dong nay thi luc chon dung khong de lai dau vet.
        /// </summary>
        private void LogMenuChon(int npcTpl, string chuoi, int m1, int m2)
        {
            string key = "ok|" + npcTpl + "|" + chuoi + "|" + m1 + "|" + m2;
            lock (_daLogMenu) { if (!_daLogMenu.Add(key)) return; }
            _client.Log(string.Format("[Nav] NPC {0}: chon menu ({1},{2}) theo chu '{3}'", npcTpl, m1, m2, chuoi));
        }

        /// <summary>
        /// cF.aq() cua ZangVPS = cot "type" trong servers.txt khac 0 (Hirosaki/Haruna = 1, server VN = 0).
        /// File servers.txt cua ta TRUNG tung ky tu voi file cua Zang (de_0.java:89 doc cot nay vao cF.az).
        /// </summary>
        private bool ServerTypeKhacKhong()
        {
            try
            {
                var sv = NSOKHODO.Protocol.ServerList.Resolve(_client.Config.ServerName, _client.Config.ServerIndex);
                return sv != null && sv.Type != 0;
            }
            catch { return false; }
        }

        // ======================== NHA THI DAU (map 0/56/73) ========================

        /// <summary>
        /// Roi Nha thi dau - clone ZangVPS av_0.java:1089-1098: lay NPC DAU TIEN trong map (bo qua neu
        /// status 15), di toi, roi gui <c>sub -103 byte(tpl)</c> + <c>cmd 29 (tpl,0,0)</c> +
        /// <c>cmd 47 (tpl,0)</c>. ⚠️ cmd 47 CHUA do hex tren server nay (xem Cmd.GET_TASK).
        /// </summary>
        private void DoArenaExit(int oldMap)
        {
            NpcState dau = null;
            try
            {
                // Npcs khong co khoa phia luong nhan (MapHandler Add / MapState.Reset Clear) -> boc try.
                var ds = _client.GameState.CurrentMap.Npcs;
                if (ds != null && ds.Count > 0) dau = ds[0];
            }
            catch { dau = null; }

            if (dau == null || dau.Status == 15)
            {
                _client.Log(string.Format("[Nav] Nha thi dau map {0}: {1} - khong roi duoc", oldMap,
                    dau == null ? "map khong co NPC" : "NPC dau tien dang an (status 15)"));
                return;
            }

            DoNpcWalk(dau.TemplateId);
            if (_client.State == ClientState.Dead) return;

            try
            {
                _client.MapChangeEvent.Reset();
                _client.GameState.IsChangingMap = true;
                byte tpl = (byte)dau.TemplateId;
                _client.Log(string.Format("[Nav] Roi nha thi dau map {0} qua NPC {1}", oldMap, tpl));
                _client.Items.SendRequestItem(tpl);      // sub -103 (Zang ad_0.q)
                _client.Npc.SendNpcMenu(tpl, 0, 0);      // cmd 29  (Zang ad_0.d)
                _client.Npc.SendGetTask(tpl, 0);         // cmd 47  (Zang ad_0.m)
                if (_client.GameState.CurrentMap.MapId == oldMap)
                    WaitForMapChange(WaitTimeout(), oldMap);
            }
            finally
            {
                _client.GameState.IsChangingMap = false;
            }
        }

        // ======================== Class_bd - ZONE CHANGE ========================

        // Template id vat pham chuyen khu - xem SERVER_FACTS.md §23.2.
        // 37 = "Vo han kha di lenh" (KHONG tieu hao). 35 = "Kha di lenh" (TIEU HAO).
        // CO Y CHI DUNG 37 (user chot 2026-09-06): NSOCHIP zc.java:50-52 co rot xuong 35, ta BO
        // nhanh do vi 30 clone nhay khu lien tuc se dot sach Kha di lenh. Muon bat lai: them 1 dong
        // `if (slot < 0) slot = FindBagSlot(KDL_SINGLE);` trong FindKdlSlot.
        private const int KDL_UNLIMITED = 37;

        // LUOI AN TOAN cho duong CHUA TUNG DUOC KIEM CHUNG tren server nay: gui cmd 28 kem O TUI
        // khi dang dung XA NPC 13. Ca MODGAME lan NSOCHIP deu lam vay, nhung tai lieu NSOCHIP ghi ro
        // "chua test in-game". Neu server nay khong nhan, trieu chung se la acc CO item 37 bi ket
        // khong doi duoc khu - im lang, rat kho doan.
        // => Hong 2 lan LIEN TIEP thi tu bo duong item, quay ve duong NPC 13 (cham hon nhung chac).
        //    Doi 2 lan chu khong 1: mot lan hong con co the do server ban / goi roi.
        private int _kdlFailStreak;
        private bool _kdlDisabled;

        /// <summary>
        /// Cooldown giua hai lan doi khu KHI CO vat pham (ms) - clone ZangVPS (ZNinjaPro Z.B(),
        /// Z.java:859). Truoc day la 800 (lay tu MODGAME Auto.a).
        ///
        /// Vi sao tin 5000 hon 800: ba nguon doc lap deu noi LAU HON - client goc chot 10000
        /// (NSOCHIP zc.java ghi "stock 10000"), ZangVPS la bot THUONG MAI dang chay that tren cung
        /// dong game va chot 5000, va chinh SERVER tra ve hop thoai "Change zone after Ns" khi ta
        /// ban qua day. MODGAME la nguon DUY NHAT noi 800, va chua bao gio duoc kiem chung tren
        /// server nay. Xem docs/features/DOI_KHU_ZANGVPS.md muc B.
        /// </summary>
        private const int ZONE_CD_ITEM_MS = 5000;
        /// <summary>Cooldown khi KHONG co vat pham (phai qua NPC 13). ZangVPS + client goc: 10000.</summary>
        private const int ZONE_CD_NPC_MS = 10000;
        /// <summary>
        /// Tran cho map ve SAU khi gui lenh doi khu.
        ///
        /// 2026-09-07: 3000 -> 2000, cho BANG <see cref="WAIT_MAP"/>.
        ///
        /// ⚠️ DINH CHINH chu thich cu o day. Ban cu ghi *"ZangVPS khong cho gi ca (Z.m ket thuc
        /// bang w = now)"* - DOC THIEU. Doc lai nguyen van thi CA HAI duong doi khu cua Zang deu
        /// goi `av_0.q()` (= cho chot "WaitObject" 2000 ms) NGAY TRUOC khi dong dau cooldown:
        ///     Z.java:841-848  `ad_0.a().a(zone, item); v73.k = zone; av_0.q(); ... w = now;`
        ///     Z.m(int)        cung khuon
        /// Tuc Zang dung DUNG MOT con so 2000 cho ca doi map lan doi khu. Con 3000 la ta tu dat.
        ///
        /// Ly do GIU viec cho (khong bo han): `MapChangeEvent` la thu DUY NHAT bao mang mob da nap
        /// xong. Bo han thi tick sau chay tren map nap do - dung lop loi da phai sua o kich yen
        /// (KICH_YEN.md 9.3). Cho nay Zang cung cho, nen giu la BAM SAT chu khong phai sang tao.
        /// </summary>
        private const int WAIT_ZONE = 2000;   // = WAIT_MAP, ZangVPS av_0.q()

        /// <summary>Dem so lan doi khu KHONG vao duoc - de chot con so cooldown dung bang so lieu that.</summary>
        private int _zoneFailCount, _zoneOkCount;
        private DateTime _zoneStatLogAt = DateTime.MinValue;

        // Ghi chu: 6 map hang {99,103,134,135,136,137} khong co NPC 13 nhung VAN doi khu duoc -
        // voi dieu kien co KDL. Ta KHONG can bang tra rieng: nhanh co-KDL ben duoi bo qua han
        // khau tim NPC 13, nen 6 map do tu dong chay dung. Xem SERVER_FACTS.md §23.5.

        /// <summary>
        /// Doi khu (cmd 28). Clone MODGAME Auto.a(int) + NSOCHIP zc.a(int).
        ///
        /// Byte thu hai cua cmd 28 KHONG phai hang -1 ma la **O TUI cua vat pham chuyen khu**
        /// (SERVER_FACTS.md §23). Co vat pham thi:
        ///   - KHONG can NPC 13, KHONG phai chay toi dau ca;
        ///   - cooldown ngan hon (xem ZONE_CD_ITEM_MS vs ZONE_CD_NPC_MS).
        /// Khong co thi giu nguyen duong cu: di toi NPC 13 roi gui cmd 28 voi indexUI = -1.
        ///
        /// <para><b>KHONG chan luong.</b> Con cooldown thi tra <c>false</c> NGAY, khong ngu bu -
        /// clone ZangVPS <c>Z.B()</c> (chi <c>return</c>). Truoc day ham nay <c>SafeSleep</c> het
        /// phan cooldown con lai (toi 5,1 s) ROI moi cho map ve them 10 s: mot lan bi tu choi ton
        /// ~15,2 giay ma tai khoan KHONG lam gi ca - khong danh, khong uong binh mau, khong bao
        /// elite. Nay: cooldown khong ngu, va cho map ve rut con <see cref="WAIT_ZONE"/> = 2 s
        /// (2026-09-07, bang ZangVPS `av_0.q()`).</para>
        /// </summary>
        /// <returns><c>true</c> = da GUI lenh doi khu (khong hua la doi duoc);
        /// <c>false</c> = chua gui (con cooldown, da dung dung khu, hoac khong co duong doi).</returns>
        public bool DoZoneChange(byte targetZone)
        {
            byte oldZone = _client.GameState.CurrentMap.ZoneId;
            if (oldZone == targetZone) return false;

            int kdl = FindKdlSlot();
            int cooldownMs = kdl >= 0 ? ZONE_CD_ITEM_MS : ZONE_CD_NPC_MS;

            // Con cooldown -> ve NGAY, KHONG ngu bu. Caller (Step 2 / TryHopZone / NavToPkSpot) deu
            // co nhip rieng va se goi lai o tick sau; ngu o day la chan ca tick cua tai khoan.
            if ((DateTime.UtcNow - _lastZoneChange).TotalMilliseconds < cooldownMs)
                return false;

            _client.Log(string.Format("[Nav] Doi zone {0} -> {1} (cd={2}ms, {3})",
                oldZone, targetZone, cooldownMs, kdl >= 0 ? "kha di lenh o " + kdl : "qua NPC 13"));

            // Game: GameScr.i(13). CHI can khi KHONG co vat pham.
            if (kdl < 0)
            {
                NpcState pillar = FindNpc(13);
                if (pillar == null)
                {
                    // Khong NPC 13 + khong KDL: chi 6 map hang moi doi duoc, va chung cung doi KDL.
                    // Truoc day cho nay `return` im lang - rat kho doan vi sao bot dung yen.
                    _client.Log(string.Format(
                        "[Nav] Khong doi duoc khu {0}: map {1} khong co NPC 13 va tui khong co Vo han kha di lenh (tpl {2})",
                        targetZone, _client.GameState.CurrentMap.MapId, KDL_UNLIMITED));
                    _lastZoneChange = DateTime.UtcNow;   // dung dap moi tick
                    return false;
                }

                // Game: if(abs(npc.cx-char.cx)>22 || abs(npc.cy-char.cy)>22) Char.b(npc.cx,npc.cy)
                if (Math.Abs(pillar.X - _client.GameState.MyChar.Cx) > 22 ||
                    Math.Abs(pillar.Y - _client.GameState.MyChar.Cy) > 22)
                {
                    CharBurstMove(pillar.X, pillar.Y);
                }
            }

            // GIU cmd 36 truoc cmd 28, du ca MODGAME (Auto.a) lan NSOCHIP (zc.a) deu gui thang cmd 28
            // khi da biet khu dich (SERVER_FACTS.md §23.4). Hai ly do co chu dich:
            //  1. Duong nay DANG CHAY TOT tren server nay tu lau. Bo mot goi khoi "cho chac" la doi
            //     hanh vi tren duong da kiem chung, doi lay dung 1 goi tin - khong dang. Neu server
            //     nay lai doi man hinh khu phai mo truoc thi bo di la doi khu chet HANG LOAT.
            //  2. Goi 36 lam server tra ve mang dan so khu => ZonePlayerCounts luon tuoi MIEN PHI,
            //     nho vay EnsureZoneCounts gan nhu khong bao gio phai chan tick 2s de di xin.
            // Muon bo thi tach thanh mot thay doi rieng, co ban test rieng.
            _client.MapChangeEvent.Reset();
            _client.Movement.SendOpenZoneList();
            _client.Movement.SendChangeZone(targetZone, unchecked((byte)kdl));

            // Cho map ve. 2026-09-07: WAIT_ZONE == WAIT_MAP == 2000 - ZangVPS dung DUNG MOT con
            // so `av_0.q()` = 2000 ms cho ca doi map lan doi khu (Z.java:841-848). Chu thich cu o
            // day ghi "NGAN hon doi map" da het dung ke tu luc ha WAIT_MAP 10000 -> 2000.
            WaitForZoneChange(WAIT_ZONE, oldZone);

            bool ok = _client.GameState.CurrentMap.ZoneId == targetZone;
            if (ok) _zoneOkCount++; else _zoneFailCount++;
            LogZoneStats();

            if (ok)
            {
                _client.Log(string.Format("[Nav] Zone -> {0}", targetZone));
                _kdlFailStreak = 0;
            }
            else if (kdl >= 0)
            {
                // Chi dem khi vua di duong ITEM - hong o duong NPC 13 la chuyen khac (khu day, ket...).
                if (++_kdlFailStreak >= 2)
                {
                    _kdlDisabled = true;
                    _client.Log(
                        "[Nav] Doi khu bang \"Vo han kha di lenh\" hong 2 lan lien - server co ve KHONG nhan "
                        + "cmd 28 khi dung xa NPC 13. Tu chuyen ve duong di toi NPC 13 (cham hon nhung chac).");
                }
            }

            SafeSleep(100);
            _lastZoneChange = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// In ti le doi khu thanh cong 60 giay/lan. Muc dich la CHOT con so cooldown bang so lieu
        /// that thay vi cai nhau giua 3 nguon (MODGAME 800, ZangVPS 5000, client goc 10000):
        /// ti le hong cao = cooldown con qua ngan.
        /// </summary>
        private void LogZoneStats()
        {
            if ((DateTime.UtcNow - _zoneStatLogAt).TotalMilliseconds < 60000) return;
            _zoneStatLogAt = DateTime.UtcNow;
            int tong = _zoneOkCount + _zoneFailCount;
            if (tong == 0) return;
            _client.Log(string.Format("[Nav] Doi khu 60s qua: {0} duoc / {1} hong (cd {2}/{3} ms)",
                _zoneOkCount, _zoneFailCount, ZONE_CD_ITEM_MS, ZONE_CD_NPC_MS));
            _zoneOkCount = 0;
            _zoneFailCount = 0;
        }

        /// <summary>
        /// O tui cua "Vo han kha di lenh" (tpl 37), -1 neu khong co. Clone MODGAME Char.g(int):
        /// tra CHI SO trong arrItemBag, khong phai true/false.
        /// </summary>
        private int FindKdlSlot()
        {
            // NSOKHODO - SUA CO CHU DICH (docs/NGUON_GOC.md): KHONG BAO GIO doi khu bang vat pham.
            // Trong kho, "Vo han kha di lenh" nam trong tui co the la DO GUI KHO cua nguoi khac, va do
            // chi bi khoa khi dem ra dung (test tay T0). Lang Tone co NPC 13 (test tay T9) nen luon di
            // duong NPC - cham hon 5 giay hoi chieu nhung khong dung vao hang cua ai.
            if (_kdlDisabled) return -1;
            return -1;
        }

        /// <summary>O tui dau tien chua item co templateId (game: Char.g()). -1 = khong co.</summary>
        private int FindBagSlot(int templateId)
        {
            var items = _client.GameState.MyChar.BagItems;
            if (items == null) return -1;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it != null && !it.IsEmpty && it.TemplateId == templateId) return i;
            }
            return -1;
        }

        // ======================== Char.b() - BURST MOVE ========================

        /// <summary>
        /// Exact replica of Char.b(int var0, int var1).
        /// </summary>
        /// <summary>
        /// Di toi mob CO CONG "o dich dung duoc khong" - clone NSOTRUNGDUC <c>Class_ba.c</c>
        /// (Class_ba.java:10250):
        /// <code>
        ///   return !Class_gj.a(x, y, out) ? false : Class_ba.b(out[0], out[1]);
        /// </code>
        /// Khong tim duoc o co nen quanh dich -> tra <c>false</c>, KHONG gui goi nao; caller
        /// (<c>TrainMode.MoveToMob</c>) bo con quai do, dung nhu <c>Class_ad.c</c> dat <c>me.cs = null</c>.
        /// Tim duoc thi di toi O DA HIEU CHINH, khong phai toa do mob tho.
        ///
        /// ⚠️ CO Y tach RIENG khoi <see cref="CharBurstMove"/>: ban goc dat cong nay o
        /// <c>Class_ba.c</c> va <c>Class_ba.c</c> co DUNG MOT noi goi (Class_ad.java:553 - lao toi
        /// mob); ~40 noi con lai deu goi thang <c>Class_ba.b</c> KHONG cong. Nhet cong vao
        /// CharBurstMove se chan ca nhung duong CO CHU DICH di toi cho khong co nen - ro nhat la
        /// <c>SuicideFall</c> (di toi DAY MAP de roi xuong vuc hoi MP) va cac buoc waypoint o ria map.
        /// </summary>
        public bool CharBurstMoveToMob(short targetX, short targetY)
        {
            var tiles = _client.GameState.Tiles;
            if (tiles != null && tiles.IsLoaded)
            {
                int sx, sy;
                if (!tiles.TryFindStandable(targetX, targetY, out sx, out sy))
                    return false;
                targetX = (short)sx;
                targetY = (short)sy;
            }
            return CharBurstMove(targetX, targetY);
        }

        public bool CharBurstMove(short targetX, short targetY)
        {
            var state = _client.GameState;
            int cx = state.MyChar.Cx;
            int cy = state.MyChar.Cy;

            // TRA VE giong Char.b cua ban goc (MODGAME Char.java:8309, NSOTRUNGDUC Class_ba.c):
            // FALSE = "khong gui lenh di nao" (da dung dung cho / dang chet). NSOTRUNGDUC dung
            // chinh gia tri nay lam chot: di khong duoc thi KHONG nhan mob lam focus
            // (Class_ad.c: `me.cs = null`) - xem TrainMode.MoveToMob.
            if (targetX == cx && targetY == cy) return false; // Game: if(x==cx && y==cy) return false
            if (_client.State == ClientState.Dead) return false;

            // (Log dd12 "Lao NHAY TANG" da doi xuong duoi - sau khi biet co di theo dia hinh khong.)

            // ===== LUOI AN TOAN (khong phai ban sua goc re) - y tuong tu `ax.h()` (ax.java:10128-10138) =====
            // ⚠️ DOC KY TRUOC KHI DUA VAO NO: cong nay HIEM KHI NO, va do la CO Y.
            //   Ta chi co DUNG HAI cho ghi MyChar.Cx/Cy ngoai handler mang: doan nay, va cuoi
            //   CharBurstMove - ma cho cuoi do nam KEP GIUA cac loi goi SendMoveForce(target), nen
            //   MyChar va _lastSent luon duoc dat cung mot luc. cmd 52 cung dat CA HAI
            //   (PlayerHandler.HandleServerSetPos). => do lech >80px gan nhu khong ton tai.
            //   Truong hop no THAT SU no: burst thoat giua chung (nhan vat chet giua duong di) -
            //   luc do _lastSent da di xa ma MyChar con o cho cu.
            //
            // ❌❌ 2026-09-07 (dd): DA THU doi nguon so sanh sang `TryGetConfirmedPos` (vi tri server
            //   xac nhan) va PHAI GO LAI - no LAM HONG, do duoc tren log that:
            //     43 lan no trong 67 giay, dang: `ta tuong (588,384), server biet (516,72)`.
            //     (516,72) la DIEM VAO map 41 do MAP_INFO vua dat. Nhan vat di toi quai HOP LE, server
            //     IM LANG (= dong y), nhung vi MAP_INFO cung lam moc do "con moi" nen cong nay nghi la
            //     lech va KEO NGUOC MyChar ve tan cua map => bot tuong minh dang o cua map, di lai tu
            //     dau, va cu ly tinh tam danh sai theo.
            //   Han su dung (POS_RESYNC_FRESH_MS) KHONG cuu duoc: cai bay khong phai moc CU, ma la moc
            //   MOI TINH nhung da bi chinh buoc di hop le cua ta vuot qua. Server chi noi khi TU CHOI;
            //   im lang = dong y - nen khong ton tai moc "server biet ta dang o dau" de so.
            //   `_lastSent` moi la thu dung o day: no la cai TA THUC SU DAT LEN DUONG TRUYEN.
            //   Va cong nay VO CAN voi dong bang: acc ket co MyChar == vi tri server ghim (cmd52 dat
            //   ca hai), nen dai luong lech = 0 du so kieu gi. Duong thoat ket la P3 (relogin).
            // Nguong 80 lay nguyen cua Zang.
            short lsx, lsy;
            if (_client.Movement.TryGetLastSent(out lsx, out lsy)
                && (Math.Abs(cx - lsx) > POS_RESYNC_PX || Math.Abs(cy - lsy) > POS_RESYNC_PX))
            {
                // PHANH LOG: ham nay nam tren duong di chuyen nong nhat (MoveToMob goi ~10 lan/giay).
                // Neu co mot ca benh ly lam do lech tai dien thi khong phanh se nhan chim file log
                // cua ca 150 acc. Cung khuon 5 giay/acc nhu PlayerHandler.LogPosOverride.
                if ((DateTime.UtcNow - _lastResyncLogAt).TotalMilliseconds >= POS_RESYNC_LOG_GAP_MS)
                {
                    _lastResyncLogAt = DateTime.UtcNow;
                    _client.Log(string.Format("[Pos] Doi soat truoc khi di: ta tuong ({0},{1}), server biet ({2},{3}) - lay lai moc server",
                        cx, cy, lsx, lsy));
                }
                cx = lsx;
                cy = lsy;
                state.MyChar.Cx = lsx;
                state.MyChar.Cy = lsy;
                if (targetX == cx && targetY == cy) return false;   // moc that trung dich -> khoi di
            }

            var tiles = state.Tiles;
            var nhip = LayNhipBuoc();   // "Han che 900s" doi do dai buoc + phanh (ca hai nhanh ben duoi)

            // ===== 2026-09-13 (ty3.5): DICH O TANG THAP HON -> DI THEO DIA HINH =====
            // Nhanh ben duoi (ban clone Char.b) ghim Y moi goi theo tang DICH => goi dau da "roi" ca tram px,
            // server bac va keo ve - acc dung chet (log 2026-09-13: 10 acc ket o map 35, cong ra o day).
            // Tim duong chi gom di ngang + roi (FloorPath, luat tuong/nen lay tu Char.cs client goc) roi gui
            // tung buoc. KHONG co duong (can leo, bi tuong bit) => giu NGUYEN cach cu ben duoi.
            // Chieu LEN khong dung toi (user chot 2026-09-13).
            int nenDichY;
            var duongXuong = LayDuongXuong(tiles, cx, cy, targetX, targetY, out nenDichY);

            // ===== 2026-09-08 dd12: GHI VET CU LAO NHAY TANG (chi ghi log, khong doi hanh vi) =====
            // Nhanh cu CHI co buoc truc X (curX +/- STEP_PX, khong he dung toi curY). Dich o tang nen khac
            // thi di ngang bao nhieu cung khong toi, va moi vong lai ban them mot loat goi.
            // Do dem 2026-09-08: do lech truc Y cua cmd52 binh thuong la 0-96px (963 lan la 24px),
            // nhung **432px** thi hiem - va CA HAI lan no xuat hien deu dan thang toi dut ket noi
            // (barbigz110 luc 02:53:07, barbigz104 luc 03:00:05). Xem docs/features/DONG_BANG_VI_TRI.md.
            // 2026-09-13: doi xuong SAU khi biet co di theo dia hinh khong - di theo dia hinh roi thi cau
            // "khong co buoc truc Y" khong con dung, dung ghi.
            int lechY = Math.Abs(targetY - cy);
            if (duongXuong == null && lechY >= BURST_LOG_DY_PX
                && (DateTime.UtcNow - _lastBurstDyLogAt).TotalMilliseconds >= BURST_LOG_GAP_MS)
            {
                _lastBurstDyLogAt = DateTime.UtcNow;
                _client.Log(string.Format(
                    "[Burst] Lao NHAY TANG: tu ({0},{1}) -> ({2},{3}) | lech {4} ngang, {5} DOC | "
                    + "~{6} goi di. Ham nay khong co buoc truc Y nen dich nay co the khong toi duoc.",
                    cx, cy, targetX, targetY,
                    Math.Abs(targetX - cx), lechY,
                    (Math.Abs(targetX - cx) + STEP_PX - 1) / STEP_PX));
            }

            if (duongXuong != null)
            {
                if (!DiTheoDuongXuong(duongXuong, cx, cy, targetX, nenDichY, nhip)) return false;
                _client.Movement.SendMove(targetX, targetY);
            }
            // KHONG co cong "o dich dung duoc khong" o day - day la ban sao cua Class_ba.b, ban goc
            // de cong do o Class_ba.c (xem CharBurstMoveToMob). Nhet vao day se chan ca SuicideFall
            // (di toi DAY MAP) va cac buoc waypoint o ria map.
            else if (tiles.IsLoaded)
            {
                // Game: var4 = TileMap.a(var0, var1-12, 64) ? TileMap.b(var1)-24 : var1
                int adjustedY;
                if (tiles.HasFlag(targetX, targetY - 12, 64))
                    adjustedY = GameData.TileEngine.AlignToTile(targetY) - 24;
                else
                    adjustedY = targetY;

                // Game: horizontal 50px steps
                // PHANH - clone Class_ba.b (Class_ba.java:10218-10222): `if (++dem <= 20) continue;
                // Thread.sleep(100L);` tuc cu 20 goi move thi nghi 100ms. Ta TRUOC DAY khong co
                // phanh nao -> burst 1700px = 34 goi ban lien tuc (log "[Flood] 29 goi/giay").
                int demGoi = 0;
                int curX = cx;
                if (targetX > curX)
                {
                    while (true)
                    {
                        curX += nhip.Step;
                        if (curX >= targetX) break;
                        if (_client.State == ClientState.Dead) return false;
                        _client.Movement.SendMove((short)curX, (short)tiles.FindGround(curX, adjustedY));
                        Phanh(ref demGoi, nhip);
                    }
                }
                else if (targetX < curX)
                {
                    while (true)
                    {
                        curX -= nhip.Step;
                        if (curX <= targetX) break;
                        if (_client.State == ClientState.Dead) return false;
                        _client.Movement.SendMove((short)curX, (short)tiles.FindGround(curX, adjustedY));
                        Phanh(ref demGoi, nhip);
                    }
                }

                // Game: charMove(var0, var1) - final
                _client.Movement.SendMove(targetX, targetY);
            }
            else
            {
                _client.Movement.SendMove(targetX, targetY);
            }

            // Game (Char.b - MODGAME Char.java:8200-8205): charMove(target) -> gan cx/cy ->
            // charMove(cx,cy). Hai goi dich di LIEN NHAU, KHONG co sleep o giua (ban goc chay
            // duoi 1ms roi de thread Sender flush). Sleep(5) truoc day la tu che, khong co trong
            // ban goc, va chan thread o MOI lan di chuyen.
            // Goi dich - clone Class_ba.b (Class_ba.java:10240-10246):
            //     charMove(x,y); sleep(20); charMove(x,y); cx=cxSend=x; cy=cySend=y; charMove(cx,cy);
            // BA lan, co ngu 20ms giua lan 1 va 2. Truoc day ta gui 2 lan lien nhau khong ngu -
            // ghi chu cu bao "ban goc chay duoi 1ms roi de thread Sender flush", doc lai Class_ba.b
            // thi ban goc CO ngu 20ms va CO goi thu ba.
            // BA goi dich - dung SendMoveForce vi ban goc gui THAT ca ba (Class_ba.java:10240-10247);
            // di qua SendMove thi chot chong trung nuot mat goi 2 va 3.
            _client.Movement.SendMoveForce(targetX, targetY);
            Thread.Sleep(FINAL_MOVE_GAP_MS);
            _client.Movement.SendMoveForce(targetX, targetY);
            state.MyChar.Cx = targetX;
            state.MyChar.Cy = targetY;
            _client.Movement.SendMoveForce(targetX, targetY);
            return true;
        }

        /// <summary>
        /// Duong "chi di ngang + roi" toi dich thap hon (FloorPath), co nho ket qua. <c>null</c> = khong ap dung
        /// hoac khong co duong => CharBurstMove giu nhanh cu.
        /// </summary>
        private List<GameData.FloorPath.DiemMoc> LayDuongXuong(GameData.TileEngine tiles, int cx, int cy,
            short targetX, short targetY, out int nenDichY)
        {
            nenDichY = targetY;
            if (tiles == null || !tiles.IsLoaded) return null;
            if (targetY - cy < DOWN_MIN_PX) return null;   // chi chieu XUONG
            // SuicideFall (HeNenRunner/DapDoRunner/BanDoRunner gui (cx, MapHeightPx)) CO Y roi thang xuong vuc:
            // dich = MapHeightPx nam NGOAI luoi o => FloorPath khong thay nen o dich => null => nhanh cu.
            // KHONG chan "cung cot": quai/NPC nam thang ben duoi van can di vong qua lo (log 2026-09-13:
            // dotay116 (732,192) -> (732,672)).

            int map = _client.GameState.CurrentMap.MapId;
            int sc = cx / 24, sr = cy / 24, gc = targetX / 24, gr = targetY / 24;
            var now = DateTime.UtcNow;
            var c = _duongXuong;
            if (c != null && c.Map == map && c.Sc == sc && c.Sr == sr && c.Gc == gc && c.Gr == gr && now < c.HetHan)
            {
                nenDichY = c.NenDichY;
                return c.Moc;
            }

            var moc = GameData.FloorPath.TimDuongXuong(tiles, cx, cy, targetX, targetY, out nenDichY);
            _duongXuong = new DuongXuongCache
            {
                Map = map, Sc = sc, Sr = sr, Gc = gc, Gr = gr, NenDichY = nenDichY, Moc = moc,
                HetHan = now.AddMilliseconds(DUONG_XUONG_CACHE_MS)
            };

            // Phanh log cung khuon 5 giay/acc voi log burst - day la duong nong nhat.
            if ((now - _lastDuongXuongLogAt).TotalMilliseconds >= BURST_LOG_GAP_MS)
            {
                _lastDuongXuongLogAt = now;
                if (moc != null)
                    _client.Log(string.Format("[Burst] Di XUONG theo dia hinh: ({0},{1}) -> ({2},{3}) | {4} lan roi, nen dich y={5}",
                        cx, cy, targetX, targetY, moc.Count / 2, nenDichY));
                else if (targetY - cy >= BURST_LOG_DY_PX)
                    _client.Log(string.Format("[Burst] Khong co duong chi di ngang + roi: ({0},{1}) -> ({2},{3}) (can leo / tuong bit / dich khong co nen) - giu cach cu",
                        cx, cy, targetX, targetY));
            }
            return moc;
        }

        /// <summary>
        /// Gui goi theo duong FloorPath: di ngang toi mep lo, roi tung buoc FALL_STEP_PX, lap lai; cuoi cung di
        /// ngang tren tang dich toi SAT dich (goi dich do CharBurstMove gui, giu nguyen hop dong). Dung chung bo
        /// dem phanh voi nhanh cu. <c>false</c> = chet giua duong.
        /// </summary>
        private bool DiTheoDuongXuong(List<GameData.FloorPath.DiemMoc> moc, int cx, int cy, short targetX, int nenDichY, NhipBuoc nhip)
        {
            int curX = cx, curY = cy;
            int demGoi = 0;
            foreach (var p in moc)
            {
                if (p.X != curX)
                {
                    // Di ngang tren nen tang hien tai (Y cua moc = nen that, khong dung Y lech cua nhan vat).
                    if (!BuocNgang(ref curX, p.Y, p.X, true, ref demGoi, nhip)) return false;
                    curY = p.Y;
                }
                while (curY < p.Y)
                {
                    curY = Math.Min(curY + FALL_STEP_PX, p.Y);
                    if (_client.State == ClientState.Dead) return false;
                    _client.Movement.SendMove((short)curX, (short)curY);
                    Phanh(ref demGoi, nhip);
                }
            }
            return BuocNgang(ref curX, nenDichY, targetX, false, ref demGoi, nhip);
        }

        /// <summary>Buoc <c>nhip.Step</c> theo truc X o do cao y. <paramref name="guiDiemCuoi"/> = false: dung truoc diem cuoi (giong vong burst cu).</summary>
        private bool BuocNgang(ref int curX, int y, int toiX, bool guiDiemCuoi, ref int demGoi, NhipBuoc nhip)
        {
            while (curX != toiX)
            {
                int buoc = toiX > curX ? nhip.Step : -nhip.Step;
                curX += buoc;
                if ((buoc > 0 && curX >= toiX) || (buoc < 0 && curX <= toiX))
                {
                    curX = toiX;
                    if (!guiDiemCuoi) break;
                }
                if (_client.State == ClientState.Dead) return false;
                _client.Movement.SendMove((short)curX, (short)y);
                Phanh(ref demGoi, nhip);
            }
            return true;
        }

        // ======================== PATHFINDING ========================

        /// <summary>
        /// BFS with task restrictions (exact game logic) + NPC destination blacklist.
        /// </summary>
        private List<int> BuildPath(int source, int target, int questMapId, bool ignoreTaskGate = false)
        {
            // Build broken links set: permanent + NPC dest blacklist + temp wp blacklist
            HashSet<string> broken;
            lock (_brokenLinks) { broken = new HashSet<string>(_brokenLinks); }

            // Waypoint temp blacklist (per-instance, expires)
            var now = DateTime.UtcNow;
            lock (_wpBlacklist)
            {
                var expired = new List<string>();
                foreach (var kv in _wpBlacklist)
                {
                    if (kv.Value > now)
                        broken.Add(kv.Key);
                    else
                        expired.Add(kv.Key);
                }
                foreach (var k in expired) _wpBlacklist.Remove(k);
            }

            // NPC destination blacklist: block all crossroad/village → dest edges
            lock (_npcDestBlacklist)
            {
                var expired = new List<int>();
                foreach (var kv in _npcDestBlacklist)
                {
                    if (kv.Value > now)
                    {
                        int dest = kv.Key;
                        // Block ALL NPC edges TO this destination
                        foreach (int hub in new[] { 1, 10, 17, 22, 27, 32, 38, 43, 48, 72 })
                            broken.Add(hub + ":" + dest);
                    }
                    else
                        expired.Add(kv.Key);
                }
                foreach (var k in expired) _npcDestBlacklist.Remove(k);
            }

            var myChar = _client.GameState.MyChar;
            return MapGraph.FindPath(source, target, broken, myChar.TaskId, myChar.IsHuman, questMapId, ignoreTaskGate,
                _client.GameState.CanCuDiaMap);
        }

        /// <summary>
        /// Find waypoint index for fromMap -> toMap.
        /// </summary>
        private int FindWaypointForTarget(int fromMap, int toMap)
        {
            // First check cache (learned from previous runs)
            int cached = FindCachedWaypoint(fromMap, toMap);
            if (cached >= 0)
            {
                _client.Log(string.Format("[Nav] Cached: map{0} wp[{1}] -> map{2}", fromMap, cached, toMap));
                return cached;
            }

            // Chi so suy ra tu thu tu MapGraph.Connections[] - PHONG DOAN, khong phai du lieu map.
            // Da chung minh sai (nam trong _wpDeadIdx) thi BO QUA, xuong nhanh quet-de-hoc ben duoi.
            int idx = MapGraph.FindWaypointIndex(fromMap, toMap);
            if (idx >= 0 && !IsWpDead(fromMap, idx, toMap)) return idx;

            // Fallback: try all waypoints with learning
            return TryLearnWaypoint(fromMap, toMap);
        }

        /// <summary>Try each waypoint to learn which one leads to toMap.</summary>
        /// <param name="daMoVan">
        /// true = lan goi nay la lan THU LAI sau khi van an toan da xoa dau chet. Chan de quy:
        /// van chi duoc mo dung mot lan cho moi lan goi tu ben ngoai.
        /// </param>
        private int TryLearnWaypoint(int fromMap, int toMap, bool daMoVan = false)
        {
            // SNAPSHOT truoc khi lap (quy tac thread-safety cua CLAUDE.md): Waypoints la List<> bi
            // luong NHAN goi Clear() trong MapState.Reset() khi doi map, trong khi luong auto dang
            // duyet -> TOCTOU. Truoc day co the nem ArgumentOutOfRange, hoac te hon: doc waypoint
            // cua map MOI bang chi so cua map CU.
            var wpSrc = _client.GameState.CurrentMap.Waypoints;
            Waypoint[] waypoints;
            lock (wpSrc) { waypoints = wpSrc.ToArray(); }

            // CHAN DOAN (DI_CHUYEN.md viec 4.1): that bai kieu "wp not found" hien sinh ra DUNG 0 bit
            // thong tin - khong mot dong [Nav] wp[i] nao. Bon nguyen nhan loai tru lan nhau deu bieu
            // hien y het: danh sach RONG / cache tro sang map khac / da bi danh dau chet / map doi
            // giua chung. Log day de mot lan chay la biet chinh xac cai nao.
            _client.Log(string.Format("[Nav] Quet wp map{0} -> map{1}: co {2} waypoint",
                fromMap, toMap, waypoints.Length));

            int skipCache = 0, skipDead = 0, tried = 0;
            for (int i = 0; i < waypoints.Length && !_stopped; i++)
            {
                if (_client.GameState.CurrentMap.MapId != fromMap)
                {
                    _client.Log(string.Format(
                        "[Nav] Quet wp map{0} DUT o i={1}: map da doi sang {2} giua chung",
                        fromMap, i, _client.GameState.CurrentMap.MapId));
                    return -1;
                }

                string cacheKey = fromMap + ":" + i;
                int knownDest;
                lock (_wpCache) { _wpCache.TryGetValue(cacheKey, out knownDest); }
                if (knownDest > 0 && knownDest != toMap)
                {
                    skipCache++;
                    _client.Log(string.Format("[Nav]   wp[{0}] bo qua: cache noi dan toi map{1}", i, knownDest));
                    continue;
                }
                if (knownDest == toMap)
                {
                    _client.Log(string.Format("[Nav]   wp[{0}] cache noi DUNG map{1} -> dung luon", i, toMap));
                    return i;
                }
                if (IsWpDead(fromMap, i, toMap))
                {
                    skipDead++;
                    _client.Log(string.Format("[Nav]   wp[{0}] bo qua: da danh dau khong dan toi dau", i));
                    continue;   // da thu, khong dan di dau -> khoi ton them mot lan cho WAIT_MAP
                }
                tried++;

                // Try this waypoint
                int oldMap = _client.GameState.CurrentMap.MapId;
                _client.MapChangeEvent.Reset();
                DoWaypointWalk(i);
                _client.MapChangeEvent.Reset();
                _client.Movement.SendRequestChangeMap();
                WaitForMapChange(WAIT_MAP, oldMap);

                int arrived = _client.GameState.CurrentMap.MapId;
                if (arrived != oldMap)
                {
                    lock (_wpCache) { _wpCache[cacheKey] = arrived; }
                    SaveCache();
                    _client.Log(string.Format("[Nav] Learned: map{0} wp[{1}] -> map{2}", fromMap, i, arrived));
                    if (arrived == toMap) return i;
                    return -1; // Wrong map, need to re-navigate
                }
                // Di toi noi + xin doi map ma server im -> waypoint nay vo dung, ghi lai de lan sau
                // khong ton them mot lan cho nua.
                // ⚠️ 2026-09-07: gia cua mot lan thu da tu 10 giay xuong 2 giay (WAIT_MAP). Toan bo
                // co che danh dau chet nay sinh ra HOI GIA CON 10 GIAY; o muc 2 giay thi loi ich cua
                // no nho hon nhieu, con rui ro (giet nham waypoint lanh - da xay ra, xem STATUS
                // 2026-09-06 (r)) thi khong doi. Neu can don bot phuc tap sau nay, day la ung vien
                // dau tien - nhung phai do lai truoc, dung go theo cam tinh.
                MarkWpDead(fromMap, i, toMap);
            }

            // Ket luan cua lan quet - day la dong tra loi cau hoi "vi sao wp not found".
            _client.Log(string.Format(
                "[Nav] Quet wp map{0} -> map{1} HET: tong {2}, bo qua vi cache {3}, vi danh dau chet {4}, that su thu {5}",
                fromMap, toMap, waypoints.Length, skipCache, skipDead, tried));

            // ===== VAN AN TOAN (A2) - clone y tuong `ct_0.c.clear()` cua Zang (bL.java:278-281) =====
            // "that su thu 0" nghia la ta khong he thu gi ma van bao that bai - day chinh xac la
            // trang thai da giet 4669 lan di chuyen trong log 2026-09-06. Neu ly do la dau chet
            // (khong phai cache tro sang map khac, cai do la thong tin DA HOC va dang tin), thi
            // xoa dau chet cua canh nay roi thu LAI MOT LAN. Tha ton mot lan cho (2 giay ke tu
            // 2026-09-07, truoc do la 10) di thu mot waypoint tung hong con hon ket vinh vien.
            //
            // `daMoVan` chan de quy: chi mo van dung mot lan cho moi lan goi tu ben ngoai.
            if (tried == 0 && skipDead > 0 && !daMoVan && !_stopped)
            {
                int n = ClearWpDeadForEdge(fromMap, toMap);
                _client.Log(string.Format(
                    "[Nav] VAN AN TOAN: map{0}->map{1} het ung vien (bi loai {2}) - xoa {3} dau chet, thu lai",
                    fromMap, toMap, skipDead, n));
                return TryLearnWaypoint(fromMap, toMap, true);
            }
            return -1;
        }

        // ======================== NPC BLACKLIST ========================

        /// <summary>
        /// Blacklist a map as NPC destination for 120s.
        /// </summary>
        private void BlacklistNpcDestination(int destMap)
        {
            lock (_npcDestBlacklist)
            {
                _npcDestBlacklist[destMap] = DateTime.UtcNow.AddSeconds(120);
            }
        }

        // ======================== WAIT METHODS ========================

        /// <summary>
        /// TileMap.h() equivalent - wait for map change with timeout.
        /// </summary>
        private bool WaitForMapChange(int timeoutMs, int oldMap)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs && !_stopped)
            {
                int remaining = timeoutMs - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0) break;
                _client.MapChangeEvent.WaitOne(Math.Min(remaining, 500));
                if (_client.GameState.CurrentMap.MapId != oldMap)
                    return true;
                // Event signaled but MapId chưa đổi → race, chờ thêm
                _client.MapChangeEvent.Reset();
            }
            return _client.GameState.CurrentMap.MapId != oldMap;
        }

        /// <summary>TileMap.h1() equivalent - wait for zone change.</summary>
        private bool WaitForZoneChange(int timeoutMs, byte oldZone)
        {
            _client.MapChangeEvent.WaitOne(timeoutMs);
            return _client.GameState.CurrentMap.ZoneId != oldZone;
        }

        /// <summary>
        /// Tran cho map ve. **PHANG, khong thich nghi** - clone ZangVPS `av_0.q()`.
        ///
        /// 2026-09-07 DA GO "adaptive timeout" (Fix #6 cu): `Math.Max(WAIT_MAP, last * 3)` cap
        /// 30000. Do la do TA TU NGHI RA, khong nguon nao co - Zang cho phang 2000 ms, khong doc
        /// do tre lan truoc, khong nhan he so nao. Giu lai thi no vo hieu hoa chinh viec ha
        /// WAIT_MAP: chi can mot chang cham 4 giay la `last * 3` = 12000, va cac chang sau lai
        /// dung im 12 giay.
        ///
        /// User chot 2026-09-07: *"Cu bam sat zangvps la duoc nhe. Khong nen tu sang tao gi them
        /// phan nay."*
        ///
        /// `GameState.LastMapChangeMs` VAN duoc ghi (Navigator.cs:303) - giu lai vi no la so lieu
        /// do luong huu ich, chi khong con ai dung de quyet dinh nua.
        /// </summary>
        private int WaitTimeout()
        {
            return WAIT_MAP;
        }

        // ======================== HELPERS ========================

        /// <summary>
        /// Match destination map name vào server's NPC menu options (cmd=40 response).
        /// </summary>
        private int FindMenuIndexForMap(int toMap)
        {
            string targetName = _client.GameState.MapStore.GetName(toMap);
            if (string.IsNullOrEmpty(targetName) || targetName.StartsWith("Map "))
                return -1;

            List<string> menu;
            lock (_client.NpcMenuOptions) { menu = new List<string>(_client.NpcMenuOptions); }
            if (menu.Count == 0) return -1;

            // Exact match trước, sau đó contains (caption thường dạng "Đến Làng X" v.v.)
            for (int i = 0; i < menu.Count; i++)
                if (string.Equals(menu[i], targetName, StringComparison.OrdinalIgnoreCase))
                    return i;
            for (int i = 0; i < menu.Count; i++)
                if (menu[i] != null && menu[i].IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            return -1;
        }

        private NpcState FindNpc(int templateId)
        {
            NpcState closest = null;
            double closestDist = double.MaxValue;
            short mx = _client.GameState.MyChar.Cx;
            short my = _client.GameState.MyChar.Cy;
            foreach (var npc in _client.GameState.CurrentMap.Npcs)
            {
                if (npc.TemplateId == templateId)
                {
                    double d = Dist(mx, my, npc.X, npc.Y);
                    if (d < closestDist) { closestDist = d; closest = npc; }
                }
            }
            return closest;
        }

        private void SafeSleep(int ms)
        {
            int s = 0;
            while (s < ms && !_stopped) { Thread.Sleep(Math.Min(100, ms - s)); s += 100; }
        }

        private static double Dist(short x1, short y1, short x2, short y2)
        {
            double dx = x2 - x1, dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ======================== CACHE ========================

        private static void LoadCache()
        {
            try
            {
                if (!System.IO.File.Exists(CacheFile)) return;
                foreach (string line in System.IO.File.ReadAllLines(CacheFile))
                {
                    string[] parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        int val;
                        if (int.TryParse(parts[1], out val))
                            _wpCache[parts[0]] = val;
                    }
                    else if (line.StartsWith("BROKEN:"))
                        _brokenLinks.Add(line.Substring(7));
                }
            }
            catch (Exception ex)
            {
                // KHONG nuot im: file cut/hong -> cache rong -> bot "quen" het waypoint da hoc ma
                // khong ai biet. Chay trong static ctor nen Logger co the chua bat; van hon khong co.
                NSOKHODO.Logging.Logger.Log("[Nav] LOI doc wp_cache: " + ex.Message);
            }
        }

        // Khoa RIENG cho viec ghi file. _wpCache/_brokenLinks la static dung chung ca fleet, nen
        // 30 account co the cung goi SaveCache mot luc.
        private static readonly object _saveFileLock = new object();

        /// <summary>
        /// Ghi cache waypoint ra dia. Ba diem da sua (DI_CHUYEN.md viec 1.4 + E.2):
        ///
        /// 1. CO KHOA. Truoc day <c>File.WriteAllLines</c> khong duoc dong bo giua cac thread -> hai
        ///    account cung luu la nem <c>IOException</c>.
        /// 2. GHI NGUYEN TU (ghi .tmp roi thay the). Truoc day crash/kill giua chung de lai FILE CUT,
        ///    va <c>LoadCache</c> nap ban thieu do ma khong bao gi.
        /// 3. KHONG NUOT LOI. <c>catch { }</c> trong lam moi that bai luu cache thanh vo hinh vinh vien
        ///    - dung thu lam ta mat nhieu thoi gian nhat khi truy nguyen su co 2026-09-04.
        /// </summary>
        private static void SaveCache()
        {
            var lines = new List<string>();
            lock (_wpCache)
            {
                foreach (var kv in _wpCache)
                    lines.Add(kv.Key + "=" + kv.Value);
            }
            lock (_brokenLinks)
            {
                foreach (var b in _brokenLinks)
                    lines.Add("BROKEN:" + b);
            }

            lock (_saveFileLock)
            {
                try
                {
                    // SharedFile: wp_cache dung CHUNG cho moi cua so tool (khong tach theo danh
                    // sach acc) nen phai chan giua cac TIEN TRINH, khong chi giua cac luong.
                    NSOKHODO.Config.SharedFile.WriteAtomic(
                        CacheFile, string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    NSOKHODO.Logging.Logger.Log(string.Format(
                        "[Nav] LOI luu wp_cache: {0} - tri thuc waypoint hoc duoc phien nay SE MAT khi thoat",
                        ex.Message));
                }
            }
        }

        private static int FindCachedWaypoint(int fromMap, int toMap)
        {
            lock (_wpCache)
            {
                foreach (var kv in _wpCache)
                {
                    if (kv.Key.StartsWith(fromMap + ":") && kv.Value == toMap)
                    {
                        int idx;
                        if (int.TryParse(kv.Key.Split(':')[1], out idx))
                            return idx;
                    }
                }
            }
            return -1;
        }
    }
}
