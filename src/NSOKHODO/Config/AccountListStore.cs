using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NSOKHODO.Config
{
    /// <summary>Mot dong trong hop thoai "Danh sach tai khoan".</summary>
    internal sealed class AccountListInfo
    {
        public string Name;
        /// <summary>So tai khoan trong danh sach; -1 = khong doc duoc (file hong / sai key).</summary>
        public int Count;
        public DateTime Modified;
        /// <summary>Dang duoc mo boi tien trinh KHAC (PID > 0) - khong cho mo lan hai.</summary>
        public int HolderPid;
    }

    /// <summary>
    /// Quan ly cac danh sach tai khoan trong <see cref="AppPaths.AccountsDir"/>.
    /// Xem docs/features/DANH_SACH_ACC.md.
    ///
    /// <para>Nguoi dung CHI dat ten - khong chon duong dan. Tool tu quan cho luu, nen khong co
    /// chuyen file acc nam rai rac roi lan sau khong tim thay.</para>
    /// </summary>
    internal static class AccountListStore
    {
        /// <summary>Liet ke moi danh sach, kem so acc va tien trinh dang giu (neu co).</summary>
        public static List<AccountListInfo> List()
        {
            var res = new List<AccountListInfo>();
            try
            {
                Directory.CreateDirectory(AppPaths.AccountsDir);
                foreach (string f in Directory.GetFiles(AppPaths.AccountsDir, "*.txt"))
                {
                    var fi = new FileInfo(f);
                    res.Add(new AccountListInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(f),
                        Count = CountAccounts(f),
                        Modified = fi.LastWriteTime,
                        HolderPid = HolderOf(Path.GetFileNameWithoutExtension(f))
                    });
                }
            }
            catch { }

            // Danh sach DANG MO ma chua co file (ban moi cai, chua luu lan nao) van phai hien -
            // neu khong user mo hop thoai ra thay trong tron, tuong mat het.
            bool hasCurrent = false;
            foreach (var i in res)
                if (string.Equals(i.Name, AppPaths.ListName, StringComparison.OrdinalIgnoreCase))
                { hasCurrent = true; break; }
            if (!hasCurrent)
                res.Add(new AccountListInfo { Name = AppPaths.ListName, Count = 0, Modified = DateTime.Now });

            res.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return res;
        }

        /// <summary>
        /// PID cua tien trinh KHAC dang mo danh sach nay; 0 = khong ai giu.
        /// CHI DOC - tuyet doi khong duoc goi <see cref="ListLock.TryAcquire"/> o day: danh sach
        /// DANG MO cua chinh cua so nay cung nam trong danh sach liet ke, acquire roi dispose se
        /// XOA mat khoa cua chinh minh.
        /// </summary>
        public static int HolderOf(string name)
        {
            return ListLock.PeekHolder(name);
        }

        public static bool Exists(string name)
        {
            try { return File.Exists(AppPaths.AccountsOf(name)); }
            catch { return false; }
        }

        /// <summary>Tao danh sach RONG. Tra false neu ten da ton tai.</summary>
        public static bool Create(string name)
        {
            if (string.IsNullOrEmpty(name) || Exists(name)) return false;
            try
            {
                Directory.CreateDirectory(AppPaths.AccountsDir);
                File.WriteAllText(AppPaths.AccountsOf(name),
                    AccountCrypto.Encrypt("# NSOKHODO Config" + Environment.NewLine), Encoding.UTF8);
                return true;
            }
            catch { return false; }
        }

        public static bool Rename(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(newName) || Exists(newName)) return false;
            try
            {
                File.Move(AppPaths.AccountsOf(oldName), AppPaths.AccountsOf(newName));
                TryDelete(AppPaths.AccountsOf(oldName) + ".bak");
                return true;
            }
            catch { return false; }
        }

        /// <summary>Nhan ban - de tach mot danh sach lon thanh hai nhom ma khong phai go lai tay.</summary>
        public static bool Duplicate(string name, string newName)
        {
            if (string.IsNullOrEmpty(newName) || Exists(newName) || !Exists(name)) return false;
            try
            {
                File.Copy(AppPaths.AccountsOf(name), AppPaths.AccountsOf(newName));
                return true;
            }
            catch { return false; }
        }

        public static bool Delete(string name)
        {
            try
            {
                File.Delete(AppPaths.AccountsOf(name));
                TryDelete(AppPaths.AccountsOf(name) + ".bak");
                TryDelete(AppPaths.LockOf(name));
                return true;
            }
            catch { return false; }
        }

        private static void TryDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch { }
        }

        /// <summary>
        /// Dem so acc mà KHONG nap ca cau hinh: chi can dem dong. Tra -1 neu file ma hoa ma giai
        /// khong ra (bao cho user thay thay vi hien "0 acc" gay hieu nham la danh sach rong).
        /// </summary>
        private static int CountAccounts(string path)
        {
            try
            {
                string raw = File.ReadAllText(path, Encoding.UTF8);
                if (AccountCrypto.IsEncrypted(raw))
                {
                    raw = AccountCrypto.Decrypt(raw);
                    if (raw == null) return -1;
                }
                int n = 0;
                foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
                {
                    string s = line.Trim();
                    if (s.Length == 0 || s[0] == '#') continue;
                    if (s.IndexOf('|') > 0) n++;
                }
                return n;
            }
            catch { return -1; }
        }
    }
}
