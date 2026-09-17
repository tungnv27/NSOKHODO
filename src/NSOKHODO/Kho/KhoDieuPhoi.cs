using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using NSOKHODO.Auto;
using NSOKHODO.Client;
using NSOKHODO.Fleet;
using NSOKHODO.Models;
using NSOKHODO.Protocol;

namespace NSOKHODO.Kho
{
    public enum VaiKho { Leader, DuPhong, Clone, KhongThuoc }

    /// <summary>So lieu trong ngay cho bao cao ngay (D36). Qua nua dem thi dem lai tu 0.</summary>
    public sealed class ThongKeNgay
    {
        public DateTime Ngay = DateTime.Today;
        public int NapLuot, NapMon, XuatLenh, XuatMon, DonLuot, DonMon;
        public long NapXu;

        public ThongKeNgay Chep()
        {
            return (ThongKeNgay)MemberwiseClone();
        }
    }

    /// <summary>
    /// BO DIEU PHOI KHO — MOT cho moi tien trinh (SPEC §11). Luong rieng, nhip 1 giay.
    ///
    /// <para><b>Mot luong duy nhat quyet dinh.</b> Mode kho cua tung acc (luong rieng) chi: xin viec
    /// (<see cref="LayViec"/>), hoi co nhan loi moi khong (<see cref="XetLoiMoi"/>), tim nguoi
    /// (<see cref="TimNguoi"/>), roi BAO ket qua (<see cref="BaoViec"/>, <see cref="BaoPhien"/>) —
    /// bao cao chi XEP HANG, nhip sau cua luong dieu phoi moi xu ly. Giao dien cung vay: moi lenh tu
    /// tool di qua <see cref="Lam"/>. Nho vay hang cho, giu cho, don kho khong bao gio bi hai luong
    /// sua cung luc.</para>
    ///
    /// <para><b>Thu tu moi nhip:</b> chup client → lenh tu giao dien → su kien (chat / phien / viec)
    /// → cap nhat so kho → bau Leader → tinh bang tong → don viec treo → nha clone → lenh rut →
    /// don kho Leader → xa nhanh (D86) → bao tri clone → gom do xep chong (D81) → theo doi / ke Rac / bao cao ngay
    /// → xa chat → luu dia.</para>
    /// </summary>
    public sealed class KhoDieuPhoi : IDisposable
    {
        public static KhoDieuPhoi HienTai { get; private set; }

        private const int NHIP_MS = 1000;
        private const string RUT = "rut", DON = "don", DON_XU = "donxu", TRA = "tra", GOM = "gom", XA = "xa";
        /// <summary>D86: ly do mo xa nhanh.</summary>
        private const string XA_NAP = "nap", XA_DAY = "day", XA_LENH = "lenh";

        /// <summary>D86: xa nhanh ket thuc sau bay nhieu phut khong nap / chuyen them; toi da bay nhieu clone dung canh Leader.</summary>
        private const int XA_PHUT = 5, XA_SO_CLONE = 2;
        /// <summary>D86: clone dung cach cho Leader bay nhieu px - trong tam moi 40 px cua mode, Leader khong phai di.</summary>
        private const int XA_LECH_X = 30;
        /// <summary>
        /// D86: clone dung canh khi tui con nhan duoc tron mot luot (12 o, khong tinh o chua); duoi muc do thi roi di cat
        /// ruong, clone khac vao thay.
        /// </summary>
        private const int XA_TUI_TOI_THIEU = 12;
        /// <summary>D86: clone duoc goi ma qua bay nhieu ms chua vao duoc khu chinh (khu day) thi bo.</summary>
        private const int XA_TOI_KHU_MS = 90000;
        /// <summary>D86: clone vua vao khu - cho 2,5 giay moi di toi canh Leader (M24), them 1 giay cho ca khu thay no toi.</summary>
        private const int XA_CHO_SAU_VAO_KHU_MS = 3500;
        /// <summary>D86: Chu kho bao xong / roi khu chinh bay nhieu giay -> chuyen not phan con lai (toi da bay nhieu giay nua) roi dong.</summary>
        private const int XA_VET_GIAY = 60;
        /// <summary>
        /// D86: clone da dung san ma Leader van khong thay qua bay nhieu giay -> doi clone khac. Leader vao lai game khong thay
        /// nguoi dung san (danh sach chi nap tu cmd 3 - D52); clone moi vao khu thi Leader thay (review 17/09).
        /// </summary>
        private const int XA_KHONG_THAY_GIAY = 10;

        /// <summary>
        /// So o tui clone LUON de trong khi don kho (D78). User 17/09: tungkhodo6 tui 30/30 (toan da) + ruong
        /// 30/30 -> khong lay duoc 17 mon trong ruong ra (khong con o nao de doi cho) -> lenh bao "chi con 0/17".
        /// Con 1 o thi buoc lay ruong van doi cho duoc (lay 1, cat 1 mon khac vao cho vua trong).
        /// </summary>
        private const int O_CHUA_TUI = 1;

        /// <summary>Khu giao rieng khong bot nao nhin thay nguoi nhan: clone doi o do toi da bay nhieu (D79).</summary>
        private const int CHO_TIM_KHU_RIENG_MS = 90000;
        /// <summary>Tim o khu rieng khong thay: lan cu clone sang tim tiep cach it nhat bay nhieu.</summary>
        private const int THU_TIM_LAI_GIAY = 60;

        private enum LoaiSuKien { Chat, Phien, Viec }

        private sealed class SuKien
        {
            public LoaiSuKien Loai;
            public NsoClient C;
            public string Acc;
            public string Nguoi;
            public string Tin;
            public PhienGiaoDich Phien;
            public Viec Viec;
            public BaoCaoViec Bao;
        }

        /// <summary>
        /// Mot luot don kho / don xu: clone sang khu chinh cho (pha 1), Leader giao (pha 2). Luot GOM (D81) dung
        /// lai lop nay: <see cref="Clone"/> = clone giao, <see cref="Dich"/> = clone nhan (<see cref="ViecNhan"/>).
        /// </summary>
        private sealed class PhienDon
        {
            public string MucDich;
            public string Clone;
            public string CloneTen;
            public string Nhom;
            public List<DongGiao> Dong = new List<DongGiao>();
            public int Xu;
            public Viec ViecClone;
            public Viec ViecLeader;
            public DateTime TaoLuc = DateTime.Now;
            public int SoLuot;
            // ---- GOM ----
            public string Dich;
            public Viec ViecNhan;
        }

        /// <summary>D86: "xa nhanh" - clone dung sat canh Leader, Leader chuyen tiep do vua nap sang ngay.</summary>
        private sealed class PhienXa
        {
            public string ChuKho;                 // Chu kho mo dot (nhan tin); nap cua Chu kho khac cung duoc chuyen tiep
            public string Leader;                 // Leader luc mo - doi Leader thi dong
            public DateTime Den;
            public readonly List<string> Clone = new List<string>();
            public int Luot, Mon;                 // da chuyen sang clone
            public DateTime VangTu = DateTime.MinValue;
            public DateTime NapLuc = DateTime.MinValue;    // lan cuoi Chu kho nap / moi (tinh la co mat)
            public bool TuLenh;                   // Chu kho nhan `xa` -> bao khi dong
            public DateTime XongLuc = DateTime.MinValue;   // Chu kho nhan `xa xong`
            public Viec ViecLeader;               // luot chuyen dang chay
            public string CloneNhan;
            // clone da san sang ma Leader chua thay (tu luc nao)
            public readonly Dictionary<string, DateTime> KhongThayTu = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            public int HongLienTiep;
            public DateTime NghiDen = DateTime.MinValue;
        }

        public KhoConfig Cfg { get; private set; }
        public SoKho So { get; private set; }
        public HangCho Hang { get; private set; }
        public KenhChat Kenh { get; private set; }

        /// <summary>Dong log cho o log cua giao dien (da ghi app.log roi - ben nghe KHONG ghi file lan nua).</summary>
        public event Action<string> OnLog;

        /// <summary>
        /// D83: dong DE DOC cho nguoi dung thuong - ai giao gi cho ai, lenh nao xong / hong (co dau, ten mon that).
        /// Da ghi sukien.log roi - ben nghe KHONG ghi file lan nua.
        /// </summary>
        public event Action<string> OnSuKien;

        private readonly FleetManager _fleet;
        private readonly Func<List<AccountConfig>> _dsAcc;
        private readonly object _lk = new object();
        private readonly ConcurrentQueue<SuKien> _suKien = new ConcurrentQueue<SuKien>();
        private readonly ConcurrentQueue<Action> _viecUi = new ConcurrentQueue<Action>();
        private readonly HashSet<NsoClient> _daMoc = new HashSet<NsoClient>();
        private Thread _luong;
        private volatile bool _chay;

        // ---- anh chup moi nhip: THAY CA doi tuong, khong sua tai cho -> luong khac doc an toan ----
        private volatile Dictionary<string, NsoClient> _clientTheoAcc = new Dictionary<string, NsoClient>(StringComparer.OrdinalIgnoreCase);
        private volatile Dictionary<string, AccountConfig> _accTheoTen = new Dictionary<string, AccountConfig>(StringComparer.OrdinalIgnoreCase);
        private volatile HashSet<string> _khacMayChu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private volatile HashSet<string> _tenBot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private volatile List<TonMon> _bangTon = new List<TonMon>();
        private volatile SucChua _sucChua = new SucChua();
        private volatile SucChua _sucChuaRac = new SucChua();
        private long _tongXu;

        // ---- viec: acc -> viec (cho lay hoac dang lam). Khoa _lk ----
        private readonly Dictionary<string, Viec> _viec = new Dictionary<string, Viec>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _viecDaLay = new HashSet<int>();

        // ---- Leader ----
        private volatile string _leaderAcc = "";
        private DateTime _chinhVangTu = DateTime.MinValue;
        private DateTime _chinhVeTu = DateTime.MinValue;

        // ---- giu cua (D41) + loi moi gan nhat cua Leader. Khoa _lk ----
        private string _giuCuaCho;
        private DateTime _giuCuaDen;
        private DateTime _moiDenLeaderLuc = DateTime.MinValue;

        // ---- chi luong dieu phoi doc/ghi ----
        private PhienDon _don;
        private DateTime _donNghiDen = DateTime.MinValue;
        // ---- gom do xep chong (D81) - doc lap voi _don (khong dung toi Leader) ----
        private PhienDon _gom;
        private string _gomDich;                          // clone dich cua dot gom dang chay
        private HashSet<short> _gomLoai = new HashSet<short>();
        private DateTime _gomDotLuc;
        private int _gomDotLuot, _gomDotMon;
        private DateTime _gomNghiDen = DateTime.MinValue;
        private DateTime _gomXetLuc = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _gomTranh = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        // ---- xa nhanh (D86) ----
        private PhienXa _xa;
        private volatile string _moTaXa = "";
        private readonly Dictionary<string, DateTime> _xaTranh = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _nghiBaoTri = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _nghiNhanDon = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _canCat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _canDocRuong = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _tranh = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _lechDem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AccountConfig> _choNha = new Dictionary<string, AccountConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _truocNha = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _offlineTu = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _theoDoiTruoc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _logHanChe = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _daBaoKhacMayChu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private volatile bool _khoDay;
        private DateTime _soLieuDoiLuc = DateTime.MinValue;
        private int _dungTruoc = -1;
        private DateTime _luuLuc = DateTime.MinValue;
        private DateTime _theoDoiLuc = DateTime.MinValue;
        private string _baoCaoDaGui = "";
        private ThongKeNgay _tk = new ThongKeNgay();

        public KhoDieuPhoi(FleetManager fleet, Func<List<AccountConfig>> dsAcc, KhoConfig cfg)
        {
            _fleet = fleet;
            _dsAcc = dsAcc;
            Cfg = cfg ?? new KhoConfig();
            So = new SoKho();
            Hang = new HangCho();
            Kenh = new KenhChat();
        }

        /// <summary>Nap so kho / hang cho / bang ten mon tu dia roi chay luong. Goi TRUOC khi mo acc nao.</summary>
        private DateTime _batDauLuc = DateTime.Now;

        public void BatDau()
        {
            _batDauLuc = DateTime.Now;
            BangMon.Nap();
            So.Nap();
            Hang.Nap();
            HienTai = this;
            _chay = true;
            _luong = new Thread(Vong) { IsBackground = true, Name = "NSO-DieuPhoi" };
            _luong.Start();
            Ghi("-", "Kho", "Bo dieu phoi khoi dong. Lenh rut dang mo: " + Hang.SoCho);
        }

        public void Dispose()
        {
            _chay = false;
            try { if (_luong != null) _luong.Join(3000); } catch { }
            LuuHet();
            if (HienTai == this) HienTai = null;
        }

        // =====================================================================
        // API cho GIAO DIEN (moi thao tac xep hang, luong dieu phoi lam)
        // =====================================================================

        public void Lam(Action a) { if (a != null) _viecUi.Enqueue(a); }

        public string LeaderAcc { get { return _leaderAcc; } }
        public string TenLeader { get { return string.IsNullOrEmpty(_leaderAcc) ? "" : TenNhanVat(_leaderAcc); } }
        public List<TonMon> BangTon { get { return _bangTon; } }
        public SucChua SucChuaKho { get { return _sucChua; } }
        public SucChua SucChuaRac { get { return _sucChuaRac; } }
        public long TongXu { get { return Interlocked.Read(ref _tongXu); } }
        public bool KhoDay { get { return _khoDay; } }
        public ThongKeNgay ThongKe { get { lock (_lk) return _tk.Chep(); } }

        public string GiuCuaCho
        {
            get { lock (_lk) return _giuCuaCho != null && DateTime.Now < _giuCuaDen ? _giuCuaCho : null; }
        }

        public Viec ViecCua(string acc)
        {
            if (string.IsNullOrEmpty(acc)) return null;
            lock (_lk)
            {
                Viec v;
                return _viec.TryGetValue(acc, out v) ? v : null;
            }
        }

        public bool LaAccKhacMayChu(string acc) { return acc != null && _khacMayChu.Contains(acc); }

        /// <summary>So clone (khong tinh Leader / du phong / acc da nha) dang online va tong so.</summary>
        public void DemClone(out int online, out int tong)
        {
            online = 0; tong = 0;
            var cs = _clientTheoAcc;
            foreach (var acc in _accTheoTen.Keys)
            {
                if (!LaCloneKho(acc)) continue;
                tong++;
                NsoClient c;
                if (cs.TryGetValue(acc, out c) && c.State == ClientState.InGame) online++;
            }
        }

        /// <summary>Chu cho cot "Trang thai kho" tren luoi acc.</summary>
        public string TrangThaiKho(string acc, NsoClient c)
        {
            if (string.IsNullOrEmpty(acc)) return "";
            if (Cfg.DaNha(acc)) return "ĐÃ NHẢ";
            if (LaAccKhacMayChu(acc)) return "KHÁC MÁY CHỦ";
            if (c == null || !c.IsOnline) return "Offline";
            var m = c.ActiveModeAs<KhoMode>();
            var p = m != null ? m.PhienHienTai : null;
            if (p != null && !p.KetThuc) return p.Vai == VaiGiaoDich.Nhan ? "Đang nhận" : "Đang giao";
            var v = ViecCua(acc);
            if (v != null) return v.MoTa;
            if (CungAcc(acc, _leaderAcc))
            {
                string g = GiuCuaCho;
                if (g != null) return "Giữ cửa: " + g;
                if (_khoDay) return "ĐẦY";
            }
            string act = c.ActivityText;
            return string.IsNullOrEmpty(act) ? "Sẵn sàng" : act;
        }

        /// <summary>
        /// Tao lenh rut tu tool. <paramref name="soLuong"/> -1 = het; <paramref name="cap"/> -1 = bat ky;
        /// <paramref name="khu"/> -1 = khu chinh (D79).
        /// </summary>
        public void RutTuTool(short tpl, int cap, int soLuong, string nguoiNhan, int khu = -1)
        {
            Lam(() => TaoLenh("tool", null, nguoiNhan, null,
                new List<DongLenh> { new DongLenh { Tpl = tpl, Cap = cap, SoXin = soLuong } }, DateTime.Now, khu));
        }

        /// <summary>Mot lenh nhieu mon (don soan tren tab Tong kho) - giao chung mot luot nhu goi rut.</summary>
        public void RutNhieuTuTool(List<DongLenh> dong, string nguoiNhan, int khu = -1)
        {
            if (dong == null || dong.Count == 0) return;
            var chep = dong.Select(d => new DongLenh { Tpl = d.Tpl, Cap = d.Cap, SoXin = d.SoXin }).ToList();
            Lam(() => TaoLenh("tool", null, nguoiNhan, null, chep, DateTime.Now, khu));
        }

        public void RutGoiTuTool(string tenGoi, string nguoiNhan, int khu = -1)
        {
            Lam(() =>
            {
                var g = Cfg.LayGoi(tenGoi);
                if (g == null) { Ghi("-", "Lenh", "Khong co goi rut '" + tenGoi + "'"); return; }
                TaoLenh("tool", null, nguoiNhan, tenGoi, DongTuGoi(g), DateTime.Now, khu);
            });
        }

        public void TiepLenhTuTool(int so)
        {
            Lam(() =>
            {
                var l = Hang.Tim(so);
                if (l == null || !l.DangMo) { Ghi("-", "Lenh", "Lenh #" + so + " khong con mo"); return; }
                Ghi("-", "Lenh", TiepLenh(l, DateTime.Now));
            });
        }

        public void HuyLenhTuTool(int so)
        {
            Lam(() =>
            {
                var l = Hang.Tim(so);
                if (l == null || !l.DangMo) { Ghi("-", "Lenh", "Lenh #" + so + " khong con mo"); return; }
                HuyLenh(l, "huy tu tool", DateTime.Now);
            });
        }

        /// <summary>Nha clone (SPEC §6.2). Tra ve ly do tu choi ngay, null = da xep hang.</summary>
        public string NhaClone(AccountConfig a)
        {
            if (a == null || string.IsNullOrEmpty(a.Username)) return "Chưa chọn acc.";
            if (CungAcc(a.Username, Cfg.Leader) || CungAcc(a.Username, _leaderAcc))
                return "Không nhả Leader (" + a.Username + "). Đổi vai Leader sang acc khác trước.";
            if (Cfg.DaNha(a.Username)) return a.Username + " đã nhả rồi.";
            Lam(() =>
            {
                _choNha[a.Username] = a;
                Ghi(a.Username, "NHA_CLONE", "Xep hang nha " + a.Username + " (cho xong phien / viec neu dang lam)");
            });
            return null;
        }

        /// <summary>Nhan lai clone da nha: dang nhap lai, doc lai rương.</summary>
        public string NhanLaiClone(AccountConfig a)
        {
            if (a == null || string.IsNullOrEmpty(a.Username)) return "Chưa chọn acc.";
            if (!Cfg.DaNha(a.Username)) return a.Username + " không ở trạng thái nhả.";
            Lam(() =>
            {
                string u = a.Username;
                _choNha.Remove(u);
                _truocNha[u] = MoTaTui(So.Lay(u));
                Cfg.DatNha(u, false);
                LuuCfg();
                _canDocRuong.Add(u);
                _fleet.StartAccount(a);
                Ghi(u, "NHA_CLONE", "Nhan lai " + u + " - truoc: " + _truocNha[u]);
                BaoChuKho("Da nhan lai " + TenNhanVat(u), true, null, 0);
            });
            return null;
        }

        public void LuuCfg()
        {
            string loi = Cfg.Luu();
            if (loi != null) Ghi("-", "Loi", "Luu cai dat kho loi: " + loi);
        }

        // =====================================================================
        // API cho MODE KHO (goi tu luong cua tung acc)
        // =====================================================================

        public VaiKho VaiCua(NsoClient c)
        {
            return c == null || c.Config == null ? VaiKho.KhongThuoc : VaiCuaAcc(c.Config.Username);
        }

        public VaiKho VaiCuaAcc(string acc)
        {
            if (string.IsNullOrEmpty(acc) || Cfg.DaNha(acc) || LaAccKhacMayChu(acc)) return VaiKho.KhongThuoc;
            if (CungAcc(acc, _leaderAcc)) return VaiKho.Leader;
            if (CungAcc(acc, Cfg.Leader) || CungAcc(acc, Cfg.LeaderDuPhong)) return VaiKho.DuPhong;
            return VaiKho.Clone;
        }

        public Viec LayViec(NsoClient c)
        {
            if (c == null || c.Config == null) return null;
            lock (_lk)
            {
                Viec v;
                if (!_viec.TryGetValue(c.Config.Username, out v) || v.BiHuy || _viecDaLay.Contains(v.Id)) return null;
                _viecDaLay.Add(v.Id);
                return v;
            }
        }

        public void BaoViec(NsoClient c, BaoCaoViec bc)
        {
            if (c == null || c.Config == null || bc == null || bc.Viec == null) return;
            _suKien.Enqueue(new SuKien { Loai = LoaiSuKien.Viec, C = c, Acc = c.Config.Username, Bao = bc });
        }

        public void BaoPhien(NsoClient c, PhienGiaoDich p, Viec v)
        {
            if (c == null || c.Config == null || p == null) return;
            _suKien.Enqueue(new SuKien { Loai = LoaiSuKien.Phien, C = c, Acc = c.Config.Username, Phien = p, Viec = v });
        }

        /// <summary>
        /// Quyet dinh cho loi moi giao dich den (SPEC §5 N1, D41).
        /// <para><b>Khac SPEC N1 co chu dich:</b> id khong co trong danh sach cung khu thi Leader VAN nhan
        /// va kiem ten o goi 37 — danh sach chi nap tu cmd 3 nen Leader vao lai game se khong thay nguoi
        /// dung san (docs/STATUS.md).</para>
        /// </summary>
        public QuyetDinhLoiMoi XetLoiMoi(NsoClient c, int id, Viec viecDangLam)
        {
            if (c == null || c.Config == null) return QuyetDinhLoiMoi.BoQua();
            string acc = c.Config.Username;

            // 1. Bot cua kho moi -> chi nhan khi dang co viec doi nhan dung bot do.
            var botMoi = TimBotTheoId(id);
            if (botMoi != null)
            {
                var v = ViecCua(acc);
                if (v != null && v.Loai == LoaiViec.DoiNhan && !v.BiHuy && CungAcc(v.TuBotAcc, botMoi.Config.Username))
                {
                    return new QuyetDinhLoiMoi
                    {
                        Nhan = true,
                        TenMongDoi = botMoi.DisplayCharName,
                        ChoMs = Math.Max(5, Cfg.ChoBotGiay) * 1000,
                        KiemHang = (xu, mon) => KiemHangNhanThem(c, xu, mon),
                        LaAi = "bot",
                    };
                }
                GhiHanChe("moibot|" + acc, 30, acc, "GD", "Tu choi loi moi cua bot " + botMoi.Config.Username + " (khong co viec doi nhan)");
                return QuyetDinhLoiMoi.KhongNhan();
            }

            if (VaiCua(c) != VaiKho.Leader)
            {
                // D86: nguoi choi chi giao dich voi Leader - clone dung canh nhan do tu Leader (nhanh 2-3 giay/luot).
                GhiHanChe("moiclone|" + acc, 30, acc, "GD", "Tu choi loi moi id " + id + ": chi Leader nhan do");
                return QuyetDinhLoiMoi.KhongNhan();
            }

            string giuCho;
            DateTime giuDen;
            lock (_lk)
            {
                giuCho = _giuCuaCho;
                giuDen = _giuCuaDen;
            }
            bool dangGiu = giuCho != null && DateTime.Now < giuDen;
            string ten = TenTheoId(c, id);
            bool laChu = ten != null && Cfg.LaChuKho(ten);
            string ai = ten ?? ("id " + id);

            // Leader dang dung doi clone tra bot (D78): nhan nguoi khac luc nay thi loi moi cua clone bi lo ->
            // go ket hong, nghi 10 phut (review 17/09). Luot ngan (< 1 phut); Chu kho can gap thi nhan `nap`.
            var viecLeader = ViecCua(acc);
            if (viecLeader != null && viecLeader.Loai == LoaiViec.DoiNhan && !viecLeader.BiHuy)
            {
                if (laChu)
                {
                    // D87: tu choi = Chu kho bi khoa moi 30 giay -> nhan roi huy ngay (khung chi mo ~0,1 giay; clone moi
                    // trung luc do thi server bao "dang giao dich khac" va clone moi lai sau 3 giay - M27).
                    BaoRieng(ten, "Leader dang nhan do tu clone, thu lai sau ~30 giay (hoac nhan: nap)", "doitra", 30);
                    GhiHanChe("doitra|" + acc, 30, acc, "GD", "Nhan roi huy ngay " + ai + ": dang doi clone " + viecLeader.TuBotAcc + " tra bot");
                    return NhanRoiHuy(ten, "Leader dang doi clone tra bot");
                }
                Ghi(acc, "GD", "Tu choi " + ai + ": dang doi clone " + viecLeader.TuBotAcc + " tra bot");
                return QuyetDinhLoiMoi.KhongNhan();
            }

            if (!Cfg.BatNap)
            {
                if (laChu) BaoRieng(ten, "Kho dang tat nhan do", "tatnap", 30);
                Ghi(acc, "GD", "Tu choi " + ai + ": dang tat nhan do");
                return QuyetDinhLoiMoi.KhongNhan();
            }
            if (ten != null && dangGiu && !ChuVan.CungTen(ten, giuCho))
            {
                Ghi(acc, "GD", "Tu choi " + ai + ": dang giu cua cho chu kho " + giuCho);
                return QuyetDinhLoiMoi.KhongNhan();
            }
            if (ten != null && !laChu && Cfg.CheDoNhan == CheDoNhan.ChiChuKho)
            {
                Ghi(acc, "GD", "Tu choi " + ai + ": che do chi nhan cua Chu kho");
                return QuyetDinhLoiMoi.KhongNhan();
            }
            int trong = DemTuiTrong(c);
            int nguong = dangGiu ? 1 : Math.Max(1, Cfg.NguongNhan);
            if (trong < nguong)
            {
                if (laChu)
                {
                    // D87: TU CHOI thi Chu kho bi server khoa moi 30 giay (M28); NHAN roi huy ngay khi khung mo thi moi lai
                    // duoc ngay. D86: dong thoi xa nhanh - clone dung canh, Leader chuyen tiep, vai giay sau la con cho.
                    // MoXa tu bao "moi lai sau ~3 giay" / "dang don kho".
                    string chu = ten;
                    if (_khoDay) BaoRieng(ten, "Kho day, chua nhan them duoc", "donkho", 30);
                    else
                    {
                        // Dang giu cua cho chinh Chu kho nay ma Leader het cho: giu cung vo ich, lai chan ca chuyen tiep lan
                        // don kho toi het GiuCuaGiay (review 17/09) -> bo giu cua.
                        if (dangGiu)
                            lock (_lk) { if (_giuCuaCho != null && ChuVan.CungTen(_giuCuaCho, ten)) _giuCuaCho = null; }
                        Lam(() => MoXa(chu, DateTime.Now, XA_DAY));
                    }
                    GhiHanChe("hetcho|" + acc, 30, acc, "GD", "Nhan roi huy ngay " + ai + ": tui Leader con " + trong + " o (nguong " + nguong + ")");
                    return NhanRoiHuy(ten, _khoDay ? "kho day" : "Leader con " + trong + " o");
                }
                Ghi(acc, "GD", "Tu choi " + ai + ": tui Leader con " + trong + " o (nguong " + nguong + ")");
                return QuyetDinhLoiMoi.KhongNhan();
            }

            // Chi danh dau "sap giao dich" khi NHAN: loi moi bi tu choi (Leader day) ma van dat moc thi Chu kho moi lien tuc
            // lam LeaderRanh() sai mai -> Leader khong bao gio don kho (user 17/09: "nhieu luc con khong don").
            lock (_lk) _moiDenLeaderLuc = DateTime.Now;
            var qd = new QuyetDinhLoiMoi
            {
                Nhan = true,
                TenMongDoi = ten,
                ChoMs = (laChu || dangGiu || (ten == null && Cfg.CheDoNhan == CheDoNhan.ChiChuKho)
                            ? Cfg.ChoNguoiGiay : Cfg.ChoNguoiLaGiay) * 1000,
                KiemHang = (xu, mon) => KiemHangNap(c, xu, mon),
                LaAi = laChu ? "chukho" : (ten == null ? "chuaro" : "nguoila"),
            };
            if (ten == null) qd.KiemTen = KiemTenNguoiNap;
            Ghi(acc, "GD", "Nhan loi moi cua " + ai + " [" + qd.LaAi + "]");
            return qd;
        }

        /// <summary>
        /// D87: nhan loi moi cua Chu kho roi huy ngay khi khung mo - TU CHOI thi Chu kho bi server khoa moi 30 giay,
        /// phien da mo roi huy thi moi lai duoc ngay (M28).
        /// </summary>
        private QuyetDinhLoiMoi NhanRoiHuy(string ten, string lyDo)
        {
            return new QuyetDinhLoiMoi
            {
                Nhan = true,
                TenMongDoi = ten,
                ChoMs = Cfg.ChoNguoiGiay * 1000,
                LaAi = "chukho",
                HuyKhiMo = lyDo,
            };
        }

        /// <summary>
        /// Kiem hang NGUOI NAP dat vao (Leader nhan). Khac <see cref="KiemHangNhanThem"/>: khoa rong thi tu choi -
        /// test song 16/09 nguoi choi khoa rong roi dong y, phien "XONG" khong co gi, chi lam Leader ban.
        /// (Ben GIAO khong duoc dung ham nay: nguoi nhan luon khoa rong.)
        /// </summary>
        public string KiemHangNap(NsoClient c, int xu, MonGiaoDich[] mon)
        {
            if ((mon == null || mon.Length == 0) && xu <= 0) return "doi phuong khong dat mon hay xu nao";
            return KiemHangNhanThem(c, xu, mon);
        }

        /// <summary>Kiem hang doi phuong dat vao: du o trong tui (moi mon mot o moi) + khong vuot tran xu.</summary>
        public string KiemHangNhanThem(NsoClient c, int xu, MonGiaoDich[] mon)
        {
            var mc = c != null ? c.GameState.MyChar : null;
            if (mc == null) return "chua co du lieu nhan vat";
            int n = mon != null ? mon.Length : 0;
            int trong = DemTuiTrong(c);
            if (n > trong) return "tui chi con " + trong + " o, doi phuong dat " + n + " mon";
            if (xu < 0) return "so xu khong hop le";
            if ((long)mc.Xu + xu > Cfg.XuTran)
                return "vuot tran xu (" + ChuVan.SoDep(mc.Xu) + " + " + ChuVan.SoDep(xu) + " > " + ChuVan.SoDep(Cfg.XuTran) + ")";
            return null;
        }

        /// <summary>
        /// Tim nguoi o KHU CHINH. Bot cua kho: lay tu chinh no. Nguoi ngoai: nhin qua cac bot dang o khu
        /// chinh, uu tien Leader (clone vua sang khu co the chua thay nguoi dung san).
        /// </summary>
        public PlayerInfo TimNguoi(string tenNhanVat)
        {
            return TimNguoi(tenNhanVat, Cfg.KhuChinh);
        }

        /// <summary>
        /// Nhu <see cref="TimNguoi(string, int)"/> nhung uu tien toa do ma <paramref name="nhin"/> (acc giao) dang
        /// thay - la toa do server phat cho no. Bot nhan tu biet vi tri "lac quan" (gan ngay khi gui lenh di); test song
        /// 17/09: buoc di cua clone vua vao khu bi server bo, clone tuong o 335 / 395 trong khi ca khu thay 420 ->
        /// Leader lai sat 335, moi 4 lan deu "qua xa".
        /// </summary>
        public PlayerInfo TimNguoi(string tenNhanVat, int khu, NsoClient nhin)
        {
            if (string.IsNullOrEmpty(tenNhanVat)) return null;
            if (nhin != null && OKhu(nhin, khu))
            {
                // Nguoi nhan la bot: chi tin mat acc giao khi bot do dang o dung khu (danh sach cua acc giao co the cu).
                foreach (var c in _clientTheoAcc.Values)
                    if (c.State == ClientState.InGame && ChuVan.CungTen(c.DisplayCharName, tenNhanVat) && !OKhu(c, khu)) return null;
                var p = TimTrongKhu(nhin, x => ChuVan.CungTen(x.Name, tenNhanVat));
                if (p != null) return ChepNguoi(p);
            }
            return TimNguoi(tenNhanVat, khu);
        }

        /// <summary>Tim nguoi o khu <paramref name="khu"/> (D79: khu giao cua lenh) qua cac bot dang dung o khu do.</summary>
        public PlayerInfo TimNguoi(string tenNhanVat, int khu)
        {
            if (string.IsNullOrEmpty(tenNhanVat)) return null;
            var cs = _clientTheoAcc;
            foreach (var c in cs.Values)
            {
                if (c.State != ClientState.InGame || !ChuVan.CungTen(c.DisplayCharName, tenNhanVat)) continue;
                return OKhu(c, khu) ? NguoiTuBot(c) : null;
            }
            var L = LeaderClient;
            if (L != null && OKhu(L, khu))
            {
                var p = TimTrongKhu(L, x => ChuVan.CungTen(x.Name, tenNhanVat));
                if (p != null) return ChepNguoi(p);
            }
            foreach (var c in cs.Values)
            {
                if (c == L || !OKhu(c, khu)) continue;
                var p = TimTrongKhu(c, x => ChuVan.CungTen(x.Name, tenNhanVat));
                if (p != null) return ChepNguoi(p);
            }
            return null;
        }

        public PlayerInfo TimNguoiTheoAcc(string username)
        {
            return TimNguoiTheoAcc(username, Cfg.KhuChinh);
        }

        /// <summary>Bot cua kho dang dung o khu <paramref name="khu"/> (luot gom D81 o khu cua clone).</summary>
        public PlayerInfo TimNguoiTheoAcc(string username, int khu)
        {
            var c = LayClient(username);
            if (c == null || c.State != ClientState.InGame || !OKhu(c, khu)) return null;
            return NguoiTuBot(c);
        }

        /// <summary>Mo ta dot xa nhanh dang chay cho thanh trang thai (D86). Rong = khong co.</summary>
        public string MoTaXa { get { return _moTaXa; } }

        // acc -> khu clone dung khi CHUA cai khu phu (khu no dang dung luc moi vao map kho). Song qua
        // dang nhap lai: mode kho tao moi moi lan vao game, nho o mode thi mat.
        private readonly ConcurrentDictionary<string, int> _khuNha = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Khu acc dung cho khi ranh. Leader: khu chinh. Con lai: khu phu; CHUA cai khu phu thi la khu
        /// acc dang dung luc moi vao map kho (user test 16/09 de trong khu phu - truoc day ca kho dung im,
        /// ke ca Leader). -1 = khong doi khu (chi can dung map).
        /// </summary>
        public int KhuDungCho(NsoClient c)
        {
            if (c == null || c.Config == null) return -1;
            if (VaiCua(c) == VaiKho.Leader) return Cfg.KhuChinh;
            if (Cfg.KhuPhu >= 0) return Cfg.KhuPhu;
            int k;
            if (_khuNha.TryGetValue(c.Config.Username, out k)) return k;
            var st = c.GameState;
            var m = st.CurrentMap;
            if (c.State != ClientState.InGame || st.IsChangingMap || m == null || m.MapId != Cfg.Map) return -1;
            if (m.ZoneId != Cfg.KhuChinh)
            {
                _khuNha[c.Config.Username] = m.ZoneId;
                return m.ZoneId;
            }
            // Vao game ngay o khu chinh: server xep khu luc dang nhap (khu thap con cho la vao - test song
            // 16/09 ca 9 acc vao khu 0). Ve khu nhieu clone khac dang o nhat, chua co thi khu sat ben.
            var dem = _khuNha.Values.Where(z => z != Cfg.KhuChinh).GroupBy(z => z)
                             .OrderByDescending(g => g.Count()).FirstOrDefault();
            int khu = dem != null ? dem.Key : (Cfg.KhuChinh > 0 ? Cfg.KhuChinh - 1 : Cfg.KhuChinh + 1);
            _khuNha[c.Config.Username] = khu;
            return khu;
        }

        // =====================================================================
        // VONG CHINH
        // =====================================================================

        private string _loiCuoi;
        private DateTime _loiCuoiLuc;

        private void Vong()
        {
            while (_chay)
            {
                var t0 = DateTime.UtcNow;
                try { Nhip(); }
                catch (Exception ex)
                {
                    string s = ex.GetType().Name + ": " + ex.Message;
                    if (s != _loiCuoi || (DateTime.UtcNow - _loiCuoiLuc).TotalSeconds > 60)
                    {
                        _loiCuoi = s;
                        _loiCuoiLuc = DateTime.UtcNow;
                        Ghi("-", "Loi", "Nhip dieu phoi loi: " + ex);
                    }
                }
                int ngu = NHIP_MS - (int)(DateTime.UtcNow - t0).TotalMilliseconds;
                Thread.Sleep(Math.Max(100, ngu));
            }
        }

        private void Nhip()
        {
            var now = DateTime.Now;
            ChupClient();

            Action a;
            int n = 0;
            while (n++ < 50 && _viecUi.TryDequeue(out a))
            {
                try { a(); }
                catch (Exception ex) { Ghi("-", "Loi", "Lenh tu giao dien loi: " + ex.Message); }
            }

            SuKien sk;
            n = 0;
            while (n++ < 300 && _suKien.TryDequeue(out sk))
            {
                try { XuLySuKien(sk, now); }
                catch (Exception ex) { Ghi(sk.Acc, "Loi", "Xu ly su kien " + sk.Loai + " loi: " + ex); }
            }

            DatLaiThongKe(now);
            CanhBaoCaiDat();
            CapNhatSo(now);
            BauLeader(now);
            TinhBang(now);
            DonViecTreo(now);
            XuLyNha(now);
            XuLyLenh(now);
            XuLyDon(now);
            XuLyXa(now);
            BaoTriClone(now);
            // Sau bao tri: clone dich vua nhan xong phai duoc CAT RUONG truoc khi vao luot gom sau.
            XuLyGom(now);
            if ((now - _theoDoiLuc).TotalSeconds >= 5)
            {
                _theoDoiLuc = now;
                TheoDoiMon();
                BaoRac();
                DonTranh(now);
            }
            BaoCaoNgay(now);
            XaChat(now);
            if ((now - _luuLuc).TotalSeconds >= 10)
            {
                _luuLuc = now;
                LuuHet();
            }
        }

        private void LuuHet()
        {
            try { So.LuuNeuCan(); } catch { }
            try { Hang.LuuNeuCan(); } catch { }
            try { BangMon.LuuNeuCan(); } catch { }
        }

        // ---------------- client ----------------

        private void ChupClient()
        {
            var ds = _fleet.Clients;
            var dangCo = new HashSet<NsoClient>(ds);
            var map = new Dictionary<string, NsoClient>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in ds)
            {
                if (c == null || c.Config == null || string.IsNullOrEmpty(c.Config.Username)) continue;
                map[c.Config.Username] = c;
                if (_daMoc.Add(c))
                {
                    c.OnPrivateChatReceived += NhanChatRieng;
                    c.OnRawPacket += NhanGoiTho;
                }
            }
            foreach (var c in _daMoc.ToArray())
            {
                if (dangCo.Contains(c)) continue;
                c.OnPrivateChatReceived -= NhanChatRieng;
                c.OnRawPacket -= NhanGoiTho;
                _daMoc.Remove(c);
                Kenh.XoaHangCua(c);
            }
            _clientTheoAcc = map;

            var now = DateTime.Now;
            foreach (var kv in map)
            {
                if (kv.Value.State == ClientState.InGame)
                {
                    if (_vaoGameLuc.ContainsKey(kv.Key)) continue;
                    _vaoGameLuc[kv.Key] = now;
                    // Vua vao game, chua mo ruong: so kho dang giu ruong CU (luc tat app / roi mang co the da
                    // choi tay, doi do) -> doc lai mot lan truoc khi dem hang do vao ke hoach.
                    var mc = kv.Value.GameState.MyChar;
                    if (mc != null && mc.BoxItems == null) _canDocRuong.Add(kv.Key);
                }
                else _vaoGameLuc.Remove(kv.Key);
            }
            foreach (var k in _vaoGameLuc.Keys.Where(k => !map.ContainsKey(k)).ToList()) _vaoGameLuc.Remove(k);

            var accs = new Dictionary<string, AccountConfig>(StringComparer.OrdinalIgnoreCase);
            AccountConfig[] la = null;
            try { var l = _dsAcc(); if (l != null) la = l.ToArray(); } catch { }
            if (la != null)
                foreach (var x in la)
                    if (x != null && !string.IsNullOrEmpty(x.Username) && !accs.ContainsKey(x.Username)) accs[x.Username] = x;
            _accTheoTen = accs;

            // D11: moi acc trong kho phai cung may chu voi Leader.
            var khac = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AccountConfig la0;
            if (!string.IsNullOrEmpty(Cfg.Leader) && accs.TryGetValue(Cfg.Leader, out la0))
            {
                string mc = MayChuCua(la0);
                foreach (var kv in accs)
                {
                    if (string.Equals(MayChuCua(kv.Value), mc, StringComparison.OrdinalIgnoreCase)) continue;
                    khac.Add(kv.Key);
                    if (_daBaoKhacMayChu.Add(kv.Key))
                        Ghi(kv.Key, "Kho", "Acc " + kv.Key + " khac may chu voi Leader (" + mc + ") - KHONG tinh vao kho");
                }
            }
            _khacMayChu = khac;

            var ten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in map.Values)
            {
                string t = c.DisplayCharName;
                if (!string.IsNullOrEmpty(t)) ten.Add(t.Trim());
            }
            foreach (var t in So.TatCa())
                if (accs.ContainsKey(t.Acc) && !string.IsNullOrEmpty(t.TenNV)) ten.Add(t.TenNV.Trim());
            _tenBot = ten;
        }

        // acc -> luc thay vao game (chi luong dieu phoi). Test song 16/09: server TU CHOI moi loi moi toi
        // nguoi vao game chua toi ~60-80 giay ("X do not accept."); doi khu thi khong bi.
        private readonly Dictionary<string, DateTime> _vaoGameLuc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        // Bo kiem tra offline dat ve 0 qua reflection (client gia vua "vao game" o nhip dau).
        private static int _choSauVaoGameGiay = 90;

        /// <summary>Acc vua vao game - server chua cho ai moi no giao dich.</summary>
        private bool VuaVaoGame(string acc)
        {
            DateTime luc;
            return !_vaoGameLuc.TryGetValue(acc ?? "", out luc) || (DateTime.Now - luc).TotalSeconds < _choSauVaoGameGiay;
        }

        private static string MayChuCua(AccountConfig a)
        {
            if (a == null) return "";
            ServerInfo s = null;
            try { s = ServerList.Resolve(a.ServerName, a.ServerIndex); } catch { }
            return s != null ? s.Name : (a.ServerName ?? a.ServerIndex.ToString(CultureInfo.InvariantCulture));
        }

        private void NhanChatRieng(NsoClient c, string tu, string tin)
        {
            try { NhatKy.Chat(c.Config.Username, false, true, tu, tin); } catch { }
            _suKien.Enqueue(new SuKien { Loai = LoaiSuKien.Chat, C = c, Acc = c.Config.Username, Nguoi = tu, Tin = tin });
        }

        private static readonly HashSet<sbyte> CMD_HEX = new HashSet<sbyte> { 37, 43, 44, 45, 46, 56, 57, 58, 16, 17, 31 };
        private static readonly HashSet<sbyte> CMD_HEX_KHI_GD = new HashSet<sbyte> { 8, 9, -24, -25, -26, 53 };

        /// <summary>Ghi hex goi giao dich / tui / ruong + tin chu server trong luc co phien (SPEC §4 LogHexGiaoDich).</summary>
        private void NhanGoiTho(NsoClient c, sbyte cmd, Core.NsoMessage msg)
        {
            if (!Cfg.LogHexGiaoDich || !NhatKy.Bat) return;
            if (!CMD_HEX.Contains(cmd) && !(c.DangGiaoDich && CMD_HEX_KHI_GD.Contains(cmd))) return;
            try { NhatKy.Hex(c.Config.Username, true, cmd, msg.GetData()); } catch { }
        }

        private NsoClient LayClient(string acc)
        {
            if (string.IsNullOrEmpty(acc)) return null;
            NsoClient c;
            return _clientTheoAcc.TryGetValue(acc, out c) ? c : null;
        }

        private NsoClient LeaderClient
        {
            get
            {
                var c = LayClient(_leaderAcc);
                return c != null && c.State == ClientState.InGame ? c : null;
            }
        }

        private bool DangOnline(string acc)
        {
            var c = LayClient(acc);
            return c != null && c.IsOnline;
        }

        private bool OKhuChinh(NsoClient c)
        {
            return OKhu(c, Cfg.KhuChinh);
        }

        private bool OKhu(NsoClient c, int khu)
        {
            if (c == null || c.State != ClientState.InGame || khu < 0) return false;
            var st = c.GameState;
            if (st.IsChangingMap) return false;
            var m = st.CurrentMap;
            return m != null && m.MapId == Cfg.Map && m.ZoneId == khu;
        }

        /// <summary>Khu giao cua lenh (D79): khu da chon, khong chon thi khu chinh.</summary>
        private int KhuGiao(LenhRut l)
        {
            return l.Khu >= 0 ? l.Khu : Cfg.KhuChinh;
        }

        /// <summary>Co bot nao cua kho dang dung o khu nay (nhin thay nguoi trong khu) khong.</summary>
        private bool CoBotOKhu(int khu)
        {
            foreach (var c in _clientTheoAcc.Values)
                if (OKhu(c, khu)) return true;
            return false;
        }

        private static bool CungKhu(NsoClient a, NsoClient b)
        {
            var ma = a.GameState.CurrentMap;
            var mb = b.GameState.CurrentMap;
            return ma != null && mb != null && ma.MapId == mb.MapId && ma.ZoneId == mb.ZoneId
                   && !a.GameState.IsChangingMap && !b.GameState.IsChangingMap;
        }

        private static PlayerInfo TimTrongKhu(NsoClient c, Func<PlayerInfo, bool> dk)
        {
            PlayerInfo[] arr = null;
            try { arr = c.GameState.CurrentMap.OtherPlayers.ToArray(); } catch { }
            if (arr == null) return null;
            foreach (var p in arr)
                if (p != null && dk(p)) return p;
            return null;
        }

        private static PlayerInfo ChepNguoi(PlayerInfo p)
        {
            return new PlayerInfo { CharId = p.CharId, Name = p.Name, X = p.X, Y = p.Y };
        }

        private static PlayerInfo NguoiTuBot(NsoClient c)
        {
            var mc = c.GameState.MyChar;
            if (mc == null) return null;
            return new PlayerInfo { CharId = mc.CharId, Name = mc.Name, X = mc.Cx, Y = mc.Cy };
        }

        private NsoClient TimBotTheoId(int id)
        {
            foreach (var c in _clientTheoAcc.Values)
            {
                var mc = c.GameState.MyChar;
                if (c.IsOnline && mc != null && mc.CharId == id) return c;
            }
            return null;
        }

        /// <summary>Ten cua id trong khu cua <paramref name="c"/> — hoi ca cac bot khac dang dung cung khu.</summary>
        private string TenTheoId(NsoClient c, int id)
        {
            var p = TimTrongKhu(c, x => x.CharId == id);
            if (p != null) return p.Name;
            foreach (var o in _clientTheoAcc.Values)
            {
                if (o == c || o.State != ClientState.InGame || !CungKhu(o, c)) continue;
                p = TimTrongKhu(o, x => x.CharId == id);
                if (p != null) return p.Name;
            }
            return null;
        }

        private string KiemTenNguoiNap(string ten)
        {
            if (string.IsNullOrEmpty(ten)) return "khong doc duoc ten doi phuong";
            if (LaTenBot(ten)) return ten + " la bot cua kho";
            string giuCho;
            DateTime giuDen;
            lock (_lk) { giuCho = _giuCuaCho; giuDen = _giuCuaDen; }
            if (giuCho != null && DateTime.Now < giuDen && !ChuVan.CungTen(ten, giuCho))
                return "dang giu cua cho chu kho " + giuCho;
            if (Cfg.CheDoNhan == CheDoNhan.ChiChuKho && !Cfg.LaChuKho(ten)) return ten + " khong phai chu kho";
            return null;
        }

        private static int DemTuiTrong(NsoClient c)
        {
            var mc = c != null ? c.GameState.MyChar : null;
            var bag = mc != null ? mc.BagItems : null;
            if (bag == null) return 0;
            int n = 0;
            foreach (var it in bag) if (it == null || it.IsEmpty) n++;
            return n;
        }

        public bool LaTenBot(string ten)
        {
            return !string.IsNullOrEmpty(ten) && _tenBot.Contains(ten.Trim());
        }

        private string TenNhanVat(string acc)
        {
            var c = LayClient(acc);
            if (c != null && c.GameState.MyChar != null && !string.IsNullOrEmpty(c.GameState.MyChar.Name))
                return c.GameState.MyChar.Name;
            var t = So.Lay(acc);
            if (t != null && !string.IsNullOrEmpty(t.TenNV)) return t.TenNV;
            return acc ?? "";
        }

        private static bool CungAcc(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool LaLeader(string acc) { return CungAcc(acc, _leaderAcc); }

        /// <summary>Clone thuc thu: trong danh sach, khong nha, cung may chu, khong phai Leader / du phong.</summary>
        private bool LaCloneKho(string acc)
        {
            return _accTheoTen.ContainsKey(acc) && !Cfg.DaNha(acc) && !LaAccKhacMayChu(acc)
                   && !CungAcc(acc, Cfg.Leader) && !CungAcc(acc, Cfg.LeaderDuPhong) && !CungAcc(acc, _leaderAcc);
        }

        private bool CoViec(string acc)
        {
            if (string.IsNullOrEmpty(acc)) return false;
            lock (_lk) return _viec.ContainsKey(acc);
        }

        private bool GiaoViec(Viec v)
        {
            lock (_lk)
            {
                if (_viec.ContainsKey(v.Acc)) return false;
                _viec[v.Acc] = v;
            }
            Ghi(v.Acc, "Viec", "Giao viec: " + ChuVan.BoDau(v.MoTa) + (v.Dong.Count > 0 ? " [" + MoTaDong(v.Dong) + "]" : "")
                + (v.Xu > 0 ? " + " + v.Xu + " xu" : ""));
            return true;
        }

        /// <summary>Go viec khoi so. false = da go truoc do (bao cao lap) -> bo qua.</summary>
        private bool GoViec(string acc, Viec v)
        {
            lock (_lk)
            {
                Viec cur;
                if (acc == null || !_viec.TryGetValue(acc, out cur) || cur.Id != v.Id) return false;
                _viec.Remove(acc);
                _viecDaLay.Remove(v.Id);
                return true;
            }
        }

        private static string MoTaDong(List<DongGiao> ds)
        {
            var p = new List<string>();
            foreach (var d in ds) p.Add(d.Khoa + " x" + d.SoLuong);
            return string.Join(", ", p.ToArray());
        }

        // ---------------- log ----------------

        private void Ghi(string acc, string nhom, string noiDung)
        {
            NhatKy.App(acc, nhom, noiDung);
            var h = OnLog;
            if (h != null)
            {
                try { h("[" + (acc ?? "-") + "] [" + nhom + "] " + noiDung); } catch { }
            }
        }

        private void GhiHanChe(string khoa, int giay, string acc, string nhom, string noiDung)
        {
            if (HanChe(khoa, giay)) Ghi(acc, nhom, noiDung);
        }

        /// <summary>true = duoc lam (lan dau, hoac da qua <paramref name="giay"/> tu lan truoc cung khoa).</summary>
        private bool HanChe(string khoa, int giay)
        {
            DateTime luc;
            var now = DateTime.UtcNow;
            lock (_logHanChe)
            {
                if (_logHanChe.TryGetValue(khoa, out luc) && (now - luc).TotalSeconds < giay) return false;
                _logHanChe[khoa] = now;
            }
            return true;
        }

        // =====================================================================
        // SU KIEN
        // =====================================================================

        private void XuLySuKien(SuKien sk, DateTime now)
        {
            switch (sk.Loai)
            {
                case LoaiSuKien.Chat: XuLyChat(sk.C, sk.Nguoi, sk.Tin, now); break;
                case LoaiSuKien.Phien: XuLyPhien(sk.Acc, sk.Phien, sk.Viec, now); break;
                case LoaiSuKien.Viec: XuLyBaoViec(sk.Bao, now); break;
            }
        }

        // ---------------- phien giao dich ----------------

        private void XuLyPhien(string acc, PhienGiaoDich p, Viec v, DateTime now)
        {
            var kq = p.KetQua;
            if (kq == null) return;
            bool laBot = LaTenBot(kq.DoiPhuong);
            bool laChu = !laBot && Cfg.LaChuKho(kq.DoiPhuong);
            string loai;
            if (kq.Vai == VaiGiaoDich.Nhan)
            {
                var vn = ViecCua(acc);
                if (laBot) loai = vn != null && vn.MucDich == GOM ? "NHAN_GOM" : "NHAN_DON";
                else loai = "NAP";
            }
            else if (v == null) loai = "GIAO";
            else loai = v.MucDich == RUT ? "RUT" : v.MucDich == DON_XU ? "DON_XU" : v.MucDich == TRA ? "TRA"
                      : v.MucDich == GOM ? "GOM" : v.MucDich == XA ? "XA" : "DON";
            string soLenh = v != null ? string.Join(" ", v.CacLenh.Select(x => "#" + x).ToArray()) : "";

            NhatKy.Csv(NhatKy.GIAO_DICH, kq.BatDau, kq.KetThuc, loai, acc, kq.DoiPhuong, kq.DoiPhuongId, laChu ? "1" : "0",
                kq.MoTaMonDua(), kq.MoTaMonNhan(), kq.XuDua, kq.XuNhan, kq.ThanhCong ? "XONG" : "HUY",
                kq.ThanhCong ? "" : ((kq.MaLoi ?? "") + " " + kq.LyDo).Trim(), string.Join(" / ", kq.TinServer), soLenh, "");
            Ghi(acc, "GD", loai + " voi " + (kq.DoiPhuong ?? ("id " + kq.DoiPhuongId)) + ": "
                + (kq.ThanhCong ? "XONG" : "HUY [" + kq.MaLoi + "] " + kq.LyDo)
                + (kq.MonNhan.Length > 0 || kq.XuNhan > 0 ? " | nhan " + kq.MoTaMonNhan() + (kq.XuNhan > 0 ? " + " + kq.XuNhan + " xu" : "") : "")
                + (kq.ThanhCong && (kq.MonDua.Count > 0 || kq.XuDua > 0) ? " | dua " + kq.MoTaMonDua() + (kq.XuDua > 0 ? " + " + kq.XuDua + " xu" : "") : ""));
            DeDocPhien(acc, loai, kq, v);

            if (kq.Vai == VaiGiaoDich.Nhan && !laBot)
            {
                if (kq.ThanhCong)
                {
                    int soMon = 0;
                    foreach (var m in kq.MonNhan) soMon += Math.Max(1, m.Quantity);
                    lock (_lk)
                    {
                        _tk.NapLuot++;
                        _tk.NapMon += soMon;
                        _tk.NapXu += kq.XuNhan;
                        if (_giuCuaCho != null && ChuVan.CungTen(_giuCuaCho, kq.DoiPhuong)) _giuCuaCho = null;
                    }
                    _soLieuDoiLuc = now;
                    if (soMon > 0 || kq.XuNhan > 0)
                        BaoSuKien(laChu ? kq.DoiPhuong : null, null, "Da nhan " + MoTaNhan(kq) + " tu " + kq.DoiPhuong, true, null, 0);
                    // D86: Chu kho nap vao Leader -> xa nhanh (Leader chuyen tiep sang clone dung canh ngay).
                    if (laChu && soMon > 0 && LaLeader(acc)) MoXa(kq.DoiPhuong, now, XA_NAP);
                }
                else if (laChu && (kq.MaLoi == MaLoiGiaoDich.TU_CHOI_HANG || kq.MaLoi == MaLoiGiaoDich.DOI_PHUONG_DAT_QUA))
                {
                    BaoRieng(kq.DoiPhuong, "Khong nhan: " + kq.LyDo, null, 0);
                }
                return;
            }

            if (kq.Vai == VaiGiaoDich.Giao && v != null && kq.ThanhCong)
            {
                int soMon = 0;
                foreach (var kv in kq.MonDua) soMon += Math.Max(1, (int)kv.Value.Quantity);
                if (v.MucDich == RUT) GhiGiaoRut(acc, v, kq, now);
                else if (v.MucDich == DON || v.MucDich == XA)
                {
                    lock (_lk) { _tk.DonLuot++; _tk.DonMon += soMon; }
                    _soLieuDoiLuc = now;
                    if (v.MucDich == XA) SauChuyenXa(v, soMon);
                }
            }
        }

        private static string MoTaNhan(KetQuaGiaoDich kq)
        {
            string mon;
            int tong = 0;
            foreach (var m in kq.MonNhan) tong += Math.Max(1, m.Quantity);
            if (kq.MonNhan.Length == 1)
                mon = tong + " x " + BangMon.Ten(kq.MonNhan[0].TemplateId) + (kq.MonNhan[0].Upgrade > 0 ? " +" + kq.MonNhan[0].Upgrade : "");
            else if (kq.MonNhan.Length > 1)
                mon = tong + " mon (" + kq.MonNhan.Length + " o)";
            else mon = "";
            if (kq.XuNhan > 0) mon = (mon.Length > 0 ? mon + " + " : "") + ChuVan.SoDep(kq.XuNhan) + " xu";
            return mon;
        }

        // ---------------- log de doc (D83) ----------------

        /// <summary>Mot dong DE DOC: "[NHAN] noi dung" - sukien.log + khung log che do "De doc".</summary>
        private void DeDoc(string nhan, string noiDung)
        {
            string dong = "[" + nhan + "] " + noiDung;
            NhatKy.SuKien(dong);
            var h = OnSuKien;
            if (h != null)
            {
                try { h(dong); } catch { }
            }
        }

        /// <summary>Ai giao gi cho ai, sau mot phien giao dich. Phien NHAN cua bot bo qua (phia giao da ghi).</summary>
        private void DeDocPhien(string acc, string loai, KetQuaGiaoDich kq, Viec v)
        {
            string toi = TenNhanVat(acc);
            string dp = kq.DoiPhuong ?? ("id " + kq.DoiPhuongId);
            string nhan, tu, den;
            switch (loai)
            {
                case "NAP": nhan = "NẠP"; tu = dp; den = "Leader " + toi; break;
                case "XA": nhan = "CHUYỂN"; tu = "Leader " + toi; den = dp; break;
                case "RUT": nhan = "GIAO"; tu = toi; den = dp; break;
                case "GIAO": nhan = "GIAO"; tu = toi; den = dp; break;
                case "DON": nhan = "DỌN KHO"; tu = "Leader " + toi; den = dp; break;
                case "DON_XU": nhan = "DỒN XU"; tu = "Leader " + toi; den = dp; break;
                case "TRA": nhan = "TRẢ BỚT"; tu = toi; den = "Leader " + dp; break;
                case "GOM": nhan = "GOM"; tu = toi; den = dp; break;
                default: return;
            }
            bool vao = kq.Vai == VaiGiaoDich.Nhan;
            var ds = new List<MonDoc>();
            if (vao) foreach (var m in kq.MonNhan) ds.Add(new MonDoc(m.TemplateId, m.Upgrade, m.Quantity));
            else foreach (var kv in kq.MonDua) ds.Add(new MonDoc(kv.Value.TemplateId, kv.Value.Upgrade, kv.Value.Quantity));
            string mon = MonDeDoc(ds, vao ? kq.XuNhan : kq.XuDua);
            string lenh = v != null && v.CacLenh.Count > 0
                ? " (lệnh " + string.Join(", ", v.CacLenh.Select(x => "#" + x).ToArray()) + ")" : "";
            if (kq.ThanhCong)
            {
                if (mon.Length > 0) DeDoc(nhan, tu + " → " + den + ": " + mon + lenh);
                return;
            }
            // Hong: chi dong co y nghia voi nguoi dung - nguoi la moi roi thoi (on ao), qua xa (bot tu lai gan).
            if (kq.MaLoi == MaLoiGiaoDich.QUA_XA) return;
            if (loai == "NAP" && !Cfg.LaChuKho(dp)) return;
            // D87: Chu kho moi lien tuc luc Leader het cho -> moi lan mot phien huy; chi ghi mot dong moi phut.
            if (kq.MaLoi == MaLoiGiaoDich.HET_CHO && !HanChe("dedoc-hetcho|" + dp, 60)) return;
            DeDoc(nhan, tu + " → " + den + " không thành" + lenh + ": " + LyDoDeDoc(kq.MaLoi, kq.LyDo));
        }

        public struct MonDoc
        {
            public short Tpl;
            public byte Cap;
            public int SoLuong;
            public MonDoc(short tpl, byte cap, int sl) { Tpl = tpl; Cap = cap; SoLuong = Math.Max(1, sl); }
        }

        /// <summary>D83: "17 Tử tinh thạch cao cấp, 2 Áo choàng +5 + 1.000 xu" - gop cung loai, toi da 6 loai.</summary>
        public static string MonDeDoc(IEnumerable<MonDoc> ds, long xu)
        {
            var gop = new List<MonDoc>();
            foreach (var m in ds)
            {
                int i = gop.FindIndex(x => x.Tpl == m.Tpl && x.Cap == m.Cap);
                if (i < 0) gop.Add(m);
                else gop[i] = new MonDoc(m.Tpl, m.Cap, gop[i].SoLuong + m.SoLuong);
            }
            var p = new List<string>();
            foreach (var m in gop.Take(6))
                p.Add(m.SoLuong + " " + BangMon.Ten(m.Tpl) + (m.Cap > 0 ? " +" + m.Cap : ""));
            if (gop.Count > 6) p.Add("và " + (gop.Count - 6) + " loại khác");
            string s = string.Join(", ", p.ToArray());
            if (xu > 0) s = (s.Length > 0 ? s + " + " : "") + ChuVan.SoDep(xu) + " xu";
            return s;
        }

        private static string MonDeDoc(IEnumerable<DongGiao> ds)
        {
            return MonDeDoc(ds.Select(d => new MonDoc(d.Khoa.Tpl, d.Khoa.Up, d.SoLuong)), 0);
        }

        /// <summary>Ma loi -> cau cho nguoi dung thuong.</summary>
        public static string LyDoDeDoc(string maLoi, string lyDo)
        {
            switch (maLoi ?? "")
            {
                case MaLoiGiaoDich.HET_GIO_CHO_NHAN_LOI: return "không nhận lời mời giao dịch";
                case MaLoiGiaoDich.HET_GIO: return "quá giờ chờ";
                case MaLoiGiaoDich.SAI_DOI_PHUONG: return "đang giao dịch với người khác";
                case MaLoiGiaoDich.THIEU_O: return "người nhận không đủ ô trống";
                case MaLoiGiaoDich.DOI_PHUONG_DAT_QUA: return "bên kia đặt nhiều món hơn chỗ trống";
                case MaLoiGiaoDich.TU_CHOI_HANG: return "không nhận hàng này (" + lyDo + ")";
                case MaLoiGiaoDich.BI_CHEN: return "bị chen ngang";
                case MaLoiGiaoDich.KHONG_CO_MON: return "không còn món để giao";
                case MaLoiGiaoDich.MAT_KET_NOI: return "mất kết nối";
                case MaLoiGiaoDich.QUA_XA: return "đứng quá xa";
                case MaLoiGiaoDich.HET_CHO: return "Leader hết chỗ, mời lại sau vài giây";
                case MaLoiGiaoDich.BI_HUY: return "bị huỷ" + (string.IsNullOrEmpty(lyDo) ? "" : " (" + lyDo + ")");
                case MaLoiViec.KHONG_THAY_NGUOI: return "không thấy người nhận";
                case MaLoiViec.TUI_DAY: return "túi và rương đầy";
                case MaLoiViec.HET_HAN: return "quá giờ";
            }
            return string.IsNullOrEmpty(lyDo) ? (string.IsNullOrEmpty(maLoi) ? "lỗi" : maLoi) : lyDo;
        }

        /// <summary>Chia so mon vua giao cho cac lenh da gop, theo phan giu tren acc giao.</summary>
        private void GhiGiaoRut(string acc, Viec v, KetQuaGiaoDich kq, DateTime now)
        {
            var cac = LenhCuaViec(v, false);
            int tong = 0;
            foreach (var kv in kq.MonDua)
            {
                var k = KhoaMon.Tu(kv.Value);
                int sl = Math.Max(1, (int)kv.Value.Quantity);
                tong += sl;
                foreach (var l in cac)
                {
                    if (sl <= 0) break;
                    int giu = 0;
                    foreach (var p in ChepKeHoach(l))
                        if (p.Khoa.Equals(k) && CungAcc(p.Acc, acc)) giu += p.ConLai;
                    int an = Math.Min(sl, giu);
                    if (an <= 0) continue;
                    Hang.GhiDaGiao(l, acc, k, an);
                    sl -= an;
                }
                if (sl > 0) Ghi(acc, "Lenh", "Giao thua " + sl + " x " + k + " - khong khop cho giu nao");
            }
            foreach (var l in cac)
            {
                l.HongLienTiep = 0;
                l.ChoCoMatDen = now.AddMinutes(Math.Max(1, Cfg.ChoCoMatPhut));
            }
            lock (_lk) _tk.XuatMon += tong;
        }

        private List<LenhRut> LenhCuaViec(Viec v, bool chiDangMo)
        {
            var r = new List<LenhRut>();
            foreach (int so in v.CacLenh)
            {
                var l = Hang.Tim(so);
                if (l != null && (!chiDangMo || l.DangMo)) r.Add(l);
            }
            return r;
        }

        private List<PhanGiao> ChepKeHoach(LenhRut l)
        {
            lock (Hang.Khoa) return new List<PhanGiao>(l.KeHoach);
        }

        // ---------------- bao cao viec ----------------

        private void XuLyBaoViec(BaoCaoViec bc, DateTime now)
        {
            var v = bc.Viec;
            if (!GoViec(v.Acc, v)) return;
            string kq = bc.Xong ? "XONG" : ("LOI " + bc.MaLoi + (string.IsNullOrEmpty(bc.LyDo) ? "" : " - " + bc.LyDo));
            Ghi(v.Acc, "Viec", ChuVan.BoDau(v.MoTa) + ": " + kq);

            // --- luot gom (D81) ---
            var g = _gom;
            if (g != null && (g.ViecClone == v || g.ViecNhan == v)) SauViecGom(g, v, bc, now);

            // --- luot chuyen tiep cua xa nhanh (D86) ---
            var xn = _xa;
            if (xn != null && xn.ViecLeader == v) SauViecChuyen(xn, v, bc, now);

            // --- luot don kho / don xu ---
            var d = _don;
            if (d != null && d.MucDich == TRA && (d.ViecClone == v || d.ViecLeader == v))
            {
                SauViecTra(d, v, bc, now);
            }
            else if (d != null && (d.ViecClone == v || d.ViecLeader == v))
            {
                if (d.ViecLeader == v)
                {
                    d.ViecLeader = null;
                    bool tiepLuot = bc.Xong && d.MucDich == DON && !d.ViecClone.BiHuy && d.SoLuot < 20;
                    if (tiepLuot)
                    {
                        // Clone van dung o khu chinh: lap luot sau o nhip toi (so kho moi cap nhat xong).
                        d.Dong.Clear();
                        d.ViecClone.HetHan = now.AddMinutes(6);
                        d.TaoLuc = now;
                    }
                    else
                    {
                        d.ViecClone.Huy(bc.Xong ? "don xong" : "Leader giao hong");
                        if (!bc.Xong)
                        {
                            if (bc.MaLoi == MaLoiGiaoDich.THIEU_O || bc.MaLoi == MaLoiGiaoDich.TU_CHOI_HANG)
                                _nghiNhanDon[d.Clone] = now.AddMinutes(10);
                            _donNghiDen = now.AddSeconds(30);
                        }
                        _don = null;
                    }
                }
                else
                {
                    if (d.ViecLeader != null)
                    {
                        d.ViecLeader.Huy("clone roi viec doi nhan");
                        HuyPhienCua(d.ViecLeader.Acc, d.ViecLeader, "clone roi viec doi nhan");
                    }
                    else if (!bc.Xong && !v.BiHuy) _nghiNhanDon[d.Clone] = now.AddMinutes(2);
                    _don = null;
                }
            }

            if (!LaLeader(v.Acc) && (v.Loai == LoaiViec.DoiNhan || v.Loai == LoaiViec.GiaoMon)) _canCat.Add(v.Acc);

            switch (v.Loai)
            {
                case LoaiViec.DocRuong:
                    if (bc.Xong)
                    {
                        string truoc;
                        if (_truocNha.TryGetValue(v.Acc, out truoc))
                        {
                            _truocNha.Remove(v.Acc);
                            Ghi(v.Acc, "NHA_CLONE", "Doc lai " + v.Acc + " sau khi nhan lai: truoc " + truoc + " | sau " + MoTaTui(So.Lay(v.Acc)));
                        }
                    }
                    else _nghiBaoTri[v.Acc] = now.AddMinutes(bc.MaLoi == MaLoiViec.KHONG_THAY_NPC ? 30 : 5);
                    break;

                case LoaiViec.CatRuong:
                    // Thanh cong van nghi 2 phut: ruong day thi cat khong het, dung lap lai moi nhip.
                    _nghiBaoTri[v.Acc] = now.AddMinutes(bc.Xong ? 2 : (bc.MaLoi == MaLoiViec.KHONG_THAY_NPC ? 30 : 5));
                    break;

                case LoaiViec.GiaoMon:
                    if (v.MucDich == RUT) SauViecRut(v, bc, now);
                    break;
            }
        }

        private void SauViecRut(Viec v, BaoCaoViec bc, DateTime now)
        {
            var cac = LenhCuaViec(v, true);
            foreach (var l in cac)
                if (CungAcc(l.CloneDangGiao, v.Acc)) l.CloneDangGiao = null;
            Hang.DanhDauDoi();
            if (bc.Xong || cac.Count == 0) return;

            var c = LayClient(v.Acc);
            bool doBot = v.BiHuy || c == null || c.State != ClientState.InGame
                         || (bc.LyDo ?? "").StartsWith("mode dung", StringComparison.Ordinal);
            if (bc.MaLoi == MaLoiViec.BI_HUY && doBot) return;   // huy / rot mang - khong phai loi cua nguoi nhan

            string lyDo = string.IsNullOrEmpty(bc.LyDo) ? bc.MaLoi : bc.LyDo;
            switch (bc.MaLoi)
            {
                case MaLoiGiaoDich.THIEU_O:
                    foreach (var l in cac)
                        TamDung(l, "nguoi nhan thieu o hanh trang",
                            l.NguoiNhan + " thieu o hanh trang (can toi da 12 o). Don tui roi nhan: tiep #" + l.So, now);
                    return;

                case MaLoiViec.KHONG_VAO_KHU:
                    // Khu chinh tam day: dem nhu mot lan hong (lan 2 moi tam dung) - khong dung ca hang cho ngay.
                    if (!cac.Exists(l => KhuGiao(l) != Cfg.KhuChinh)) goto default;
                    // Khu giao rieng day / khong ton tai (M12) - acc khac cung khong vao duoc -> tam dung, bao ro.
                    foreach (var l in cac)
                        TamDung(l, "khong vao duoc khu " + KhuGiao(l), "Lenh #" + l.So + ": clone khong vao duoc khu " + KhuGiao(l)
                            + " (khu day?). Thu lai: tiep #" + l.So + ", bo: huy #" + l.So, now);
                    return;

                case MaLoiViec.KHONG_THAY_NGUOI:
                    {
                        // Khu giao rieng khong ai nhin thay (D79): nguoi nhan chua toi -> lan sau thu lai, khong tinh hong.
                        // KHONG dat lai DaBaoCho: moi 60 giay mot cau "cho X toi khu N" la spam (review 17/09).
                        bool rieng = false;
                        foreach (var l in cac)
                        {
                            if (KhuGiao(l) == Cfg.KhuChinh) continue;
                            rieng = true;
                            l.ThuTimSau = now.AddSeconds(THU_TIM_LAI_GIAY);
                        }
                        if (rieng)
                        {
                            Ghi(v.Acc, "Lenh", "Khong thay " + v.NguoiNhan + " o khu " + v.Khu + " - thu lai sau " + THU_TIM_LAI_GIAY + " giay");
                            return;
                        }
                        goto default;
                    }

                case MaLoiViec.TUI_DAY:
                    {
                        // Clone ket cung (D78): so kho da thay tui + ruong day -> ChonAccGiao cho, TaoTraBot go ket, roi
                        // giao lai. KHONG tranh acc (tranh = nha giu cho = lenh tam dung "chi con 0/N" - dung loi user
                        // gap 17/09). Lap lai qua 3 lan trong 10 phut (go ket khong an thua) thi xu nhu loi phia acc.
                        int lan;
                        DateTime tu;
                        if (!_tuiDayTu.TryGetValue(v.Acc, out tu) || (now - tu).TotalMinutes > 10) { _tuiDayTu[v.Acc] = now; lan = 0; }
                        else _tuiDayDem.TryGetValue(v.Acc, out lan);
                        _tuiDayDem[v.Acc] = ++lan;
                        string khongGo = "khong co so lieu acc";
                        var tk = So.Lay(v.Acc);
                        if (tk == null || !CoTheTraBot(tk, out khongGo))
                        {
                            // Khong go ket tu dong duoc (Leader vang / tui Leader day / vua hong...) -> cho cung vo ich.
                            lyDo += " - khong tu go ket duoc: " + khongGo;
                        }
                        else if (lan < 3)
                        {
                            Ghi(v.Acc, "Lenh", "Acc " + v.Acc + " tui + ruong day (" + lyDo + ") - cho go ket roi giao lai (lan " + lan + ")");
                            return;
                        }
                        _tuiDayDem.Remove(v.Acc);
                        _tuiDayTu.Remove(v.Acc);
                        goto case MaLoiViec.KHONG_CO_MON;
                    }

                case MaLoiViec.KHONG_CO_MON:
                case MaLoiViec.TACH_CHONG_HONG:
                case MaLoiViec.RUONG_KHONG_DAP:
                case MaLoiViec.KHONG_THAY_NPC:
                case MaLoiViec.KHONG_TOI_DUOC:
                case MaLoiViec.HET_HAN:
                    // Loi phia acc giao: tranh acc nay 10 phut cho lenh do, lap lai voi acc khac.
                    foreach (var l in cac)
                    {
                        _tranh[l.So + "|" + v.Acc] = now.AddMinutes(10);
                        l.HongLienTiep++;
                        if (l.HongLienTiep >= 3)
                            TamDung(l, lyDo, "Lenh #" + l.So + " tam dung: " + lyDo + ". Lam lai: tiep #" + l.So, now);
                        else
                            LapLai(l, now, "acc " + v.Acc + " hong: " + lyDo);
                    }
                    return;

                default:
                    foreach (var l in cac)
                    {
                        l.HongLienTiep++;
                        l.DaBaoCho = false;
                        if (l.HongLienTiep >= 2)
                            TamDung(l, lyDo, "Lenh #" + l.So + " tam dung (" + lyDo + "). Lam lai: tiep #" + l.So, now);
                    }
                    return;
            }
        }

        // ---------------- chat rieng (lenh Chu kho) ----------------

        private void XuLyChat(NsoClient bot, string tu, string tin, DateTime now)
        {
            if (bot == null || string.IsNullOrEmpty(tu)) return;
            if (LaTenBot(tu)) return;
            if (!Cfg.LaChuKho(tu)) return;   // D24: nguoi la bo qua, chat.log da ghi
            var lc = LenhChat.Doc(tin);
            if (lc.Loai == LoaiLenh.Khong) return;
            NhatKy.App(bot.Config.Username, "Lenh", "Chu kho " + tu + ": " + ChuVan.BoTem(tin));
            var gui = bot.State == ClientState.InGame ? bot : BotGui();
            if (gui == null) return;
            if (!Cfg.BatLenhChat) { TraLoi(gui, tu, "Lenh chat dang tat tren tool"); return; }

            switch (lc.Loai)
            {
                case LoaiLenh.Loi:
                    TraLoi(gui, tu, lc.Loi);
                    break;
                case LoaiLenh.Kho:
                    TraLoi(gui, tu, TomTatKho());
                    break;
                case LoaiLenh.Nap:
                    LenhNap(gui, tu, now);
                    break;
                case LoaiLenh.Tim:
                    TraLoi(gui, tu, TimMon(lc.TuKhoa));
                    break;
                case LoaiLenh.Co:
                    TraLoi(gui, tu, CoMon(lc.Tpl));
                    break;
                case LoaiLenh.Goi:
                    {
                        var ds = Cfg.TenGoi;
                        TraLoi(gui, tu, ds.Count == 0 ? "Chua co goi rut nao" : "Goi: " + string.Join(", ", ds.ToArray()));
                        break;
                    }
                case LoaiLenh.Xa:
                    MoXa(tu, now, XA_LENH);
                    break;
                case LoaiLenh.XaXong:
                    // Leader chuyen not phan con trong tui roi moi dong (XuLyXa).
                    if (_xa != null)
                    {
                        if (_xa.XongLuc == DateTime.MinValue) _xa.XongLuc = now;
                        _xa.ChuKho = tu;
                    }
                    else TraLoi(gui, tu, "Khong co dot xa nao dang chay");
                    break;
                case LoaiLenh.Lay:
                    TaoLenh("chat", tu, lc.ChoTen ?? tu, null,
                        new List<DongLenh> { new DongLenh { Tpl = lc.Tpl, Cap = lc.Cap, SoXin = lc.SoLuong } }, now, lc.Khu);
                    break;
                case LoaiLenh.LayGoi:
                    {
                        var g = Cfg.LayGoi(lc.TenGoi);
                        if (g == null)
                        {
                            var ds = Cfg.TenGoi;
                            TraLoi(gui, tu, "Khong co goi " + lc.TenGoi + (ds.Count > 0 ? ". Goi: " + string.Join(", ", ds.ToArray()) : ""));
                            break;
                        }
                        TaoLenh("chat", tu, lc.ChoTen ?? tu, lc.TenGoi, DongTuGoi(g), now, lc.Khu);
                        break;
                    }
                case LoaiLenh.Tiep:
                case LoaiLenh.Huy:
                    {
                        bool tiep = lc.Loai == LoaiLenh.Tiep;
                        var l = TimLenhCua(tu, lc.SoLenh, tiep);
                        if (l == null) { TraLoi(gui, tu, lc.SoLenh > 0 ? "Khong co lenh #" + lc.SoLenh : "Ban khong co lenh nao dang " + (tiep ? "tam dung" : "mo")); break; }
                        if (!l.DangMo) { TraLoi(gui, tu, "Lenh #" + l.So + " da ket thuc"); break; }
                        if (!ChuVan.CungTen(l.ChuKho, tu)) { TraLoi(gui, tu, "Lenh #" + l.So + " khong phai cua ban"); break; }
                        if (tiep) TraLoi(gui, tu, TiepLenh(l, now));
                        else HuyLenh(l, "chu kho " + tu + " huy", now);
                        break;
                    }
                case LoaiLenh.Theo:
                    Cfg.DatTheoDoi(lc.Tpl, lc.Nguong, tu);
                    LuuCfg();
                    lock (_theoDoiTruoc) _theoDoiTruoc.Remove(lc.Tpl + "|" + tu.ToLowerInvariant());
                    TraLoi(gui, tu, "Theo doi " + lc.Tpl + " " + BangMon.Ten(lc.Tpl) + ": "
                        + (lc.Nguong >= 0 ? "bao khi duoi " + lc.Nguong : "bao moi khi ve kho"));
                    break;
                case LoaiLenh.BoTheo:
                    {
                        bool co = Cfg.BoTheoDoi(lc.Tpl, tu);
                        if (co) LuuCfg();
                        TraLoi(gui, tu, co ? "Da bo theo doi " + lc.Tpl : "Ban chua theo doi " + lc.Tpl);
                        break;
                    }
            }
        }

        private LenhRut TimLenhCua(string chu, int so, bool chiTamDung)
        {
            if (so > 0) return Hang.Tim(so);
            LenhRut r = null;
            foreach (var l in Hang.DangMo)
                if (ChuVan.CungTen(l.ChuKho, chu) && (!chiTamDung || l.TrangThai == TrangThaiLenh.TamDung)) r = l;
            return r;
        }

        private string TomTatKho()
        {
            var sc = _sucChua;
            int on, tong;
            DemClone(out on, out tong);
            return string.Format("Kho: {0}/{1} clone online, {2}{3}/{4} o, {5} lenh cho, xu {6}, Leader {7}",
                on, tong, sc.ChuaDu ? "~" : "", sc.Dung, sc.Tong, Hang.SoCho, XuGon(TongXu), TenLeader);
        }

        private string TimMon(string tuKhoa)
        {
            var ds = BangMon.TimTheoTen(tuKhoa, 300);
            if (ds.Count == 0) return "Khong co mon nao ten khop '" + tuKhoa + "'";
            var ton = new Dictionary<short, int>();
            foreach (var d in _bangTon)
            {
                if (d.Khoa.Khoa) continue;
                int n;
                ton.TryGetValue(d.Khoa.Tpl, out n);
                ton[d.Khoa.Tpl] = n + d.KhaDung;
            }
            var co = ds.Where(t => ton.ContainsKey(t.Id) && ton[t.Id] > 0)
                       .OrderByDescending(t => ton[t.Id]).Take(5).ToList();
            if (co.Count == 0)
                return "Kho khong co. Mon khop: " + string.Join(", ", ds.Take(3).Select(t => t.Id + " " + t.Name).ToArray());
            return string.Join(" | ", co.Select(t => t.Id + " " + t.Name + " x" + ton[t.Id]).ToArray());
        }

        private string CoMon(short tpl)
        {
            int tong = 0, giu = 0;
            var theoCap = new SortedDictionary<int, int>();
            var accs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool coNha = false;
            foreach (var d in _bangTon)
            {
                if (d.Khoa.Tpl != tpl || d.Khoa.Khoa) continue;
                tong += d.KhaDung;
                giu += d.Giu;
                if (d.TrenAccNha) coNha = true;
                int n;
                theoCap.TryGetValue(d.Khoa.Up, out n);
                theoCap[d.Khoa.Up] = n + d.KhaDung;
                foreach (var a in d.PhanBo.Keys) accs.Add(a);
            }
            string ten = tpl + " " + BangMon.Ten(tpl);
            if (tong == 0 && giu == 0) return ten + ": kho khong co" + (coNha ? " (co tren clone dang nha)" : "");
            string s = ten + ": " + tong + " (" + accs.Count + " acc)";
            if (giu > 0) s += ", dang giu " + giu;
            if (theoCap.Count > 1 || (theoCap.Count == 1 && theoCap.Keys.First() > 0))
                s += ". " + string.Join(", ", theoCap.Select(kv => "+" + kv.Key + " x" + kv.Value).ToArray());
            return s;
        }

        private void LenhNap(NsoClient gui, string tu, DateTime now)
        {
            var L = LeaderClient;
            if (L == null) { TraLoi(gui, tu, "Leader dang offline, chua nhan do duoc"); return; }
            if (!Cfg.BatNap) { TraLoi(gui, tu, "Kho dang tat nhan do"); return; }

            var m = L.ActiveModeAs<KhoMode>();
            var p = m != null ? m.PhienHienTai : null;
            if (p != null && !p.KetThuc)
            {
                string dp = p.TenDoiPhuong ?? p.TenMongDoi;
                var vv = m.ViecHienTai;
                bool phienRut = vv != null && vv.MucDich == RUT && p.Vai == VaiGiaoDich.Giao;
                // D86: luot chuyen tiep sang clone chi 2-3 giay - huy thi phan vua nap ket lai tren Leader.
                bool phienXa = vv != null && vv.MucDich == XA && p.Vai == VaiGiaoDich.Giao;
                if (dp != null && ChuVan.CungTen(dp, tu)) { TraLoi(gui, tu, "Dang giao dich voi ban roi"); return; }
                if (!phienXa && dp != null && Cfg.LaChuKho(dp)) { TraLoi(gui, tu, "Leader dang giao dich voi chu kho khac, doi chut roi nhan lai: nap"); return; }
                if (phienRut) { TraLoi(gui, tu, "Leader dang giao lenh rut cho " + dp + ", doi chut roi nhan lai: nap"); return; }
                if (phienXa)
                {
                    // Giu cua ngay (khong chuyen luot moi), de luot dang chay xong.
                    lock (_lk)
                    {
                        _giuCuaCho = tu;
                        _giuCuaDen = now.AddSeconds(Math.Max(10, Cfg.GiuCuaGiay));
                    }
                    TraLoi(gui, tu, "Leader dang chuyen do sang clone, moi giao dich sau ~3 giay");
                    return;
                }
                m.YeuCauHuyPhien("chu kho " + tu + " nap");
                Ghi(L.Config.Username, "Nap", "Huy phien voi " + (dp ?? "?") + " de nhuong cho chu kho " + tu);
            }
            HuyDon("chu kho " + tu + " nap", now);
            lock (_lk)
            {
                _giuCuaCho = tu;
                _giuCuaDen = now.AddSeconds(Math.Max(10, Cfg.GiuCuaGiay));
            }
            Ghi(L.Config.Username, "Nap", "Giu cua " + Cfg.GiuCuaGiay + "s cho chu kho " + tu);
            TraLoi(gui, tu, "San sang nhan " + DemTuiTrong(L) + " mon. Moi giao dich " + L.DisplayCharName + " ngay");
        }

        // ---------------- tin nhan ----------------

        private NsoClient BotGui()
        {
            var L = LeaderClient;
            if (L != null) return L;
            foreach (var c in _clientTheoAcc.Values)
                if (c.State == ClientState.InGame && !Cfg.DaNha(c.Config.Username)) return c;
            return null;
        }

        public static List<string> ChiaTin(string s, int toiDa)
        {
            var r = new List<string>();
            s = (s ?? "").Trim();
            while (s.Length > toiDa)
            {
                int cat = s.LastIndexOf(' ', toiDa);
                // Khong tach "tiep #12" thanh hai tin: chu sau cho cat bat dau bang '#' thi lui them mot tu.
                if (cat > 0 && cat + 1 < s.Length && s[cat + 1] == '#')
                {
                    int truoc = s.LastIndexOf(' ', cat - 1);
                    if (truoc >= toiDa / 2) cat = truoc;
                }
                if (cat < toiDa / 2) cat = toiDa;
                r.Add(s.Substring(0, cat).Trim());
                s = s.Substring(cat).Trim();
            }
            if (s.Length > 0 || r.Count == 0) r.Add(s);
            return r;
        }

        private void GuiDai(NsoClient bot, string den, string tin, string khoa, int giay)
        {
            var ds = ChiaTin(tin, KenhChat.DO_DAI_TOI_DA - 10);
            if (!Kenh.GuiRieng(bot, den, ds[0], khoa, giay)) return;
            for (int i = 1; i < ds.Count; i++) Kenh.GuiRieng(bot, den, ds[i]);
        }

        private void TraLoi(NsoClient bot, string den, string tin)
        {
            if (bot == null || string.IsNullOrEmpty(tin)) return;
            GuiDai(bot, den, tin, null, 0);
        }

        /// <summary>Tin rieng cho mot nguoi — chi Chu kho (hoac moi nguoi khi bat ChatVoiNguoiLa, D24).</summary>
        private void BaoRieng(string ten, string tin, string khoa, int giay)
        {
            if (string.IsNullOrEmpty(ten) || LaTenBot(ten)) return;
            if (!Cfg.LaChuKho(ten) && !Cfg.ChatVoiNguoiLa) return;
            var bot = BotGui();
            if (bot != null) GuiDai(bot, ten, tin, khoa, giay);
        }

        /// <summary>Su kien (D20): chat rieng Chu kho lien quan + nguoi nhan (neu duoc phep) + chat cong dong.</summary>
        private void BaoSuKien(string chuKho, string nguoiNhan, string tin, bool congDong, string khoa, int giay)
        {
            Ghi("-", "SuKien", tin);
            if (!string.IsNullOrEmpty(chuKho)) BaoRieng(chuKho, tin, khoa, giay);
            if (!string.IsNullOrEmpty(nguoiNhan) && !ChuVan.CungTen(nguoiNhan, chuKho)) BaoRieng(nguoiNhan, tin, khoa, giay);
            if (congDong && Cfg.BaoCongDong) Kenh.GuiCongDong(ChiaTin(tin, KenhChat.DO_DAI_TOI_DA - 10)[0], khoa, giay);
        }

        private void BaoChuKho(string tin, bool congDong, string khoa, int giay)
        {
            Ghi("-", "SuKien", tin);
            foreach (var ck in Cfg.ChuKho) BaoRieng(ck, tin, khoa, giay);
            if (congDong && Cfg.BaoCongDong) Kenh.GuiCongDong(ChiaTin(tin, KenhChat.DO_DAI_TOI_DA - 10)[0], khoa, giay);
        }

        private void XaChat(DateTime now)
        {
            var L = LeaderClient;
            bool oKhu = L != null && OKhuChinh(L);
            string rao = oKhu ? NoiDungRao() : null;
            bool ngoai = oKhu && TimTrongKhu(L, x => !LaTenBot(x.Name)) != null;
            bool doi = (now - _soLieuDoiLuc).TotalSeconds < 60;
            Kenh.Xa(L, oKhu, Cfg, rao, ngoai, doi, DateTime.UtcNow);
        }

        public string NoiDungRao()
        {
            var sc = _sucChua;
            int on, tong;
            DemClone(out on, out tong);
            string s = (Cfg.RaoMau ?? "")
                .Replace("{dung}", (sc.ChuaDu ? "~" : "") + sc.Dung)
                .Replace("{tong}", sc.Tong.ToString(CultureInfo.InvariantCulture))
                .Replace("{trong}", sc.Trong.ToString(CultureInfo.InvariantCulture))
                .Replace("{online}", on.ToString(CultureInfo.InvariantCulture))
                .Replace("{tongacc}", tong.ToString(CultureInfo.InvariantCulture))
                .Replace("{lenh}", Hang.SoCho.ToString(CultureInfo.InvariantCulture))
                .Replace("{leader}", TenLeader);
            return s.Trim().Length == 0 ? null : s;
        }

        public static string XuGon(long xu)
        {
            if (xu >= 1000000000L) return (xu / 1e9).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " ty";
            if (xu >= 1000000L) return (xu / 1e6).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " tr";
            return ChuVan.SoDep(xu);
        }

        // =====================================================================
        // SO KHO / LEADER / BANG TONG
        // =====================================================================

        /// <summary>
        /// Cai dat thieu ma kho van "chay" thi nhin ngoai nhu treo (user test 16/09: khu phu -1 -> ca kho
        /// dung im, log khong co dong nao). Nhac trong log, moi 10 phut mot lan khi co acc online.
        /// </summary>
        private void CanhBaoCaiDat()
        {
            if (_clientTheoAcc.Count == 0) return;
            if (Cfg.KhuChinh < 0)
                GhiHanChe("thieukhuchinh", 600, "-", "CaiDat",
                    "CHUA CAI KHU CHINH (Cai dat > Kho): Leader khong ve cho, kho KHONG nhan / giao / don duoc");
            else if (Cfg.KhuPhu < 0)
                GhiHanChe("thieukhuphu", 3600, "-", "CaiDat",
                    "Chua cai khu phu: clone dung o khu dang dung luc vao map kho (khong gom ve mot khu)");
            else if (Cfg.KhuPhu == Cfg.KhuChinh)
                GhiHanChe("khutrung", 3600, "-", "CaiDat",
                    "Khu phu trung khu chinh: clone dung chung khu voi Leader, khu chinh se dong");
            if (string.IsNullOrEmpty(Cfg.Leader))
                GhiHanChe("thieuleader", 600, "-", "CaiDat", "CHUA CHON LEADER (tab Acc > chuot phai > Dat lam Leader)");
        }

        private void DatLaiThongKe(DateTime now)
        {
            lock (_lk)
                if (_tk.Ngay != now.Date) _tk = new ThongKeNgay { Ngay = now.Date };
        }

        private void CapNhatSo(DateTime now)
        {
            var cs = _clientTheoAcc;
            foreach (var acc in _accTheoTen.Keys)
            {
                NsoClient c;
                bool vao = cs.TryGetValue(acc, out c) && c.State == ClientState.InGame
                           && c.GameState.MyChar != null && !string.IsNullOrEmpty(c.GameState.MyChar.Name)
                           && c.GameState.MyChar.BagItems != null;
                if (vao && !Cfg.DaNha(acc))
                {
                    So.CapNhat(c);
                    _offlineTu.Remove(acc);
                    try { BangMon.NapTuStore(c.GameState.ItemStore); } catch { }
                }
                else
                {
                    So.DanhDauOffline(acc);
                    if (!_offlineTu.ContainsKey(acc)) _offlineTu[acc] = now;
                }
            }
        }

        /// <summary>
        /// Leader thuc thu: Leader chinh online thi la no; vang qua 60 giay va du phong online thi du
        /// phong len thay (SPEC §4). Chinh ve lai: doi 10 giay + du phong ranh roi moi tra vai.
        /// </summary>
        private void BauLeader(DateTime now)
        {
            string chinh = (Cfg.Leader ?? "").Trim();
            string phong = (Cfg.LeaderDuPhong ?? "").Trim();
            string cu = _leaderAcc ?? "";
            string moi;
            if (chinh.Length == 0 || !_accTheoTen.ContainsKey(chinh)) moi = "";
            else if (DangOnline(chinh))
            {
                _chinhVangTu = DateTime.MinValue;
                moi = cu;
                if (!CungAcc(cu, chinh))
                {
                    if (_chinhVeTu == DateTime.MinValue) _chinhVeTu = now;
                    var cb = LayClient(cu);
                    bool phongBan = cb != null && (cb.DangGiaoDich || CoViec(cu));
                    if (cu.Length == 0 || !DangOnline(cu) || (!phongBan && (now - _chinhVeTu).TotalSeconds >= 10)) moi = chinh;
                }
            }
            else
            {
                _chinhVeTu = DateTime.MinValue;
                if (_chinhVangTu == DateTime.MinValue) _chinhVangTu = now;
                bool phongDung = phong.Length > 0 && !CungAcc(phong, chinh) && _accTheoTen.ContainsKey(phong)
                                 && DangOnline(phong) && !Cfg.DaNha(phong) && !LaAccKhacMayChu(phong);
                if (phongDung && (CungAcc(cu, phong) || (now - _chinhVangTu).TotalSeconds >= 60)) moi = phong;
                else moi = chinh;
            }
            if (CungAcc(moi, cu) || (moi.Length == 0 && cu.Length == 0)) return;
            _leaderAcc = moi;
            if (cu.Length > 0 && moi.Length > 0)
            {
                Ghi(moi, "Leader", "Doi Leader: " + cu + " -> " + moi);
                BaoChuKho("Leader moi: " + TenNhanVat(moi) + " (" + TenMap() + " khu " + Cfg.KhuChinh + ")", true, null, 0);
                DeDoc("LEADER", "Đổi Leader: " + TenNhanVat(cu) + " → " + TenNhanVat(moi));
            }
            else Ghi(moi.Length == 0 ? "-" : moi, "Leader", "Leader: " + (moi.Length == 0 ? "(chua cai / khong co trong danh sach)" : moi));
        }

        private string _dauBangTon;

        private static string DauVanTay(List<TonMon> bang)
        {
            var sb = new System.Text.StringBuilder(bang.Count * 64);
            foreach (var d in bang)
            {
                sb.Append(d.Khoa).Append(':').Append(d.Ten).Append(':').Append(d.Tong).Append(',').Append(d.Giu).Append(',')
                  .Append(d.SoO).Append(',').Append(d.Nhom).Append(d.LaRac ? 'r' : '-').Append(d.TheoDoi ? 't' : '-')
                  .Append(d.TrenAccNha ? 'n' : '-');
                foreach (var kv in d.PhanBo)
                    sb.Append('|').Append(kv.Key).Append('=').Append(kv.Value[0]).Append('/').Append(kv.Value[1]);
                sb.Append(';');
            }
            return sb.ToString();
        }

        private bool TinhVaoSucChua(string acc)
        {
            return LaCloneKho(acc) && KeHang.KeCuaAcc(acc, Cfg) != KeHang.RAC;
        }

        private void TinhBang(DateTime now)
        {
            var accs = _accTheoTen;
            var bang = So.Gop(a => accs.ContainsKey(a) && !LaAccKhacMayChu(a), Cfg.DaNha, Cfg);
            Hang.ApGiuCho(bang);
            var td = Cfg.TheoDoi;
            foreach (var d in bang)
                foreach (var m in td)
                    if (m.Tpl == d.Khoa.Tpl) { d.TheoDoi = true; break; }
            // Chi phat hanh bang MOI khi noi dung doi: giao dien so tham chieu de biet co can ve lai khong
            // (user 16/09: "giao dien qua giat" - truoc day moi giay mot bang moi -> ve lai ca luoi moi giay).
            string dau = DauVanTay(bang);
            if (dau != _dauBangTon)
            {
                _dauBangTon = dau;
                _bangTon = bang;
            }

            var sc = So.TinhSucChua(TinhVaoSucChua);
            _sucChua = sc;
            _sucChuaRac = So.TinhSucChua(a => LaCloneKho(a) && KeHang.KeCuaAcc(a, Cfg) == KeHang.RAC);

            long xu = 0;
            foreach (var t in So.TatCa())
                if (accs.ContainsKey(t.Acc) && !LaAccKhacMayChu(t.Acc)) xu += t.Xu;
            Interlocked.Exchange(ref _tongXu, xu);

            if (_dungTruoc != sc.Dung)
            {
                if (_dungTruoc >= 0) _soLieuDoiLuc = now;
                _dungTruoc = sc.Dung;
            }

            // KHO DAY (SPEC §6 buoc 5): clone het cho VA tui Leader duoi nguong nhan. Moi clone luon chua
            // O_CHUA_TUI o (D78) -> "het cho" = chi con dung phan chua do.
            var lt = So.Lay(_leaderAcc);
            int soClone = So.TatCa().Count(x => TinhVaoSucChua(x.Acc));
            bool day = sc.Tong > 0 && !sc.ChuaDu && sc.Trong <= soClone * O_CHUA_TUI
                       && lt != null && lt.Online && lt.TuiTrong < Cfg.NguongNhan;
            if (day != _khoDay)
            {
                _khoDay = day;
                if (day) BaoChuKho("KHO DAY - tam ngung nhan do", true, "khoday", 600);
                else Ghi("-", "Kho", "Kho co cho trong tro lai");
                DeDoc("KHO", day ? "KHO ĐẦY — tạm ngừng nhận đồ (thêm clone hoặc dọn bớt)" : "Kho có chỗ trống trở lại");
            }
        }

        /// <summary>Viec cho lay ma acc offline / da huy / qua han -> go; viec da lay ma qua han lau -> go (mode khong bao).</summary>
        private void DonViecTreo(DateTime now)
        {
            var bo = new List<Viec>();
            lock (_lk)
            {
                foreach (var kv in _viec)
                {
                    var v = kv.Value;
                    bool daLay = _viecDaLay.Contains(v.Id);
                    DateTime tu;
                    bool offLau = _offlineTu.TryGetValue(kv.Key, out tu) && (now - tu).TotalSeconds > 20;
                    bool quaHan = v.HetHan != DateTime.MaxValue && now > v.HetHan;
                    if (!daLay && (offLau || v.BiHuy || quaHan)) bo.Add(v);
                    else if (daLay && v.HetHan != DateTime.MaxValue && now > v.HetHan.AddSeconds(90)) bo.Add(v);
                    else if (daLay && offLau && (now - tu).TotalSeconds > 120) bo.Add(v);
                }
            }
            foreach (var v in bo)
            {
                if (!v.BiHuy) v.Huy("viec treo / acc offline");
                XuLyBaoViec(new BaoCaoViec { Viec = v, MaLoi = MaLoiViec.BI_HUY, LyDo = "viec treo / acc offline" }, now);
            }
        }

        private void DonTranh(DateTime now)
        {
            foreach (var k in _tranh.Where(kv => now > kv.Value).Select(kv => kv.Key).ToList()) _tranh.Remove(k);
            foreach (var k in _nghiBaoTri.Where(kv => now > kv.Value).Select(kv => kv.Key).ToList()) _nghiBaoTri.Remove(k);
            foreach (var k in _nghiNhanDon.Where(kv => now > kv.Value).Select(kv => kv.Key).ToList()) _nghiNhanDon.Remove(k);
        }

        private string TenMap()
        {
            try { return UI.MapNames.NameOf(Cfg.Map); } catch { return "map " + Cfg.Map; }
        }

        private static string MoTaTui(TuiAcc t)
        {
            if (t == null) return "chua co so lieu";
            int mon = 0;
            foreach (var m in t.Mon) mon += m.SoLuong;
            return string.Format("{0} mon / {1} o (tui {2}/{3}, ruong {4}/{5}), xu {6}", mon, t.Mon.Count, t.TuiDung, t.SoOTui,
                t.RuongDung, t.SoORuong < 0 ? "?" : t.SoORuong.ToString(CultureInfo.InvariantCulture), t.Xu);
        }

        // =====================================================================
        // NHA CLONE
        // =====================================================================

        private void XuLyNha(DateTime now)
        {
            if (_choNha.Count == 0) return;
            foreach (var kv in _choNha.ToList())
            {
                string u = kv.Key;
                var c = LayClient(u);
                if (c != null && (c.DangGiaoDich || CoViec(u)))
                {
                    var v = ViecCua(u);
                    // Viec noi bo huy ngay duoc; viec giao lenh rut thi cho xong.
                    if (v != null && v.MucDich != RUT && !v.BiHuy)
                    {
                        if (_don != null && CungAcc(_don.Clone, u)) HuyDon("nha clone " + u, now);
                        else v.Huy("nha clone");
                    }
                    continue;
                }
                _choNha.Remove(u);
                var t = So.Lay(u);
                try { _fleet.StopAccount(kv.Value); }
                catch (Exception ex) { Ghi(u, "NHA_CLONE", "Dung client loi (van danh dau nha): " + ex.Message); }
                Cfg.DatNha(u, true);
                LuuCfg();
                foreach (var l in Hang.DangMo)
                {
                    if (!ChepKeHoach(l).Exists(p => CungAcc(p.Acc, u) && p.ConLai > 0)) continue;
                    Hang.NhaGiuTrenAcc(l, u);
                    if (l.CloneDangGiao == null && l.TrangThai != TrangThaiLenh.TamDung) LapLai(l, now, "nha clone " + u);
                }
                Ghi(u, "NHA_CLONE", "Da nha " + u + ": " + MoTaTui(t));
                BaoChuKho("Da nha " + TenNhanVat(u) + " de don tay", true, null, 0);
                DeDoc("NHẢ CLONE", "Đã nhả " + TenNhanVat(u) + " để dọn tay (" + MoTaTui(t) + ")");
            }
        }

        // =====================================================================
        // LENH RUT (SPEC §7)
        // =====================================================================

        private static List<DongLenh> DongTuGoi(List<DongGoi> g)
        {
            var r = new List<DongLenh>();
            foreach (var d in g) r.Add(new DongLenh { Tpl = d.Tpl, SoXin = d.SoLuong });
            return r;
        }

        private LenhRut TaoLenh(string nguon, string chuKho, string nguoiNhan, string tenGoi, List<DongLenh> dong, DateTime now, int khu = -1)
        {
            if (khu > LenhChat.KHU_TOI_DA) khu = -1;
            if (khu == Cfg.KhuChinh) khu = -1;   // chon dung khu chinh = mac dinh (doi khu chinh sau van theo)
            nguoiNhan = (nguoiNhan ?? "").Trim();
            if (!Cfg.BatRut)
            {
                BaoRieng(chuKho, "Kho dang tat rut do", null, 0);
                Ghi("-", "Lenh", "Bo lenh rut (" + nguon + "): dang tat rut do");
                return null;
            }
            if (nguoiNhan.Length == 0 || dong == null || dong.Count == 0)
            {
                Ghi("-", "Lenh", "Bo lenh rut (" + nguon + "): thieu nguoi nhan hoac mon");
                return null;
            }
            if (LaTenBot(nguoiNhan))
            {
                BaoRieng(chuKho, "Khong giao cho bot cua kho (" + nguoiNhan + ")", null, 0);
                Ghi("-", "Lenh", "Bo lenh rut: nguoi nhan " + nguoiNhan + " la bot cua kho");
                return null;
            }
            var l = new LenhRut
            {
                Nguon = nguon, ChuKho = chuKho, NguoiNhan = nguoiNhan, TenGoi = tenGoi, Dong = dong, TaoLuc = now,
                ChoCoMatDen = now.AddMinutes(Math.Max(1, Cfg.ChoCoMatPhut)), Khu = khu,
            };
            Hang.Them(l);
            if (dong.Exists(d => d.Cap >= 0 && KhoaMon.LaThuCuoi(d.Tpl)))
            {
                // D80: cap thu cuoi khong doc duoc o ruong / giao dich -> "+cap" bi bo qua; noi ro de Chu kho khong bat ngo.
                BaoRieng(chuKho, "Luu y: kho khong phan biet cap thu cuoi, lenh #" + l.So + " lay bat ky cap", null, 0);
                DeDoc("LỆNH #" + l.So, "Lưu ý: kho không phân biệt cấp thú cưỡi — lấy bất kỳ cấp");
            }
            string thieu = Hang.LapKeHoach(l, So, a => DuocGiao(l, a));
            string mon = l.MoTaMon();
            int khuGiao = KhuGiao(l);
            Ghi("-", "Lenh", string.Format("Lenh #{0} ({1}{2}): {3} cho {4} o khu {5}{6}{7}", l.So, nguon, chuKho != null ? " " + chuKho : "",
                mon, nguoiNhan, khuGiao, khu >= 0 ? " (chon rieng)" : "", thieu != null ? " | thieu: " + thieu : ""));
            bool choLeader = thieu != null && ChoLeaderDonCho(l);
            DeDoc("LỆNH #" + l.So, (nguon == "chat" ? (chuKho ?? "?") + " nhắn" : "Tool") + ": rút " + mon + " cho " + nguoiNhan
                + (khu >= 0 ? " ở khu " + khuGiao : "") + (thieu != null && !choLeader ? " — thiếu: " + thieu : ""));

            if (choLeader)
            {
                l.ChoLeaderDon = true;
                Ghi("-", "Lenh", "Lenh #" + l.So + ": phan thieu dang tren Leader - cho don sang clone roi giao o khu " + khuGiao);
                DeDoc("LỆNH #" + l.So, "Hàng đang ở Leader (Leader không rời khu chính) — chờ chuyển sang clone rồi giao ở khu " + khuGiao);
                BaoSuKien(chuKho, nguoiNhan, "Lenh #" + l.So + ": " + mon + " cho " + nguoiNhan + ". Hang dang o Leader, cho chuyen sang clone (~1 phut). Toi "
                    + TenMap() + " khu " + khuGiao + " de nhan", true, null, 0);
                l.DaBaoCho = true;
                return l;
            }
            if (thieu == null)
            {
                bool coMat = TimNguoi(nguoiNhan, khuGiao) != null;
                if (!coMat) l.DaBaoCho = true;
                BaoSuKien(chuKho, nguoiNhan, "Lenh #" + l.So + ": " + mon + " cho " + nguoiNhan
                    + (coMat ? ". Dang chuan bi" : ". Toi " + TenMap() + " khu " + khuGiao + " de nhan"), true, null, 0);
            }
            else if (!CoConLai(l))
            {
                Hang.KetThuc(l, TrangThaiLenh.Huy, "khong co hang: " + thieu);
                GhiCsvLenh(l, "HUY");
                DeDoc("LỆNH #" + l.So, "Huỷ: kho không có hàng");
                BaoSuKien(chuKho, null, "Lenh #" + l.So + " huy: " + thieu, false, null, 0);
            }
            else
            {
                TamDung(l, "thieu hang: " + thieu, "Lenh #" + l.So + ": " + mon + " cho " + nguoiNhan + ". Thieu: " + thieu
                    + ". Giao phan dang co: tiep #" + l.So + ", bo: huy #" + l.So, now);
            }
            return l;
        }

        /// <summary>
        /// Lenh khu rieng (D79) thieu hang ma Leader dang giu dung loai do: Leader khong roi khu chinh nen phai don sang
        /// clone truoc -> cho (khong huy / tam dung). Tat don kho thi khong cho duoc (user 17/09 kiem ky: truoc day bao
        /// "kho khong co hang" trong khi hang nam ngay tren Leader).
        /// </summary>
        private bool ChoLeaderDonCho(LenhRut l)
        {
            if (!Cfg.BatDonKho || KhuGiao(l) == Cfg.KhuChinh || string.IsNullOrEmpty(_leaderAcc)) return false;
            var lt = So.Lay(_leaderAcc);
            if (lt == null || !lt.Online) return false;
            foreach (var d in l.Dong)
            {
                if (d.SoXin >= 0 && d.DaGiao >= d.SoXin) continue;
                short tpl = d.Tpl;
                if (lt.Mon.Exists(m => !m.Khoa.Khoa && m.Khoa.Tpl == tpl)) return true;
            }
            return false;
        }

        /// <summary>Loai hang cac lenh dang cho Leader don (don truoc, va don ca khi Chu kho dang trong khu).</summary>
        private HashSet<short> LoaiCanDonGap()
        {
            var r = new HashSet<short>();
            foreach (var l in Hang.DangMo)
                if (l.ChoLeaderDon && l.TrangThai != TrangThaiLenh.TamDung)
                    foreach (var d in l.Dong) r.Add(d.Tpl);
            return r;
        }

        /// <summary>Acc nay co duoc dung de giao cho lenh <paramref name="l"/> khong (R2).</summary>
        private bool DuocGiao(LenhRut l, string acc)
        {
            if (string.IsNullOrEmpty(acc) || Cfg.DaNha(acc) || LaAccKhacMayChu(acc)) return false;
            if (!_accTheoTen.ContainsKey(acc)) return false;
            // Vua mo kho: acc chua kip dang nhap (mo so le 2 giay/acc) - dem "offline" tu luc mo kho, khong
            // tu lan cuoi thay trong so kho (test song 16/09: tat kho 5 phut, mo lai la nha het giu cho,
            // tam dung ca 3 lenh truoc khi acc nao vao game).
            bool vuaMo = (DateTime.Now - _batDauLuc).TotalMinutes < 5;
            if (LayClient(acc) == null && !vuaMo) return false;
            var t = So.Lay(acc);
            if (t == null) return false;
            var moc = t.ThayLuc > _batDauLuc ? t.ThayLuc : _batDauLuc;
            if (!t.Online && (DateTime.Now - moc).TotalMinutes >= 5) return false;
            if (l != null)
            {
                DateTime den;
                if (_tranh.TryGetValue(l.So + "|" + acc, out den) && DateTime.Now < den) return false;
                // Lenh giao o khu rieng (D79): Leader khong roi khu chinh (mat cua nap + mat "mat" tim nguoi nhan).
                // Hang tren Leader se duoc don sang clone roi lap lai.
                if (KhuGiao(l) != Cfg.KhuChinh && CungAcc(acc, _leaderAcc)) return false;
            }
            return true;
        }

        private bool CoConLai(LenhRut l)
        {
            foreach (var p in ChepKeHoach(l)) if (p.ConLai > 0) return true;
            return false;
        }

        private static bool DongDu(LenhRut l)
        {
            if (l.Dong.Count == 0) return false;
            foreach (var d in l.Dong)
                if (d.SoXin < 0 || d.DaGiao < d.SoXin) return false;
            return true;
        }

        private void TamDung(LenhRut l, string lyDo, string tin, DateTime now)
        {
            l.TrangThai = TrangThaiLenh.TamDung;
            l.LyDo = lyDo;
            l.CloneDangGiao = null;
            l.ChoCoMatDen = now.AddMinutes(30);
            Hang.DanhDauDoi();
            Ghi("-", "Lenh", "Tam dung lenh #" + l.So + ": " + lyDo);
            DeDoc("LỆNH #" + l.So, "Tạm dừng: " + lyDo + " (chạy tiếp ở tab Hàng chờ hoặc nhắn: tiep #" + l.So + ")");
            BaoSuKien(l.ChuKho, l.NguoiNhan, tin, true, null, 0);
        }

        private void LapLai(LenhRut l, DateTime now, string viSao)
        {
            string thieu = Hang.LapKeHoach(l, So, a => DuocGiao(l, a));
            if (thieu == null || (l.ChapNhanThieu && CoConLai(l)))
            {
                if (l.ChoLeaderDon && thieu == null) Ghi("-", "Lenh", "Lenh #" + l.So + ": hang da sang clone, giao tiep");
                l.ChoLeaderDon = false;
                Ghi("-", "Lenh", "Lap lai ke hoach lenh #" + l.So + " (" + viSao + ")" + (thieu != null ? " - van thieu: " + thieu : ""));
                return;
            }
            // Phan thieu dang tren Leader (lenh khu rieng): cho Leader don sang clone - khong tam dung.
            if (ChoLeaderDonCho(l))
            {
                if (!l.ChoLeaderDon) Ghi("-", "Lenh", "Lenh #" + l.So + ": phan thieu dang tren Leader - cho don sang clone");
                l.ChoLeaderDon = true;
                return;
            }
            l.ChoLeaderDon = false;
            if (DongDu(l)) return;
            // Ly do lap lai (vd. "acc X hong: tui + ruong day ...") vao cot Ly do - "thieu hang" tran thi nguoi doc khong
            // biet vi sao kho con ma lenh bao thieu (user 17/09).
            TamDung(l, "thieu hang: " + thieu + (string.IsNullOrEmpty(viSao) ? "" : " | " + viSao), "Lenh #" + l.So + " thieu: " + thieu
                + ". Giao phan dang co: tiep #" + l.So + ", bo: huy #" + l.So, now);
        }

        private string TiepLenh(LenhRut l, DateTime now)
        {
            if (l.TrangThai != TrangThaiLenh.TamDung) return "Lenh #" + l.So + " dang chay, khong can tiep";
            l.HongLienTiep = 0;
            l.ChapNhanThieu = true;
            l.DaBaoCho = false;
            l.TrangThai = TrangThaiLenh.ChoCoMat;
            l.LyDo = "";
            l.ChoCoMatDen = now.AddMinutes(Math.Max(1, Cfg.ChoCoMatPhut));
            foreach (var k in _tranh.Keys.Where(k => k.StartsWith(l.So + "|", StringComparison.Ordinal)).ToList()) _tranh.Remove(k);
            Hang.LapKeHoach(l, So, a => DuocGiao(l, a));
            if (!CoConLai(l))
            {
                if (l.TongDaGiao > 0)
                {
                    XongLenh(l);
                    return "Lenh #" + l.So + ": het hang, ket thuc voi " + l.TongDaGiao + " mon da giao";
                }
                l.TrangThai = TrangThaiLenh.TamDung;
                l.LyDo = "van khong co hang";
                l.ChoCoMatDen = now.AddMinutes(30);
                Hang.DanhDauDoi();
                return "Lenh #" + l.So + ": kho van khong co hang. Bo lenh: huy #" + l.So;
            }
            Hang.DanhDauDoi();
            return "Tiep lenh #" + l.So + ": " + l.MoTaMon() + " cho " + l.NguoiNhan;
        }

        private void XongLenh(LenhRut l)
        {
            int xin = l.TongXin;
            int da = l.TongDaGiao;
            Hang.KetThuc(l, TrangThaiLenh.Xong, da < xin ? "giao duoc " + da + "/" + xin : "");
            lock (_lk) _tk.XuatLenh++;
            GhiCsvLenh(l, "XONG");
            DeDoc("LỆNH #" + l.So, "Xong: đã giao " + (da < xin ? da + "/" + xin : da.ToString()) + " món cho " + l.NguoiNhan);
            BaoSuKien(l.ChuKho, l.NguoiNhan, "Xong lenh #" + l.So + ": da giao " + (da < xin ? da + "/" + xin : da.ToString())
                + " mon (" + l.MoTaMon() + ") cho " + l.NguoiNhan, true, null, 0);
        }

        private void HuyLenh(LenhRut l, string lyDo, DateTime now)
        {
            if (!l.DangMo) return;
            if (l.CloneDangGiao != null)
            {
                var v = ViecCua(l.CloneDangGiao);
                if (v != null && v.CacLenh.Contains(l.So))
                {
                    v.Huy("lenh #" + l.So + " bi huy");
                    HuyPhienCua(v.Acc, v, "lenh #" + l.So + " bi huy");
                }
            }
            Hang.KetThuc(l, TrangThaiLenh.Huy, lyDo);
            GhiCsvLenh(l, "HUY");
            DeDoc("LỆNH #" + l.So, "Huỷ: " + lyDo + (l.TongDaGiao > 0 ? " (đã giao " + l.TongDaGiao + " món)" : ""));
            BaoSuKien(l.ChuKho, l.NguoiNhan, "Da huy lenh #" + l.So + " (" + lyDo + ")"
                + (l.TongDaGiao > 0 ? ", da giao " + l.TongDaGiao + " mon" : ""), true, null, 0);
        }

        /// <summary>Huy phien giao dich dang chay cua acc NEU phien do thuoc viec <paramref name="v"/>.</summary>
        private void HuyPhienCua(string acc, Viec v, string lyDo)
        {
            var c = LayClient(acc);
            var m = c != null ? c.ActiveModeAs<KhoMode>() : null;
            var p = m != null ? m.PhienHienTai : null;
            if (p == null || m.ViecHienTai != v) return;
            if (v.Loai == LoaiViec.DoiNhan)
            {
                // Viec doi nhan KHONG so huu phien: phien dang chay co the la cua nguoi choi dang nap -> chi huy khi
                // doi phuong dung la bot cua luot (review 17/09).
                var bot = LayClient(v.TuBotAcc);
                var mcBot = bot != null ? bot.GameState.MyChar : null;
                if (mcBot == null || p.DoiPhuongId != mcBot.CharId) return;
            }
            m.YeuCauHuyPhien(lyDo);
        }

        private void GhiCsvLenh(LenhRut l, string ketQua)
        {
            NhatKy.Csv(NhatKy.LENH, l.So, l.TaoLuc, l.KetThucLuc == default(DateTime) ? DateTime.Now : l.KetThucLuc,
                l.Nguon, l.ChuKho ?? "", l.NguoiNhan, l.MoTaMon(), l.TongXin, l.TongDaGiao,
                string.Join(" ", l.CacClone.ToArray()), ketQua, l.LyDo);
        }

        private void XuLyLenh(DateTime now)
        {
            var ds = Hang.DangMo;
            if (ds.Count == 0) return;

            // ---- 1. don dep tung lenh ----
            foreach (var l in ds)
            {
                if (!l.DangMo) continue;
                if (l.TrangThai == TrangThaiLenh.TamDung)
                {
                    if (now > l.ChoCoMatDen) HuyLenh(l, "tam dung qua 30 phut", now);
                    continue;
                }
                if (l.CloneDangGiao != null)
                {
                    var v = ViecCua(l.CloneDangGiao);
                    if (v != null && v.CacLenh.Contains(l.So)) continue;
                    l.CloneDangGiao = null;   // viec da mat ma bao cao chua toi - khong de lenh ket
                }
                // Da giao du phan DA LAP ma lenh con cho hang tren Leader (D79) -> chua xong: lap lai ben duoi (review 17/09:
                // truoc day dong "XONG 5/10" ngay khi clone giao xong phan co san).
                if (DongDu(l) || (Hang.DaGiaoDu(l) && !l.ChoLeaderDon)) { XongLenh(l); continue; }

                bool lap = !CoConLai(l);
                foreach (var p in ChepKeHoach(l))
                {
                    if (p.ConLai <= 0) continue;
                    if (!DuocGiao(l, p.Acc))
                    {
                        Hang.NhaGiuTrenAcc(l, p.Acc);
                        Ghi(p.Acc, "Lenh", "Lenh #" + l.So + ": nha giu cho tren " + p.Acc + " (offline qua 5 phut / nha / tranh)");
                        lap = true;
                    }
                    else if (LechGiuCho(l, p)) lap = true;
                }
                if (lap)
                {
                    LapLai(l, now, "acc giu hang thay doi");
                    if (!l.DangMo || l.TrangThai == TrangThaiLenh.TamDung) continue;
                }
                if (now > l.ChoCoMatDen)
                    HuyLenh(l, "qua " + Cfg.ChoCoMatPhut + " phut khong giao duoc cho " + l.NguoiNhan, now);
            }

            if (!Cfg.BatRut) return;

            // ---- 2. giao: moi luc MOT viec rut (SPEC §7: khu chinh toi da mot clone dang giao) ----
            ds = Hang.DangMo;
            foreach (var l in ds) if (l.CloneDangGiao != null) return;
            // Luot 1: lenh co nguoi nhan dang duoc bot nhin thay. Luot 2 (chi khi luot 1 khong giao duoc gi): lenh o
            // khu rieng khong bot nao dung (D79) - cu clone sang tim. Lam nguoc lai thi mot lenh "khu mu" chan ca hang
            // ~100 giay moi chuyen trong khi nguoi nhan lenh khac dang dung cho (review 17/09).
            for (int luot = 1; luot <= 2; luot++)
            {
                foreach (var l in ds)
                {
                    if (l.TrangThai == TrangThaiLenh.TamDung || !CoConLai(l)) continue;
                    int khu = KhuGiao(l);
                    bool thay = TimNguoi(l.NguoiNhan, khu) != null;
                    if (thay && luot == 2) continue;   // da xet o luot 1
                    if (!thay)
                    {
                        if (luot == 1)
                        {
                            if (!l.DaBaoCho)
                            {
                                l.DaBaoCho = true;
                                BaoSuKien(l.ChuKho, l.NguoiNhan, "Lenh #" + l.So + ": cho " + l.NguoiNhan + " toi " + TenMap()
                                    + " khu " + khu + " de nhan", true, null, 0);
                            }
                            continue;
                        }
                        // Khu chinh: Leader luon nhin thay -> cho. Khu rieng co bot dung -> cung nhin thay -> cho.
                        if (khu == Cfg.KhuChinh || CoBotOKhu(khu) || now < l.ThuTimSau) continue;
                    }
                    var cac = ds.Where(o => o.TrangThai != TrangThaiLenh.TamDung && o.CloneDangGiao == null
                                            && ChuVan.CungTen(o.NguoiNhan, l.NguoiNhan) && KhuGiao(o) == khu).ToList();
                    // Lenh moi cung nguoi / cung khu khong duoc "vuot" moc thu lai cua lenh cu trong nhom.
                    if (!thay && cac.Exists(o => now < o.ThuTimSau)) continue;
                    // Chuyen di tim o khu mu la phong: khong huy viec noi bo de nhuong.
                    string acc = ChonAccGiao(cac, now, thay);
                    if (acc == null) continue;
                    GiaoLenhChoAcc(cac, l, acc, now);
                    return;
                }
            }
        }

        /// <summary>
        /// GIU_CHO_LECH (SPEC §7): acc thuc te con it hon tong dang giu 3 nhip lien -> nha giu cua lenh nay
        /// tren acc do de lap lai. Bo qua acc dang giao (so lieu dang doi giua chung).
        /// </summary>
        private bool LechGiuCho(LenhRut l, PhanGiao p)
        {
            var t = So.Lay(p.Acc);
            if (t == null || !t.Online) return false;
            var v = ViecCua(p.Acc);
            if (v != null && v.MucDich == RUT) return false;
            int co = t.SoLuong(p.Khoa);
            int giu = Hang.GiuTrenAcc(p.Khoa, p.Acc, null);
            string k = p.Acc + "|" + p.Khoa;
            if (co >= giu) { _lechDem.Remove(k); return false; }
            int n;
            _lechDem.TryGetValue(k, out n);
            _lechDem[k] = ++n;
            if (n < 3) return false;
            _lechDem.Remove(k);
            Ghi(p.Acc, "GIU_CHO_LECH", "Lenh #" + l.So + ": " + p.Khoa + " dang giu " + giu + " nhung acc chi con " + co + " -> lap lai");
            Hang.NhaGiuTrenAcc(l, p.Acc);
            return true;
        }

        /// <summary>Acc giu nhieu nhat va dang ranh. Acc tot nhat dang ban viec noi bo -> huy viec do, nhip sau giao.</summary>
        private string ChonAccGiao(List<LenhRut> cac, DateTime now, bool duocNhuong = true)
        {
            var theoAcc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in cac)
                foreach (var p in ChepKeHoach(o))
                {
                    if (p.ConLai <= 0) continue;
                    int n;
                    theoAcc.TryGetValue(p.Acc, out n);
                    theoAcc[p.Acc] = n + p.ConLai;
                }
            string banNhat = null;
            foreach (var kv in theoAcc.OrderByDescending(x => x.Value))
            {
                var c = LayClient(kv.Key);
                if (c == null || c.State != ClientState.InGame) continue;
                if (ChoGoKet(kv.Key, cac))
                {
                    GhiHanChe("chogoket|" + kv.Key, 60, kv.Key, "Lenh", "Cho go ket " + kv.Key
                        + " (tui + ruong day, hang can giao nam trong ruong) roi moi giao lenh "
                        + string.Join(", ", cac.Select(o => "#" + o.So).ToArray()));
                    continue;
                }
                if (!c.DangGiaoDich && !CoViec(kv.Key)) return kv.Key;
                if (banNhat == null) banNhat = kv.Key;
            }
            if (banNhat != null && duocNhuong) NhuongViec(banNhat, now);
            return null;
        }

        /// <summary>
        /// Acc ket cung (D78) ma KHONG co mon nao cua phan phai giao nam trong tui -> chua giao duoc (khong con
        /// o de lay ruong). Co it nhat mot mon trong tui thi van giao: giao xong tui trong ra, lay ruong tiep.
        /// </summary>
        private bool ChoGoKet(string acc, List<LenhRut> cac)
        {
            var t = So.Lay(acc);
            string viSao;
            if (!KetCung(t) || !CoTheTraBot(t, out viSao)) return false;
            // Mon giao duoc NGAY tu tui (chong khong lon hon so con phai giao, khong qua MAX_SO_LUONG - dung luat ChonO
            // cua mode) -> giao. Chong qua MAX_SO_LUONG trong tui het o thi khong tach duoc -> van phai go ket.
            var con = new Dictionary<KhoaMon, int>();
            foreach (var o in cac)
                foreach (var p in ChepKeHoach(o))
                {
                    if (p.ConLai <= 0 || !CungAcc(p.Acc, acc)) continue;
                    int n;
                    con.TryGetValue(p.Khoa, out n);
                    con[p.Khoa] = n + p.ConLai;
                }
            foreach (var m in t.Mon)
            {
                int c;
                int sl = Math.Max(1, m.SoLuong);
                if (!m.TrongRuong && con.TryGetValue(m.Khoa, out c) && sl <= c && sl <= Controller.TradeHandler.MAX_SO_LUONG) return false;
            }
            return true;
        }

        private void NhuongViec(string acc, DateTime now)
        {
            var v = ViecCua(acc);
            if (v == null || v.BiHuy || v.MucDich == RUT) return;
            // Gom do (D81) chi la don dep - lenh rut len truoc.
            var gg = _gom;
            if (gg != null && (CungAcc(gg.Clone, acc) || CungAcc(gg.Dich, acc)))
            {
                HuyGom("nhuong cho lenh rut", now);
                return;
            }
            // Luot go ket ngan (< 1 phut) va chinh no mo duong cho lenh rut - khong huy (chi acc thuoc luot do).
            var dg = _don;
            if (dg != null && dg.MucDich == TRA
                && (CungAcc(dg.Clone, acc) || (dg.ViecLeader != null && CungAcc(dg.ViecLeader.Acc, acc)))) return;
            if (_don != null && (CungAcc(_don.Clone, acc) || (_don.ViecLeader != null && CungAcc(_don.ViecLeader.Acc, acc))))
                HuyDon("nhuong cho lenh rut", now);
            else if (v.Loai == LoaiViec.CatRuong || v.Loai == LoaiViec.DocRuong)
                v.Huy("nhuong cho lenh rut");
            else if (v.Loai == LoaiViec.DoiNhan && v.MucDich == XA)
            {
                // Clone dung canh Leader (D86): lenh rut len truoc; xa nhanh tu bu clone khac (review 17/09).
                var c = LayClient(acc);
                if (c == null || c.DangGiaoDich) return;
                var xn = _xa;
                if (xn != null && xn.ViecLeader != null && CungAcc(xn.CloneNhan, acc))
                {
                    var L = LeaderClient;
                    if (L != null && L.DangGiaoDich) return;   // co the dang giao cho chinh clone nay - de xong
                    xn.ViecLeader.Huy("clone nhuong lenh rut");
                }
                v.Huy("nhuong cho lenh rut");
            }
            else return;
            GhiHanChe("nhuong|" + acc, 30, acc, "Lenh", "Huy viec " + ChuVan.BoDau(v.MoTa) + " de uu tien lenh rut");
        }

        private void GiaoLenhChoAcc(List<LenhRut> cac, LenhRut l0, string acc, DateTime now)
        {
            int khu = KhuGiao(l0);
            bool khuRieng = khu != Cfg.KhuChinh;
            var v = new Viec
            {
                Loai = LoaiViec.GiaoMon, Acc = acc, NguoiNhan = l0.NguoiNhan, MucDich = RUT, LenhSo = l0.So,
                HetHan = now.AddMinutes(Math.Max(5, Cfg.ChoCoMatPhut)),
                Khu = khuRieng ? khu : -1,
                // Khu rieng chua ai nhin thay nguoi nhan: clone doi o do lau hon (khong phai di di ve ve).
                ChoTimMs = khuRieng && TimNguoi(l0.NguoiNhan, khu) == null ? CHO_TIM_KHU_RIENG_MS : 0,
            };
            var gop = new Dictionary<KhoaMon, int>();
            var dung = new List<LenhRut>();
            foreach (var o in cac)
            {
                int co = 0;
                foreach (var p in ChepKeHoach(o))
                {
                    if (p.ConLai <= 0 || !CungAcc(p.Acc, acc)) continue;
                    int n;
                    gop.TryGetValue(p.Khoa, out n);
                    gop[p.Khoa] = n + p.ConLai;
                    co += p.ConLai;
                }
                if (co > 0) { v.CacLenh.Add(o.So); dung.Add(o); }
            }
            foreach (var kv in gop) v.Dong.Add(new DongGiao { Khoa = kv.Key, SoLuong = kv.Value });
            if (v.Dong.Count == 0 || !GiaoViec(v)) return;
            foreach (var o in dung)
            {
                o.CloneDangGiao = acc;
                o.TrangThai = TrangThaiLenh.DangGiao;
                if (!o.CacClone.Contains(acc)) o.CacClone.Add(acc);
            }
            Hang.DanhDauDoi();
            string ten = TenNhanVat(acc);
            string soLenh = string.Join(", ", dung.Select(o => "#" + o.So).ToArray());
            var daBao = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool congDong = true;
            foreach (var o in dung)
            {
                string key = o.ChuKho ?? "";
                if (!daBao.Add(key)) continue;
                // Chuyen di tim o khu mu lap lai moi ~60 s -> chi bao mot lan moi 10 phut cho moi nhom lenh.
                BaoSuKien(o.ChuKho, daBao.Count == 1 ? l0.NguoiNhan : null,
                    ten + " se moi " + l0.NguoiNhan + " giao lenh " + soLenh + (khuRieng ? " o khu " + khu : ""), congDong,
                    v.ChoTimMs > 0 ? "semoi|" + soLenh + "|" + key : null, v.ChoTimMs > 0 ? 600 : 0);
                congDong = false;
            }
        }

        // =====================================================================
        // DON KHO LEADER (SPEC §6)
        // =====================================================================

        private bool LeaderRanh(DateTime now)
        {
            lock (_lk)
            {
                if ((now - _moiDenLeaderLuc).TotalSeconds < 10) return false;
                if (_giuCuaCho != null && now < _giuCuaDen) return false;
            }
            return true;
        }

        private bool CoChuKhoTrongKhu(NsoClient L)
        {
            return TimTrongKhu(L, x => Cfg.LaChuKho(x.Name)) != null;
        }

        /// <summary>
        /// D84 + D86: lenh dang cho hang ma xa nhanh KHONG chuyen tiep duoc - nam trong RUONG Leader, hoac chong qua
        /// MAX_SO_LUONG trong tui (luot chuyen khong tach - M30).
        /// </summary>
        private bool GapTrongRuongLeader()
        {
            var gap = LoaiCanDonGap();
            if (gap.Count == 0) return false;
            var t = So.Lay(_leaderAcc);
            return t != null && t.Mon.Exists(m => !m.Khoa.Khoa && gap.Contains(m.Khoa.Tpl)
                && (m.TrongRuong || m.SoLuong > Controller.TradeHandler.MAX_SO_LUONG));
        }

        private void HuyDon(string lyDo, DateTime now)
        {
            var d = _don;
            if (d == null) return;
            if (d.ViecClone != null) d.ViecClone.Huy(lyDo);
            if (d.ViecLeader != null)
            {
                d.ViecLeader.Huy(lyDo);
                HuyPhienCua(d.ViecLeader.Acc, d.ViecLeader, lyDo);
            }
            _don = null;
            _donNghiDen = now.AddSeconds(Math.Max(30, Cfg.GiuCuaGiay));
            Ghi("-", "Don", "Huy luot don kho (" + lyDo + ")");
        }

        private string NhomCua(short tpl) { return KeHang.NhomCuaMon(tpl, BangMon.Lay(tpl), Cfg); }

        /// <summary>So o mot chong chiem khi giao dich: chong qua MAX_SO_LUONG phai tach thanh nhieu o (M30).</summary>
        private static int SoOGiao(int soLuong)
        {
            int toiDa = Controller.TradeHandler.MAX_SO_LUONG;
            return Math.Max(1, (soLuong + toiDa - 1) / toiDa);
        }

        private static bool LaChong(short tpl)
        {
            var t = BangMon.Lay(tpl);
            return t != null && t.IsUpToUp;
        }

        private static int MonCoTheCat(TuiAcc t, HashSet<KhoaMon> giu)
        {
            int n = 0;
            foreach (var m in t.Mon)
                if (!m.TrongRuong && !m.Khoa.Khoa && !giu.Contains(m.Khoa)) n++;
            return n;
        }

        /// <summary>Ruong con nhan them: chua biet ruong (viec cat se doc), con o trong, hoac co chong cung loai de gop.</summary>
        private static bool RuongConCho(TuiAcc t, HashSet<KhoaMon> giu)
        {
            if (t.SoORuong < 0 || t.RuongTrong > 0) return true;
            foreach (var m in t.Mon)
            {
                if (m.TrongRuong || m.Khoa.Khoa || giu.Contains(m.Khoa) || !LaChong(m.Khoa.Tpl)) continue;
                foreach (var r in t.Mon)
                    if (r.TrongRuong && r.Khoa.Equals(m.Khoa) && r.SoLuong + m.SoLuong < 30000) return true;
            }
            return false;
        }

        private void XuLyDon(DateTime now)
        {
            var L = LeaderClient;
            var d = _don;
            if (d != null) { TiepDon(d, L, now); return; }

            if (L == null || !OKhuChinh(L) || now < _donNghiDen) return;
            if (L.DangGiaoDich || CoViec(_leaderAcc) || !LeaderRanh(now)) return;
            // Go ket truoc ca luat "Chu kho dang trong khu" (va xa nhanh): lenh rut cua chinh Chu kho do co the dang cho
            // clone ket; do clone tra ve Leader lai duoc chuyen tiep sang clone dung canh.
            if (TaoTraBot(L, now)) return;
            // D86: dang xa nhanh ma tui Leader con do chuyen tiep duoc -> XuLyXa lo (khong di cat ruong giua luc nguoi
            // choi nap, khong goi clone khac). Tui chi con do KHONG chuyen tiep duoc (Rac / khoa / giu cho lenh) -> don
            // nhu thuong; lenh (D84) cho hang trong RUONG Leader -> don phan do (ChuyenTiep chi lay tui) - review 17/09.
            var xa = _xa;
            bool xaChay = xa != null && xa.Clone.Count > 0 && MonChuyenDuoc(L, 1).Count > 0;
            if (xaChay && !GapTrongRuongLeader()) return;
            // Chu kho trong khu: giu Leader ranh cho ho nap - NHUNG tui Leader da duoi nguong nhan thi giu cung vo ich
            // (moi loi moi deu bi tu choi) -> don ngay (user 17/09: "phai doi Leader don kho, nhieu luc con khong don").
            // Lenh khu rieng dang cho hang tren Leader (D79) -> cung don ngay.
            if (CoChuKhoTrongKhu(L) && DemTuiTrong(L) >= Math.Max(1, Cfg.NguongNhan) && LoaiCanDonGap().Count == 0) return;
            DateTime nghi;
            bool leaderNghi = _nghiBaoTri.TryGetValue(_leaderAcc, out nghi) && now < nghi;
            var t = So.Lay(_leaderAcc);
            if (t == null || !t.Online) return;
            var giu = Hang.KhoaDangGiuTren(_leaderAcc);

            // C. don xu (D45)
            if (Cfg.BatDonXu && t.Xu > Cfg.XuNguong && TaoDonXu(t, now)) return;

            // A. cat ruong Leader (dang xa nhanh thi khong - tui do ChuyenTiep lo)
            int tuiCat = MonCoTheCat(t, giu);
            bool ruongCho = RuongConCho(t, giu);
            if (Cfg.BatCatRuong && !leaderNghi && !xaChay)
            {
                if ((t.SoORuong < 0 || CanDocRuong(_leaderAcc, L)) && tuiCat == 0)
                {
                    // Co tu xoa trong CanDocRuong khi ruong da ve (viec bi nhuong / hong thi van con co).
                    GiaoViec(new Viec { Loai = LoaiViec.DocRuong, Acc = _leaderAcc, HetHan = now.AddMinutes(3) });
                    return;
                }
                if (tuiCat > 0 && ruongCho)
                {
                    var v = new Viec { Loai = LoaiViec.CatRuong, Acc = _leaderAcc, HetHan = now.AddMinutes(5) };
                    foreach (var k in giu) v.KhongCat.Add(k);
                    GiaoViec(v);
                    return;
                }
            }

            // B. chuyen sang clone theo ke
            if (!Cfg.BatDonKho) return;
            bool tuRuong = t.Mon.Exists(m => m.TrongRuong && !m.Khoa.Khoa && !giu.Contains(m.Khoa));
            // Dang xa nhanh (buoc A bi chan) ma toi day: lenh cho chong lon trong tui Leader -> don thang tu tui.
            bool tuTui = !tuRuong && tuiCat > 0 && (!Cfg.BatCatRuong || !ruongCho || xaChay);
            if (!tuRuong && !tuTui) return;
            var gap = LoaiCanDonGap();
            var nguon = t.Mon.Where(m => !m.Khoa.Khoa && !giu.Contains(m.Khoa))
                             .OrderByDescending(m => gap.Contains(m.Khoa.Tpl)).ThenBy(m => m.TrongRuong).ThenBy(m => m.Slot).ToList();
            var nha = NhaCuaChong(nguon.Select(m => m.Khoa.Tpl));

            // Chon nick nhan (user 16/09: "do gop duoc thi bo vao cung nick"):
            //  1. mon XEP CHONG da co nick giu -> dua ve dung nick do (nick dang ban thi CHO, khong rai sang nick khac);
            //  2. con lai -> chon theo ke nhu cu (ChonCloneNhan).
            string clone = null, nhom = null;
            bool tran = false, choNha = false;
            foreach (var m in nguon)
            {
                string h;
                if (nha.TryGetValue(m.Khoa.Tpl, out h))
                {
                    // Luot cua nha: mang theo ca mon chua co nha thuoc KE CUA NICK DO.
                    if (RanhNhanDon(h, now)) { clone = h; nhom = KeHang.KeCuaAcc(h, Cfg); break; }
                    choNha = true;
                    continue;
                }
                nhom = NhomCua(m.Khoa.Tpl);
                var cungNhom = nguon.Where(x => !nha.ContainsKey(x.Khoa.Tpl) && NhomCua(x.Khoa.Tpl) == nhom).ToList();
                clone = ChonCloneNhan(nhom, cungNhom, now, out tran);
                if (clone == null)
                {
                    GhiHanChe("khongclone|" + nhom, 300, "-", "Don", "Khong co clone nao nhan duoc ke " + nhom
                        + " (het cho / offline / dang ban / vao game chua du " + _choSauVaoGameGiay + " giay)");
                    _donNghiDen = now.AddSeconds(60);
                    return;
                }
                break;
            }
            if (clone == null)
            {
                if (choNha) _donNghiDen = now.AddSeconds(10);   // chi con mon cho nick giu dang ban
                return;
            }
            if (tran) Ghi(clone, "KE_TRAN", "Ke " + nhom + " het cho - don sang " + clone + " (ke " + KeHang.KeCuaAcc(clone, Cfg) + ")");
            var nd = new PhienDon { MucDich = DON, Clone = clone, CloneTen = TenNhanVat(clone), Nhom = nhom };
            if (!LapLuotDon(nd, t, giu))
            {
                // Luot rong (vd mon dau tien la chong > MAX_SO_LUONG can 2 o ma nick chi con 1 - M30): nick nay nghi 2 phut
                // de lan sau chon nick / mon khac, khong chon lai dung cap do mai (review 17/09).
                _nghiNhanDon[clone] = now.AddMinutes(2);
                _donNghiDen = now.AddSeconds(30);
                return;
            }
            nd.ViecClone = new Viec { Loai = LoaiViec.DoiNhan, Acc = clone, TuBotAcc = _leaderAcc, HetHan = now.AddMinutes(6) };
            if (!GiaoViec(nd.ViecClone)) return;
            _don = nd;
            Ghi(clone, "Don", "Goi " + clone + " sang khu chinh nhan ke " + nhom + " tu " + (tuRuong ? "ruong" : "tui") + " Leader");
        }

        // ---------------- go ket (D78) ----------------

        // clone -> luc duoc thu tra bot lai (sau mot lan hong)
        private readonly Dictionary<string, DateTime> _traNghi = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        // clone -> vua tra bot XONG: khong mo luot moi toi luc nay (so kho chua kip thay tui trong)
        private readonly Dictionary<string, DateTime> _traXongDen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        // acc -> so lan / moc dau bao TUI_DAY khi giao lenh rut (chot chan vong lap giao - hong - giao)
        private readonly Dictionary<string, int> _tuiDayDem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _tuiDayTu = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Clone KET CUNG: tui khong con o nao VA ruong khong nhan them (khong o trong, khong gop chong duoc) -
        /// mon trong ruong khong lay ra duoc nua, cat ruong cung khong duoc. Can ruong da doc trong phien nay.
        /// </summary>
        private bool KetCung(TuiAcc t)
        {
            if (t == null || t.TuiTrong >= 1) return false;
            // Mode vua bao TUI_DAY (no nhin tui + ruong THAT, luat doi cho cua no) -> tin mode. So kho khong
            // tinh duoc het: co han cua chong trong ruong, mon dang giu... (review 17/09).
            DateTime tu;
            if (_tuiDayTu.TryGetValue(t.Acc, out tu) && (DateTime.Now - tu).TotalMinutes < 10) return true;
            if (t.SoORuong < 0 || RuongConCho(t, Hang.KhoaDangGiuTren(t.Acc))) return false;
            if (_canDocRuong.Contains(t.Acc))
            {
                // Ruong trong so kho co the cu (vua dang nhap, chua mo ruong) - viec khac da mo thi dung duoc.
                var c = LayClient(t.Acc);
                var mc = c != null ? c.GameState.MyChar : null;
                if (mc == null || mc.BoxItems == null) return false;
            }
            return true;
        }

        /// <summary>
        /// Luot go ket co the chay cho acc nay khong (bo tam luc Leader vua vao game - cai do chi can cho).
        /// false kem ly do -> lenh rut KHONG cho go ket nua ma xu nhu loi phia acc (tam dung co ly do ro).
        /// </summary>
        private bool CoTheTraBot(TuiAcc t, out string viSao)
        {
            viSao = null;
            if (!Cfg.BatDonKho) { viSao = "dang tat don kho"; return false; }
            if (!LaCloneKho(t.Acc)) { viSao = t.Acc + " la Leader / du phong / dang nha"; return false; }
            DateTime nghi;
            if (_traNghi.TryGetValue(t.Acc, out nghi) && DateTime.Now < nghi) { viSao = "vua tra bot hong, cho toi " + nghi.ToString("HH:mm"); return false; }
            var giu = Hang.KhoaDangGiuTren(t.Acc);
            // Cung luat voi TaoTraBot: chong qua MAX_SO_LUONG khong tach duoc trong tui het o (M30).
            if (!t.Mon.Exists(m => !m.TrongRuong && !m.Khoa.Khoa && !giu.Contains(m.Khoa)
                                   && m.SoLuong <= Controller.TradeHandler.MAX_SO_LUONG))
            {
                viSao = "tui chi co mon khoa / mon dang giu cho lenh / chong qua " + Controller.TradeHandler.MAX_SO_LUONG;
                return false;
            }
            var L = LeaderClient;
            var lt = So.Lay(_leaderAcc);
            if (L == null || !OKhuChinh(L) || lt == null || !lt.Online) { viSao = "Leader khong o khu chinh"; return false; }
            if (Math.Min(lt.TuiTrong, DemTuiTrong(L)) < 2) { viSao = "tui Leader day"; return false; }
            return true;
        }

        /// <summary>
        /// GO KET (D78): clone ket cung dua toi da 12 mon trong tui (khong giu cho lenh nao) cho Leader - luot
        /// don NGUOC CHIEU: clone giao, Leader dung yen doi nhan. Leader don tiep sang nick khac (clone vua tra
        /// nghi nhan 30 phut). Uu tien clone dang giu hang cho lenh rut.
        /// </summary>
        private bool TaoTraBot(NsoClient L, DateTime now)
        {
            if (!Cfg.BatDonKho) return false;
            var lt = So.Lay(_leaderAcc);
            if (lt == null || !lt.Online || VuaVaoGame(_leaderAcc)) return false;
            int leaderTrong = Math.Min(lt.TuiTrong, DemTuiTrong(L));
            if (leaderTrong < 2) return false;

            TuiAcc chon = null;
            List<OMon> dua = null;
            bool chonCoLenh = false;
            foreach (var t in So.TatCa())
            {
                if (!LaCloneKho(t.Acc) || !t.Online || !KetCung(t)) continue;
                DateTime nghi;
                if (_traNghi.TryGetValue(t.Acc, out nghi) && now < nghi) continue;
                if (_traXongDen.TryGetValue(t.Acc, out nghi) && now < nghi) continue;
                var c = LayClient(t.Acc);
                if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || CoViec(t.Acc)) continue;
                if (c.GameState.IsChangingMap || c.GameState.CurrentMap == null || c.GameState.CurrentMap.MapId != Cfg.Map) continue;
                var giu = Hang.KhoaDangGiuTren(t.Acc);
                // Ket cung = tui het o -> khong tach duoc chong qua MAX_SO_LUONG (M30), khong dua chong do.
                var mon = t.Mon.Where(m => !m.TrongRuong && !m.Khoa.Khoa && !giu.Contains(m.Khoa)
                                           && m.SoLuong <= Controller.TradeHandler.MAX_SO_LUONG)
                               .OrderBy(m => m.Slot).ToList();
                if (mon.Count == 0)
                {
                    GhiHanChe("ketcung|" + t.Acc, 600, t.Acc, "GoKet", "Clone " + t.Acc + " ket cung (tui + ruong day) nhung tui chi co mon dang giu cho lenh"
                        + " / chong qua " + Controller.TradeHandler.MAX_SO_LUONG + " khong tach duoc - khong tra bot duoc");
                    continue;
                }
                bool coLenh = giu.Count > 0;
                if (chon == null || (coLenh && !chonCoLenh)) { chon = t; dua = mon; chonCoLenh = coLenh; }
            }
            if (chon == null) return false;

            // Co lenh rut dang cho clone nay: dung toi gan het tui Leader. Go ket "phong truoc" thi giu nguyen nguong
            // nhan do cua Leader (khong de nguoi nap bi tu choi chi vi Leader dang cam do cua clone).
            int n = Math.Min(Controller.TradeHandler.MAX_MON, chonCoLenh ? leaderTrong - 1 : leaderTrong - Math.Max(1, Cfg.NguongNhan));
            if (n < 1) return false;
            var gop = new Dictionary<KhoaMon, int>();
            foreach (var m in dua.Take(n))
            {
                int s;
                gop.TryGetValue(m.Khoa, out s);
                gop[m.Khoa] = s + m.SoLuong;
            }
            var d = new PhienDon { MucDich = TRA, Clone = chon.Acc, CloneTen = TenNhanVat(chon.Acc) };
            foreach (var kv in gop) d.Dong.Add(new DongGiao { Khoa = kv.Key, SoLuong = kv.Value });
            d.ViecLeader = new Viec { Loai = LoaiViec.DoiNhan, Acc = _leaderAcc, TuBotAcc = chon.Acc, HetHan = now.AddMinutes(4) };
            if (!GiaoViec(d.ViecLeader)) return false;
            d.ViecClone = new Viec
            {
                Loai = LoaiViec.GiaoMon, Acc = chon.Acc, NguoiNhan = TenNhanVat(_leaderAcc), NguoiNhanLaBot = true,
                MucDich = TRA, HetHan = now.AddMinutes(4),
            };
            foreach (var g in d.Dong) d.ViecClone.Dong.Add(new DongGiao { Khoa = g.Khoa, SoLuong = g.SoLuong });
            if (!GiaoViec(d.ViecClone))
            {
                d.ViecLeader.Huy("khong giao duoc viec tra bot cho " + chon.Acc);
                return false;
            }
            _don = d;
            Ghi(chon.Acc, "GoKet", "Clone " + chon.Acc + " ket cung (tui " + chon.TuiDung + "/" + chon.SoOTui + ", ruong "
                + chon.RuongDung + "/" + chon.SoORuong + ") -> tra " + Math.Min(n, dua.Count) + " o cho Leader"
                + (chonCoLenh ? " (dang giu hang cho lenh rut)" : ""));
            DeDoc("GỠ KẸT", TenNhanVat(chon.Acc) + " đầy cả túi lẫn rương → trả bớt " + Math.Min(n, dua.Count) + " ô cho Leader");
            return true;
        }

        /// <summary>Bao cao viec cua mot luot go ket: clone giao xong / hong, hoac Leader thoi doi.</summary>
        private void SauViecTra(PhienDon d, Viec v, BaoCaoViec bc, DateTime now)
        {
            if (d.ViecClone == v)
            {
                if (d.ViecLeader != null) d.ViecLeader.Huy(bc.Xong ? "tra bot xong" : "clone tra bot hong");
                if (bc.Xong)
                {
                    _nghiNhanDon[d.Clone] = now.AddMinutes(30);
                    _traXongDen[d.Clone] = now.AddSeconds(60);   // so kho cap nhat tui vai nhip sau - khong mo luot thua
                    Ghi(d.Clone, "GoKet", "Xong: " + d.Clone + " da tra " + d.Dong.Sum(x => x.SoLuong) + " mon cho Leader, nghi nhan don 30 phut");
                }
                else
                {
                    _traNghi[d.Clone] = now.AddMinutes(10);
                    _donNghiDen = now.AddSeconds(30);
                    Ghi(d.Clone, "GoKet", "Tra bot hong (" + bc.MaLoi + " " + bc.LyDo + ") - thu lai sau 10 phut");
                }
            }
            else
            {
                d.ViecClone.Huy("Leader thoi doi nhan");
                HuyPhienCua(d.ViecClone.Acc, d.ViecClone, "Leader thoi doi nhan");
                if (!bc.Xong) _traNghi[d.Clone] = now.AddMinutes(10);
            }
            _don = null;
        }

        /// <summary>
        /// "Nha" cua tung loai XEP CHONG: clone (dang online) giu nhieu nhat loai do trong tui + ruong. Mon
        /// cung loai ve cung nick thi server tu gop thanh MOT chong (test song 16/09: nhan 10 o le -> 1 chong
        /// x10) - kho do ton o, rut ra do phai gom tu nhieu nick. Loai thuoc ke Rac chi tim nha trong ke Rac
        /// va nguoc lai.
        /// </summary>
        private static readonly HashSet<KhoaMon> KHONG_GIU = new HashSet<KhoaMon>();

        private Dictionary<short, string> NhaCuaChong(IEnumerable<short> tpls)
        {
            var r = new Dictionary<short, string>();
            var can = new HashSet<short>(tpls.Where(LaChong));
            if (can.Count == 0) return r;
            var tot = new Dictionary<short, int>();
            foreach (var t in So.TatCa())
            {
                if (!LaCloneKho(t.Acc) || !t.Online) continue;
                var c = LayClient(t.Acc);
                if (c == null || c.State != ClientState.InGame) continue;
                // Het cho that (tui chi con o chua VA ruong khong nhan them) thi khong con la nha - mon moi di
                // nick khac, khong cho mai lam day Leader. Tui day ma ruong con cho: sap tu cat ruong, van la nha.
                if (t.TuiTrong <= O_CHUA_TUI && !RuongConCho(t, KHONG_GIU)) continue;
                // Dang nghi nhan (vua tra bot do cho Leader - D78, hoac vua nhan hong) -> khong la nha, khong thi
                // Leader cam mon cho mai / tra nguoc lai dung nick vua go ket.
                DateTime nghiNhan;
                if (_nghiNhanDon.TryGetValue(t.Acc, out nghiNhan) && DateTime.Now < nghiNhan) continue;
                bool keRac = KeHang.KeCuaAcc(t.Acc, Cfg) == KeHang.RAC;
                var dem = new Dictionary<short, int>();
                foreach (var m in t.Mon)
                {
                    if (m.Khoa.Khoa || !can.Contains(m.Khoa.Tpl)) continue;
                    if (keRac != (NhomCua(m.Khoa.Tpl) == KeHang.RAC)) continue;
                    int n;
                    dem.TryGetValue(m.Khoa.Tpl, out n);
                    dem[m.Khoa.Tpl] = n + Math.Max(1, m.SoLuong);
                }
                foreach (var kv in dem)
                {
                    int cu;
                    if (tot.TryGetValue(kv.Key, out cu) && cu >= kv.Value) continue;
                    tot[kv.Key] = kv.Value;
                    r[kv.Key] = t.Acc;
                }
            }
            return r;
        }

        /// <summary>Clone nay nhan duoc mot luot don ngay bay gio khong.</summary>
        private bool RanhNhanDon(string acc, DateTime now)
        {
            var c = LayClient(acc);
            if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || CoViec(acc) || VuaVaoGame(acc)) return false;
            DateTime nghi;
            if (_nghiNhanDon.TryGetValue(acc, out nghi) && now < nghi) return false;
            var t = So.Lay(acc);
            return t != null && t.Online && t.TuiTrong > O_CHUA_TUI;
        }

        /// <summary>Lap danh sach mon cho mot luot (toi da 12 o, khong qua o trong tui clone / tui Leader).</summary>
        private bool LapLuotDon(PhienDon d, TuiAcc leader, HashSet<KhoaMon> giu)
        {
            var ct = So.Lay(d.Clone);
            if (ct == null) return false;
            int n = Math.Min(Controller.TradeHandler.MAX_MON, Math.Min(ct.TuiTrong, DemTuiTrong(LayClient(d.Clone))) - O_CHUA_TUI);
            if (n <= 0) return false;
            // Gom CA tui lan ruong cua ke nay, tui truoc (khong ton buoc lay ruong). Test song 16/09: luot
            // hong de lai mon trong tui Leader, ban cu chi gom phia ruong -> 3 luot lien cho 3 vien da le.
            // Mon lay tu ruong khong duoc vuot so o trong tui Leader.
            // Mon xep chong da co nick giu: CHI vao luot cua dung nick do (bat ke ke). Mon chua co nick giu:
            // theo ke cua luot - nick nay thanh nha cua no tu luot sau.
            var nha = NhaCuaChong(leader.Mon.Select(x => x.Khoa.Tpl));
            var gop = new Dictionary<KhoaMon, int>();
            int dem = 0, tuRuong = 0;
            var gap = LoaiCanDonGap();
            foreach (var m in leader.Mon.OrderByDescending(x => gap.Contains(x.Khoa.Tpl)).ThenBy(x => x.TrongRuong).ThenBy(x => x.Slot))
            {
                if (dem >= n) break;
                if (m.Khoa.Khoa || giu.Contains(m.Khoa)) continue;
                string h;
                if (nha.TryGetValue(m.Khoa.Tpl, out h)) { if (!CungAcc(h, d.Clone)) continue; }
                else if (NhomCua(m.Khoa.Tpl) != d.Nhom) continue;
                int o = SoOGiao(m.SoLuong);
                if (dem + o > n) continue;
                // O trong tui Leader can dung: lay tu ruong ra (1 o) + moi lan tach chong lon (o - 1, M30).
                int canTui = m.TrongRuong ? o : o - 1;
                if (canTui > 0)
                {
                    if (tuRuong + canTui > leader.TuiTrong) continue;
                    tuRuong += canTui;
                }
                int c;
                gop.TryGetValue(m.Khoa, out c);
                gop[m.Khoa] = c + m.SoLuong;
                dem += o;
            }
            d.Dong.Clear();
            foreach (var kv in gop) d.Dong.Add(new DongGiao { Khoa = kv.Key, SoLuong = kv.Value });
            return d.Dong.Count > 0;
        }

        /// <summary>Pha 2 cua luot don: clone da toi khu chinh -> giao viec cho Leader (va lap luot sau).</summary>
        private void TiepDon(PhienDon d, NsoClient L, DateTime now)
        {
            if (d.ViecLeader != null) return;
            if (d.ViecClone.BiHuy) { _don = null; return; }
            if (L == null || !CungAcc(L.Config.Username, d.ViecClone.TuBotAcc))
            {
                d.ViecClone.Huy("Leader doi / offline");
                _don = null;
                return;
            }
            if (d.MucDich == DON && d.Dong.Count == 0)
            {
                var t = So.Lay(_leaderAcc);
                if (t == null || !LapLuotDon(d, t, Hang.KhoaDangGiuTren(_leaderAcc)))
                {
                    d.ViecClone.Huy("het mon ke " + d.Nhom + " / clone het cho");
                    Ghi(d.Clone, "Don", "Xong don ke " + d.Nhom + " sau " + d.SoLuot + " luot");
                    _don = null;
                    return;
                }
            }
            if ((now - d.TaoLuc).TotalSeconds > 90)
            {
                Ghi(d.Clone, "Don", "Clone " + d.Clone + " khong san sang sau 90s - bo luot don");
                d.ViecClone.Huy("qua 90s");
                _nghiNhanDon[d.Clone] = now.AddMinutes(2);
                _don = null;
                return;
            }
            if (TimNguoiTheoAcc(d.Clone) == null || VuaVaoGame(d.Clone)) return;
            if (L.DangGiaoDich || CoViec(_leaderAcc) || !LeaderRanh(now)) return;
            var v = new Viec
            {
                Loai = LoaiViec.GiaoMon, Acc = _leaderAcc, NguoiNhan = d.CloneTen, NguoiNhanLaBot = true,
                MucDich = d.MucDich, Xu = d.Xu, HetHan = now.AddMinutes(5),
            };
            foreach (var g in d.Dong) v.Dong.Add(new DongGiao { Khoa = g.Khoa, SoLuong = g.SoLuong });
            if (!GiaoViec(v)) return;
            d.ViecLeader = v;
            d.SoLuot++;
        }

        /// <summary>
        /// Chon clone nhan (SPEC §6 buoc 3): cung ke + dang giu cung loai chong → cung ke nhieu cho nhat →
        /// ke Khac → bat ky (tru ke Rac). <paramref name="tran"/> = phai dung ke khac (log KE_TRAN).
        /// </summary>
        private string ChonCloneNhan(string nhom, List<OMon> mon, DateTime now, out bool tran)
        {
            tran = false;
            var ung = new List<TuiAcc>();
            foreach (var acc in _accTheoTen.Keys)
            {
                if (!LaCloneKho(acc)) continue;
                var c = LayClient(acc);
                if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || CoViec(acc) || VuaVaoGame(acc)) continue;
                DateTime nghi;
                if (_nghiNhanDon.TryGetValue(acc, out nghi) && now < nghi) continue;
                var t = So.Lay(acc);
                if (t == null || !t.Online || t.TuiTrong <= O_CHUA_TUI) continue;
                ung.Add(t);
            }
            Func<TuiAcc, int> cho = x => x.TuiTrong + x.RuongTrong;
            // Trong cung ke: nick DANG GIU nhieu mon cung nhom nhat truoc (lap day tung nick, mon cung nhom
            // o cung cho -> rut ra mot nick giao du), roi moi toi nick trong nhat (user 16/09: lam thong minh hon).
            Func<TuiAcc, int> soNhom = x => x.Mon.Count(m => !m.Khoa.Khoa && NhomCua(m.Khoa.Tpl) == nhom);
            // Mon khong xep chong cung loai (da, trang bi cung id...) cung nen o mot nick: rut "het" chi mot nick giao.
            var loaiLuot = new HashSet<short>(mon.Select(m => m.Khoa.Tpl));
            Func<TuiAcc, int> soCungLoai = x => x.Mon.Count(m => !m.Khoa.Khoa && loaiLuot.Contains(m.Khoa.Tpl));
            var cungKe = ung.Where(x => KeHang.KeCuaAcc(x.Acc, Cfg) == nhom)
                            .OrderByDescending(soCungLoai).ThenByDescending(soNhom).ThenByDescending(cho).ToList();
            foreach (var x in cungKe)
                if (mon.Exists(m => LaChong(m.Khoa.Tpl) && x.CoTpl(m.Khoa.Tpl))) return x.Acc;
            if (cungKe.Count > 0) return cungKe[0].Acc;
            tran = true;
            // Chua co clone gan ke nay: clone dang giu nhieu mon cung nhom nhat la "ke tu nhien".
            if (nhom != KeHang.RAC)
            {
                var tuNhien = ung.Where(x => KeHang.KeCuaAcc(x.Acc, Cfg) != KeHang.RAC && (soNhom(x) > 0 || soCungLoai(x) > 0))
                                 .OrderByDescending(soCungLoai).ThenByDescending(soNhom).ThenByDescending(cho).FirstOrDefault();
                if (tuNhien != null) return tuNhien.Acc;
            }
            if (nhom != KeHang.KHAC)
            {
                var khac = ung.Where(x => KeHang.KeCuaAcc(x.Acc, Cfg) == KeHang.KHAC).OrderByDescending(cho).FirstOrDefault();
                if (khac != null) return khac.Acc;
            }
            var bat = ung.Where(x => KeHang.KeCuaAcc(x.Acc, Cfg) != KeHang.RAC).OrderByDescending(cho).FirstOrDefault();
            return bat != null ? bat.Acc : null;
        }

        /// <summary>Don xu (D45): chuyen phan vuot XuNguong sang clone it xu nhat, clone giu toi da XuTran - 100 trieu.</summary>
        private bool TaoDonXu(TuiAcc leader, DateTime now)
        {
            TuiAcc chon = null;
            foreach (var acc in _accTheoTen.Keys)
            {
                if (!LaCloneKho(acc)) continue;
                var c = LayClient(acc);
                if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || CoViec(acc) || VuaVaoGame(acc)) continue;
                DateTime nghi;
                if (_nghiNhanDon.TryGetValue(acc, out nghi) && now < nghi) continue;
                var t = So.Lay(acc);
                if (t == null || !t.Online) continue;
                if (chon == null || t.Xu < chon.Xu) chon = t;
            }
            if (chon == null) return false;
            long thua = (long)leader.Xu - Cfg.XuNguong;
            long cho = Math.Max(0L, (long)Cfg.XuTran - 100000000L) - chon.Xu;
            long xu = Math.Min(thua, cho);
            if (xu <= 0)
            {
                GhiHanChe("donxu", 600, "-", "DonXu", "Leader vuot nguong xu nhung moi clone da gan tran xu");
                _donNghiDen = now.AddMinutes(10);
                return false;
            }
            var d = new PhienDon { MucDich = DON_XU, Clone = chon.Acc, CloneTen = TenNhanVat(chon.Acc), Xu = (int)xu };
            d.ViecClone = new Viec { Loai = LoaiViec.DoiNhan, Acc = chon.Acc, TuBotAcc = _leaderAcc, HetHan = now.AddMinutes(4) };
            if (!GiaoViec(d.ViecClone)) return false;
            _don = d;
            Ghi(chon.Acc, "DonXu", "Goi " + chon.Acc + " nhan " + ChuVan.SoDep(xu) + " xu tu Leader");
            return true;
        }

        // =====================================================================
        // GOM DO XEP CHONG (D81)
        // =====================================================================

        /// <summary>
        /// Mot loai xep chong nam o NHIEU clone thi ton o, rut ra phai di nhieu nick. Gom ve mot nick dich:
        /// <para>Dich chon de <b>giai phong nhieu o nhat</b>, roi <b>it luot giao dich nhat</b>. Moi luot mot clone giao toi
        /// da min(12, o trong cua dich - O_CHUA_TUI) chong: server doi nguoi nhan con du o cho TUNG chong, ke ca chong se
        /// tu gop (user 17/09: "B chi con 1 o thi A chi giao duoc tung mon") -> nick gan day tui khong lam dich; ca nhom
        /// giu hang deu chat thi nick trong lam trung gian (goi y cua user). Nhan nhieu chong cung loai thi server gop
        /// thanh MOT (test song 16/09).</para>
        /// <para>Chay o khu cua clone (khong dung toi Leader / khu chinh), chi khi kho ranh: khong lenh rut, khong xa nhanh,
        /// khong clone cho nha. Dot gom GHIM dich toi khi xong - khong doi dich giua chung (khong chuyen qua chuyen lai).
        /// Lenh rut can nick dang gom -> huy luot gom (NhuongViec).</para>
        /// </summary>
        private void XuLyGom(DateTime now)
        {
            var g = _gom;
            if (g != null)
            {
                if ((now - g.TaoLuc).TotalMinutes > 6) HuyGom("luot gom qua 6 phut", now);
                return;
            }
            if (!Cfg.BatGomChong || !Cfg.BatCatRuong || Cfg.KhuChinh < 0) return;
            if (now < _gomNghiDen || (now - _gomXetLuc).TotalSeconds < 15) return;
            _gomXetLuc = now;
            if (Hang.SoCho > 0 || _xa != null || _choNha.Count > 0) return;
            foreach (var k in _gomTranh.Where(kv => now > kv.Value).Select(kv => kv.Key).ToList()) _gomTranh.Remove(k);

            var ds = CloneGom();
            if (_gomDich != null)
            {
                string het = DotGomConDung(ds, now);
                if (het != null)
                {
                    KetThucDotGom(het.Length == 0 ? null : het);
                    return;   // nghi 2 phut (KetThucDotGom) truoc khi lap dot moi
                }
            }
            if (_gomDich == null && !LapDotGom(ds, now)) return;
            TaoLuotGom(ds, now);
        }

        /// <summary>Clone xet duoc cho gom: online o map kho, ruong da doc trong phien nay, khong tranh.</summary>
        private List<TuiAcc> CloneGom()
        {
            var r = new List<TuiAcc>();
            foreach (var t in So.TatCa())
            {
                if (!LaCloneKho(t.Acc) || !t.Online || t.SoORuong < 0) continue;
                var c = LayClient(t.Acc);
                if (c == null || c.State != ClientState.InGame || CanDocRuong(t.Acc, c)) continue;
                var m = c.GameState.CurrentMap;
                if (c.GameState.IsChangingMap || m == null || m.MapId != Cfg.Map) continue;
                DateTime den;
                if (_gomTranh.TryGetValue(t.Acc, out den) && DateTime.Now < den) continue;
                r.Add(t);
            }
            return r;
        }

        /// <summary>
        /// So CHONG (o) moi loai xep chong nick nay GIAO DUOC (tui + ruong), bo mon khoa + loai sai ke (Rac / khong Rac).
        /// Tui khong con o nao: chi chong trong tui (chong trong ruong khong lay ra duoc - go ket D78 lo) - khong thi dot
        /// gom lap lai mai cho mot nick khong giao duoc.
        /// </summary>
        private Dictionary<short, int> ChongCua(TuiAcc t)
        {
            var r = new Dictionary<short, int>();
            bool keRac = KeHang.KeCuaAcc(t.Acc, Cfg) == KeHang.RAC;
            bool chiTui = t.TuiTrong < 1;
            foreach (var m in t.Mon)
            {
                if (m.Khoa.Khoa || !LaChong(m.Khoa.Tpl) || (chiTui && m.TrongRuong)) continue;
                if (keRac != (NhomCua(m.Khoa.Tpl) == KeHang.RAC)) continue;
                int n;
                r.TryGetValue(m.Khoa.Tpl, out n);
                r[m.Khoa.Tpl] = n + 1;
            }
            return r;
        }

        /// <summary>Nick lam dich gom cho nhom ke nay duoc khong (luat nhu ChonCloneNhan): Rac chi vao ke Rac; co clone gan
        /// dung ke thi chi cac clone do; con lai tranh ke Rac.</summary>
        private bool LamDichDuoc(string acc, string nhom, List<TuiAcc> ds)
        {
            string ke = KeHang.KeCuaAcc(acc, Cfg);
            if (nhom == KeHang.RAC) return ke == KeHang.RAC;
            if (ke == KeHang.RAC) return false;
            return ke == nhom || !ds.Exists(t => KeHang.KeCuaAcc(t.Acc, Cfg) == nhom);
        }

        /// <summary>Tim loai dang rai + chon dich cho mot dot. true = da ghim dot moi.</summary>
        private bool LapDotGom(List<TuiAcc> ds, DateTime now)
        {
            var chong = new Dictionary<string, Dictionary<short, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in ds) chong[t.Acc] = ChongCua(t);
            var giu = new Dictionary<short, int>();       // loai -> so nick giu
            var tong = new Dictionary<short, long>();     // loai -> tong so luong
            foreach (var t in ds)
            {
                foreach (var tpl in chong[t.Acc].Keys)
                {
                    int n;
                    giu.TryGetValue(tpl, out n);
                    giu[tpl] = n + 1;
                }
                foreach (var m in t.Mon)
                {
                    if (m.Khoa.Khoa || !chong[t.Acc].ContainsKey(m.Khoa.Tpl)) continue;
                    long s;
                    tong.TryGetValue(m.Khoa.Tpl, out s);
                    tong[m.Khoa.Tpl] = s + m.SoLuong;
                }
            }
            // Mot o giao dich toi da MAX_SO_LUONG (M30; chong trong tui gop toi 32.000): loai tong vuot muc nay gom ve mot
            // nick van phai nhieu chong -> bo. Duoi muc thi moi chong deu giao nguyen duoc, khong can tach.
            var rai = giu.Where(kv => kv.Value >= 2 && tong[kv.Key] <= Controller.TradeHandler.MAX_SO_LUONG).Select(kv => kv.Key).ToList();
            if (rai.Count == 0) return false;

            foreach (var nhom in rai.GroupBy(NhomCua).OrderByDescending(x => x.Count()))
            {
                string best = null;
                List<short> bestLoai = null;
                int bestLoi = 0, bestLuot = int.MaxValue, bestCong = int.MaxValue, bestCho = -1;
                long bestCo = -1;
                foreach (var x in ds)
                {
                    if (!LamDichDuoc(x.Acc, nhom.Key, ds)) continue;
                    int moiLuot = Math.Min(Controller.TradeHandler.MAX_MON, x.TuiTrong - O_CHUA_TUI);
                    if (moiLuot < 1) continue;
                    var cx = chong[x.Acc];
                    // Loai dich chua giu -> ton mot o ruong moi (khong vuot ruong trong). Loai nhieu chong o nick khac truoc.
                    Func<short, int> oNgoai = tp => ds.Where(a => !CungAcc(a.Acc, x.Acc)).Sum(a =>
                    {
                        int n;
                        return chong[a.Acc].TryGetValue(tp, out n) ? n : 0;
                    });
                    var chon = new List<short>();
                    int oMoi = 0;
                    foreach (var tp in nhom.OrderByDescending(oNgoai))
                    {
                        if (!cx.ContainsKey(tp))
                        {
                            if (oMoi >= x.RuongTrong) continue;
                            oMoi++;
                        }
                        chon.Add(tp);
                    }
                    int chuyen = 0, luot = 0;
                    foreach (var a in ds)
                    {
                        if (CungAcc(a.Acc, x.Acc)) continue;
                        int s = 0;
                        foreach (var tp in chon)
                        {
                            int n;
                            if (chong[a.Acc].TryGetValue(tp, out n)) s += n;
                        }
                        if (s == 0) continue;
                        chuyen += s;
                        // Nick giao cung chi mang duoc chung ay chong moi luot: o trong + chong da nam san trong tui
                        // (review 17/09: nick giao tui chat lay ruong ra tung it mot).
                        int trongTui = a.Mon.Count(m => !m.TrongRuong && !m.Khoa.Khoa && chon.Contains(m.Khoa.Tpl));
                        int mang = Math.Max(1, Math.Min(moiLuot, a.TuiTrong - O_CHUA_TUI + trongTui));
                        luot += (s + mang - 1) / mang;
                    }
                    int loi = chuyen - oMoi;
                    if (loi < 1) continue;
                    // Cong viec = so luot giao dich + so lan dich phai cat ruong cho trong tui (moi lan nhan day moiLuot o):
                    // dich chi nhan 1 chong / luot thi luot nao cung phai chay ra Thu kho.
                    int cong = luot + (chuyen + moiLuot - 1) / moiLuot;
                    long coSan = x.Mon.Where(m => !m.Khoa.Khoa && chon.Contains(m.Khoa.Tpl)).Sum(m => (long)m.SoLuong);
                    int cho = x.TuiTrong + x.RuongTrong;
                    bool hon = best == null || loi > bestLoi
                               || (loi == bestLoi && (cong < bestCong
                                   || (cong == bestCong && (coSan > bestCo || (coSan == bestCo && cho > bestCho)))));
                    if (!hon) continue;
                    best = x.Acc; bestLoai = chon; bestLoi = loi; bestLuot = luot; bestCong = cong; bestCo = coSan; bestCho = cho;
                }
                if (best == null) continue;
                _gomDich = best;
                _gomLoai = new HashSet<short>(bestLoai);
                _gomDotLuc = now;
                _gomDotLuot = 0;
                _gomDotMon = 0;
                Ghi(best, "Gom", "Dot gom moi: " + bestLoai.Count + " loai xep chong ve " + best + " (du kien " + bestLuot
                    + " luot, bot ~" + bestLoi + " o): " + string.Join(", ", bestLoai.Select(tp => tp.ToString()).ToArray()));
                DeDoc("GOM", "Gom " + bestLoai.Count + " loại đồ xếp chồng đang rải ở nhiều nick về " + TenNhanVat(best)
                    + " (khoảng " + bestLuot + " lượt, trống thêm ~" + bestLoi + " ô)");
                return true;
            }
            GhiHanChe("gomkhong", 1800, "-", "Gom", "Co " + rai.Count + " loai xep chong rai nhieu nick nhung chua nick nao lam dich duoc (tui / ruong het cho)");
            _gomNghiDen = now.AddMinutes(5);
            return false;
        }

        /// <summary>null = dot dang ghim lam tiep; "" = xong (khong con nick nao khac giu loai cua dot); khac = ly do dung.</summary>
        private string DotGomConDung(List<TuiAcc> ds, DateTime now)
        {
            if ((now - _gomDotLuc).TotalMinutes > 60) return "quá 60 phút";
            if (!LaCloneKho(_gomDich) || !DangOnline(_gomDich)) return TenNhanVat(_gomDich) + " không còn làm kho / offline";
            // Chi dem nick XET DUOC (cung danh sach TaoLuotGom dung): dem ca nick dang tranh / chua doc ruong / sai map thi
            // dot bi ghim toi 60 phut roi lap lai dung dot do (review 17/09). Nick dang ban viec khac van tinh (se ranh).
            foreach (var t in ds)
            {
                if (CungAcc(t.Acc, _gomDich)) continue;
                if (ChongCua(t).Keys.Any(_gomLoai.Contains)) return null;
            }
            return "";
        }

        private void KetThucDotGom(string loi)
        {
            if (_gomDich == null) return;
            string ten = TenNhanVat(_gomDich);
            if (loi == null)
            {
                Ghi(_gomDich, "Gom", "Xong dot gom ve " + _gomDich + ": " + _gomDotLuot + " luot, " + _gomDotMon + " mon");
                if (_gomDotLuot > 0) DeDoc("GOM", "Xong: đã gom " + _gomDotMon + " món (" + _gomDotLuot + " lượt) về " + ten);
            }
            else
            {
                Ghi(_gomDich, "Gom", "Dung dot gom ve " + _gomDich + ": " + ChuVan.BoDau(loi));
                DeDoc("GOM", "Dừng gom về " + ten + ": " + loi);
            }
            _gomDich = null;
            _gomLoai = new HashSet<short>();
            _gomNghiDen = DateTime.Now.AddMinutes(2);
        }

        /// <summary>Mot luot: nick giao giu nhieu chong nhat (dang ranh) giao cho dich o khu cua dich.</summary>
        private void TaoLuotGom(List<TuiAcc> ds, DateTime now)
        {
            var x = ds.Find(t => CungAcc(t.Acc, _gomDich));
            if (x == null) return;   // dich dang doc ruong / tam tranh - cho
            var cx = LayClient(x.Acc);
            if (cx == null || cx.DangGiaoDich || CoViec(x.Acc) || VuaVaoGame(x.Acc)) return;   // dich dang cat ruong... - cho
            int moiLuot = Math.Min(Controller.TradeHandler.MAX_MON, Math.Min(x.TuiTrong, DemTuiTrong(cx)) - O_CHUA_TUI);
            if (moiLuot < 1)
            {
                if (!Cfg.BatCatRuong || !RuongConCho(x, KHONG_GIU)) KetThucDotGom("túi và rương " + TenNhanVat(x.Acc) + " hết chỗ");
                else _canCat.Add(x.Acc);
                return;
            }
            int khu = KhuDungCho(cx);
            if (khu < 0) return;

            TuiAcc chon = null;
            List<OMon> monChon = null;
            foreach (var t in ds)
            {
                if (CungAcc(t.Acc, x.Acc)) continue;
                var c = LayClient(t.Acc);
                if (c == null || c.DangGiaoDich || CoViec(t.Acc) || VuaVaoGame(t.Acc)) continue;
                // Tui khong con o nao: chi giao chong dang o tui (ruong khong lay ra duoc - xem ChongCua).
                // Chong qua MAX_SO_LUONG (vd nick vua doc ruong sau luc ghim dot) khong gom - M30.
                var mon = t.Mon.Where(m => !m.Khoa.Khoa && _gomLoai.Contains(m.Khoa.Tpl) && (t.TuiTrong >= 1 || !m.TrongRuong)
                                           && m.SoLuong <= Controller.TradeHandler.MAX_SO_LUONG)
                               .OrderBy(m => m.TrongRuong).ThenBy(m => m.Slot).ToList();
                if (mon.Count == 0) continue;
                if (chon == null || mon.Count > monChon.Count) { chon = t; monChon = mon; }
            }
            if (chon == null) return;

            var gop = new Dictionary<KhoaMon, int>();
            int dem = 0;
            foreach (var m in monChon)
            {
                if (dem >= moiLuot) break;
                int s;
                gop.TryGetValue(m.Khoa, out s);
                gop[m.Khoa] = s + m.SoLuong;
                dem++;
            }
            var d = new PhienDon { MucDich = GOM, Clone = chon.Acc, CloneTen = TenNhanVat(chon.Acc), Dich = x.Acc };
            foreach (var kv in gop) d.Dong.Add(new DongGiao { Khoa = kv.Key, SoLuong = kv.Value });
            d.ViecNhan = new Viec { Loai = LoaiViec.DoiNhan, Acc = x.Acc, TuBotAcc = chon.Acc, Khu = khu, MucDich = GOM, HetHan = now.AddMinutes(4) };
            if (!GiaoViec(d.ViecNhan)) return;
            d.ViecClone = new Viec
            {
                Loai = LoaiViec.GiaoMon, Acc = chon.Acc, NguoiNhan = TenNhanVat(x.Acc), NguoiNhanLaBot = true,
                MucDich = GOM, Khu = khu, HetHan = now.AddMinutes(4),
            };
            foreach (var gd in d.Dong) d.ViecClone.Dong.Add(new DongGiao { Khoa = gd.Khoa, SoLuong = gd.SoLuong });
            if (!GiaoViec(d.ViecClone))
            {
                d.ViecNhan.Huy("khong giao duoc viec gom cho " + chon.Acc);
                return;
            }
            _gom = d;
            Ghi(chon.Acc, "Gom", "Luot gom: " + chon.Acc + " -> " + x.Acc + " o khu " + khu + " (" + dem + " chong)");
        }

        /// <summary>Bao cao viec cua mot luot gom: nick giao xong / hong, hoac nick dich thoi doi.</summary>
        private void SauViecGom(PhienDon g, Viec v, BaoCaoViec bc, DateTime now)
        {
            _gom = null;
            if (g.ViecClone == v)
            {
                if (g.ViecNhan != null) g.ViecNhan.Huy(bc.Xong ? "xong luot gom" : "nick giao gom hong");
                if (bc.Xong)
                {
                    _gomDotLuot++;
                    _gomDotMon += g.Dong.Sum(x => x.SoLuong);
                    _gomNghiDen = now.AddSeconds(3);   // so kho hai nick cap nhat vai nhip sau
                    _gomXetLuc = DateTime.MinValue;
                    return;
                }
                _gomNghiDen = now.AddSeconds(30);
                if (bc.MaLoi == MaLoiViec.BI_HUY) return;   // huy / nhuong / rot mang - thu lai sau
                Ghi(g.Clone, "Gom", "Luot gom hong (" + bc.MaLoi + " " + bc.LyDo + ")");
                bool loiDich = bc.MaLoi == MaLoiGiaoDich.THIEU_O || bc.MaLoi == MaLoiGiaoDich.TU_CHOI_HANG
                               || bc.MaLoi == MaLoiGiaoDich.HET_GIO_CHO_NHAN_LOI || bc.MaLoi == MaLoiViec.KHONG_THAY_NGUOI
                               || bc.MaLoi == MaLoiViec.KHONG_VAO_KHU;
                if (loiDich)
                {
                    _gomTranh[g.Dich] = now.AddMinutes(15);
                    KetThucDotGom(TenNhanVat(g.Dich) + " nhận không được: " + LyDoDeDoc(bc.MaLoi, bc.LyDo));
                }
                else
                {
                    _gomTranh[g.Clone] = now.AddMinutes(30);
                    DeDoc("GOM", TenNhanVat(g.Clone) + " → " + TenNhanVat(g.Dich) + " không thành: "
                        + LyDoDeDoc(bc.MaLoi, bc.LyDo) + " (bỏ qua " + TenNhanVat(g.Clone) + " 30 phút)");
                }
                return;
            }
            // Nick dich roi viec doi nhan truoc (rot mang / het han / nha) -> bo luot.
            if (g.ViecClone != null)
            {
                g.ViecClone.Huy("nick dich thoi doi");
                HuyPhienCua(g.ViecClone.Acc, g.ViecClone, "nick dich thoi doi");
            }
            _gomNghiDen = now.AddSeconds(30);
        }

        private void HuyGom(string lyDo, DateTime now)
        {
            var g = _gom;
            if (g == null) return;
            _gom = null;
            if (g.ViecClone != null)
            {
                g.ViecClone.Huy(lyDo);
                HuyPhienCua(g.ViecClone.Acc, g.ViecClone, lyDo);
            }
            if (g.ViecNhan != null) g.ViecNhan.Huy(lyDo);
            _gomNghiDen = now.AddMinutes(2);
            Ghi(g.Clone, "Gom", "Huy luot gom " + g.Clone + " -> " + g.Dich + " (" + lyDo + ")");
        }

        // =====================================================================
        // XA NHANH (D86): clone dung canh Leader, Leader chuyen tiep do vua nap
        // =====================================================================

        /// <summary>
        /// D86 "xa nhanh": Chu kho nap lien tuc vao Leader -> goi toi da XA_SO_CLONE clone ranh sang dung SAT canh
        /// Leader; sau moi luot nap Leader chuyen ngay phan vua nhan sang clone (bot - bot 2-3 giay) de tui Leader
        /// luon con cho. Nguoi choi chi giao dich voi Leader (user 17/09: "clone nen tu lay do tu Leader thi nhanh
        /// hon ... thay vi bat nguoi choi gd vao clone"). <paramref name="vi"/>: XA_NAP = Chu kho vua nap xong,
        /// XA_DAY = Chu kho moi luc Leader day, XA_LENH = Chu kho nhan `xa` (goi clone san truoc khi xa).
        /// </summary>
        private void MoXa(string chu, DateTime now, string vi)
        {
            if (string.IsNullOrEmpty(chu) || Cfg.KhuChinh < 0 || string.IsNullOrEmpty(_leaderAcc)) return;
            bool lenh = vi == XA_LENH, day = vi == XA_DAY;
            if (!Cfg.BatNap)
            {
                if (lenh || day) BaoRieng(chu, "Kho dang tat nhan do", "tatnap", 30);
                return;
            }
            if (_khoDay)
            {
                if (lenh || day) BaoRieng(chu, "Kho day, chua nhan them duoc", "donkho", 30);
                return;
            }
            if (!Cfg.BatDonKho)
            {
                if (lenh) BaoRieng(chu, "Kho dang tat don kho - khong xa nhanh duoc", "xatat", 30);
                else if (day) BaoRieng(chu, "Leader day, kho dang tat don kho", "donkho", 30);
                return;
            }
            // Leader day ma khong co gi chuyen tiep duoc (Rac / khoa / giu cho lenh): don kho thuong lo, khong goi clone,
            // khong gia han dot (review 17/09: dot keo dai mai lam kho dung im).
            if (day && MonChuyenDuoc(LeaderClient, 1).Count == 0)
            {
                BaoRieng(chu, "Leader dang don kho, moi lai sau ~15 giay", "donkho", 15);
                return;
            }
            var x = _xa;
            bool moi = x == null;
            // Mot dot cho ca kho: Chu kho khac nap trong luc nay cung duoc Leader chuyen tiep, chi gia han.
            if (moi) x = new PhienXa { ChuKho = chu, Leader = _leaderAcc };
            x.Den = now.AddMinutes(XA_PHUT);
            if (!lenh) x.NapLuc = now;
            if (lenh)
            {
                x.TuLenh = true;
                x.XongLuc = DateTime.MinValue;
            }
            BoSungXa(x);
            if (x.Clone.Count == 0)
            {
                if (lenh) BaoRieng(chu, "Chua co clone nao ranh de dung canh Leader, thu lai sau ~1 phut", "xakhong", 30);
                else if (day) BaoRieng(chu, "Leader dang don kho, moi lai sau ~15 giay", "donkho", 15);
                if (moi)
                {
                    GhiHanChe("xakhong", 60, "-", "Xa", "Khong mo duoc xa nhanh cho " + chu + ": khong clone nao ranh");
                    return;
                }
            }
            _xa = x;
            string ds = string.Join(", ", x.Clone.Select(TenNhanVat).ToArray());
            // Khong co "moi lai sau ~N giay" nao ngan hon thuc te: Leader nhan roi huy (D87) nen moi lai khong bi khoa.
            if (lenh && x.Clone.Count > 0) BaoRieng(chu, "Xa nhanh: " + ds + " dung canh Leader. Cu giao Leader, xong nhan: xa xong", null, 0);
            else if (day && x.Clone.Count > 0)
                BaoRieng(chu, CloneXaSanSang(x) ? "Leader dang chuyen do sang clone, moi lai sau ~3 giay"
                                                : "Clone dang toi canh Leader, moi lai sau ~10 giay", "xaday|" + chu, 10);
            else if (moi) BaoRieng(chu, "Leader se chuyen do sang clone ~3s/luot. Bao dang giao dich thi moi lai", "xagoiy|" + chu, 600);
            if (moi)
            {
                Ghi("-", "Xa", "Mo xa nhanh (" + vi + ", " + chu + "): " + string.Join(", ", x.Clone.ToArray()));
                DeDoc("XẢ", chu + (lenh ? " nhắn xả" : day ? " mời lúc Leader đầy" : " đang nạp")
                    + " — Leader chuyển ngay sang " + ds + " đứng cạnh");
            }
            CapNhatMoTaXa(x);
        }

        /// <summary>Bo clone da roi dot (viec het / huy / da doi sang viec khac).</summary>
        private void DonCloneXa(PhienXa x)
        {
            x.Clone.RemoveAll(a =>
            {
                var v = ViecCua(a);
                return v == null || v.BiHuy || v.MucDich != XA;
            });
            foreach (var a in x.KhongThayTu.Keys.ToList())
                if (!x.Clone.Exists(c => CungAcc(c, a))) x.KhongThayTu.Remove(a);
        }

        /// <summary>Co clone nao cua dot da dung o khu chinh du lau (Leader sap chuyen duoc) chua.</summary>
        private bool CloneXaSanSang(PhienXa x)
        {
            foreach (var a in x.Clone)
            {
                var c = LayClient(a);
                if (c != null && OKhuChinh(c) && (DateTime.UtcNow - c.VaoKhuLucUtc).TotalMilliseconds >= XA_CHO_SAU_VAO_KHU_MS)
                    return true;
            }
            return false;
        }

        private static DateTime HanCloneXa(PhienXa x, DateTime now)
        {
            return (x.Den > now ? x.Den : now).AddMinutes(1);
        }

        /// <summary>
        /// X cho clone dung cho canh Leader: sat hai ben (trong tam moi 40 px cua mode - Leader khong phai di), CUNG TANG
        /// DAT voi Leader. Test song 17/09 o Lang Tone: x = LeaderX - 30 la mep tang - clone roi xuong y = 288 (cmd52),
        /// Leader thay no o 216, moi 4 lan deu "qua xa". Uu tien cho chua ai dung; het thi dung chung; khong co cho nao
        /// cung tang thi dung ngay cho Leader.
        /// </summary>
        private int ChoDungXa(HashSet<int> dangDung)
        {
            var L = LeaderClient;
            var map = L != null ? L.GameState.CurrentMap : null;
            var tiles = map != null && map.MapId == Cfg.Map ? L.GameState.Tiles : null;
            int nenY = GameData.TileEngine.AlignToTile(Cfg.LeaderY);
            // Khong co du lieu o, hoac chinh cho Leader khong doc duoc la dat (cai sai / map la) -> khong loc.
            bool loc = tiles != null && tiles.IsLoaded && tiles.HasFlag(Cfg.LeaderX, nenY, O_DAT);
            var lech = new[] { XA_LECH_X, -XA_LECH_X };
            for (int lan = 0; lan < 2; lan++)
            {
                foreach (int l in lech)
                {
                    int x = Cfg.LeaderX + l;
                    if (x <= 24 || (lan == 0 && dangDung.Contains(x))) continue;
                    if (loc && !tiles.HasFlag(x, nenY, O_DAT)) continue;
                    return x;
                }
            }
            return Cfg.LeaderX;
        }

        /// <summary>Co "dung duoc" cua o ban do (T_TOP - Char.cs client goc; TileEngine.COLL_GROUND).</summary>
        private const int O_DAT = 2;

        /// <summary>Bo clone da roi; them clone ranh cho du XA_SO_CLONE. Tra ve so clone moi them.</summary>
        private int BoSungXa(PhienXa x)
        {
            DonCloneXa(x);
            if (x.Clone.Count >= XA_SO_CLONE) return 0;
            var dangDung = new HashSet<int>();
            foreach (var a in x.Clone)
            {
                var v = ViecCua(a);
                if (v != null) dangDung.Add(v.DungX);
            }
            var ung = new List<TuiAcc>();
            foreach (var acc in _accTheoTen.Keys)
            {
                if (!LaCloneKho(acc) || x.Clone.Exists(a => CungAcc(a, acc))) continue;
                if (KeHang.KeCuaAcc(acc, Cfg) == KeHang.RAC) continue;
                // Dang giu hang cho lenh rut -> de danh cho lenh (review 17/09: keo mat clone, lenh qua han bi huy).
                if (Hang.KhoaDangGiuTren(acc).Count > 0) continue;
                DateTime den;
                if (_xaTranh.TryGetValue(acc, out den) && DateTime.Now < den) continue;
                if (_nghiNhanDon.TryGetValue(acc, out den) && DateTime.Now < den) continue;
                var c = LayClient(acc);
                if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || CoViec(acc) || VuaVaoGame(acc)) continue;
                var m = c.GameState.CurrentMap;
                if (c.GameState.IsChangingMap || m == null || m.MapId != Cfg.Map) continue;
                var t = So.Lay(acc);
                if (t == null || !t.Online) continue;
                if (Math.Min(t.TuiTrong, DemTuiTrong(c)) - O_CHUA_TUI < XA_TUI_TOI_THIEU) continue;
                ung.Add(t);
            }
            int them = 0;
            // Tui trong nhieu nhat truoc (nhan duoc nhieu luot), roi ruong trong (cat vao duoc nhieu).
            foreach (var t in ung.OrderByDescending(a => a.TuiTrong).ThenByDescending(a => a.RuongTrong))
            {
                if (x.Clone.Count >= XA_SO_CLONE) break;
                var v = new Viec
                {
                    Loai = LoaiViec.DoiNhan, Acc = t.Acc, TuBotAcc = x.Leader, MucDich = XA, HetHan = HanCloneXa(x, DateTime.Now),
                };
                if (Cfg.LeaderX != 0 || Cfg.LeaderY != 0)
                {
                    v.DungX = (short)ChoDungXa(dangDung);
                    v.DungY = Cfg.LeaderY;
                }
                if (!GiaoViec(v)) continue;
                dangDung.Add(v.DungX);
                x.Clone.Add(t.Acc);
                them++;
            }
            return them;
        }

        private void XuLyXa(DateTime now)
        {
            var x = _xa;
            if (x == null) { _moTaXa = ""; return; }
            if (!Cfg.BatNap) { DongXa("tắt nhận đồ", now); return; }
            if (!Cfg.BatDonKho) { DongXa("tắt dọn kho", now); return; }
            if (_khoDay) { DongXa("kho đầy", now); return; }
            if (!CungAcc(x.Leader, _leaderAcc)) { DongXa("Leader đổi", now); return; }
            var L = LeaderClient;
            DonCloneXa(x);
            bool ranh = x.ViecLeader == null && (L == null || !L.DangGiaoDich);
            if (now > x.Den && ranh) { DongXa(XA_PHUT + " phút không nạp thêm", now); return; }
            // Leader dang ban (Chu kho dang giao dich co the toi 2 phut): chua tinh het gio, clone dung canh khong het han.
            if (!ranh && x.Den < now.AddMinutes(1)) x.Den = now.AddMinutes(1);

            // Chu kho bao xong / roi khu chinh: chuyen not phan con trong tui Leader roi dong. Co mat = bot nao cung duoc
            // (Leader vua vao lai khu chua thay nguoi dung san), hoac vua nap / moi trong 60 giay (review 17/09).
            bool coChu = (L != null && CoChuKhoTrongKhu(L)) || TimNguoi(x.ChuKho, Cfg.KhuChinh) != null
                         || (now - x.NapLuc).TotalSeconds < XA_VET_GIAY;
            if (coChu) x.VangTu = DateTime.MinValue;
            else if (x.VangTu == DateTime.MinValue) x.VangTu = now;
            string xong = null;
            DateTime vetTu = DateTime.MinValue;
            if (x.XongLuc != DateTime.MinValue) { xong = "Chủ kho báo xong"; vetTu = x.XongLuc; }
            else if (!coChu && (now - x.VangTu).TotalSeconds >= XA_VET_GIAY) { xong = "Chủ kho rời khu chính"; vetTu = x.VangTu.AddSeconds(XA_VET_GIAY); }
            if (xong != null && ranh
                && (x.Clone.Count == 0 || MonChuyenDuoc(L, 1).Count == 0 || (now - vetTu).TotalSeconds > XA_VET_GIAY))
            {
                DongXa(xong, now);
                return;
            }

            // Clone gan het cho -> roi di cat ruong, clone khac vao thay.
            foreach (var a in x.Clone)
            {
                if (x.ViecLeader != null && CungAcc(a, x.CloneNhan)) continue;
                var v = ViecCua(a);
                if (v == null || v.BiHuy || v.MucDich != XA) continue;
                v.HetHan = HanCloneXa(x, now);
                var c = LayClient(a);
                if (c == null || c.DangGiaoDich) continue;
                if (DemTuiTrong(c) - O_CHUA_TUI < XA_TUI_TOI_THIEU) v.Huy("tui gan day - di cat ruong");
                else if (!OKhuChinh(c) && (now - v.TaoLuc).TotalMilliseconds > XA_TOI_KHU_MS)
                {
                    // Khu chinh day / doi khu hong: bo, lay clone khac (review 17/09).
                    v.Huy("khong vao duoc khu chinh");
                    _xaTranh[a] = now.AddMinutes(10);
                }
            }
            if (xong == null) BoSungXa(x);
            ChuyenTiep(x, L, now);
            CapNhatMoTaXa(x);
        }

        /// <summary>
        /// D86: Leader ranh + trong tui con mon + co clone dung canh san sang -> giao mot luot (toi da 12 o) cho clone
        /// vao truoc (day truoc, roi truoc - clone sau con cho khi clone moi dang toi).
        /// </summary>
        private void ChuyenTiep(PhienXa x, NsoClient L, DateTime now)
        {
            if (x.ViecLeader != null || now < x.NghiDen || _don != null) return;
            if (L == null || !OKhuChinh(L) || L.DangGiaoDich || CoViec(_leaderAcc) || VuaVaoGame(_leaderAcc)) return;
            // Chu kho vua nhan `nap` (giu cua): de Leader ranh cho ho.
            lock (_lk) if (_giuCuaCho != null && now < _giuCuaDen) return;
            string clone = null;
            int cho = 0;
            foreach (var a in x.Clone)
            {
                var v = ViecCua(a);
                if (v == null || v.BiHuy || v.MucDich != XA) continue;
                var c = LayClient(a);
                if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || !OKhuChinh(c) || VuaVaoGame(a)) continue;
                // Vua toi khu / dang di toi cho dung: Leader thay toa do cu -> moi hong (M24). Clone cho 2,5 giay moi di.
                if ((DateTime.UtcNow - c.VaoKhuLucUtc).TotalMilliseconds < XA_CHO_SAU_VAO_KHU_MS) continue;
                var mc = c.GameState.MyChar;
                if (mc == null) continue;
                if ((v.DungX != 0 || v.DungY != 0) && Math.Abs(mc.Cx - v.DungX) > 20 && (now - v.TaoLuc).TotalSeconds < 15) continue;
                // Leader phai thay clone (mode Leader lai sat theo toa do NO thay).
                int id = mc.CharId;
                if (TimTrongKhu(L, p => p.CharId == id) == null)
                {
                    DateTime tu;
                    if (!x.KhongThayTu.TryGetValue(a, out tu)) x.KhongThayTu[a] = now;
                    else if ((now - tu).TotalSeconds > XA_KHONG_THAY_GIAY)
                    {
                        x.KhongThayTu.Remove(a);
                        v.Huy("Leader khong thay clone");
                        _xaTranh[a] = now.AddMinutes(2);
                        Ghi(a, "Xa", "Leader khong thay " + a + " sau " + XA_KHONG_THAY_GIAY + "s (Leader vua vao lai game?) - doi clone khac");
                    }
                    continue;
                }
                x.KhongThayTu.Remove(a);
                int n = DemTuiTrong(c) - O_CHUA_TUI;
                if (n < 1) continue;
                clone = a;
                cho = n;
                break;
            }
            if (clone == null) return;
            var dong = MonChuyenDuoc(L, Math.Min(Controller.TradeHandler.MAX_MON, cho));
            if (dong.Count == 0) return;
            var vl = new Viec
            {
                Loai = LoaiViec.GiaoMon, Acc = _leaderAcc, NguoiNhan = TenNhanVat(clone), NguoiNhanLaBot = true,
                MucDich = XA, ChiTui = true, HetHan = now.AddMinutes(2),
            };
            vl.Dong.AddRange(dong);
            if (!GiaoViec(vl)) return;
            x.ViecLeader = vl;
            x.CloneNhan = clone;
        }

        /// <summary>
        /// D86: mon Leader chuyen tiep duoc - trong TUI (tui that, khong doi so kho), khong khoa, khong giu cho lenh,
        /// khong thuoc ke Rac (clone dung canh khong phai ke Rac - de don kho thuong dua ve dung ke), khong qua
        /// MAX_SO_LUONG (luot chuyen khong tach chong - M30; don kho thuong tach). Toi da <paramref name="n"/> o, theo thu tu o.
        /// </summary>
        private List<DongGiao> MonChuyenDuoc(NsoClient L, int n)
        {
            var r = new List<DongGiao>();
            var mc = L != null ? L.GameState.MyChar : null;
            var bag = mc != null ? mc.BagItems : null;
            if (bag == null || n <= 0) return r;
            var giu = Hang.KhoaDangGiuTren(_leaderAcc);
            var gop = new Dictionary<KhoaMon, int>();
            var thuTu = new List<KhoaMon>();
            int dem = 0;
            foreach (var it in bag.ToArray())
            {
                if (dem >= n) break;
                if (it == null || it.IsEmpty || it.IsLock || it.Quantity > Controller.TradeHandler.MAX_SO_LUONG) continue;
                var k = KhoaMon.Tu(it);
                if (giu.Contains(k) || NhomCua(k.Tpl) == KeHang.RAC) continue;
                int s;
                if (!gop.TryGetValue(k, out s)) thuTu.Add(k);
                gop[k] = s + Math.Max(1, (int)it.Quantity);
                dem++;
            }
            foreach (var k in thuTu) r.Add(new DongGiao { Khoa = k, SoLuong = gop[k] });
            return r;
        }

        private void SauViecChuyen(PhienXa x, Viec v, BaoCaoViec bc, DateTime now)
        {
            string clone = x.CloneNhan;
            x.ViecLeader = null;
            x.CloneNhan = null;
            if (bc.Xong)
            {
                x.HongLienTiep = 0;
                x.Den = now.AddMinutes(XA_PHUT);
                return;
            }
            // Dong xa / nhuong lenh rut / Leader ban nguoi nap qua lau / tui Leader vua doi (chong gop khi nguoi choi nap
            // them) / bi chen: khong phai loi cua ai, nhip sau lap lai luot moi theo tui that (review 17/09).
            // "BI_HUY" cua PHIEN (doi phuong / server huy - vd M24) KHONG trung tinh: chi tin v.BiHuy (bo dieu phoi huy).
            if (v.BiHuy || bc.MaLoi == MaLoiViec.HET_HAN
                || bc.MaLoi == MaLoiViec.KHONG_CO_MON || bc.MaLoi == MaLoiGiaoDich.BI_CHEN)
            {
                x.NghiDen = now.AddSeconds(1);
                return;
            }
            x.HongLienTiep++;
            x.NghiDen = now.AddSeconds(3);
            Ghi(_leaderAcc, "Xa", "Chuyen sang " + clone + " hong [" + bc.MaLoi + "] " + bc.LyDo + " (lan " + x.HongLienTiep + ")");
            bool loiLeader = bc.MaLoi == MaLoiGiaoDich.MAT_KET_NOI;
            if (!loiLeader && clone != null)
            {
                // Phia clone: khong thay / khong nhan loi / thieu o / qua xa -> doi clone khac.
                var vc = ViecCua(clone);
                if (vc != null && vc.MucDich == XA && !vc.BiHuy) vc.Huy("Leader chuyen do hong: " + bc.MaLoi);
                _xaTranh[clone] = now.AddMinutes(10);
            }
            if (x.HongLienTiep >= 3) DongXa("chuyển đồ hỏng 3 lần liền", now);
        }

        private void SauChuyenXa(Viec v, int soMon)
        {
            var x = _xa;
            if (x == null || x.ViecLeader != v) return;
            x.Luot++;
            x.Mon += soMon;
        }

        private void CapNhatMoTaXa(PhienXa x)
        {
            _moTaXa = string.Format("Xả nhanh: Leader → {0} · đã chuyển {1} món",
                x.Clone.Count == 0 ? "(chờ clone rảnh)" : string.Join(", ", x.Clone.Select(TenNhanVat).ToArray()), x.Mon);
        }

        private void DongXa(string lyDo, DateTime now)
        {
            var x = _xa;
            if (x == null) return;
            _xa = null;
            _moTaXa = "";
            // Luot chuyen dang do: viec Leader bi huy (mode chi xet huy SAU phien -> phien dang chay van xong, chua moi thi
            // thoi). Clone dang nhan thi giu them 30 giay (huy ngay thi no tu choi loi moi Leader vua gui, Leader cho 20 giay).
            string dangNhan = x.ViecLeader != null ? x.CloneNhan : null;
            if (x.ViecLeader != null) x.ViecLeader.Huy("xong xa nhanh");
            foreach (var a in x.Clone)
            {
                var v = ViecCua(a);
                if (v == null || v.MucDich != XA || v.BiHuy) continue;
                if (CungAcc(a, dangNhan)) v.HetHan = now.AddSeconds(30);
                else v.Huy("xong xa nhanh");
            }
            Ghi("-", "Xa", "Dong xa nhanh (" + ChuVan.BoDau(lyDo) + "): " + x.Luot + " luot, " + x.Mon + " mon");
            DeDoc("XẢ", "Xong đợt xả (" + lyDo + "): "
                + (x.Luot > 0 ? "Leader đã chuyển " + x.Mon + " món sang clone qua " + x.Luot + " lượt" : "chưa chuyển món nào"));
            if (x.TuLenh || x.XongLuc != DateTime.MinValue)
                BaoRieng(x.ChuKho, x.Luot > 0 ? "Xa xong (" + ChuVan.BoDau(lyDo) + "): Leader da chuyen " + x.Mon + " mon sang clone"
                                              : "Da dong xa nhanh (" + ChuVan.BoDau(lyDo) + ")", null, 0);
        }

        // =====================================================================
        // BAO TRI CLONE: doc ruong / cat ruong o khu phu
        // =====================================================================

        /// <summary>Acc con can doc lai ruong khong (viec khac da mo ruong trong phien nay thi thoi).</summary>
        private bool CanDocRuong(string acc, NsoClient c)
        {
            if (!_canDocRuong.Contains(acc)) return false;
            var mc = c != null ? c.GameState.MyChar : null;
            if (mc != null && mc.BoxItems != null && !_truocNha.ContainsKey(acc))
            {
                _canDocRuong.Remove(acc);
                return false;
            }
            return true;
        }

        private void BaoTriClone(DateTime now)
        {
            if (!Cfg.BatCatRuong || Cfg.KhuChinh < 0) return;
            var d = _don;
            foreach (var acc in _accTheoTen.Keys)
            {
                if (LaLeader(acc) || Cfg.DaNha(acc) || LaAccKhacMayChu(acc) || _choNha.ContainsKey(acc)) continue;
                if (d != null && CungAcc(d.Clone, acc)) continue;
                var c = LayClient(acc);
                if (c == null || c.State != ClientState.InGame || c.DangGiaoDich || CoViec(acc)) continue;
                var st = c.GameState;
                if (st.IsChangingMap || st.CurrentMap == null || st.CurrentMap.MapId != Cfg.Map) continue;
                // Chi bao tri khi clone da ve dung khu cho (khong mo ruong o khu chinh chiem cho nguoi nhan).
                int khuCho = KhuDungCho(c);
                if (khuCho >= 0 && st.CurrentMap.ZoneId != khuCho) continue;
                DateTime nghi;
                if (_nghiBaoTri.TryGetValue(acc, out nghi) && now < nghi) continue;
                var t = So.Lay(acc);
                if (t == null || !t.Online) continue;

                if (t.SoORuong < 0 || CanDocRuong(acc, c))
                {
                    // Co tu xoa trong CanDocRuong khi ruong da ve (viec bi nhuong / hong thi van con co).
                    GiaoViec(new Viec { Loai = LoaiViec.DocRuong, Acc = acc, HetHan = now.AddMinutes(3) });
                    continue;
                }
                bool can = _canCat.Contains(acc) || t.TuiTrong < Cfg.NguongNhan;
                _canCat.Remove(acc);
                if (!can) continue;
                var giu = Hang.KhoaDangGiuTren(acc);
                if (MonCoTheCat(t, giu) == 0 || !RuongConCho(t, giu)) continue;
                var v = new Viec { Loai = LoaiViec.CatRuong, Acc = acc, HetHan = now.AddMinutes(5) };
                foreach (var k in giu) v.KhongCat.Add(k);
                GiaoViec(v);
            }
        }

        // =====================================================================
        // THEO DOI / KE RAC / BAO CAO NGAY (D36, D43)
        // =====================================================================

        private void TheoDoiMon()
        {
            if (!Cfg.BatTheoDoi) return;
            var ds = Cfg.TheoDoi;
            if (ds.Count == 0) return;
            var tong = new Dictionary<short, int>();
            foreach (var d in _bangTon)
            {
                if (d.Khoa.Khoa) continue;
                int n;
                tong.TryGetValue(d.Khoa.Tpl, out n);
                tong[d.Khoa.Tpl] = n + d.KhaDung;
            }
            foreach (var m in ds)
            {
                int co;
                tong.TryGetValue(m.Tpl, out co);
                string k = m.Tpl + "|" + (m.ChuKho ?? "").ToLowerInvariant();
                int truoc;
                lock (_theoDoiTruoc)
                {
                    if (!_theoDoiTruoc.TryGetValue(k, out truoc)) { _theoDoiTruoc[k] = co; continue; }
                    _theoDoiTruoc[k] = co;
                }
                string ten = m.Tpl + " " + BangMon.Ten(m.Tpl);
                string tin = null;
                if (m.Nguong >= 0)
                {
                    if (truoc >= m.Nguong && co < m.Nguong) tin = "Theo doi " + ten + ": con " + co + " (duoi " + m.Nguong + ")";
                    else if (truoc < m.Nguong && co >= m.Nguong) tin = "Theo doi " + ten + ": da ve " + co + " (tu " + m.Nguong + " tro len)";
                }
                else if (co > truoc) tin = "Theo doi " + ten + ": vua ve kho, nay co " + co + " (+" + (co - truoc) + ")";
                if (tin != null) BaoSuKien(m.ChuKho, null, tin, true, "theodoi|" + k, 600);
            }
        }

        private void BaoRac()
        {
            var r = _sucChuaRac;
            if (r.Tong <= 0) return;
            int pt = r.Dung * 100 / r.Tong;
            if (pt < Cfg.RacBaoNguong) return;
            var ten = new List<string>();
            foreach (var acc in _accTheoTen.Keys)
                if (LaCloneKho(acc) && KeHang.KeCuaAcc(acc, Cfg) == KeHang.RAC) ten.Add(TenNhanVat(acc));
            BaoChuKho("Ke Rac day " + pt + "%, vao don " + string.Join(", ", ten.ToArray()), true, "racday", 3600);
        }

        private void BaoCaoNgay(DateTime now)
        {
            if (!Cfg.BatBaoCao) return;
            int g, p;
            if (!Cfg.LayGioBaoCao(out g, out p)) return;
            string hom = now.ToString("yyyy-MM-dd");
            if (_baoCaoDaGui == hom) return;
            var moc = now.Date.AddHours(g).AddMinutes(p);
            if (now < moc) return;
            _baoCaoDaGui = hom;
            if ((now - moc).TotalMinutes > 30) return;   // mo app muon qua gio bao - bo hom nay
            var tk = ThongKe;
            var sc = _sucChua;
            var rac = _sucChuaRac;
            string tin = string.Format("Hom nay: nhap {0} mon ({1} luot), xuat {2} ({3} lenh), don {4} mon. Kho {5}{6}/{7}{8}, xu {9}",
                tk.NapMon, tk.NapLuot, tk.XuatMon, tk.XuatLenh, tk.DonMon, sc.ChuaDu ? "~" : "", sc.Dung, sc.Tong,
                rac.Tong > 0 ? ", ke Rac " + rac.Dung + "/" + rac.Tong : "", XuGon(TongXu));
            Ghi("-", "BaoCao", tin);
            foreach (var ck in Cfg.ChuKho) BaoRieng(ck, tin, null, 0);
        }
    }
}
