using System.Collections.Generic;
using NSOKHODO.Models;

namespace NSOKHODO.GameData
{
    public class ItemTemplateStore
    {
        private readonly Dictionary<short, ItemTemplate> _items = new Dictionary<short, ItemTemplate>();
        // Option-template theo thu tu nap (index = OptionId server gui o cmd 42). Ten chua dau '#'.
        private readonly List<ItemOptionTemplate> _options = new List<ItemOptionTemplate>();

        public void Add(ItemTemplate item)
        {
            _items[item.Id] = item;
        }

        public void AddOption(string name, byte type)
        {
            _options.Add(new ItemOptionTemplate { Name = name, Type = type });
        }

        public ItemOptionTemplate GetOption(int id)
        {
            return id >= 0 && id < _options.Count ? _options[id] : null;
        }

        public ItemTemplate Get(short id)
        {
            ItemTemplate t;
            _items.TryGetValue(id, out t);
            return t;
        }

        public bool HasUpgrade(short templateId)
        {
            ItemTemplate t;
            if (_items.TryGetValue(templateId, out t))
                return t.HasUpgrade;
            // Default: assume no upgrade for unknown items
            return false;
        }

        /// <summary>
        /// Tra template theo TEN - dung cho Auto Danh Vong (server chi dinh mon do bang ten trong
        /// text nhiem vu, khong gui templateId). Dieu kien: trung ten VA
        /// (<c>Gender == 2</c> tuc unisex, HOAC dung gioi tinh nhan vat).
        /// ⚠️ Ten trong bang template KHONG co tien to dang "[176]" - tien to chi duoc ghep luc VE.
        /// </summary>
        public ItemTemplate FindByName(string name, int myGender)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in _items.Values)
            {
                if (t == null || t.Name == null) continue;
                if (!string.Equals(t.Name, name, System.StringComparison.Ordinal)) continue;
                if (t.Gender == 2 || t.Gender == myGender) return t;
            }
            return null;
        }

        public int Count { get { return _items.Count; } }

        public void Clear()
        {
            _items.Clear();
            _options.Clear();
        }
    }
}
