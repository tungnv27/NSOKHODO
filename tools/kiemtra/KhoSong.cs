using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using NSOKHODO;
using NSOKHODO.Auto;
using NSOKHODO.Client;
using NSOKHODO.Config;
using NSOKHODO.Fleet;
using NSOKHODO.Kho;
using NSOKHODO.Logging;
using NSOKHODO.Protocol;
using NSOKHODO.UI;

// CHAY KHO THAT TREN SERVER, KHONG GIAO DIEN — dung de test song (khong nam trong chay.ps1).
// Dung y het MainForm: FleetManager + KhoDieuPhoi + NhatKy, doc cung Data/ + Logs/ cua thu muc song.
//
//   KhoSong.exe <thu muc song> [--bo=acc1,acc2]      (--bo: acc KHONG dua vao kho, vd acc lam "nguoi choi")
//
// Lenh: ghi them dong vao <thu muc song>/lenh-kho.txt (moi dong mot lenh, file chi duoc NOI THEM):
//   rut <tpl> <cap|-1> <sl|-1> <nguoi>   goi <ten goi> <nguoi>   tiep <so>   huy <so>
//   nha <acc>   nhanlai <acc>   dungacc <acc>   chayacc <acc>   cfg <Khoa> <gia tri...>   tt   dung
// Trang thai ghi ra <thu muc song>/trangthai.txt moi 5 giay.
// ⚠ csc cua .NET Framework chi hieu C# 5.
static class KhoSong
{
    static string _exe, _dir;
    static FleetManager _fleet;
    static KhoDieuPhoi _dp;
    static KhoConfig _cfg;
    static List<AccountConfig> _tatCa, _kho;
    static volatile bool _chay = true;
    static readonly object _inLk = new object();

    static int Main(string[] args)
    {
        _exe = Environment.GetEnvironmentVariable("NSOKHODO_EXE") ?? @"..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe";
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            return new AssemblyName(e.Name).Name == "NSOKHODO" ? Assembly.LoadFrom(_exe) : null;
        };
        if (args.Length < 1) { Console.WriteLine("KhoSong.exe <thu muc song> [--bo=acc,...]"); return 2; }
        try { return Chay(args); }
        catch (Exception ex) { Console.WriteLine("LOI: " + ex); return 1; }
    }

    static void In(string s)
    {
        lock (_inLk) Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + s);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Chay(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try { ThreadPool.SetMinThreads(200, 200); } catch { }
        _dir = Path.GetFullPath(args[0]);
        var bo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in args)
            if (a.StartsWith("--bo=", StringComparison.OrdinalIgnoreCase))
                foreach (var x in a.Substring(5).Split(',')) if (x.Trim().Length > 0) bo.Add(x.Trim());

        AppPaths.UseBaseDir(_dir);
        AppPaths.EnsureReady();
        AppPaths.UseList(AppPaths.DEFAULT_LIST);
        NhatKy.DatThuMucGoc(Path.Combine(Path.Combine(_dir, "Logs"), AppPaths.SafeName(AppPaths.ListName)));
        StartOptions.Load();
        LoginGate.Load();
        NetOptions.Load();
        ServerList.Initialize();
        DisconnectStats.Nap();

        var app = ConfigManager.Load();
        if (ConfigManager.LoadError != null) { In("Doc acc loi: " + ConfigManager.LoadError); return 1; }
        _tatCa = app.Accounts;
        _kho = _tatCa.Where(a => !bo.Contains(a.Username)).ToList();

        _cfg = KhoConfig.Nap();
        NhatKy.Bat = true;
        Logger.Enabled = true;
        FileLog.SetEnabled(false);
        NhatKy.KhoiDong(0);
        NhatKy.App("-", "App", "KhoSong mo, danh sach " + AppPaths.ListName + ", " + _kho.Count + " acc (bo: " + string.Join(",", bo.ToArray()) + ")");
        In("Kho " + _kho.Count + " acc: " + string.Join(", ", _kho.Select(a => a.Username).ToArray()));
        In("Cai dat: map " + _cfg.Map + " khu chinh " + _cfg.KhuChinh + " khu phu " + _cfg.KhuPhu + " Leader " + _cfg.Leader
           + " du phong '" + _cfg.LeaderDuPhong + "' chu kho " + string.Join(",", _cfg.ChuKho.ToArray()));

        _fleet = new FleetManager();
        _dp = new KhoDieuPhoi(_fleet, () => _kho, _cfg);
        _dp.OnLog += s => In("[DP] " + s);
        _dp.BatDau();
        _fleet.OnLog += s => { NhatKy.AppTho(s); In(s); };

        _fleet.StartStaggered(_kho.Where(a => !_cfg.DaNha(a.Username)).ToList(), 2000, 1);

        string lenh = Path.Combine(_dir, "lenh-kho.txt");
        if (!File.Exists(lenh)) File.WriteAllText(lenh, "");
        int daDoc = File.ReadAllLines(lenh).Length;
        var ttLuc = DateTime.MinValue;
        while (_chay)
        {
            Thread.Sleep(500);
            try
            {
                var dong = File.ReadAllLines(lenh, Encoding.UTF8);
                for (; daDoc < dong.Length; daDoc++)
                {
                    string d = dong[daDoc].Trim();
                    if (d.Length == 0 || d.StartsWith("#")) continue;
                    In(">>> LENH: " + d);
                    try { LamLenh(d); }
                    catch (Exception ex) { In(">>> LENH LOI: " + ex.Message); }
                }
            }
            catch { }
            if ((DateTime.Now - ttLuc).TotalSeconds >= 5)
            {
                ttLuc = DateTime.Now;
                try { GhiTrangThai(); } catch (Exception ex) { In("trang thai loi: " + ex.Message); }
            }
        }
        In("Dung kho...");
        try { _fleet.StopAll(); } catch { }
        try { _dp.Dispose(); } catch { }
        DisconnectStats.LuuNeuBan();
        NhatKy.App("-", "App", "KhoSong dong");
        NhatKy.XaNgay();
        GhiTrangThai();
        return 0;
    }

    static AccountConfig Acc(string u)
    {
        return _tatCa.FirstOrDefault(a => string.Equals(a.Username, u, StringComparison.OrdinalIgnoreCase));
    }

    static void LamLenh(string d)
    {
        var p = d.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        switch (p[0].ToLowerInvariant())
        {
            case "rut":
                _dp.RutTuTool(short.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), string.Join(" ", p.Skip(4).ToArray()));
                break;
            case "goi":
                _dp.RutGoiTuTool(p[1], string.Join(" ", p.Skip(2).ToArray()));
                break;
            case "rutk":
                // rutk <khu> <tpl> <cap> <sl> <nguoi nhan>  (D79: khu giao rieng)
                _dp.RutTuTool(short.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]), string.Join(" ", p.Skip(5).ToArray()), int.Parse(p[1]));
                break;
            case "rutn":
                {
                    // rutn <nguoi nhan> tpl:cap:sl,tpl:cap:sl  (don nhieu mon - nut "Giao don" tren tab Tong kho)
                    var dong = new List<DongLenh>();
                    foreach (var x in p[2].Split(','))
                    {
                        var q = x.Split(':');
                        dong.Add(new DongLenh { Tpl = short.Parse(q[0]), Cap = int.Parse(q[1]), SoXin = int.Parse(q[2]) });
                    }
                    _dp.RutNhieuTuTool(dong, p[1]);
                    break;
                }
            case "tiep": _dp.TiepLenhTuTool(int.Parse(p[1])); break;
            case "huy": _dp.HuyLenhTuTool(int.Parse(p[1])); break;
            case "nha": In(">>> " + (_dp.NhaClone(Acc(p[1])) ?? "da xep hang nha")); break;
            case "nhanlai":
                {
                    var a = Acc(p[1]);
                    if (a != null && !_kho.Contains(a)) _kho.Add(a);
                    In(">>> " + (_dp.NhanLaiClone(a) ?? "da xep hang nhan lai"));
                    break;
                }
            case "dungacc": _fleet.StopAccount(Acc(p[1])); break;
            case "chayacc": _fleet.StartAccount(Acc(p[1])); break;
            case "cfg":
                {
                    string gt = string.Join(" ", p.Skip(2).ToArray());
                    var m = typeof(KhoConfig).GetMethod("DocMotKhoa", BindingFlags.NonPublic | BindingFlags.Instance);
                    _dp.Lam(() => { m.Invoke(_cfg, new object[] { p[1], gt }); In(">>> cfg " + p[1] + " = " + gt); });
                    break;
                }
            case "tt": GhiTrangThai(); break;
            case "dung": _chay = false; break;
            default: In(">>> lenh la: " + d); break;
        }
    }

    static void GhiTrangThai()
    {
        var sb = new StringBuilder();
        var sc = _dp.SucChuaKho;
        sb.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  Leader=" + _dp.LeaderAcc + " (" + _dp.TenLeader + ")  kho "
                      + (sc.ChuaDu ? "~" : "") + sc.Dung + "/" + sc.Tong + "  xu " + _dp.TongXu + "  cho " + _dp.Hang.SoCho
                      + (_dp.KhoDay ? "  KHO DAY" : "") + "  giu cua: " + (_dp.GiuCuaCho ?? "-"));
        var snap = _fleet.SnapshotByAccount();
        foreach (var a in _tatCa)
        {
            NsoClient c;
            snap.TryGetValue(a, out c);
            var st = c != null ? c.GameState : null;
            var mc = st != null ? st.MyChar : null;
            var m = st != null ? st.CurrentMap : null;
            var t = _dp.So.Lay(a.Username);
            var km = c != null ? c.ActiveModeAs<KhoMode>() : null;
            sb.AppendFormat("{0,-11} {1,-8} {2,-8} {3,-12} pos {4,-10} tui {5,-6} ruong {6,-6} xu {7,-8} | {8} | ly do: {9}",
                a.Username, _dp.VaiCuaAcc(a.Username), c == null ? "-" : c.State.ToString(),
                m == null ? "-" : ("m" + m.MapId + " k" + m.ZoneId + (st.IsChangingMap ? "*" : "")),
                mc == null ? "-" : (mc.Cx + "," + mc.Cy),
                t == null ? "-" : (t.TuiTrong + "/" + t.SoOTui),
                t == null ? "-" : (t.SoORuong < 0 ? "?" : t.RuongTrong + "/" + t.SoORuong),
                mc == null ? "-" : mc.Xu.ToString(),
                _dp.TrangThaiKho(a.Username, c), km == null ? "-" : km.LyDoCuoi);
            sb.AppendLine();
            if (m != null && a.Username == _dp.LeaderAcc)
            {
                var ds = new List<string>();
                try { foreach (var o in m.OtherPlayers.ToArray()) if (o != null) ds.Add(o.Name + "#" + o.CharId + "@" + o.X + "," + o.Y); } catch { }
                sb.AppendLine("   Leader thay " + ds.Count + " nguoi: " + string.Join(" ", ds.ToArray()));
            }
        }
        sb.AppendLine("--- TON ---");
        foreach (var d in _dp.BangTon)
            sb.AppendLine("  " + d.Khoa + " " + BangMon.Ten(d.Khoa.Tpl) + " kha dung " + d.KhaDung + " giu " + d.Giu + " @ "
                          + string.Join(" ", d.PhanBo.Select(kv => kv.Key + ":t" + kv.Value[0] + "/r" + kv.Value[1]).ToArray()));
        sb.AppendLine("--- LENH ---");
        foreach (var l in _dp.Hang.DangMo)
            sb.AppendLine("  #" + l.So + " " + l.TrangThai + " " + l.MoTaMon() + " cho " + l.NguoiNhan + " clone=" + l.CloneDangGiao
                          + " da giao " + l.TongDaGiao + "/" + l.TongXin + " ly do: " + l.LyDo);
        File.WriteAllText(Path.Combine(_dir, "trangthai.txt"), sb.ToString(), new UTF8Encoding(false));
    }
}
