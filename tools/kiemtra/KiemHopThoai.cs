using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Dung thu cac cua so: MapPickerForm va MainForm (bo cuc A) trong thu muc tam.
// Muc dich: bat loi chi xay ra luc DUNG giao dien (SplitContainer dat kich thuoc trong constructor,
// control null khi nap cai dat...) - loai loi lam app chet truoc khi hien cua so, khong mot dong log.
static class KiemHopThoai
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
        try
        {
            Application.EnableVisualStyles();
            var asm = Assembly.LoadFrom(
                Environment.GetEnvironmentVariable("NSOKHODO_EXE")
                ?? @"..\..\src\NSOKHODO\bin\Release\net452\NSOKHODO.exe");

            using (var f2 = (Form)Activator.CreateInstance(asm.GetType("NSOKHODO.UI.MapPickerForm"), new object[] { 25 }))
                Xac(f2.Text.Length > 0, "MapPickerForm dung duoc :: " + f2.Text);

            // ---- MainForm trong thu muc tam: khong dung vao Data/ that ----
            string tam = Path.Combine(Path.GetTempPath(), "nsokhodo_kiem_ui_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tam);
            var tPaths = asm.GetType("NSOKHODO.Config.AppPaths");
            tPaths.GetMethod("UseBaseDir").Invoke(null, new object[] { tam });
            tPaths.GetMethod("EnsureReady").Invoke(null, null);
            asm.GetType("NSOKHODO.Kho.NhatKy").GetMethod("DatThuMucGoc").Invoke(null, new object[] { Path.Combine(tam, "Logs") });
            asm.GetType("NSOKHODO.Protocol.ServerList").GetMethod("Initialize").Invoke(null, null);

            var tMain = asm.GetType("NSOKHODO.UI.MainForm");
            var f = (Form)Activator.CreateInstance(tMain, new object[] { false });
            Xac(true, "MainForm dung duoc :: " + f.Text);
            f.Show();
            Application.DoEvents();

            var tabs = (TabControl)tMain.GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f);
            Xac(tabs.TabPages.Count == 5, "5 tab (Tong kho / Acc / Hang cho / Nhat ky / Cai dat): " + tabs.TabPages.Count);
            var lamMoi = tMain.GetMethod("LamMoi", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                tabs.SelectedIndex = i;
                Application.DoEvents();
                lamMoi.Invoke(f, null);
                Xac(true, "mo tab '" + tabs.TabPages[i].Text + "' + lam moi khong nem");
            }
            var tcd = (TabControl)tMain.GetField("_tabsCaiDat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f);
            Xac(tcd.TabPages.Count == 6, "Cai dat co 6 tab con: " + tcd.TabPages.Count);
            for (int i = 0; i < tcd.TabPages.Count; i++)
            {
                tabs.SelectedIndex = 4;
                tcd.SelectedIndex = i;
                Application.DoEvents();
                lamMoi.Invoke(f, null);
            }
            Xac(true, "mo du 6 tab con Cai dat khong nem");

            // Sua mot o -> luu cai dat kho khi dong
            var numKhu = (NumericUpDown)tMain.GetField("_numKhuChinh", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f);
            numKhu.Value = 7;
            f.Close();
            Application.DoEvents();
            f.Dispose();

            string cfg = Path.Combine(Path.Combine(Path.Combine(tam, "Data"), "Kho"), "Mặc định.txt");
            Xac(File.Exists(cfg), "dong app -> ghi cai dat kho: " + cfg);
            Xac(File.Exists(cfg) && File.ReadAllText(cfg).Contains("KhuChinh=7"), "o Khu chinh sua tren giao dien -> KhuChinh=7 trong file");
            string ngay = Path.Combine(Path.Combine(tam, "Logs"), DateTime.Now.ToString("yyyy-MM-dd"));
            Xac(File.Exists(Path.Combine(ngay, "app.log")), "co app.log trong thu muc ngay");

            try { Directory.Delete(tam, true); } catch { }
            Console.WriteLine();
            Console.WriteLine(loi == 0 ? ">>> PASS" : ">>> CO " + loi + " CHO SAI");
            return loi;
        }
        catch (Exception ex)
        {
            var e = ex; while (e.InnerException != null) e = e.InnerException;
            Console.WriteLine("LOI: " + e.GetType().Name + ": " + e.Message);
            Console.WriteLine(e.StackTrace);
            return 1;
        }
    }
}
