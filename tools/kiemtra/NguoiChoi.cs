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
using NSOKHODO.Models;
using NSOKHODO.Protocol;

// MOT ACC DONG VAI NGUOI CHOI (nguoi nap / nguoi nhan) de test kho song. Tien trinh RIENG voi KhoSong:
// o day KHONG co KhoDieuPhoi nen KhoMode cua acc nay dung im, khong an loi moi.
//
//   NguoiChoi.exe <thu muc song> <acc> [file lenh]
//
// Lenh: noi them dong vao <thu muc song>/lenh-nguoi.txt (hoac [file lenh])
//   moi <ten>               chi gui loi moi (khong mo phien)
//   tach <o> <sl>           tach chong (-28/-85) - de lam day tui khi thu "nguoi nhan thieu o"
//   di <map> <khu>          ve map + khu (qua Navigator nhu bot kho)
//   toi <x> <y>             di toi toa do
//   sat <ten>               di sat nguoi co ten
//   nhan on|off             tu nhan moi loi moi giao dich (khoa 0 o, dong y)
//   nap <ten> <o,o,..|het> [xu]   moi <ten> giao dich, dat cac o tui do (het = moi o co do, toi da 12)
//   pm <ten> <noi dung>     chat rieng        chat <noi dung>   chat khu
//   tui   ai   huy   vaolai (thoat roi dang nhap lai)   dung
// ⚠ csc cua .NET Framework chi hieu C# 5.
static class NguoiChoi
{
    static string _exe, _dir;
    static NsoClient _c;
    static Navigator _nav;
    static PhienGiaoDich _phien;
    static bool _nhan;
    static int _diMap = -1, _diKhu = -1;
    static volatile bool _chay = true;
    static readonly object _inLk = new object();

    static int Main(string[] args)
    {
        _exe = Environment.GetEnvironmentVariable("NSOKHODO_EXE") ?? @"..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe";
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            return new AssemblyName(e.Name).Name == "NSOKHODO" ? Assembly.LoadFrom(_exe) : null;
        };
        if (args.Length < 2) { Console.WriteLine("NguoiChoi.exe <thu muc song> <acc>"); return 2; }
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
        _dir = Path.GetFullPath(args[0]);
        AppPaths.UseBaseDir(_dir);
        AppPaths.EnsureReady();
        AppPaths.UseList(AppPaths.DEFAULT_LIST);
        NhatKy.Bat = false;
        Logger.Enabled = true;   // log client ra console (khong ghi file: FileLog tat)
        FileLog.SetEnabled(false);
        StartOptions.Load();
        LoginGate.Load();
        NetOptions.Load();
        ServerList.Initialize();

        var app = ConfigManager.Load();
        if (ConfigManager.LoadError != null) { In("Doc acc loi: " + ConfigManager.LoadError); return 1; }
        var acc = app.Accounts.FirstOrDefault(a => string.Equals(a.Username, args[1], StringComparison.OrdinalIgnoreCase));
        if (acc == null) { In("Khong co acc " + args[1]); return 1; }

        _acc = acc;
        TaoClient();

        // Nhieu nguoi choi cung thu muc: moi acc mot file lenh (acc dau tien giu ten cu).
        string lenh = Path.Combine(_dir, args.Length > 2 ? args[2] : "lenh-nguoi.txt");
        if (!File.Exists(lenh)) File.WriteAllText(lenh, "");
        int daDoc = File.ReadAllLines(lenh).Length;
        while (_chay)
        {
            Thread.Sleep(100);
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
            try { Nhip(); }
            catch (Exception ex) { In("nhip loi: " + ex.Message); }
        }
        _c.Stop();
        return 0;
    }

    static AccountConfig _acc;

    static void TaoClient()
    {
        _c = new NsoClient(_acc);
        _c.OnLog += s => { if (s.IndexOf("CHUA XU LY", StringComparison.Ordinal) < 0) In(s); };
        _c.OnPrivateChatReceived += (c, tu, tin) => In("<<< PM tu " + tu + ": " + tin);
        _c.OnChatMessage += s => In("<<< server: " + s);
        _nav = new Navigator(_c);
        _phien = null;
        _c.Start();
    }

    static void Nhip()
    {
        if (_c.State != ClientState.InGame) return;
        var st = _c.GameState;
        if (_phien != null)
        {
            _phien.Tick();
            if (!_phien.KetThuc) return;
            var kq = _phien.KetQua;
            In(">>> PHIEN KET THUC: " + (kq.ThanhCong ? "XONG" : "HUY [" + kq.MaLoi + "] " + kq.LyDo)
               + " voi " + kq.DoiPhuong + " | nhan " + kq.MoTaMonNhan() + " + " + kq.XuNhan + " xu | dua "
               + kq.MoTaMonDua() + " + " + kq.XuDua + " xu | server: " + string.Join(" / ", kq.TinServer));
            _phien = null;
            return;
        }
        int id; DateTime luc;
        if (_c.Trade.LayLoiMoi(out id, out luc))
        {
            var p = TimTheoId(id);
            In(">>> LOI MOI tu id " + id + " (" + (p != null ? p.Name : "?") + ")" + (_nhan ? " -> nhan" : " -> bo qua"));
            if (_nhan && (DateTime.UtcNow - luc).TotalSeconds < 25)
            {
                _phien = new PhienGiaoDich(_c, VaiGiaoDich.Nhan, id, null, 60000, 0, 0, null, null, null);
                return;
            }
        }
        if (_diMap >= 0 && !st.IsChangingMap)
        {
            if (st.CurrentMap.MapId != _diMap) { _nav.DoGmNavigation(_diMap); return; }
            if (_diKhu >= 0 && st.CurrentMap.ZoneId != _diKhu)
            {
                if ((DateTime.UtcNow - _c.VaoKhuLucUtc).TotalMilliseconds < 10500) return;
                _nav.DoZoneChange((byte)_diKhu);
                return;
            }
            In(">>> DA TOI map " + _diMap + " khu " + _diKhu + " (" + st.MyChar.Cx + "," + st.MyChar.Cy + ")");
            _diMap = -1;
        }
    }

    static PlayerInfo TimTheoId(int id)
    {
        try { return _c.GameState.CurrentMap.OtherPlayers.ToArray().FirstOrDefault(x => x != null && x.CharId == id); }
        catch { return null; }
    }

    static PlayerInfo TimTheoTen(string ten)
    {
        try { return _c.GameState.CurrentMap.OtherPlayers.ToArray().FirstOrDefault(x => x != null && ChuVan.CungTen(x.Name, ten)); }
        catch { return null; }
    }

    static void LamLenh(string d)
    {
        var p = d.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var mc = _c.GameState.MyChar;
        switch (p[0].ToLowerInvariant())
        {
            case "di": _diMap = int.Parse(p[1]); _diKhu = int.Parse(p[2]); break;
            case "toi": _nav.CharBurstMove(short.Parse(p[1]), short.Parse(p[2])); break;
            case "sat":
                {
                    var o = TimTheoTen(p[1]);
                    if (o == null) { In(">>> khong thay " + p[1]); break; }
                    _nav.CharBurstMove((short)(o.X + 20), o.Y);
                    break;
                }
            case "nhan": _nhan = p.Length > 1 && p[1] == "on"; In(">>> tu nhan = " + _nhan); break;
            case "nap":
                {
                    var o = TimTheoTen(p[1]);
                    if (o == null) { In(">>> khong thay " + p[1]); break; }
                    var o2 = new List<byte>();
                    if (p[2] == "het")
                    {
                        for (int i = 0; i < mc.BagItems.Length && o2.Count < 12; i++)
                            if (mc.BagItems[i] != null && !mc.BagItems[i].IsEmpty && !mc.BagItems[i].IsLock) o2.Add((byte)i);
                    }
                    else if (p[2] != "0")
                        foreach (var x in p[2].Split(',')) o2.Add(byte.Parse(x));
                    int xu = p.Length > 3 ? int.Parse(p[3]) : 0;
                    var chon = o2.ToArray();
                    _phien = new PhienGiaoDich(_c, VaiGiaoDich.Giao, o.CharId, o.Name, 60000, 31000, xu, () => chon, null, null);
                    break;
                }
            case "moi":
                {
                    // Chi GUI loi moi (khong mo phien) - de do luat server chan loi moi.
                    var o = TimTheoTen(p[1]);
                    if (o == null) { In(">>> khong thay " + p[1]); break; }
                    _c.Trade.BatDauPhienMoi();
                    _c.TradeSvc.SendInvite(o.CharId);
                    break;
                }
            case "theo":
                {
                    // theo <ten> <giay>: in moi lan toa do nguoi do (nhin tu acc nay) doi - do loi "nhan vat bay".
                    string ten = p[1];
                    int giay = p.Length > 2 ? int.Parse(p[2]) : 90;
                    var th = new Thread(() =>
                    {
                        string cu = null;
                        var het = DateTime.UtcNow.AddSeconds(giay);
                        while (DateTime.UtcNow < het && _chay)
                        {
                            var o = TimTheoTen(ten);
                            string moi = o == null ? "khong thay" : (o.X + "," + o.Y);
                            if (moi != cu) { In(">>> THEO " + ten + " @ " + moi); cu = moi; }
                            Thread.Sleep(50);
                        }
                        In(">>> THEO " + ten + " xong");
                    });
                    th.IsBackground = true;
                    th.Start();
                    break;
                }
            case "nhay":
            case "nhayx":
                {
                    // nhay <dy,dy,..> <gapMs> [cuoiMs]: gui (x, y+dy) lan luot tu cho dang dung (nhayx: truc X);
                    // cuoiMs > 0: doi them roi gui lai dung cho cu mot lan. Do luat server voi nhip chong AFK.
                    bool trucX = p[0].ToLowerInvariant() == "nhayx";
                    var ds = p[1].Split(',').Select(int.Parse).ToArray();
                    int gap = int.Parse(p[2]);
                    int cuoi = p.Length > 3 ? int.Parse(p[3]) : 0;
                    short x0 = mc.Cx, y0 = mc.Cy;
                    var th = new Thread(() =>
                    {
                        foreach (var d0 in ds)
                        {
                            short nx = trucX ? (short)(x0 + d0) : x0, ny = trucX ? y0 : (short)(y0 + d0);
                            _c.Movement.SendMoveForce(nx, ny);
                            In(">>> NHAY gui " + nx + "," + ny);
                            Thread.Sleep(gap);
                        }
                        if (cuoi > 0)
                        {
                            // Ve cho cu kieu CharBurstMove: 3 goi, goi 1-2 cach 20 ms.
                            Thread.Sleep(cuoi);
                            _c.Movement.SendMoveForce(x0, y0);
                            Thread.Sleep(20);
                            _c.Movement.SendMoveForce(x0, y0);
                            _c.Movement.SendMoveForce(x0, y0);
                            In(">>> NHAY gui lai x3 " + x0 + "," + y0);
                        }
                    });
                    th.IsBackground = true;
                    th.Start();
                    break;
                }
            case "tach": _c.Items.SendSplitStack(byte.Parse(p[1]), int.Parse(p[2])); break;
            case "pm": _c.Chat.SendPrivateChat(p[1], string.Join(" ", p.Skip(2).ToArray())); break;
            case "chat": _c.Chat.SendPublicChat(string.Join(" ", p.Skip(1).ToArray())); break;
            case "tui":
                {
                    var sb = new StringBuilder("TUI " + mc.Name + " xu " + mc.Xu + ":");
                    for (int i = 0; i < mc.BagItems.Length; i++)
                    {
                        var it = mc.BagItems[i];
                        if (it == null || it.IsEmpty) continue;
                        sb.Append(" [" + i + "]" + it.TemplateId + (it.Upgrade > 0 ? "+" + it.Upgrade : "") + "x" + it.Quantity
                                  + (it.IsLock ? "(khoa)" : "") + (it.IsExpires ? "(han)" : ""));
                    }
                    In(sb.ToString());
                    break;
                }
            case "ai":
                {
                    var m = _c.GameState.CurrentMap;
                    var ds = new List<string>();
                    foreach (var o in m.OtherPlayers.ToArray()) if (o != null) ds.Add(o.Name + "#" + o.CharId + "@" + o.X + "," + o.Y);
                    In("map " + m.MapId + " khu " + m.ZoneId + " toi " + mc.Cx + "," + mc.Cy + " | " + ds.Count + " nguoi: " + string.Join(" ", ds.ToArray()));
                    break;
                }
            case "huy": if (_phien != null) _phien.YeuCauHuy("nguoi choi huy"); break;
            case "vaolai":
                _c.Stop();
                Thread.Sleep(3000);
                TaoClient();
                break;
            case "dung": _chay = false; break;
            default: In(">>> lenh la: " + d); break;
        }
    }
}
