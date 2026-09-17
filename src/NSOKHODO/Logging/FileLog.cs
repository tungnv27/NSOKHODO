using System;
using System.IO;
using System.Text;
using NSOKHODO.Config;

namespace NSOKHODO.Logging
{
    /// <summary>
    /// Ghi log ra file canh du lieu (AppPaths.Log), xoay sang AppPaths.OldLog khi qua 5MB.
    ///
    /// <para>NAM O NGUON DUNG CHUNG chu khong o lop UI: truoc day toan bo doan nay nam trong
    /// <c>MainForm</c> (file WinForms, bi csproj Android loai tru) nen ban Android bat o tich
    /// "Bat log (hien UI + ghi file nsolite.log...)" ma KHONG he co file nao duoc tao - tren
    /// dien thoai thanh ra khong co duong nao lay log ra ngoai. Dat o day thi ca hai ban dung
    /// chung mot cai dat, sua mot cho la ca hai doi theo.</para>
    ///
    /// <para>MAC DINH TAT. Khi bat: giu san mot <see cref="StreamWriter"/> (AutoFlush), tu dem
    /// byte de biet luc nao phai xoay -> khong stat dia, khong open-close moi dong.</para>
    /// </summary>
    public static class FileLog
    {
        private const long FILE_LOG_MAX = 5 * 1024 * 1024;

        private static volatile bool _enabled;
        private static readonly object _lock = new object();
        private static StreamWriter _writer;
        private static long _bytes;

        /// <summary>
        /// Bat/tat ghi file. Tat thi tha luon file handle (khong ton I/O, va cho phep xoa file).
        /// KHONG dung den <see cref="Logger.Enabled"/> - cong tac tong do phia goi tu dat, giong
        /// nhu <c>MainForm.SetFileLog</c> van lam.
        /// </summary>
        public static void SetEnabled(bool on)
        {
            _enabled = on;
            if (!on) Close();
        }

        public static bool Enabled { get { return _enabled; } }

        /// <summary>Dong file handle dang mo (goi luc thoat app / doi danh sach acc).</summary>
        public static void Close()
        {
            lock (_lock)
            {
                try { if (_writer != null) { _writer.Dispose(); _writer = null; } }
                catch { }
            }
        }

        /// <summary>
        /// Ghi mot LO dong (da co san newline o cuoi moi dong). Loi ghi dia thi tha handle roi
        /// bo qua - log hong khong duoc phep lam chet app dang treo bot.
        /// </summary>
        public static void Write(string batch)
        {
            if (!_enabled || string.IsNullOrEmpty(batch)) return;
            lock (_lock)
            {
                try
                {
                    OpenIfNeeded();
                    int n = Encoding.UTF8.GetByteCount(batch);
                    if (_bytes + n > FILE_LOG_MAX)
                    {
                        _writer.Dispose();
                        _writer = null;
                        try
                        {
                            if (File.Exists(AppPaths.OldLog)) File.Delete(AppPaths.OldLog);
                            File.Move(AppPaths.Log, AppPaths.OldLog);
                        }
                        catch { }
                        OpenIfNeeded();
                    }
                    _writer.Write(batch);
                    _bytes += n;
                }
                catch
                {
                    try { if (_writer != null) { _writer.Dispose(); _writer = null; } }
                    catch { }
                }
            }
        }

        /// <summary>Mo file + ghi moc ngay-gio dau doan. Chi goi ben trong lock.</summary>
        private static void OpenIfNeeded()
        {
            if (_writer != null) return;
            var fi = new FileInfo(AppPaths.Log);
            _bytes = fi.Exists ? fi.Length : 0;
            _writer = new StreamWriter(AppPaths.Log, true) { AutoFlush = true };
            // Moi dong da co [HH:mm:ss] nhung khong co NGAY - ghi mot moc day du moi lan mo/xoay.
            string marker = string.Format("===== {0:yyyy-MM-dd HH:mm:ss} ====={1}",
                                          DateTime.Now, Environment.NewLine);
            _writer.Write(marker);
            _bytes += Encoding.UTF8.GetByteCount(marker);
        }
    }
}
