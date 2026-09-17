using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace NSOKHODO.Config
{
    /// <summary>
    /// Ghi cac file CACHE DUNG CHUNG giua nhieu cua so tool (wp_cache.txt, map_levels.txt).
    /// Xem docs/features/DANH_SACH_ACC.md §C.
    ///
    /// <para><b>Vi sao can:</b> tu khi cho mo nhieu cua so cung luc (moi cua so mot danh sach acc),
    /// hai tien trinh co the ghi CUNG mot file cache. Hai loi that:
    /// (1) <c>File.WriteAllText</c> khong nguyen tu - ghi dang do ma tien trinh kia doc vao la
    /// duoc file cut; (2) ten file <c>.tmp</c> co dinh - hai ben cung dung mot ten thi de len nhau.</para>
    ///
    /// <para><b>Cach lam:</b> mutex dat ten (chan giua cac tien trinh) + file tam mang PID rieng +
    /// <c>File.Replace</c> de doi cho nguyen tu.</para>
    ///
    /// <para>Dung mutex <c>Local\</c> chu khong <c>Global\</c>: Global can quyen cao hon va co the
    /// bi tu choi tren may bi siet quyen, doi lai chi them tac dung khi chay nhieu phien RDP khac
    /// nhau - truong hop khong co thuc voi tool nay.</para>
    /// </summary>
    internal static class SharedFile
    {
        private const string MUTEX = @"Local\NSOKHODO_CacheWrite";
        private const int WAIT_MS = 3000;

        /// <summary>
        /// Ghi de <paramref name="path"/> mot cach an toan giua nhieu tien trinh.
        /// Nem loi ra ngoai de noi goi tu quyet dinh (cache hong khong duoc lam chet bot).
        /// </summary>
        public static void WriteAtomic(string path, string content)
        {
            bool created;
            using (var mtx = new Mutex(false, MUTEX, out created))
            {
                bool held = false;
                try
                {
                    try { held = mtx.WaitOne(WAIT_MS); }
                    catch (AbandonedMutexException) { held = true; }  // tien trinh giu no da chet

                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    // Ten tam mang PID: hai tien trinh cung ghi thi khong dung chung file tam.
                    string tmp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
                    try
                    {
                        File.WriteAllText(tmp, content, Encoding.UTF8);
                        if (File.Exists(path)) File.Replace(tmp, path, null);
                        else File.Move(tmp, path);
                    }
                    catch
                    {
                        try { if (File.Exists(tmp)) File.Delete(tmp); }
                        catch { }
                        throw;
                    }
                }
                finally
                {
                    if (held) { try { mtx.ReleaseMutex(); } catch { } }
                }
            }
        }
    }
}
