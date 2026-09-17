using System;
using System.Collections.Generic;
using NSOKHODO.Client;

namespace NSOKHODO.Kho
{
    public enum LoaiViec
    {
        DocRuong,   // toi Thu kho, xin danh sach ruong
        CatRuong,   // toi Thu kho, cat do trong tui vao ruong
        GiaoMon,    // (lay ruong) -> (tach chong) -> sang khu giao -> toi sat nguoi nhan -> giao dich
        DoiNhan,    // sang khu (chinh / Khu), (toi DungX/DungY), dung cho bot giao (TuBotAcc) moi
    }

    /// <summary>Mot dong cua viec giao: mon (khoa) + so luong can giao.</summary>
    public sealed class DongGiao
    {
        public KhoaMon Khoa;
        public int SoLuong;
        public int DaGiao;
        public int ConLai { get { return Math.Max(0, SoLuong - DaGiao); } }
    }

    /// <summary>
    /// VIEC bo dieu phoi giao cho MOT acc. Moi acc lam toi da mot viec mot luc; mode kho xin viec khi
    /// ranh (<see cref="KhoDieuPhoi.LayViec"/>) va bao ket qua khi xong (<see cref="KhoDieuPhoi.BaoViec"/>).
    /// </summary>
    public sealed class Viec
    {
        private static int _dem;

        public readonly int Id = System.Threading.Interlocked.Increment(ref _dem);
        public LoaiViec Loai;
        public string Acc;
        public DateTime TaoLuc = DateTime.Now;
        public DateTime HetHan = DateTime.MaxValue;

        // ---- GiaoMon ----
        public string NguoiNhan;          // ten nhan vat
        public bool NguoiNhanLaBot;
        public List<DongGiao> Dong = new List<DongGiao>();
        public int Xu;
        public int LenhSo;                // 0 = viec noi bo (don kho / don xu)
        // Moi lenh rut duoc GOP vao viec nay (cung nguoi nhan, giu cho tren cung acc - SPEC §7).
        public readonly List<int> CacLenh = new List<int>();
        public string MucDich = "";       // "rut" / "don" / "donxu" / "tra" / "gom" / "xa"
        public int Khu = -1;              // khu giao (D79); -1 = khu chinh
        public int ChoTimMs;              // cho thay nguoi nhan toi da (0 = mac dinh cua mode)
        // D86: chi giao mon DANG O TUI (khong ra Thu kho lay ruong, khong tach chong) - Leader chuyen tiep do
        // vua nap sang clone dung canh, phai xong trong vai giay.
        public bool ChiTui;

        // ---- DoiNhan ----
        public string TuBotAcc;           // username bot se giao
        public short DungX, DungY;        // D86: cho dung canh Leader khi cho chuyen tiep (0,0 = dung dau cung duoc)

        // ---- CatRuong ----
        public HashSet<KhoaMon> KhongCat = new HashSet<KhoaMon>();

        // ---- trang thai ----
        private volatile string _lyDoHuy;
        public bool BiHuy { get { return _lyDoHuy != null; } }
        public string LyDoHuy { get { return _lyDoHuy; } }
        public void Huy(string lyDo) { _lyDoHuy = lyDo ?? "huy"; }

        public volatile string TienDo = "";
        public volatile bool DaBatDau;

        public int TongConLai { get { int n = 0; foreach (var d in Dong) n += d.ConLai; return n; } }

        public string MoTa
        {
            get
            {
                switch (Loai)
                {
                    case LoaiViec.DocRuong: return "Đọc rương";
                    case LoaiViec.CatRuong: return "Cất rương";
                    case LoaiViec.DoiNhan:
                        return MucDich == "xa" ? "Đứng cạnh Leader nhận đồ xả" : "Chờ nhận từ " + TuBotAcc;
                    default:
                        string ai = NguoiNhan ?? "?";
                        if (MucDich == "donxu") return "Dồn xu → " + ai;
                        if (MucDich == "don") return "Dọn kho → " + ai;
                        if (MucDich == "xa") return "Chuyển đồ xả → " + ai;
                        if (MucDich == "tra") return "Trả bớt → " + ai;
                        if (MucDich == "gom") return "Gom đồ → " + ai + (Khu >= 0 ? " (khu " + Khu + ")" : "");
                        return "Giao lệnh #" + LenhSo + " → " + ai + (Khu >= 0 ? " (khu " + Khu + ")" : "");
                }
            }
        }
    }

    /// <summary>Quyet dinh cua bo dieu phoi cho mot loi moi den.</summary>
    public sealed class QuyetDinhLoiMoi
    {
        public bool Nhan;
        public bool TuChoi;              // gui 56; ca hai false = bo qua im lang
        public string TenMongDoi;        // null = chua biet ten, kiem sau goi 37
        public int ChoMs = 120000;
        public Func<string, string> KiemTen;
        public Func<int, MonGiaoDich[], string> KiemHang;
        public string LaAi = "";         // "chukho" / "nguoila" / "bot" - de ghi log
        public string HuyKhiMo;          // D87: nhan roi huy ngay khi khung mo (tu choi = nguoi moi bi khoa 30 giay)

        public static QuyetDinhLoiMoi BoQua() { return new QuyetDinhLoiMoi(); }
        public static QuyetDinhLoiMoi KhongNhan() { return new QuyetDinhLoiMoi { TuChoi = true }; }
    }

    /// <summary>Ket qua mot viec, mode kho gui lai bo dieu phoi.</summary>
    public sealed class BaoCaoViec
    {
        public Viec Viec;
        public bool Xong;
        public string MaLoi;
        public string LyDo = "";
    }

    public static class MaLoiViec
    {
        public const string KHONG_THAY_NGUOI = "KHONG_THAY_NGUOI";
        public const string KHONG_THAY_NPC = "KHONG_THAY_NPC";
        public const string KHONG_CO_MON = "KHONG_CO_MON";
        public const string TACH_CHONG_HONG = "TACH_CHONG_HONG";
        public const string TUI_DAY = "TUI_DAY";
        public const string RUONG_KHONG_DAP = "RUONG_KHONG_DAP";
        public const string KHONG_TOI_DUOC = "KHONG_TOI_DUOC";
        public const string KHONG_VAO_KHU = "KHONG_VAO_KHU";
        public const string HET_HAN = "HET_HAN";
        public const string BI_HUY = "BI_HUY";
    }
}
