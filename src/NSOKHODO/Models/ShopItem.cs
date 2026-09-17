using System.Collections.Generic;

namespace NSOKHODO.Models
{
    /// <summary>Mot dong trong bang shop (cmd 33) + gia lay ve sau bang cmd 42.</summary>
    public class ShopItem
    {
        /// <summary>O trong bang shop - la thu PHAI gui trong cmd 13 (buyItem) va cmd 42.</summary>
        public int IndexUI;
        public short TemplateId;

        // ===== GIA - TEN THAT theo NinjaSchool_251_src, KHONG theo ten cua MODGAME =====
        // MODGAME dat ten 3 field nay LECH 1 SLOT (buyCoinLock/buyGold/buyGoldLock) vi bi
        // obfuscate; wire thi giong het nhau. Chep ten cua ho sang day se tinh nham loai tien
        // y het bug §J.1. Bang chung: chuoi hien thi trung vi tri o ca 2 ban
        // ("Gia mua: # xu" / "# yen" / "# luong") + Item cua MODGAME KHONG co field buyCoin.
        /// <summary>Slot int #1 tren day = XU.</summary>
        public int BuyCoin;
        /// <summary>Slot int #2 tren day = YEN.</summary>
        public int BuyCoinLock;
        /// <summary>Slot int #3 tren day = LUONG.</summary>
        public int BuyGold;

        /// <summary>Da hoi gia bang cmd 42 va nhan duoc chua.</summary>
        public bool PriceLoaded;
    }

    /// <summary>
    /// Cac bang shop server da gui (cmd 33), tra cuu theo <c>typeUI</c>.
    /// Bang typeUI -> shop o DANH_VONG.md §B.4: 2=vu khi · 16..19=lien/nhan/ngoc boi/phu ·
    /// 20..29=trang phuc nam/nu · 14=tap hoa (luong) · 4=ruong.
    /// </summary>
    public class ShopTables
    {
        private readonly Dictionary<int, ShopItem[]> _tables = new Dictionary<int, ShopItem[]>();
        private readonly object _gate = new object();

        public void Set(int typeUI, ShopItem[] items)
        {
            lock (_gate) { _tables[typeUI] = items; }
        }

        /// <summary>Tra ve bang shop, hoac null neu chua nhan duoc cmd 33 cho typeUI do.</summary>
        public ShopItem[] Get(int typeUI)
        {
            lock (_gate)
            {
                ShopItem[] v;
                return _tables.TryGetValue(typeUI, out v) ? v : null;
            }
        }

        /// <summary>Tim dong dau tien co templateId nay trong bang. null = bang chua co / khong ban mon do.</summary>
        public ShopItem Find(int typeUI, int templateId)
        {
            var arr = Get(typeUI);
            if (arr == null) return null;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] != null && arr[i].TemplateId == templateId) return arr[i];
            return null;
        }

        public ShopItem FindByIndex(int typeUI, int indexUI)
        {
            var arr = Get(typeUI);
            if (arr == null) return null;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] != null && arr[i].IndexUI == indexUI) return arr[i];
            return null;
        }

        public void Reset()
        {
            lock (_gate) { _tables.Clear(); }
        }
    }
}
