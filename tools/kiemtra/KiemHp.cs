using System;
using System.Reflection;

static class KiemHp
{
    static int loi = 0;
    static void Xac(bool ok, string mota)
    {
        Console.WriteLine((ok ? "  OK   " : "  SAI  ") + mota);
        if (!ok) loi++;
    }

    [STAThread]
    static int Main()
    {
        var asm = Assembly.LoadFrom(
            Environment.GetEnvironmentVariable("NSOKHODO_EXE")
            ?? @"..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe");
        var tCfg = asm.GetType("NSOKHODO.Client.AccountConfig");
        var tCli = asm.GetType("NSOKHODO.Client.NsoClient");
        var tGsm = asm.GetType("NSOKHODO.Client.GameStateManager");
        var tChar = asm.GetType("NSOKHODO.Models.CharacterState");

        var cfg = Activator.CreateInstance(tCfg);
        tCfg.GetProperty("Username").SetValue(cfg, "kiemtra", null);
        var cli = Activator.CreateInstance(tCli, new object[] { cfg });

        var state = tCli.GetProperty("GameState").GetValue(cli, null);
        var mc = Activator.CreateInstance(tChar);
        tChar.GetProperty("MaxHp").SetValue(mc, 1742, null);
        tChar.GetProperty("Hp").SetValue(mc, 500, null);
        tGsm.GetProperty("MyChar").SetValue(state, mc, null);

        var fRouter = tCli.GetField("_router", BindingFlags.NonPublic | BindingFlags.Instance);
        var router = fRouter.GetValue(cli);
        var evt = router.GetType().GetField("OnChatReceived", BindingFlags.NonPublic | BindingFlags.Instance);
        var del = (Delegate)evt.GetValue(router);

        Func<int> hp = delegate { return (int)tChar.GetProperty("Hp").GetValue(mc, null); };

        Console.WriteLine("=== NHANH SUA 'HP da day' ===");
        Console.WriteLine("  truoc khi ban tin: Hp = " + hp());

        del.DynamicInvoke("Tin nhan khong lien quan gi ca");
        Xac(hp() == 500, "tin nhan khac -> KHONG dong vao HP");

        del.DynamicInvoke("HP \u0111\u00e3 \u0111\u1ea7y");
        Xac(hp() == 1742, "server bao 'HP da day' -> nan HP cuc bo 500 -> 1742");

        del.DynamicInvoke("HP \u0111\u00e3 \u0111\u1ea7y");
        Xac(hp() == 1742, "goi lai khi da day -> giu nguyen, khong nem");

        tChar.GetProperty("Hp").SetValue(mc, 300, null);
        del.DynamicInvoke("Kh\u00f4ng \u0111\u1ee7 MP \u0111\u1ec3 s\u1eed d\u1ee5ng");
        Xac(hp() == 300, "tin 'Khong du MP' -> KHONG dong vao HP");

        tGsm.GetProperty("MyChar").SetValue(state, null, null);
        try
        {
            del.DynamicInvoke("HP \u0111\u00e3 \u0111\u1ea7y");
            Xac(true, "MyChar = null -> khong nem");
        }
        catch (Exception)
        {
            Xac(false, "MyChar = null -> KHONG duoc nem");
        }

        Console.WriteLine();
        Console.WriteLine(loi == 0 ? ">>> PASS" : ">>> CO " + loi + " CHO SAI");
        return loi;
    }
}
