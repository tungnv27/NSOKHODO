using System;
using System.Collections.Generic;
using NSOKHODO.Client;
using NSOKHODO.Kho;
using NSOKHODO.Models;

namespace NSOKHODO.Auto
{
    /// <summary>
    /// MODE DUY NHAT cua NSOKHODO — moi acc (Leader, du phong, clone) chay mot ban.
    ///
    /// <para><b>Thu tu uu tien moi tick</b> (SPEC §11):</para>
    /// <code>
    /// cong song/chet → PHIEN giao dich dang chay → LOI MOI den → VIEC bo dieu phoi giao → RANH: ve dung cho
    /// </code>
    /// <para>Moi buoc la "lam mot chut roi tra ve" de loi moi giao dich (song 31 giay) luon duoc xet kip.
    /// Rieng Navigator (di map / doi khu) chan vai giay — chap nhan duoc, vi bot chi di xa khi co viec.</para>
    ///
    /// <para><b>KHONG BAO GIO dung / mac / ban / vut mon</b> (SPEC D38) — ItemService da go cac lenh do,
    /// mode nay khong the goi ke ca khi muon.</para>
    /// </summary>
    public sealed class KhoMode : AutoModeBase
    {
        /// <summary>Thu kho — NPC mo ruong (MODGAME "Thu kho" = template 5, SPEC D42).</summary>
        public const short NPC_THU_KHO = 5;
        /// <summary>Dung cach Thu kho toi da ngan nay (MODGAME GameScr.java:14357).</summary>
        public const int CACH_NPC = 22;
        /// <summary>Dung sat nguoi nhan: duoi nguong menu client 60/40 (SPEC R5).</summary>
        private const int CACH_NGUOI_X = 40, CACH_NGUOI_X_SAT = 16, CUNG_TANG_Y = 60;
        private const int QUA_XA_TOI_DA = 3;
        private const int SAI_SO_CHO_DUNG = 48;
        /// <summary>Hoi chieu doi khu 10 s tinh tu luc dat chan toi khu (D28) + 0,5 s du phong.</summary>
        private const int HOI_CHIEU_KHU_MS = 10500;
        private const int CHO_RUONG_DAP_MS = 7000;
        private const int CHO_MOT_MON_RUONG_MS = 3000;
        private const int CHO_TACH_MS = 4000;
        private const int TIM_NGUOI_TOI_DA_MS = 30000;
        private const int SANG_KHU_TOI_DA_MS = 90000;
        private const int CHO_SAU_VAO_KHU_MS = 2500;
        private DateTime _sangKhuTu = DateTime.MinValue;

        private enum Buoc { BatDau, LayRuong, TachChong, SangKhu, DenGan, GiaoDich, Cho, Xong }

        private readonly Navigator _nav;
        private PhienGiaoDich _phien;
        private bool _phienCuaViec;
        private Viec _viec;
        private Buoc _buoc;
        private DateTime _buocLuc;

        // cho ruong
        private int _choBoxSeq = -1;
        private DateTime _choBoxDen;
        private int _xinRuongLan;
        // cho tach chong
        private int _tachTuO = -1;
        private int _tachSo;
        private short _tachTpl;
        private HashSet<int> _oTrongTruocTach;
        private DateTime _tachDen;

        private string _lyDoCuoi = "";
        private volatile string _huyPhien;

        public KhoMode(NsoClient client) : base(client)
        {
            _nav = new Navigator(client);
        }

        public override string Name { get { return "Kho"; } }
        public override bool DungImHopLe { get { return true; } }
        protected override int StartupDelayMs { get { return 2000; } }

        public override void Start() { _nav.Reset(); base.Start(); }

        public override void Stop()
        {
            base.Stop();
            _nav.Stop();
            var v = _viec;
            _viec = null;
            var dp = KhoDieuPhoi.HienTai;
            if (v != null && dp != null) dp.BaoViec(Client, new BaoCaoViec { Viec = v, MaLoi = MaLoiViec.BI_HUY, LyDo = "mode dung (mat ket noi / tat)" });
        }

        // ---------------- cho giao dien / bo dieu phoi ----------------
        public string LyDoCuoi { get { return _lyDoCuoi; } }
        public Viec ViecHienTai { get { return _viec; } }
        public PhienGiaoDich PhienHienTai { get { return _phien; } }
        /// <summary>Yeu cau huy phien dang chay (Chu kho `nap` chen ngang). An toan goi tu moi luong.</summary>
        public void YeuCauHuyPhien(string lyDo) { _huyPhien = lyDo ?? "bi chen"; }

        protected override int Tick()
        {
            var dp = KhoDieuPhoi.HienTai;
            if (dp == null) return Nghi("chua co bo dieu phoi", 1000);
            var cfg = dp.Cfg;

            if (Client.State == ClientState.Dead) return Nghi("dang CHET - cho luong hoi sinh", 500);
            var state = Client.GameState;
            if (state.IsChangingMap) return Nghi("dang chuyen map", 200);
            var mc = state.MyChar;
            if (mc == null || string.IsNullOrEmpty(mc.Name)) return Nghi("chua co du lieu nhan vat", 1000);

            // ===== 1. Phien giao dich dang chay =====
            if (_phien != null)
            {
                string huy = _huyPhien;
                if (huy != null) { _huyPhien = null; _phien.YeuCauHuy(huy); }
                int ms = _phien.Tick();
                if (!_phien.KetThuc) return Math.Max(50, ms);
                var p = _phien;
                _phien = null;
                bool cuaViec = _phienCuaViec;
                _phienCuaViec = false;
                dp.BaoPhien(Client, p, cuaViec ? _viec : null);
                if (cuaViec) SauPhienCuaViec(dp, p);
                return 100;
            }
            _huyPhien = null;

            // ===== 2. Loi moi den =====
            int id; DateTime luc;
            if (Client.Trade.LayLoiMoi(out id, out luc))
            {
                if ((DateTime.UtcNow - luc).TotalSeconds <= 25)
                {
                    var qd = dp.XetLoiMoi(Client, id, _viec);
                    if (qd.Nhan)
                    {
                        _phien = new PhienGiaoDich(Client, VaiGiaoDich.Nhan, id, qd.TenMongDoi, qd.ChoMs, 0, 0,
                                                   null, qd.KiemTen, qd.KiemHang);
                        _phien.HuyKhiMo = qd.HuyKhiMo;
                        _phienCuaViec = false;
                        SetActivity("Đang nhận");
                        return 0;
                    }
                    if (qd.TuChoi)
                    {
                        Client.TradeSvc.SendRejectInvite();
                        Client.Log("[Kho] Tu choi loi moi cua id " + id);
                    }
                }
            }

            // ===== 3. Viec duoc giao =====
            if (_viec == null)
            {
                _viec = dp.LayViec(Client);
                if (_viec != null)
                {
                    _buoc = Buoc.BatDau;
                    _buocLuc = DateTime.UtcNow;
                    _quaXaLan = 0;
                    _diToiLan = 0;
                    _goiRuongLan = 0;
                    _oDaGui.Clear();
                    _xinRuongLan = 0;
                    _choDsRuong = -1;
                    _sangKhuTu = DateTime.MinValue;
                }
            }
            if (_viec != null)
            {
                if (_viec.BiHuy) { KetThucViec(dp, false, MaLoiViec.BI_HUY, _viec.LyDoHuy); return 200; }
                if (DateTime.Now > _viec.HetHan) { KetThucViec(dp, false, MaLoiViec.HET_HAN, "qua han viec"); return 200; }
                return LamViec(dp, cfg, mc);
            }

            // ===== 4. Ranh =====
            return Ranh(dp, cfg, mc);
        }

        // ==================================================================
        // RANH: ve dung cho theo vai
        // ==================================================================
        private int Ranh(KhoDieuPhoi dp, KhoConfig cfg, CharacterState mc)
        {
            var vai = dp.VaiCua(Client);
            // Chi khu CHINH la bat buoc. Khu phu de trong thi clone dung o khu dang dung (KhuDungCho) -
            // truoc day thieu mot trong hai la CA kho dung im, ke ca Leader (user test 16/09).
            if (cfg.KhuChinh < 0)
            {
                SetActivity("Chưa cài khu chính (Cài đặt → Kho)");
                return Nghi("chua cai khu chinh trong Cai dat", 3000);
            }
            int khu = dp.KhuDungCho(Client);
            int ms;
            if (!VeDungKhu(cfg.Map, khu, out ms)) return ms;

            if (vai == VaiKho.Leader && (cfg.LeaderX != 0 || cfg.LeaderY != 0))
            {
                int dx = mc.Cx - cfg.LeaderX, dy = mc.Cy - cfg.LeaderY;
                if (dx * dx + dy * dy > SAI_SO_CHO_DUNG * SAI_SO_CHO_DUNG)
                {
                    SetActivity("Về chỗ đứng");
                    _nav.CharBurstMove(cfg.LeaderX, cfg.LeaderY);
                    return Nghi("ve cho dung da cai", 1000);
                }
            }

            SetActivity(ACT_DUNG_CANH);
            if (!Client.DangGiaoDich) Heartbeat(mc);
            return Nghi("ranh", 300);
        }

        /// <summary>Dua nhan vat ve map + khu. true = da dung dung cho.</summary>
        private bool VeDungKhu(int map, int khu, out int ms)
        {
            ms = 0;
            var st = Client.GameState;
            if (st.CurrentMap.MapId != map)
            {
                if (_nav.IsKnownUnreachable(st.CurrentMap.MapId, map))
                {
                    SetActivity("Không tới được");
                    ms = Nghi("BFS bao khong co duong toi map " + map, 15000);
                    return false;
                }
                SetActivity("Di chuyển");
                bool ok = _nav.DoGmNavigation(map);
                ms = ok ? 0 : Nghi("di toi map " + map + " chua xong", 3000);
                return false;
            }
            if (khu >= 0 && st.CurrentMap.ZoneId != khu)
            {
                SetActivity("Đổi khu");
                var cho = (DateTime.UtcNow - Client.VaoKhuLucUtc).TotalMilliseconds;
                if (cho < HOI_CHIEU_KHU_MS)
                {
                    ms = Nghi("cho hoi chieu doi khu", (int)Math.Max(200, HOI_CHIEU_KHU_MS - cho));
                    return false;
                }
                int tuKhu = st.CurrentMap.ZoneId;
                if (_nav.DoZoneChange((byte)khu))
                    Client.Log(string.Format("[Kho] Doi khu {0} -> {1}", tuKhu, khu));
                ms = Nghi("dang doi khu", 1000);
                return false;
            }
            return true;
        }

        // ==================================================================
        // VIEC
        // ==================================================================
        private int LamViec(KhoDieuPhoi dp, KhoConfig cfg, CharacterState mc)
        {
            var v = _viec;
            if (!v.DaBatDau) { v.DaBatDau = true; Client.Log("[Kho] Bat dau viec: " + ChuVan.BoDau(v.MoTa)); }
            SetActivity(v.MoTa);
            switch (v.Loai)
            {
                case LoaiViec.DocRuong: return ViecDocRuong(dp, cfg);
                case LoaiViec.CatRuong: return ViecCatRuong(dp, cfg, mc);
                case LoaiViec.DoiNhan: return ViecDoiNhan(dp, cfg, mc);
                default: return ViecGiaoMon(dp, cfg, mc);
            }
        }

        private void KetThucViec(KhoDieuPhoi dp, bool xong, string maLoi, string lyDo)
        {
            var v = _viec;
            _viec = null;
            _choBoxSeq = -1;
            _choDsRuong = -1;
            _sangKhuTu = DateTime.MinValue;
            _xinRuongLan = 0;
            _tachTuO = -1;
            if (v == null) return;
            Client.Log(string.Format("[Kho] Ket thuc viec {0}: {1}{2}", ChuVan.BoDau(v.MoTa), xong ? "XONG" : "LOI " + maLoi,
                string.IsNullOrEmpty(lyDo) ? "" : " - " + ChuVan.BoDau(lyDo)));
            dp.BaoViec(Client, new BaoCaoViec { Viec = v, Xong = xong, MaLoi = maLoi, LyDo = lyDo ?? "" });
        }

        // ---------------- DocRuong ----------------
        private int ViecDocRuong(KhoDieuPhoi dp, KhoConfig cfg)
        {
            int ms;
            if (!DenThuKho(dp, out ms)) return ms;
            if (DamBaoRuong(dp, true, out ms)) KetThucViec(dp, true, null, null);
            return ms;
        }

        // ---------------- CatRuong ----------------
        private int ViecCatRuong(KhoDieuPhoi dp, KhoConfig cfg, CharacterState mc)
        {
            int ms;
            if (!DenThuKho(dp, out ms)) return ms;
            if (!DamBaoRuong(dp, false, out ms)) return ms;

            if (_choBoxSeq >= 0)
            {
                if (Client.GameState.BoxSeq != _choBoxSeq) { _choBoxSeq = -1; return 50; }
                if (DateTime.UtcNow < _choBoxDen) return 100;
                _choBoxSeq = -1;
                KetThucViec(dp, false, MaLoiViec.RUONG_KHONG_DAP, "server khong xac nhan cat ruong (co the ruong day hoac dung xa Thu kho)");
                return 500;
            }

            var bag = mc.BagItems;
            var box = mc.BoxItems;
            if (bag == null || box == null) { KetThucViec(dp, false, MaLoiViec.RUONG_KHONG_DAP, "chua co danh sach ruong"); return 500; }
            for (int i = 0; i < bag.Length; i++)
            {
                var it = bag[i];
                if (it == null || it.IsEmpty || it.IsLock) continue;
                var k = KhoaMon.Tu(it);
                if (_viec.KhongCat.Contains(k)) continue;
                if (!RuongConCho(box, it)) continue;
                // Moi o chi gui MOT lan moi viec: server tra loi ma mon van nam do (ruong tu choi) thi bo qua,
                // khong gui lai lien tuc (bai hoc xin ruong 1.537 lan).
                if (!_oDaGui.Add(i)) continue;
                if (!ConDuocGuiRuong(dp)) return 500;
                _choBoxSeq = Client.GameState.BoxSeq;
                _choBoxDen = DateTime.UtcNow.AddMilliseconds(CHO_MOT_MON_RUONG_MS);
                Client.Items.SendItemBagToBox((byte)i);
                _viec.TienDo = "cất ô " + i;
                return 150;
            }
            KetThucViec(dp, true, null, null);
            return 200;
        }

        // Chot chan so goi chuyen mon tui <-> ruong trong MOT viec (reset o LayViec).
        private readonly HashSet<int> _oDaGui = new HashSet<int>();
        private int _goiRuongLan;
        private const int GOI_RUONG_TOI_DA = 80;

        private bool ConDuocGuiRuong(KhoDieuPhoi dp)
        {
            if (++_goiRuongLan <= GOI_RUONG_TOI_DA) return true;
            KetThucViec(dp, false, MaLoiViec.RUONG_KHONG_DAP, "qua " + GOI_RUONG_TOI_DA + " lan chuyen mon tui/ruong trong mot viec");
            return false;
        }

        /// <summary>Ruong con cho cho mon nay: con o trong, hoac (mon xep chong) da co cung loai.</summary>
        private static bool RuongConCho(Item[] box, Item it)
        {
            var tpl = BangMon.Lay(it.TemplateId);
            bool chong = tpl != null && tpl.IsUpToUp;
            for (int j = 0; j < box.Length; j++)
            {
                var b = box[j];
                if (b == null || b.IsEmpty) return true;
                if (chong && b.TemplateId == it.TemplateId && b.IsLock == it.IsLock && b.IsExpires == it.IsExpires
                    && b.Quantity + it.Quantity < 30000) return true;
            }
            return false;
        }

        // ---------------- DoiNhan ----------------
        private int ViecDoiNhan(KhoDieuPhoi dp, KhoConfig cfg, CharacterState mc)
        {
            int ms;
            var v = _viec;
            // Luot gom (D81) dien ra o khu cua clone, khong phai khu chinh.
            int khu = v.Khu >= 0 ? v.Khu : cfg.KhuChinh;
            if (!VeDungKhu(cfg.Map, khu, out ms)) return ms;
            // D86: clone cho chuyen tiep dung sat canh cho Leader (trong tam moi, Leader khong phai di).
            if ((v.DungX != 0 || v.DungY != 0) && _diToiLan < 10 && !Client.DangGiaoDich
                && (Math.Abs(mc.Cx - v.DungX) > 20 || Math.Abs(mc.Cy - v.DungY) > CUNG_TANG_Y))
            {
                // Test song 17/09: di ngay khi vua vao khu (~1 giay) thi server lang le bo buoc di - bot tuong minh da
                // toi (395) trong khi ca khu van thay no o diem vao (420). Cho vi tri on dinh nhu buoc DenGan (M24).
                var tuLucVao = (DateTime.UtcNow - Client.VaoKhuLucUtc).TotalMilliseconds;
                if (tuLucVao < CHO_SAU_VAO_KHU_MS) return Nghi("vua vao khu, cho roi moi toi canh Leader", (int)(CHO_SAU_VAO_KHU_MS - tuLucVao) + 50);
                _diToiLan++;
                v.TienDo = "tới cạnh Leader";
                _nav.CharBurstMove(v.DungX, v.DungY);
                return Nghi("toi cho dung canh Leader", 800);
            }
            // DUNG YEN: bot giao (Leader) se tu toi sat. Neu ca hai cung di ve phia nhau thi moi ben
            // nhay toi cho CU cua ben kia -> doi cho cho nhau mai khong gap.
            var bot = dp.TimNguoiTheoAcc(v.TuBotAcc, khu);
            v.TienDo = bot == null ? "chờ thấy bot giao" : "đứng chờ được mời";
            if (!Client.DangGiaoDich) Heartbeat(mc);
            return Nghi("cho bot giao moi", 300);
        }

        // ---------------- GiaoMon ----------------
        private int ViecGiaoMon(KhoDieuPhoi dp, KhoConfig cfg, CharacterState mc)
        {
            var v = _viec;
            int ms;
            switch (_buoc)
            {
                case Buoc.BatDau:
                    // D86: chuyen tiep chi giao phan dang o tui - khong ra Thu kho, khong tach chong.
                    _buoc = v.ChiTui ? Buoc.SangKhu : Buoc.LayRuong;
                    _buocLuc = DateTime.UtcNow;
                    return 0;

                case Buoc.LayRuong:
                    {
                        string loi, maLoi;
                        bool xong = LayDuTuRuong(dp, mc, out ms, out loi, out maLoi);
                        if (loi != null) { KetThucViec(dp, false, maLoi, loi); return 500; }
                        if (!xong) return ms;
                        _buoc = Buoc.TachChong;
                        return 0;
                    }

                case Buoc.TachChong:
                    {
                        string loi;
                        bool xong = TachChoKhop(mc, out ms, out loi);
                        if (loi != null) { KetThucViec(dp, false, MaLoiViec.TACH_CHONG_HONG, loi); return 500; }
                        if (!xong) return ms;
                        _buoc = Buoc.SangKhu;
                        return 0;
                    }

                case Buoc.SangKhu:
                    {
                        int khuGiao = v.Khu >= 0 ? v.Khu : cfg.KhuChinh;
                        if (_sangKhuTu == DateTime.MinValue) _sangKhuTu = DateTime.UtcNow;
                        if (!VeDungKhu(cfg.Map, khuGiao, out ms))
                        {
                            // Khu day / khong ton tai (M12): doi mai toi het han viec la chan ca hang cho.
                            if ((DateTime.UtcNow - _sangKhuTu).TotalMilliseconds > SANG_KHU_TOI_DA_MS)
                            {
                                KetThucViec(dp, false, MaLoiViec.KHONG_VAO_KHU,
                                    "doi " + (SANG_KHU_TOI_DA_MS / 1000) + "s khong vao duoc khu " + khuGiao);
                                return 500;
                            }
                            return ms;
                        }
                        _sangKhuTu = DateTime.MinValue;
                        _buoc = Buoc.DenGan;
                        _buocLuc = DateTime.UtcNow;
                        _diToiLan = 0;
                        return 0;
                    }

                case Buoc.DenGan:
                    {
                        // Vua dat chan toi khu: vi tri nguoi trong khu con cu vai giay. Test song 17/09: moi 1,5 s sau khi
                        // toi khu 5 -> tuong nguoi nhan con o diem vao (420) trong khi ho da di toi 300; hai ben dong y
                        // roi server huy phien (tinh la hong phia nguoi nhan).
                        var tuLucVao = (DateTime.UtcNow - Client.VaoKhuLucUtc).TotalMilliseconds;
                        if (tuLucVao < CHO_SAU_VAO_KHU_MS) return Nghi("vua vao khu, cho vi tri on dinh", (int)(CHO_SAU_VAO_KHU_MS - tuLucVao) + 50);
                        int khuGiao = v.Khu >= 0 ? v.Khu : cfg.KhuChinh;
                        // Toa do theo MAT CHINH acc giao (server phat cho no): bot nhan tu biet vi tri "lac quan" -
                        // buoc di bi server bo thi Leader lai sat cho sai, moi mai "qua xa" (test song 17/09).
                        var nguoi = dp.TimNguoi(v.NguoiNhan, khuGiao, Client);
                        if (nguoi == null)
                        {
                            v.TienDo = "chưa thấy " + v.NguoiNhan;
                            int choMs = v.ChoTimMs > 0 ? v.ChoTimMs : TIM_NGUOI_TOI_DA_MS;
                            if ((DateTime.UtcNow - _buocLuc).TotalMilliseconds > choMs)
                            {
                                KetThucViec(dp, false, MaLoiViec.KHONG_THAY_NGUOI, "khong thay " + v.NguoiNhan + " o khu " + khuGiao);
                                return 500;
                            }
                            if (!Client.DangGiaoDich) Heartbeat(mc);
                            return Nghi("tim nguoi nhan", 500);
                        }
                        // Y cua nguoi khac nhin qua goi di chuyen co the lech toi 52 px (nhip chong AFK nhay
                        // len roi ve - test song 16/09: bot dung y=216 ma nguoi khac thay y=164). Cung tang thi
                        // giu Y cua minh, chi keo X.
                        int dx = Math.Abs(mc.Cx - nguoi.X), dy = Math.Abs(mc.Cy - nguoi.Y);
                        bool cungTang = dy <= CUNG_TANG_Y;
                        int canX = _quaXaLan > 0 ? CACH_NGUOI_X_SAT : CACH_NGUOI_X;
                        if (dx > canX || !cungTang)
                        {
                            v.TienDo = "đi tới " + v.NguoiNhan;
                            int lech = _quaXaLan > 0 ? 8 : 24;
                            _nav.CharBurstMove((short)(nguoi.X + (mc.Cx < nguoi.X ? -lech : lech)), cungTang ? mc.Cy : nguoi.Y);
                            if (++_diToiLan > 40)
                            {
                                KetThucViec(dp, false, MaLoiViec.KHONG_TOI_DUOC, "di 40 lan khong toi sat " + v.NguoiNhan
                                    + " (" + nguoi.X + "," + nguoi.Y + ")");
                                return 500;
                            }
                            return Nghi("toi sat nguoi nhan", 600);
                        }
                        _buoc = Buoc.GiaoDich;
                        _nguoiNhanId = nguoi.CharId;
                        return 0;
                    }

                case Buoc.GiaoDich:
                    {
                        if (v.TongConLai <= 0 && v.Xu <= 0) { KetThucViec(dp, true, null, null); return 200; }
                        byte[] thu = ChonO(mc, v);
                        if (v.ChiTui && thu.Length == 0 && v.Xu <= 0)
                        {
                            // Tui doi tu luc giao viec (nguoi nap them lam chong gop lai...): giao duoc phan nao thi xong phan do.
                            bool coGiao = v.Dong.Exists(d => d.DaGiao > 0);
                            KetThucViec(dp, coGiao, coGiao ? null : MaLoiViec.KHONG_CO_MON, coGiao ? null : "tui khong con mon cua luot");
                            return 200;
                        }
                        if (!v.ChiTui && ((thu.Length == 0 && v.Xu <= 0) || NenLayThem(mc, v, thu.Length)))
                        {
                            // Het mon giao duoc trong tui (luot truoc da giao / sub 115 dung lai tui), hoac tui
                            // vua trong ra ma ruong con hang -> quay ve Thu kho lay luot tiep.
                            _buoc = Buoc.LayRuong;
                            return 200;
                        }
                        var viec = v;
                        _phien = new PhienGiaoDich(Client, VaiGiaoDich.Giao, _nguoiNhanId, v.NguoiNhan,
                            (v.NguoiNhanLaBot ? cfg.ChoBotGiay : cfg.ChoNguoiGiay) * 1000,
                            cfg.MoiLaiGiay * 1000, v.Xu,
                            () => ChonO(Client.GameState.MyChar, viec),
                            null,
                            (xu, mon) => dp.KiemHangNhanThem(Client, xu, mon));
                        _phienCuaViec = true;
                        v.TienDo = "giao dịch với " + v.NguoiNhan;
                        return 0;
                    }
            }
            return 200;
        }

        private int _nguoiNhanId;
        private int _quaXaLan, _diToiLan;

        private void SauPhienCuaViec(KhoDieuPhoi dp, PhienGiaoDich p)
        {
            var v = _viec;
            if (v == null) return;
            var kq = p.KetQua;
            if (kq != null && kq.MaLoi == MaLoiGiaoDich.QUA_XA && _quaXaLan < QUA_XA_TOI_DA)
            {
                // Chua co gi doi chu: lai sat hon roi moi lai (khoa moi 31 giay khong ap dung - loi moi chua toi).
                _quaXaLan++;
                _diToiLan = 0;
                _buoc = Buoc.DenGan;
                _buocLuc = DateTime.UtcNow;
                Client.Log("[Kho] Server bao qua xa - lai sat " + v.NguoiNhan + " (lan " + _quaXaLan + ")");
                return;
            }
            if (kq != null && kq.ThanhCong)
            {
                foreach (var kv in kq.MonDua)
                {
                    var k = KhoaMon.Tu(kv.Value);
                    int sl = Math.Max(1, (int)kv.Value.Quantity);
                    foreach (var d in v.Dong)
                    {
                        if (!d.Khoa.Equals(k) || d.ConLai <= 0) continue;
                        int an = Math.Min(sl, d.ConLai);
                        d.DaGiao += an;
                        sl -= an;
                        if (sl <= 0) break;
                    }
                }
                if (kq.XuDua > 0) v.Xu = 0;
                if (v.TongConLai <= 0 && v.Xu <= 0) KetThucViec(dp, true, null, null);
                // con lai: tick sau vao lai buoc GiaoDich - server cho moi lai NGAY sau phien xong (T4)
                return;
            }
            KetThucViec(dp, false, kq != null ? kq.MaLoi : MaLoiGiaoDich.BI_HUY, kq != null ? kq.LyDo : "");
        }

        /// <summary>
        /// Chon toi da 12 o tui cho mot luot: moi o phai la NGUYEN CHONG vua du (khong vuot so con lai).
        /// </summary>
        private static byte[] ChonO(CharacterState mc, Viec v)
        {
            var r = new List<byte>();
            var bag = mc != null ? mc.BagItems : null;
            if (bag == null) return r.ToArray();
            var conLai = new Dictionary<KhoaMon, int>();
            foreach (var d in v.Dong)
            {
                int n; conLai.TryGetValue(d.Khoa, out n);
                conLai[d.Khoa] = n + d.ConLai;
            }
            for (int i = 0; i < bag.Length && r.Count < Controller.TradeHandler.MAX_MON; i++)
            {
                var it = bag[i];
                if (it == null || it.IsEmpty || it.IsLock) continue;
                var k = KhoaMon.Tu(it);
                int con;
                if (!conLai.TryGetValue(k, out con) || con <= 0) continue;
                int sl = Math.Max(1, (int)it.Quantity);
                if (sl > con) continue;
                r.Add((byte)i);
                conLai[k] = con - sl;
            }
            return r.ToArray();
        }

        /// <summary>
        /// Lay tu ruong ra tui cho luot giao sau. true = di giao duoc (tui da du, HOAC tui day / ruong het
        /// nhung trong tui da co mon giao duoc - giao truoc, buoc GiaoDich quay lai day lay tiep).
        /// Tui day ma chua co gi de giao: cat tam mon KHONG can vao ruong de lay cho (doi cho).
        /// </summary>
        private bool LayDuTuRuong(KhoDieuPhoi dp, CharacterState mc, out int ms, out string loi, out string maLoi)
        {
            ms = 0; loi = null; maLoi = MaLoiViec.KHONG_CO_MON;
            var v = _viec;
            var bag = mc.BagItems ?? new Item[0];
            var thieu = ThieuTrongTui(bag, v);
            int oTrong = DemOTrong(bag);
            int soGiao = ChonO(mc, v).Length;
            // Tui day, chi con chong lon hon so can -> phai co mot o trong de tach.
            bool canOTach = oTrong == 0 && soGiao == 0 && CanTachChong(bag, v);
            if (thieu.Count == 0 && !canOTach) { _choBoxSeq = -1; return true; }

            if (_choBoxSeq >= 0)
            {
                if (Client.GameState.BoxSeq != _choBoxSeq) { _choBoxSeq = -1; ms = 50; return false; }
                if (DateTime.UtcNow < _choBoxDen) { ms = 100; return false; }
                _choBoxSeq = -1;
                loi = "server khong xac nhan chuyen mon tui/ruong";
                return false;
            }

            // Test song 16/09: 30 mon nam 12 trong tui + 18 trong ruong, tui day -> bao hong ca 2 acc,
            // lenh tam dung "chi con 9/30". Tui du mot luot (12 o) thi giao ngay, khong can ra Thu kho.
            if (oTrong == 0 && soGiao >= Controller.TradeHandler.MAX_MON) return true;

            if (!DenThuKho(dp, out ms)) return false;
            if (!DamBaoRuong(dp, false, out ms)) return false;

            var box = mc.BoxItems;
            if (box == null) { ms = 500; return false; }

            int jLay = canOTach ? -1 : ORuongCanLay(box, thieu);
            if (jLay < 0 && !canOTach)
            {
                if (soGiao > 0) return true;   // ruong het: giao phan dang co, luot sau moi bao thieu
                var p = new List<string>();
                foreach (var kv in thieu) p.Add(BangMon.Ten(kv.Key.Tpl) + " thieu " + kv.Value);
                loi = "khong du hang: " + string.Join(", ", p.ToArray());
                return false;
            }

            if (oTrong == 0)
            {
                int iCat = TimOCatDoiCho(bag, box, v);
                if (iCat < 0)
                {
                    if (soGiao > 0) return true;
                    loi = "tui day, ruong khong con cho cat bot - khong lay duoc do tu ruong";
                    maLoi = MaLoiViec.TUI_DAY;
                    return false;
                }
                if (!ConDuocGuiRuong(dp)) { ms = 500; return false; }
                _choBoxSeq = Client.GameState.BoxSeq;
                _choBoxDen = DateTime.UtcNow.AddMilliseconds(CHO_MOT_MON_RUONG_MS);
                Client.Items.SendItemBagToBox((byte)iCat);
                Client.Log(string.Format("[Kho] Tui day: cat tam o {0} ({1}) vao ruong de lay cho", iCat,
                    ChuVan.BoDau(BangMon.Ten(bag[iCat].TemplateId))));
                v.TienDo = "cất tạm ô " + iCat;
                ms = 150;
                return false;
            }

            if (!ConDuocGuiRuong(dp)) { ms = 500; return false; }
            _choBoxSeq = Client.GameState.BoxSeq;
            _choBoxDen = DateTime.UtcNow.AddMilliseconds(CHO_MOT_MON_RUONG_MS);
            Client.Items.SendItemBoxToBag((byte)jLay);
            v.TienDo = "lấy rương ô " + jLay;
            ms = 150;
            return false;
        }

        /// <summary>Con thieu bao nhieu trong TUI theo tung khoa (gop dong trung khoa).</summary>
        private static Dictionary<KhoaMon, int> ThieuTrongTui(Item[] bag, Viec v)
        {
            // Cong so can theo khoa TRUOC roi moi tru tui mot lan (hai dong trung khoa thi khong tru tui hai lan).
            var can = new Dictionary<KhoaMon, int>();
            foreach (var d in v.Dong)
            {
                if (d.ConLai <= 0) continue;
                int n; can.TryGetValue(d.Khoa, out n);
                can[d.Khoa] = n + d.ConLai;
            }
            var thieu = new Dictionary<KhoaMon, int>();
            foreach (var kv in can)
            {
                int x = kv.Value - DemTrongTui(bag, kv.Key);
                if (x > 0) thieu[kv.Key] = x;
            }
            return thieu;
        }

        private static int ORuongCanLay(Item[] box, Dictionary<KhoaMon, int> thieu)
        {
            if (box == null) return -1;
            foreach (var kv in thieu)
                for (int j = 0; j < box.Length; j++)
                    if (kv.Key.Khop(box[j])) return j;
            return -1;
        }

        /// <summary>
        /// Truoc moi luot giao: luot chua du 12 o, tui con o trong va ruong con mon dang thieu -> nen ve
        /// Thu kho lay them truoc (giam so luot giao). Cung dieu kien dung voi <see cref="LayDuTuRuong"/>
        /// nen khong lap qua lai.
        /// </summary>
        private static bool NenLayThem(CharacterState mc, Viec v, int soGiao)
        {
            if (soGiao >= Controller.TradeHandler.MAX_MON || mc == null) return false;
            var bag = mc.BagItems;
            if (bag == null || DemOTrong(bag) == 0) return false;
            var thieu = ThieuTrongTui(bag, v);
            return thieu.Count > 0 && ORuongCanLay(mc.BoxItems, thieu) >= 0;
        }

        /// <summary>
        /// O tui cat tam vao ruong de lay cho: mon KHONG thuoc viec, khong khoa, ruong con cho, chua gui
        /// trong viec nay. -1 = khong co.
        /// </summary>
        private int TimOCatDoiCho(Item[] bag, Item[] box, Viec v)
        {
            for (int i = 0; i < bag.Length; i++)
            {
                var it = bag[i];
                if (it == null || it.IsEmpty || it.IsLock) continue;
                bool canGiao = false;
                foreach (var d in v.Dong)
                    if (d.ConLai > 0 && d.Khoa.Khop(it)) { canGiao = true; break; }
                if (canGiao || _oDaGui.Contains(i) || !RuongConCho(box, it)) continue;
                _oDaGui.Add(i);
                return i;
            }
            return -1;
        }

        /// <summary>Co dong nao can tach chong khong (cung phep tinh voi <see cref="TachChoKhop"/>).</summary>
        private static bool CanTachChong(Item[] bag, Viec v)
        {
            foreach (var d in v.Dong)
            {
                if (d.ConLai <= 0) continue;
                var cacO = new List<int>();
                for (int i = 0; i < bag.Length; i++)
                    if (d.Khoa.Khop(bag[i])) cacO.Add(i);
                cacO.Sort((a, b) => bag[b].Quantity.CompareTo(bag[a].Quantity));
                int tong = 0, oDu = -1;
                foreach (int i in cacO)
                {
                    int sl = Math.Max(1, (int)bag[i].Quantity);
                    if (tong + sl <= d.ConLai) tong += sl;
                    else if (oDu < 0) oDu = i;
                }
                if (tong >= d.ConLai || oDu < 0) continue;
                int can = d.ConLai - tong;
                if (can >= 1 && can < bag[oDu].Quantity) return true;
            }
            return false;
        }

        /// <summary>
        /// Tach chong de moi dong co cac o NGUYEN CHONG cong lai VUA DU so con lai. true = da khop.
        /// Chi dung <c>-28/-85</c> (tach chong) — KHONG BAO GIO gui cmd 22 (tach trang bi).
        /// </summary>
        private bool TachChoKhop(CharacterState mc, out int ms, out string loi)
        {
            ms = 0; loi = null;
            var bag = mc.BagItems ?? new Item[0];

            if (_tachTuO >= 0)
            {
                // Dang cho ket qua mot lan tach
                for (int i = 0; i < bag.Length; i++)
                {
                    if (_oTrongTruocTach == null || !_oTrongTruocTach.Contains(i)) continue;
                    var it = bag[i];
                    if (it != null && !it.IsEmpty && it.TemplateId == _tachTpl && it.Quantity == _tachSo)
                    {
                        Client.Log(string.Format("[Kho] Tach chong xong: o {0} -> o {1} x{2}", _tachTuO, i, _tachSo));
                        _tachTuO = -1;
                        ms = 50;
                        return false;
                    }
                }
                if (DateTime.UtcNow < _tachDen) { ms = 150; return false; }
                loi = string.Format("tach chong o {0} ({1} mon) khong thay ket qua sau {2}s", _tachTuO, _tachSo, CHO_TACH_MS / 1000);
                _tachTuO = -1;
                return false;
            }

            foreach (var d in _viec.Dong)
            {
                if (d.ConLai <= 0) continue;
                // Gom o nguyen chong tu lon den nho khong vuot so can
                var cacO = new List<int>();
                for (int i = 0; i < bag.Length; i++)
                    if (d.Khoa.Khop(bag[i])) cacO.Add(i);
                cacO.Sort((a, b) => bag[b].Quantity.CompareTo(bag[a].Quantity));
                int tong = 0;
                int oDu = -1;
                foreach (int i in cacO)
                {
                    int sl = Math.Max(1, (int)bag[i].Quantity);
                    if (tong + sl <= d.ConLai) tong += sl;
                    else if (oDu < 0) oDu = i;
                }
                if (tong >= d.ConLai) continue;
                if (oDu < 0) continue;   // thieu hang - buoc lay ruong da bao, o day de giao phan dang co
                int can = d.ConLai - tong;
                int trongO = bag[oDu].Quantity;
                if (can < 1 || can >= trongO) continue;   // client goc: 1 <= so < so trong o
                var trong = new HashSet<int>();
                for (int i = 0; i < bag.Length; i++) if (bag[i] == null || bag[i].IsEmpty) trong.Add(i);
                if (trong.Count == 0)
                {
                    // Giao cac chong vua du truoc; tui trong ra thi luot sau tach.
                    if (ChonO(mc, _viec).Length > 0) return true;
                    loi = "tui day, khong con o de tach chong";
                    return false;
                }
                _oTrongTruocTach = trong;
                _tachTuO = oDu;
                _tachSo = can;
                _tachTpl = bag[oDu].TemplateId;
                _tachDen = DateTime.UtcNow.AddMilliseconds(CHO_TACH_MS);
                Client.Items.SendSplitStack((byte)oDu, can);
                Client.Log(string.Format("[Kho] Tach chong o {0}: lay {1}/{2}", oDu, can, trongO));
                _viec.TienDo = "tách chồng";
                ms = 150;
                return false;
            }
            return true;
        }

        // ==================================================================
        // Thu kho / ruong
        // ==================================================================
        private bool DenThuKho(KhoDieuPhoi dp, out int ms)
        {
            ms = 0;
            NpcState npc = null;
            try
            {
                foreach (var n in Client.GameState.CurrentMap.Npcs.ToArray())
                    if (n != null && n.TemplateId == NPC_THU_KHO) { npc = n; break; }
            }
            catch { }
            var mc = Client.GameState.MyChar;
            if (npc == null)
            {
                KetThucViec(dp, false, MaLoiViec.KHONG_THAY_NPC, "map " + Client.GameState.CurrentMap.MapId + " khong co Thu kho (NPC 5)");
                ms = 3000;
                return false;
            }
            if (Math.Abs(npc.X - mc.Cx) > CACH_NPC || Math.Abs(npc.Y - mc.Cy) > CACH_NPC)
            {
                _nav.CharBurstMove(npc.X, npc.Y);
                ms = Nghi("toi Thu kho", 600);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Bao dam da co danh sach ruong. <paramref name="docLai"/> = xin lai du da co (viec DocRuong).
        /// Mo ruong KHONG qua menu NPC: dung sat NPC 5 roi gui <c>-30 {-103, 4}</c> (SPEC D42).
        /// </summary>
        private bool DamBaoRuong(KhoDieuPhoi dp, bool docLai, out int ms)
        {
            // ⚠ BIEN CHO RIENG (_choDsRuong), KHONG dung chung _choBoxSeq cua buoc lay/cat mon. Test song
            // 16/09: dung chung thi LayDuTuRuong "an" mat tin ruong ve, ham nay tuong chua co tra loi va
            // xin lai 3-4 lan/giay - 1.537 goi trong 7 phut.
            ms = 0;
            var mc = Client.GameState.MyChar;
            if (_choDsRuong >= 0)
            {
                if (Client.GameState.BoxSeq != _choDsRuong && mc.BoxItems != null)
                {
                    _choDsRuong = -1;
                    _xinRuongLan = 0;
                    Client.Log("[Kho] Ruong: " + mc.BoxItems.Length + " o, trong " + mc.FreeBoxSlots());
                    return true;
                }
                if (DateTime.UtcNow < _choDsRuongDen) { ms = 150; return false; }
                _choDsRuong = -1;
                if (_xinRuongLan >= XIN_RUONG_TOI_DA)
                {
                    _xinRuongLan = 0;
                    KetThucViec(dp, false, MaLoiViec.RUONG_KHONG_DAP, "xin danh sach ruong " + XIN_RUONG_TOI_DA + " lan khong thay tra loi");
                    ms = 3000;
                    return false;
                }
            }
            else if (mc.BoxItems != null && !docLai) return true;
            else if (_xinRuongLan >= XIN_RUONG_TOI_DA)
            {
                // Chot chan cuoi: khong duong nao duoc xin qua so lan nay trong mot viec.
                _xinRuongLan = 0;
                KetThucViec(dp, false, MaLoiViec.RUONG_KHONG_DAP, "da xin danh sach ruong " + XIN_RUONG_TOI_DA + " lan trong mot viec");
                ms = 3000;
                return false;
            }

            // San nhip: du co loi logic nao khac, khong bao gio gui qua mot goi xin ruong moi 2 giay.
            var tu = (DateTime.UtcNow - _xinRuongLuc).TotalMilliseconds;
            if (tu < SAN_XIN_RUONG_MS) { ms = (int)(SAN_XIN_RUONG_MS - tu) + 10; return false; }

            _xinRuongLan++;
            _xinRuongLuc = DateTime.UtcNow;
            _choDsRuong = Client.GameState.BoxSeq;
            _choDsRuongDen = DateTime.UtcNow.AddMilliseconds(CHO_RUONG_DAP_MS);
            Client.Items.SendRequestItem(Service.ItemService.TYPE_BOX);
            Client.Log("[Kho] Xin danh sach ruong (lan " + _xinRuongLan + ")");
            ms = 200;
            return false;
        }

        private int _choDsRuong = -1;
        private DateTime _choDsRuongDen;
        private DateTime _xinRuongLuc = DateTime.MinValue;
        private const int XIN_RUONG_TOI_DA = 3;
        private const int SAN_XIN_RUONG_MS = 2000;

        private static int DemTrongTui(Item[] bag, KhoaMon k)
        {
            int n = 0;
            foreach (var it in bag) if (k.Khop(it)) n += Math.Max(1, (int)it.Quantity);
            return n;
        }

        private static int DemOTrong(Item[] bag)
        {
            int n = 0;
            foreach (var it in bag) if (it == null || it.IsEmpty) n++;
            return n;
        }

        private int Nghi(string lyDo, int ms)
        {
            _lyDoCuoi = lyDo;
            return ms;
        }
    }
}
