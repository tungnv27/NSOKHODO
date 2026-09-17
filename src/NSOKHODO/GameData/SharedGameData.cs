using System;
using System.Collections.Generic;
using System.Text;

namespace NSOKHODO.GameData
{
    /// <summary>
    /// Mot bo du lieu tinh cua server (item/skill/mob/map/npc template + bang effect type).
    /// Bat bien sau khi nap xong: chi duoc GHI trong luc DataSync (NotMapHandler), sau do
    /// moi account chi DOC -> chia se giua nhieu NsoClient la an toan.
    /// </summary>
    public class GameDataSet
    {
        public ItemTemplateStore ItemStore;
        public SkillTemplateStore SkillStore;
        public MobTemplateStore MobStore;
        public MapTemplateStore MapStore;
        public NpcTemplateStore NpcStore;
        public int[] EffectTypes;   // co the null neu server khong gui bang

        // Ten + icon hieu ung, CUNG mot goi DataSync voi EffectTypes.
        //
        // BUG DA SUA 2026-09-07: truoc day bo du lieu dung chung chi mang EffectTypes. Account DAU
        // TIEN chay DataSync day du nen co bang -> ve ICON; moi account sau nhan ban dung chung
        // ("DataSync skipped (cached)") nen _effectIcons = null -> GetEffectIcon() tra -1 -> roi
        // xuong nhanh CHU. Do chinh la trieu chung "luc hien icon, luc hien text" user bao.
        public string[] EffectNames;
        public int[] EffectIcons;
    }

    /// <summary>
    /// Kho dung chung cho CA TIEN TRINH, khoa theo (ten server + data version).
    ///
    /// Van de: moi account tu goi 4 lenh DataSync (~200-220 KB: 1093 item template kem
    /// name/description, 955 skill, 161 map, block "data" 80 KB) va giu rieng 1 ban trong RAM.
    /// 600 account = ~130 MB tai xuong moi lan mo fleet + 180-300 MB RAM trung lap, trong khi
    /// du lieu GIONG HET nhau vi cung server + cung version.
    ///
    /// Cach lam: account dau tien nap binh thuong roi Publish; cac account sau thay key da co
    /// -> dung lai ngay, KHONG gui 4 lenh DataSync nua.
    ///
    /// Chu y: KHONG cho account den sau "ghi ke" vao bo dang nap dang do (ItemTemplateStore
    /// .AddOption append vao List -> se nhan doi option). Bo dang nap chua Publish nen account
    /// den giua chung se tu nap ban rieng cua no - ton them 1 lan tai nhung tuyet doi an toan,
    /// va vi start giai cach nen chi vai account dau tien roi vao truong hop nay.
    /// Cung vi vay ham nay KHONG bao gio chan thread nhan packet de "cho ban kia nap xong".
    ///
    /// Mo hinh tham chieu co san trong repo: Navigator._wpCache (static + persist wp_cache.txt).
    /// </summary>
    public static class SharedGameData
    {
        private static readonly Dictionary<string, GameDataSet> _sets = new Dictionary<string, GameDataSet>();
        private static readonly object _lock = new object();

        /// <summary>Khoa = ten server + version 4 block server gui o sub -123.</summary>
        public static string MakeKey(string serverName, byte[] versions)
        {
            var sb = new StringBuilder();
            sb.Append(serverName == null ? "?" : serverName).Append('|');
            if (versions != null)
            {
                for (int i = 0; i < versions.Length; i++)
                    sb.Append(versions[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>Lay bo da nap xong cho khoa nay, null neu chua co.</summary>
        public static GameDataSet Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (_lock)
            {
                GameDataSet s;
                return _sets.TryGetValue(key, out s) ? s : null;
            }
        }

        /// <summary>
        /// Cong bo bo vua nap xong. Ban dau tien thang - cac ban trung sau bi bo qua
        /// (chung giong nhau, giu ban dau de moi account dang tro toi no van dung).
        /// Tra ve true neu ban nay duoc nhan lam ban dung chung.
        /// </summary>
        public static bool Publish(string key, GameDataSet set)
        {
            if (string.IsNullOrEmpty(key) || set == null) return false;
            lock (_lock)
            {
                if (_sets.ContainsKey(key)) return false;
                _sets[key] = set;
                return true;
            }
        }

        /// <summary>So bo du lieu dang giu (thuong = 1 neu ca fleet cung 1 server).</summary>
        public static int Count
        {
            get { lock (_lock) return _sets.Count; }
        }
    }
}
