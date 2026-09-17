using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;

namespace NSOKHODO.Controller
{
    public class ItemHandler
    {
        public GameStateManager State { get; private set; }

        public ItemHandler(GameStateManager state)
        {
            State = state;
        }

        public void HandleItemMapAdd(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                var item = new ItemOnMap();
                item.ItemMapId = r.ReadShort();
                item.TemplateId = r.ReadShort();
                item.X = r.ReadShort();
                item.Y = r.ReadShort();
                // Sau 4 short co the con byte[] captcha (int-len) - khong dung, bo qua phan con lai.
                State.CurrentMap.AddItemOnMap(item);
            }
            catch { }
        }

        /// <summary>
        /// cmd=-6: item roi tai vi tri 1 char (MODGAME Controller case -6). Format:
        /// charId int, itemMapId short, templateId short, xEnd short, yEnd short.
        /// MODGAME bo ca goi neu char khong ton tai; bot dung thang xEnd/yEnd (vi tri settle)
        /// nen them duoc ke ca khi chua thay char do.
        /// </summary>
        public void HandleItemMapAddAtChar(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                r.ReadInt(); // charId - chi de client ve animation roi tu char, bot khong can
                var item = new ItemOnMap();
                item.ItemMapId = r.ReadShort();
                item.TemplateId = r.ReadShort();
                item.X = r.ReadShort();
                item.Y = r.ReadShort();
                State.CurrentMap.AddItemOnMap(item);
            }
            catch { }
        }

        /// <summary>
        /// cmd=-13: nguoi KHAC nhat item (MODGAME Controller case -13: itemMapId short, charId int).
        /// Xoa khoi danh sach ngay - khong xoa thi item "ma" ton dong, bot chay toi nhat do khong con.
        /// </summary>
        public void HandleItemMapPickOther(NsoMessage msg)
        {
            try
            {
                short itemMapId = msg.Reader.ReadShort();
                State.CurrentMap.RemoveItemOnMap(itemMapId);
            }
            catch { }
        }

        public void HandleItemMapRemove(NsoMessage msg)
        {
            try
            {
                short itemMapId = msg.Reader.ReadShort();
                State.CurrentMap.RemoveItemOnMap(itemMapId);
            }
            catch { }
        }

        /// <summary>
        /// cmd=-14: ket qua NHAT do (clone game Controller case -14). Format:
        ///   itemMapId(short); NEU do la VANG (template type 19) thi co them unsignedShort = so yen.
        /// Phan biet bang DO DAI payload: 4 byte = co yen (itemMapId 2 + amount 2); 2 byte = do thuong.
        /// Day la duong CAP NHAT YEN REAL-TIME khi farm. Truoc day NSOKHODO chi GUI cmd -14 ma KHONG
        /// xu ly goi tra ve -> yen tren UI khong doi luc train, chi doi khi relog (char info day du).
        ///
        /// XOA ITEM KHOI MAT DAT NGAY TAI DAY. Cho toi truoc, chu thich cu ghi "item bi xoa qua cmd
        /// -15 rieng" - SAI, va la goc re cua "HP/MP roi day san": server KHONG gui -15 cho mon minh
        /// vua nhat. Ca hai client tu xoa lay: case -14 goi ItemMap.setPoint(cx, cy-10) dat
        /// status = 2, roi ItemMap.update() xoa khi anh bay toi noi
        /// (NinjaSchool_251 ItemMap.cs:62-70 + Controller.cs:1767; MODGAME GameScr.java:4101-4104 +
        /// Controller.java:417). Bot khong ve animation bay nen xoa thang.
        /// Hau qua cu khong chi xau mat: TrainMode cu 10s lai chay toi nhat lai tung mon ma.
        /// </summary>
        public void HandleItemPickResult(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                short itemMapId = r.ReadShort();
                if (State.CurrentMap != null) State.CurrentMap.RemoveItemOnMap(itemMapId);
                if (msg.DataLength >= 4 && State.MyChar != null)
                {
                    int yenGot = r.ReadUnsignedShort();
                    State.MyChar.Yen += yenGot;
                    // THONG BAO NHAN (thanh chay chu day man) - clone Controller.java:435:
                    //   InfoMe.addInfo(mResources.ku + " " + soYen + " " + mResources.kj)
                    // ku = "Ban nhan duoc", kj = "Yen". CHI yen moi bao o day.
                    if (State.Notices.Enabled)
                        State.Notices.Push("Bạn nhận được " + yenGot + " Yên");
                }
                // Do thuong (khong phai yen): item vao tui qua cmd 8 (o moi) hoac cmd 9 (cong stack) rieng.
                // ⚠️ Dung tim cho bao "nhat duoc <ten mon>" o day - ban goc cung khong bao o cmd -14,
                // no chi chay animation bay ve nguoi. Dong ten mon den tu cmd 8/9 (Controller.java:919/966).
            }
            catch { }
        }

        /// <summary>
        /// Cmd 11 chieu nhan: ket qua dung item. Verify tu MODGAME Controller case 11:
        /// readByte slot -> Char.a(slot) (chi swap trang bi, bo qua) -> Char.a(msg) doc
        /// speed/maxHp/maxMp -> 2 short eff5BuffHp/Mp. HP/MP thuc te ve qua SubCmd HP/MP_UPDATE.
        /// </summary>
        public void HandleUseItemResult(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                var c = State.MyChar;
                if (c == null) return;

                // Giong game Char.useItem(slot) (goi TRUOC readParam, thuan local khong doc stream):
                // item trang bi -> MAC vao nguoi (hoan doi tui<->body); item THU CUOI -> deo vao o
                // thu cuoi (hoan doi tui<->mounts). Do an/thuoc -> server gui cmd 18 rieng.
                EquipFromBag(c, slot);

                c.Speed = r.ReadByte();
                c.MaxHp = r.ReadInt();
                c.MaxMp = r.ReadInt();
                // 2 short eff5BuffHp/eff5BuffMp - chua dung, doc bo cho sach stream
                r.ReadShort();
                r.ReadShort();
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 15: ket qua THAO trang bi (clone Char.itemBodyToBag 251). Server chuyen 1 mon tu o
        /// body ve o tui. Format: byte speed, int maxHp, int maxMp, short buffHp, short buffMp,
        /// ubyte bodyIndex (o body thao ra), ubyte bagIndex (o tui nhan), short head.
        /// </summary>
        public void HandleItemBodyToBag(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                var c = State.MyChar;
                if (c == null) return;

                c.Speed = r.ReadByte();
                c.MaxHp = r.ReadInt();
                c.MaxMp = r.ReadInt();
                r.ReadShort(); // eff5BuffHp - chua dung
                r.ReadShort(); // eff5BuffMp - chua dung

                int bodyIndex = r.ReadUnsignedByte(); // o body thao ra
                int bagIndex = r.ReadUnsignedByte();   // o tui nhan mon vua thao
                c.Head = r.ReadShort();

                var body = c.BodyItems;
                var bag = c.BagItems;
                if (body != null && bodyIndex >= 0 && bodyIndex < body.Length)
                {
                    var it = body[bodyIndex];
                    body[bodyIndex] = new Item(); // o body -> trong
                    if (it != null && !it.IsEmpty && bag != null && bagIndex >= 0 && bagIndex < bag.Length)
                        bag[bagIndex] = it;
                }
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 108: ket qua THAO THU CUOI / trang suc thu ve tui — clone
        /// <c>Char.itemMonToBag(Message)</c> (`NinjaSchool_251_src/Char.cs:11997-12020`).
        /// Wire: <c>byte speed, int maxHp, int maxMp, short buffHp, short buffMp,
        /// ubyte mountIndex, ubyte bagIndex</c>.
        ///
        /// <para>⚠️ KHAC cmd 15 o cho <b>khong co short head o cuoi</b> — thao trang suc thu khong
        /// doi hinh dang nhan vat.</para>
        ///
        /// <para>Truoc 2026-09-12 ta <b>GUI</b> cmd 108 (nut "Tháo" o tab Thú cưỡi) nhung router
        /// KHONG co nhanh nhan → goi tra ve roi vao <c>default</c>, mon khong bao gio ve tui trong
        /// trang thai cua bot. Xem `docs/features/GIAO_TIEP_VAT_PHAM.md` §3 (ca A4).</para>
        /// </summary>
        public void HandleItemMountToBag(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                var c = State.MyChar;
                if (c == null) return;

                c.Speed = r.ReadByte();
                c.MaxHp = r.ReadInt();
                c.MaxMp = r.ReadInt();
                r.ReadShort(); // eff5BuffHp - chua dung
                r.ReadShort(); // eff5BuffMp - chua dung

                int mountIndex = r.ReadUnsignedByte(); // o thu cuoi thao ra
                int bagIndex = r.ReadUnsignedByte();   // o tui nhan mon vua thao

                var mounts = c.MountItems;
                if (mounts != null && mountIndex >= 0 && mountIndex < mounts.Length)
                {
                    var it = mounts[mountIndex];
                    mounts[mountIndex] = new Item(); // o thu cuoi -> trong
                    if (it != null && !it.IsEmpty && c.EnsureBagSlot(bagIndex))
                        c.BagItems[bagIndex] = it;
                }
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Hoan doi tui ↔ trang bi / thu cuoi khi "Dung" mot mon deo duoc — clone nguyen
        /// <c>Char.useItem(indexUI)</c> (`NinjaSchool_251_src/Char.cs:1158-1223`). Thuan local,
        /// KHONG doc stream (ban goc cung goi truoc <c>readParam</c>).
        ///
        /// <para>Ban goc co <b>HAI</b> nhanh, va truoc 2026-09-12 ta chi lam nhanh dau:</para>
        /// <list type="number">
        /// <item><b>isTypeBody()</b> (type 0..15) → o <c>BodyItems[template.type]</c>.</item>
        /// <item><b>isTypeMounts()</b> (type 29..33) → o <c>MountItems[template.type - 29]</c>
        ///       (`Char.cs:1198-1222`). <b>Thieu nhanh nay = deo trang suc thu cuoi xong, mon
        ///       KET LAI trong tui vinh vien</b> cho toi khi co mot lan re-sync (sub 115 / relogin)
        ///       — dung triệu chung "dung roi ma van con trong hanh trang, bam Sap xep thi mat".</item>
        /// </list>
        /// Ca hai nhanh deu <b>hoan doi</b>: mon dang deo (neu co) bi day nguoc ve DUNG o tui vua
        /// trong ra, y het ban goc.
        /// </summary>
        private void EquipFromBag(CharacterState c, int slot)
        {
            var bag = c.BagItems;
            if (bag == null || slot < 0 || slot >= bag.Length) return;
            var it = bag[slot];
            if (it == null || it.IsEmpty) return;
            var tpl = State.ItemStore != null ? State.ItemStore.Get(it.TemplateId) : null;
            if (tpl == null) return;

            // --- Nhanh 1: trang bi thuong (Char.cs:1161-1194) ---
            if (tpl.IsTypeBody)
            {
                var body = c.BodyItems;
                if (body == null) return;
                int t = tpl.Type; // o body = template.type (0..15)
                if (t < 0 || t >= body.Length) return;
                it.IsLock = true;
                var prev = body[t];
                bag[slot] = (prev != null && !prev.IsEmpty) ? prev : new Item();
                body[t] = it;
                return;
            }

            // --- Nhanh 2: THU CUOI / trang suc thu (Char.cs:1198-1222) ---
            if (tpl.IsTypeMount)
            {
                var mounts = c.MountItems;
                if (mounts == null) return;
                int m = tpl.Type - 29; // ban goc: `int num = item.template.type - 29`
                if (m < 0 || m >= mounts.Length) return;
                it.IsLock = true;
                var prev = mounts[m];
                bag[slot] = (prev != null && !prev.IsEmpty) ? prev : new Item();
                mounts[m] = it;
                return;
            }

            // Moi type khac: ban goc `return` ngay, khong dong vao tui — do an/thuoc/sach... deu
            // do server bao rieng (cmd 18 / cmd 10 / sub -102).
        }

        /// <summary>
        /// Cmd -12: ket qua VUT item. Wire ban goc (`NinjaSchool_251_src/Controller.cs:1971`):
        /// <c>byte slot, short itemMapId, short x, short y</c> → xoa o tui VA tha mon xuong dat.
        ///
        /// <para>⚠️ <b>CO Y LECH BAN GOC: ta KHONG them mon vua vut vao danh sach do tren dat.</b>
        /// Client goc co nguoi choi tu quyet dinh nhat lai; bot thi <c>TrainMode</c> quet do roi
        /// moi vai giay → them vao la sinh vong "vut ra rồi nhặt lại rồi lại vut". Doc byte con
        /// lai cung khong can: moi goi tin co buffer rieng, khong doc het khong lech goi sau.</para>
        /// </summary>
        public void HandleItemThrowResult(NsoMessage msg)
        {
            try
            {
                int slot = msg.Reader.ReadByte() & 0xFF;
                var bag = State.MyChar != null ? State.MyChar.BagItems : null;
                if (bag == null || slot >= bag.Length) return;
                bag[slot] = new Item(); // TemplateId -1 = o trong
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 14: ket qua BAN cho NPC lay YEN — clone `NinjaSchool_251_src/Controller.cs:2478-2506`:
        /// <c>byte slot, int yen, [short soLuongDaBan, thieu/EOF = 1]</c> → tru dan so luong,
        /// ve 0 thi don o, <b>va cap nhat Yen tu goi</b>.
        ///
        /// <para><b>Truoc 2026-09-12 lenh nay bi route thang sang <c>HandleItemThrowResult</c></b>
        /// (doc DUNG MOT byte roi xoa sach o tui). Hai hau qua:</para>
        /// <list type="bullet">
        /// <item>Ban 1 binh trong chong 50 → ta xoa ca 50 khoi trang thai; bam "Sap xep" (server
        ///       re-sync sub 115) la 49 cai quay lai.</item>
        /// <item><b>Mat duong cap nhat Yen</b> → cot Yen/h khong dem tien ban do.</item>
        /// </list>
        ///
        /// </summary>
        /// <remarks>
        /// ⚠️ <b>VI SAO RE NHANH THEO DO DAI GOI thay vi doc thang khuon ban goc.</b>
        /// <para>`SERVER_FACTS.md §4` (do hex 2026-06-15) ghi rang server NAY tra ket qua <b>VUT</b>
        /// bang cmd 14 voi <c>byte0 = slot</c>. Client goc thi tra ket qua vut bang <c>cmd -12</c>
        /// (<c>byte slot, short itemMapId, short x, short y</c> = <b>7 byte</b>) va dung cmd 14 cho
        /// BAN. Ta <b>khong the phan biet</b> mot goi cmd 14 dai 7 byte la "ban kem so luong" hay
        /// "ACK vut" — doc nham thi <c>Yen</c> nhan mot so rac (itemMapId ghep voi x) va cot Yên/h
        /// hong theo.</para>
        /// <list type="bullet">
        /// <item><b>len = 5</b> → chi co the la BAN (khuon ban goc thieu truong so luong):
        ///       cap nhat Yen + tru 1.</item>
        /// <item><b>moi do dai khac</b> → giu hanh vi cu (don ca o), <b>khong dung vao Yen</b>.
        ///       Day la <i>khong lui</i> so voi truoc, khong phai thu hep tinh nang.</item>
        /// </list>
        /// <para>Moi hinh dang goi cmd 14 duoc ghi hex <b>mot lan/phien</b> (<c>[wire] cmd14 len=…</c>).
        /// Mot lan test co ban co vut la du ket luan, luc do bo hang <c>else</c> di va doc thang
        /// khuon ban goc cho moi do dai.</para>
        /// </remarks>
        public void HandleSaleResult(NsoMessage msg)
        {
            try
            {
                GhiHexMotLan("cmd14", msg);

                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                var c = State.MyChar;
                var bag = c != null ? c.BagItems : null;
                if (bag == null || slot >= bag.Length || bag[slot] == null) return;

                // Re nhanh theo DO DAI GOI - xem khoi <remarks> duoi ham nay de biet vi sao.
                if (msg.DataLength == 5)
                {
                    // KHONG NHAP NHANG: dung khuon ban goc khi thieu truong so luong
                    // (byte slot + int yen). Ban 1 mon.
                    int yen = r.ReadInt();
                    if (yen >= 0) c.Yen = yen;          // Yen la TONG tuyet doi, khong phai delta.
                    if (bag[slot].Quantity > 1)
                        bag[slot].Quantity = (ushort)(bag[slot].Quantity - 1);
                    else
                        bag[slot] = new Item();
                }
                else
                {
                    // Con lai: don ca o (hanh vi cu, da chay tren server that tu 2026-06-15).
                    bag[slot] = new Item();
                }
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 102: ket qua BAN lay XU (`Controller.cs:2451-2476`): <c>byte slot, int xu</c>.
        /// Khac cmd 14 o hai cho: tra bang <b>Xu</b>, va <b>xoa han o tui</b> chu khong tru dan.
        /// Truoc 2026-09-12 lenh nay khong duoc dinh nghia lan xu ly.
        /// </summary>
        public void HandleSaleForXuResult(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                var c = State.MyChar;
                if (c == null) return;

                int xu = r.ReadInt();
                if (xu >= 0) c.Xu = xu;

                var bag = c.BagItems;
                if (bag != null && slot < bag.Length)
                    bag[slot] = new Item();
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 112: doi CAP NANG CAP cua 1 mon trong tui tai cho (`Controller.cs:1135-1140`):
        /// <c>byte slot, byte upgrade</c> + <c>expires = 0</c>. Khong doi o, khong doi so luong.
        /// </summary>
        public void HandleItemUpgradeChange(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                byte up = r.ReadByte();
                var bag = State.MyChar != null ? State.MyChar.BagItems : null;
                if (bag == null || slot >= bag.Length || bag[slot] == null || bag[slot].IsEmpty) return;
                bag[slot].Upgrade = up;
                bag[slot].Expires = 0L;     // ban goc: item.expires = 0 (het han bi xoa)
                bag[slot].IsExpires = false;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        // Ghi hex cua MOT hinh dang goi DUY NHAT moi phien - dung de ket luan tranh chap wire
        // ma khong lam ngap log. Khoa theo (tag, do dai goi).
        private readonly System.Collections.Generic.HashSet<string> _daGhiHex
            = new System.Collections.Generic.HashSet<string>();

        private void GhiHexMotLan(string tag, NsoMessage msg)
        {
            try
            {
                string key = tag + ":" + msg.DataLength;
                lock (_daGhiHex) { if (!_daGhiHex.Add(key)) return; }
                var d = msg.GetData();
                int n = d.Length < 16 ? d.Length : 16;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < n; i++) sb.Append(d[i].ToString("X2")).Append(' ');
                State.RaiseDebugLog("[wire] " + tag + " len=" + d.Length + " hex=" + sb.ToString().Trim());
            }
            catch { }
        }

        /// <summary>
        /// Cmd 22: ket qua TACH item (clone Controller 251 case 22). Format: byte n, roi n lan
        /// {byte slot, short templateId} - moi mon moi co quantity 1. Ghi vao tui roi bao UI.
        /// </summary>
        public void HandleItemSplitResult(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int n = r.ReadByte() & 0xFF;
                var bag = State.MyChar != null ? State.MyChar.BagItems : null;
                for (int i = 0; i < n; i++)
                {
                    int slot = r.ReadByte() & 0xFF;
                    short tid = r.ReadShort();
                    if (bag != null && slot < bag.Length)
                    {
                        var it = new Item();
                        it.TemplateId = tid;
                        it.Quantity = 1;
                        it.Expires = -1L;
                        bag[slot] = it;
                    }
                }
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 18: server tru so luong item trong tui (verify MODGAME Controller case 18).
        /// readByte slot + readShort qty (thieu = 1); ve 0 thi don ô (TemplateId=-1).
        /// Giu BagItems dong bo de auto HP/MP khong spam dung vao ô da het.
        /// </summary>
        public void HandleItemQtyDecrease(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                int qty = 1;
                try { qty = r.ReadShort(); } catch { }

                var bag = State.MyChar != null ? State.MyChar.BagItems : null;
                if (bag == null || slot >= bag.Length || bag[slot] == null) return;
                if (bag[slot].Quantity > qty)
                    bag[slot].Quantity = (ushort)(bag[slot].Quantity - qty);
                else
                    bag[slot] = new Item(); // TemplateId mac dinh -1 = ô trong
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 8: THEM item vao 1 o tui (clone MODGAME Controller case 8). Quan trong: khi mua/nhat
        /// item, server gui cmd 8 -> phai cap nhat BagItems thi auto an/dung/loc moi thay item moi.
        /// Format: byte slot, short templateId, bool isLock, [byte upgrade neu trang bi/ngoc kham],
        /// bool isExpires, [unsigned short quantity, thieu = 1].
        /// </summary>
        public void HandleBagItemAdd(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                var mc = State.MyChar;
                if (mc == null || !mc.EnsureBagSlot(slot)) return;
                var bag = mc.BagItems;

                var it = new Item();
                it.TemplateId = r.ReadShort();
                it.IsLock = r.ReadBoolean();

                var tpl = State.ItemStore != null ? State.ItemStore.Get(it.TemplateId) : null;
                if (tpl != null && (tpl.IsTypeBody || tpl.IsTypeNgocKham))
                    it.Upgrade = r.ReadByte();

                it.IsExpires = r.ReadBoolean();
                try { it.Quantity = (ushort)r.ReadUnsignedShort(); }
                catch { it.Quantity = 1; }
                if (it.Quantity == 0) it.Quantity = 1;

                bag[slot] = it;
                // THONG BAO NHAN - clone Controller.java:919:
                //   InfoMe.addInfo(mResources.ku + " [" + template.id + "] " + template.name)
                // Ngoai le ban goc: type 20 im lang (Controller.java:918).
                if (State.Notices.Enabled && (tpl == null || tpl.Type != 20))
                    State.Notices.Push("Bạn nhận được " + TenMon(tpl, it.TemplateId, it.Quantity));
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 42: chi tiet 1 item (tra loi REQUEST_ITEM_INFO). Format verify tu Service/Controller 251:
        ///   byte typeUI, ubyte indexUI, long expires, int saleCoinLock (vi tui/trang bi la cua minh),
        ///   NEU trang bi/thu/ngoc kham: byte sys + loop {ubyte optionId, int param} cho toi het goi.
        /// Ghi vao dung Item trong BagItems/BodyItems roi bao UI cap nhat (detail panel).
        /// </summary>
        public void HandleItemInfo(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int typeUI = r.ReadByte() & 0xFF;
                int index = r.ReadUnsignedByte();

                Item it = null;
                var c = State.MyChar;
                if (c != null)
                {
                    if (typeUI == ItemServiceTypeBag && c.BagItems != null && index < c.BagItems.Length) it = c.BagItems[index];
                    else if (typeUI == ItemServiceTypeBody && c.BodyItems != null && index < c.BodyItems.Length) it = c.BodyItems[index];
                }

                // ===== NHANH SHOP (them cho Auto Danh Vong) =====
                // Cau truc cmd 42 re nhanh theo typeUI (DANH_VONG.md §J.1):
                //   long expires
                //   typeUI thuoc {3,4,5,39} (do CUA MINH): int saleCoinLock   <- nhanh CU o duoi
                //   nguoc lai (bang SHOP):                 int xu, int yen, int luong
                // Nhanh cu doc saleCoinLock VO DIEU KIEN - dung cho tui/nguoi (thu duy nhat truoc
                // day ta hoi), nhung SAI khi hoi mon trong shop. Chi THEM nhanh moi, KHONG dung
                // vao duong dap do dang chay.
                if (it == null && typeUI != ItemServiceTypeBag && typeUI != ItemServiceTypeBody
                    && typeUI != 4 && typeUI != 39)
                {
                    var si = State.Shops.FindByIndex(typeUI, index);
                    if (si == null) return;
                    r.ReadLong();                     // expires
                    // ⚠️ TEN THAT theo NinjaSchool_251_src. MODGAME dat ten 3 field nay LECH 1 SLOT
                    // vi bi obfuscate; chep ten cua ho sang day la tinh nham loai tien (§J.1).
                    si.BuyCoin = r.ReadInt();         // slot #1 = XU
                    si.BuyCoinLock = r.ReadInt();     // slot #2 = YEN
                    si.BuyGold = r.ReadInt();         // slot #3 = LUONG
                    si.PriceLoaded = true;
                    return;
                }

                if (it == null || it.IsEmpty) return;

                it.Expires = r.ReadLong();
                // LOC DO doc truong nay (docs/features/LOC_DO.md §C). Truoc 2026-09-12 o day la
                // `r.ReadInt(); // saleCoinLock - bo qua` - doc dung so byte nhung nem gia tri di.
                it.SaleCoinLock = r.ReadInt();

                var tpl = State.ItemStore != null ? State.ItemStore.Get(it.TemplateId) : null;
                if (tpl != null && (tpl.IsTypeBody || tpl.IsTypeMount || tpl.IsTypeNgocKham))
                {
                    it.Sys = r.ReadByte();
                    var opts = new System.Collections.Generic.List<ItemOption>();
                    try
                    {
                        while (true)
                        {
                            int optId = r.ReadUnsignedByte();
                            int param = r.ReadInt();
                            opts.Add(new ItemOption(optId, param));
                        }
                    }
                    catch { } // het goi -> ket thuc (giong game: readInt EOF -> thoat loop)
                    it.Options = opts;
                }
                it.DetailLoaded = true;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        // typeUI quy uoc (giong ItemService.TYPE_BAG/TYPE_BODY) - giu cuc bo de khong phu thuoc Service.
        private const int ItemServiceTypeBag = 3;
        private const int ItemServiceTypeBody = 5;

        /// <summary>
        /// Cmd 9: CONG so luong vao 1 o tui da co (clone game 251 Controller case 9):
        ///   byte slot, [short amount, thieu/EOF = 1]  ->  BagItems[slot].Quantity += amount.
        /// Day la duong server NAY bao "nhat duoc item xep chong (binh HP/MP...) da co san trong tui".
        /// Truoc day cmd 9 bi coi la BODY_UPDATE va bo qua (default) -> nhat binh xong tui KHONG doi.
        /// </summary>
        public void HandleBagAddQty(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                int amount = 1;
                try { amount = r.ReadShort(); } catch { amount = 1; } // thieu = 1 (giong game)
                if (amount <= 0) amount = 1;

                var mc = State.MyChar;
                if (mc == null || !mc.EnsureBagSlot(slot) || mc.BagItems[slot] == null || mc.BagItems[slot].IsEmpty)
                    return;
                var it = mc.BagItems[slot];
                it.Quantity = (ushort)(it.Quantity + amount);
                // THONG BAO NHAN - clone Controller.java:966 (cung chuoi voi cmd 8).
                // Ban goc in so luong TRONG O sau khi cong; ta in SO VUA NHAN (amount) - dung cai
                // nguoi xem dang hoi ("vua duoc may cai"), va la khac biet duy nhat o dong nay.
                if (State.Notices.Enabled)
                {
                    var tplQ = State.ItemStore != null ? State.ItemStore.Get(it.TemplateId) : null;
                    State.Notices.Push("Bạn nhận được " + TenMon(tplQ, it.TemplateId, amount));
                }
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Chuỗi tên món cho dòng thông báo — khuôn <c>"[id] tên"</c> của bản gốc
        /// (<c>Controller.java:919</c>), thêm <c>" xN"</c> khi nhận nhiều hơn 1.
        ///
        /// Thiếu bảng item (chưa nạp xong DataSync) thì in <c>"[id]"</c> trơn: mất tên còn hơn
        /// mất cả dòng — người xem vẫn tra được id.
        /// </summary>
        private static string TenMon(Models.ItemTemplate tpl, int templateId, int qty)
        {
            string t = "[" + templateId + "]";
            if (tpl != null && !string.IsNullOrEmpty(tpl.Name)) t += " " + tpl.Name;
            if (qty > 1) t += " x" + qty;
            return t;
        }

        /// <summary>Cmd 7: set so luong 1 o tui (clone MODGAME case 7: byte slot + short qty).</summary>
        public void HandleBagSetQty(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                short qty = r.ReadShort();
                var mc = State.MyChar;
                if (mc == null || !mc.EnsureBagSlot(slot) || mc.BagItems[slot] == null) return;
                var bag = mc.BagItems;
                bag[slot].Quantity = (ushort)(qty < 0 ? 0 : qty);
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 19/20: ket qua LUYEN DA (clone MODGAME Char.crystalCollect, Char.java:7743).
        /// Format: byte status (1 = xong; khac = buoc trung gian con luyen tiep) + byte indexUI
        /// (o tui cua da ket qua) + short templateId (da cap moi) + bool isLock + bool isExpires
        /// + (cmd 19: int xu | cmd 20: int yen [+ int xu, co the thieu]).
        /// Ca 2 nhanh status deu dat da ket qua vao tui (auto MODGAME NSOT_MOB:758-759 lam vay
        /// cho ca nhanh trung gian). Tang LuyenDaSeq cho TrainMode.TryLuyenDa biet da co phan hoi.
        /// </summary>
        public void HandleLuyenDaResult(NsoMessage msg, bool xuOnly)
        {
            try
            {
                var r = msg.Reader;
                r.ReadByte(); // status - 2 nhanh xu ly nhu nhau (xem summary)
                int slot = r.ReadByte() & 0xFF;
                var it = new Item();
                it.TemplateId = r.ReadShort();
                it.IsLock = r.ReadBoolean();
                it.IsExpires = r.ReadBoolean();
                it.Quantity = 1;

                var c = State.MyChar;
                if (c != null)
                {
                    if (xuOnly)
                        c.Xu = r.ReadInt();
                    else
                    {
                        c.Yen = r.ReadInt();
                        try { c.Xu = r.ReadInt(); } catch { } // xu optional (MODGAME cung try/catch)
                    }
                    var bag = c.BagItems;
                    if (bag != null && slot < bag.Length)
                        bag[slot] = it;
                }
                State.LuyenDaSeq++;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 21: ket qua DAP DO (nang cap trang bi). Wire xac nhan boi 2 nguon doc lap
        /// (NinjaSchool_251_src Controller.cs:933 + MODGAME Controller.java:1091):
        ///     byte result | int luong | int xu | int yen | byte upgradeMoi
        /// result: 1 = LEN cap | 5/6 = kham ngoc thanh/bai | con lai (0/2...) = XIT.
        ///
        /// !! Client goc chi doc byte thu 5 KHI dang mo man nang cap (GameScr.itemUpGrade != null),
        ///    nhung SERVER LUON GUI. Bot headless khong co man hinh -> phai LUON doc, neu khong
        ///    se khong biet cap moi. Doc trong try rieng vi la byte cuoi goi.
        ///
        /// Server KHONG tra mon/da ve tui - engine dap do phai tu tra (xem DAP_DO_MODGAME.md D.8).
        /// </summary>
        public void HandleUpgradeResult(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int result = r.ReadByte();

                var c = State.MyChar;
                int luong = r.ReadInt();
                int xu = r.ReadInt();
                int yen = r.ReadInt();
                if (c != null)
                {
                    c.Luong = luong;
                    c.Xu = xu;
                    c.Yen = yen;
                }

                int newLevel = -1;
                try { newLevel = r.ReadByte() & 0xFF; } catch { }

                State.UpgradeResult = result;
                State.UpgradeNewLevel = newLevel;
                State.UpgradeSeq++;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 31: DANH SACH RUONG (tra loi sub -103 requestItem(4)). Wire clone MODGAME
        /// Controller.java:1230-1250:
        ///     int xuInBox | ubyte soO | soO x { short templateId,
        ///       neu templateId != -1: bool isLock, [byte upgrade neu trang bi/ngoc kham],
        ///                             bool isExpires, short quantity }
        /// O trong ghi bang Item rong (TemplateId -1) giong tui, KHONG phai null - de FreeBoxSlots
        /// va vong quet dung chung mot quy uoc voi BagItems.
        /// </summary>
        public void HandleBoxItemList(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                var c = State.MyChar;
                if (c == null) return;

                c.XuInBox = r.ReadInt();
                int n = r.ReadUnsignedByte();
                var box = new Item[n];
                for (int i = 0; i < n; i++)
                {
                    var it = new Item();
                    short tid = r.ReadShort();
                    if (tid != -1)
                    {
                        it.TemplateId = tid;
                        it.IsLock = r.ReadBoolean();
                        var tpl = State.ItemStore != null ? State.ItemStore.Get(tid) : null;
                        if (tpl != null && (tpl.IsTypeBody || tpl.IsTypeNgocKham))
                            it.Upgrade = r.ReadByte();
                        it.IsExpires = r.ReadBoolean();
                        it.Quantity = (ushort)r.ReadShort();
                        if (it.Quantity == 0) it.Quantity = 1;
                    }
                    box[i] = it;
                }
                c.BoxItems = box;
                State.BoxSeq++;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 17: ket qua CAT vao ruong (clone Char.itemBagToBox, Char.java:7810).
        /// Wire = ubyte bagIdx + ubyte boxIdx - server KHONG gui lai template/quantity, client tu
        /// chuyen object. O ruong da co san mon cung loai thi CONG don quantity.
        /// </summary>
        public void HandleItemBagToBox(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int bagIdx = r.ReadUnsignedByte();
                int boxIdx = r.ReadUnsignedByte();
                var c = State.MyChar;
                if (c == null) return;
                var bag = c.BagItems;
                var box = c.BoxItems;
                if (bag == null || bagIdx >= bag.Length) return;
                var it = bag[bagIdx];
                if (it == null || it.IsEmpty) return;
                bag[bagIdx] = new Item();
                if (box != null && boxIdx < box.Length)
                {
                    if (box[boxIdx] == null || box[boxIdx].IsEmpty) box[boxIdx] = it;
                    else box[boxIdx].Quantity = (ushort)(box[boxIdx].Quantity + it.Quantity);
                }
                State.BoxSeq++;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 16: ket qua LAY tu ruong ve tui (clone Char.itemBoxToBag, Char.java:7844) - doi xung
        /// cmd 17: ubyte boxIdx + ubyte bagIdx.
        /// </summary>
        public void HandleItemBoxToBag(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int boxIdx = r.ReadUnsignedByte();
                int bagIdx = r.ReadUnsignedByte();
                var c = State.MyChar;
                if (c == null) return;
                var box = c.BoxItems;
                if (box == null || boxIdx >= box.Length) return;
                var it = box[boxIdx];
                if (it == null || it.IsEmpty) return;
                box[boxIdx] = new Item();
                if (c.EnsureBagSlot(bagIdx))
                {
                    var bag = c.BagItems;
                    if (bag[bagIdx] == null || bag[bagIdx].IsEmpty) bag[bagIdx] = it;
                    else bag[bagIdx].Quantity = (ushort)(bag[bagIdx].Quantity + it.Quantity);
                }
                State.BoxSeq++;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// Cmd 10: item bien khoi tui (verify MODGAME Controller case 10: arrItemBag[slot]=null).
        /// </summary>
        public void HandleBagItemRemove(NsoMessage msg)
        {
            try
            {
                int slot = msg.Reader.ReadByte() & 0xFF;
                var bag = State.MyChar != null ? State.MyChar.BagItems : null;
                if (bag == null || slot >= bag.Length) return;
                bag[slot] = new Item(); // TemplateId mac dinh -1 = ô trong
                State.RaiseInventoryChanged();
            }
            catch { }
        }
    }
}
