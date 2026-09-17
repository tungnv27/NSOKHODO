using System;
using System.Collections.Generic;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    public class SubCommandHandler
    {
        public GameStateManager State { get; private set; }

        public SubCommandHandler(GameStateManager state)
        {
            State = state;
        }

        public void Handle(sbyte sub, NsoMessage msg)
        {
            switch (sub)
            {
                case SubCmd.Sub.MY_CHAR_INFO:
                    ParseMyCharInfo(msg);
                    break;
                case SubCmd.Sub.UPDATE_INFO_ME:
                    HandleUpdateInfoMe(msg);
                    break;
                case SubCmd.Sub.HP_UPDATE:
                    HandleHpUpdate(msg);
                    break;
                case SubCmd.Sub.MP_UPDATE:
                    HandleMpUpdate(msg);
                    break;
                case SubCmd.Sub.ME_LOAD_LEVEL:
                    HandleLoadLevel(msg);
                    break;
                case SubCmd.Sub.ME_LOAD_CLASS:
                    HandleLoadClass(msg);
                    break;
                case SubCmd.Sub.ME_LOAD_SKILL:
                    HandleLoadSkill(msg);
                    break;
                case SubCmd.Sub.POTENTIAL_UP:
                    HandlePotentialUp(msg);
                    break;
                case SubCmd.Sub.LOAD_THU_CUOI:
                    HandleLoadMounts(msg);
                    break;
                case SubCmd.Sub.SPEED_UPDATE:
                    HandleSpeedUpdate(msg);
                    break;
                case SubCmd.Sub.RESOURCE_UPDATE:
                    HandleResourceUpdate(msg);
                    break;
                case SubCmd.Sub.STAT_UPDATE:
                    HandleStatUpdate(msg);
                    break;
                case SubCmd.Sub.EFFECT_ADD:
                    HandleEffectAdd(msg);
                    break;
                case SubCmd.Sub.EFFECT_REFRESH:
                    HandleEffectRefresh(msg);
                    break;
                case SubCmd.Sub.EFFECT_REMOVE:
                    HandleEffectRemove(msg);
                    break;
                case SubCmd.Sub.PK_POINT_UPDATE:        // -117: "Diem hieu chien hien tai la X"
                case SubCmd.Sub.PK_POINT_CLEAR_FOCUS:   // -81: cap nhat karma + clear focus
                    HandlePkPoint(msg);
                    break;
                case SubCmd.Sub.PK_TYPE_UPDATE:         // -92: 1 char doi co PK (int charId + byte cTypePk)
                    HandlePkTypeUpdate(msg);
                    break;
                case SubCmd.Sub.USE_BOOK_SKILL:         // -102: dung SACH -> xoa o tui + hoc chieu
                    HandleUseBookSkill(msg);
                    break;
                case SubCmd.Sub.BAG_EXPAND:             // -91: mo rong tui + xoa o chua mon mo rong
                    HandleBagExpand(msg);
                    break;
                case SubCmd.Sub.BODY_ITEM_CLEAR:        // -80: xoa 1 o TRANG BI
                    HandleBodyItemClear(msg);
                    break;
                case SubCmd.Sub.BOX_ITEM_CLEAR:         // -75: xoa 1 o RUONG
                    HandleBoxItemClear(msg);
                    break;
                default:
                    LogUnknownSub(sub, msg);
                    break;
            }
        }

        // Moi sub LA chi ghi log MOT lan/phien (khuon FirstTime cua SubMessageHandler).
        // Truoc 2026-09-07 switch nay KHONG co nhanh default -> sub la bi nuot IM LANG, nen khong
        // co cach nao biet server co gui gi ma ta chua doc. Do la ly do mai khong biet chac
        // server co gui -54 (Thu cuoi) hay khong.
        private readonly HashSet<int> _seenUnknownSub = new HashSet<int>();

        private void LogUnknownSub(sbyte sub, NsoMessage msg)
        {
            bool first;
            lock (_seenUnknownSub) first = _seenUnknownSub.Add(sub);
            if (!first) return;
            State.RaiseDebugLog(string.Format("[sub] CHUA XU LY sub={0} len={1}", sub, msg.DataLength));
        }

        /// <summary>
        /// <c>Char.readParam</c> (`NinjaSchool_251_src/Char.cs:1033-1044`; MODGAME <c>Char.a(Message)</c>):
        /// <c>byte cspeed · int cMaxHP · int cMaxMP</c>. Mo dau CA BON goi -109/-124/-125/-126,
        /// bo qua la lech toan bo khung tu do tro di.
        /// </summary>
        private static void ReadParam(CharacterState c, BigEndianBinaryReader r)
        {
            c.Speed = r.ReadByte();
            c.MaxHp = r.ReadInt();
            c.MaxMp = r.ReadInt();
        }

        /// <summary>4 chi so tiem nang - kieu KHONG dong nhat tren day: 2 short roi 2 int.</summary>
        private static void ReadPotential(CharacterState c, BigEndianBinaryReader r)
        {
            c.Potential0 = r.ReadShort();
            c.Potential1 = r.ReadShort();
            c.Potential2 = r.ReadInt();
            c.Potential3 = r.ReadInt();
        }

        /// <summary>
        /// sub -124 ME_LOAD_LEVEL = LEN CAP (day moi la "len cap", KHONG phai -126).
        /// Wire: <c>readParam · long cEXP · short sPoint · short pPoint · potential[0..3]</c>
        /// (`Controller.cs:4990` + MODGAME `Controller.java:4058`).
        /// Day chinh la luc diem ky nang / tiem nang TANG, nen bo qua goi nay thi hai tab moi se
        /// hien so cu cho toi lan dang nhap sau.
        /// </summary>
        private void HandleLoadLevel(NsoMessage msg)
        {
            try
            {
                var c = State.MyChar;
                var r = msg.Reader;
                ReadParam(c, r);
                c.Exp = r.ReadLong();
                c.SkillPoint = r.ReadShort();
                c.PotentialPoint = r.ReadShort();
                ReadPotential(c, r);

                // Level KHONG co tren day - suy tu Exp, y het ParseMyCharInfo.
                int lv = GameData.ExpTable.GetLevelFromExp(c.Exp);
                if (lv > 0) c.Level = (byte)lv;
                State.RaiseDebugLog(string.Format("[Char] LEN CAP -> Lv.{0} sPoint={1} pPoint={2}",
                    c.Level, c.SkillPoint, c.PotentialPoint));
            }
            catch { }
        }

        /// <summary>
        /// sub -126 ME_LOAD_CLASS = DOI LOP (ten cu "LEVEL_UP" la SAI - xem chu thich o SubCommandCodes).
        /// Wire: <c>readParam · potential[0..3] · byte classId · short sPoint · short pPoint</c>.
        /// Doi lop thi game xoa sach vSkill (`Controller.java:4001-4003`) va cho server gui lai
        /// bang skill moi qua -125, nen ta cung xoa <c>SkillIds</c>.
        /// </summary>
        private void HandleLoadClass(NsoMessage msg)
        {
            try
            {
                var c = State.MyChar;
                var r = msg.Reader;
                ReadParam(c, r);
                ReadPotential(c, r);
                c.ClassId = r.ReadByte();
                c.SkillPoint = r.ReadShort();
                c.PotentialPoint = r.ReadShort();

                // ⚠️ CO Y KHAC GAME: game xoa sach vSkill o day (`Controller.java:4001-4003`) roi cho
                // server gui lai qua -125. Ta KHONG xoa.
                // Ly do la can nhac rui ro bat doi xung: neu ban do sub cua server nay lech (ban cu
                // cua repo tung coi -126 la "len cap"), viec xoa se cuop mat danh sach chieu GIUA
                // luc dang cay -> TrainMode het skill de danh. Con neu doi lop THAT thi -125 ve ngay
                // sau do va ghi de toan bo danh sach, nen khong xoa cung chang mat gi.
                // Dong log nay chinh la cai bay: neu no hien ra trong luc cay binh thuong (khong ai
                // doi lop) thi ban do sub SAI, phai dieu tra lai.
                State.RaiseDebugLog("[Char] DOI LOP -> classId=" + c.ClassId
                    + " (neu ban KHONG doi lop that thi bao lai: ma sub -126 co the sai)");
            }
            catch { }
        }

        /// <summary>
        /// sub -125 ME_LOAD_SKILL - tra loi hoc/nang ky nang.
        /// Wire: <c>readParam · short sPoint · byte n · n x short skillId</c>.
        ///
        /// ⚠️ Client GOC tu bia day HP/MP o goi nay (MODGAME phai va bang <c>HpMpSync.resync()</c>).
        /// Ta KHONG chep cai bia do - chi doc dung cac field tren.
        /// </summary>
        private void HandleLoadSkill(NsoMessage msg)
        {
            try
            {
                var c = State.MyChar;
                var r = msg.Reader;
                ReadParam(c, r);
                c.SkillPoint = r.ReadShort();
                int n = r.ReadUnsignedByte();
                c.SkillIds.Clear();
                for (int i = 0; i < n; i++) c.SkillIds.Add(r.ReadShort());
                State.SkillUpSeq++;   // danh thuc CongDiemRunner (vai tro Class_cl.u() cua MODGAME)
                // TU THEM, KHONG PHAI clone (CONG_DIEM.md §7.5, Q4): khong client nao chon lai chieu dang
                // danh sau -125, ma goi danh khong mang skill id. Server kieu "giu object chieu" se danh
                // bang CAP CU toi lan cmd 41 ke (Tan sat gui moi don; AttackSkillSelector/PK Am 60 s). Dat moc nay -> TrainMode/AttackSkillSelector gui
                // lai cmd 41 ngay tick sau = dung hieu ung ma MODGAME phai dang nhap lai moi co.
                State.SkillDanhBiDeAt = System.DateTime.UtcNow;
                State.RaiseDebugLog(string.Format("[Char] Ky nang cap nhat: {0} chieu, con {1} diem",
                    n, c.SkillPoint));
            }
            catch { }
        }

        /// <summary>
        /// sub -109 POTENTIAL_UP - tra loi cong diem tiem nang.
        /// Wire: <c>readParam · short pPoint · potential[0..3]</c>. (Cung so -109 dung de GUI.)
        /// Cung canh bao "tu bia day HP/MP" nhu -125.
        /// </summary>
        private void HandlePotentialUp(NsoMessage msg)
        {
            try
            {
                var c = State.MyChar;
                var r = msg.Reader;
                ReadParam(c, r);
                c.PotentialPoint = r.ReadShort();
                ReadPotential(c, r);
                State.PotentialUpSeq++;   // danh thuc CongDiemRunner (vai tro Class_cl.w() cua MODGAME)
                State.RaiseDebugLog(string.Format("[Char] Tiem nang: {0}/{1}/{2}/{3} - con {4} diem",
                    c.Potential0, c.Potential1, c.Potential2, c.Potential3, c.PotentialPoint));
            }
            catch { }
        }

        /// <summary>
        /// sub -54 LOAD_THU_CUOI - 5 o thu cuoi. Hai nguon doc lap khop tung byte:
        /// `NinjaSchool_251_src/Controller.cs:5577-5626` + `MODGAME/Controller.java:5049-5090`.
        ///
        /// Wire: <c>int charID</c> roi 5 lan <c>short tplId</c>; <b>chi khi != -1</b> moi doc tiep
        /// <c>byte upgrade · long expires · byte sys · byte nOption · nOption x {byte optId, int param}</c>.
        ///
        /// Goi nay dung chung cho CA nguoi khac trong map (co charID o dau) - ta chi giu cua MINH,
        /// nhung VAN PHAI doc het gap de khong lech buffer... that ra moi goi la mot NsoMessage rieng
        /// nen thoat som cung an toan; du vay van doc tron cho giong game va de sau nay muon lay
        /// thu cuoi cua nguoi khac thi co san cho.
        /// </summary>
        private void HandleLoadMounts(NsoMessage msg)
        {
            try
            {
                var c = State.MyChar;
                var r = msg.Reader;
                int charId = r.ReadInt();
                bool mine = (charId == c.CharId);

                var arr = CharacterState.NewMounts();
                int filled = 0;
                for (int i = 0; i < CharacterState.MOUNT_SLOTS; i++)
                {
                    var it = new Item();
                    it.TemplateId = r.ReadShort();
                    if (it.TemplateId != -1)
                    {
                        it.Upgrade = r.ReadByte();
                        it.Expires = r.ReadLong();
                        it.Sys = r.ReadByte();
                        it.Quantity = 1;
                        // ⚠️ KHONG co tren day - do DANG DEO thi LUON khoa. Ban goc dat tay:
                        // `arrItemMounts[i].isLock = true` (`NINJAPC/Controller.cs:5601`).
                        // Thieu dong nay = thao trang suc thu ra thi mon ve tui voi co "khong khoa"
                        // (cmd 108 chuyen nguyen doi tuong), bam Sap xep moi dung lai vi sub 115
                        // doc co that tu day. Xem GIAO_TIEP_VAT_PHAM.md §3b.
                        it.IsLock = true;
                        it.DetailLoaded = true;   // option ve ngay trong goi nay, khong can cmd 42
                        int nOpt = r.ReadUnsignedByte();
                        if (nOpt > 0) it.Options = new List<ItemOption>(nOpt);
                        for (int o = 0; o < nOpt; o++)
                        {
                            int optId = r.ReadUnsignedByte();
                            int param = r.ReadInt();
                            it.Options.Add(new ItemOption(optId, param));
                        }
                        filled++;
                    }
                    arr[i] = it;
                }

                if (!mine) return;   // thu cuoi cua nguoi khac - chua dung den
                c.MountItems = arr;
                State.RaiseDebugLog("[ThuCuoi] Nhan goi -54: " + filled + "/5 o co do");
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// sub -92: broadcast 1 char (bat ky) doi co PK. MODGAME messageSubCommand case -92
        /// (Controller.java:4963): int charId + byte cTypePk. Cap nhat cTypePk cua char do trong
        /// OtherPlayers (hoac MyChar neu la minh) - CAN cho Danh PK loc cTypePk==3 khi target bat PK
        /// SAU khi da vao map (readCharInfo chi doc cTypePk luc PLAYER_ADD).
        /// </summary>
        private void HandlePkTypeUpdate(NsoMessage msg)
        {
            try
            {
                int charId = msg.Reader.ReadInt();
                byte typePk = msg.Reader.ReadByte();
                if (State.MyChar != null && charId == State.MyChar.CharId)
                {
                    State.MyChar.TypePk = typePk;
                    // Phe o chien truong -> can cu dia NPC 25 dua toi. Clone ZangVPS aH.java:1427-1436.
                    if (typePk == 4) State.CanCuDiaMap = 98;
                    else if (typePk == 5) State.CanCuDiaMap = 104;
                    return;
                }
                var list = State.CurrentMap != null ? State.CurrentMap.OtherPlayers : null;
                if (list == null) return;
                foreach (var p in list)
                    if (p != null && p.CharId == charId) { p.TypePk = typePk; break; }
            }
            catch { }
        }

        /// <summary>
        /// sub -117 / -81: cap nhat KARMA (cPk / diem hieu chien) khi PK. MODGAME Controller case -117
        /// (dong 3589) / -81 (dong 5113) nam DUOI cmd -30 (messageSubCommand) - KHONG phai top-level.
        /// LOI CU: route nham top-level -117/-81 trong MessageRouter -> goi karma truc tiep KHONG BAO GIO
        /// khop -> mc.Pk chi doi luc chet (cmd 72) -> hysteresis PK am (danh->11->hap->6) tinh SAI.
        /// Doc 1 byte = cPk moi.
        /// </summary>
        private void HandlePkPoint(NsoMessage msg)
        {
            try { State.MyChar.Pk = msg.Reader.ReadByte(); }
            catch { }
        }

        private void ParseMyCharInfo(NsoMessage msg)
        {
            var r = msg.Reader;
            var c = State.MyChar;

            try
            {
                c.CharId = r.ReadInt();

                c.ClanName = r.ReadUTF();
                if (!string.IsNullOrEmpty(c.ClanName))
                    c.ClanType = r.ReadByte();

                c.TaskId = r.ReadByte();
                c.Gender = r.ReadByte();
                c.Head = r.ReadShort();
                c.Speed = r.ReadByte();
                c.Name = r.ReadUTF();
                c.Pk = r.ReadByte();
                c.TypePk = r.ReadByte();

                c.MaxHp = r.ReadInt();
                c.Hp = r.ReadInt();
                c.MaxMp = r.ReadInt();
                c.Mp = r.ReadInt();
                State.NoteServerHp(c.Hp, c.MaxHp, "sub MY_CHAR_INFO (dang nhap)");   // nguon HP THAT
            }
            catch { return; } // Critical fields failed - abort

            // HP/MP are now set correctly. Rest is optional.
            c.IsDead = false;
            State.IsInGame = true;

            try
            {
                c.Exp = r.ReadLong();
                c.ExpDown = r.ReadLong();
                // Level is NOT in packet - calculated from Exp (game: GameScr.getLevelExp)
                int calcLevel = GameData.ExpTable.GetLevelFromExp(c.Exp);
                if (calcLevel > 0) c.Level = (byte)calcLevel;
                // else: keep Level from char list (fallback)
                c.BuffHp = r.ReadShort();
                c.BuffMp = r.ReadShort();
                c.ClassId = r.ReadByte();
                c.PotentialPoint = r.ReadShort();
                c.Potential0 = r.ReadShort();
                c.Potential1 = r.ReadShort();
                c.Potential2 = r.ReadInt();
                c.Potential3 = r.ReadInt();
                c.SkillPoint = r.ReadShort();

                int numSkills = r.ReadUnsignedByte();
                c.SkillIds.Clear();
                for (int i = 0; i < numSkills; i++)
                    c.SkillIds.Add(r.ReadShort());

                c.Xu = r.ReadInt();
                c.Yen = r.ReadInt();
                c.Luong = r.ReadInt();

                int bagSize = r.ReadUnsignedByte();
                c.BagItems = new Item[bagSize];
                for (int i = 0; i < bagSize; i++)
                {
                    var item = new Item();
                    item.TemplateId = r.ReadShort();
                    if (item.TemplateId != -1)
                    {
                        item.IsLock = r.ReadBoolean();
                        if (State.ItemStore.HasUpgrade(item.TemplateId))
                            item.Upgrade = r.ReadByte();
                        item.IsExpires = r.ReadBoolean();
                        item.Quantity = r.ReadUnsignedShort();
                    }
                    c.BagItems[i] = item;
                }

                // ⚠️ O BODY = `template.type`, KHONG phai vi tri i trong goi. Game:
                //   byte var55 = ItemTemplates.get(var13).type;  arrItemBody[var55] = new Item();
                // (Controller.java:3892-3896). Trang 2 ngay ben duoi da lam DUNG, rieng trang 1 truoc
                // day dung `[i]` - hai trang hai luat. Quan trong vi `EquipFromBag` (cmd 11) ghi theo
                // Type, va `DanhVongMode.DangMacDungMon()` doc `BodyItems[MauItem.Type]`: chi can
                // server gui lech thu tu mot o la doc nham o ngay tu luc dang nhap, va chi "tu lanh"
                // sau lan mac do dau tien.
                for (int i = 0; i < CharacterState.BODY_PAGE; i++) c.BodyItems[i] = new Item();
                for (int i = 0; i < CharacterState.BODY_PAGE; i++)
                {
                    var item = new Item();
                    item.TemplateId = r.ReadShort();
                    if (item.TemplateId == -1) continue;
                    item.Upgrade = r.ReadByte();
                    item.Sys = r.ReadByte();
                    item.IsLock = true;   // do DANG MAC luon khoa - xem ghi chu o HandleLoadMounts

                    var tplBody = State.ItemStore != null ? State.ItemStore.Get(item.TemplateId) : null;
                    int slotBody = tplBody != null ? tplBody.Type : i;   // chua co bang -> lui ve vi tri
                    if (slotBody >= 0 && slotBody < CharacterState.BODY_PAGE) c.BodyItems[slotBody] = item;
                }

                c.IsHuman = r.ReadBoolean();
                c.IsNhanban = r.ReadBoolean();
                for (int i = 0; i < 4; i++) c.Fashion[i] = r.ReadShort();
            }
            catch { } // Non-critical fields - ignore errors

            try { for (int i = 0; i < 10; i++) r.ReadShort(); } catch { }
            ReadBodyPage2(c, r, c.BodyItems);
            try { r.ReadShort(); } catch { }   // ID_SUSANO

            // Tui + trang bi vua nap day du -> bao UI cap nhat hanh trang ngay.
            State.RaiseInventoryChanged();
        }

        /// <summary>
        /// Doc KHOI TRANG BI THU HAI ("Trang bi 2", o 16..31) - khoi 16 mon nam o CUOI goi
        /// MY_CHAR_INFO / sub 115, ngay sau 10 short thoi trang va ngay truoc short ID_SUSANO.
        /// Dinh dang y het khoi 1: `short templateId`, neu != -1 thi `byte upgrade, byte sys`.
        ///
        /// O DAT THEO `template.type + 16`, DUNG nhu game (Controller.cs:4703 / :4478 / :1685) chu
        /// khong theo thu tu tren day. Khoi 1 ben ta van dat theo thu tu vi da chung minh server gui
        /// dung thu tu type (anh hanh trang cua user 2026-09-07: o 2 = ao, 3 = vong co, ... 9 = bua);
        /// khoi 2 khong co bang chung do nen bam sat game, va chi lui ve dung thu tu khi bang item
        /// chua co template (luc do khong biet type).
        ///
        /// ⚠ Truoc 2026-09-07 khoi nay bi doc nham ten thanh "FashionBody" roi vut di, nen tab
        /// Trang bi cua tool chi hien duoc mot nua so o cua game. Xem docs/features/ICON_HANH_TRANG.md.
        /// </summary>
        private void ReadBodyPage2(CharacterState c, BigEndianBinaryReader r, Item[] body)
        {
            if (body == null || body.Length < CharacterState.BODY_SLOTS) return;

            // Xoa trang 2 truoc khi doc: relogin/sap xep tui gui lai ca goi, khong xoa thi mon da
            // thao van con nam lai o luoi.
            for (int i = CharacterState.BODY_PAGE; i < CharacterState.BODY_SLOTS; i++)
                body[i] = new Item();

            try
            {
                for (int i = 0; i < CharacterState.BODY_PAGE; i++)
                {
                    var it = new Item();
                    it.TemplateId = r.ReadShort();
                    if (it.TemplateId == -1) continue;
                    it.Upgrade = r.ReadByte();
                    it.Sys = r.ReadByte();
                    it.IsLock = true;   // do DANG MAC luon khoa - xem ghi chu o HandleLoadMounts

                    var tpl = State.ItemStore != null ? State.ItemStore.Get(it.TemplateId) : null;
                    int slot = (tpl != null ? tpl.Type : i) + CharacterState.BODY_PAGE;
                    if (slot >= CharacterState.BODY_PAGE && slot < CharacterState.BODY_SLOTS)
                        body[slot] = it;
                }
            }
            catch { }   // goi cu / server khong gui khoi 2 -> trang 2 de trong
        }

        /// <summary>
        /// sub 115 "UPDATE INFO ME": server re-gui TOAN BO thong tin nhan vat (sau Sap xep tui...).
        /// Port nguyen thu tu field tu MODGAME Controller case 115 (header KHAC MY_CHAR_INFO -127),
        /// phan tui/body cuoi giong het login. Dung TUI/BODY local roi gan cuoi de khong hong neu loi.
        /// </summary>
        private void HandleUpdateInfoMe(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                var c = State.MyChar;
                if (c == null) return;

                // 2026-09-07: goi nay chay CUNG thu tu field voi MY_CHAR_INFO nhung truoc day chi
                // giu Speed/Name/Pk/HP/MP/Xu/Yen/Luong/tui/trang bi - phan con lai doc de TROI
                // BUFFER roi vut. Hau qua thay ngay khi co tab Ky nang/Tiem nang: bam "Sap xep tui"
                // (chinh la luc server gui goi 115 nay) la hai tab do quay ve so cu.
                // Nay giu lai het nhung field ta co cho chua.
                r.ReadInt();                       // charID
                c.ClanName = r.ReadUTF();          // cClanName
                if (c.ClanName != "") c.ClanType = r.ReadByte();  // ctypeClan (chi khi co clan)
                c.TaskId = r.ReadByte();           // ctaskId
                r.ReadByte();                      // cgender
                c.Head = r.ReadShort();            // head
                c.Speed = r.ReadByte();            // cspeed
                c.Name = r.ReadUTF();              // cName
                c.Pk = r.ReadByte();               // cPk
                c.TypePk = r.ReadByte();           // cTypePk
                c.MaxHp = r.ReadInt();
                c.Hp = r.ReadInt();
                State.NoteServerHp(c.Hp, c.MaxHp, "sub MY_CHAR_INFO");   // nguon HP THAT
                c.MaxMp = r.ReadInt();
                c.Mp = r.ReadInt();
                c.Exp = r.ReadLong();              // cEXP
                c.ExpDown = r.ReadLong();          // cExpDown
                c.BuffHp = r.ReadShort();          // eff5BuffHp
                c.BuffMp = r.ReadShort();          // eff5BuffMp
                c.ClassId = r.ReadByte();          // nClass index
                c.PotentialPoint = r.ReadShort();  // pPoint
                c.Potential0 = r.ReadShort();
                c.Potential1 = r.ReadShort();
                c.Potential2 = r.ReadInt();
                c.Potential3 = r.ReadInt();
                c.SkillPoint = r.ReadShort();      // sPoint
                int nSkill = r.ReadByte() & 0xFF;
                c.SkillIds.Clear();
                for (int i = 0; i < nSkill; i++) c.SkillIds.Add(r.ReadShort());

                // Level KHONG co tren day (y het MY_CHAR_INFO) - suy tu Exp.
                int lvl115 = GameData.ExpTable.GetLevelFromExp(c.Exp);
                if (lvl115 > 0) c.Level = (byte)lvl115;

                c.Xu = r.ReadInt();
                c.Yen = r.ReadInt();
                c.Luong = r.ReadInt();

                // Tui (giong login MY_CHAR_INFO)
                int bagSize = r.ReadUnsignedByte();
                var bag = new Item[bagSize];
                for (int i = 0; i < bagSize; i++)
                {
                    var it = new Item();
                    it.TemplateId = r.ReadShort();
                    if (it.TemplateId != -1)
                    {
                        it.IsLock = r.ReadBoolean();
                        if (State.ItemStore.HasUpgrade(it.TemplateId))
                            it.Upgrade = r.ReadByte();
                        it.IsExpires = r.ReadBoolean();
                        it.Quantity = r.ReadUnsignedShort();
                    }
                    bag[i] = it;
                }

                // Body trang 1 (o 0..15) - giong login: o = template.type (Controller.java:3892-3896),
                // KHONG phai vi tri i trong goi. Xem chu thich dai o nhanh dang nhap.
                var body = CharacterState.NewBody();
                for (int i = 0; i < CharacterState.BODY_PAGE; i++)
                {
                    var it = new Item();
                    it.TemplateId = r.ReadShort();
                    if (it.TemplateId == -1) continue;
                    it.Upgrade = r.ReadByte();
                    it.Sys = r.ReadByte();
                    it.IsLock = true;   // do DANG MAC luon khoa - xem ghi chu o HandleLoadMounts

                    var tplB1 = State.ItemStore != null ? State.ItemStore.Get(it.TemplateId) : null;
                    int slotB1 = tplB1 != null ? tplB1.Type : i;
                    if (slotB1 >= 0 && slotB1 < CharacterState.BODY_PAGE) body[slotB1] = it;
                }

                // Body trang 2 (o 16..31) nam cuoi goi, sau vai truong khac - game doc y het o
                // case 115 (Controller.cs:4425-4510). Bao trong try rieng: goi 115 hong doan duoi
                // thi van giu duoc tui + trang 1 vua doc duoc.
                try
                {
                    c.IsHuman = r.ReadBoolean();
                    c.IsNhanban = r.ReadBoolean();
                    for (int i = 0; i < 4; i++) c.Fashion[i] = r.ReadShort();  // head/wp/body/leg
                    for (int i = 0; i < 10; i++) r.ReadShort();                // thoi trang
                    ReadBodyPage2(c, r, body);
                }
                catch { }

                // Gan cuoi cung (atomic) -> UI doc tui moi.
                c.BagItems = bag;
                c.BodyItems = body;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        // ===================================================================================
        // BON SUB LAM DOI TUI/TRANG BI/RUONG — port tu `NinjaSchool_251_src/Controller.cs`
        // (messageSubCommand). Truoc 2026-09-12 ca bon deu roi vao `LogUnknownSub` nen trang thai
        // cua bot lech han so voi server cho toi lan re-sync ke tiep. Chi tiet + trieu chung:
        // `docs/features/GIAO_TIEP_VAT_PHAM.md` §3.
        // ===================================================================================

        /// <summary>
        /// sub -102: dung SACH KY NANG (`Controller.cs:4500-4502`): <c>byte slot, short skillId</c>.
        /// Ban goc <b>xoa han o tui</b> roi them chieu vao <c>vSkill</c>.
        /// Day la ca dien hinh cua "dung vat pham xong no van con trong hanh trang": server bao
        /// bang sub -102 chu KHONG bang cmd 10/18, ta khong doc nen o tui khong bao gio don.
        /// </summary>
        private void HandleUseBookSkill(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int slot = r.ReadByte() & 0xFF;
                short skillId = r.ReadShort();

                var c = State.MyChar;
                if (c == null) return;

                var bag = c.BagItems;
                if (bag != null && slot < bag.Length)
                    bag[slot] = new Item();     // TemplateId -1 = o trong

                // Ban goc dung ca doi tuong Skill; ta chi giu danh sach id (giong ME_LOAD_SKILL).
                if (c.SkillIds != null && !c.SkillIds.Contains(skillId))
                    c.SkillIds.Add(skillId);

                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>
        /// sub -91: MO RONG TUI (`Controller.cs:5750-5761`): <c>ubyte soOMoi, ubyte slotXoa</c>.
        /// Ban goc cap mang moi kich thuoc <paramref name="soOMoi"/>, <b>copy theo do dai CU</b>
        /// (o moi de trong), roi xoa o chua chinh mon vua dung.
        /// Thieu nhanh nay thi tui khong bao gio nới ra ⇒ cmd 8 cho o moi bi
        /// <c>EnsureBagSlot</c> vá lại tung o mot, con mon "Mở rộng hành trang" thi ket lai.
        /// </summary>
        private void HandleBagExpand(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int soOMoi = r.ReadUnsignedByte();
                int slotXoa = r.ReadUnsignedByte();

                var c = State.MyChar;
                if (c == null || soOMoi <= 0 || soOMoi > 128) return;

                var cu = c.BagItems ?? new Item[0];
                var moi = new Item[soOMoi];
                for (int i = 0; i < soOMoi; i++)
                    moi[i] = (i < cu.Length && cu[i] != null) ? cu[i] : new Item();
                if (slotXoa >= 0 && slotXoa < soOMoi)
                    moi[slotXoa] = new Item();

                c.BagItems = moi;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>sub -80: xoa 1 o TRANG BI (`Controller.cs:5747-5748`): <c>byte slot</c>.</summary>
        private void HandleBodyItemClear(NsoMessage msg)
        {
            try
            {
                int slot = msg.Reader.ReadByte() & 0xFF;
                var body = State.MyChar != null ? State.MyChar.BodyItems : null;
                if (body == null || slot >= body.Length) return;
                body[slot] = new Item();
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        /// <summary>sub -75: xoa 1 o RUONG (`Controller.cs:5741-5742`): <c>byte slot</c>.</summary>
        private void HandleBoxItemClear(NsoMessage msg)
        {
            try
            {
                int slot = msg.Reader.ReadByte() & 0xFF;
                var box = State.MyChar != null ? State.MyChar.BoxItems : null;
                if (box == null || slot >= box.Length) return;
                box[slot] = new Item();
                State.BoxSeq++;
                State.RaiseInventoryChanged();
            }
            catch { }
        }

        private void HandleHpUpdate(NsoMessage msg)
        {
            int hp = msg.Reader.ReadInt();
            int maxHp = msg.Reader.ReadInt();
            State.MyChar.Hp = hp;
            State.MyChar.MaxHp = maxHp;
            State.NoteServerHp(hp, maxHp, "sub HP_UPDATE");   // nguon HP THAT - xem CharacterState.ServerHp
        }

        private void HandleMpUpdate(NsoMessage msg)
        {
            State.MyChar.Mp = msg.Reader.ReadInt();
            State.MyChar.MaxMp = msg.Reader.ReadInt();
        }


        private void HandleSpeedUpdate(NsoMessage msg)
        {
            State.MyChar.Speed = msg.Reader.ReadByte();
        }

        private void HandleResourceUpdate(NsoMessage msg)
        {
            try
            {
                State.MyChar.Xu = msg.Reader.ReadInt();
                State.MyChar.Yen = msg.Reader.ReadInt();
                State.MyChar.Luong = msg.Reader.ReadInt();
            }
            catch { }
        }

        private void HandleStatUpdate(NsoMessage msg)
        {
            try
            {
                State.MyChar.PotentialPoint = msg.Reader.ReadShort();
                State.MyChar.Potential0 = msg.Reader.ReadShort();
                State.MyChar.Potential1 = msg.Reader.ReadShort();
                State.MyChar.Potential2 = msg.Reader.ReadInt();
                State.MyChar.Potential3 = msg.Reader.ReadInt();
            }
            catch { }
        }

        // Log [EFF] moi su kien effect (verify wire). Da VERIFY 2026-07-07 (c): wire -101/-100/-99 khop
        // (id=8 dur=5000ms add->remove sau dung 5s...). Tat mac dinh; bat lai khi can soi effect.
        private static readonly bool EFFECT_DIAG = false;

        /// <summary>
        /// -101 THEM effect (clone MODGAME case -101): wire = byte id, int elapsedSec, int durationMs,
        /// short param. Ban cu doc thieu 1 int -> Param lech offset. ExpiresAt = het han cuc bo.
        /// </summary>
        private void HandleEffectAdd(NsoMessage msg)
        {
            try
            {
                byte id = msg.Reader.ReadByte();
                int elapsedSec = msg.Reader.ReadInt();
                int durationMs = msg.Reader.ReadInt();
                short param = msg.Reader.ReadShort();
                UpsertEffect(id, elapsedSec, durationMs, param);
                if (EFFECT_DIAG)
                    State.RaiseDebugLog(string.Format("[EFF] +add id={0} elapsed={1}s dur={2}ms param={3}",
                        id, elapsedSec, durationMs, param));
            }
            catch { }
        }

        /// <summary>-100 REFRESH effect cung id (cung wire nhu -101). Upsert theo id.</summary>
        private void HandleEffectRefresh(NsoMessage msg)
        {
            try
            {
                byte id = msg.Reader.ReadByte();
                int elapsedSec = msg.Reader.ReadInt();
                int durationMs = msg.Reader.ReadInt();
                short param = msg.Reader.ReadShort();
                UpsertEffect(id, elapsedSec, durationMs, param);
                if (EFFECT_DIAG)
                    State.RaiseDebugLog(string.Format("[EFF] ~refresh id={0} dur={1}ms", id, durationMs));
            }
            catch { }
        }

        /// <summary>-99 REMOVE effect theo template.id (clone MODGAME case -99: chi 1 byte id).</summary>
        private void HandleEffectRemove(NsoMessage msg)
        {
            try
            {
                byte id = msg.Reader.ReadByte();
                lock (State.Effects) State.Effects.RemoveAll(e => e.EffectId == id);
                if (EFFECT_DIAG) State.RaiseDebugLog("[EFF] -remove id=" + id);
            }
            catch { }
        }

        private void UpsertEffect(byte id, int elapsedSec, int durationMs, short param)
        {
            long remainMs = (long)durationMs - (long)elapsedSec * 1000L;
            if (remainMs < 0) remainMs = 0;
            var expires = DateTime.UtcNow.AddMilliseconds(remainMs);
            lock (State.Effects)
            {
                for (int i = 0; i < State.Effects.Count; i++)
                {
                    var e = State.Effects[i];
                    if (e != null && e.EffectId == id)
                    {
                        e.DurationMs = durationMs;
                        e.Param = param;
                        e.ExpiresAt = expires;
                        return;
                    }
                }
                State.Effects.Add(new Effect { EffectId = id, DurationMs = durationMs, Param = param, ExpiresAt = expires });
            }
        }

    }
}
