using System;
using System.Collections.Generic;

namespace NSOKHODO.Client
{
    /// <summary>Mot mon doi phuong dat vao khung giao dich (goi 45 chieu nhan).</summary>
    public sealed class MonGiaoDich
    {
        public short TemplateId;
        public byte Upgrade;
        public bool IsExpires;
        public int Quantity;

        public override string ToString()
        {
            return string.Format("[{0}]{1} x{2}{3}", TemplateId,
                Upgrade > 0 ? " +" + Upgrade : "", Quantity, IsExpires ? " (han)" : "");
        }
    }

    /// <summary>Anh chup bat bien cua <see cref="TradeState"/> — luong mode doc cai nay, khong doc field song.</summary>
    public sealed class TradeSnapshot
    {
        public int Seq;
        public bool CoMoiDen;
        public int MoiDenId;
        public DateTime MoiDenLuc;

        public bool DangMo;
        public string DoiPhuong;
        public DateTime MoLuc;

        public bool DoiPhuongDaKhoa;
        public int DoiPhuongXu;
        public MonGiaoDich[] DoiPhuongMon = new MonGiaoDich[0];
        public DateTime DoiPhuongKhoaLuc;

        public bool DoiPhuongDongY;
        public DateTime DoiPhuongDongYLuc;

        public bool Xong;
        public int XuSauXong;
        public DateTime XongLuc;

        public bool BiHuy;
        public DateTime HuyLuc;
    }

    /// <summary>
    /// Trang thai giao dich cua MOT tai khoan. Luong NHAN goi ghi vao (TradeHandler), luong MODE doc
    /// qua <see cref="Chup"/>. Moi truy cap deu qua mot khoa - cac goi 37/45/46/57/58 den sat nhau.
    ///
    /// <para>Song qua ca phien ket noi (NsoClient giu mot doi tuong duy nhat). Ket noi dut giua phien
    /// thi server tu huy giao dich (test tay T6) - <see cref="DatLaiKhiMatKetNoi"/> xoa sach.</para>
    /// </summary>
    public sealed class TradeState
    {
        private readonly object _lock = new object();
        private int _seq;

        private bool _coMoiDen;
        private int _moiDenId;
        private DateTime _moiDenLuc;

        private bool _dangMo;
        private string _doiPhuong;
        private DateTime _moLuc;
        private bool _dpKhoa;
        private int _dpXu;
        private MonGiaoDich[] _dpMon = new MonGiaoDich[0];
        private DateTime _dpKhoaLuc;
        private bool _dpDongY;
        private DateTime _dpDongYLuc;
        private bool _xong;
        private int _xuSauXong;
        private DateTime _xongLuc;
        private bool _huy;
        private DateTime _huyLuc;

        // Tin chu server gui gan day (-24/-25/-26). Server bao loi giao dich bang chu, cau chu chinh
        // xac CHUA biet (SPEC M18) -> giu lai de ghi kem moi lan huy phien.
        private struct TinServer { public DateTime Luc; public string Chu; }
        private readonly Queue<TinServer> _tin = new Queue<TinServer>();
        private const int TIN_TOI_DA = 20;

        public int Seq { get { lock (_lock) return _seq; } }

        // ================= GHI (luong nhan goi) =================

        public void GhiLoiMoi(int inviterId)
        {
            lock (_lock)
            {
                _coMoiDen = true;
                _moiDenId = inviterId;
                _moiDenLuc = DateTime.UtcNow;
                _seq++;
            }
        }

        public void GhiMoKhung(string tenDoiPhuong)
        {
            lock (_lock)
            {
                // 37 = khung MOI. Moi co cua phien cu phai mat, ke ca co huy/xong con sot.
                XoaPhienKhongKhoa();
                _dangMo = true;
                _doiPhuong = tenDoiPhuong ?? "";
                _moLuc = DateTime.UtcNow;
                _seq++;
            }
        }

        public void GhiDoiPhuongKhoa(int xu, MonGiaoDich[] mon)
        {
            lock (_lock)
            {
                _dpKhoa = true;
                _dpXu = xu;
                _dpMon = mon ?? new MonGiaoDich[0];
                _dpKhoaLuc = DateTime.UtcNow;
                _seq++;
            }
        }

        public void GhiDoiPhuongDongY()
        {
            lock (_lock)
            {
                _dpDongY = true;
                _dpDongYLuc = DateTime.UtcNow;
                _seq++;
            }
        }

        public void GhiHuy()
        {
            lock (_lock)
            {
                _huy = true;
                _huyLuc = DateTime.UtcNow;
                _dangMo = false;
                _seq++;
            }
        }

        public void GhiXong(int xuMoi)
        {
            lock (_lock)
            {
                _xong = true;
                _xuSauXong = xuMoi;
                _xongLuc = DateTime.UtcNow;
                _dangMo = false;
                _seq++;
            }
        }

        public void GhiTinServer(string chu)
        {
            if (string.IsNullOrEmpty(chu)) return;
            lock (_lock)
            {
                _tin.Enqueue(new TinServer { Luc = DateTime.UtcNow, Chu = chu });
                while (_tin.Count > TIN_TOI_DA) _tin.Dequeue();
            }
        }

        // ================= DOC / DIEU KHIEN (luong mode) =================

        public TradeSnapshot Chup()
        {
            lock (_lock)
            {
                return new TradeSnapshot
                {
                    Seq = _seq,
                    CoMoiDen = _coMoiDen,
                    MoiDenId = _moiDenId,
                    MoiDenLuc = _moiDenLuc,
                    DangMo = _dangMo,
                    DoiPhuong = _doiPhuong,
                    MoLuc = _moLuc,
                    DoiPhuongDaKhoa = _dpKhoa,
                    DoiPhuongXu = _dpXu,
                    DoiPhuongMon = _dpMon,
                    DoiPhuongKhoaLuc = _dpKhoaLuc,
                    DoiPhuongDongY = _dpDongY,
                    DoiPhuongDongYLuc = _dpDongYLuc,
                    Xong = _xong,
                    XuSauXong = _xuSauXong,
                    XongLuc = _xongLuc,
                    BiHuy = _huy,
                    HuyLuc = _huyLuc,
                };
            }
        }

        /// <summary>Lay VA xoa loi moi dang cho. false = khong co loi moi nao.</summary>
        public bool LayLoiMoi(out int inviterId, out DateTime luc)
        {
            lock (_lock)
            {
                inviterId = _moiDenId;
                luc = _moiDenLuc;
                if (!_coMoiDen) return false;
                _coMoiDen = false;
                return true;
            }
        }

        /// <summary>
        /// Bat dau mot phien MOI tu phia ta (truoc khi gui 43/44). Xoa co cua phien truoc de khong doc
        /// nham 57/58 cu. GIU loi moi dang cho — do la viec cua nguoi khac, khong phai phien cu.
        /// </summary>
        public void BatDauPhienMoi()
        {
            lock (_lock)
            {
                XoaPhienKhongKhoa();
                _seq++;
            }
        }

        /// <summary>Tin chu server nhan duoc tu moc <paramref name="tuUtc"/> tro di (cu -> moi).</summary>
        public string[] TinServerTu(DateTime tuUtc)
        {
            lock (_lock)
            {
                var r = new List<string>();
                foreach (var t in _tin)
                    if (t.Luc >= tuUtc) r.Add(t.Chu);
                return r.ToArray();
            }
        }

        public void DatLaiKhiMatKetNoi()
        {
            lock (_lock)
            {
                XoaPhienKhongKhoa();
                _coMoiDen = false;
                _tin.Clear();
                _seq++;
            }
        }

        private void XoaPhienKhongKhoa()
        {
            _dangMo = false;
            _doiPhuong = null;
            _moLuc = DateTime.MinValue;
            _dpKhoa = false;
            _dpXu = 0;
            _dpMon = new MonGiaoDich[0];
            _dpKhoaLuc = DateTime.MinValue;
            _dpDongY = false;
            _dpDongYLuc = DateTime.MinValue;
            _xong = false;
            _xuSauXong = 0;
            _xongLuc = DateTime.MinValue;
            _huy = false;
            _huyLuc = DateTime.MinValue;
        }
    }
}
