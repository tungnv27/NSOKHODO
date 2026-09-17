using System;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    public class CombatHandler
    {
        public GameStateManager State { get; private set; }

        public CombatHandler(GameStateManager state)
        {
            State = state;
        }

        /// <summary>
        /// charId co phai TRUONG NHOM khong. Truong = <c>PartyMembers[0]</c> - cung dinh nghia voi
        /// <c>GroupPlayController</c> (:72) va V9_X1 (<c>ba_0.J.firstElement()</c>, <c>f.java:2222</c>).
        /// KHONG cache: truong doi khi truong cu roi nhom (da tung gay ping-pong, xem
        /// TrainMode.cs:2338-2341) -> tra lai moi lan goi.
        /// Chinh minh la truong -> tra false (khong tu bam chinh minh).
        /// </summary>
        private bool IsPartyLeader(int charId)
        {
            try
            {
                var party = State.PartyMembers;
                if (party == null || party.Count == 0) return false;
                var head = party[0];
                if (head == null || head.CharId != charId) return false;
                var mc = State.MyChar;
                return mc == null || head.CharId != mc.CharId;
            }
            catch { return false; }
        }

        /// <summary>
        /// cmd=-1: ket qua minh danh quai. Format MODGAME Controller case -1:
        /// mobId(unsignedByte), hp(int), damage(int), crit(bool), levelBoss(byte), maxHp(int) -> Auto.a().
        /// QUAN TRONG: levelBoss (1=Tinh Anh, 2=Thu Linh, 3=Ta Thu) chi DUOC TIET LO o day (va cmd -5),
        /// KHONG co trong MAP_INFO. Bug cu dung sau crit -> mob.LevelBoss mai = 0 -> San TATL khong thay gi.
        /// </summary>
        public void HandleMyAttackResult(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                byte mobId = r.ReadByte();
                int mobHp = r.ReadInt();
                int damage = r.ReadInt();
                bool isCritical = r.ReadBoolean();

                // levelBoss + maxHp o cuoi (try rieng: 1 so packet bien the co the thieu).
                byte levelBoss = 255; int maxHp = -1;
                try { levelBoss = r.ReadByte(); maxHp = r.ReadInt(); } catch { }

                State.NoteAttackAck();   // dd16: server CON dap lai don danh cua ta

                var mobs = State.CurrentMap.Mobs;
                if (mobId >= 0 && mobId < mobs.Length && mobs[mobId] != null)
                {
                    mobs[mobId].Hp = mobHp;
                    if (levelBoss != 255) mobs[mobId].LevelBoss = levelBoss;
                    if (maxHp > 0) mobs[mobId].MaxHp = maxHp;
                    if (mobHp <= 0)
                        mobs[mobId].IsDead = true;

                    // MODGAME Controller case -1 (dong 648-650): sau khi gan levelBoss(=0 server gui)
                    // + maxHp THAT (10x/100x voi elite), goi Auto.a(mob) PHAN LOAI LAI tu maxHp.
                    // THIEU buoc nay: cmd -1 ghi de LevelBoss=0 (mat Tinh Anh/Thu Linh da gan o MAP_INFO)
                    // -> bot danh elite 1 phat roi tick sau coi la quai thuong -> bo San TATL.
                    MapHandler.ClassifyElite(mobs[mobId], State.MobStore);

                    PushDame(mobs[mobId], damage, isCritical);
                }
            }
            catch { }
        }

        /// <summary>
        /// Đẩy một số sát thương lên bảng "số bay" của cửa sổ Xem game.
        /// Clone <c>GameScr.a("-"+dmg, mob.x, mob.y-mob.h, crit ? 3 : 5)</c>
        /// (MODGAME Controller.java:677-681 · NSOPC Controller.cs:2803-2846).
        ///
        /// ⚠️ BẪY: <c>dmg &lt; 0</c> ở cmd −1/−4 nghĩa là **sát thương lớn tràn số**, phải
        /// <c>abs(dmg) + 32767</c> (Controller.java:664-666). Ở **cmd 62** thì <c>dmg &lt; 0</c>
        /// lại có nghĩa **CHÍ MẠNG** — hai quy ước NGƯỢC nhau trên hai gói khác nhau. Tuyệt đối
        /// không gộp chung một hàm chuẩn hoá cho cả hai.
        ///
        /// Không tốn gì khi cửa sổ đóng: <see cref="FlyTextBoard.Enabled"/> tắt thì thoát ngay,
        /// KHÔNG dựng chuỗi (yêu cầu user: tắt Xem game là tool nhẹ như cũ).
        /// </summary>
        /// <summary>
        /// Chữ bay <c>"+N"</c> trên đầu NHÂN VẬT MÌNH khi nhận yên / kinh nghiệm — clone
        /// <c>GameScr.a("+" + n, myChar.cx, myChar.cy - myChar.ch, type)</c>
        /// (MODGAME <c>Controller.java:505-506</c> cmd -8 · <c>:860</c> cmd 5).
        ///
        /// Dùng CHUNG bảng 5 slot với số sát thương, đúng như bản gốc (client cũng chỉ có một bộ
        /// <c>lc/ld/le/lf/lg/lh/li</c> cho mọi loại). Nghĩa là khi đang tàn sát, số dame có thể
        /// chiếm hết chỗ và chữ "+yên" bị bỏ — <b>đó là hành vi của game, không phải lỗi</b>.
        ///
        /// Cổng: <see cref="Models.NoticeBoard.Enabled"/> chứ không phải cổng số sát thương —
        /// hai thứ này thuộc ô tích "Bật thông báo nhận", tắt ô đó là không dựng chuỗi nào.
        /// Số ≤ 0 thì bỏ qua: bản gốc in cả số âm, nhưng dòng "+0" mỗi lần server đồng bộ lại
        /// tài nguyên thì chỉ là rác.
        /// </summary>
        private void PushNhan(long delta, byte kind)
        {
            if (delta <= 0 || !State.Notices.Enabled || !State.FlyTexts.Enabled) return;
            var c = State.MyChar;
            if (c == null) return;
            State.FlyTexts.Push("+" + delta, c.Cx, c.Cy, FlyText.TPL_MYCHAR, kind);
        }

        private void PushDame(MobState mob, int damage, bool crit)
        {
            if (mob == null || !State.FlyTexts.Enabled) return;
            if (damage < 0) damage = Math.Abs(damage) + 32767;
            State.FlyTexts.Push("-" + damage, mob.X, mob.Y, mob.TemplateId,
                                crit ? FlyText.KIND_CRIT : FlyText.KIND_NORMAL);
        }

        /// <summary>
        /// cmd=51: quái NÉ đòn của mình. Wire <c>mobId(unsignedByte), hp(int)</c> — len = 5.
        /// Clone MODGAME Controller.java:1484-1497 (<c>mob.hp = readInt(); GameScr.a("", …, 4)</c>),
        /// khớp NSOPC Controller.cs:2848-2863.
        ///
        /// ⚠️ CHƯA XÁC MINH BẰNG HEX trên server đích — bot chưa từng định tuyến cmd này, và
        /// <c>SERVER_FACTS.md §2</c> không có dòng nào cho 51. Handler viết theo hai nguồn client;
        /// nếu server không gửi thì đơn giản là không bao giờ chạy (không gây hại).
        ///
        /// Chữ "Né": MODGAME vẽ **ảnh** <c>SmallImage 1062</c>, bản 251 vẽ **chữ** — ta theo 251 vì
        /// ảnh 1062 nằm ngoài kho sprite đóng kèm.
        /// </summary>
        public void HandleMobDodge(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                byte mobId = r.ReadByte();
                int mobHp = r.ReadInt();

                var mobs = State.CurrentMap.Mobs;
                if (mobId >= 0 && mobId < mobs.Length && mobs[mobId] != null)
                {
                    mobs[mobId].Hp = mobHp;
                    if (State.FlyTexts.Enabled)
                        State.FlyTexts.Push("Né", mobs[mobId].X, mobs[mobId].Y,
                                            mobs[mobId].TemplateId, FlyText.KIND_DODGE);
                }
            }
            catch { }
        }

        /// <summary>
        /// cmd=-3: QUAI DANH MINH. Wire (Controller.cs:2928-2960 ban 2.5.1):
        /// <c>mobId(unsignedByte), dame(int), dameMp(int - co the thieu)</c>.
        ///
        /// ===== 2026-09-07 (dd3): TRUOC DAY LA HAM RONG, VA DO LA GOC RE =====
        /// Chu thich cu: <i>"Don't manually subtract HP/MP - server sends real values via HP_UPDATE"</i>.
        /// GIA DINH DO SAI TREN SERVER NAY, da do bang log (22:08-22:13, 15 acc):
        ///   - <c>sub HP_UPDATE (-122)</c>: **0 goi**   - <c>cmd HP_CHANGE (62)</c>: **0 goi**
        ///   - Nguon HP that DUY NHAT la <c>sub 115 UPDATE_INFO_ME</c>, va no ve rat thua:
        ///     **p50 = 20s, p90 = 30s, xa nhat 161s**.
        /// Tuc giua hai lan do, nhan vat KHONG HE BIET minh con bao nhieu mau - trong khi
        /// NsoClient da bia <c>Hp = MaxHp</c> luc hoi sinh. Do dung la hien tuong user bao:
        /// *"nhan vat het HP nhung mo Xem game van day HP"*.
        ///
        /// Client goc KHONG cho HP_UPDATE: no TU TRU MAU tai cho moi lan trung don
        /// (<c>Char.doInjure</c>, Char.cs:12282-12297) roi de server chinh lai dinh ky. Ta clone lai.
        ///
        /// Khac ban goc MOT diem, co chu dich: ban goc chi tru ngay khi <c>mob.isBusyAttackSomeOne</c>,
        /// con lai thi cat vao <c>mob.dame</c> de tru theo HOAT CANH danh. Ta headless, khong co hoat
        /// canh, nen tru ngay o ca hai nhanh.
        /// </summary>
        public void HandleMobAttackMe(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                r.ReadByte();                 // mobId - khong dung
                int dame = r.ReadInt();
                int dameMp = 0;
                try { dameMp = r.ReadInt(); } catch { }   // ban goc cung boc try rieng: goi co the thieu

                // dame <= 0 = NE don (ban goc ve chu "MISS"), khong tru gi.
                if (dame <= 0 && dameMp <= 0) return;

                var c = State.MyChar;
                if (c == null) return;

                if (dame > 0) c.Hp -= dame;
                if (dameMp > 0) c.Mp -= dameMp;
                if (c.Hp < 0) c.Hp = 0;
                if (c.Mp < 0) c.Mp = 0;

                // Clone Char.cs:12294-12297: chua nhan goi chet thi GIU o 1, khong tu tuyen bo chet.
                // Quyet dinh "da chet" van chi den tu cmd -11 / cmd 72 - dung nhu ban goc.
                if (c.Hp < 1 && !c.IsDead) c.Hp = 1;

                State.NoteLocalHp(dame);
            }
            catch { }
        }

        public void HandleMobAttackOther(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                byte mobId = r.ReadByte();
                int charId = r.ReadInt();
                int newHp = r.ReadInt();

                foreach (var p in State.CurrentMap.OtherPlayers)
                {
                    if (p.CharId == charId)
                    {
                        p.Hp = newHp;
                        break;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// cmd=-4: mob chết. Biến thể đã thấy hex: len=6 (mobId, dmg int, crit byte), len=1 (xóa xác).
        /// ĐÍNH CHÍNH 2026-07-06 (MODGAME Controller case -4, :523-555): sau crit còn ĐUÔI ITEM
        /// TÙY CHỌN {itemMapId short, templateId short, xEnd short, yEnd short} - không có cờ
        /// "có drop", client gốc thử đọc và nuốt EOF nếu quái không rơi. len=6 = biến thể không
        /// rơi (quái xám). Thiếu đuôi này = gốc rễ "nhặt đồ không hoạt động" (NHAT_ITEM.md §A).
        /// </summary>
        public void HandleMobDie(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                byte mobId = r.ReadByte();

                State.NoteAttackAck();   // dd16: don ket lieu cung la mot tin dap lai

                var mobs = State.CurrentMap.Mobs;
                if (mobId >= 0 && mobId < mobs.Length && mobs[mobId] != null)
                {
                    mobs[mobId].IsDead = true;
                    mobs[mobId].Hp = 0;
                }

                // damage + crit (don ket lieu) roi duoi item tuy chon - doc kieu try-EOF nhu client goc
                try
                {
                    int dmg = r.ReadInt();
                    bool crit = r.ReadBoolean();
                    if (mobId >= 0 && mobId < mobs.Length) PushDame(mobs[mobId], dmg, crit);
                    ReadItemDropTail(r);
                }
                catch { }
            }
            catch { }
        }

        /// <summary>
        /// cmd=78: quái chết biến thể event (MODGAME Controller case 78) - KHÔNG có dmg/crit,
        /// LUÔN kèm item: mobId ubyte, itemMapId short, templateId short, xEnd short, yEnd short.
        /// </summary>
        public void HandleMobDieDrop(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                byte mobId = r.ReadByte();

                var mobs = State.CurrentMap.Mobs;
                if (mobId >= 0 && mobId < mobs.Length && mobs[mobId] != null)
                {
                    mobs[mobId].IsDead = true;
                    mobs[mobId].Hp = 0;
                    mobs[mobId].Status = 0;   // MODGAME: var27.status = 0
                }

                ReadItemDropTail(r);
            }
            catch { }
        }

        // Doc {itemMapId, templateId, xEnd, yEnd} (4 short) roi them vao ItemsOnMap. Dung
        // xEnd/yEnd (vi tri settle tren nen) lam toa do nhat - x/y goc chi de client ve animation.
        private void ReadItemDropTail(Core.BigEndianBinaryReader r)
        {
            var item = new Models.ItemOnMap();
            item.ItemMapId = r.ReadShort();
            item.TemplateId = r.ReadShort();
            item.X = r.ReadShort();
            item.Y = r.ReadShort();
            State.CurrentMap.AddItemOnMap(item);
        }

        /// <summary>
        /// cmd=-5: mob hồi sinh. Format MODGAME Controller case -5:
        /// mobId(unsignedByte), sys(byte), levelBoss(byte), [x=xFirst,y=yFirst, status=5], hp(int), maxHp=hp.
        /// KHÔNG có x/y trong packet - quái sống lại tại chỗ spawn cũ. Bug cu BO QUA byte levelBoss
        /// -> doc hp lech + khong gan elite -> quai respawn voi tu cach Tinh Anh/Thu Linh khong duoc nhan ra.
        /// </summary>
        /// <summary>
        /// cmd=60 (CHIEU NHAN): NGUOI CHOI KHAC dung chieu len QUAI. HAI nguoi dung:
        ///  (1) cua so "Xem game" ve lai hoat canh (thuan trang tri);
        ///  (2) "Bam nhom truong" doc xem TRUONG dang danh con nao (co doi hanh vi - xem duoi).
        ///
        /// Wire (MODGAME Controller.java:1589-1646, khop NSOPC Controller.cs:2629-2705):
        ///   int charId ; byte skillTemplateId ; ubyte mobId x N   (doc toi HET goi, toi da 10)
        /// Do that tren server dich: len=6/7/8 -> N = 1..3. SERVER_FACTS.md §2.
        ///
        /// Huong nhin lay theo muc tieu DAU TIEN: cdir = (char.cx &lt;= mob.x) ? 1 : -1
        /// (Controller.java:1626-1630).
        ///
        /// KHONG TON GI khi cua so dong VA khong bam truong: thoat ngay o phep so bool dau ham.
        ///
        /// ⚠ 2026-09-07 - gop them MOT NGUOI DUNG thu hai: tinh nang "Bam nhom truong"
        /// (features/BAM_NHOM_TRUONG.md). Goi nay la NGUON DUY NHAT cho biet TRUONG dang danh con
        /// quai nao; V9_X1 doc thang bien `roster[0].p.a` cua client no (`f.java:1587`), ta headless
        /// nen chi co cmd 60. Vi vay cong `SkillFx.Enabled` KHONG con duoc dat dau ham - no chi noi
        /// "cua so Xem game dang dong", khong lien quan gi den viec bam truong.
        /// </summary>
        public void HandleOtherCastMob(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int charId = r.ReadInt();
                int skillTpl = r.ReadByte();

                // Hai nguoi dung doc lap. Khong ai can -> thoat ngay (van re nhu truoc: 1 phep so
                // bool + 1 phep so int voi phan tu dau roster).
                bool wantFx = State.SkillFx.Enabled;
                bool ofLeader = IsPartyLeader(charId);
                if (!wantFx && !ofLeader) return;

                var mobs = State.CurrentMap.Mobs;
                var ids = new byte[SkillFxCast.MAX_TARGET];
                var tx = new short[SkillFxCast.MAX_TARGET];
                var ty = new short[SkillFxCast.MAX_TARGET];
                int n = 0, nId = 0, firstX = 0;
                bool haveFirst = false;
                for (int i = 0; i < SkillFxCast.MAX_TARGET; i++)
                {
                    int id;
                    try { id = r.ReadByte() & 0xFF; } catch { break; }   // doc toi het goi, y client
                    ids[nId++] = (byte)id;
                    if (id < 0 || id >= mobs.Length || mobs[id] == null) continue;
                    tx[n] = (short)mobs[id].X;
                    ty[n] = (short)mobs[id].Y;
                    if (!haveFirst) { firstX = mobs[id].X; haveFirst = true; }
                    n++;
                }

                // Ghi ban ghi "truong dang danh" - MANG TRUOC, MOC THOI GIAN SAU. Ben doc (TrainMode,
                // thread khac) doc nguoc lai (moc truoc, mang sau) nen khong bao gio thay moc moi di
                // kem mang cu. Khong khoa: doc tre mot nhip chi lam cham/nham 1 con quai.
                if (ofLeader && nId > 0)
                {
                    var snap = new byte[nId];
                    Array.Copy(ids, snap, nId);
                    State.LeaderFocusMobIds = snap;
                    State.LeaderFocusAt = DateTime.UtcNow;
                }

                if (!wantFx || n == 0) return;

                int dir = 1;
                var others = State.CurrentMap.OtherPlayers;
                if (others != null && haveFirst)
                {
                    PlayerInfo[] arr = null;
                    try { arr = others.ToArray(); } catch { }
                    if (arr != null)
                        for (int k = 0; k < arr.Length; k++)
                            if (arr[k] != null && arr[k].CharId == charId)
                            { dir = arr[k].X <= firstX ? 1 : -1; break; }
                }

                State.SkillFx.Push(false, charId, skillTpl, 0, dir, tx, ty, null, n);
            }
            catch { }
        }

        /// <summary>
        /// cmd=61 (CHIEU NHAN): NGUOI CHOI KHAC dung chieu len NGUOI. Cung tinh chat trang tri.
        /// Wire: <c>int charId ; byte skillTemplateId ; int charId x N</c> (MODGAME
        /// Controller.java:1647-1706).
        ///
        /// CHUA THAY GOI NAY trong log server dich (chi thay 60) - viet theo hai nguon client;
        /// khong ve thi khong bao gio chay, vo hai.
        /// </summary>
        public void HandleOtherCastChar(NsoMessage msg)
        {
            if (!State.SkillFx.Enabled) return;
            try
            {
                var r = msg.Reader;
                int charId = r.ReadInt();
                int skillTpl = r.ReadByte();

                var ids = new int[SkillFxCast.MAX_TARGET];
                int n = 0;
                for (int i = 0; i < SkillFxCast.MAX_TARGET; i++)
                {
                    int id;
                    try { id = r.ReadInt(); } catch { break; }
                    ids[n++] = id;
                }
                if (n == 0) return;

                int dir = 1;
                var others = State.CurrentMap.OtherPlayers;
                if (others != null)
                {
                    PlayerInfo[] arr = null;
                    try { arr = others.ToArray(); } catch { }
                    if (arr != null)
                    {
                        int selfX = int.MinValue, tgtX = int.MinValue;
                        for (int k = 0; k < arr.Length; k++)
                        {
                            if (arr[k] == null) continue;
                            if (arr[k].CharId == charId) selfX = arr[k].X;
                            if (arr[k].CharId == ids[0]) tgtX = arr[k].X;
                        }
                        var mc = State.MyChar;
                        if (mc != null && ids[0] == mc.CharId) tgtX = mc.Cx;
                        if (selfX != int.MinValue && tgtX != int.MinValue) dir = selfX <= tgtX ? 1 : -1;
                    }
                }

                State.SkillFx.Push(false, charId, skillTpl, 0, dir, null, null, ids, n);
            }
            catch { }
        }

        public void HandleMobRespawn(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                byte mobId = r.ReadByte();
                byte sys = r.ReadByte();
                byte levelBoss = r.ReadByte();
                int hp = r.ReadInt();

                var mobs = State.CurrentMap.Mobs;
                if (mobId >= 0 && mobId < mobs.Length && mobs[mobId] != null)
                {
                    mobs[mobId].Hp = hp;
                    mobs[mobId].MaxHp = hp;        // MODGAME: maxHp = hp luc respawn
                    mobs[mobId].Sys = sys;
                    mobs[mobId].LevelBoss = levelBoss;
                    mobs[mobId].Status = 5;        // MODGAME set status = 5
                    mobs[mobId].IsDead = false;
                    mobs[mobId].IsActive = true;
                    // Respawn reset maxHp=hp + levelBoss=packet(0) -> phan loai lai Tinh Anh/Thu Linh
                    // theo maxHp (MODGAME case -5 goi Auto.a(var27) sau khi set). Khong co buoc nay thi
                    // elite hoi sinh thanh quai thuong -> "San TATL" mat dau con elite (đứng/đổi mục tiêu).
                    MapHandler.ClassifyElite(mobs[mobId], State.MobStore);
                }
            }
            catch { }
        }

        /// <summary>
        /// cmd=5: NHAN exp. Clone game Controller case 5 (NSOTool 251 line 2678-2696):
        ///   delta = readLong(); cExpDown = 0; cEXP += delta; recompute level.
        /// QUAN TRONG: packet gui DELTA (so exp vua nhan), KHONG phai tong tich luy -> phai CONG.
        /// Reset ExpDown=0 vi dang LEN exp (het am) -> EXP% hien duong giong game.
        /// Bug cu: gan Exp = delta (sai cumulative) + khong reset ExpDown -> ExpDown cu ket lai
        /// -> EXP% ra so am khong lo (-471069%).
        /// </summary>
        /// <summary>
        /// cmd=-8: nhan YEN truc tiep (clone game Controller case -8: yen += readInt()). Dung cho
        /// cac nguon yen khong qua nhat do (vd thuong su kien/nhiem vu). Cap nhat yen real-time.
        /// </summary>
        public void HandleYenGain(NsoMessage msg)
        {
            try
            {
                int delta = msg.Reader.ReadInt();
                State.MyChar.Yen += delta;
                PushNhan(delta, FlyText.KIND_YEN);
            }
            catch { }
        }

        public void HandleExpGain(NsoMessage msg)
        {
            try
            {
                long delta = msg.Reader.ReadLong();
                var c = State.MyChar;
                c.ExpDown = 0;
                c.Exp += delta;
                int lvl = GameData.ExpTable.GetLevelFromExp(c.Exp);
                if (lvl > 0) c.Level = (byte)lvl;

                // Chu bay "+N" XANH LA tren dau nhan vat - clone Controller.java:860
                //   GameScr.a("+" + exp, cx, cy - ch, 2)   // type 2 = number_green
                PushNhan(delta, FlyText.KIND_EXP);

                // Rieng EXP LON con them mot dong o thanh chay chu - clone Controller.java:861-863:
                //   if (exp >= 1000000) InfoMe.addInfo(ku + " " + exp + " Kinh nghiem", 20, vang)
                // Nguong 1 trieu la cua ban goc, khong phai tôi chon: nhan exp la chuyen xay ra
                // moi don danh, bao het thi thanh chay chu khong con cho cho thu gi khac.
                if (delta >= 1000000L && State.Notices.Enabled)
                    State.Notices.Push("Bạn nhận được " + delta + " Kinh nghiệm",
                                       Notice.KIND_YELLOW, Notice.SPEED_DEFAULT);
            }
            catch { }
        }

        /// <summary>
        /// cmd=71: dieu chinh exp-down (clone game case 71: cExpDown -= delta). Dung khi exp dang
        /// AM (vd luc PK am bi tru exp) - server gui delta de tang/giam phan thieu. Khong co packet
        /// nay thi ExpDown khong cap nhat khi PK -> PK am khong biet da mat bao nhieu -> khong quay
        /// lai train. Doc 1 long, tru vao ExpDown (giu nguyen dau am cua game).
        /// </summary>
        public void HandleExpDownAdjust(NsoMessage msg)
        {
            try
            {
                long delta = msg.Reader.ReadLong();
                State.MyChar.ExpDown -= delta;
            }
            catch { }
        }

        /// <summary>
        /// cmd=72: MAT exp khi PK am (clone game Controller case 72, NSOTool/MODGAME line 2273-2279):
        ///   cPk = readByte(); waitToDie(short wdx, short wdy); cEXP = getMaxExp(clevel-1); cExpDown = readLong().
        /// Day la goi DUY NHAT cap nhat cPk (diem hieu chien) + cExpDown trong khi PK am. THIEU NO thi:
        ///   - cPk khong doi -> PK am kep o khu xa diem / khu danh nguoi sai -> dung im "khong danh nguoi".
        ///   - cExpDown khong tang -> dieu kien dung "mat 15%" khong bao gio dat -> PK am khong biet da mat exp,
        ///     truoc day phai dua vao timer ep ket thuc -> "doi 1 luc roi ve train" ma CHUA mat exp (nguy co len lv).
        /// Server keo cEXP ve DAU CAP hien tai (getMaxExp(clevel-1)) roi gui cExpDown = so exp dang thieu (am).
        /// Wire: byte cPk | short wdx | short wdy | long cExpDown. cEXP tinh client-side (khong doc tu goi).
        /// </summary>
        public void HandlePkExpLoss(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                var c = State.MyChar;
                c.Pk = r.ReadByte();
                State.DeathX = r.ReadShort(); // wdx (waitToDie -> toa do hoi sinh)
                State.DeathY = r.ReadShort(); // wdy
                long down = r.ReadLong();
                c.Exp = GameData.ExpTable.GetTotalExpForLevelStart(c.Level); // = getMaxExp(clevel-1)
                c.ExpDown = down;

                // cmd 72 = CHET trong PK am (waitToDie). Server nay KHONG gui cmd -11 cho cai chet PK ->
                // truoc day char HP am ma KHONG hoi sinh/remap (loi user bao "het HP phai quay ve").
                // Danh dau chet de Router ban OnDeath -> returnTown -> MAP_INFO -> hoi sinh + remap nhu cmd -11.
                c.IsDead = true;
                c.Hp = 0;
                State.NoteServerHp(0, 0, "cmd 72 waitToDie");   // server noi thang: HP = 0

                // CHAN DOAN 2026-09-07 (dd): phan biet HAI loai chet. Log cu chi co mot dong
                // "[Death] Died" chung cho ca hai nen KHONG THE biet cai chet nao sinh ra dong bang.
                // Nghi can: cmd 72 la "waitToDie" - ban goc coi day la mot trang thai CHO co dong ho
                // (`readLong`), khac han cmd -11. Neu doi chieu log thay cac ca dong bang deu di sau
                // cmd 72 thi do chinh la cau tra loi. Xem docs/features/DONG_BANG_VI_TRI.md.
                State.RaiseDebugLog(string.Format("[Death] <- cmd 72 waitToDie (pk={0} wd={1},{2} down={3})",
                    c.Pk, State.DeathX, State.DeathY, down));
            }
            catch { }
        }

        /// <summary>
        /// cmd=-117 / -81 dang TOP-LEVEL: cap nhat KARMA (cPk). LUU Y: tren server (theo MODGAME) -117/-81
        /// la SUB-COMMAND cua cmd -30 (xem SubCommandHandler.HandlePkPoint) - duong top-level nay nhieu
        /// kha nang KHONG BAO GIO khop. Giu lai lam FALLBACK + de test server that lo duong nao thuc su
        /// ban (neu duong sub chay -> karma doi luc PK; neu chi top-level chay -> day moi la that). Doc 1 byte.
        /// </summary>
        public void HandlePkPointUpdate(NsoMessage msg)
        {
            try { State.MyChar.Pk = msg.Reader.ReadByte(); }
            catch { }
        }

        public void HandlePlayerHpChange(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int charId = r.ReadInt();
                int hp = r.ReadInt();
                int damage = r.ReadInt();

                if (charId == State.MyChar.CharId)
                {
                    State.MyChar.Hp = hp;
                    State.NoteServerHp(hp, 0, "cmd HP_CHANGE");   // nguon HP THAT
                }
                else
                {
                    foreach (var p in State.CurrentMap.OtherPlayers)
                    {
                        if (p.CharId == charId)
                        {
                            p.Hp = hp;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// cmd=-11: My character died
        /// From source: readByte(pk), readShort(wdx), readShort(wdy), [readLong(exp)]
        /// wdx,wdy = town respawn coordinates
        /// </summary>
        public void HandleMyDeath(NsoMessage msg)
        {
            var c = State.MyChar;
            c.IsDead = true;
            c.Hp = 0;
            State.NoteServerHp(0, 0, "cmd -11 chet");   // server noi thang: HP = 0
            try
            {
                var r = msg.Reader;
                c.Pk = r.ReadByte();
                // wdx, wdy = respawn position (sent by server)
                State.DeathX = r.ReadShort();
                State.DeathY = r.ReadShort();
                // CHAN DOAN 2026-09-07 (dd) - xem chu thich cung ten o HandlePkExpLoss (cmd 72).
                State.RaiseDebugLog(string.Format("[Death] <- cmd -11 (pk={0} wd={1},{2})",
                    c.Pk, State.DeathX, State.DeathY));
            }
            catch { }
            try { c.Exp = msg.Reader.ReadLong(); } catch { }
        }
    }
}

