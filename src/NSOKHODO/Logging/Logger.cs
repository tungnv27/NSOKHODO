using System;
using System.Collections.Concurrent;

namespace NSOKHODO.Logging
{
    public class Logger
    {
        private static readonly ConcurrentQueue<string> _logs = new ConcurrentQueue<string>();
        private const int MAX_LOGS = 5000;

        /// <summary>
        /// Cong tac TONG cho toan bo viec sinh log (UI + file). Gate o moi nguon sinh log
        /// (NsoClient.Log / MessageRouter.Log / Logger.Log / MainForm.AppendLog) -> tat thi
        /// pipeline log dung im hoan toan (do chi phi log khi treo nhieu acc). Dieu khien boi
        /// checkbox "Ghi log" tren tab Log; MAC DINH TAT (= khong sinh log).
        /// </summary>
        public static volatile bool Enabled;

        public static event Action<string> OnLog;

        public static void Log(string message)
        {
            if (!Enabled) return;
            string entry = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message);
            _logs.Enqueue(entry);

            // Trim old logs
            string dummy;
            while (_logs.Count > MAX_LOGS)
                _logs.TryDequeue(out dummy);

            var handler = OnLog;
            if (handler != null)
                handler(entry);
        }

        public static string[] GetRecentLogs(int count)
        {
            var arr = _logs.ToArray();
            if (arr.Length <= count)
                return arr;

            var recent = new string[count];
            Array.Copy(arr, arr.Length - count, recent, 0, count);
            return recent;
        }
    }
}
