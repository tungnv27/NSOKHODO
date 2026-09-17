using System;
using System.Diagnostics;
using System.IO;

namespace NSOKHODO.Config
{
    /// <summary>
    /// Khoa mot danh sach tai khoan cho MOT tien trinh. Xem docs/features/DANH_SACH_ACC.md §C.
    ///
    /// <para><b>Vi sao can:</b> tool autosave moi lan sua acc. Mo hai cua so cung TRO VAO MOT danh
    /// sach thi ban ghi cua cua so nay de len cua so kia - sua 20 acc ben A, ben B luu mot phat la
    /// mat sach. Khoa nay chan tu luc MO.</para>
    ///
    /// <para><b>Khoa mo coi:</b> lan truoc tool crash/tat dien thi file .lock con nam lai. Khong the
    /// coi no la "dang mo" vinh vien -> kiem tra PID ghi trong do con song khong; chet roi thi
    /// chiem luon.</para>
    /// </summary>
    internal sealed class ListLock : IDisposable
    {
        private readonly string _path;
        private bool _held;

        private ListLock(string path) { _path = path; }

        /// <summary>
        /// Thu khoa danh sach. Tra <c>null</c> neu danh sach dang duoc tien trinh KHAC giu -
        /// <paramref name="holderPid"/> la PID cua no (0 = khong doc duoc).
        /// </summary>
        public static ListLock TryAcquire(string listName, out int holderPid)
        {
            holderPid = 0;
            string path = AppPaths.LockOf(listName);
            try
            {
                if (File.Exists(path))
                {
                    int pid = ReadPid(path);
                    if (pid > 0 && pid != Process.GetCurrentProcess().Id && IsAlive(pid))
                    {
                        holderPid = pid;
                        return null;       // tien trinh khac dang giu that
                    }
                    // Khoa mo coi (tien trinh da chet) hoac chinh ta -> chiem lai.
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Process.GetCurrentProcess().Id.ToString());
                return new ListLock(path) { _held = true };
            }
            catch
            {
                // Khong ghi duoc file khoa (o dia chi doc...) -> KHONG chan nguoi dung lam viec,
                // chi mat lop bao ve. Tra ve mot khoa "rong".
                return new ListLock(path);
            }
        }

        /// <summary>
        /// PID cua tien trinh KHAC dang giu danh sach (0 = khong ai, hoac chinh ta dang giu).
        /// Chi doc, KHONG dung cham file khoa - dung cho man hinh liet ke.
        /// </summary>
        public static int PeekHolder(string listName)
        {
            try
            {
                string path = AppPaths.LockOf(listName);
                if (!File.Exists(path)) return 0;
                int pid = ReadPid(path);
                if (pid <= 0 || pid == Process.GetCurrentProcess().Id) return 0;
                return IsAlive(pid) ? pid : 0;
            }
            catch { return 0; }
        }

        private static int ReadPid(string path)
        {
            try
            {
                int pid;
                return int.TryParse((File.ReadAllText(path) ?? "").Trim(), out pid) ? pid : 0;
            }
            catch { return 0; }
        }

        private static bool IsAlive(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                return p != null && !p.HasExited;
            }
            catch { return false; }   // ArgumentException = khong con tien trinh nao mang PID do
        }

        public void Dispose()
        {
            if (!_held) return;
            _held = false;
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch { }
        }
    }
}
