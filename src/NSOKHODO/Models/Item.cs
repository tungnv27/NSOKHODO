using System.Collections.Generic;

namespace NSOKHODO.Models
{
    public class Item
    {
        public short TemplateId { get; set; }
        public bool IsLock { get; set; }
        public byte Upgrade { get; set; }
        public bool IsExpires { get; set; }
        public ushort Quantity { get; set; }
        public byte Sys { get; set; }

        // Chi tiet nap rieng qua cmd 42 (REQUEST_ITEM_INFO) khi nguoi dung xem item:
        public long Expires { get; set; }              // timestamp ms het han (-1/0 = vinh vien)
        public List<ItemOption> Options { get; set; }  // option/tinh nang (trang bi/ngoc kham)
        public bool DetailLoaded { get; set; }         // da nhan tra loi cmd 42 chua

        /// <summary>
        /// GIA BAN (yen) server bao o cmd 42 - truong <c>saleCoinLock</c>, doc ngay sau
        /// <see cref="Expires"/> (<c>Controller/ItemHandler.cs</c>). Truoc 2026-09-12 gia tri nay
        /// bi <c>ReadInt()</c> roi VUT DI; nay giu lai vi LOC DO quyet dinh theo no
        /// (docs/features/LOC_DO.md §C).
        ///
        /// <para><b>Chi co nghia khi <see cref="DetailLoaded"/> = true</b> - truoc do 0 chi la gia
        /// tri mac dinh. Da co chi tiet ma van 0 thi CA HAI mode tu dong deu coi la can cu de vut:
        /// Vip gom 0 vao nhanh "&lt;= 5 yen -> vut" (dung <c>Class_ed.java:523</c> NSOTRUNGDUC, sua
        /// 2026-09-14 - truoc day Vip coi 0 la "chua ro gia"), mode "du chi so" vut TRANG BI THU
        /// (type 29-32) gia 0. Xem <c>Auto/AddOns/LocDoRunner.cs</c>.</para>
        /// </summary>
        public int SaleCoinLock { get; set; }

        public bool IsEmpty { get { return TemplateId == -1; } }

        public Item()
        {
            TemplateId = -1;
        }

        public override string ToString()
        {
            if (IsEmpty) return "(empty)";
            return string.Format("Item[{0}] x{1} +{2}", TemplateId, Quantity, Upgrade);
        }
    }
}
