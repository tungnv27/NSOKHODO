using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

// Kiem cac phan LOI giu lai tu NSOBAOTATL. Phan "chia khu" da go cung ChiaKhuForm (NSOKHODO khong
// chia khu theo acc - map/khu la cai dat chung cua kho).
static class KiemLoi
{
    static Assembly asm;
    static int loi = 0;

    static void Xac(bool dieuKien, string mota) {
        Console.WriteLine((dieuKien ? "  OK   " : "  SAI  ") + mota);
        if (!dieuKien) loi++;
    }

    [STAThread]
    static int Main() {
        Application.EnableVisualStyles();
        asm = Assembly.LoadFrom(
            Environment.GetEnvironmentVariable("NSOKHODO_EXE")
            ?? @"..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe");

        Console.WriteLine("=== DAI KHU (ZoneRange) ===");
        var tZr = asm.GetType("NSOKHODO.ZoneRange");
        var parse = tZr.GetMethod("Parse");
        Func<string,string> P = s2 => string.Join(",", ((List<int>)parse.Invoke(null, new object[]{ s2 })).ConvertAll(x => x.ToString()).ToArray());
        Xac(P("0-4") == "0,1,2,3,4", "0-4");
        Xac(P("1,3,5") == "1,3,5", "1,3,5");
        Xac(P("0-2,10-11") == "0,1,2,10,11", "0-2,10-11");

        Console.WriteLine("=== DEM MAT KET NOI ===");
        var tDs = asm.GetType("NSOKHODO.Fleet.DisconnectStats");
        var tLoai = asm.GetType("NSOKHODO.Fleet.DisconnectStats+Loai");
        var ghi = tDs.GetMethod("Ghi");
        tDs.GetMethod("XoaHet").Invoke(null, null);
        ghi.Invoke(null, new object[]{ "acc1", Enum.ToObject(tLoai, 0) });
        ghi.Invoke(null, new object[]{ "acc1", Enum.ToObject(tLoai, 0) });
        ghi.Invoke(null, new object[]{ "acc1", Enum.ToObject(tLoai, 1) });
        ghi.Invoke(null, new object[]{ "ACC1", Enum.ToObject(tLoai, 2) });   // khac hoa thuong
        tDs.GetMethod("GhiVaoGame").Invoke(null, new object[]{ "acc1" });
        var ban = tDs.GetMethod("Lay").Invoke(null, new object[]{ "acc1" });
        var tb = ban.GetType();
        Xac((int)tb.GetField("RotKhiDangChoi").GetValue(ban) == 2, "dem 'rot khi dang choi' = 2");
        Xac((int)tb.GetField("ServerChan").GetValue(ban) == 1, "dem 'server chan' = 1");
        Xac((int)tb.GetField("LoiDangNhap").GetValue(ban) == 1, "khoa KHONG phan biet hoa thuong");
        Xac((int)tb.GetProperty("Tong").GetValue(ban, null) == 4, "tong = 4");
        Xac((int)tDs.GetMethod("TongTatCa").Invoke(null, null) == 4, "TongTatCa = 4");
        string csv = (string)tDs.GetMethod("XuatCsv").Invoke(null, new object[]{ new string[]{ "acc1", "accLa" } });
        Xac(csv.Contains("acc1,2,1,1,4,1,80.0"), "CSV: ti le rot 4/(4+1) = 80.0%");
        Xac(csv.Contains("accLa,0,0,0,0,0,,"), "CSV: acc chua co so lieu -> hang rong, khong no");

        Console.WriteLine("=== TEN MAP ===");
        var tMn = asm.GetType("NSOKHODO.UI.MapNames");
        Xac((string)tMn.GetMethod("NameOf").Invoke(null, new object[]{ 23 }) == "Vách Ichidai", "map 23 = Vach Ichidai");
        Xac(((string)tMn.GetMethod("NameOf").Invoke(null, new object[]{ 22 })).Length > 0, "map 22 (Lang Tone - map kho mac dinh) co ten");

        Console.WriteLine("=== CHAN DUNG / MAC / BAN / VUT MON (D38) ===");
        // ItemService chi con cac lenh kho can. Them lai bat ky lenh nao duoi day = co duong tieu hao do trong kho.
        var tIs = asm.GetType("NSOKHODO.Service.ItemService");
        var conLai = new List<string>();
        foreach (var m in tIs.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            conLai.Add(m.Name);
        conLai.Sort();
        string ds = string.Join(",", conLai.ToArray());
        Xac(ds == "SendItemBagToBox,SendItemBoxToBag,SendRequestItem,SendRequestItemInfo,SendSortBag,SendSplitStack",
            "ItemService chi con: " + ds);
        foreach (string cam in new[] { "UseItem", "SellItem", "DropItem", "SendUseItem", "SendSellItem", "SendThrowItem", "SendUpgrade", "SendSplitItem" })
            Xac(tIs.GetMethod(cam) == null, "khong co " + cam);
        var tNav = asm.GetType("NSOKHODO.Auto.Navigator");
        var kdl = tNav.GetMethod("FindKdlSlot", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        Xac(kdl != null, "Navigator.FindKdlSlot con ton tai (de kiem no luon -1)");

        Console.WriteLine();
        Console.WriteLine(loi == 0 ? ">>> TAT CA " + "PASS" : ">>> CO " + loi + " CHO SAI");
        return loi;
    }
}
