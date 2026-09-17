using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NSOKHODO.Config;
using NSOKHODO.GameData;
using NSOKHODO.Models;

namespace NSOKHODO.Kho
{
    /// <summary>
    /// Bang ten/loai mon dung chung cho so kho — CACHE RA DIA (<c>Data/Kho/tenmon.txt</c>).
    ///
    /// <para>Vi sao can: bang template chi co khi co it nhat mot acc dang nhap (server gui luc vao game,
    /// NSOLITEPRO khong cache). Mo app luc ca kho dang offline thi tong kho van phai hien duoc ten.</para>
    /// </summary>
    public static class BangMon
    {
        private static readonly object _lk = new object();
        private static Dictionary<short, ItemTemplate> _bang = new Dictionary<short, ItemTemplate>();
        private static int _soDaNap = -1;
        private static bool _canLuu;

        public static string DuongDan { get { return Path.Combine(Path.Combine(AppPaths.DataDir, "Kho"), "tenmon.txt"); } }

        public static int SoMon { get { lock (_lk) return _bang.Count; } }

        /// <summary>
        /// Chep bang template tu mot acc dang online. Id template = chi so vong lap (DataSyncParser) nen
        /// quet tuan tu 0..Count la du; chi chep lai khi so luong khac lan truoc.
        /// </summary>
        public static void NapTuStore(ItemTemplateStore store)
        {
            if (store == null) return;
            int n = store.Count;
            if (n <= 0 || n == _soDaNap) return;
            var moi = new Dictionary<short, ItemTemplate>(n);
            int trong = 0;
            for (int i = 0; i < 32000 && trong < 200; i++)
            {
                var t = store.Get((short)i);
                if (t == null) { trong++; continue; }
                trong = 0;
                moi[(short)i] = t;
            }
            lock (_lk)
            {
                _bang = moi;
                _soDaNap = n;
                _canLuu = true;
            }
        }

        public static ItemTemplate Lay(short tpl)
        {
            ItemTemplate t;
            lock (_lk) return _bang.TryGetValue(tpl, out t) ? t : null;
        }

        public static string Ten(short tpl)
        {
            var t = Lay(tpl);
            // Ten goc cua server co mon thua dau cach cuoi ("Nham Thạch ") -> tin nhan bi hai dau cach.
            string ten = t != null && t.Name != null ? t.Name.Trim() : "";
            return ten.Length > 0 ? ten : ("[" + tpl + "]");
        }

        /// <summary>Tim theo tu khoa (khong dau, khong phan biet hoa thuong). Toi da <paramref name="toiDa"/> ket qua.</summary>
        public static List<ItemTemplate> TimTheoTen(string tuKhoa, int toiDa)
        {
            var r = new List<ItemTemplate>();
            string k = ChuVan.ChuanHoa(tuKhoa);
            if (k.Length == 0) return r;
            lock (_lk)
            {
                foreach (var t in _bang.Values)
                {
                    if (t == null || string.IsNullOrEmpty(t.Name)) continue;
                    if (ChuVan.ChuanHoa(t.Name).Contains(k))
                    {
                        r.Add(t);
                        if (r.Count >= toiDa) break;
                    }
                }
            }
            return r;
        }

        public static void Nap()
        {
            try
            {
                if (!File.Exists(DuongDan)) return;
                var moi = new Dictionary<short, ItemTemplate>();
                foreach (var dong in File.ReadAllLines(DuongDan, Encoding.UTF8))
                {
                    var p = dong.Split('|');
                    short id; byte type; if (p.Length < 5) continue;
                    if (!short.TryParse(p[0], out id) || !byte.TryParse(p[1], out type)) continue;
                    moi[id] = new ItemTemplate
                    {
                        Id = id,
                        Type = type,
                        IsUpToUp = p[2] == "1",
                        Level = (byte)(int.Parse(p[3], CultureInfo.InvariantCulture) & 0xFF),
                        Name = p[4],
                    };
                }
                lock (_lk) { if (_bang.Count == 0) _bang = moi; }
            }
            catch { }
        }

        public static void LuuNeuCan()
        {
            List<ItemTemplate> ds;
            lock (_lk)
            {
                if (!_canLuu) return;
                _canLuu = false;
                ds = new List<ItemTemplate>(_bang.Values);
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DuongDan));
                var sb = new StringBuilder();
                foreach (var t in ds)
                    sb.Append(t.Id).Append('|').Append(t.Type).Append('|').Append(t.IsUpToUp ? "1" : "0")
                      .Append('|').Append(t.Level).Append('|').Append((t.Name ?? "").Replace('|', '/')).Append("\r\n");
                File.WriteAllText(DuongDan, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
