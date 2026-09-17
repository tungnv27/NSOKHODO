using System;

namespace NSOKHODO.Config
{
    /// <summary>
    /// Giu danh sach tai khoan DANG MO cua tien trinh nay + file khoa cua no.
    /// Xem docs/features/DANH_SACH_ACC.md §C.
    ///
    /// <para>Vi sao tach rieng khoi <see cref="AppPaths"/>: AppPaths chi biet duong dan, khong biet
    /// vong doi. Doi danh sach la mot thao tac CO TRINH TU (tra khoa cu -> chiem khoa moi -> doi
    /// duong dan); de lan trong UI thi som muon cung co cho quen tra khoa.</para>
    /// </summary>
    internal static class ListSession
    {
        private static ListLock _lock;

        /// <summary>Danh sach dang mo.</summary>
        public static string Current { get { return AppPaths.ListName; } }

        /// <summary>
        /// Mo (hoac doi sang) mot danh sach. Tra <c>false</c> neu danh sach dang bi tien trinh khac
        /// giu - khi do KHONG doi gi ca, danh sach cu van dang mo nguyen ven.
        /// </summary>
        public static bool Open(string name, out int holderPid)
        {
            holderPid = 0;
            if (string.IsNullOrEmpty(name)) return false;

            string safe = AppPaths.SafeName(name);

            // Kiem tra TRUOC khi tra khoa cu: that bai thi phien hien tai khong bi anh huong.
            int pid = ListLock.PeekHolder(safe);
            if (pid > 0)
            {
                holderPid = pid;
                return false;
            }

            if (_lock != null) { _lock.Dispose(); _lock = null; }

            AppPaths.UseList(safe);
            _lock = ListLock.TryAcquire(safe, out holderPid);
            if (_lock == null)
            {
                // Cuoc dua hiem: tien trinh khac vua chiem giua hai buoc tren.
                return false;
            }
            return true;
        }

        /// <summary>Tra khoa - goi khi dong app.</summary>
        public static void Close()
        {
            if (_lock != null) { _lock.Dispose(); _lock = null; }
        }
    }
}
