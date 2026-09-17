using System;
using System.Collections.Generic;

namespace NSOKHODO.Models
{
    public class CharacterState
    {
        // Identity
        public int CharId { get; set; }
        public string Name { get; set; }
        public string ClanName { get; set; }
        public byte ClanType { get; set; }
        public byte Gender { get; set; }
        public short Head { get; set; }
        public byte Speed { get; set; }
        public byte ClassId { get; set; }
        public byte TaskId { get; set; }

        // Nhiem vu hang ngay (cmd 96, TaskOrder taskId==0 = Char.j(0)). MapId = map can toi
        // (server doi moi ngay). Dung cho "Di map nhiem vu qua NPC 25". -1 = chua nhan.
        public int DailyQuestMapId { get; set; } = -1;
        public int DailyQuestKillId { get; set; } = -1;

        // Nhiem vu CHINH TUYEN dang lam (cmd 47/48/50; cmd 49 + NOT_MAP sub -98 xoa). null = chua nhan.
        // Chi luong nhan goi GHI, moi lan doi gan doi tuong moi -> UI doc 1 lan vao bien cuc bo.
        public MainTask MainTask { get; set; }

        // Nhiem vu PHU TUYEN (cmd 96 them / 97 cap nhat / 98 xoa). COPY-ON-WRITE: doi thi gan mang moi.
        public TaskOrderInfo[] TaskOrders { get; set; } = new TaskOrderInfo[0];

        // PK
        public byte Pk { get; set; }
        public byte TypePk { get; set; }

        // Stats
        public int MaxHp { get; set; }
        public int Hp { get; set; }

        /// <summary>
        /// HP do SERVER noi ra, va CHI server - khong bao gio bi ghi bang so bia.
        ///
        /// Vi sao tach khoi <see cref="Hp"/>: khi nhan MAP_INFO sau luc chet, NsoClient dat
        /// <c>Hp = MaxHp</c> de "tu phong song" (chua chac server dong y). Tu do <c>Hp</c> khong con
        /// la su that - cua so Xem game hien thanh mau day trong khi nhan vat da la XAC tren server
        /// (user bao 2026-09-07: "trong anh nhan vat con HP nhung that ra da het mau roi").
        ///
        /// ZangVPS biet minh chet BANG CHINH HP: <c>Z.b()</c> tra ve chet khi <c>bU &lt;= 0</c>
        /// (Z.java:3561-3573), va <c>bU</c> chi duoc ghi tu goi server + <c>ax.dv()</c> (cmd -10/88).
        /// Day la ban sao cua <c>bU</c>: nguon DUY NHAT de tra loi "minh con song that khong".
        /// -1 = chua he nhan duoc con so nao tu server.
        /// </summary>
        public int ServerHp { get; set; }

        /// <summary>Luc (UTC) <see cref="ServerHp"/> duoc server cap nhat lan cuoi.</summary>
        public DateTime ServerHpAt { get; set; }

        /// <summary>MaxHp kem theo lan cuoi server bao HP (de doi chieu, khong bi so bia de len).</summary>
        public int ServerHpMaxSeen { get; set; }

        /// <summary>
        /// Luc (UTC) SERVER XAC NHAN nhan vat CON SONG. Ba nguon, deu la loi server chu khong phai
        /// suy dien cua ta: <c>cmd -10</c> / <c>cmd 88</c> (hoi sinh) va moi lan bao HP &gt; 0.
        ///
        /// ===== VI SAO CAN MOT MOC RIENG (2026-09-08 dd11, do tren log that) =====
        /// Server nay CHI noi HP cua chinh minh khi no BANG 0. Dem tren phien 02:21-02:25:
        /// bao HP = 0 **193 lan**, bao HP &gt; 0 dung **15 lan** - va ca 15 deu la goi luc DANG NHAP
        /// (dung bang so account). Tuc trong suot phien choi, khong bao gio co "may con X mau".
        /// He qua: sau MOI lan hoi sinh, <see cref="ServerHp"/> nam lai o 0 VINH VIEN cho toi lan
        /// chet sau. Bat cu chot nao doc `ServerHp &lt;= 0` deu se dung tren mot con so DA BI CHINH
        /// SERVER PHU NHAN, va se bao dong tren ca acc dang danh khoe.
        /// Ca cu the: barbigz114 chet 02:23:19, server xac nhan hoi sinh 02:23:21, nhung dong
        /// chan doan 02:23:25 van doc "server bao 0/1651 cach day 6s" - so 0 do la goi bao CHET
        /// da bi lat 2 giay sau do.
        ///
        /// ⚠️ CO Y KHONG gan `ServerHp = MaxHp` o cac cho hoi sinh: lam vay la BIA SO, dung cai sai
        /// da de ra toan bo chuyen "HP ao". O day chi ghi mot su that kiem chung duoc: "luc T server
        /// noi ta con song". Doc: goi bao 0 chi dang tin khi no MOI HON moc nay.
        /// </summary>
        public DateTime ServerAliveAt { get; set; }
        public int MaxMp { get; set; }
        public int Mp { get; set; }
        public long Exp { get; set; }
        public long ExpDown { get; set; }
        public byte Level { get; set; }

        // Buffs
        public short BuffHp { get; set; }
        public short BuffMp { get; set; }

        // Potential
        public short PotentialPoint { get; set; }
        public short Potential0 { get; set; }
        public short Potential1 { get; set; }
        public int Potential2 { get; set; }
        public int Potential3 { get; set; }
        public short SkillPoint { get; set; }

        // Economy
        public int Xu { get; set; }
        public int Yen { get; set; }
        public int Luong { get; set; }

        // Position
        public short Cx { get; set; }
        public short Cy { get; set; }

        // Flags
        public bool IsHuman { get; set; }
        public bool IsNhanban { get; set; }
        public bool IsDead { get; set; }

        // Inventory
        public Item[] BagItems { get; set; }

        /// <summary>
        /// Do dang MAC - <b>32 o</b>, y het `arrItemBody = new Item[32]` cua game
        /// (Controller.cs:4389/4617). Popup Trang bi trong game co nut lat trang
        /// (`GameScr.indextabTrangbi` 0 &lt;-&gt; 16):
        /// <list type="bullet">
        /// <item><b>0..15 = "Trang bi 1"</b> - non, vu khi, ao, vong co, gang tay, nhan, quan, boi,
        /// giay, bua, thu nuoi, mat na, ninja yoroi, gia toc, (chua mo), bi kip.</item>
        /// <item><b>16..31 = "Trang bi 2"</b> - bo thu hai, CUNG bo cuc o. Server gui o KHOI THU HAI
        /// cuoi goi login/115, game dat vao `arrItemBody[template.type + 16]`.</item>
        /// </list>
        /// Truoc 2026-09-07 mang chi dai 16 va khoi thu hai bi doc nham ten thanh "FashionBody" roi
        /// vut di. Cac engine chi duoc phep dung 0..9 (do that) van tu kep - xem DapDoRunner.ChonMon.
        /// </summary>
        public Item[] BodyItems { get; set; }

        /// <summary>So o moi TRANG trang bi (game: buoc lat `indextabTrangbi`).</summary>
        public const int BODY_PAGE = 16;

        /// <summary>Tong so o trang bi ca hai trang.</summary>
        public const int BODY_SLOTS = 32;

        /// <summary>
        /// RUONG (ky gui). NULL = CHUA tung xin danh sach - y het `arrItemBox == null` cua MODGAME,
        /// va engine dap do dung dung dau hieu do de biet luc nao phai gui sub -103 requestItem(4).
        /// Server tra ve qua cmd 31 (BOX_ITEM_LIST); o trong = Item rong (TemplateId -1) nhu tui.
        /// </summary>
        public Item[] BoxItems { get; set; }

        /// <summary>Xu dang gui trong ruong (cmd 31 tra ve dau goi).</summary>
        public int XuInBox { get; set; }

        /// <summary>
        /// THU CUOI - 5 o (game: <c>Char.arrItemMounts</c>, `typeUI = 41`):
        /// <list type="bullet">
        /// <item><b>0..3</b> = trang suc. Thu thuong: trang suc / ao giap / yen / cuong.
        /// Mo-to: bo dieu khien / dong co / dinh vi / binh nitro (game doi nhan theo loai thu).</item>
        /// <item><b>4</b> = CHINH CON THU. Cap hien thi = <c>Upgrade + 1</c>, so sao = <c>Sys + 1</c>,
        /// con Kinh nghiem / Sinh luc nam trong <c>Options</c> (id 65 / 66) chu KHONG phai field rieng.</item>
        /// </list>
        /// Server gui qua <c>cmd -30 sub -54</c>. O trong = <see cref="Item"/> rong (TemplateId -1),
        /// giong tui/trang bi - de UI khoi phai kiem null.
        /// </summary>
        public Item[] MountItems { get; set; }

        /// <summary>So o Thu cuoi (4 trang suc + 1 con thu).</summary>
        public const int MOUNT_SLOTS = 5;

        /// <summary>Chi so o chua CHINH CON THU trong <see cref="MountItems"/>.</summary>
        public const int MOUNT_BEAST_SLOT = 4;
        public List<short> SkillIds { get; set; }

        /// <summary>
        /// Skill dang trang bi/chon cua nhan vat (server gui qua sub-command "CSkill", cmd -65).
        /// -1 = chua nhan duoc. TrainMode dung lam skill danh mac dinh (giong Java myskill).
        /// </summary>
        public int CurrentSkillId { get; set; }

        // Fashion
        public short[] Fashion { get; set; }

        public CharacterState()
        {
            BagItems = new Item[0];
            BodyItems = NewBody();
            MountItems = NewMounts();
            SkillIds = new List<short>();
            CurrentSkillId = -1;
            // -1 = CHUA CO thoi trang. Bat buoc, khong duoc de 0 mac dinh: luat client la
            // "if (fashion[i] > -1) thi de len trang bi" (NinjaSchool_251 Controller.cs:4662-4676),
            // nen mang toan 0 se ep MOI nhan vat ve part 0 ngay ca khi server chua gui gi.
            Fashion = new short[4];
            for (int i = 0; i < 4; i++) Fashion[i] = -1;
        }

        /// <summary>
        /// Mang trang bi rong dung co (<see cref="BODY_SLOTS"/> o, moi o mot <see cref="Item"/> rong).
        /// KHONG de o nao null: ca UI lan engine deu kiem `IsEmpty` chu khong kiem null.
        /// </summary>
        public static Item[] NewBody()
        {
            var a = new Item[BODY_SLOTS];
            for (int i = 0; i < BODY_SLOTS; i++) a[i] = new Item();
            return a;
        }

        /// <summary>Mang Thu cuoi rong dung co (<see cref="MOUNT_SLOTS"/> o rong, khong o nao null).</summary>
        public static Item[] NewMounts()
        {
            var a = new Item[MOUNT_SLOTS];
            for (int i = 0; i < MOUNT_SLOTS; i++) a[i] = new Item();
            return a;
        }

        /// <summary>
        /// Bao dam BagItems du dai cho o `slot` (game goc arrItemBag duoc server cap size sat -
        /// khong bao gio tran; ben ta co the tran neu size login lech / mo rong tui giua phien).
        /// Grow (giu item cu, do o trong) thay vi lang le bo goi cmd 8/7 -> item nhat vao bi mat.
        /// Tra false neu slot vo ly (>127 = rac, khong grow). Cap 128 = du cho tui + mo rong toi da.
        /// </summary>
        public bool EnsureBagSlot(int slot)
        {
            if (slot < 0 || slot > 127) return false;
            if (BagItems == null) BagItems = new Item[0];
            if (slot < BagItems.Length) return true;
            var grown = new Item[slot + 1];
            for (int i = 0; i < grown.Length; i++)
                grown[i] = i < BagItems.Length ? BagItems[i] : new Item();
            BagItems = grown;
            return true;
        }

        /// <summary>Dem so o RUONG con trong (clone Char.ag()). Chua xin danh sach -> 0.</summary>
        public int FreeBoxSlots()
        {
            var box = BoxItems;
            if (box == null) return 0;
            int n = 0;
            for (int i = 0; i < box.Length; i++)
                if (box[i] == null || box[i].IsEmpty) n++;
            return n;
        }
    }
}
