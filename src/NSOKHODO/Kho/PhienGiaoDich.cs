using System;
using System.Collections.Generic;
using NSOKHODO.Client;
using NSOKHODO.Models;

namespace NSOKHODO.Kho
{
    public enum VaiGiaoDich { Nhan, Giao }

    public static class MaLoiGiaoDich
    {
        public const string HET_GIO_CHO_NHAN_LOI = "HET_GIO_CHO_NHAN_LOI";
        public const string HET_GIO = "HET_GIO";
        public const string SAI_DOI_PHUONG = "SAI_DOI_PHUONG";
        public const string THIEU_O = "THIEU_O";                  // D39: bi dong ngay sau khi ta khoa (vai giao)
        public const string DOI_PHUONG_DAT_QUA = "DOI_PHUONG_DAT_QUA"; // bi dong khi doi phuong khoa (vai nhan)
        public const string TU_CHOI_HANG = "TU_CHOI_HANG";        // kiem hang doi phuong khong dat
        public const string BI_HUY = "BI_HUY";
        public const string BI_CHEN = "BI_CHEN";                  // Chu kho `nap` chen ngang / lenh bi huy
        public const string KHONG_CO_MON = "KHONG_CO_MON";
        public const string MAT_KET_NOI = "MAT_KET_NOI";
        /// <summary>Server bao "Khoảng cách quá xa không thể giao dịch" (test song 16/09) - lai gan roi moi lai.</summary>
        public const string QUA_XA = "QUA_XA";

        // ---- Cau chu server (test song / test tay 16/09, da bo dau + chu thuong qua ChuVan.ChuanHoa) ----
        /// <summary>Nguoi nhan vao game chua toi ~1 phut: server tu choi ngay, KHONG chuyen loi moi ("X do not accept.").</summary>
        public const string TIN_CHUA_NHAN_MOI = "do not accept";
        public const string TIN_QUA_XA = "khoang cach qua xa";
        /// <summary>Anh user gui 16/09: "Đối phương không đủ ô trống để chứa vật phẩm giao dịch".</summary>
        public const string TIN_THIEU_O = "khong du o trong";
    }

    public sealed class KetQuaGiaoDich
    {
        public bool ThanhCong;
        public string MaLoi;
        public string LyDo = "";
        public VaiGiaoDich Vai;
        public string DoiPhuong;
        public int DoiPhuongId;
        public DateTime BatDau;
        public DateTime KetThuc;
        public MonGiaoDich[] MonNhan = new MonGiaoDich[0];
        public int XuNhan;
        public List<KeyValuePair<byte, Item>> MonDua = new List<KeyValuePair<byte, Item>>();
        public int XuDua;
        public int XuSauXong;
        public string[] TinServer = new string[0];

        public string MoTaMonNhan()
        {
            var p = new List<string>();
            foreach (var m in MonNhan) p.Add(m.ToString());
            return string.Join(" ", p.ToArray());
        }

        public string MoTaMonDua()
        {
            var p = new List<string>();
            foreach (var kv in MonDua)
                p.Add(string.Format("[{0}]{1} x{2}{3}", kv.Value.TemplateId, kv.Value.Upgrade > 0 ? " +" + kv.Value.Upgrade : "",
                    kv.Value.Quantity, kv.Value.IsExpires ? " (han)" : ""));
            return string.Join(" ", p.ToArray());
        }
    }

    /// <summary>
    /// MOT PHIEN GIAO DICH — may trang thai hai vai (SPEC §5, §7, §11; GIAO_DICH.md §4).
    ///
    /// <code>
    /// (giao) MOI ──31 s──► MOI LAI … ─┐
    /// (nhan) 44 ──────────────────────┴► CHO_37 → KHOA(45) → CHO_45_DP → KIEM → CHO_TRUOC_46(1,5 s)
    ///                                     → DA_46 → CHO_58 → XONG (gui 57 don phien)
    /// bat ky buoc nao: 57 den / qua han / sai doi phuong / bi chen → HUY
    /// </code>
    ///
    /// <para><b>Khong chan luong:</b> moi lan <see cref="Tick"/> lam dung mot buoc roi tra ve. Mode kho
    /// goi lai ~100 ms/lan. Luong nhan goi ghi <see cref="TradeState"/>, phien chi DOC anh chup.</para>
    ///
    /// <para><b>Khac NSOTRUNGDUC Class_aj co chu dich:</b> vai giao KHONG gui 46 khi chua thay 45 cua
    /// doi phuong (Class_aj gui mu sau 1 giay).</para>
    /// </summary>
    public sealed class PhienGiaoDich
    {
        private enum Buoc { Moi, Cho37, DaKhoa, ChoTruoc46, Da46, Het }

        private const int CHO_37_SAU_KHI_NHAN_MS = 15000;
        private const int CHO_TRUOC_46_MS = 1500;          // Class_ap cua NSOTRUNGDUC - da chay that
        private const int DONG_NGAY_MS = 3000;             // bi dong trong khoang nay sau khi ta khoa = D39

        private readonly NsoClient _c;
        private readonly VaiGiaoDich _vai;
        private readonly int _dpId;
        private readonly string _tenMongDoi;
        private readonly int _choToiDaMs;
        private readonly int _moiLaiMs;
        private readonly int _xu;
        private readonly Func<byte[]> _chonO;
        private readonly Func<string, string> _kiemTen;
        private readonly Func<int, MonGiaoDich[], string> _kiemHang;

        private Buoc _buoc;
        private DateTime _batDau, _hanBuoc, _moiLaiLuc, _khoaLuc, _gui46Luc;
        private byte[] _oDaDua = new byte[0];
        private volatile string _yeuCauHuy;

        public KetQuaGiaoDich KetQua { get; private set; }
        public bool KetThuc { get { return _buoc == Buoc.Het; } }
        public VaiGiaoDich Vai { get { return _vai; } }
        public int DoiPhuongId { get { return _dpId; } }
        public string TenMongDoi { get { return _tenMongDoi; } }
        public string TenDoiPhuong { get; private set; }

        /// <summary>Mo ta buoc hien tai cho cot "Phien GD" tren luoi.</summary>
        public string MoTa
        {
            get
            {
                int giay = (int)Math.Max(0, (DateTime.UtcNow - _batDau).TotalSeconds);
                string ten = TenDoiPhuong ?? _tenMongDoi ?? ("#" + _dpId);
                switch (_buoc)
                {
                    case Buoc.Moi: return ten + " · Mời · " + giay + "s";
                    case Buoc.Cho37: return ten + " · Chờ mở khung · " + giay + "s";
                    case Buoc.DaKhoa: return ten + " · Chờ khoá · " + giay + "s";
                    case Buoc.ChoTruoc46: return ten + " · Sắp đồng ý";
                    case Buoc.Da46: return ten + " · Chờ xong · " + giay + "s";
                    default: return ten + " · Xong";
                }
            }
        }

        /// <param name="choToiDaMs">Han cho moi buoc (nguoi 120 s / nguoi la 45 s / bot 20 s).</param>
        /// <param name="moiLaiMs">Vai giao: khoang cach giua hai lan moi (31 s — test tay T4).</param>
        /// <param name="chonO">Vai giao: chon o tui ngay truoc khi khoa (toi da 12). Null o vai nhan.</param>
        /// <param name="kiemTen">Nhan ten doi phuong tu goi 37; tra null = dung nguoi, khac null = ly do huy.</param>
        /// <param name="kiemHang">Kiem hang/xu doi phuong dat vao; tra null = dong y, khac null = ly do tu choi.</param>
        public PhienGiaoDich(NsoClient c, VaiGiaoDich vai, int doiPhuongId, string tenMongDoi,
                             int choToiDaMs, int moiLaiMs, int xu, Func<byte[]> chonO,
                             Func<string, string> kiemTen, Func<int, MonGiaoDich[], string> kiemHang)
        {
            _c = c;
            _vai = vai;
            _dpId = doiPhuongId;
            _tenMongDoi = tenMongDoi;
            _choToiDaMs = Math.Max(5000, choToiDaMs);
            _moiLaiMs = Math.Max(5000, moiLaiMs);
            _xu = Math.Max(0, xu);
            _chonO = chonO;
            _kiemTen = kiemTen;
            _kiemHang = kiemHang;
            _buoc = Buoc.Moi;
        }

        /// <summary>Yeu cau huy tu ben ngoai (Chu kho `nap`, lenh bi huy). An toan goi tu moi luong.</summary>
        public void YeuCauHuy(string lyDo) { _yeuCauHuy = lyDo ?? "bi huy"; }

        /// <summary>Lam mot buoc. Tra ve so ms nen ngu truoc lan goi sau.</summary>
        public int Tick()
        {
            if (_buoc == Buoc.Het) return 0;
            var now = DateTime.UtcNow;

            if (_c.State != ClientState.InGame)
            {
                KetThucPhien(false, MaLoiGiaoDich.MAT_KET_NOI, "mat ket noi / chet giua phien", false);
                return 0;
            }

            if (_buoc == Buoc.Moi) { BatDau(now); return 100; }

            var s = _c.Trade.Chup();
            string huy = _yeuCauHuy;
            if (huy != null)
            {
                KetThucPhien(false, MaLoiGiaoDich.BI_CHEN, huy, s.DangMo || _buoc != Buoc.Cho37);
                return 0;
            }

            switch (_buoc)
            {
                case Buoc.Cho37: BuocCho37(s, now); break;
                case Buoc.DaKhoa: BuocDaKhoa(s, now); break;
                case Buoc.ChoTruoc46: BuocChoTruoc46(s, now); break;
                case Buoc.Da46: BuocDa46(s, now); break;
            }
            return 100;
        }

        private void BatDau(DateTime now)
        {
            _batDau = now;
            _c.DangGiaoDich = true;
            _c.Trade.BatDauPhienMoi();
            if (_vai == VaiGiaoDich.Nhan)
            {
                _c.TradeSvc.SendAcceptInvite(_dpId);
                _hanBuoc = now.AddMilliseconds(CHO_37_SAU_KHI_NHAN_MS);
                Log("Nhan loi moi cua id " + _dpId + (_tenMongDoi != null ? " (" + _tenMongDoi + ")" : ""));
            }
            else
            {
                _c.TradeSvc.SendInvite(_dpId);
                _moiLuc = now;
                _moiLaiLuc = now.AddMilliseconds(_moiLaiMs);
                _hanBuoc = now.AddMilliseconds(_choToiDaMs);
                Log("Moi giao dich " + (_tenMongDoi ?? "") + " (id " + _dpId + ")");
            }
            _buoc = Buoc.Cho37;
        }

        private DateTime _moiLuc;
        private bool _daXetTuChoi;
        /// <summary>Server tu choi (khong chuyen loi moi) thi khong bi khoa 31 giay - test song 16/09: moi lai sau 17 giay van co tra loi.</summary>
        private const int MOI_LAI_SAU_TU_CHOI_MS = 10000;

        /// <summary>Vai giao: doc tin chu server tu lan moi gan nhat. true = da ket thuc phien.</summary>
        private bool XetTinSauMoi(DateTime now)
        {
            if (_vai != VaiGiaoDich.Giao || _moiLuc == DateTime.MinValue) return false;
            string tin = ChuVan.ChuanHoa(string.Join(" | ", _c.Trade.TinServerTu(_moiLuc)));
            if (tin.Length == 0) return false;
            if (tin.Contains(MaLoiGiaoDich.TIN_QUA_XA))
            {
                KetThucPhien(false, MaLoiGiaoDich.QUA_XA, "server bao khoang cach qua xa", false);
                return true;
            }
            if (!_daXetTuChoi && tin.Contains(MaLoiGiaoDich.TIN_CHUA_NHAN_MOI))
            {
                _daXetTuChoi = true;
                var som = now.AddMilliseconds(MOI_LAI_SAU_TU_CHOI_MS);
                if (_moiLaiLuc > som) _moiLaiLuc = som;
                Log("Server chua cho moi " + (_tenMongDoi ?? ("id " + _dpId)) + " (moi vao game?) - moi lai sau "
                    + MOI_LAI_SAU_TU_CHOI_MS / 1000 + "s");
            }
            return false;
        }

        private void BuocCho37(TradeSnapshot s, DateTime now)
        {
            if (s.DangMo)
            {
                TenDoiPhuong = s.DoiPhuong;
                string loiTen = null;
                if (_tenMongDoi != null && !ChuVan.CungTen(_tenMongDoi, s.DoiPhuong))
                    loiTen = "khung mo voi " + s.DoiPhuong + " (cho " + _tenMongDoi + ")";
                else if (_kiemTen != null)
                    loiTen = _kiemTen(s.DoiPhuong);
                if (loiTen != null)
                {
                    KetThucPhien(false, MaLoiGiaoDich.SAI_DOI_PHUONG, loiTen, true);
                    return;
                }

                byte[] o = new byte[0];
                if (_vai == VaiGiaoDich.Giao)
                {
                    o = _chonO != null ? (_chonO() ?? new byte[0]) : new byte[0];
                    if (o.Length > Controller.TradeHandler.MAX_MON)
                    {
                        var cat = new byte[Controller.TradeHandler.MAX_MON];
                        Array.Copy(o, cat, cat.Length);
                        o = cat;
                    }
                    if (o.Length == 0 && _xu == 0)
                    {
                        KetThucPhien(false, MaLoiGiaoDich.KHONG_CO_MON, "khong con mon nao de dat vao khung", true);
                        return;
                    }
                    ChupMonDua(o);
                }
                _oDaDua = o;
                _c.TradeSvc.SendLock(_vai == VaiGiaoDich.Giao ? _xu : 0, o);
                _khoaLuc = now;
                _hanBuoc = now.AddMilliseconds(_choToiDaMs);
                _buoc = Buoc.DaKhoa;
                Log(string.Format("Khung mo voi {0} -> khoa {1} o{2}", s.DoiPhuong, o.Length,
                    _vai == VaiGiaoDich.Giao && _xu > 0 ? " + " + _xu + " xu" : ""));
                return;
            }

            if (s.BiHuy)
            {
                KetThucPhien(false, MaLoiGiaoDich.BI_HUY, "bi huy truoc khi mo khung", false);
                return;
            }

            if (XetTinSauMoi(now)) return;

            if (now >= _hanBuoc)
            {
                string lyDo = _vai == VaiGiaoDich.Giao ? "doi phuong khong nhan loi moi" : "server khong mo khung sau khi nhan loi";
                if (_daXetTuChoi) lyDo = "server khong cho moi (" + MaLoiGiaoDich.TIN_CHUA_NHAN_MOI + " - nguoi nhan moi vao game?)";
                KetThucPhien(false, _vai == VaiGiaoDich.Giao ? MaLoiGiaoDich.HET_GIO_CHO_NHAN_LOI : MaLoiGiaoDich.HET_GIO,
                    lyDo, false);
                return;
            }

            if (_vai == VaiGiaoDich.Giao && now >= _moiLaiLuc)
            {
                _c.TradeSvc.SendInvite(_dpId);
                _moiLuc = now;
                _daXetTuChoi = false;
                _moiLaiLuc = now.AddMilliseconds(_moiLaiMs);
                Log("Moi lai " + (_tenMongDoi ?? ("id " + _dpId)));
            }
        }

        private void BuocDaKhoa(TradeSnapshot s, DateTime now)
        {
            if (s.BiHuy)
            {
                bool dongNgay = (s.HuyLuc - _khoaLuc).TotalMilliseconds <= DONG_NGAY_MS && !s.DoiPhuongDaKhoa;
                string tin = string.Join(" | ", _c.Trade.TinServerTu(_batDau));
                string chuan = ChuVan.ChuanHoa(tin);
                bool noiHanhTrang = chuan.Contains("hanh trang") || chuan.Contains(MaLoiGiaoDich.TIN_THIEU_O);
                if (_vai == VaiGiaoDich.Giao && (noiHanhTrang || dongNgay))
                    KetThucPhien(false, MaLoiGiaoDich.THIEU_O,
                        noiHanhTrang ? "nguoi nhan thieu o hanh trang" : "bi dong ngay sau khi khoa (nghi nguoi nhan thieu o)", false);
                else if (_vai == VaiGiaoDich.Nhan && !s.DoiPhuongDaKhoa)
                    KetThucPhien(false, MaLoiGiaoDich.DOI_PHUONG_DAT_QUA,
                        noiHanhTrang ? "doi phuong dat qua so o trong" : "bi dong truoc khi doi phuong khoa", false);
                else
                    HuyVi("doi phuong huy");
                return;
            }

            if (s.DoiPhuongDaKhoa)
            {
                if (s.DoiPhuongMon != null && s.DoiPhuongMon.Length > 0 || s.DoiPhuongXu > 0)
                    Log(string.Format("Doi phuong khoa: {0} mon{1}", s.DoiPhuongMon.Length,
                        s.DoiPhuongXu > 0 ? " + " + s.DoiPhuongXu + " xu" : ""));
                string loi = _kiemHang != null ? _kiemHang(s.DoiPhuongXu, s.DoiPhuongMon) : null;
                if (loi != null)
                {
                    KetThucPhien(false, MaLoiGiaoDich.TU_CHOI_HANG, loi, true);
                    return;
                }
                var moc = s.DoiPhuongKhoaLuc > _khoaLuc ? s.DoiPhuongKhoaLuc : _khoaLuc;
                if (moc < now.AddSeconds(-30)) moc = now;
                _gui46Luc = moc.AddMilliseconds(CHO_TRUOC_46_MS);
                _buoc = Buoc.ChoTruoc46;
                return;
            }

            if (now >= _hanBuoc)
                KetThucPhien(false, MaLoiGiaoDich.HET_GIO, "doi phuong khong khoa", true);
        }

        /// <summary>
        /// Vai giao: server da noi "thieu o" chua (tu luc bat dau phien). Test song 16/09: nguoi nhan khoa
        /// (rong) TRUOC khi server dong phien -> phien bi xep "doi phuong huy", thua mot lan moi lai.
        /// </summary>
        private bool ServerBaoThieuO()
        {
            if (_vai != VaiGiaoDich.Giao) return false;
            string chuan = ChuVan.ChuanHoa(string.Join(" | ", _c.Trade.TinServerTu(_batDau)));
            return chuan.Contains("hanh trang") || chuan.Contains(MaLoiGiaoDich.TIN_THIEU_O);
        }

        private void HuyVi(string lyDoThuong)
        {
            if (ServerBaoThieuO()) KetThucPhien(false, MaLoiGiaoDich.THIEU_O, "nguoi nhan thieu o hanh trang", false);
            else KetThucPhien(false, MaLoiGiaoDich.BI_HUY, lyDoThuong, false);
        }

        private void BuocChoTruoc46(TradeSnapshot s, DateTime now)
        {
            if (s.BiHuy) { HuyVi("doi phuong huy truoc khi dong y"); return; }
            if (now < _gui46Luc) return;
            _c.TradeSvc.SendAccept();
            _hanBuoc = now.AddMilliseconds(_choToiDaMs);
            _buoc = Buoc.Da46;
            Log("Dong y (46)");
        }

        private void BuocDa46(TradeSnapshot s, DateTime now)
        {
            if (s.Xong)
            {
                KetQua = TaoKetQua(true, null, "", s);
                KetQua.XuSauXong = s.XuSauXong;
                if (_vai == VaiGiaoDich.Giao) XoaODaDua();
                // Client goc cung gui 57 sau 58 (GameScr.cs:3127-3130): don phien de server khong
                // coi ta van dang giao dich.
                try { _c.TradeSvc.SendCancel(); } catch { }
                Log("XONG: nhan " + KetQua.MoTaMonNhan() + (s.DoiPhuongXu > 0 ? " + " + s.DoiPhuongXu + " xu" : "")
                    + (_oDaDua.Length > 0 ? " | dua " + KetQua.MoTaMonDua() : ""));
                Dong();
                return;
            }
            if (s.BiHuy) { HuyVi("doi phuong huy sau khi da dong y"); return; }
            if (now >= _hanBuoc)
                KetThucPhien(false, MaLoiGiaoDich.HET_GIO, "doi phuong khong dong y", true);
        }

        private void KetThucPhien(bool ok, string ma, string lyDo, bool gui57)
        {
            if (_buoc == Buoc.Het) return;
            if (gui57) { try { _c.TradeSvc.SendCancel(); } catch { } }
            KetQua = TaoKetQua(ok, ma, lyDo, _c.Trade.Chup());
            Log("HUY [" + ma + "] " + lyDo + (KetQua.TinServer.Length > 0 ? " | server: " + string.Join(" / ", KetQua.TinServer) : ""));
            Dong();
        }

        private void Dong()
        {
            _buoc = Buoc.Het;
            _c.DangGiaoDich = false;
        }

        private KetQuaGiaoDich TaoKetQua(bool ok, string ma, string lyDo, TradeSnapshot s)
        {
            var k = new KetQuaGiaoDich
            {
                ThanhCong = ok,
                MaLoi = ma,
                LyDo = lyDo ?? "",
                Vai = _vai,
                DoiPhuong = TenDoiPhuong ?? s.DoiPhuong ?? _tenMongDoi,
                DoiPhuongId = _dpId,
                BatDau = _batDau.ToLocalTime(),
                KetThuc = DateTime.Now,
                XuDua = _vai == VaiGiaoDich.Giao ? _xu : 0,
                TinServer = _batDau == DateTime.MinValue ? new string[0] : _c.Trade.TinServerTu(_batDau),
            };
            if (ok)
            {
                k.MonNhan = s.DoiPhuongMon ?? new MonGiaoDich[0];
                k.XuNhan = s.DoiPhuongXu;
            }
            k.MonDua = _monDua;
            if (!ok) k.XuDua = 0;
            return k;
        }

        private List<KeyValuePair<byte, Item>> _monDua = new List<KeyValuePair<byte, Item>>();

        private void ChupMonDua(byte[] o)
        {
            _monDua = new List<KeyValuePair<byte, Item>>();
            var bag = _c.GameState.MyChar != null ? _c.GameState.MyChar.BagItems : null;
            if (bag == null) return;
            foreach (var b in o)
            {
                if (b >= bag.Length || bag[b] == null || bag[b].IsEmpty) continue;
                var it = bag[b];
                _monDua.Add(new KeyValuePair<byte, Item>(b, new Item
                {
                    TemplateId = it.TemplateId, Upgrade = it.Upgrade, IsExpires = it.IsExpires,
                    IsLock = it.IsLock, Quantity = it.Quantity,
                }));
            }
        }

        /// <summary>
        /// Client goc xoa o da dua ngay khi chon mon; goi 58 xoa han (khong co goi xoa o tu server —
        /// SPEC M2 se do lai). Chi xoa o con dung mon da chup, tranh xoa nham neu sub 115 vua ve.
        /// </summary>
        private void XoaODaDua()
        {
            var mc = _c.GameState.MyChar;
            var bag = mc != null ? mc.BagItems : null;
            if (bag == null) return;
            bool doi = false;
            foreach (var kv in _monDua)
            {
                int i = kv.Key;
                if (i >= bag.Length || bag[i] == null || bag[i].IsEmpty) continue;
                if (bag[i].TemplateId != kv.Value.TemplateId || bag[i].Quantity != kv.Value.Quantity) continue;
                bag[i] = new Item();
                doi = true;
            }
            if (doi) _c.GameState.RaiseInventoryChanged();
        }

        private void Log(string s)
        {
            // Chi qua client.Log: log cua client da di vao app.log (MainForm -> NhatKy), ghi them o day
            // la moi dong hai lan.
            _c.Log("[GD " + (_vai == VaiGiaoDich.Nhan ? "nhan" : "giao") + "] " + ChuVan.BoDau(s));
        }
    }
}
