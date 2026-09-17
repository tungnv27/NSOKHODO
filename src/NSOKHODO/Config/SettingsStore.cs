using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NSOKHODO.Config
{
    /// <summary>
    /// Kho cai dat TOAN CUC dang key=value trong Data/settings.txt (khong phai per-account).
    ///
    /// Ly do co lop nay: truoc day <see cref="AutoCreateChar"/> tu ghi de nguyen file bang 2 key cua no
    /// -> setting toan cuc thu 2 nao ghi sau se XOA key cua cai truoc. Moi ghi giu doc-hop-nhat-ghi
    /// (load toan bo key cu -> set key moi -> ghi lai) nen them setting toan cuc moi la an toan.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly object _lock = new object();

        public static Dictionary<string, string> LoadAll()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = AppPaths.Settings;
                if (!File.Exists(path)) return map;
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return map;
        }

        /// <summary>Ghi 1 hoac nhieu key, GIU nguyen cac key khac dang co trong file.</summary>
        public static void Set(params string[] keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Length < 2) return;
            lock (_lock)
            {
                var map = LoadAll();
                for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
                    map[keyValuePairs[i]] = keyValuePairs[i + 1];

                try
                {
                    Directory.CreateDirectory(AppPaths.DataDir);
                    var sb = new StringBuilder();
                    foreach (var kv in map)
                        sb.Append(kv.Key).Append('=').Append(kv.Value).Append(Environment.NewLine);
                    File.WriteAllText(AppPaths.Settings, sb.ToString());
                }
                catch { }
            }
        }

        public static bool GetBool(Dictionary<string, string> map, string key, bool def)
        {
            string v;
            if (map == null || !map.TryGetValue(key, out v) || string.IsNullOrEmpty(v)) return def;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static int GetInt(Dictionary<string, string> map, string key, int def)
        {
            string v; int n;
            if (map == null || !map.TryGetValue(key, out v) || !int.TryParse(v, out n)) return def;
            return n;
        }
    }
}
