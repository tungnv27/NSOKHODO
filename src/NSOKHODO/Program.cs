using System;
using System.Threading;
using System.Windows.Forms;
using NSOKHODO.Config;
using NSOKHODO.Protocol;
using NSOKHODO.UI;

namespace NSOKHODO
{
    /// <summary>
    /// Entry point cua NSOKHODO. Mo MainForm - cua so quan ly nhieu account (kieu NSOManager).
    /// </summary>
    internal static class Program
    {
        /// <summary>Key trong Data/settings.txt nho danh sach mo lan cuoi.</summary>
        private const string KEY_LAST_LIST = "LastAccountList";

        [STAThread]
        private static void Main(string[] args)
        {
            // ThreadPool khoi dau bang DUNG so core va chi bom them ~1 luong/giay. Chay 30 account
            // trong 1 tien trinh, moi luc co vai chuc viec ngan chen nhau (callback KeepAlive,
            // Timer hen gio login lai, timer UI) - dat san day de mot dot relogin dong loat khong
            // lam tre viec cua nhung account dang khoe. Luong chi duoc tao THAT khi co viec.
            try { ThreadPool.SetMinThreads(200, 200); } catch { }

            // ⚠️ PHAI goi TRUOC moi thu co the tao control. SetCompatibleTextRenderingDefault nem
            // InvalidOperationException neu goi sau khi control dau tien da duoc tao - ma
            // OpenAccountList() ben duoi co the bat MessageBox + hop thoai chon danh sach. Truoc
            // day hai dong nay nam o CUOI Main va OpenAccountList tu goi lai mot ban rieng => chon
            // danh sach xong la app chet, user phai mo lai app (loi bao 2026-09-11).
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Tao Data/ + di tru file cu (accounts/wp_cache/log) TRUOC khi mo form.
            AppPaths.EnsureReady();

            // Chon danh sach tai khoan TRUOC moi thu khac: duong dan file acc VA ten file log deu
            // phu thuoc no (xem docs/features/DANH_SACH_ACC.md).
            if (!OpenAccountList(args)) return;

            // Nap cai dat toan cuc (Tu tao NV: bat/tat + gioi tinh) tu Data/settings.txt.
            AutoCreateChar.Load();
            // Nap cai dat "Mo cung luc" (nut Mo Game) tu Data/settings.txt.
            StartOptions.Load();
            // Nap cai dat "Gioi han login/IP" (cong hoan login theo nhom proxy->IP server).
            NSOKHODO.Fleet.LoginGate.Load();
            // Nap 3 num van tang ket noi (han bat tay proxy, tran/gian cach login lai).
            NetOptions.Load();
            // Nap danh sach may chu tu local (cache URL / baked / custom) NGAY de UI co du lieu.
            ServerList.Initialize();
            // Lay ban moi nhat tu URL chinh thuc o NEN -> khong treo khoi dong; xong fire OnListChanged
            // (MainForm/combo tu nap lai). Loi mang -> giu ban local.
            ThreadPool.QueueUserWorkItem(_ => { try { ServerList.RefreshFromOfficial(); } catch { } });

            // --chay: mo het tai khoan ngay khi cua so hien (VPS 24/7 - khoi dong lai may la tu
            // vao lai ca dan). Khong co thi phai bam nut "Chay" nhu thuong.
            bool tuChay = false;
            if (args != null)
                foreach (string a in args)
                    if (string.Equals(a, "--chay", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(a, "--autorun", StringComparison.OrdinalIgnoreCase))
                        tuChay = true;

            try { Application.Run(new MainForm(tuChay)); }
            finally { ListSession.Close(); }   // tra file khoa du thoat kieu gi
        }

        /// <summary>
        /// Quyet dinh mo danh sach nao va CHIEM KHOA cua no.
        /// Thu tu: tham so dong lenh <c>--list=Ten</c> -> danh sach mo lan cuoi -> "Mac dinh".
        ///
        /// <para><b>Vi sao co tham so dong lenh:</b> muc dich chinh cua tinh nang la mo NHIEU cua so
        /// cung luc, moi cua so mot nhom acc. Neu chi dua vao settings.txt thi 4 cua so cung ghi mot
        /// key, mo lan sau khong biet cua so nao la cua so nao. Nhan doi shortcut, moi cai mot
        /// <c>--list=</c>, la xong. Co tham so thi KHONG ghi settings (khong dam len lua chon tay).</para>
        ///
        /// <para>Tra <c>false</c> = khong mo duoc gi, thoat app.</para>
        /// </summary>
        private static bool OpenAccountList(string[] args)
        {
            string wanted = null;
            bool fromArgs = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a != null && a.StartsWith("--list=", StringComparison.OrdinalIgnoreCase))
                    {
                        wanted = a.Substring("--list=".Length).Trim().Trim('"');
                        fromArgs = true;
                    }
                }
            }

            if (string.IsNullOrEmpty(wanted))
            {
                string v;
                if (SettingsStore.LoadAll().TryGetValue(KEY_LAST_LIST, out v) && !string.IsNullOrEmpty(v))
                    wanted = v;
            }
            if (string.IsNullOrEmpty(wanted)) wanted = AppPaths.DEFAULT_LIST;

            int holder;
            if (ListSession.Open(wanted, out holder))
            {
                if (!fromArgs) SettingsStore.Set(KEY_LAST_LIST, AppPaths.ListName);
                return true;
            }

            // Danh sach dang mo o cua so khac -> cho chon cai khac thay vi chet im.
            // Canh bao gop THANG vao hop thoai chon danh sach: mot MessageBox rieng truoc do chi
            // bat user bam them mot lan ma khong noi them duoc gi.
            string warn = "Danh sách \"" + wanted + "\" đang mở ở cửa sổ khác (PID " + holder
                        + "). Hãy chọn một danh sách khác.";
            using (var dlg = new AccountListForm(null, warn))
            {
                if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(dlg.SelectedName))
                    return false;
                if (!ListSession.Open(dlg.SelectedName, out holder)) return false;
                SettingsStore.Set(KEY_LAST_LIST, AppPaths.ListName);
                return true;
            }
        }
    }
}
