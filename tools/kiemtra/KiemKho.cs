using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using NSOKHODO.Client;
using NSOKHODO.Config;
using NSOKHODO.Fleet;
using NSOKHODO.Kho;
using NSOKHODO.Models;

// BO KIEM TRA KHO — chay offline, KHONG ket noi server. Bien dich voi -r:NSOKHODO.exe.
// ⚠ csc cua .NET Framework chi hieu C# 5: khong ?. / $"" / nameof / out var / => cho thuoc tinh.
//
// Gia lap: client "gia" (khong Start) + gan MyChar / CurrentMap / OtherPlayers bang reflection; goi tin
// server gia lap bang cach GHI thang vao TradeState (dung duong ma TradeHandler ghi).
static class KiemKho
{
    static int loi;
    static string _exe;
    const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    static void Xac(bool ok, string mota)
    {
        Console.WriteLine((ok ? "  OK   " : "  SAI  ") + mota);
        if (!ok) loi++;
    }

    static void Bang(object thuc, object mong, string mota)
    {
        bool ok = Equals(thuc, mong);
        Xac(ok, mota + (ok ? "" : "   [thuc te: " + thuc + " | mong doi: " + mong + "]"));
    }

    [STAThread]
    static int Main()
    {
        _exe = Environment.GetEnvironmentVariable("NSOKHODO_EXE") ?? @"..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe";
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            return new AssemblyName(e.Name).Name == "NSOKHODO" ? Assembly.LoadFrom(_exe) : null;
        };
        try
        {
            Chay();
        }
        catch (Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            Console.WriteLine("LOI NEM RA: " + e.GetType().Name + ": " + e.Message);
            Console.WriteLine(e.StackTrace);
            loi++;
        }
        Console.WriteLine();
        Console.WriteLine(loi == 0 ? ">>> PASS" : ">>> CO " + loi + " CHO SAI");
        return loi;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Chay()
    {
        string tam = Path.Combine(Path.GetTempPath(), "nsokhodo_kiem_kho_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tam);
        AppPaths.UseBaseDir(tam);
        AppPaths.EnsureReady();
        NhatKy.DatThuMucGoc(Path.Combine(tam, "Logs"));
        NhatKy.Bat = true;
        NapBangMon();

        KiemChuVan();
        KiemLenhChat();
        KiemCaiDat();
        KiemKeHang();
        KiemSoKho();
        KiemHangCho();
        KiemPhien();
        KiemNhatKy();
        KiemKenhChat();
        KiemNavigator();
        KiemModeRuong();
        KiemDieuPhoi();
        KiemNhaChong();
        KiemGoKetVaKhu();
        KiemGomVaXa();

        NhatKy.XaNgay();
        try { Directory.Delete(tam, true); } catch { }
    }

    // ================= du lieu gia =================

    static void NapBangMon()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BangMon.DuongDan));
        File.WriteAllText(BangMon.DuongDan,
            "457|26|1|1|Đá cấp 5\r\n" +
            "458|0|0|10|Áo choàng\r\n" +
            "459|34|0|1|Ngọc khảm\r\n" +
            "460|27|0|1|Thư tay\r\n" +
            "461|27|0|1|Giấy vụn\r\n" +
            "462|29|0|1|Thú cưỡi\r\n" +
            "463|27|1|1|Hoa tuyết\r\n" +
            "464|27|1|1|Nham thạch\r\n" +
            "465|27|1|1|Pha lê\r\n" +
            "466|27|1|1|Phôi giáp\r\n" +
            "467|27|1|1|Quả chaka\r\n", new UTF8Encoding(false));
        BangMon.Nap();
    }

    static Item Mon(short tpl, int sl, byte up, bool han, bool khoa)
    {
        return new Item { TemplateId = tpl, Quantity = (ushort)sl, Upgrade = up, IsExpires = han, IsLock = khoa };
    }

    static Item Mon(short tpl, int sl) { return Mon(tpl, sl, 0, false, false); }

    static Item[] OTrong(int n)
    {
        var a = new Item[n];
        for (int i = 0; i < n; i++) a[i] = new Item();
        return a;
    }

    static Item[] Tui(int soO, params Item[] mon)
    {
        var a = OTrong(soO);
        for (int i = 0; i < mon.Length; i++) a[i] = mon[i];
        return a;
    }

    static void DatProp(object o, string ten, object v)
    {
        o.GetType().GetProperty(ten, NP).SetValue(o, v, null);
    }

    static object LayField(object o, string ten)
    {
        return o.GetType().GetField(ten, NP).GetValue(o);
    }

    static void DatField(object o, string ten, object v)
    {
        o.GetType().GetField(ten, NP).SetValue(o, v);
    }

    static object Goi(object o, string ten, params object[] thamSo)
    {
        return o.GetType().GetMethod(ten, NP).Invoke(o, thamSo);
    }

    static NsoClient TaoClient(string acc, string ten, int charId, Item[] tui, Item[] ruong, int xu, string mayChu)
    {
        var cfg = new AccountConfig { Username = acc, Password = "x", ServerName = mayChu };
        var c = new NsoClient(cfg);
        var mc = new CharacterState { Name = ten, CharId = charId, Xu = xu };
        mc.BagItems = tui;
        mc.BoxItems = ruong;
        DatProp(c.GameState, "MyChar", mc);
        c.State = ClientState.InGame;
        return c;
    }

    static void DatKhu(NsoClient c, int map, int khu)
    {
        c.GameState.CurrentMap.MapId = map;
        c.GameState.CurrentMap.ZoneId = (byte)khu;
    }

    // ================= 1. ChuVan =================

    static void KiemChuVan()
    {
        Console.WriteLine("=== ChuVan: bo dau / tem (D18, D46) ===");
        Bang(ChuVan.BoDau("Đá cấp 5 Kho đồ"), "Da cap 5 Kho do", "bo dau + đ -> d");
        Bang(ChuVan.BoDau("ĐỆ Ự ỗ"), "DE U o", "chu hoa co dau");
        Bang(ChuVan.BoDau("a\tb"), "a b", "ky tu dieu khien -> dau cach");
        Xac(ChuVan.CoKyTuLa("Tên 中"), "ky tu ngoai tieng Viet -> bao co ky tu la");
        Xac(!ChuVan.CoKyTuLa("Đá cấp"), "tieng Viet co dau -> khong phai ky tu la");
        string t1 = ChuVan.TemMoi(), t2 = ChuVan.TemMoi();
        Xac(System.Text.RegularExpressions.Regex.IsMatch(t1, "^@[0-9]{3} $"), "tem dang '@NNN ': '" + t1 + "'");
        Xac(t1 != t2, "hai tem lien tiep khac nhau");
        string gui = ChuVan.ChuanBiTinGui("Kho đồ 70/360", 100);
        Xac(gui.StartsWith("@") && gui.EndsWith(" Kho do 70/360"), "tin gui: tem + khong dau: '" + gui + "'");
        Bang(ChuVan.ChuanBiTinGui(new string('x', 300), 100).Length, 100, "cat 100 ky tu (tinh ca tem)");
        Bang(ChuVan.BoTem("@123 kho"), "kho", "bo tem @123");
        Bang(ChuVan.BoTem("@7kho"), "kho", "bo tem @7 dinh lien");
        Bang(ChuVan.BoTem("kho"), "kho", "khong co tem -> giu nguyen");
        Bang(ChuVan.BoTem("@abc"), "@abc", "@ khong phai so -> giu nguyen");
        Bang(ChuVan.ChuanHoa("  @045  LẤY   457  "), "lay 457", "chuan hoa lenh");
        Xac(ChuVan.CungTen("Abc ", "abc"), "ten khong phan biet hoa thuong / khoang trang");
        Bang(ChuVan.SoDep(1234567), "1.234.567", "so dep");
        Bang(KhoDieuPhoi.XuGon(1200000000), "1,2 ty", "xu gon: ty");
        Bang(KhoDieuPhoi.XuGon(350000000), "350 tr", "xu gon: trieu");
        Bang(KhoDieuPhoi.XuGon(12345), "12.345", "xu gon: nho");
        var chia = KhoDieuPhoi.ChiaTin("mot hai ba bon nam sau bay tam chin muoi", 12);
        Xac(chia.All(x => x.Length <= 12) && string.Join(" ", chia.ToArray()) == "mot hai ba bon nam sau bay tam chin muoi",
            "chia tin dai theo dau cach: " + string.Join(" | ", chia.ToArray()));
        chia = KhoDieuPhoi.ChiaTin("Don tui roi nhan: tiep #12, bo: huy #12", 24);
        Xac(chia.All(x => x.Length <= 24) && !chia.Any(x => x.StartsWith("#")),
            "khong tach 'tiep #12' / 'huy #12' ra hai tin: " + string.Join(" | ", chia.ToArray()));
    }

    // ================= 2. LenhChat =================

    static void KiemLenhChat()
    {
        Console.WriteLine("=== LenhChat (SPEC §9.4) ===");
        Bang(LenhChat.Doc("kho").Loai, LoaiLenh.Kho, "kho");
        Bang(LenhChat.Doc("@123 KHO ").Loai, LoaiLenh.Kho, "@123 KHO (tem + hoa)");
        Bang(LenhChat.Doc("kho abc").Loai, LoaiLenh.Khong, "kho abc -> khong phai lenh");
        Bang(LenhChat.Doc("nạp").Loai, LoaiLenh.Nap, "nạp (co dau)");
        var l = LenhChat.Doc("tìm Đá cấp");
        Xac(l.Loai == LoaiLenh.Tim && l.TuKhoa == "da cap", "tim Da cap -> tu khoa 'da cap'");
        l = LenhChat.Doc("co 457");
        Xac(l.Loai == LoaiLenh.Co && l.Tpl == 457, "co 457");
        Bang(LenhChat.Doc("co abc").Loai, LoaiLenh.Loi, "co abc -> sai cu phap");
        l = LenhChat.Doc("lay 457");
        Xac(l.Loai == LoaiLenh.Lay && l.Tpl == 457 && l.SoLuong == 1 && l.Cap == -1 && l.ChoTen == null, "lay 457 -> 1 cai, cap bat ky, cho chinh minh");
        l = LenhChat.Doc("lay 457 10 +5 cho TenAbc");
        Xac(l.Loai == LoaiLenh.Lay && l.SoLuong == 10 && l.Cap == 5 && l.ChoTen == "TenAbc", "lay 457 10 +5 cho TenAbc (giu hoa thuong ten)");
        l = LenhChat.Doc("lay 457 het");
        Xac(l.Loai == LoaiLenh.Lay && l.SoLuong == -1, "lay 457 het");
        Bang(LenhChat.Doc("lay 457 het 10").Loai, LoaiLenh.Loi, "lay 457 het 10 -> sai");
        Bang(LenhChat.Doc("lay 457 10 cho").Loai, LoaiLenh.Loi, "lay 457 10 cho (thieu ten) -> sai");
        Bang(LenhChat.Doc("lay 457 10 cho Ong Ba").Loai, LoaiLenh.Loi, "ten co dau cach -> sai (khong ho tro)");
        Bang(LenhChat.Doc("lay 457 0").Loai, LoaiLenh.Loi, "so luong 0 -> sai");
        l = LenhChat.Doc("lay goi dapdo cho Xyz");
        Xac(l.Loai == LoaiLenh.LayGoi && l.TenGoi == "dapdo" && l.ChoTen == "Xyz", "lay goi dapdo cho Xyz");
        Bang(LenhChat.Doc("lay goi").Loai, LoaiLenh.Loi, "lay goi (thieu ten) -> sai");
        l = LenhChat.Doc("tiep");
        Xac(l.Loai == LoaiLenh.Tiep && l.SoLenh == -1, "tiep");
        Bang(LenhChat.Doc("tiep #12").SoLenh, 12, "tiep #12");
        Bang(LenhChat.Doc("huy 3").SoLenh, 3, "huy 3");
        Bang(LenhChat.Doc("huy #x").Loai, LoaiLenh.Loi, "huy #x -> sai");
        l = LenhChat.Doc("theo 457 50");
        Xac(l.Loai == LoaiLenh.Theo && l.Nguong == 50, "theo 457 50");
        Bang(LenhChat.Doc("theo 457").Nguong, -1, "theo 457 -> bao moi lan ve");
        Bang(LenhChat.Doc("botheo 457").Loai, LoaiLenh.BoTheo, "botheo 457");
        Bang(LenhChat.Doc("goi").Loai, LoaiLenh.Goi, "goi");
        Bang(LenhChat.Doc("xin chao ban").Loai, LoaiLenh.Khong, "chat thuong -> bo qua");
        Bang(LenhChat.Doc("").Loai, LoaiLenh.Khong, "tin rong");
    }

    // ================= 3. KhoConfig =================

    static void KiemCaiDat()
    {
        Console.WriteLine("=== KhoConfig: doc / ghi ===");
        var c = new KhoConfig();
        Bang(c.CheDoNhan, CheDoNhan.TatCa, "mac dinh che do nhan = Tat ca (D16)");
        Xac(!c.ChatVoiNguoiLa && c.LogFile && c.RaoCheDo == CheDoRao.ThongMinh && c.MoiLaiGiay == 31, "mac dinh: khong chat nguoi la, log bat, rao thong minh, moi lai 31s");
        c.KhuChinh = 3; c.KhuPhu = 5; c.LeaderX = 120; c.LeaderY = 240;
        c.Leader = "leader1"; c.LeaderDuPhong = "backup1";
        c.DatChuKho(new[] { "ChuA", "chua", " ChuB ", "" });
        Bang(string.Join(",", c.ChuKho.ToArray()), "ChuA,ChuB", "Chu kho bo trung (khong phan biet hoa thuong) + bo rong");
        Xac(c.LaChuKho("CHUB"), "LaChuKho khong phan biet hoa thuong");
        c.CheDoNhan = CheDoNhan.ChiChuKho;
        c.RaoMau = "Kho đồ {dung}/{tong} = ok";
        c.DatRac(461, true);
        c.DatNha("clone9", true);
        c.DatKeCuaAcc("clone1", KeHang.TRANG_BI);
        c.DatKeCuaMon(460, KeHang.CHONG);
        c.DatGoi("dapdo", new List<DongGoi> { new DongGoi { Tpl = 457, SoLuong = 10 }, new DongGoi { Tpl = 458, SoLuong = -1 } });
        c.DatTheoDoi(457, 50, "ChuA");
        c.DatTheoDoi(459, -1, "ChuB");
        c.BaoCaoGio = "22:00";
        c.BatDonXu = false;
        string chu = c.VietChu();

        var d = new KhoConfig();
        d.DocChu(chu.Split(new[] { "\r\n" }, StringSplitOptions.None));
        Bang(d.VietChu(), chu, "ghi -> doc -> ghi ra giong het");
        Xac(d.KhuChinh == 3 && d.LeaderY == 240 && d.CheDoNhan == CheDoNhan.ChiChuKho && !d.BatDonXu, "gia tri don doc lai dung");
        Bang(d.RaoMau, "Kho đồ {dung}/{tong} = ok", "gia tri co dau '=' van doc dung");
        Xac(d.LaRac(461) && d.DaNha("CLONE9"), "rac + da nha doc lai dung");
        Bang(d.KeCuaAcc("clone1"), KeHang.TRANG_BI, "ke cua acc");
        Bang(d.KeCuaMon(460), KeHang.CHONG, "ke cua mon");
        var g = d.LayGoi("DAPDO");
        Xac(g != null && g.Count == 2 && g[1].SoLuong == -1, "goi rut (ten khong phan biet hoa thuong, het = -1)");
        Bang(d.TheoDoi.Count, 2, "2 muc theo doi");
        int gio, phut;
        Xac(d.LayGioBaoCao(out gio, out phut) && gio == 22 && phut == 0, "gio bao cao 22:00");
        d.BaoCaoGio = "25:00";
        Xac(!d.LayGioBaoCao(out gio, out phut), "gio 25:00 -> khong hop le");
        d.BaoCaoGio = "";
        Xac(!d.LayGioBaoCao(out gio, out phut), "trong -> tat");
        var e = new KhoConfig();
        e.DocChu(new[] { "rac khong co dau bang", "=khong khoa", "KhuChinh=abc", "# ghi chu", "TheoDoi=x:y:z;;457" });
        Bang(e.KhuChinh, -1, "dong hong khong lam hong gia tri mac dinh");
        Bang(e.TheoDoi.Count, 1, "TheoDoi hong mot phan van doc duoc phan dung");
        Xac(d.BoTheoDoi(457, "chua") && d.TheoDoi.Count == 1, "bo theo doi dung chu kho");

        string loiLuu = c.Luu();
        Xac(loiLuu == null && File.Exists(KhoConfig.DuongDan), "Luu() ghi file: " + (loiLuu ?? KhoConfig.DuongDan));
        var n = KhoConfig.Nap();
        Bang(n.KhuPhu, 5, "Nap() doc lai file");
    }

    // ================= 4. KeHang =================

    static void KiemKeHang()
    {
        Console.WriteLine("=== KeHang (SPEC §6.1) ===");
        var c = new KhoConfig();
        Bang(KeHang.NhomCuaMon(458, BangMon.Lay(458), c), KeHang.TRANG_BI, "type 0 -> Trang bi");
        Bang(KeHang.NhomCuaMon(459, BangMon.Lay(459), c), KeHang.THU_NGOC, "type 34 -> Thu & ngoc");
        Bang(KeHang.NhomCuaMon(462, BangMon.Lay(462), c), KeHang.THU_NGOC, "type 29 -> Thu & ngoc");
        Bang(KeHang.NhomCuaMon(457, BangMon.Lay(457), c), KeHang.CHONG, "xep chong -> Chong");
        Bang(KeHang.NhomCuaMon(460, BangMon.Lay(460), c), KeHang.KHAC, "con lai -> Khac");
        Bang(KeHang.NhomCuaMon(999, null, c), KeHang.KHAC, "khong biet template -> Khac");
        c.DatKeCuaMon(460, KeHang.TRANG_BI);
        Bang(KeHang.NhomCuaMon(460, BangMon.Lay(460), c), KeHang.TRANG_BI, "gan tay thang doan");
        c.DatRac(460, true);
        Bang(KeHang.NhomCuaMon(460, BangMon.Lay(460), c), KeHang.RAC, "danh dau rac thang gan tay");
        Bang(KeHang.KeCuaAcc("ai_do", c), KeHang.KHAC, "acc chua gan ke -> Khac");
        Xac(KeHang.TatCa.All(k => KeHang.TuTenHienThi(KeHang.TenHienThi(k)) == k), "ten hien thi <-> khoa ke khop hai chieu");
        Bang(BangMon.Ten(457), "Đá cấp 5", "BangMon doc tenmon.txt");
        Bang(BangMon.Ten(12345), "[12345]", "mon chua biet -> [id]");
        Bang(BangMon.TimTheoTen("da cap", 5).Count, 1, "tim ten khong dau");
    }

    // ================= 5. SoKho =================

    static void KiemSoKho()
    {
        Console.WriteLine("=== KhoaMon / SoKho (SPEC §8) ===");
        var k = new KhoaMon(457, 8, true, true);
        Bang(k.ToString(), "457+8hk", "KhoaMon.ToString");
        KhoaMon k2;
        Xac(KhoaMon.TryParse("457+8hk", out k2) && k2.Equals(k) && k2.Han, "KhoaMon.TryParse nguoc lai");
        Xac(new KhoaMon(457, 8, false, true).Equals(k) && k.Khop(Mon(457, 1, 8, false, true)),
            "D72: co han KHONG thuoc khoa (server bao khong on dinh giua goi giao dich va danh sach tui)");
        Xac(new KhoaMon(462, 5, false, false).Up == 0 && new KhoaMon(462, 0, false, false).Khop(Mon(462, 1, 5, false, false))
            && KhoaMon.TryParse("462+5", out k2) && k2.Equals(new KhoaMon(462, 0, false, false)),
            "D80: thu cuoi - cap KHONG thuoc khoa (tui co byte cap, ruong / cmd 8 / giao dich thi khong)");
        Xac(!new KhoaMon(458, 5, false, false).Khop(Mon(458, 1, 7, false, false)), "trang bi van phan biet cap");
        Xac(KhoaMon.TryParse("460", out k2) && k2.Tpl == 460 && k2.Up == 0 && !k2.Han && !k2.Khoa, "KhoaMon '460'");
        Xac(!KhoaMon.TryParse("abc", out k2), "KhoaMon 'abc' -> false");

        var so = new SoKho();
        var a = TaoClient("sa", "NvA", 1, Tui(10, Mon(457, 5), Mon(457, 3, 0, true, false), Mon(458, 1, 0, false, true)), null, 100, "S");
        so.CapNhat(a);
        var t = so.Lay("SA");
        Xac(t != null && t.Online && t.TuiDung == 3 && t.SoOTui == 10 && t.SoORuong == -1, "chep tui, chua doc ruong -> SoORuong -1");
        a.GameState.MyChar.BoxItems = Tui(6, Mon(457, 7), Mon(459, 1));
        so.CapNhat(a);
        t = so.Lay("sa");
        Xac(t.SoORuong == 6 && t.RuongDung == 2 && t.RuongTrong == 4, "doc ruong 6 o, dung 2");
        a.GameState.MyChar.BoxItems = null;   // dang nhap lai: chua xin ruong
        so.CapNhat(a);
        t = so.Lay("sa");
        Xac(t.SoORuong == 6 && t.RuongDung == 2, "ruong null -> GIU ruong da biet");
        Bang(t.SoLuong(new KhoaMon(457, 0, false, false)), 15, "SoLuong 457 = 5 + 3 co han (D72: han khong thuoc khoa) + 7 ruong");

        var b = TaoClient("sb", "NvB", 2, Tui(10, Mon(457, 10)), Tui(4), 0, "S");
        so.CapNhat(b);
        var c3 = TaoClient("sc", "NvC", 3, Tui(10, Mon(457, 99)), Tui(4), 0, "S");
        so.CapNhat(c3);
        var bang = so.Gop(x => x != "sx", x => x == "sc", new KhoConfig());
        var d = bang.First(x => x.Khoa.Equals(new KhoaMon(457, 0, false, false)));
        Xac(d.Tong == 25 && d.PhanBo.Count == 2 && d.PhanBo["sa"][0] == 8 && d.PhanBo["sa"][1] == 7 && d.Khoa.Han,
            "gop 457: 15 (sa, ca mon co han) + 10 (sb), phan bo tui/ruong, cot Han = co");
        Xac(d.TrenAccNha, "acc dang nha -> co co 'tren clone nha', khong cong vao tong");
        var dk = bang.First(x => x.Khoa.Khoa);
        Bang(dk.KhaDung, 0, "mon khoa: kha dung = 0");
        var sc = so.TinhSucChua(x => x != "sc");
        Xac(sc.Tong == 10 + 6 + 10 + 4 && sc.Dung == 3 + 2 + 1 && !sc.ChuaDu, "suc chua sa+sb: 30 o, dung 6");
        var mo = TaoClient("sd", "NvD", 4, Tui(10), null, 0, "S");
        so.CapNhat(mo);
        Xac(so.TinhSucChua(x => x == "sd").ChuaDu, "acc chua doc ruong -> suc chua 'chua du' (~)");

        so.LuuNeuCan();
        Xac(File.Exists(SoKho.DuongDan), "luu so kho ra dia");
        var so2 = new SoKho();
        so2.Nap();
        var t2 = so2.Lay("sa");
        Xac(t2 != null && !t2.Online && t2.Mon.Count == 5 && t2.SoORuong == 6 && t2.TenNV == "NvA" && t2.Xu == 100, "nap lai: offline, du mon + ruong + xu");
    }

    // ================= 6. HangCho =================

    static void KiemHangCho()
    {
        Console.WriteLine("=== HangCho: lap ke hoach + giu cho (SPEC §7, D34) ===");
        var so = new SoKho();
        // A: 10 thuong + 5 co han (tui); B: 20 (ruong) + ao choang +5 va +7
        so.CapNhat(TaoClient("ha", "A", 1, Tui(20, Mon(457, 10), Mon(457, 5, 0, true, false)), Tui(5), 0, "S"));
        so.CapNhat(TaoClient("hb", "B", 2, Tui(20, Mon(458, 1, 5, false, false)), Tui(5, Mon(457, 20), Mon(458, 1, 7, false, false)), 0, "S"));
        var hang = new HangCho();
        Func<string, bool> tatCa = x => true;

        var l1 = LenhRut(457, -1, 12, "Nhan1");
        hang.Them(l1);
        Bang(hang.LapKeHoach(l1, so, tatCa), null, "lenh #1: 12 x 457 -> du hang");
        Xac(l1.KeHoach.Count == 1 && l1.KeHoach[0].Acc == "hb" && l1.KeHoach[0].SoLuong == 12, "uu tien acc giu nhieu nhat (hb 20) -> 1 acc");

        var l2 = LenhRut(457, -1, 20, "Nhan2");
        hang.Them(l2);
        Bang(hang.LapKeHoach(l2, so, tatCa), null, "lenh #2: 20 x 457 -> du (15 ha + 5 hb)");
        int haHan = l2.KeHoach.Where(p => p.Acc == "ha" && p.Khoa.Han).Sum(p => p.SoLuong);
        int hb = l2.KeHoach.Where(p => p.Acc == "hb").Sum(p => p.SoLuong);
        int ha = l2.KeHoach.Where(p => p.Acc == "ha").Sum(p => p.SoLuong);
        Xac(l2.KeHoach[0].Acc == "ha" && ha == 15 && hb == 5 && haHan == 0,
            "ha (15, gom ca mon co han - D72) truoc hb (8 con lai)");
        Bang(hang.TongGiu(new KhoaMon(457, 0, false, false), null), 32, "tong giu 457 = 12 + 15 + 5");
        Bang(hang.GiuTrenAcc(new KhoaMon(457, 0, false, false), "hb", l1), 5, "giu tren hb (tru lenh #1) = 5");

        var l3 = LenhRut(457, -1, 10, "Nhan3");
        hang.Them(l3);
        string thieu = hang.LapKeHoach(l3, so, tatCa);
        Xac(thieu != null && thieu.Contains("chi con 3/10") && thieu.Contains("dang giu cho lenh khac"), "lenh #3 thieu: " + thieu);
        Bang(l3.KeHoach.Sum(p => p.SoLuong), 3, "lenh thieu van giu phan dang co (3)");

        var l4 = LenhRut(458, -1, 1, "Nhan4");
        hang.Them(l4);
        thieu = hang.LapKeHoach(l4, so, tatCa);
        Xac(thieu != null && thieu.Contains("nhieu cap") && l4.KeHoach.Count == 0, "458 co +5 va +7, khong ghi cap -> bat ghi ro: " + thieu);
        var l5 = LenhRut(458, 7, 1, "Nhan5");
        hang.Them(l5);
        Xac(hang.LapKeHoach(l5, so, tatCa) == null && l5.KeHoach[0].Khoa.Up == 7 && l5.KeHoach[0].Acc == "hb", "lay 458 +7 -> dung acc, dung cap");
        var l6 = LenhRut(459, -1, -1, "Nhan6");
        hang.Them(l6);
        thieu = hang.LapKeHoach(l6, so, tatCa);
        Xac(thieu != null && thieu.Contains("kho khong co"), "lay het mon kho khong co -> bao: " + thieu);
        var l7 = LenhRut(457, -1, -1, "Nhan7");
        hang.Them(l7);
        Bang(hang.LapKeHoach(l7, so, tatCa), "Đá cấp 5: kho khong co", "lay het 457 khi da giu het -> kho khong co");

        Bang(hang.LapKeHoach(l1, so, x => x != "hb"),
            "Đá cấp 5: chi con 0/12 (23 dang giu cho lenh khac) (20 nam tren acc tam khong dung: offline/nha/vua giao hong/Leader khi giao khu rieng)",
            "loai acc hb -> lenh #1 khong con nguon (giu: #2 20 + #3 3), bao ro 20 mon nam tren acc bi loai");
        hang.LapKeHoach(l1, so, tatCa);
        hang.GhiDaGiao(l1, "hb", new KhoaMon(457, 0, false, false), 12);
        Xac(hang.DaGiaoDu(l1) && l1.Dong[0].DaGiao == 12 && l1.TongDaGiao == 12, "ghi da giao 12 -> lenh #1 du");
        Bang(hang.TongGiu(new KhoaMon(457, 0, false, false), null), 23, "da giao thi het giu (con 20 + 3)");

        hang.NhaGiuTrenAcc(l2, "ha");
        Xac(l2.KeHoach.All(p => p.Acc == "hb"), "nha giu tren ha -> ke hoach lenh #2 chi con hb");

        hang.KetThuc(l1, TrangThaiLenh.Xong, "");
        Xac(hang.GanDay.Count == 1 && hang.DangMo.Count == 6 && hang.Tim(1) == l1, "ket thuc -> sang danh sach gan day, van tim duoc");

        l3.TrangThai = TrangThaiLenh.DangGiao;
        l3.ChapNhanThieu = true;
        hang.DanhDauDoi();
        hang.LuuNeuCan();
        var h2 = new HangCho();
        h2.Nap();
        var l3b = h2.Tim(l3.So);
        Xac(l3b != null && l3b.TrangThai == TrangThaiLenh.ChoCoMat && l3b.ChapNhanThieu && l3b.KeHoach.Count == l3.KeHoach.Count,
            "nap lai: lenh dang giao -> quay ve cho, giu ke hoach + co chap nhan thieu");
        Xac(h2.Tim(1) == null, "lenh da xong khong luu");
        var lMoi = LenhRut(457, -1, 1, "X");
        h2.Them(lMoi);
        Xac(lMoi.So > l7.So, "so lenh tiep tuc tang sau khi nap (" + lMoi.So + ")");

        var soT = new SoKho();
        soT.CapNhat(TaoClient("ta", "TA", 11, Tui(5, Mon(462, 1, 5, false, false)), Tui(5, Mon(462, 1)), 0, "S"));
        var hT = new HangCho();
        var lT = LenhRut(462, 3, 2, "NhanT");
        hT.Them(lT);
        Xac(hT.LapKeHoach(lT, soT, tatCa) == null && lT.KeHoach.Sum(p => p.SoLuong) == 2,
            "D80: lay thu cuoi +3 -> bo qua cap: lay ca con o tui (doc duoc +5) lan con o ruong");
    }

    static LenhRut LenhRut(short tpl, int cap, int sl, string nguoi)
    {
        return new LenhRut
        {
            NguoiNhan = nguoi,
            Dong = new List<DongLenh> { new DongLenh { Tpl = tpl, Cap = cap, SoXin = sl } },
        };
    }

    // ================= 7. PhienGiaoDich =================

    static void KiemPhien()
    {
        Console.WriteLine("=== PhienGiaoDich: gia lap goi server (SPEC §5, §7, D39) ===");
        Func<int, MonGiaoDich[], string> kiemOk = delegate(int xu, MonGiaoDich[] mon) { return null; };
        var mon1 = new[] { new MonGiaoDich { TemplateId = 457, Quantity = 5 } };

        // 1. Nhan thanh cong
        var c = TaoClient("pg", "Leader", 10, Tui(20), null, 0, "S");
        var p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 77, "ChuA", 120000, 0, 0, null, null, kiemOk);
        p.Tick();
        Xac(c.DangGiaoDich && !p.KetThuc, "nhan loi moi -> dang giao dich");
        p.Tick();
        c.Trade.GhiMoKhung("chua");
        p.Tick();
        Xac(p.TenDoiPhuong == "chua", "goi 37 -> biet ten doi phuong (khong phan biet hoa thuong)");
        c.Trade.GhiDoiPhuongKhoa(500, mon1);
        p.Tick();
        p.Tick();
        Xac(!p.KetThuc && p.MoTa.Contains("Sắp đồng ý"), "doi phuong khoa -> cho 1,5s truoc 46: " + p.MoTa);
        Thread.Sleep(1600);
        p.Tick();
        Xac(p.MoTa.Contains("Chờ xong"), "qua 1,5s -> da gui 46");
        c.Trade.GhiXong(500);
        p.Tick();
        Xac(p.KetThuc && p.KetQua.ThanhCong && p.KetQua.XuNhan == 500 && p.KetQua.MonNhan.Length == 1 && !c.DangGiaoDich,
            "58 -> XONG, nhan 1 mon + 500 xu, het dang giao dich");

        // 2. Sai doi phuong
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 77, "ChuA", 120000, 0, 0, null, null, kiemOk);
        p.Tick();
        c.Trade.GhiMoKhung("KeKhac");
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.SAI_DOI_PHUONG, "khung mo voi nguoi khac -> SAI_DOI_PHUONG");

        // 3. Kiem ten (nguoi moi chua ro)
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 78, null, 120000, 0, 0, null,
            delegate(string ten) { return ten == "XauXa" ? "khong phai chu kho" : null; }, kiemOk);
        p.Tick();
        c.Trade.GhiMoKhung("XauXa");
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.SAI_DOI_PHUONG && p.KetQua.LyDo == "khong phai chu kho", "kiem ten o goi 37 tu choi");

        // 4. Kiem hang tu choi
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 77, "ChuA", 120000, 0, 0, null, null,
            delegate(int xu, MonGiaoDich[] mon) { return "tui chi con 0 o"; });
        p.Tick();
        c.Trade.GhiMoKhung("ChuA");
        p.Tick();
        c.Trade.GhiDoiPhuongKhoa(0, mon1);
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.TU_CHOI_HANG, "kiem hang khong dat -> TU_CHOI_HANG");

        // 4b. D87: Leader het cho -> NHAN loi moi (tu choi thi nguoi moi bi khoa 30 giay) roi huy ngay khi khung mo
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 77, "ChuA", 120000, 0, 0, null, null, kiemOk);
        p.HuyKhiMo = "Leader con 0 o";
        p.Tick();
        Xac(!p.KetThuc && c.DangGiaoDich, "HuyKhiMo: van nhan loi moi");
        c.Trade.GhiMoKhung("ChuA");
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.HET_CHO && p.KetQua.LyDo == "Leader con 0 o" && !c.DangGiaoDich,
            "HuyKhiMo: khung vua mo -> huy ngay (HET_CHO)");
        Bang(KhoDieuPhoi.LyDoDeDoc(MaLoiGiaoDich.HET_CHO, "x"), "Leader hết chỗ, mời lại sau vài giây", "HET_CHO -> cau de doc");

        // 5. Vai nhan: bi dong truoc khi doi phuong khoa -> DOI_PHUONG_DAT_QUA
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 77, "ChuA", 120000, 0, 0, null, null, kiemOk);
        p.Tick();
        c.Trade.GhiMoKhung("ChuA");
        p.Tick();
        c.Trade.GhiHuy();
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.DOI_PHUONG_DAT_QUA, "vai nhan bi dong truoc 45 cua doi phuong");

        // 6. Giao: bi dong ngay sau khi khoa -> THIEU_O (D39)
        var g = TaoClient("pg2", "Clone", 11, Tui(20, Mon(457, 5), Mon(460, 1)), null, 0, "S");
        Func<byte[]> chon = delegate { return new byte[] { 0 }; };
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        g.Trade.GhiMoKhung("ChuA");
        p.Tick();
        g.Trade.GhiHuy();
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.THIEU_O, "giao: dong ngay sau khoa -> THIEU_O");

        // 7. Giao: server bao 'hanh trang' -> THIEU_O du dong muon
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        g.Trade.GhiMoKhung("ChuA");
        p.Tick();
        Thread.Sleep(3200);
        g.Trade.GhiTinServer("Đối phương không đủ chỗ trống trong hành trang");
        g.Trade.GhiHuy();
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.THIEU_O && p.KetQua.TinServer.Length == 1,
            "giao: tin server 'hanh trang' -> THIEU_O, giu tin server");

        // 7b. Cau server THAT (anh user 16/09) cung la THIEU_O
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        g.Trade.GhiMoKhung("ChuA");
        p.Tick();
        Thread.Sleep(3200);
        g.Trade.GhiTinServer("Đối phương không đủ ô trống để chứa vật phẩm giao dịch");
        g.Trade.GhiHuy();
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.THIEU_O, "giao: 'Doi phuong khong du o trong...' -> THIEU_O");

        // 7a. Nguoi nhan KHOA RONG truoc khi server dong phien (test song 16/09) -> van la THIEU_O
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        g.Trade.GhiMoKhung("ChuA");
        p.Tick();
        g.Trade.GhiDoiPhuongKhoa(0, new MonGiaoDich[0]);
        p.Tick();
        g.Trade.GhiTinServer("Đối phương không đủ ô trống để chứa vật phẩm giao dịch");
        g.Trade.GhiHuy();
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.THIEU_O, "giao: doi phuong da khoa roi server moi dong vi thieu o -> THIEU_O");

        // 7c. Server tu choi loi moi ("X do not accept." - nguoi nhan moi vao game) -> moi lai sau 10 giay, khong doi 31
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        Thread.Sleep(20);
        g.Trade.GhiTinServer("ChuA do not accept.");
        p.Tick();
        var moiLai = (DateTime)LayField(p, "_moiLaiLuc");
        Xac(!p.KetThuc && moiLai <= DateTime.UtcNow.AddSeconds(10.5) && moiLai >= DateTime.UtcNow.AddSeconds(9),
            "do not accept -> moi lai sau ~10s (" + (moiLai - DateTime.UtcNow).TotalSeconds.ToString("0.0") + "s)");
        p.YeuCauHuy("xong ca");
        p.Tick();

        // 7c2. Doi phuong dang giao dich voi nguoi khac (M27) -> moi lai sau ~3s (khong khoa 31 giay)
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        Thread.Sleep(20);
        g.Trade.GhiTinServer("Người chơi đang chờ hoàn thành một giao dịch khác.");
        p.Tick();
        moiLai = (DateTime)LayField(p, "_moiLaiLuc");
        Xac(!p.KetThuc && moiLai <= DateTime.UtcNow.AddSeconds(3.5) && moiLai >= DateTime.UtcNow.AddSeconds(2),
            "dang giao dich khac -> moi lai sau ~3s (" + (moiLai - DateTime.UtcNow).TotalSeconds.ToString("0.0") + "s)");
        p.YeuCauHuy("xong ca");
        p.Tick();

        // 7d. Server bao qua xa -> QUA_XA (mode se lai gan roi moi lai)
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, chon, null, kiemOk);
        p.Tick();
        Thread.Sleep(20);
        g.Trade.GhiTinServer("Khoảng cách quá xa không thể giao dịch");
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.QUA_XA, "giao: 'Khoang cach qua xa' -> QUA_XA");
        Xac(!g.DangGiaoDich, "QUA_XA -> nha dang giao dich");

        // 8. Giao thanh cong: xoa o da dua khoi tui local
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 1000, chon, null, kiemOk);
        p.Tick();
        g.Trade.GhiMoKhung("ChuA");
        p.Tick();
        g.Trade.GhiDoiPhuongKhoa(0, new MonGiaoDich[0]);
        p.Tick();
        Thread.Sleep(1600);
        p.Tick();
        g.Trade.GhiXong(0);
        p.Tick();
        var bag = g.GameState.MyChar.BagItems;
        Xac(p.KetThuc && p.KetQua.ThanhCong && p.KetQua.MonDua.Count == 1 && p.KetQua.MonDua[0].Value.TemplateId == 457
            && p.KetQua.XuDua == 1000 && bag[0].IsEmpty && !bag[1].IsEmpty,
            "giao xong: ghi mon da dua + xu, xoa DUNG o 0, giu o 1");

        // 9. Giao: khong con mon -> KHONG_CO_MON
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, delegate { return new byte[0]; }, null, kiemOk);
        p.Tick();
        g.Trade.GhiMoKhung("ChuA");
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.KHONG_CO_MON, "khong con mon de dat -> KHONG_CO_MON");

        // 10. Chen ngang (Chu kho nap)
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 88, "NguoiLa", 120000, 0, 0, null, null, kiemOk);
        p.Tick();
        c.Trade.GhiMoKhung("NguoiLa");
        p.Tick();
        p.YeuCauHuy("chu kho ChuA nap");
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.BI_CHEN && !c.DangGiaoDich, "YeuCauHuy -> BI_CHEN, nha dang giao dich");

        // 11. Mat ket noi
        p = new PhienGiaoDich(c, VaiGiaoDich.Nhan, 77, "ChuA", 120000, 0, 0, null, null, kiemOk);
        p.Tick();
        c.State = ClientState.Disconnected;
        p.Tick();
        Xac(p.KetThuc && p.KetQua.MaLoi == MaLoiGiaoDich.MAT_KET_NOI, "rot mang giua phien -> MAT_KET_NOI");
        c.State = ClientState.InGame;

        // 12. Giao: nguoi nhan khong nhan loi moi -> het gio (han toi thieu 5s)
        p = new PhienGiaoDich(g, VaiGiaoDich.Giao, 77, "ChuA", 1000, 1000, 0, chon, null, kiemOk);
        p.Tick();
        Thread.Sleep(5200);
        p.Tick();
        Bang(p.KetThuc ? p.KetQua.MaLoi : "(chua xong)", MaLoiGiaoDich.HET_GIO_CHO_NHAN_LOI, "giao: qua han khong co 37 -> HET_GIO_CHO_NHAN_LOI");
    }

    // ================= 8. NhatKy =================

    static void KiemNhatKy()
    {
        Console.WriteLine("=== NhatKy: log theo ngay (SPEC §8.1) ===");
        Bang(NhatKy.CsvO("a,b"), "\"a,b\"", "CSV co dau phay -> boc ngoac kep");
        Bang(NhatKy.CsvO("x\"y"), "\"x\"\"y\"", "CSV co ngoac kep -> nhan doi");
        Bang(NhatKy.CsvO(new DateTime(2026, 9, 16, 8, 5, 3)), "2026-09-16 08:05:03", "CSV ngay gio");
        Bang(NhatKy.CsvO(null), "", "CSV null");
        NhatKy.App("acc1", "Kiem", "dong kiem tra app");
        NhatKy.Csv(NhatKy.GIAO_DICH, DateTime.Now, DateTime.Now, "NAP", "acc1", "Chu, A", 77, "1", "", "[457] x5", 0, 0, "XONG", "", "", "", "");
        NhatKy.XaNgay();
        string ngay = NhatKy.ThuMucHomNay;
        string app = File.ReadAllText(Path.Combine(ngay, NhatKy.APP));
        Xac(app.Contains("[acc1] [Kiem] dong kiem tra app"), "app.log co dong vua ghi");
        var raw = File.ReadAllBytes(Path.Combine(ngay, NhatKy.GIAO_DICH));
        Xac(raw.Length > 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF, "giaodich.csv co BOM (Excel doc dung tieng Viet)");
        var dong = File.ReadAllLines(Path.Combine(ngay, NhatKy.GIAO_DICH), Encoding.UTF8);
        Xac(dong[0].StartsWith("BatDau,KetThuc,Vai") && dong[1].Contains("\"Chu, A\""), "giaodich.csv: tieu de + dong du lieu boc ngoac");
        NhatKy.Bat = false;
        NhatKy.App("acc1", "Kiem", "KHONG DUOC GHI");
        NhatKy.XaNgay();
        NhatKy.Bat = true;
        Xac(!File.ReadAllText(Path.Combine(ngay, NhatKy.APP)).Contains("KHONG DUOC GHI"), "tat log -> khong ghi");
    }

    // ================= 9. KenhChat =================

    static void KiemKenhChat()
    {
        Console.WriteLine("=== KenhChat (SPEC §9) ===");
        var k = new KenhChat();
        var bot = TaoClient("kc", "BotKc", 20, Tui(5), null, 0, "S");
        Xac(k.GuiRieng(bot, "A", "x", "dang don", 30), "tin co han che: lan dau qua");
        Xac(!k.GuiRieng(bot, "A", "x", "dang don", 30), "cung nguoi + cung khoa trong 30s -> chan");
        Xac(k.GuiRieng(bot, "B", "x", "dang don", 30), "nguoi khac -> qua");
        Xac(!k.GuiRieng(null, "A", "x"), "bot null -> false");
        var cfg = new KhoConfig();
        var k2 = new KenhChat();
        k2.GuiRieng(bot, "ChuA", "Đá cấp 5 đã về");
        k2.GuiRieng(bot, "ChuA", "tin thu hai");
        k2.GuiCongDong("Sự kiện nạp");
        var luc = DateTime.UtcNow;
        k2.Xa(bot, true, cfg, "Kho 1/2", false, false, luc);
        k2.Xa(bot, true, cfg, "Kho 1/2", false, false, luc.AddMilliseconds(500));
        NhatKy.XaNgay();
        string chat = File.ReadAllText(Path.Combine(NhatKy.ThuMucHomNay, NhatKy.CHAT));
        Xac(System.Text.RegularExpressions.Regex.IsMatch(chat, @"\[kc\] \[RIENG\] \[RA\] <ChuA> @[0-9]{3} Da cap 5 da ve"), "chat rieng gui di: tem + khong dau");
        Xac(!chat.Contains("tin thu hai"), "tin rieng thu hai chua gui (san 3 giay/bot)");
        Xac(chat.Contains("[CONGDONG] [RA] <> ") && chat.Contains("Su kien nap"), "su kien cong dong chen truoc tin rao");
        Xac(!chat.Contains("Kho 1/2"), "tin rao chua gui (cach tin cong dong truoc < nhip)");
        k2.Xa(bot, true, cfg, "Kho 1/2", false, false, luc.AddSeconds(6));
        NhatKy.XaNgay();
        chat = File.ReadAllText(Path.Combine(NhatKy.ThuMucHomNay, NhatKy.CHAT));
        Xac(chat.Contains("tin thu hai") && chat.Contains("Kho 1/2"), "6 giay sau: gui tin rieng thu hai + tin rao");
        Bang(k2.SoTinRao, 1, "dem tin rao");
    }

    // ================= 10. Navigator: khong bao gio dung Kha di lenh =================

    static void KiemNavigator()
    {
        Console.WriteLine("=== Navigator: khong dung vat pham doi khu (D38) ===");
        var c = TaoClient("nv", "Nv", 30, Tui(10, Mon(37, 1), Mon(36, 5)), null, 0, "S");
        var nav = new NSOKHODO.Auto.Navigator(c);
        Bang(Goi(nav, "FindKdlSlot"), -1, "tui co Vo han kha di lenh (37) -> FindKdlSlot van -1");
    }

    // ================= 10b. KhoMode: ruong (loi test song 16/09) =================

    static void KiemModeRuong()
    {
        Console.WriteLine("=== KhoMode: xin ruong / lay ruong / tach chong / cat ruong ===");
        NSOKHODO.Logging.Logger.Enabled = true;
        var dpM = new KhoDieuPhoi(new FleetManager(), delegate { return new List<AccountConfig>(); },
                                  new KhoConfig { KhuChinh = 3, KhuPhu = 5 });
        var hienTai = typeof(KhoDieuPhoi).GetProperty("HienTai");
        hienTai.SetValue(null, dpM, null);
        try
        {
            var c = TaoClient("mr", "ModeRuong", 2001, Tui(10), null, 0, "S");
            DatKhu(c, 22, 5);
            c.GameState.MyChar.Cx = 100;
            c.GameState.MyChar.Cy = 200;
            c.GameState.CurrentMap.Npcs.Add(new NpcState { TemplateId = 5, X = 100, Y = 200 });
            var log = new List<string>();
            c.OnLog += delegate(string s) { lock (log) log.Add(s); };
            var m = new NSOKHODO.Auto.KhoMode(c);
            Func<int> tick = delegate { return (int)Goi(m, "Tick"); };
            Func<string, int> dem = delegate(string mau) { lock (log) return log.Count(s => s.Contains(mau)); };

            // --- GiaoMon: ruong CHUA co trong bo nho (vua dang nhap), hang nam trong ruong ---
            var v = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", HetHan = DateTime.Now.AddMinutes(5) };
            v.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(457, 1)), SoLuong = 5 });
            DatField(m, "_viec", v);
            for (int i = 0; i < 5; i++) tick();
            Bang(dem("Xin danh sach ruong"), 1, "ruong chua co -> xin DUNG 1 lan roi cho");
            c.GameState.MyChar.BoxItems = Tui(30, Mon(457, 106));
            c.GameState.BoxSeq++;
            tick();
            for (int i = 0; i < 10; i++) tick();
            Bang(dem("Xin danh sach ruong"), 1, "dang cho server chuyen mon ruong->tui -> KHONG xin lai danh sach (ban loi: 1.537 lan)");
            c.GameState.MyChar.BoxItems[0] = new Item();
            c.GameState.MyChar.BagItems[0] = Mon(457, 106);
            c.GameState.BoxSeq++;
            tick(); tick(); tick();
            Bang(dem("Tach chong o 0: lay 5/106"), 1, "mon da ve tui -> tach 5 tu chong 106 (-28/-85)");
            c.GameState.MyChar.BagItems[0] = Mon(457, 101);
            c.GameState.MyChar.BagItems[1] = Mon(457, 5);
            tick();
            Bang(dem("Tach chong xong"), 1, "thay o moi 5 mon -> tach xong");
            tick();
            Bang(LayField(m, "_buoc").ToString(), "SangKhu", "chong vua du -> buoc sang khu chinh");
            Bang(dem("Xin danh sach ruong"), 1, "ca viec chi xin danh sach ruong 1 lan");

            // --- xin ruong khong ai tra loi: toi da 3 lan, cach nhau >= 2 giay ---
            c.GameState.MyChar.BoxItems = null;
            var v2 = new Viec { Loai = LoaiViec.DocRuong, Acc = "mr", HetHan = DateTime.Now.AddMinutes(5) };
            DatField(m, "_viec", v2);
            DatField(m, "_xinRuongLuc", DateTime.MinValue);
            DatField(m, "_choDsRuongDen", DateTime.MinValue);
            int truoc = dem("Xin danh sach ruong");
            var den = DateTime.UtcNow.AddSeconds(1.5);
            while (DateTime.UtcNow < den) { tick(); Thread.Sleep(20); }
            Bang(dem("Xin danh sach ruong") - truoc, 1, "1,5 giay ticks lien tuc -> van chi 1 goi xin ruong (san 2 giay)");
            DatField(m, "_choDsRuongDen", DateTime.MinValue);
            DatField(m, "_xinRuongLuc", DateTime.MinValue);
            tick();
            DatField(m, "_choDsRuongDen", DateTime.MinValue);
            DatField(m, "_xinRuongLuc", DateTime.MinValue);
            tick();
            DatField(m, "_choDsRuongDen", DateTime.MinValue);
            DatField(m, "_xinRuongLuc", DateTime.MinValue);
            tick();
            Xac(LayField(m, "_viec") == null && dem("Ket thuc viec Doc ruong: LOI RUONG_KHONG_DAP") == 1 && dem("Xin danh sach ruong") - truoc == 3,
                "server im 3 lan -> ket thuc RUONG_KHONG_DAP, khong xin lan 4");

            // --- CatRuong: server tra loi nhung mon khong vao ruong -> moi o chi gui 1 lan ---
            c.GameState.MyChar.BoxItems = Tui(30);
            c.GameState.MyChar.BagItems = Tui(10, Mon(457, 5), Mon(460, 1));
            var v3 = new Viec { Loai = LoaiViec.CatRuong, Acc = "mr", HetHan = DateTime.Now.AddMinutes(5) };
            ((HashSet<int>)LayField(m, "_oDaGui")).Clear();
            DatField(m, "_goiRuongLan", 0);
            DatField(m, "_viec", v3);
            for (int i = 0; i < 6; i++) { tick(); c.GameState.BoxSeq++; tick(); }
            Xac(LayField(m, "_viec") == null && (int)LayField(m, "_goiRuongLan") == 2,
                "cat ruong: 2 o, server khong nhan -> gui dung 2 goi roi ket thuc (" + LayField(m, "_goiRuongLan") + " goi)");

            KiemGiaoTuiDay(c, m, tick, dem);
        }
        finally { hienTai.SetValue(null, null, null); }
    }

    // User 16/09 23:22: rut 30 Tu tinh thach, acc giu 12 trong tui (tui day) + 18 trong ruong -> ca 2 acc bao
    // "tui day, khong lay duoc do tu ruong", lenh tam dung "chi con 9/30" du kho con 52.
    static void KiemGiaoTuiDay(NsoClient c, NSOKHODO.Auto.KhoMode m, Func<int> tick, Func<string, int> dem)
    {
        Console.WriteLine("=== KhoMode: tui day -> giao theo luot / cat doi cho ===");
        var mc = c.GameState.MyChar;
        Action<Viec> batDau = delegate(Viec vv)
        {
            ((HashSet<int>)LayField(m, "_oDaGui")).Clear();
            DatField(m, "_goiRuongLan", 0);
            DatField(m, "_choBoxSeq", -1);
            DatField(m, "_viec", vv);
            DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "BatDau"));
        };
        Func<string> buoc = delegate { return LayField(m, "_buoc").ToString(); };
        // Server gia: moi goi tui<->ruong dang cho thi thuc hien ngay (cat tam: theo dong log; lay: mon can dau tien).
        int catTruoc = 0;
        Action<short> server = delegate(short can)
        {
            if ((int)LayField(m, "_choBoxSeq") < 0) return;
            var bag = mc.BagItems; var box = mc.BoxItems;
            int cat = dem("cat tam o ");
            if (cat > catTruoc)
            {
                catTruoc = cat;
                int i = Array.FindIndex(bag, x => !x.IsEmpty && x.TemplateId != can);
                int j = Array.FindIndex(box, x => x.IsEmpty);
                box[j] = bag[i]; bag[i] = new Item();
            }
            else
            {
                int j = Array.FindIndex(box, x => !x.IsEmpty && x.TemplateId == can);
                int i = Array.FindIndex(bag, x => x.IsEmpty);
                bag[i] = box[j]; box[j] = new Item();
            }
            c.GameState.BoxSeq++;
        };
        Func<short, int, int> chay = delegate(short can, int toiDa)
        {
            int n = 0;
            while (n++ < toiDa && LayField(m, "_viec") != null && buoc() != "SangKhu") { tick(); server(can); }
            return n;
        };
        Func<short, int> demTui = delegate(short tpl) { return mc.BagItems.Count(x => !x.IsEmpty && x.TemplateId == tpl); };
        Action<short, int> giaoTui = delegate(short tpl, int so)
        {
            for (int i = 0; i < mc.BagItems.Length && so > 0; i++)
                if (!mc.BagItems[i].IsEmpty && mc.BagItems[i].TemplateId == tpl) { mc.BagItems[i] = new Item(); so--; }
        };

        // --- A: 12 trong tui (tui day) + 18 trong ruong -> 3 luot 12/12/6 ---
        var tui = new List<Item>();
        for (int i = 0; i < 12; i++) tui.Add(Mon(460, 1));
        for (int i = 0; i < 18; i++) tui.Add(Mon(461, 1));
        mc.BagItems = Tui(30, tui.ToArray());
        var ruong = new List<Item>();
        for (int i = 0; i < 18; i++) ruong.Add(Mon(460, 1));
        mc.BoxItems = Tui(30, ruong.ToArray());
        catTruoc = dem("cat tam o ");
        var v = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", HetHan = DateTime.Now.AddMinutes(5) };
        v.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 30 });
        batDau(v);
        chay(460, 10);
        Xac(buoc() == "SangKhu" && (int)LayField(m, "_goiRuongLan") == 0,
            "tui day nhung co san 12 mon can giao -> di giao luot 1 ngay, khong dung ruong (buoc " + buoc() + ")");
        giaoTui(460, 12); v.Dong[0].DaGiao = 12;
        DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "GiaoDich"));
        tick();
        Bang(buoc(), "LayRuong", "giao xong luot 1, tui het mon -> quay ve lay ruong");
        chay(460, 60);
        Xac(buoc() == "SangKhu" && demTui(460) == 12 && (int)LayField(m, "_goiRuongLan") == 12,
            "luot 2: lay 12 mon tu ruong roi di giao (tui " + demTui(460) + ", " + LayField(m, "_goiRuongLan") + " goi)");
        giaoTui(460, 12); v.Dong[0].DaGiao = 24;
        DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "GiaoDich"));
        chay(460, 60);
        Xac(buoc() == "SangKhu" && demTui(460) == 6 && (int)LayField(m, "_goiRuongLan") == 18,
            "luot 3: lay 6 mon con lai (tui " + demTui(460) + ", " + LayField(m, "_goiRuongLan") + " goi)");
        giaoTui(460, 6); v.Dong[0].DaGiao = 30;
        DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "GiaoDich"));
        tick();
        Xac(LayField(m, "_viec") == null && dem("Ket thuc viec Giao lenh #0") > 0 && dem("LOI KHONG_CO_MON") == 0,
            "du 30/30 -> viec XONG, khong con bao KHONG_CO_MON");

        // --- A2: con 3 trong tui, tui con cho, ruong con hang -> lay them truoc khi giao ---
        mc.BagItems = Tui(30, Mon(460, 1), Mon(460, 1), Mon(460, 1));
        mc.BoxItems = Tui(30, Mon(460, 1), Mon(460, 1), Mon(460, 1), Mon(460, 1));
        var vA2 = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", HetHan = DateTime.Now.AddMinutes(5) };
        vA2.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 7 });
        batDau(vA2);
        DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "GiaoDich"));
        tick();
        Bang(buoc(), "LayRuong", "3 trong tui + 4 trong ruong, tui con cho -> lay them truoc (khong giao le 3)");
        chay(460, 30);
        Xac(buoc() == "SangKhu" && demTui(460) == 7, "lay du 7 roi moi di giao (tui " + demTui(460) + ")");

        // --- B: tui day toan mon khac, ruong con 1 o trong -> cat doi cho tung mon ---
        var tuiB = new List<Item>();
        for (int i = 0; i < 30; i++) tuiB.Add(Mon(461, 1));
        mc.BagItems = Tui(30, tuiB.ToArray());
        var ruongB = new List<Item>();
        for (int i = 0; i < 5; i++) ruongB.Add(Mon(460, 1));
        for (int i = 0; i < 24; i++) ruongB.Add(Mon(462, 1));
        mc.BoxItems = Tui(30, ruongB.ToArray());
        catTruoc = dem("cat tam o ");
        int catDau = catTruoc;
        var vB = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", HetHan = DateTime.Now.AddMinutes(5) };
        vB.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 5 });
        batDau(vB);
        chay(460, 60);
        Xac(buoc() == "SangKhu" && demTui(460) == 5 && dem("cat tam o ") - catDau == 5 && (int)LayField(m, "_goiRuongLan") == 10,
            "tui day mon khac -> cat tam 5 mon, lay 5 mon (tui " + demTui(460) + ", cat " + (dem("cat tam o ") - catDau)
            + ", " + LayField(m, "_goiRuongLan") + " goi)");

        // --- B2: tui day mon khac + ruong day -> TUI_DAY (khong phai KHONG_CO_MON "thieu hang") ---
        mc.BagItems = Tui(30, tuiB.ToArray());
        var ruongB2 = new List<Item>(ruongB);
        ruongB2.Add(Mon(462, 1));
        mc.BoxItems = Tui(30, ruongB2.ToArray());
        var vB2 = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", HetHan = DateTime.Now.AddMinutes(5) };
        vB2.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 5 });
        int tuiDayTruoc = dem("LOI TUI_DAY");
        batDau(vB2);
        chay(460, 10);
        Xac(LayField(m, "_viec") == null && dem("LOI TUI_DAY") == tuiDayTruoc + 1 && (int)LayField(m, "_goiRuongLan") == 0,
            "tui + ruong deu day -> ket thuc TUI_DAY, khong gui goi nao");

        // --- C: tui day, chi co chong 106 can tach 5 -> cat tam 1 mon lay o trong roi tach ---
        var tuiC = new List<Item> { Mon(457, 106) };
        for (int i = 0; i < 29; i++) tuiC.Add(Mon(461, 1));
        mc.BagItems = Tui(30, tuiC.ToArray());
        mc.BoxItems = Tui(30);
        catTruoc = dem("cat tam o ");
        catDau = catTruoc;
        int tachTruoc = dem("Tach chong o 0: lay 5/106");
        var vC = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", HetHan = DateTime.Now.AddMinutes(5) };
        vC.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(457, 1)), SoLuong = 5 });
        batDau(vC);
        for (int i = 0; i < 12 && dem("Tach chong o 0: lay 5/106") == tachTruoc && LayField(m, "_viec") != null; i++) { tick(); server(457); }
        Xac(dem("cat tam o ") - catDau == 1 && dem("Tach chong o 0: lay 5/106") == tachTruoc + 1,
            "tui day + chong 106 can tach -> cat tam 1 mon roi tach (cat " + (dem("cat tam o ") - catDau) + ")");
        DatField(m, "_tachTuO", -1);
        DatField(m, "_viec", null);

        // --- D (D79): khu giao rieng khong vao duoc (khu day) -> KHONG_VAO_KHU sau 90 s, khong doi toi het han viec ---
        mc.BagItems = Tui(30, Mon(460, 1));
        mc.BoxItems = Tui(30);
        var vD = new Viec { Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "ChuA", MucDich = "rut", Khu = 9, HetHan = DateTime.Now.AddMinutes(5) };
        vD.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 1 });
        int vaoKhuTruoc = dem("LOI KHONG_VAO_KHU");
        batDau(vD);
        chay(460, 6);
        Bang(buoc(), "SangKhu", "viec khu 9: toi buoc sang khu");
        DatField(c, "_vaoKhuLucTicks", DateTime.UtcNow.Ticks);   // dang hoi chieu doi khu -> chua gui doi khu
        tick();
        Xac(LayField(m, "_viec") != null && dem("LOI KHONG_VAO_KHU") == vaoKhuTruoc, "moi sang khu -> van cho");
        DatField(m, "_sangKhuTu", DateTime.UtcNow.AddSeconds(-100));
        DatField(c, "_vaoKhuLucTicks", DateTime.UtcNow.Ticks);
        tick();
        Xac(LayField(m, "_viec") == null && dem("LOI KHONG_VAO_KHU") == vaoKhuTruoc + 1 && dem("khong vao duoc khu 9") > 0,
            "100 s chua vao duoc khu 9 -> ket thuc KHONG_VAO_KHU");

        // --- E (D86): luot chuyen tiep ChiTui - tui thieu hang / chong lon hon cung KHONG ra Thu kho, KHONG tach ---
        mc.BagItems = Tui(30, Mon(457, 20));
        mc.BoxItems = Tui(30, Mon(460, 5));
        DatField(c, "_vaoKhuLucTicks", DateTime.UtcNow.AddSeconds(-30).Ticks);
        var vE = new Viec
        {
            Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "CloneX", NguoiNhanLaBot = true, MucDich = "xa", ChiTui = true,
            Khu = 5, HetHan = DateTime.Now.AddMinutes(5),
        };
        vE.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 5 });
        vE.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(457, 1)), SoLuong = 12 });
        int xinTruoc = dem("Xin danh sach ruong"), tachTruocE = dem("Tach chong");
        batDau(vE);
        for (int i = 0; i < 4; i++) tick();
        Xac(buoc() == "DenGan" && (int)LayField(m, "_goiRuongLan") == 0 && dem("Xin danh sach ruong") == xinTruoc
            && dem("Tach chong") == tachTruocE,
            "ChiTui: thieu hang trong tui, chong 20 > 12 -> di giao ngay, khong mo ruong / tach chong (buoc " + buoc() + ")");
        DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "GiaoDich"));
        int khongCoTruoc = dem("LOI KHONG_CO_MON");
        tick();
        Xac(LayField(m, "_viec") == null && dem("LOI KHONG_CO_MON") == khongCoTruoc + 1 && (int)LayField(m, "_goiRuongLan") == 0,
            "ChiTui: tui khong con mon khop -> ket thuc KHONG_CO_MON (khong ve Thu kho)");
        var vE2 = new Viec
        {
            Loai = LoaiViec.GiaoMon, Acc = "mr", NguoiNhan = "CloneX", NguoiNhanLaBot = true, MucDich = "xa", ChiTui = true,
            Khu = 5, HetHan = DateTime.Now.AddMinutes(5),
        };
        vE2.Dong.Add(new DongGiao { Khoa = KhoaMon.Tu(Mon(460, 1)), SoLuong = 5, DaGiao = 2 });
        int xongTruoc = dem(": XONG");
        batDau(vE2);
        DatField(m, "_buoc", Enum.Parse(LayField(m, "_buoc").GetType(), "GiaoDich"));
        tick();
        Xac(LayField(m, "_viec") == null && dem(": XONG") == xongTruoc + 1,
            "ChiTui: da giao mot phan, tui het mon khop -> XONG phan da giao (khong bao hong)");
    }

    // ================= 10c. Don kho: mon xep chong ve nick dang giu (user 16/09) =================

    static void KiemNhaChong()
    {
        Console.WriteLine("=== Don kho: do xep chong ve cung nick ===");
        var cL = TaoClient("ncL", "NcLeader", 3001, Tui(30, Mon(457, 5), Mon(460, 1)), Tui(30), 0, "Kiem");
        var cA = TaoClient("ncA", "NcA", 3002, Tui(30), Tui(30, Mon(457, 100)), 0, "Kiem");
        var cB = TaoClient("ncB", "NcB", 3003, Tui(30), Tui(30), 0, "Kiem");
        DatKhu(cL, 22, 3);
        DatKhu(cA, 22, 5);
        DatKhu(cB, 22, 5);
        var accs = new List<AccountConfig> { cL.Config, cA.Config, cB.Config };
        var fleet = new FleetManager();
        ((List<NsoClient>)LayField(fleet, "_clients")).AddRange(new[] { cL, cA, cB });
        var cfg = new KhoConfig { Leader = "ncL", KhuChinh = 3, KhuPhu = 5, RaoBat = false, BatCatRuong = false };
        cfg.DatKeCuaAcc("ncB", KeHang.CHONG);   // ncA ke Khac (mac dinh) nhung dang giu 457
        var dp = new KhoDieuPhoi(fleet, delegate { return accs; }, cfg);
        Action nhip = delegate { Goi(dp, "Nhip"); };
        nhip();
        foreach (var c in new[] { cL, cA, cB }) ChanGui(dp.Kenh, c);

        // Leader khong cat ruong (tat) -> chuyen thang tu tui
        nhip();
        var v = dp.ViecCua("ncA");
        Xac(v != null && v.Loai == LoaiViec.DoiNhan && dp.ViecCua("ncB") == null,
            "457 xep chong: goi ncA (dang giu 457, ke Khac) chu KHONG goi ncB (ke Xep chong)");
        DatKhu(cA, 22, 3);
        nhip();
        var vL = dp.ViecCua("ncL");
        Xac(vL != null && vL.Dong.Count == 2 && vL.Dong.Any(d => d.Khoa.Tpl == 457 && d.SoLuong == 5) && vL.Dong.Any(d => d.Khoa.Tpl == 460),
            "luot cua ncA mang ca 457 va 460 (mon ke Khac cua chinh no)");

        // Huy luot; cho nha ban -> KHONG rai sang ncB
        dp.LayViec(cL);
        dp.BaoViec(cL, new BaoCaoViec { Viec = vL, MaLoi = MaLoiViec.BI_HUY, LyDo = "kiem" });
        nhip();
        DatField(dp, "_donNghiDen", DateTime.MinValue);
        DatKhu(cA, 22, 5);
        var chan = new Viec { Loai = LoaiViec.CatRuong, Acc = "ncA", HetHan = DateTime.Now.AddMinutes(5) };
        ((System.Collections.IDictionary)LayField(dp, "_viec"))["ncA"] = chan;
        cL.GameState.MyChar.BagItems[1] = new Item();   // chi con 457
        nhip();
        Xac(dp.ViecCua("ncB") == null, "nha 457 (ncA) dang ban -> cho, khong dua 457 sang ncB");

        // Nha het cho that (tui day + ruong day, khong gop duoc) -> di nick khac
        ((System.Collections.IDictionary)LayField(dp, "_viec")).Remove("ncA");
        cA.GameState.MyChar.BagItems = Tui(1, Mon(461, 1));
        var ruongDay = Tui(30);
        for (int i = 0; i < 30; i++) ruongDay[i] = Mon(i == 0 ? (short)457 : (short)461, i == 0 ? 29999 : 1);
        cA.GameState.MyChar.BoxItems = ruongDay;
        DatField(dp, "_donNghiDen", DateTime.MinValue);
        nhip();
        // ncA tui 1/1 (461 khong xep chong) + ruong day -> KET CUNG (D78): tra bot cho Leader truoc.
        var vTra = dp.ViecCua("ncA");
        var vDoi = dp.ViecCua("ncL");
        Xac(vTra != null && vTra.MucDich == "tra" && vTra.NguoiNhanLaBot && vTra.Dong.Count == 1 && vTra.Dong[0].Khoa.Tpl == 461
            && vDoi != null && vDoi.Loai == LoaiViec.DoiNhan && vDoi.TuBotAcc == "ncA",
            "nha 457 ket cung (tui 1/1 + ruong day) -> go ket: ncA tra 461 cho Leader, Leader dung doi");
        dp.LayViec(cA);
        cA.GameState.MyChar.BagItems = Tui(1);
        dp.BaoViec(cA, new BaoCaoViec { Viec = vTra, Xong = true });
        nhip();
        {
            // Viec doi cua Leader chua ai nhan -> go ngay -> cung nhip Leader don hang vua nhan sang ncB (dung y).
            var donG = LayField(dp, "_don");
            string mdG = donG == null ? null : (string)donG.GetType().GetField("MucDich").GetValue(donG);
            Xac(vDoi.BiHuy && mdG != "tra", "tra xong -> Leader thoi doi, het luot go ket (luot tiep: " + (mdG ?? "-") + ")");
        }
        dp.LayViec(cL);
        dp.BaoViec(cL, new BaoCaoViec { Viec = vDoi, MaLoi = MaLoiViec.BI_HUY, LyDo = "tra bot xong" });
        cA.GameState.MyChar.BagItems = Tui(1, Mon(461, 1));   // gia su van day (khong nhan lai)
        DatField(dp, "_donNghiDen", DateTime.MinValue);
        nhip();
        nhip();
        var vB = dp.ViecCua("ncB");
        Xac(vB != null && vB.Loai == LoaiViec.DoiNhan && dp.ViecCua("ncA") == null,
            "nha 457 vua go ket (nghi nhan 30 phut) + het cho -> don sang ncB, khong tra lai ncA");

        // Mon KHONG xep chong: uu tien nick dang giu cung loai du nick khac trong hon
        DatField(dp, "_don", null);
        ((System.Collections.IDictionary)LayField(dp, "_viec")).Remove("ncB");
        cfg.DatKeCuaAcc("ncB", KeHang.KHAC);
        cA.GameState.MyChar.BagItems = Tui(30);
        cA.GameState.MyChar.BoxItems = Tui(30);
        cB.GameState.MyChar.BoxItems = Tui(30, Mon(460, 1));
        cL.GameState.MyChar.BagItems = Tui(30, Mon(460, 1));
        DatField(dp, "_donNghiDen", DateTime.MinValue);
        nhip();
        nhip();
        Xac(dp.ViecCua("ncB") != null && dp.ViecCua("ncA") == null, "thu tay (khong chong): ve ncB dang giu thu tay, khong ve ncA trong hon");
    }

    // ================= 10d. Go ket (D78) + khu giao (D79) — loi user 17/09 =================

    static void KiemGoKetVaKhu()
    {
        Console.WriteLine("=== Go ket clone (D78) + khu giao rieng (D79) ===");

        // ---- lenh chat: khu ----
        var lc = LenhChat.Doc("lay 457 5 khu 7");
        Xac(lc.Loai == LoaiLenh.Lay && lc.SoLuong == 5 && lc.Khu == 7 && lc.ChoTen == null, "chat: lay 457 5 khu 7");
        lc = LenhChat.Doc("lay 457 khu 3 cho Abc");
        Xac(lc.Loai == LoaiLenh.Lay && lc.Khu == 3 && lc.ChoTen == "Abc" && lc.SoLuong == 1, "chat: lay 457 khu 3 cho Abc");
        lc = LenhChat.Doc("lay goi dapdo khu 2 cho Abc");
        Xac(lc.Loai == LoaiLenh.LayGoi && lc.TenGoi == "dapdo" && lc.Khu == 2 && lc.ChoTen == "Abc", "chat: lay goi dapdo khu 2 cho Abc");
        lc = LenhChat.Doc("lay goi dapdo khu 2");
        Xac(lc.Loai == LoaiLenh.LayGoi && lc.Khu == 2 && lc.ChoTen == null, "chat: lay goi dapdo khu 2");
        foreach (var sai in new[] { "lay 457 khu", "lay 457 khu 100", "lay 457 khu -1", "lay 457 khu 3 khu 4", "lay 457 cho Abc khu 3", "lay goi dapdo khu", "lay goi dapdo khu x" })
            Bang(LenhChat.Doc(sai).Loai, LoaiLenh.Loi, "chat sai: " + sai);
        Xac(LenhChat.Doc("lay 457 x").Loi.Length <= 90, "cach dung 'lay' vua mot tin (<= 90 ky tu): " + LenhChat.Doc("lay 457 x").Loi.Length);
        Bang(LenhChat.Doc("lay 457").Khu, -1, "chat: khong ghi khu -> -1 (khu chinh)");

        // ---- hang cho: luu / nap khu ----
        try { File.Delete(HangCho.DuongDan); } catch { }
        var h1 = new HangCho();
        var lk = new LenhRut { NguoiNhan = "Abc", Khu = 7, ChoCoMatDen = DateTime.Now.AddMinutes(5) };
        lk.Dong.Add(new DongLenh { Tpl = 457, SoXin = 2 });
        h1.Them(lk);
        var lk2 = new LenhRut { NguoiNhan = "Xyz", ChoCoMatDen = DateTime.Now.AddMinutes(5) };
        lk2.Dong.Add(new DongLenh { Tpl = 457, SoXin = 1 });
        h1.Them(lk2);
        h1.LuuNeuCan();
        var h2 = new HangCho();
        h2.Nap();
        var m2 = h2.DangMo;
        Xac(m2.Count == 2 && m2[0].Khu == 7 && m2[1].Khu == -1, "hang cho: luu / nap khu giao (7 va -1)");
        File.WriteAllText(HangCho.DuongDan, "SoKe=5\r\nL|4|tool||Old||0||" + DateTime.Now.Ticks + "|" + DateTime.Now.AddMinutes(5).Ticks
            + "|0|0\r\nD|4|457|-1|1|0|-1|0\r\n", new UTF8Encoding(false));
        var h3 = new HangCho();
        h3.Nap();
        Xac(h3.DangMo.Count == 1 && h3.DangMo[0].Khu == -1, "hang cho: file cu (khong co cot khu) -> khu chinh");
        try { File.Delete(HangCho.DuongDan); } catch { }
        try { File.Delete(SoKho.DuongDan); } catch { }

        // ---- dieu phoi: clone ket cung giu hang cho lenh ----
        // gkK: tui 3/3 (460 khong xep chong) + ruong 2/2 (hai 459) -> khong lay 459 ra duoc.
        var cL = TaoClient("gkL", "GkLeader", 4001, Tui(30), Tui(30), 0, "Kiem");
        var cK = TaoClient("gkK", "GkKet", 4002, Tui(3, Mon(460, 1), Mon(460, 1), Mon(460, 1)), Tui(2, Mon(459, 1), Mon(459, 1)), 0, "Kiem");
        var cM = TaoClient("gkM", "GkMo", 4003, Tui(30, Mon(462, 1), Mon(462, 1)), Tui(30), 0, "Kiem");
        DatKhu(cL, 22, 3);
        DatKhu(cK, 22, 5);
        DatKhu(cM, 22, 5);
        cL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 77, Name = "ChuG", X = 300, Y = 200 });
        var accs = new List<AccountConfig> { cL.Config, cK.Config, cM.Config };
        var fleet = new FleetManager();
        ((List<NsoClient>)LayField(fleet, "_clients")).AddRange(new[] { cL, cK, cM });
        var cfg = new KhoConfig { Leader = "gkL", KhuChinh = 3, KhuPhu = 5, RaoBat = false, BatCatRuong = false };
        cfg.DatChuKho(new[] { "ChuG" });
        var dp = new KhoDieuPhoi(fleet, delegate { return accs; }, cfg);
        Action nhip = delegate { Goi(dp, "Nhip"); };
        nhip();
        foreach (var c in new[] { cL, cK, cM }) ChanGui(dp.Kenh, c);
        Func<LenhRut> lenhDau = delegate { return dp.Hang.DangMo.FirstOrDefault(); };

        dp.RutTuTool(459, -1, 2, "ChuG");
        nhip();
        var l1 = lenhDau();
        var vTra = dp.ViecCua("gkK");
        var vDoi = dp.ViecCua("gkL");
        Xac(l1 != null && l1.TrangThai != TrangThaiLenh.TamDung && l1.CloneDangGiao == null,
            "lenh 459 x2 (hang trong ruong clone ket) KHONG tam dung, KHONG giao cho clone ket");
        Xac(vTra != null && vTra.MucDich == "tra" && vTra.NguoiNhanLaBot && vTra.Dong.Count == 1 && vTra.Dong[0].Khoa.Tpl == 460
            && vTra.Dong[0].SoLuong == 3 && vDoi != null && vDoi.Loai == LoaiViec.DoiNhan && vDoi.TuBotAcc == "gkK",
            "go ket: gkK tra 3 x 460 (khong giu cho lenh) cho Leader, Leader dung doi (du Chu kho dang trong khu)");
        Xac(dp.XetLoiMoi(cL, 4002, null).Nhan, "Leader nhan loi moi cua gkK khi dang doi tra bot");
        Xac(dp.XetLoiMoi(cM, 4002, null).TuChoi, "clone khac khong nhan loi moi cua gkK");

        dp.LayViec(cK);
        cK.GameState.MyChar.BagItems = Tui(3);
        dp.BaoViec(cK, new BaoCaoViec { Viec = vTra, Xong = true });
        nhip();
        Xac(vDoi.BiHuy && LayField(dp, "_don") == null, "tra xong -> Leader thoi doi, het luot go ket");
        dp.LayViec(cL);
        dp.BaoViec(cL, new BaoCaoViec { Viec = vDoi, MaLoi = MaLoiViec.BI_HUY, LyDo = "tra bot xong" });
        nhip();
        var vRut = dp.ViecCua("gkK");
        Xac(vRut != null && vRut.MucDich == "rut" && vRut.Dong.Count == 1 && vRut.Dong[0].Khoa.Tpl == 459 && vRut.Khu == -1,
            "go ket xong (tui con cho) -> gkK giao lenh 459 o khu chinh");

        // TUI_DAY that (tui lai day - so kho chua kip thay ruong day): KHONG tranh acc, KHONG tam dung, KHONG giao lai
        // ngay (mode vua bao ket -> tin mode) ma cho go ket.
        cK.GameState.MyChar.BagItems = Tui(3, Mon(460, 1), Mon(460, 1), Mon(460, 1));
        nhip();   // so kho thay tui day (that ra tui da day tu truoc khi mode bao - nhip xu ly bao cao chay truoc CapNhatSo)
        dp.LayViec(cK);
        dp.BaoViec(cK, new BaoCaoViec { Viec = vRut, MaLoi = MaLoiViec.TUI_DAY, LyDo = "tui day, ruong khong con cho cat bot" });
        nhip();
        Xac(l1.TrangThai != TrangThaiLenh.TamDung && l1.HongLienTiep == 0 && dp.ViecCua("gkK") == null && l1.CloneDangGiao == null,
            "TUI_DAY (go ket duoc) -> khong tam dung, khong tinh hong, khong giao lai ngay (vua tra xong: cho 60 s)");
        Xac(dp.XetLoiMoi(cL, 77, null).Nhan, "Leader ranh -> nhan loi moi cua Chu kho binh thuong");
        DatField(dp, "_moiDenLeaderLuc", DateTime.MinValue);   // loi moi vua xet lam Leader "ban" 10 s
        ((System.Collections.IDictionary)LayField(dp, "_traXongDen")).Clear();
        nhip();
        var vTra2 = dp.ViecCua("gkK");
        var vDoi2 = dp.ViecCua("gkL");
        Xac(vTra2 != null && vTra2.MucDich == "tra" && vDoi2 != null && vDoi2.Loai == LoaiViec.DoiNhan,
            "het 60 s -> go ket lan nua (mode vua bao TUI_DAY = tin la ket du so kho chua thay)");
        var qdChu = dp.XetLoiMoi(cL, 77, null);
        Xac(qdChu.Nhan && qdChu.HuyKhiMo != null, "Leader dang doi clone tra bot -> Chu kho: nhan roi huy ngay (khong nhan do, khong bi khoa 30 giay)");
        Xac(dp.XetLoiMoi(cL, 999, null).TuChoi, "Leader dang doi clone tra bot -> nguoi la: tu choi");
        // Go ket hong -> nghi 10 phut; TUI_DAY luc do -> khong cho vo ich: xu nhu loi acc NGAY, ly do ro
        dp.LayViec(cK);
        dp.BaoViec(cK, new BaoCaoViec { Viec = vTra2, MaLoi = MaLoiGiaoDich.HET_GIO_CHO_NHAN_LOI, LyDo = "kiem" });
        nhip();
        Xac(vDoi2.BiHuy && LayField(dp, "_don") == null, "tra bot hong -> Leader thoi doi");
        dp.LayViec(cL);
        dp.BaoViec(cL, new BaoCaoViec { Viec = vDoi2, MaLoi = MaLoiViec.BI_HUY, LyDo = "clone tra bot hong" });
        nhip();
        var vRut2 = dp.ViecCua("gkK");
        Xac(vRut2 != null && vRut2.MucDich == "rut", "go ket dang nghi (khong tu go duoc) -> van giao cho gkK (de mode tu bao)");
        if (vRut2 != null)
        {
            dp.LayViec(cK);
            dp.BaoViec(cK, new BaoCaoViec { Viec = vRut2, MaLoi = MaLoiViec.TUI_DAY, LyDo = "tui day, ruong khong con cho cat bot" });
            nhip();
        }
        Xac(l1.TrangThai == TrangThaiLenh.TamDung && l1.LyDo.Contains("khong tu go ket duoc: vua tra bot hong"),
            "TUI_DAY khi khong tu go ket duoc -> tam dung ngay, ly do ro: " + l1.LyDo);
        dp.HuyLenhTuTool(l1.So);
        ((System.Collections.IDictionary)LayField(dp, "_traNghi")).Clear();
        nhip();

        // ---- chua o (D78): clone chi con 1 o khong nhan don ----
        var ranh = typeof(KhoDieuPhoi).GetMethod("RanhNhanDon", BindingFlags.NonPublic | BindingFlags.Instance);
        cM.GameState.MyChar.BagItems = Tui(2, Mon(462, 1));
        nhip();
        Xac(!(bool)ranh.Invoke(dp, new object[] { "gkM", DateTime.Now }), "clone con 1 o (o chua) -> khong nhan luot don");
        cM.GameState.MyChar.BagItems = Tui(3, Mon(462, 1));
        nhip();
        Xac((bool)ranh.Invoke(dp, new object[] { "gkM", DateTime.Now }), "clone con 2 o -> nhan duoc");
        cM.GameState.MyChar.BagItems = Tui(30, Mon(462, 1), Mon(462, 1));
        nhip();

        // ---- khu giao rieng (D79) ----
        dp.RutTuTool(462, -1, 1, "ChuG", 7);
        dp.RutTuTool(462, -1, 1, "ChuG");
        nhip();
        var lk7 = dp.Hang.DangMo.FirstOrDefault(x => x.Khu == 7);
        var lkc = dp.Hang.DangMo.FirstOrDefault(x => x.Khu == -1);
        var vC = dp.ViecCua("gkM");
        Xac(lk7 != null && lkc != null, "tao 2 lenh cung nguoi nhan: khu 7 (tao truoc) va khu chinh");
        Xac(vC != null && vC.Khu == -1 && vC.CacLenh.Count == 1 && lkc != null && vC.CacLenh[0] == lkc.So,
            "nguoi nhan dang dung o khu chinh -> lenh khu chinh giao TRUOC lenh khu 7 khong ai nhin thay, KHONG gop");
        dp.LayViec(cM);
        dp.BaoViec(cM, new BaoCaoViec { Viec = vC, MaLoi = MaLoiViec.BI_HUY, LyDo = "mode dung (mat ket noi / tat)" });
        dp.HuyLenhTuTool(lkc.So);
        nhip();
        var vK = dp.ViecCua("gkM");
        Xac(vK != null && vK.Khu == 7 && vK.ChoTimMs == 90000 && vK.CacLenh.Count == 1 && lk7 != null && vK.CacLenh[0] == lk7.So,
            "het lenh khac -> khu 7 khong bot nao nhin thay: van cu gkM sang tim (doi 90 s)");
        dp.LayViec(cM);
        dp.BaoViec(cM, new BaoCaoViec { Viec = vK, MaLoi = MaLoiViec.KHONG_THAY_NGUOI, LyDo = "khong thay ChuG o khu 7" });
        nhip();
        Xac(lk7.TrangThai != TrangThaiLenh.TamDung && lk7.HongLienTiep == 0 && lk7.ThuTimSau > DateTime.Now.AddSeconds(50)
            && dp.ViecCua("gkM") == null, "khong thay o khu 7 -> khong tinh hong, 60 s sau moi thu lai");
        dp.RutTuTool(462, -1, 1, "ChuG", 7);
        nhip();
        Xac(dp.ViecCua("gkM") == null, "lenh moi cung nguoi / cung khu 7 khong vuot moc thu lai cua lenh cu");
        var lk7b = dp.Hang.DangMo.FirstOrDefault(x => x.Khu == 7 && x != lk7);
        lk7.ThuTimSau = DateTime.MinValue;
        nhip();
        var vK2 = dp.ViecCua("gkM");
        Xac(vK2 != null && vK2.Khu == 7 && vK2.CacLenh.Count == 2, "het 60 s -> cu gkM sang khu 7 tim lai, gop 2 lenh khu 7");
        if (lk7b != null) { dp.HuyLenhTuTool(lk7b.So); }
        dp.LayViec(cM);
        dp.BaoViec(cM, new BaoCaoViec { Viec = vK2, MaLoi = MaLoiViec.KHONG_VAO_KHU, LyDo = "doi 90s khong vao duoc khu 7" });
        nhip();
        Xac(lk7.TrangThai == TrangThaiLenh.TamDung && lk7.LyDo.Contains("khong vao duoc khu 7"),
            "khong vao duoc khu 7 -> tam dung, bao ro: " + lk7.LyDo);
        dp.HuyLenhTuTool(lk7.So);
        dp.RutTuTool(462, -1, 1, "ChuG", 3);
        nhip();
        var lk3 = dp.Hang.DangMo.FirstOrDefault();
        Xac(lk3 != null && lk3.Khu == -1, "chon khu 3 = khu chinh -> luu la -1 (theo khu chinh)");
        dp.HuyLenhTuTool(lk3.So);
        nhip();

        // Lenh khu rieng KHONG lap ke hoach len Leader (Leader khong roi khu chinh); lenh khu chinh thi duoc.
        cfg.BatDonKho = false;
        cL.GameState.MyChar.BagItems = Tui(30, Mon(461, 1));
        nhip();
        dp.RutTuTool(461, -1, 1, "ChuG", 7);
        nhip();
        Xac(dp.Hang.DangMo.Count == 0 && dp.Hang.GanDay.Count > 0 && dp.Hang.GanDay[0].LyDo.Contains("khong co hang"),
            "hang chi nam tren Leader + giao khu 7 -> khong lap ke hoach len Leader (huy: khong co hang)");
        dp.RutTuTool(461, -1, 1, "ChuG");
        nhip();
        var vLeader = dp.ViecCua("gkL");
        Xac(vLeader != null && vLeader.MucDich == "rut" && vLeader.Khu == -1, "cung mon, giao khu chinh -> Leader giao duoc");
        var lL = dp.Hang.DangMo.FirstOrDefault();
        if (lL != null) dp.HuyLenhTuTool(lL.So);
        nhip();

        // Bat don kho: lenh khu rieng ma hang chi tren Leader -> CHO Leader don (khong huy "kho khong co"), Leader don NGAY
        // du Chu kho dang trong khu va Leader con cho; hang sang clone -> clone giao o khu 7.
        // Lenh 2 x 461 o khu 7: 1 tren gkM, 1 tren Leader.
        cfg.BatDonKho = true;
        cK.GameState.MyChar.BagItems = Tui(30);
        cM.GameState.MyChar.BagItems = Tui(30, Mon(461, 1));
        foreach (var acc in new[] { "gkL", "gkK", "gkM" })
        {
            var vv = dp.ViecCua(acc);
            if (vv != null) dp.BaoViec(acc == "gkL" ? cL : acc == "gkK" ? cK : cM, new BaoCaoViec { Viec = vv, MaLoi = MaLoiViec.BI_HUY, LyDo = "kiem" });
        }
        DatField(dp, "_donNghiDen", DateTime.MinValue);
        DatField(dp, "_moiDenLeaderLuc", DateTime.MinValue);
        ((IDictionary)LayField(dp, "_nghiNhanDon")).Clear();
        ((IDictionary)LayField(dp, "_tranh")).Clear();
        nhip();
        dp.RutTuTool(461, -1, 2, "ChuG", 7);
        nhip();
        var lw = dp.Hang.DangMo.FirstOrDefault();
        Xac(lw != null && lw.ChoLeaderDon && lw.DangMo && lw.TrangThai != TrangThaiLenh.TamDung,
            "bat don kho: lenh khu 7 thieu 1 (dang tren Leader) -> CHO Leader don, khong tam dung / huy");
        var vRm = dp.ViecCua("gkM");
        Xac(vRm != null && vRm.MucDich == "rut" && vRm.Khu == 7 && vRm.TongConLai == 1, "phan co san (gkM) giao truoc o khu 7");
        var vdg = dp.ViecCua("gkK");
        Xac(vdg != null && vdg.Loai == LoaiViec.DoiNhan && vdg.TuBotAcc == "gkL",
            "Leader con 29 o + Chu kho dang trong khu -> VAN don ngay (gkK nhan) vi lenh dang cho hang tren Leader");

        // gkM giao xong phan cua no -> lenh CHUA xong (review 17/09: truoc day dong "XONG 1/2").
        dp.LayViec(cM);
        var pR = new PhienGiaoDich(cM, VaiGiaoDich.Giao, 77, "ChuG", 120000, 31000, 0, null, null, null);
        var kqR = new KetQuaGiaoDich { ThanhCong = true, Vai = VaiGiaoDich.Giao, DoiPhuong = "ChuG", DoiPhuongId = 77 };
        kqR.MonDua.Add(new KeyValuePair<byte, Item>(0, Mon(461, 1)));
        DatProp(pR, "KetQua", kqR);
        dp.BaoPhien(cM, pR, vRm);
        cM.GameState.MyChar.BagItems = Tui(30);
        dp.BaoViec(cM, new BaoCaoViec { Viec = vRm, Xong = true });
        nhip();
        nhip();
        Xac(lw.DangMo && lw.TongDaGiao == 1 && lw.ChoLeaderDon, "giao xong 1/2 -> lenh van mo, van cho phan tren Leader");

        DatKhu(cK, 22, 3);
        nhip();
        var vLd = dp.ViecCua("gkL");
        Xac(vLd != null && vLd.MucDich == "don" && vLd.Dong.Any(d => d.Khoa.Tpl == 461), "Leader giao 461 cho gkK");
        if (vLd != null && vdg != null)
        {
            dp.LayViec(cL);
            cL.GameState.MyChar.BagItems = Tui(30);
            cK.GameState.MyChar.BagItems = Tui(30, Mon(461, 1));
            dp.BaoViec(cL, new BaoCaoViec { Viec = vLd, Xong = true });
            nhip();
            nhip();
            dp.LayViec(cK);
            dp.BaoViec(cK, new BaoCaoViec { Viec = vdg, MaLoi = MaLoiViec.BI_HUY, LyDo = "don xong" });
            DatKhu(cK, 22, 5);
            nhip();
            nhip();
        }
        var vRk = dp.ViecCua("gkK");
        Xac(!lw.ChoLeaderDon && lw.DangMo && vRk != null && vRk.MucDich == "rut" && vRk.Khu == 7 && vRk.TongConLai == 1,
            "hang da sang gkK -> lap lai, gkK giao not 1 o khu 7");
        dp.HuyLenhTuTool(lw.So);
        nhip();
        try { File.Delete(HangCho.DuongDan); } catch { }
    }

    // ================= 10e. Gom do xep chong (D81) + xa nhanh (D86) + log de doc (D83) — user 17/09 =================

    static Item[] TuiKhoa(int soO, int trong)
    {
        var a = OTrong(soO);
        for (int i = 0; i < soO - trong; i++) a[i] = Mon(460, 1, 0, false, true);
        return a;
    }

    static string PhienDonMucDich(object d)
    {
        return d == null ? null : (string)d.GetType().GetField("MucDich").GetValue(d);
    }

    static void KiemGomVaXa()
    {
        Console.WriteLine("=== Gom do xep chong (D81) + xa nhanh (D86) + log de doc (D83) ===");

        // ---- lenh chat ----
        Bang(LenhChat.Doc("xa").Loai, LoaiLenh.Xa, "chat: xa");
        Bang(LenhChat.Doc("Xả xong").Loai, LoaiLenh.XaXong, "chat: xa xong (co dau)");
        Bang(LenhChat.Doc("xa abc").Loai, LoaiLenh.Loi, "chat sai: xa abc");

        // ---- log de doc ----
        var dsMon = new List<KhoDieuPhoi.MonDoc> { new KhoDieuPhoi.MonDoc(457, 0, 5), new KhoDieuPhoi.MonDoc(457, 0, 3), new KhoDieuPhoi.MonDoc(458, 5, 1) };
        Bang(KhoDieuPhoi.MonDeDoc(dsMon, 1000), "8 Đá cấp 5, 1 Áo choàng +5 + 1.000 xu", "D83: mo ta mon de doc (gop cung loai, ten that, xu)");
        Bang(KhoDieuPhoi.LyDoDeDoc(MaLoiGiaoDich.THIEU_O, "x"), "người nhận không đủ ô trống", "D83: ma loi -> cau de doc");

        // ================= GOM =================
        // A, B: tui chi con 2 o (28 mon khoa), ruong giu 5 loai xep chong. C: trong. Dich tot nhat = C (trung gian):
        // A / B lam dich thi moi luot chi nhan 1 chong (5 luot); C nhan 5 chong mot luot (2 luot).
        // A, B: tui 23 mon khoa + 5 chong + 2 o trong; ruong day mon khoa (khong cat duoc). C: trong.
        Func<Item[]> tui5 = delegate
        {
            var t = TuiKhoa(30, 7);
            t[23] = Mon(463, 10); t[24] = Mon(464, 20); t[25] = Mon(465, 30); t[26] = Mon(466, 40); t[27] = Mon(467, 50);
            return t;
        };
        var gL = TaoClient("gmL", "GmLeader", 5001, Tui(30), Tui(30), 0, "Kiem");
        var gA = TaoClient("gmA", "GmA", 5002, tui5(), TuiKhoa(30, 0), 0, "Kiem");
        var gB = TaoClient("gmB", "GmB", 5003, tui5(), TuiKhoa(30, 0), 0, "Kiem");
        var gC = TaoClient("gmC", "GmC", 5004, Tui(30), Tui(30), 0, "Kiem");
        DatKhu(gL, 22, 3);
        foreach (var c in new[] { gA, gB, gC }) DatKhu(c, 22, 5);
        var accs = new List<AccountConfig> { gL.Config, gA.Config, gB.Config, gC.Config };
        var fleet = new FleetManager();
        ((List<NsoClient>)LayField(fleet, "_clients")).AddRange(new[] { gL, gA, gB, gC });
        var cfg = new KhoConfig { Leader = "gmL", KhuChinh = 3, KhuPhu = 5, RaoBat = false, BatDonKho = false };
        cfg.DatChuKho(new[] { "ChuG" });
        var dp = new KhoDieuPhoi(fleet, delegate { return accs; }, cfg);
        var suKien = new List<string>();
        dp.OnSuKien += delegate(string s) { suKien.Add(s); };
        Action nhip = delegate { Goi(dp, "Nhip"); };
        Action choGom = delegate
        {
            DatField(dp, "_gomNghiDen", DateTime.MinValue);
            DatField(dp, "_gomXetLuc", DateTime.MinValue);
        };
        nhip();
        foreach (var c in new[] { gL, gA, gB, gC }) ChanGui(dp.Kenh, c);

        Bang((string)LayField(dp, "_gomDich"), "gmC", "D81: A / B chi con 1 o nhan -> dich la C (nick trong lam trung gian)");
        var gomD = LayField(dp, "_gom");
        string nguoiGiao = gomD == null ? null : (string)gomD.GetType().GetField("Clone").GetValue(gomD);
        var vGiao = nguoiGiao != null ? dp.ViecCua(nguoiGiao) : null;
        var vNhan = dp.ViecCua("gmC");
        Xac(vGiao != null && vGiao.MucDich == "gom" && vGiao.Khu == 5 && vGiao.NguoiNhanLaBot && vGiao.NguoiNhan == "GmC" && vGiao.Dong.Count == 5,
            "luot 1: " + nguoiGiao + " giao ca 5 chong cho C o khu cua clone (5) trong MOT luot");
        Xac(vNhan != null && vNhan.Loai == LoaiViec.DoiNhan && vNhan.Khu == 5 && vNhan.TuBotAcc == nguoiGiao && vNhan.DungX == 0,
            "C doi nhan o khu 5 tu " + nguoiGiao);
        Xac(suKien.Any(x => x.StartsWith("[GOM]") && x.Contains("5 loại") && x.Contains("GmC")), "log de doc: bat dau gom: " + string.Join(" / ", suKien.ToArray()));
        var cGiao = nguoiGiao == "gmA" ? gA : gB;
        var cKhac = nguoiGiao == "gmA" ? gB : gA;
        Xac(dp.XetLoiMoi(gC, cGiao.GameState.MyChar.CharId, null).Nhan, "C nhan loi moi cua nick giao gom");
        Xac(dp.XetLoiMoi(gC, cKhac.GameState.MyChar.CharId, null).TuChoi, "C khong nhan loi moi cua nick khac");

        // Nick giao xong: 5 chong sang tui C.
        dp.LayViec(cGiao);
        dp.LayViec(gC);
        cGiao.GameState.MyChar.BagItems = TuiKhoa(30, 7);
        gC.GameState.MyChar.BagItems = Tui(30, Mon(463, 10), Mon(464, 20), Mon(465, 30), Mon(466, 40), Mon(467, 50));
        dp.BaoViec(cGiao, new BaoCaoViec { Viec = vGiao, Xong = true });
        nhip();
        Xac(vNhan.BiHuy && LayField(dp, "_gom") == null && (int)LayField(dp, "_gomDotLuot") == 1, "giao xong -> C thoi doi, dem 1 luot");
        dp.BaoViec(gC, new BaoCaoViec { Viec = vNhan, MaLoi = MaLoiViec.BI_HUY, LyDo = "xong luot gom" });
        nhip();
        var vCat = dp.ViecCua("gmC");
        Xac(vCat != null && vCat.Loai == LoaiViec.CatRuong, "C nhan xong -> cat ruong truoc luot sau");
        choGom();
        nhip();
        Xac(LayField(dp, "_gom") == null, "C dang cat ruong -> chua mo luot 2");
        dp.LayViec(gC);
        gC.GameState.MyChar.BagItems = Tui(30);
        gC.GameState.MyChar.BoxItems = Tui(30, Mon(463, 10), Mon(464, 20), Mon(465, 30), Mon(466, 40), Mon(467, 50));
        dp.BaoViec(gC, new BaoCaoViec { Viec = vCat, Xong = true });
        nhip();
        choGom();
        nhip();
        var vGiao2 = dp.ViecCua(cKhac.Config.Username);
        Xac(vGiao2 != null && vGiao2.MucDich == "gom" && vGiao2.Dong.Count == 5 && dp.ViecCua("gmC") != null,
            "luot 2: nick con lai giao 5 chong cho C");
        dp.LayViec(cKhac);
        var vNhan2 = dp.ViecCua("gmC");
        dp.LayViec(gC);
        cKhac.GameState.MyChar.BagItems = TuiKhoa(30, 7);
        dp.BaoViec(cKhac, new BaoCaoViec { Viec = vGiao2, Xong = true });
        nhip();
        dp.BaoViec(gC, new BaoCaoViec { Viec = vNhan2, MaLoi = MaLoiViec.BI_HUY, LyDo = "xong luot gom" });
        nhip();
        var vCat2 = dp.ViecCua("gmC");
        if (vCat2 != null)
        {
            dp.LayViec(gC);
            dp.BaoViec(gC, new BaoCaoViec { Viec = vCat2, Xong = true });
        }
        choGom();
        nhip();
        Xac(LayField(dp, "_gomDich") == null && suKien.Any(x => x.StartsWith("[GOM] Xong") && x.Contains("2 lượt")),
            "het nick giu -> xong dot, log de doc: " + suKien.LastOrDefault());
        choGom();
        nhip();
        Xac(LayField(dp, "_gomDich") == null && LayField(dp, "_gom") == null, "khong con loai nao rai -> khong mo dot moi");

        // Nick B chi con 1 o ma dang giu -> KHONG lam dich; A trong nhieu va cung giu -> A la dich, 1 luot.
        gC.GameState.MyChar.BoxItems = Tui(30);
        gA.GameState.MyChar.BagItems = Tui(30);
        gA.GameState.MyChar.BoxItems = Tui(30, Mon(463, 1), Mon(464, 1));
        gB.GameState.MyChar.BagItems = TuiKhoa(30, 2);
        gB.GameState.MyChar.BoxItems = Tui(30, Mon(463, 5), Mon(464, 5));
        choGom();
        nhip();
        choGom();
        nhip();
        Bang((string)LayField(dp, "_gomDich"), "gmA", "A trong 30 o + dang giu -> dich (B con 1 o nhan khong lam dich)");
        var vB = dp.ViecCua("gmB");
        Xac(vB != null && vB.MucDich == "gom" && vB.Dong.Count == 2 && dp.ViecCua("gmA") != null && dp.ViecCua("gmA").TuBotAcc == "gmB",
            "B giao 2 chong cho A trong mot luot");

        // Lenh rut can nick dang gom -> huy luot gom (lenh len truoc).
        gL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 5099, Name = "ChuG", X = 300, Y = 200 });
        dp.RutTuTool(464, -1, 5, "ChuG");
        nhip();
        Xac(LayField(dp, "_gom") == null && vB.BiHuy, "lenh rut 464 (dang tren B, B dang gom) -> huy luot gom");
        dp.LayViec(gB);
        dp.BaoViec(gB, new BaoCaoViec { Viec = vB, MaLoi = MaLoiViec.BI_HUY, LyDo = "nhuong cho lenh rut" });
        var vA = dp.ViecCua("gmA");
        if (vA != null) { dp.LayViec(gA); dp.BaoViec(gA, new BaoCaoViec { Viec = vA, MaLoi = MaLoiViec.BI_HUY, LyDo = "kiem" }); }
        nhip();
        var lG = dp.Hang.DangMo.FirstOrDefault();
        Xac(lG != null && dp.ViecCua("gmB") != null && dp.ViecCua("gmB").MucDich == "rut", "nhip sau: B giao lenh rut");
        choGom();
        nhip();
        Xac(LayField(dp, "_gom") == null, "con lenh rut -> khong gom");
        if (lG != null) dp.HuyLenhTuTool(lG.So);
        nhip();

        // ================= XA NHANH (D86) =================
        // Leader: 20 mon don duoc (460) + 5 khoa + 5 o trong (duoi nguong 12). xA, xB trong 30; xC con 14 o; xD con 3 o.
        var xL = TaoClient("xaL", "XaLeader", 6001, TuiKhoa(30, 5), Tui(30), 0, "Kiem");
        for (int i = 0; i < 20; i++) xL.GameState.MyChar.BagItems[i] = Mon(460, 1);   // 20 mon don duoc, 5 khoa, 5 trong
        var xA = TaoClient("xaA", "XaA", 6002, Tui(30), Tui(30), 0, "Kiem");
        var xB = TaoClient("xaB", "XaB", 6003, Tui(30), Tui(30), 0, "Kiem");
        var xC = TaoClient("xaC", "XaC", 6004, TuiKhoa(30, 14), Tui(30), 0, "Kiem");
        var xD = TaoClient("xaD", "XaD", 6005, TuiKhoa(30, 3), Tui(30), 0, "Kiem");
        DatKhu(xL, 22, 3);
        xL.GameState.MyChar.Cx = 365;
        xL.GameState.MyChar.Cy = 216;
        foreach (var c in new[] { xA, xB, xC, xD }) DatKhu(c, 22, 5);
        var chuX = new PlayerInfo { CharId = 6077, Name = "ChuX", X = 360, Y = 216 };
        xL.GameState.CurrentMap.OtherPlayers.Add(chuX);
        xL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 6088, Name = "NguoiLa", X = 380, Y = 216 });
        var accsX = new List<AccountConfig> { xL.Config, xA.Config, xB.Config, xC.Config, xD.Config };
        var fleetX = new FleetManager();
        ((List<NsoClient>)LayField(fleetX, "_clients")).AddRange(new[] { xL, xA, xB, xC, xD });
        var cfgX = new KhoConfig { Leader = "xaL", KhuChinh = 3, KhuPhu = 5, RaoBat = false, BatCatRuong = false, BatGomChong = false };
        cfgX.LeaderX = 365;
        cfgX.LeaderY = 216;
        cfgX.DatChuKho(new[] { "ChuX" });
        var dx = new KhoDieuPhoi(fleetX, delegate { return accsX; }, cfgX);
        var suKienX = new List<string>();
        dx.OnSuKien += delegate(string s) { suKienX.Add(s); };
        Action nhipX = delegate { Goi(dx, "Nhip"); };
        Action<NsoClient, Viec> boViec = delegate(NsoClient c, Viec v)
        {
            if (v == null) return;
            dx.LayViec(c);
            dx.BaoViec(c, new BaoCaoViec { Viec = v, MaLoi = MaLoiViec.BI_HUY, LyDo = "kiem" });
        };
        // Leader giao xong mot luot chuyen: bao phien + bao viec (nhu mode).
        Action<Viec, string, int, Item[]> chuyenXong = delegate(Viec v, string ten, int id, Item[] mon)
        {
            dx.LayViec(xL);
            var p = new PhienGiaoDich(xL, VaiGiaoDich.Giao, id, ten, 20000, 31000, 0, null, null, null);
            var kq = new KetQuaGiaoDich { ThanhCong = true, Vai = VaiGiaoDich.Giao, DoiPhuong = ten, DoiPhuongId = id };
            for (int i = 0; i < mon.Length; i++) kq.MonDua.Add(new KeyValuePair<byte, Item>((byte)i, mon[i]));
            DatProp(p, "KetQua", kq);
            dx.BaoPhien(xL, p, v);
            foreach (var d in v.Dong) d.DaGiao = d.SoLuong;
            dx.BaoViec(xL, new BaoCaoViec { Viec = v, Xong = true });
        };
        Action<string, MonGiaoDich[]> nap = delegate(string ten, MonGiaoDich[] mon)
        {
            var p = new PhienGiaoDich(xL, VaiGiaoDich.Nhan, 6077, ten, 120000, 0, 0, null, null, null);
            var kq = new KetQuaGiaoDich { ThanhCong = true, Vai = VaiGiaoDich.Nhan, DoiPhuong = ten, DoiPhuongId = 6077, MonNhan = mon };
            DatProp(p, "KetQua", kq);
            dx.BaoPhien(xL, p, null);
        };
        Func<Item[]> tuiLeaderSau = delegate
        {
            var t = TuiKhoa(30, 7);
            t[23] = Mon(457, 12);
            t[24] = Mon(458, 1, 5, false, false);
            return t;
        };
        nhipX();
        foreach (var c in new[] { xL, xA, xB, xC, xD }) ChanGui(dx.Kenh, c);
        nhipX();

        // Cho dung CUNG TANG DAT voi Leader (test song 17/09: x = 335 la mep, clone roi xuong y = 288 -> "qua xa").
        var te = new NSOKHODO.GameData.TileEngine();
        var oDat = new int[30 * 20];
        for (int tx = 14; tx <= 20; tx++) oDat[9 * 30 + tx] = 2;   // hang y 216: dat tu x 336 toi 503
        DatField(te, "_width", 30);
        DatField(te, "_height", 20);
        DatField(te, "_coll", oDat);
        var tilesCu = xL.GameState.Tiles;
        DatProp(xL.GameState, "Tiles", te);
        Bang((int)Goi(dx, "ChoDungXa", new HashSet<int>()), 395, "cho dung: +30 (395) cung tang dat");
        Bang((int)Goi(dx, "ChoDungXa", new HashSet<int> { 395 }), 395, "+30 da co nguoi, -30 (335) khong co dat -> dung chung 395");
        oDat[9 * 30 + 16] = 0;
        Bang((int)Goi(dx, "ChoDungXa", new HashSet<int>()), 365, "hai ben deu khong co dat -> dung ngay cho Leader");
        DatProp(xL.GameState, "Tiles", tilesCu);
        Bang((int)Goi(dx, "ChoDungXa", new HashSet<int> { 395 }), 335, "chua co du lieu o -> khong loc (335)");

        // Chu kho dung canh + Leader duoi nguong -> Leader VAN don kho (truoc day dung im cho Chu kho).
        var vDon = dx.ViecCua("xaA") ?? dx.ViecCua("xaB");
        Xac(vDon != null && vDon.Loai == LoaiViec.DoiNhan && vDon.TuBotAcc == "xaL" && vDon.MucDich == "",
            "Leader con 5 o (< nguong 12) + Chu kho trong khu -> VAN don kho (goi clone sang nhan)");
        // Bo luot don de kiem xa nhanh rieng; chan don kho thuong.
        foreach (var c in new[] { xA, xB, xC, xD })
        {
            var vv = dx.ViecCua(c.Config.Username);
            if (vv == null) continue;
            vv.Huy("kiem");
            boViec(c, vv);
        }
        DatField(dx, "_don", null);
        DatField(dx, "_donNghiDen", DateTime.MaxValue);
        nhipX();
        RutTin(dx.Kenh, xL);

        // ---- Leader day, Chu kho moi -> tu choi + xa nhanh: 2 clone trong nhat sang dung SAT canh Leader ----
        var qdL = dx.XetLoiMoi(xL, 6077, null);
        Xac(qdL.Nhan && !qdL.TuChoi && qdL.HuyKhiMo == "Leader con 5 o" && (DateTime)LayField(dx, "_moiDenLeaderLuc") == DateTime.MinValue,
            "D87: Leader day -> NHAN roi huy khi khung mo (tu choi = Chu kho bi khoa 30 giay); khong danh dau 'sap giao dich'");
        Xac(dx.XetLoiMoi(xL, 6088, null).TuChoi, "Leader day -> nguoi la van bi tu choi");
        nhipX();
        var vxA = dx.ViecCua("xaA");
        var vxB = dx.ViecCua("xaB");
        Xac(LayField(dx, "_xa") != null && vxA != null && vxA.MucDich == "xa" && vxA.TuBotAcc == "xaL"
            && vxB != null && vxB.MucDich == "xa" && dx.ViecCua("xaC") == null && dx.ViecCua("xaD") == null,
            "Leader day + Chu kho moi -> xa nhanh: xA, xB (trong nhat) sang dung canh, toi da 2 clone");
        Xac(vxA.Loai == LoaiViec.DoiNhan && vxA.Khu == -1 && vxA.DungY == 216 && vxA.DungX == 395 && vxB.DungX == 335
            && vxA.HetHan > DateTime.Now.AddMinutes(5), "clone xa nhanh: khu chinh, dung sat hai ben Leader (+-30 px), han ~6 phut");
        var tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(tinX == "ChuX: Clone dang toi canh Leader, moi lai sau ~10 giay",
            "bao Chu kho: moi lai Leader (KHONG bat giao vao clone), clone con dang toi: " + tinX);
        Xac(suKienX.Contains("[XẢ] ChuX mời lúc Leader đầy — Leader chuyển ngay sang XaA, XaB đứng cạnh"),
            "log de doc mo dot: " + suKienX.LastOrDefault());
        Bang(dx.MoTaXa, "Xả nhanh: Leader → XaA, XaB · đã chuyển 0 món", "thanh trang thai");

        // Clone chua toi -> Leader dung yen; don kho thuong KHONG chen vao (dang xa nhanh).
        DatField(dx, "_donNghiDen", DateTime.MinValue);
        nhipX();
        Xac(dx.ViecCua("xaL") == null && LayField(dx, "_don") == null, "clone chua toi -> Leader chua chuyen, khong goi luot don kho khac");
        DatField(dx, "_donNghiDen", DateTime.MaxValue);   // het dot thi don kho thuong khong xen vao cac buoc kiem sau

        // Clone dung canh chi nhan Leader, khong nhan nguoi choi.
        DatKhu(xA, 22, 3);
        DatKhu(xD, 22, 3);
        Xac(dx.XetLoiMoi(xA, 6077, null).TuChoi, "clone dung canh KHONG nhan Chu kho (nguoi choi chi giao voi Leader)");
        var qdAL = dx.XetLoiMoi(xA, 6001, null);
        Xac(qdAL.Nhan && qdAL.LaAi == "bot", "clone dung canh nhan loi moi cua Leader");
        Xac(dx.XetLoiMoi(xD, 6001, null).TuChoi, "clone ngoai dot KHONG nhan Leader");

        // xA toi khu nhung chua dung toi cho -> cho; toi cho -> Leader chuyen 12 o (chi tui, bo mon khoa).
        nhipX();
        Xac(dx.ViecCua("xaL") == null, "xA chua toi cho canh Leader -> chua chuyen");
        xA.GameState.MyChar.Cx = vxA.DungX;
        xA.GameState.MyChar.Cy = 216;
        nhipX();
        Xac(dx.ViecCua("xaL") == null, "Leader chua thay xA trong khu -> chua chuyen");
        // Server bo buoc di cua clone (test song 17/09): Leader thay xA o diem vao 420, xA tuong minh o 395.
        xL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 6002, Name = "XaA", X = 420, Y = 216 });
        var thayA = dx.TimNguoi("XaA", 3, xL);
        Xac(thayA != null && thayA.X == 420 && dx.TimNguoi("XaA", 3).X == vxA.DungX && dx.TimNguoi("XaA", 5, xL) == null,
            "Leader lai sat theo toa do NO thay (420), khong theo toa do clone tu bao (" + vxA.DungX + "); sai khu -> khong thay");
        nhipX();
        var vc1 = dx.ViecCua("xaL");
        Xac(vc1 != null && vc1.Loai == LoaiViec.GiaoMon && vc1.MucDich == "xa" && vc1.ChiTui && vc1.NguoiNhan == "XaA" && vc1.NguoiNhanLaBot
            && vc1.Dong.Count == 1 && vc1.Dong[0].Khoa.Tpl == 460 && !vc1.Dong[0].Khoa.Khoa && vc1.Dong[0].SoLuong == 12,
            "xA toi cho -> Leader chuyen 12 o (chi tui, khong mon khoa) sang XaA: " + (vc1 == null ? "null" : vc1.MoTa));

        // Chuyen xong: log de doc, dem, nhip sau chuyen tiep 8 mon con lai.
        for (int i = 0; i < 12; i++) xL.GameState.MyChar.BagItems[i] = new Item();
        var muoiHai = new Item[12];
        for (int i = 0; i < 12; i++) muoiHai[i] = Mon(460, 1);
        xA.GameState.MyChar.BagItems = Tui(30, muoiHai);
        chuyenXong(vc1, "XaA", 6002, muoiHai);
        nhipX();
        Xac(suKienX.Contains("[CHUYỂN] Leader XaLeader → XaA: 12 Thư tay"), "log de doc: Leader chuyen gi cho ai: " + suKienX.LastOrDefault());
        Xac(dx.MoTaXa.EndsWith("đã chuyển 12 món") && dx.ThongKe.DonMon == 12, "dem: da chuyen 12 mon (tinh vao don kho): " + dx.MoTaXa);
        var vc2 = dx.ViecCua("xaL");
        Xac(vc2 != null && vc2 != vc1 && vc2.NguoiNhan == "XaA" && vc2.TongConLai == 8, "nhip sau: chuyen tiep 8 mon con lai, khong cho");

        // Chu kho moi luc Leader co viec chuyen chua giao dich (Leader con 17 o) -> Leader nhan nguoi choi truoc.
        var qdN = dx.XetLoiMoi(xL, 6077, vc2);
        Xac(qdN.Nhan && qdN.LaAi == "chukho", "Chu kho moi luc Leader sap chuyen -> Leader van nhan");

        // Chu kho nap them -> gia han dot, chi bao "da nhan".
        var xa = LayField(dx, "_xa");
        xa.GetType().GetField("Den").SetValue(xa, DateTime.Now.AddMinutes(1));
        nap("ChuX", new[] { new MonGiaoDich { TemplateId = 457, Quantity = 12 }, new MonGiaoDich { TemplateId = 458, Upgrade = 5, Quantity = 1 } });
        nhipX();
        Xac((DateTime)xa.GetType().GetField("Den").GetValue(xa) > DateTime.Now.AddMinutes(4) && dx.ThongKe.NapMon == 13,
            "Chu kho nap them -> gia han dot 5 phut; thong ke nap 13 mon");
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(tinX.Contains("Da nhan 13 mon") && !tinX.Contains("Leader se chuyen"), "dot dang chay: chi bao da nhan: " + tinX);

        // Chuyen xong 8 mon: xA con 10 o (< 12 + o chua) -> roi di cat ruong, xC vao thay (dung cho xA); Chu kho khong bi lam phien.
        xL.GameState.MyChar.BagItems = tuiLeaderSau();
        var tam = new Item[8];
        for (int i = 0; i < 8; i++) tam[i] = Mon(460, 1);
        var haiMuoi = new Item[20];
        for (int i = 0; i < 20; i++) haiMuoi[i] = Mon(460, 1);
        xA.GameState.MyChar.BagItems = Tui(30, haiMuoi);
        chuyenXong(vc2, "XaA", 6002, tam);
        nhipX();
        Xac(vxA.BiHuy && dx.ViecCua("xaL") == null, "xA con 9 o nhan -> roi di cat ruong; xB chua toi -> chua chuyen");
        var vxC = dx.ViecCua("xaC");
        Xac(vxC != null && vxC.MucDich == "xa" && vxC.DungX == 395, "xC vao thay xA (dung cho xA vua roi)");
        Bang(Gop(RutTin(dx.Kenh, xL)), "", "doi clone khong nhan tin Chu kho");
        boViec(xA, vxA);

        // xB toi cho -> Leader chuyen 2 o (chong 12 da + ao +5); xB bao thieu o -> xB roi dot, tranh 10 phut.
        DatKhu(xB, 22, 3);
        xB.GameState.MyChar.Cx = vxB.DungX;
        xB.GameState.MyChar.Cy = 216;
        xL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 6003, Name = "XaB", X = 335, Y = 216 });
        nhipX();
        var vc3 = dx.ViecCua("xaL");
        Xac(vc3 != null && vc3.NguoiNhan == "XaB" && vc3.Dong.Count == 2 && vc3.TongConLai == 13, "Leader chuyen chong 12 da + ao +5 sang XaB");
        // Tui Leader vua doi (chong gop khi nap them) -> mode bao KHONG_CO_MON: khong tinh hong, lap lai theo tui that.
        dx.LayViec(xL);
        dx.BaoViec(xL, new BaoCaoViec { Viec = vc3, MaLoi = MaLoiViec.KHONG_CO_MON, LyDo = "tui khong con mon cua luot" });
        nhipX();
        xa = LayField(dx, "_xa");
        Xac((int)xa.GetType().GetField("HongLienTiep").GetValue(xa) == 0 && !vxB.BiHuy && dx.ViecCua("xaL") == null,
            "chuyen bao KHONG_CO_MON -> khong tinh hong, khong bo clone, nghi 1 giay");
        xa.GetType().GetField("NghiDen").SetValue(xa, DateTime.MinValue);
        nhipX();
        vc3 = dx.ViecCua("xaL");
        Xac(vc3 != null && vc3.NguoiNhan == "XaB" && vc3.TongConLai == 13, "nhip sau: lap lai luot chuyen sang XaB");
        dx.LayViec(xL);
        dx.BaoViec(xL, new BaoCaoViec { Viec = vc3, MaLoi = MaLoiGiaoDich.THIEU_O, LyDo = "doi phuong khong du o" });
        nhipX();
        var tranhX = (IDictionary)LayField(dx, "_xaTranh");
        Xac(vxB.BiHuy && tranhX.Contains("xaB") && dx.ViecCua("xaL") == null, "chuyen sang xB hong (thieu o) -> xB roi dot, tranh 10 phut");
        boViec(xB, vxB);

        // Lenh rut can hang tren clone dang dung canh -> clone nhuong; dot khong keo lai clone dang giu hang.
        xC.GameState.MyChar.BagItems[20] = Mon(463, 3);
        nhipX();
        dx.RutTuTool(463, -1, 3, "ChuX");
        nhipX();
        Xac(vxC.BiHuy, "lenh rut 463 (tren xC dang dung canh) -> xC nhuong lenh");
        boViec(xC, vxC);
        nhipX();
        var vxRut = dx.ViecCua("xaC");
        xa = LayField(dx, "_xa");
        var dsXa = (List<string>)xa.GetType().GetField("Clone").GetValue(xa);
        Xac(vxRut != null && vxRut.MucDich == "rut" && !dsXa.Contains("xaC"), "xC giao lenh rut; dot khong keo lai xC");
        var lX = dx.Hang.DangMo.FirstOrDefault();
        if (lX != null) dx.HuyLenhTuTool(lX.So);
        nhipX();
        boViec(xC, dx.ViecCua("xaC"));
        nhipX();
        RutTin(dx.Kenh, xL);

        // Lenh rut da huy -> xC het giu hang -> dot goi lai xC (van con 12 o nhan).
        xa = LayField(dx, "_xa");
        dsXa = (List<string>)xa.GetType().GetField("Clone").GetValue(xa);
        Xac(dsXa.Count == 1 && dsXa[0] == "xaC", "het lenh rut -> dot goi lai xC: " + string.Join(",", dsXa.ToArray()));

        // `xa xong`: Leader con 2 o, xC chua toi -> cho chuyen not; qua 60 giay chua xong -> dong (don kho thuong lo tiep).
        Goi(dx, "NhanChatRieng", xL, "ChuX", "xa xong");
        nhipX();
        Xac(LayField(dx, "_xa") != null && dx.ViecCua("xaL") == null, "xa xong nhung Leader con do, clone dang toi -> chua dong");
        xa.GetType().GetField("XongLuc").SetValue(xa, DateTime.Now.AddSeconds(-61));
        nhipX();
        Xac(LayField(dx, "_xa") == null && dx.MoTaXa == "" && dx.ViecCua("xaC").BiHuy, "qua 60 giay chua chuyen not -> dong dot, tha clone");
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(tinX.Contains("Xa xong (Chu kho bao xong): Leader da chuyen 20 mon sang clone"), "bao Chu kho khi xong: " + tinX);
        Xac(suKienX.Contains("[XẢ] Xong đợt xả (Chủ kho báo xong): Leader đã chuyển 20 món sang clone qua 2 lượt"),
            "log de doc xong dot: " + suKienX.LastOrDefault());
        foreach (var c in new[] { xA, xB, xC, xD }) boViec(c, dx.ViecCua(c.Config.Username));
        xA.GameState.MyChar.BagItems = Tui(30);
        tranhX.Clear();
        nhipX();
        Xac(LayField(dx, "_xa") == null && dx.ViecCua("xaL") == null, "het dot -> Leader khong tu chuyen tiep nua");

        // Lenh `xa`: goi clone san + bao danh sach. Chu kho roi khu -> chuyen not phan con lai roi dong.
        DatKhu(xA, 22, 5);
        DatKhu(xB, 22, 5);
        Goi(dx, "NhanChatRieng", xL, "ChuX", "xa");
        nhipX();
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(LayField(dx, "_xa") != null && tinX.Contains("Xa nhanh: XaA, XaB dung canh Leader. Cu giao Leader, xong nhan: xa xong"),
            "lenh xa -> goi clone san: " + tinX);
        var vxA2 = dx.ViecCua("xaA");
        var vxB2 = dx.ViecCua("xaB");
        xL.GameState.CurrentMap.OtherPlayers.Remove(chuX);
        nhipX();
        xa = LayField(dx, "_xa");
        xa.GetType().GetField("VangTu").SetValue(xa, DateTime.Now.AddSeconds(-61));
        nhipX();
        Xac(LayField(dx, "_xa") != null && dx.ViecCua("xaL") == null,
            "Chu kho vang 61 giay nhung Leader con 2 o chua chuyen, clone dang toi -> chua dong");
        DatKhu(xA, 22, 3);
        xA.GameState.MyChar.Cx = vxA2.DungX;
        nhipX();
        var vc4 = dx.ViecCua("xaL");
        Xac(vc4 != null && vc4.NguoiNhan == "XaA" && vc4.TongConLai == 13, "Chu kho vang: van chuyen not phan con lai");
        xL.GameState.MyChar.BagItems = TuiKhoa(30, 7);
        chuyenXong(vc4, "XaA", 6002, new[] { Mon(457, 12), Mon(458, 1, 5, false, false) });
        nhipX();
        Xac(LayField(dx, "_xa") == null && vxA2.BiHuy && vxB2.BiHuy, "chuyen het + Chu kho vang -> dong dot, tha clone");
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(tinX.Contains("Xa xong (Chu kho roi khu chinh): Leader da chuyen 13 mon sang clone"), "dot mo bang lenh -> bao khi dong: " + tinX);
        boViec(xA, vxA2);
        boViec(xB, vxB2);
        nhipX();

        // Chu kho quay lai nap -> tu mo dot (khong can lenh), nhac mot lan; tat don kho thi khong.
        xL.GameState.CurrentMap.OtherPlayers.Add(chuX);
        cfgX.BatDonKho = false;
        nap("ChuX", new[] { new MonGiaoDich { TemplateId = 457, Quantity = 5 } });
        nhipX();
        Xac(LayField(dx, "_xa") == null, "tat don kho -> nap khong mo xa nhanh");
        cfgX.BatDonKho = true;
        RutTin(dx.Kenh, xL);
        nap("NguoiLa", new[] { new MonGiaoDich { TemplateId = 457, Quantity = 5 } });
        nhipX();
        Xac(LayField(dx, "_xa") == null, "nguoi la nap -> khong mo xa nhanh");
        nap("ChuX", new[] { new MonGiaoDich { TemplateId = 457, Quantity = 5 } });
        nhipX();
        Xac(LayField(dx, "_xa") != null && dx.ViecCua("xaA") != null && dx.ViecCua("xaA").MucDich == "xa", "Chu kho nap -> tu mo xa nhanh");
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(tinX.Contains("ChuX: Leader se chuyen do sang clone ~3s/luot. Bao dang giao dich thi moi lai"), "nhac Chu kho mot lan: " + tinX);
        Xac(suKienX.Any(x => x.StartsWith("[XẢ] ChuX đang nạp — Leader chuyển ngay sang XaA")), "log de doc: tu mo khi nap");

        // Dong dot giua luot chuyen: viec Leader bi huy (phien chua mo thi thoi), clone dang nhan giu them 30 giay.
        var tl5 = TuiKhoa(30, 7);
        tl5[23] = Mon(460, 1);
        xL.GameState.MyChar.BagItems = tl5;
        var vxA5 = dx.ViecCua("xaA");
        var vxB5 = dx.ViecCua("xaB");
        DatKhu(xA, 22, 3);
        xA.GameState.MyChar.Cx = vxA5.DungX;
        nhipX();
        var vc5 = dx.ViecCua("xaL");
        Xac(vc5 != null && vc5.NguoiNhan == "XaA" && vc5.TongConLai == 1, "dot tu mo: chuyen tiep 1 mon sang XaA");
        cfgX.BatNap = false;
        nhipX();
        Xac(LayField(dx, "_xa") == null && vc5.BiHuy && !vxA5.BiHuy && vxA5.HetHan < DateTime.Now.AddSeconds(31) && vxB5.BiHuy,
            "dong dot giua luot chuyen: huy viec Leader, clone dang nhan giu them 30 giay, clone kia tha ngay");
        cfgX.BatNap = true;
        dx.LayViec(xL);
        dx.BaoViec(xL, new BaoCaoViec { Viec = vc5, MaLoi = MaLoiViec.BI_HUY, LyDo = "xong xa nhanh" });
        boViec(xA, vxA5);
        boViec(xB, vxB5);
        xL.GameState.MyChar.BagItems = TuiKhoa(30, 7);
        DatKhu(xA, 22, 5);
        nhipX();

        // Review 17/09: tui Leader chi con do KHONG chuyen tiep duoc (Rac) -> Chu kho moi: khong mo dot, bao dang don kho;
        // dang co dot thi don kho thuong van chay (truoc day dot chan XuLyDon -> kho dung im).
        cfgX.DatRac(461, true);
        var tlR = TuiKhoa(30, 7);
        for (int i = 23; i < 30; i++) tlR[i] = Mon(461, 1);
        xL.GameState.MyChar.BagItems = tlR;
        nhipX();
        RutTin(dx.Kenh, xL);
        Xac(dx.XetLoiMoi(xL, 6077, null).HuyKhiMo != null, "Leader het cho (toan Rac) -> van nhan roi huy");
        nhipX();
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(LayField(dx, "_xa") == null && tinX == "ChuX: Leader dang don kho, moi lai sau ~15 giay",
            "toan do Rac -> khong goi clone dung canh, bao dang don kho: " + tinX);
        // Dang giu cua (`nap`) cho ChuX ma Leader het cho -> bo giu cua (giu chi chan chuyen tiep / don kho toi het gio).
        DatField(dx, "_giuCuaCho", "ChuX");
        DatField(dx, "_giuCuaDen", DateTime.Now.AddSeconds(60));
        var qdG = dx.XetLoiMoi(xL, 6077, null);
        Xac(qdG.Nhan && qdG.HuyKhiMo != null && LayField(dx, "_giuCuaCho") == null,
            "dang giu cua cho ChuX ma Leader het cho -> nhan roi huy + bo giu cua");
        nhipX();
        RutTin(dx.Kenh, xL);
        // Log de doc: Chu kho moi lien tuc luc Leader het cho -> chi mot dong moi phut.
        Action<string> huyHetCho = delegate(string ten)
        {
            var p = new PhienGiaoDich(xL, VaiGiaoDich.Nhan, 6077, ten, 120000, 0, 0, null, null, null);
            DatProp(p, "KetQua", new KetQuaGiaoDich
            {
                ThanhCong = false, MaLoi = MaLoiGiaoDich.HET_CHO, LyDo = "Leader con 0 o", Vai = VaiGiaoDich.Nhan,
                DoiPhuong = ten, DoiPhuongId = 6077,
            });
            dx.BaoPhien(xL, p, null);
        };
        int hetChoTruoc = suKienX.Count(z => z.Contains("không thành: Leader hết chỗ"));
        huyHetCho("ChuX");
        huyHetCho("ChuX");
        huyHetCho("ChuX");
        nhipX();
        Bang(suKienX.Count(z => z.Contains("không thành: Leader hết chỗ")) - hetChoTruoc, 1, "3 lan nhan roi huy lien -> log de doc 1 dong");
        Goi(dx, "NhanChatRieng", xL, "ChuX", "xa");
        DatField(dx, "_donNghiDen", DateTime.MinValue);
        DatField(dx, "_moiDenLeaderLuc", DateTime.MinValue);   // ca "Leader van nhan" o tren vua dat moc (< 10 giay)
        nhipX();
        Xac(LayField(dx, "_xa") != null && (LayField(dx, "_don") != null || (DateTime)LayField(dx, "_donNghiDen") > DateTime.Now),
            "dang co dot nhung tui Leader chi con Rac -> don kho thuong VAN chay");
        var dR = LayField(dx, "_don");
        if (dR != null)
        {
            var vcR = (Viec)dR.GetType().GetField("ViecClone").GetValue(dR);
            DatField(dx, "_don", null);
            foreach (var c in new[] { xA, xB, xC, xD })
                if (dx.ViecCua(c.Config.Username) == vcR) { vcR.Huy("kiem"); boViec(c, vcR); }
        }
        DatField(dx, "_donNghiDen", DateTime.MaxValue);
        cfgX.DatRac(461, false);
        xL.GameState.MyChar.BagItems = TuiKhoa(30, 7);
        RutTin(dx.Kenh, xL);
        nap("ChuX", new[] { new MonGiaoDich { TemplateId = 457, Quantity = 5 } });
        nhipX();
        RutTin(dx.Kenh, xL);

        // Phien bi DOI PHUONG / server huy (ma "BI_HUY" cua phien, vd M24) -> tinh la hong; viec bi dieu phoi huy -> trung tinh.
        xa = LayField(dx, "_xa");
        var hongF = xa.GetType().GetField("HongLienTiep");
        hongF.SetValue(xa, 0);
        var vGia = new Viec { Loai = LoaiViec.GiaoMon, Acc = "xaL", MucDich = "xa" };
        Goi(dx, "SauViecChuyen", xa, vGia, new BaoCaoViec { Viec = vGia, MaLoi = MaLoiGiaoDich.BI_HUY, LyDo = "doi phuong huy" }, DateTime.Now);
        Bang((int)hongF.GetValue(xa), 1, "phien bi doi phuong / server huy -> tinh 1 lan hong");
        vGia.Huy("dong dot");
        Goi(dx, "SauViecChuyen", xa, vGia, new BaoCaoViec { Viec = vGia, MaLoi = MaLoiViec.BI_HUY, LyDo = "dong dot" }, DateTime.Now);
        Bang((int)hongF.GetValue(xa), 1, "viec bi bo dieu phoi huy -> trung tinh");
        hongF.SetValue(xa, 0);
        xa.GetType().GetField("NghiDen").SetValue(xa, DateTime.MinValue);

        // Leader vao lai game khong thay clone dung san (D52) -> qua 10 giay doi clone khac.
        xL.GameState.CurrentMap.OtherPlayers.RemoveAll(p => p.CharId == 6002 || p.CharId == 6003);
        var vxB6 = dx.ViecCua("xaB");
        Xac(vxB6 != null && vxB6.MucDich == "xa", "dot co xB dung canh");
        DatKhu(xB, 22, 3);
        xB.GameState.MyChar.Cx = vxB6.DungX;
        nhipX();
        var kThay = (IDictionary)xa.GetType().GetField("KhongThayTu").GetValue(xa);
        Xac(kThay.Contains("xaB") && !vxB6.BiHuy && dx.ViecCua("xaL") == null, "xB san sang nhung Leader khong thay -> cho, ghi moc");
        kThay["xaB"] = DateTime.Now.AddSeconds(-11);
        nhipX();
        Xac(vxB6.BiHuy && ((IDictionary)LayField(dx, "_xaTranh")).Contains("xaB") && !kThay.Contains("xaB"),
            "Leader khong thay xB qua 10 giay (vua vao lai game) -> doi clone khac, tranh xB 2 phut");
        boViec(xB, vxB6);
        DatKhu(xB, 22, 5);
        nhipX();

        // Het gio khong nap them -> dong; dot co lenh `xa` thi bao Chu kho.
        xa = LayField(dx, "_xa");
        xa.GetType().GetField("Den").SetValue(xa, DateTime.Now.AddSeconds(-1));
        nhipX();
        Xac(LayField(dx, "_xa") == null, "het 5 phut khong nap -> dong dot");
        tinX = Gop(RutTin(dx.Kenh, xL));
        Xac(tinX.Contains("Da dong xa nhanh (5 phut khong nap them)"), "dot co lenh xa -> bao khi dong: " + tinX);
        foreach (var c in new[] { xA, xB, xC, xD }) boViec(c, dx.ViecCua(c.Config.Username));
        nhipX();
    }

    // ================= 11. KhoDieuPhoi (tich hop, khong mang) =================

    static List<string> RutTin(KenhChat k, NsoClient bot)
    {
        var r = new List<string>();
        var dict = (IDictionary)LayField(k, "_rieng");
        if (!dict.Contains(bot)) return r;
        var q = dict[bot];
        var qt = q.GetType();
        int n = (int)qt.GetProperty("Count").GetValue(q, null);
        var deq = qt.GetMethod("Dequeue");
        for (int i = 0; i < n; i++)
        {
            var t = deq.Invoke(q, null);
            r.Add(t.GetType().GetField("Den").GetValue(t) + ": " + t.GetType().GetField("NoiDung").GetValue(t));
        }
        return r;
    }

    static List<string> RutCongDong(KenhChat k)
    {
        var q = (Queue<string>)LayField(k, "_suKien");
        var r = new List<string>(q);
        q.Clear();
        return r;
    }

    static void ChanGui(KenhChat k, NsoClient bot)
    {
        ((IDictionary)LayField(k, "_riengLuc"))[bot] = DateTime.MaxValue;
        DatField(k, "_congDongLuc", DateTime.MaxValue);
    }

    static string Gop(List<string> ds) { return string.Join(" || ", ds.ToArray()); }

    static void KiemDieuPhoi()
    {
        Console.WriteLine("=== KhoDieuPhoi: vai, loi moi, lenh chat, hang cho, don kho ===");
        var cL = TaoClient("leader1", "KhoTong", 1001, Tui(30, Mon(457, 5)), null, 0, "Kiem");
        var cB = TaoClient("backup1", "KhoPhong", 1002, Tui(30), null, 0, "Kiem");
        var c1 = TaoClient("clone1", "Kho01", 1003, Tui(30, Mon(457, 20)), Tui(30, Mon(458, 1, 8, false, false)), 0, "Kiem");
        var c2 = TaoClient("clone2", "Kho02", 1004, Tui(30, Mon(460, 1)), Tui(30), 0, "MayKhac");
        var nha = new AccountConfig { Username = "clone9", ServerName = "Kiem" };
        var accs = new List<AccountConfig> { cL.Config, cB.Config, c1.Config, c2.Config, nha };
        foreach (var c in new[] { cL, cB, c1, c2 }) DatKhu(c, 22, 5);
        DatKhu(cL, 22, 3);
        cL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 77, Name = "ChuA", X = 300, Y = 200 });
        cL.GameState.CurrentMap.OtherPlayers.Add(new PlayerInfo { CharId = 88, Name = "NguoiLa", X = 320, Y = 200 });

        var fleet = new FleetManager();
        var ds = (List<NsoClient>)LayField(fleet, "_clients");
        ds.AddRange(new[] { cL, cB, c1, c2 });

        var cfg = new KhoConfig { Leader = "leader1", LeaderDuPhong = "backup1", KhuChinh = 3, KhuPhu = 5 };
        cfg.DatChuKho(new[] { "ChuA" });
        cfg.DatNha("clone9", true);
        cfg.BatCatRuong = false;   // bat lai o phan don kho
        cfg.BatDonKho = false;
        cfg.RaoBat = false;
        var dp = new KhoDieuPhoi(fleet, delegate { return accs; }, cfg);
        // Client gia "vao game" ngay nhip dau -> bo cho 90 giay (server chan loi moi toi nguoi moi vao game).
        var choVao = typeof(KhoDieuPhoi).GetField("_choSauVaoGameGiay", BindingFlags.NonPublic | BindingFlags.Static);
        choVao.SetValue(null, 0);
        Action nhip = delegate { Goi(dp, "Nhip"); };
        nhip();
        foreach (var c in new[] { cL, cB, c1, c2 }) ChanGui(dp.Kenh, c);

        // ---- vai ----
        Bang(dp.LeaderAcc, "leader1", "Leader chinh online -> la Leader");
        Bang(dp.VaiCuaAcc("leader1"), VaiKho.Leader, "vai leader1");
        Bang(dp.VaiCuaAcc("backup1"), VaiKho.DuPhong, "vai backup1");
        Bang(dp.VaiCuaAcc("clone1"), VaiKho.Clone, "vai clone1");
        Bang(dp.VaiCuaAcc("clone2"), VaiKho.KhongThuoc, "clone2 khac may chu -> khong thuoc kho (D11)");
        Bang(dp.VaiCuaAcc("clone9"), VaiKho.KhongThuoc, "clone dang nha -> khong thuoc");

        // ---- khu dung cho ----
        Bang(dp.KhuDungCho(cL), 3, "Leader dung khu chinh");
        Bang(dp.KhuDungCho(c1), 5, "clone dung khu phu");
        cfg.KhuPhu = -1;
        Bang(dp.KhuDungCho(c1), 5, "chua cai khu phu -> clone giu khu dang dung luc vao (5)");
        DatKhu(c1, 22, 3);
        Bang(dp.KhuDungCho(c1), 5, "da nho khu -> sang khu chinh roi van ve 5");
        DatKhu(cB, 22, 3);
        Bang(dp.KhuDungCho(cB), 5, "vao game ngay khu chinh -> ve khu clone khac dang o (5)");
        var nhaRieng = (System.Collections.Concurrent.ConcurrentDictionary<string, int>)LayField(dp, "_khuNha");
        nhaRieng.Clear();
        Bang(dp.KhuDungCho(cB), 2, "vao game o khu chinh, chua ai co khu -> khu chinh - 1");
        nhaRieng.Clear();
        cfg.KhuPhu = 5;
        DatKhu(c1, 22, 5);
        DatKhu(cB, 22, 5);
        var choVaoDict = (IDictionary)LayField(dp, "_vaoGameLuc");
        Xac(choVaoDict.Contains("clone1") && !choVaoDict.Contains("clone9"), "ghi luc vao game cua client dang InGame");

        // ---- so kho / bang tong ----
        var d457 = dp.BangTon.FirstOrDefault(x => x.Khoa.Tpl == 457);
        Xac(d457 != null && d457.Tong == 25, "tong kho 457 = 5 (Leader) + 20 (clone1)");
        Xac(!dp.BangTon.Any(x => x.Khoa.Tpl == 460), "do tren acc khac may chu khong tinh");
        var sc = dp.SucChuaKho;
        Xac(sc.Tong == 60 && sc.Dung == 2 && !sc.ChuaDu, "suc chua chi tinh clone1: 60 o, dung 2 (" + sc.Dung + "/" + sc.Tong + ")");

        // ---- loi moi ----
        var qd = dp.XetLoiMoi(cL, 77, null);
        Xac(qd.Nhan && qd.LaAi == "chukho" && qd.TenMongDoi == "ChuA", "Leader nhan loi moi cua Chu kho");
        qd = dp.XetLoiMoi(cL, 88, null);
        Xac(qd.Nhan && qd.LaAi == "nguoila", "che do Tat ca -> nhan nguoi la");
        cfg.CheDoNhan = CheDoNhan.ChiChuKho;
        qd = dp.XetLoiMoi(cL, 88, null);
        Xac(!qd.Nhan && qd.TuChoi, "che do Chi Chu kho -> tu choi nguoi la (56)");
        qd = dp.XetLoiMoi(cL, 12345, null);
        Xac(qd.Nhan && qd.TenMongDoi == null && qd.KiemTen != null && qd.KiemTen("NguoiLa") != null && qd.KiemTen("ChuA") == null,
            "id khong thay trong khu -> van nhan, kiem ten o goi 37");
        cfg.CheDoNhan = CheDoNhan.TatCa;
        qd = dp.XetLoiMoi(c1, 77, null);
        Xac(!qd.Nhan && qd.TuChoi, "clone khong nhan loi moi cua nguoi");
        qd = dp.XetLoiMoi(c1, 1001, null);
        Xac(!qd.Nhan && qd.TuChoi, "clone khong co viec doi nhan -> tu choi ca Leader");
        cfg.NguongNhan = 30;
        qd = dp.XetLoiMoi(cL, 88, null);
        Xac(!qd.Nhan && qd.TuChoi, "tui Leader duoi nguong nhan -> tu choi");
        cfg.NguongNhan = 12;
        cfg.BatNap = false;
        Xac(dp.XetLoiMoi(cL, 77, null).TuChoi, "tat nhan do -> tu choi ca Chu kho");
        cfg.BatNap = true;
        Xac(dp.KiemHangNhanThem(cL, 0, new MonGiaoDich[30]) != null, "doi phuong dat 30 mon, tui con 29 -> tu choi");
        Xac(dp.KiemHangNhanThem(cL, 2000000000, new MonGiaoDich[0]) == null, "nhan 2 ty xu khi dang 0 -> duoc");
        Xac(dp.KiemHangNhanThem(cL, 2000000001, new MonGiaoDich[0]) != null, "vuot tran 2 ty -> tu choi (T14)");
        Xac(dp.KiemHangNhanThem(cL, 0, new MonGiaoDich[0]) == null, "ben GIAO: nguoi nhan khoa rong -> hop le");
        Xac(dp.KiemHangNap(cL, 0, new MonGiaoDich[0]) != null, "Leader nhan nap: khoa rong, 0 xu -> tu choi (test song)");
        Xac(dp.KiemHangNap(cL, 5, new MonGiaoDich[0]) == null, "nap chi xu -> nhan");
        qd = dp.XetLoiMoi(cL, 77, null);
        Xac(qd.Nhan && qd.KiemHang != null && qd.KiemHang(0, new MonGiaoDich[0]) != null, "loi moi nap cua Chu kho: khoa rong -> tu choi");
        RutTin(dp.Kenh, cL);

        // ---- tim nguoi ----
        var ng = dp.TimNguoi("chua");
        Xac(ng != null && ng.CharId == 77, "TimNguoi qua mat Leader (khu chinh)");
        Xac(dp.TimNguoi("Kho01") == null, "clone o khu phu -> khong tinh la co mat");
        DatKhu(c1, 22, 3);
        ng = dp.TimNguoi("Kho01");
        Xac(ng != null && ng.CharId == 1003 && dp.TimNguoiTheoAcc("clone1") != null, "clone sang khu chinh -> tim thay bang chinh no");
        DatKhu(c1, 22, 5);

        // ---- lenh chat ----
        Action<string, string> chat = delegate(string tu, string noiDung) { Goi(dp, "NhanChatRieng", cL, tu, noiDung); nhip(); };
        chat("NguoiLa", "kho");
        Bang(RutTin(dp.Kenh, cL).Count, 0, "nguoi la nhan 'kho' -> im lang (D24)");
        chat("ChuA", "@321 kho");
        var tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count == 1 && tin[0].StartsWith("ChuA: Kho: 1/1 clone online, 2/60 o, 0 lenh cho"), "Chu kho 'kho' -> tom tat: " + Gop(tin));
        chat("ChuA", "tim da");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count == 1 && tin[0].Contains("457 Đá cấp 5 x25"), "tim da -> " + Gop(tin));
        chat("ChuA", "co 457");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count == 1 && tin[0].Contains("457 Đá cấp 5: 25 (2 acc)"), "co 457 -> " + Gop(tin));
        chat("ChuA", "lay 457 x");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count == 1 && tin[0].Contains("Sai cu phap"), "sai cu phap -> huong dan: " + Gop(tin));

        // lenh thieu hang -> tam dung -> tiep
        chat("ChuA", "lay 457 30");
        var l = dp.Hang.DangMo.FirstOrDefault();
        Xac(l != null && l.TrangThai == TrangThaiLenh.TamDung && l.ChuKho == "ChuA" && l.NguoiNhan == "ChuA", "lay 30 khi kho co 25 -> tam dung");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count >= 1 && Gop(tin).Contains("Thieu") && Gop(tin).Contains("tiep #" + l.So), "bao thieu + cach tiep: " + Gop(tin));
        Xac(tin.All(x => x.Length <= "ChuA: ".Length + 90), "tin dai duoc chia doan <= 90 ky tu");
        Xac(RutCongDong(dp.Kenh).Count >= 1, "su kien lenh ra chat cong dong");
        chat("ChuA", "tiep");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count >= 1 && tin[0].Contains("Tiep lenh #" + l.So), "tiep -> " + Gop(tin));
        nhip();
        var v = dp.ViecCua("clone1");
        Xac(v != null && v.Loai == LoaiViec.GiaoMon && v.MucDich == "rut" && v.TongConLai == 20 && v.CacLenh.Contains(l.So),
            "nguoi nhan co mat -> giao viec cho clone1 (giu nhieu nhat, 20)");
        Bang(l.CloneDangGiao, "clone1", "lenh ghi acc dang giao");
        Xac(dp.LayViec(c1) == v && dp.LayViec(c1) == null, "LayViec tra viec mot lan duy nhat");
        Xac(dp.ViecCua("leader1") == null, "moi luc mot viec rut (Leader chua duoc giao)");
        RutTin(dp.Kenh, cL);

        // nguoi nhan thieu o -> tam dung
        dp.BaoViec(c1, new BaoCaoViec { Viec = v, MaLoi = MaLoiGiaoDich.THIEU_O, LyDo = "nguoi nhan thieu o" });
        nhip();
        Xac(l.TrangThai == TrangThaiLenh.TamDung && l.CloneDangGiao == null && dp.ViecCua("clone1") == null, "THIEU_O -> tam dung lenh, go viec");
        tin = RutTin(dp.Kenh, cL);
        Xac(Gop(tin).Contains("thieu o hanh trang") && Gop(tin).Contains("tiep #" + l.So), "bao thieu o: " + Gop(tin));
        chat("ChuA", "tiep #" + l.So);
        nhip();
        v = dp.ViecCua("clone1");
        Xac(v != null && dp.LayViec(c1) == v, "tiep -> giao lai clone1");

        // giao xong 20 tu clone1
        var p = new PhienGiaoDich(c1, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, null, null, null);
        var kq = new KetQuaGiaoDich { ThanhCong = true, Vai = VaiGiaoDich.Giao, DoiPhuong = "ChuA", DoiPhuongId = 77 };
        kq.MonDua.Add(new KeyValuePair<byte, Item>(0, Mon(457, 20)));
        DatProp(p, "KetQua", kq);
        dp.BaoPhien(c1, p, v);
        c1.GameState.MyChar.BagItems[0] = new Item();
        dp.BaoViec(c1, new BaoCaoViec { Viec = v, Xong = true });
        nhip();
        Xac(l.TongDaGiao == 20 && l.DangMo, "ghi da giao 20, lenh con mo (con 5 tren Leader)");
        var vL = dp.ViecCua("leader1");
        Xac(vL != null && vL.TongConLai == 5 && dp.LayViec(cL) == vL, "phan con lai giao cho Leader (5)");
        var p2 = new PhienGiaoDich(cL, VaiGiaoDich.Giao, 77, "ChuA", 120000, 31000, 0, null, null, null);
        var kq2 = new KetQuaGiaoDich { ThanhCong = true, Vai = VaiGiaoDich.Giao, DoiPhuong = "ChuA", DoiPhuongId = 77 };
        kq2.MonDua.Add(new KeyValuePair<byte, Item>(0, Mon(457, 5)));
        DatProp(p2, "KetQua", kq2);
        dp.BaoPhien(cL, p2, vL);
        cL.GameState.MyChar.BagItems[0] = new Item();
        dp.BaoViec(cL, new BaoCaoViec { Viec = vL, Xong = true });
        nhip();
        nhip();
        Xac(!l.DangMo && l.TrangThai == TrangThaiLenh.Xong && l.TongDaGiao == 25, "du 25 (chap nhan thieu) -> XONG");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Any(x => x.Contains("Xong lenh #" + l.So + ": da giao 25/30 mon")), "bao xong lenh (giao thieu ghi 25/30): " + Gop(tin));
        Bang(dp.ThongKe.XuatMon, 25, "thong ke xuat 25 mon");

        // cap mon + nguoi nhan vang mat + huy
        cL.GameState.MyChar.BagItems[1] = Mon(458, 1, 0, false, false);
        nhip();
        chat("ChuA", "lay 458");
        tin = RutTin(dp.Kenh, cL);
        Xac(Gop(tin).Contains("huy") && Gop(tin).Contains("nhieu cap"), "lay 458 (co +0 va +8) -> huy, bat ghi cap: " + Gop(tin));
        chat("ChuA", "lay 458 +8 cho Xyz");
        var l2 = dp.Hang.DangMo.FirstOrDefault();
        Xac(l2 != null && l2.NguoiNhan == "Xyz" && l2.TrangThai == TrangThaiLenh.ChoCoMat && l2.DaBaoCho, "lay cho nguoi khac, nguoi do chua toi -> cho");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count >= 1 && tin.All(x => x.StartsWith("ChuA:")) && Gop(tin).Contains("khu 3"), "chi Chu kho nhan tin (Xyz la nguoi la): " + Gop(tin));
        chat("ChuA", "huy");
        Xac(l2 != null && l2.TrangThai == TrangThaiLenh.Huy, "huy -> huy lenh cua chinh minh");
        RutTin(dp.Kenh, cL);
        chat("ChuA", "huy");
        tin = RutTin(dp.Kenh, cL);
        Xac(tin.Count == 1 && tin[0].Contains("khong co lenh"), "huy khi khong con lenh: " + Gop(tin));

        // theo doi
        chat("ChuA", "theo 457 10");
        Xac(cfg.TheoDoi.Any(m => m.Tpl == 457 && m.Nguong == 10 && m.ChuKho == "ChuA"), "theo 457 10 -> luu muc theo doi");
        chat("ChuA", "botheo 457");
        Xac(!cfg.TheoDoi.Any(m => m.Tpl == 457), "botheo 457");
        RutTin(dp.Kenh, cL);

        // nap / giu cua
        chat("ChuA", "nap");
        tin = RutTin(dp.Kenh, cL);
        Xac(dp.GiuCuaCho == "ChuA" && tin.Count == 1 && tin[0].Contains("San sang nhan"), "nap -> giu cua cho ChuA: " + Gop(tin));
        Xac(dp.XetLoiMoi(cL, 88, null).TuChoi, "dang giu cua -> tu choi nguoi la");
        Xac(dp.XetLoiMoi(cL, 77, null).Nhan, "dang giu cua -> nhan dung Chu kho do");
        DatField(dp, "_giuCuaCho", null);

        // ---- don xu (D45) ----
        cL.GameState.CurrentMap.OtherPlayers.Clear();   // Chu kho da roi khu -> duoc don
        DatField(dp, "_moiDenLeaderLuc", DateTime.MinValue);
        cL.GameState.MyChar.Xu = 1500000000;
        nhip();
        var vx = dp.ViecCua("clone1");
        var don = LayField(dp, "_don");
        Xac(vx != null && vx.Loai == LoaiViec.DoiNhan && vx.TuBotAcc == "leader1" && don != null
            && (string)LayField(don, "MucDich") == "donxu" && (int)LayField(don, "Xu") == 500000000,
            "Leader 1,5 ty > nguong 1 ty -> goi clone1 sang nhan 500 trieu");
        var qdBot = dp.XetLoiMoi(c1, 1001, vx);
        Xac(qdBot.Nhan && qdBot.LaAi == "bot" && qdBot.TenMongDoi == "KhoTong", "clone dang doi nhan -> nhan loi moi cua Leader");
        Xac(dp.ViecCua("leader1") == null, "clone chua toi khu chinh -> Leader chua giao");
        DatKhu(c1, 22, 3);
        nhip();
        var vxL = dp.ViecCua("leader1");
        Xac(vxL != null && vxL.MucDich == "donxu" && vxL.Xu == 500000000 && vxL.NguoiNhanLaBot && vxL.NguoiNhan == "Kho01" && vxL.Dong.Count == 0,
            "clone toi khu chinh -> Leader nhan viec giao 500 trieu xu");
        dp.LayViec(cL);
        cL.GameState.MyChar.Xu = 1000000000;
        dp.BaoViec(cL, new BaoCaoViec { Viec = vxL, Xong = true });
        nhip();
        Xac(LayField(dp, "_don") == null && vx.BiHuy, "giao xu xong -> huy viec doi nhan, het luot");
        nhip();
        Xac(dp.ViecCua("clone1") == null, "viec doi nhan chua lay ma bi huy -> da go");
        DatKhu(c1, 22, 5);

        // ---- don kho: cat ruong Leader, ruong day thi chuyen sang clone theo ke ----
        cfg.BatCatRuong = true;
        cfg.BatDonKho = true;
        var bagL = cL.GameState.MyChar.BagItems;
        bagL[0] = Mon(460, 1);
        bagL[2] = Mon(460, 1);
        bagL[3] = Mon(460, 1);
        nhip();
        var vc = dp.ViecCua("leader1");
        Xac(vc != null && vc.Loai == LoaiViec.CatRuong, "tui Leader co do, ruong chua doc -> viec cat ruong");
        dp.LayViec(cL);
        cL.GameState.MyChar.BoxItems = Tui(2, Mon(460, 1), Mon(460, 1));
        dp.BaoViec(cL, new BaoCaoViec { Viec = vc, Xong = true });
        nhip();
        var vd = dp.ViecCua("clone1");
        Xac(vd != null && vd.Loai == LoaiViec.DoiNhan && dp.ViecCua("leader1") == null, "ruong Leader con do ke Khac -> goi clone1 (ke Khac) sang nhan");
        nhip();
        Xac(dp.ViecCua("leader1") == null, "clone con o khu phu -> Leader cho, chua giao");
        DatKhu(c1, 22, 3);
        nhip();
        var vdL = dp.ViecCua("leader1");
        Xac(vdL != null && vdL.MucDich == "don" && vdL.Dong.Count == 1 && vdL.Dong[0].Khoa.Tpl == 460 && vdL.Dong[0].SoLuong == 5,
            "luot 1: Leader gom CA 3 thu tay trong tui lan 2 trong ruong giao clone1 (test song: truoc day 3 luot)");
        dp.LayViec(cL);
        cL.GameState.MyChar.BoxItems = Tui(2);
        bagL[0] = new Item();
        bagL[2] = new Item();
        bagL[3] = new Item();
        for (int i = 0; i < 5; i++) c1.GameState.MyChar.BagItems[i] = Mon(460, 1);
        dp.BaoViec(cL, new BaoCaoViec { Viec = vdL, Xong = true });
        nhip();
        Xac(vd.BiHuy && LayField(dp, "_don") == null, "het mon ke Khac -> huy viec doi nhan, xong don");
        Xac(dp.ViecCua("leader1") == null || dp.ViecCua("leader1").MucDich != "don", "khong co luot don thu hai");
        DatKhu(c1, 22, 5);
        nhip();
        var vcc = dp.ViecCua("clone1");
        Xac(vcc != null && vcc.Loai == LoaiViec.CatRuong, "clone1 ve khu phu sau khi nhan -> tu cat ruong");
        var vb = dp.ViecCua("backup1");
        Xac(vb != null && vb.Loai == LoaiViec.DocRuong, "du phong chua doc ruong -> doc ruong");

        // ---- nha clone (§6.2) ----
        Xac(dp.NhaClone(cL.Config) != null, "khong cho nha Leader");
        dp.LayViec(c1);
        Xac(dp.NhaClone(c1.Config) == null, "xep hang nha clone1");
        nhip();
        Xac(!cfg.DaNha("clone1") && vcc.BiHuy, "clone1 dang cat ruong -> huy viec noi bo, cho bao ve");
        dp.BaoViec(c1, new BaoCaoViec { Viec = vcc, MaLoi = MaLoiViec.BI_HUY, LyDo = "mode dung" });
        nhip();
        Xac(cfg.DaNha("clone1") && dp.VaiCuaAcc("clone1") == VaiKho.KhongThuoc && !fleet.Clients.Contains(c1),
            "xong viec -> dang xuat clone1 + danh dau DA NHA");
        nhip();
        var tonNha = dp.BangTon.FirstOrDefault(x => x.Khoa.Tpl == 458 && x.Khoa.Up == 8);
        Xac(tonNha != null && tonNha.TrenAccNha && tonNha.Tong == 0, "hang tren clone nha: van hien, khong tinh vao tong");
        Xac(KhoConfig.Nap().DaNha("clone1"), "trang thai nha duoc luu file");

        // ---- Leader du phong (§4) ----
        dp.LayViec(cB);
        cB.GameState.MyChar.BoxItems = Tui(10);
        dp.BaoViec(cB, new BaoCaoViec { Viec = vb, Xong = true });
        cL.State = ClientState.Disconnected;
        nhip();
        Bang(dp.LeaderAcc, "leader1", "Leader vua roi -> chua doi ngay");
        DatField(dp, "_chinhVangTu", DateTime.Now.AddSeconds(-61));
        RutCongDong(dp.Kenh);
        nhip();
        Bang(dp.LeaderAcc, "backup1", "vang qua 60 giay -> du phong len Leader");
        Bang(dp.VaiCuaAcc("backup1"), VaiKho.Leader, "vai backup1 = Leader");
        Xac(RutCongDong(dp.Kenh).Any(x => x.Contains("Leader moi: KhoPhong")), "bao doi Leader ra chat cong dong");
        cL.State = ClientState.InGame;
        nhip();
        Bang(dp.LeaderAcc, "backup1", "Leader chinh vua ve -> doi 10 giay");
        DatField(dp, "_chinhVeTu", DateTime.Now.AddSeconds(-11));
        nhip();
        Bang(dp.LeaderAcc, "leader1", "sau 10 giay, du phong ranh -> tra vai cho Leader chinh");
    }
}
