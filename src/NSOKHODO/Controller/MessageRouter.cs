using System;
using NSOKHODO.Auto;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    public class MessageRouter
    {
        private readonly NotLoginHandler _notLoginHandler;
        private readonly NotMapHandler _notMapHandler;
        private readonly SubCommandHandler _subCommandHandler;
        private readonly MapHandler _mapHandler;
        public MapHandler MapHandler { get { return _mapHandler; } }
        /// <summary>Để đọc bảng nj_image/nj_part SỐNG mà server gửi lúc đăng nhập.</summary>
        public NotMapHandler NotMapHandler { get { return _notMapHandler; } }
        private readonly CombatHandler _combatHandler;
        private readonly ChatHandler _chatHandler;
        private readonly PartyHandler _partyHandler;
        private readonly ItemHandler _itemHandler;
        private readonly PlayerHandler _playerHandler;
        private readonly MiscHandler _miscHandler;
        private readonly SubMessageHandler _subMessageHandler;
        // NSOKHODO: giao dich giua hai nguoi choi (37/43/45/46/57/58).
        private readonly TradeHandler _tradeHandler;
        private readonly TradeState _trade;

        /// <summary>
        /// NSOKHODO: moc xem MOI goi nhan TRUOC khi xu ly - de ghi hex cac goi giao dich + goi tui
        /// (docs/SPEC.md §8.1, <c>LogHexGiaoDich</c>). Null = khong ai nghe. Chay tren luong nhan:
        /// ben nghe phai tu loc cmd va tra ve nhanh.
        /// </summary>
        public Action<sbyte, NsoMessage> RawHook;

        // Theo doi cac cmd chua xu ly da log (moi loai chi log 1 lan, tranh spam).
        private readonly System.Collections.Generic.HashSet<sbyte> _unhandledSeen = new System.Collections.Generic.HashSet<sbyte>();

        // Chan spam cho SYSTEM_CHAT (-25): do la BANG CHAY TOAN SERVER (ai nang cap thanh cong,
        // ai len chuc...) nen 50 account se nhan cung mot chuoi -> khong ha nguong thi no nhan chim
        // log. SERVER_MSG (-26) va INFO_MSG (-24) thi KHONG chan: do la loi nhan gui RIENG cho
        // nhan vat nay, va chinh no la thu ta dang can (xem DONG_BANG_VI_TRI.md muc F1).
        private DateTime _lastTickerLogAt = DateTime.MinValue;
        private const int TICKER_LOG_GAP_MS = 10000;

        public MessageRouter(GameStateManager state, TradeState trade)
        {
            _trade = trade;
            _tradeHandler = new TradeHandler(state, trade);
            _notLoginHandler = new NotLoginHandler(state);
            _notMapHandler = new NotMapHandler(state);
            _subCommandHandler = new SubCommandHandler(state);
            _mapHandler = new MapHandler(state);
            _combatHandler = new CombatHandler(state);
            _chatHandler = new ChatHandler(state);
            _partyHandler = new PartyHandler(state);
            _itemHandler = new ItemHandler(state);
            _playerHandler = new PlayerHandler(state);
            _miscHandler = new MiscHandler(state);
            _subMessageHandler = new SubMessageHandler(state);
        }

        /// <summary>Tra handle kernel cua MapHandler khi router cu bi thay (moi lan InitSession).</summary>
        public void Dispose()
        {
            if (_mapHandler != null) _mapHandler.Dispose();
        }

        // Events for UI/Auto to hook into
        public event Action<string> OnLog;
        public event Action<string[], byte[]> OnCharListReceived;
        public event Action OnDataVersionReceived;
        public event Action OnCharInfoReceived;
        public event Action OnMapInfoReceived;
        public event Action<string> OnChatReceived;
        public event Action<string, string> OnPartyChat;   // (from, text) - cho GroupPlayController (danh theo nhom)
        public event Action<string, string> OnPrivateChat; // (from, text) - cho Kich Yen (lenh goi "ky ...")
        public event Action OnPartyUpdated;
        public event Action<int, string> OnPartyInviteReceived;
        public event Action<string> OnPartyJoinRequest;

        public event Action<int, int> OnHpMpChanged;
        public event Action OnDeath;
        public event Action OnRevive;

        public void HandleMessage(NsoMessage msg)
        {
            try
            {
                sbyte cmd = msg.Command;

                var mocTho = RawHook;
                if (mocTho != null)
                {
                    try { mocTho(cmd, msg); } catch { }
                }

                switch (cmd)
                {
                    // ===== GIAO DICH (NSOKHODO) =====
                    case Cmd.TRADE_INVITE:
                    case Cmd.TRADE_OPEN_UI:
                    case Cmd.TRADE_LOCK_ITEM:
                    case Cmd.TRADE_ACCEPT:
                    case Cmd.TRADE_CANCEL:
                    case Cmd.TRADE_OK:
                        _tradeHandler.Handle(cmd, msg);
                        break;

                    case Cmd.HANDSHAKE:
                        Log("[Handshake] Key received");
                        break;

                    case Cmd.NOT_LOGIN:
                        {
                            sbyte sub = msg.Reader.ReadSignedByte();
                            _notLoginHandler.Handle(sub, msg);
                        }
                        break;

                    case Cmd.NOT_MAP:
                        {
                            sbyte sub = msg.Reader.ReadSignedByte();
                            _notMapHandler.Handle(sub, msg);

                            if (sub == SubCmd.NotMap.DATA_VERSION)
                            {
                                var h = OnDataVersionReceived;
                                if (h != null) h();
                            }
                            else if (sub == SubCmd.NotMap.CHAR_LIST)
                            {
                                var h = OnCharListReceived;
                                if (h != null) h(_notMapHandler.LastCharList, _notMapHandler.LastCharLevels);
                            }
                        }
                        break;

                    case Cmd.SUB_COMMAND:
                        {
                            sbyte sub = msg.Reader.ReadSignedByte();
                            // -95 duoi cmd -30 = DONG HO TRAN loi dai (int giay). SubCommandHandler
                            // cua ta khong co case nao cho -95 nen chan o day la vo hai.
                            // (Cung byte -95 nhung trong gói VAO MAP lai la thong bao gia toc -
                            // hai dispatcher khac nhau, xem SubCommandCodes.DUEL_CLOCK.)
                            // -95 = dong ho tran loi dai. Ban nay khong co loi dai; NUOT goi (khong
                            // cho roi xuong _subCommandHandler, no khong co case -95 va se log "la").
                            if (sub == SubCmd.Sub.DUEL_CLOCK) break;
                            _subCommandHandler.Handle(sub, msg);

                            if (sub == SubCmd.Sub.MY_CHAR_INFO)
                            {
                                var h = OnCharInfoReceived;
                                if (h != null) h();
                            }
                            else if (sub == SubCmd.Sub.HP_UPDATE || sub == SubCmd.Sub.MP_UPDATE)
                            {
                                var state = _subCommandHandler.State;
                                var h = OnHpMpChanged;
                                if (h != null) h(state.MyChar.Hp, state.MyChar.Mp);
                            }
                        }
                        break;

                    case Cmd.MAP_INFO:
                        _mapHandler.HandleMapInfo(msg);
                        // dd15: the gioi quanh ta vua duoc dung lai -> khong phai cai xac im lang.
                        _mapHandler.State.NoteWorldEvent();
                        {
                            var h = OnMapInfoReceived;
                            if (h != null) h();
                        }
                        Log(string.Format("[Map] Entered: {0} (zone {1})",
                            _mapHandler.State.CurrentMap.MapName,
                            _mapHandler.State.CurrentMap.ZoneId));
                        break;

                    case Cmd.PRE_MAP_CHANGE:
                        _mapHandler.HandlePreMapChange(msg);
                        break;

                    // ===== GOI DA BIET NHUNG BAN NAY KHONG DUNG =====
                    // Nuot im lang thay vi de roi xuong nhanh "cmd la": chung den deu deu tu server
                    // (shop, text nhiem vu, loi moi ty thi...) va se lam ban log chan doan.
                    case Cmd.QUEST_TEXT:        // 53
                    case Cmd.SHOP_ITEM_LIST:    // 33
                    case Cmd.NPC_SAY:           // 38
                    case Cmd.ACCEPT_INVITE_DUEL:// 99
                    case Cmd.INVITE_TY_THI:     // 65
                    case Cmd.DUEL_START:        // 66
                    case Cmd.DUEL_END:          // 67
                        break;
                    case Cmd.NPC_MENU_LIST: // 63: danh sach muc menu NPC (lap UTF toi het goi)
                        // MODGAME cho menu NPC bang cmd 63; server NAY tra ve theo cmd 40. Nhan ca
                        // hai de khong phu thuoc phia nao - Navigator doi o NpcMenuEvent.
                        _mapHandler.HandleNpcOpenMenuResponse(msg);
                        break;

                    // Player events
                    case Cmd.PLAYER_MOVE:
                        _playerHandler.HandlePlayerMove(msg);
                        break;
                    case Cmd.PLAYER_LEAVE:
                        _playerHandler.HandlePlayerLeave(msg);
                        break;
                    case Cmd.PLAYER_ADD:
                        _playerHandler.HandlePlayerAdd(msg);
                        break;
                    case Cmd.SERVER_SET_POS:
                        _playerHandler.HandleServerSetPos(msg);
                        break;
                    case Cmd.WAKE_UP: // cmd=-10: server confirm revive (empty packet)
                        _playerHandler.HandleWakeUp(msg);
                        {
                            var h = OnRevive;
                            if (h != null) h();
                        }
                        break;
                    case Cmd.PLAYER_REVIVE: // cmd=88: revive with position
                        _playerHandler.HandleRevive(msg);
                        {
                            var h = OnRevive;
                            if (h != null) h();
                        }
                        break;

                    // Combat
                    case Cmd.MY_ATTACK_RESULT:
                        _combatHandler.HandleMyAttackResult(msg);
                        break;
                    case Cmd.MOB_ATTACK_ME:
                        _combatHandler.HandleMobAttackMe(msg);
                        {
                            var state = _combatHandler.State;
                            var h = OnHpMpChanged;
                            if (h != null) h(state.MyChar.Hp, state.MyChar.Mp);
                        }
                        break;
                    case Cmd.MOB_ATTACK_OTHER:
                        _combatHandler.HandleMobAttackOther(msg);
                        break;
                    case Cmd.MOB_DIE:
                        _combatHandler.HandleMobDie(msg);
                        break;
                    case Cmd.MOB_DODGE: // 51: quai ne don (truoc day roi vao default, chi log 1 lan)
                        _combatHandler.HandleMobDodge(msg);
                        break;
                    case Cmd.MOB_DIE_DROP: // 78: quai chet bien the event, LUON kem item drop
                        _combatHandler.HandleMobDieDrop(msg);
                        break;
                    case Cmd.MOB_RESPAWN:
                        _combatHandler.HandleMobRespawn(msg);
                        break;
                    // 60/61 CHIEU NHAN: nguoi choi KHAC dung chieu. Truoc day roi vao default va
                    // SERVER_FACTS.md §2 ghi "cosmetic, BO QUA" - nay chinh cai cosmetic do la thu
                    // cua so "Xem game" can. Handler thoat ngay khi cua so dong.
                    case Cmd.OTHER_CAST_MOB:
                        _combatHandler.HandleOtherCastMob(msg);
                        break;
                    case Cmd.OTHER_CAST_CHAR:
                        _combatHandler.HandleOtherCastChar(msg);
                        break;
                    case Cmd.EXP_GAIN:
                        _combatHandler.HandleExpGain(msg);
                        break;
                    case Cmd.EXP_DOWN_ADJUST:
                        _combatHandler.HandleExpDownAdjust(msg);
                        break;
                    case Cmd.PK_EXP_LOSS: // 72: chet + mat exp khi PK am (waitToDie) -> cap nhat cPk/cExpDown + CHET
                        _combatHandler.HandlePkExpLoss(msg);
                        {
                            // Server nay khong gui cmd -11 cho cai chet PK -> ban OnDeath o day de
                            // chay luong hoi sinh + remap (giong cmd DEATH). OnDeath tu guard neu da Dead.
                            var h = OnDeath;
                            if (h != null) h();
                        }
                        break;
                    case Cmd.YEN_GAIN: // -8: nhan yen truc tiep (real-time)
                        _combatHandler.HandleYenGain(msg);
                        break;
                    case Cmd.PK_POINT_UPDATE:      // -117: cap nhat karma (cPk) khi PK - karma tang/giam
                    case Cmd.PK_POINT_CLEAR_FOCUS:  // -81: cap nhat karma + clear focus
                        _combatHandler.HandlePkPointUpdate(msg);
                        break;
                    case Cmd.PLAYER_HP_CHANGE:
                        _combatHandler.HandlePlayerHpChange(msg);
                        {
                            var state = _combatHandler.State;
                            var h = OnHpMpChanged;
                            if (h != null) h(state.MyChar.Hp, state.MyChar.Mp);
                        }
                        break;
                    case Cmd.DEATH:
                        _combatHandler.HandleMyDeath(msg);
                        {
                            var h = OnDeath;
                            if (h != null) h();
                        }
                        break;

                    // Chat
                    case Cmd.CHAT_PUBLIC:
                    case Cmd.CHAT_PRIVATE:
                    case Cmd.CHAT_GLOBAL:
                    case Cmd.CHAT_PARTY:
                    case Cmd.CHAT_CLAN:
                    case Cmd.SERVER_MSG:
                    case Cmd.SYSTEM_CHAT:
                    case Cmd.INFO_MSG:
                        {
                            string chatText = _chatHandler.Handle(cmd, msg);

                            // NSOKHODO: server bao loi giao dich BANG CHU (khong co goi rieng). Giu lai
                            // cau tho cua -24/-26 de phien giao dich ghi kem ly do huy (SPEC M18).
                            // -25 la bang chay toan server - khong lay, chi lam nhieu.
                            if (cmd == Cmd.SERVER_MSG || cmd == Cmd.INFO_MSG)
                                _trade.GhiTinServer(_chatHandler.LastRaw);

                            // 2026-09-07: TRUOC DAY MOI TIN NHAN CHU CUA SERVER DEU BI VUT.
                            // `NsoClient.OnChatMessage` duoc phat nhung KHONG CO NOI NAO DANG KY
                            // (grep toan repo: 1 dong khai bao, 0 noi nghe), nen ca lop "server noi
                            // thang vi sao no tu choi" khong bao gio den tay ai. Day la diem mu so 1
                            // trong docs/features/DONG_BANG_VI_TRI.md muc F1.
                            // CHI ghi, KHONG doi hanh vi.
                            if (cmd == Cmd.SERVER_MSG || cmd == Cmd.INFO_MSG)
                            {
                                Log(chatText);
                            }
                            // THONG BAO NHAN: cmd -24 la chu server gui RIENG cho minh, ban goc day
                            // thang vao thanh chay chu day man - clone Controller.java:295:
                            //   InfoMe.addInfo(readUTF(), 50, mFont.tahoma_7_yellow)
                            // speed 50 (~2s) chu khong phai 20: dong nay thuong dai hon dong nhat do.
                            // Cmd -25/-26 KHONG vao day - ban goc cho chung o thanh TREN DINH (Info),
                            // ma thanh do ta khong lam.
                            // Dung chatRaw chu KHONG phai chatText: chatText co tien to "[Info] "
                            // cua khung log, ma game in dung chuoi server gui (user bao 2026-09-09).
                            if (cmd == Cmd.INFO_MSG)
                            {
                                var st = _mapHandler.State;
                                string raw = _chatHandler.LastRaw;
                                if (st.Notices.Enabled && !string.IsNullOrEmpty(raw))
                                    st.Notices.Push(raw, Models.Notice.KIND_YELLOW,
                                                    Models.Notice.SPEED_SERVER);
                            }
                            else if (cmd == Cmd.SYSTEM_CHAT)
                            {
                                if ((DateTime.UtcNow - _lastTickerLogAt).TotalMilliseconds >= TICKER_LOG_GAP_MS)
                                {
                                    _lastTickerLogAt = DateTime.UtcNow;
                                    Log(chatText);
                                }
                            }
                            // Dap do can bat chuoi "qua nhanh" tu -26 (dialog) / -25 (ticker): goi cmd 21
                            // bi tu choi vi gui day -> thoat cho NGAY thay vi doi het 8s timeout.
                            if (cmd == Cmd.SERVER_MSG || cmd == Cmd.SYSTEM_CHAT)
                                _mapHandler.State.SetLastServerText(chatText);
                            var h = OnChatReceived;
                            if (h != null) h(chatText);
                            if (cmd == Cmd.CHAT_PARTY)
                            {
                                var ph = OnPartyChat;
                                if (ph != null) ph(_chatHandler.LastPartyFrom, _chatHandler.LastPartyText);
                            }
                            if (cmd == Cmd.CHAT_PRIVATE)
                            {
                                var vh = OnPrivateChat;
                                if (vh != null) vh(_chatHandler.LastPrivateFrom, _chatHandler.LastPrivateText);
                            }
                        }
                        break;

                    // Party
                    // Cmd 23 chieu nhan (truong nhom): co nguoi xin vao nhom, payload = ten (UTF).
                    // Verify tu MODGAME Controller.java case 23: readUTF -> acceptPleaseParty(name).
                    case Cmd.PARTY_REQUEST_JOIN:
                        {
                            string requester = null;
                            try { requester = msg.Reader.ReadUTF(); } catch { }
                            var h = OnPartyJoinRequest;
                            if (h != null && !string.IsNullOrEmpty(requester)) h(requester);
                        }
                        break;
                    case Cmd.PARTY_INVITE:
                        _partyHandler.HandleInvite(msg);
                        {
                            var h = OnPartyInviteReceived;
                            if (h != null)
                                h(_partyHandler.LastInviteCharId, _partyHandler.LastInviteFrom);
                        }
                        break;
                    // ===== KET BAN - CHI NHAN DE GHI LOG, TA KHONG BAO GIO TRA LOI =====
                    // User chot 2026-09-10: "tren tool chi can lenh gui kb la xong".
                    // cmd 59 chieu NHAN = "<ten> vua them minh vao ds ban be, dang cho minh dong y".
                    // Cung ma lenh voi chieu gui - xem CommandCodes.FRIEND_ADD.
                    case Cmd.FRIEND_ADD:
                        {
                            string friend = null;
                            try { friend = msg.Reader.ReadUTF(); } catch { }
                            if (!string.IsNullOrEmpty(friend))
                                Log("[Ban be] " + friend + " vua gui loi ket ban (ta khong tra loi)");
                        }
                        break;
                    // cmd 84 = KET QUA ket ban (client goc Controller.cs case 84):
                    //   UTF(ten) + byte(type); type 0 = "ban da them <ten> vao danh sach",
                    //   type 1 = "ban va <ten> da la ban be" (hai ben da them nhau -> thanh cong).
                    // Day moi la thu bao ta biet lenh gui kb co an hay khong.
                    case Cmd.FRIEND_RESULT:
                        {
                            string friend = null; int type = -1;
                            try { friend = msg.Reader.ReadUTF(); type = msg.Reader.ReadByte(); } catch { }
                            if (!string.IsNullOrEmpty(friend))
                                Log(type == 1
                                    ? "[Ban be] Da la ban be voi " + friend
                                    : "[Ban be] Da them " + friend + " vao danh sach (type=" + type + ")");
                        }
                        break;
                    case Cmd.PARTY_UPDATE:
                        _partyHandler.HandleUpdate(msg);
                        {
                            var h = OnPartyUpdated;
                            if (h != null) h();
                        }
                        break;
                    case Cmd.PARTY_LEAVE:
                        _partyHandler.HandleDisband(msg);
                        {
                            var h = OnPartyUpdated;
                            if (h != null) h();
                        }
                        break;

                    // Items on map
                    case Cmd.ITEM_MAP_ADD:
                        _itemHandler.HandleItemMapAdd(msg);
                        break;
                    case Cmd.ITEM_MAP_ADD_AT_CHAR: // -6: item roi tai vi tri 1 char
                        _itemHandler.HandleItemMapAddAtChar(msg);
                        break;
                    case Cmd.ITEM_MAP_PICK_OTHER: // -13: nguoi khac nhat -> xoa khoi danh sach
                        _itemHandler.HandleItemMapPickOther(msg);
                        break;
                    case Cmd.ITEM_MAP_REMOVE:
                        _itemHandler.HandleItemMapRemove(msg);
                        break;
                    case Cmd.ITEM_MAP_PICK: // -14: ket qua nhat do; neu la vang thi cong yen (real-time)
                        _itemHandler.HandleItemPickResult(msg);
                        break;

                    // Items in bag (chieu nhan - giu BagItems dong bo cho auto an/dung/mua/nhat)
                    case Cmd.BAG_SET_QTY: // 7: set so luong 1 o tui
                        _itemHandler.HandleBagSetQty(msg);
                        break;
                    case Cmd.BAG_UPDATE: // 8: them item vao tui (mua/nhat) - de auto thay item moi
                        _itemHandler.HandleBagItemAdd(msg);
                        break;
                    case Cmd.BAG_ADD_QTY: // 9: cong so luong 1 o da co (server NAY dung khi nhat binh xep chong)
                        _itemHandler.HandleBagAddQty(msg);
                        break;
                    case Cmd.USE_ITEM: // 11: ket qua dung item (speed/maxHp/maxMp)
                        _itemHandler.HandleUseItemResult(msg);
                        break;
                    case Cmd.ITEM_BODY_TO_BAG: // 15: ket qua thao trang bi (o body -> o tui)
                        _itemHandler.HandleItemBodyToBag(msg);
                        break;
                    case Cmd.ITEM_QTY_DECREASE: // 18: tru so luong item trong tui
                        _itemHandler.HandleItemQtyDecrease(msg);
                        break;
                    case Cmd.BAG_ITEM_CHANGE: // 10: item bien khoi tui
                        _itemHandler.HandleBagItemRemove(msg);
                        break;
                    case Cmd.REQUEST_ITEM_INFO: // 42: chi tiet item (expires/option) khi UI xem; shop thi co GIA
                        _itemHandler.HandleItemInfo(msg);
                        break;
                    case Cmd.ITEM_THROW: // -12: ket qua vut item (xoa o tui)
                        _itemHandler.HandleItemThrowResult(msg);
                        break;
                    // 14: ket qua BAN lay Yen (byte slot, int yen, [short so luong da ban]).
                    // Goi NGAN (<5 byte) = ket qua VUT tren server nay - handler tu re nhanh.
                    case Cmd.SALE_ITEM:
                        _itemHandler.HandleSaleResult(msg);
                        break;
                    case Cmd.SALE_ITEM_XU: // 102: ket qua BAN lay Xu (byte slot, int xu) - xoa han o
                        _itemHandler.HandleSaleForXuResult(msg);
                        break;
                    // 108: ket qua thao THU CUOI / trang suc thu ve tui.
                    // KHONG danh thuc diem cho nao cua Danh Vong: mode do khong bao gio dung thu
                    // cuoi, them tin hieu thua chi tao rui ro danh thuc nham.
                    case Cmd.ITEM_MON_TO_BAG:
                        _itemHandler.HandleItemMountToBag(msg);
                        break;
                    case Cmd.ITEM_UPGRADE_CHANGE: // 112: doi cap nang cap 1 mon trong tui tai cho
                        _itemHandler.HandleItemUpgradeChange(msg);
                        break;
                    case Cmd.SPLIT_ITEM: // 22: ket qua tach item
                        _itemHandler.HandleItemSplitResult(msg);
                        break;
                    case Cmd.LUYEN_DA: // 19: ket qua luyen da thuong (tra Xu)
                        _itemHandler.HandleLuyenDaResult(msg, true);
                        break;
                    case Cmd.LUYEN_DA_LOCK: // 20: ket qua luyen da KHOA (tra Yen [+ Xu]) - auto dung cai nay
                        _itemHandler.HandleLuyenDaResult(msg, false);
                        break;
                    case Cmd.UPGRADE_ITEM: // 21: ket qua dap do (LEN / XIT) + cap nhat luong/xu/yen
                        _itemHandler.HandleUpgradeResult(msg);
                        break;
                    case Cmd.OPEN_UI: // 30: server mo 1 man hinh; 10 = man NANG CAP (Tho ren)
                        HandleOpenUi(msg);
                        // Hai diem cho lay cmd 30 lam mot trong cac nguon danh thuc (§C.9).
                        // DV_SHOP thi CO DIEU KIEN - clone ban fix MODGAME 2026-08-30: chi danh thuc
                        // khi day dung la mot bang SHOP va ta DA co bang do (nhanh "cache am").
                        // Danh thuc vo dieu kien se cho mode chay tiep voi bang shop rong.
                        break;
                    case Cmd.ITEM_BOX_TO_BAG: // 16: ket qua lay 1 mon tu ruong ve tui
                        _itemHandler.HandleItemBoxToBag(msg);
                        break;
                    case Cmd.ITEM_BAG_TO_BOX: // 17: ket qua cat 1 mon tu tui vao ruong
                        _itemHandler.HandleItemBagToBox(msg);
                        break;
                    case Cmd.BOX_ITEM_LIST: // 31: danh sach ruong (tra loi sub -103 requestItem(4))
                        _itemHandler.HandleBoxItemList(msg);
                        break;

                    case 13: // chieu nhan: cap nhat xu/yen/luong (MODGAME Controller case 13) - cho Yen/h
                        {
                            var c = _itemHandler.State.MyChar;
                            if (c != null)
                            {
                                c.Xu = msg.Reader.ReadInt();
                                c.Yen = msg.Reader.ReadInt();
                                c.Luong = msg.Reader.ReadInt();
                            }
                        }
                        break;

                    case Cmd.SKILL_SUBCMD: // -65: "CSkill"/"KSkill"/"OSkill" - skill dang chon
                        HandleSkillSubCommand(msg);
                        break;

                    case 36: // Zone list response
                        HandleZoneList(msg);
                        break;

                    case 40: // NPC openMenu response - list of UTF menu captions
                        _mapHandler.HandleNpcOpenMenuResponse(msg);
                        // ⚠️ Tren server NAY danh sach menu NPC ve theo cmd 40, KHONG phai cmd 63
                        // nhu MODGAME (DANH_VONG.md §H.2 ghi nham khuon nay la cmd 63).
                        break;

                    case Cmd.CHAR_VIEW_INFO: // 93: bang "Thong tin" nhan vat (tra loi viewInfo)
                        _miscHandler.HandleCharViewInfo(msg);
                        break;

                    case Cmd.CHAR_VIEW_INFO_EXTRA: // 101: 3 field bo sung cua bang do (tinh tu / banh)
                        _miscHandler.HandleCharViewInfoExtra(msg);
                        break;

                    case 96: // TaskOrder (nhiem vu): byte taskId,int count,int max,UTF name,UTF desc,ubyte killId,ubyte mapId
                        HandleTaskOrder(msg);
                        break;

                    case 97: // TaskOrder cap nhat: byte taskId, int count
                        HandleTaskOrderUpdate(msg);
                        break;

                    case 98: // TaskOrder xoa: byte taskId
                        HandleTaskOrderRemove(msg);
                        break;

                    // ===== NHIEM VU CHINH TUYEN (SERVER_FACTS §1d) =====
                    case 47: // TASK_GET: nap ca nhiem vu. Cung so voi Cmd.GET_TASK chieu GUI.
                        HandleTaskGet(msg);
                        break;

                    case 48: // TASK_NEXT: sang buoc ke, count ve 0. Khong payload.
                        {
                            var c = _mapHandler.State.MyChar;
                            var t = c != null ? c.MainTask : null;
                            if (t != null) c.MainTask = t.With(t.Index + 1, 0);   // MODGAME co kiem null, 251 khong
                        }
                        break;

                    case 49: // TASK_FINISH: tra nhiem vu -> ctaskId++ roi clearTask(). Khong payload.
                        {
                            var c = _mapHandler.State.MyChar;
                            if (c != null)
                            {
                                c.TaskId++;
                                c.MainTask = null;
                                Log("[Task] Hoan thanh NV chinh tuyen -> taskId=" + c.TaskId);
                            }
                        }
                        break;

                    case 50: // TASK_UPDATE: short count cua buoc hien tai
                        {
                            short cnt = msg.Reader.ReadShort();
                            var c = _mapHandler.State.MyChar;
                            var t = c != null ? c.MainTask : null;
                            if (t != null) c.MainTask = t.With(t.Index, cnt);
                        }
                        break;

                    case 122: // sub=0: THEM mob moi giua map (addMob) - Tinh Anh/Thu Linh XUAT HIEN
                        {
                            sbyte sub122 = msg.Reader.ReadSignedByte();
                            if (sub122 == 0)
                            {
                                _mapHandler.HandleAddMob(msg);
                                Log("[Mob] Co mob moi xuat hien (cmd 122)");
                            }
                        }
                        break;

                    case Cmd.SUB_MESSAGE: // 117: goi phu (-1) -> sub 2 = dong ho hieu ung. Nhanh khac bo qua.
                        {
                            string s117 = _subMessageHandler.Handle(msg);
                            if (s117 != null) Log(s117);
                        }
                        break;

                    default:
                        // Log MOI cmd chua xu ly (moi loai 1 lan, tranh spam).
                        lock (_unhandledSeen)
                        {
                            if (_unhandledSeen.Add(cmd))
                                Log(string.Format("[Server] cmd CHUA XU LY={0} len={1} hex={2}",
                                    cmd, msg.DataLength, HexHead(msg)));
                        }
                        break;
                }

            }
            catch (System.IO.EndOfStreamException)
            {
                // Ignore - optional fields in packets we don't fully parse
            }
            catch (Exception ex)
            {
                Log(string.Format("[Error] cmd={0}: {1}", msg.Command, ex.Message));
            }
        }

        /// <summary>
        /// cmd -65: sub-command dang chuoi. Format (giong Java Controller case -65):
        /// UTF name + int len + len bytes (+ 1 byte trailing). "CSkill" => bytes[0] = skill id dang chon.
        /// Luu vao MyChar.CurrentSkillId de TrainMode dung lam skill danh mac dinh.
        /// </summary>
        private void HandleSkillSubCommand(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                string name = r.ReadUTF();
                int len = r.ReadInt();
                byte[] data = len > 0 ? r.ReadFully(len) : null;

                if (name == "CSkill" && data != null && data.Length > 0)
                {
                    var state = _subCommandHandler.State;
                    if (state.MyChar != null)
                    {
                        state.MyChar.CurrentSkillId = data[0];
                        Log(string.Format("[Skill] Skill dang chon: {0}", data[0]));
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// cmd 96 (MODGAME Controller case 96): server gui 1 nhiem vu (TaskOrder).
        /// Format da verify bang hex tren server (len=99): byte taskId, int count, int maxCount,
        /// UTF name, UTF description, ubyte killId, ubyte mapId. taskId==0 = "Nhiem vu hang ngay"
        /// (Char.j(0)) -> luu DailyQuestMapId de "Di map nhiem vu qua NPC 25".
        /// </summary>
        private void HandleTaskOrder(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int taskId = r.ReadByte() & 0xFF;
                int count = r.ReadInt();
                int maxCount = r.ReadInt();
                string name = r.ReadUTF();
                string desc = r.ReadUTF();
                int killId = r.ReadByte() & 0xFF;
                int mapId = r.ReadByte() & 0xFF;

                var c = _mapHandler.State.MyChar;
                if (c != null)
                    c.TaskOrders = UpsertTaskOrder(c.TaskOrders,
                        new TaskOrderInfo(taskId, count, maxCount, name, desc, killId, mapId));
                if (c != null && taskId == 0)
                {
                    c.DailyQuestMapId = mapId;
                    c.DailyQuestKillId = killId;
                    Log(string.Format("[Task] NV hang ngay: map={0} killId={1} ({2}/{3})",
                        mapId, killId, count, maxCount));
                }
            }
            catch { }
        }

        // Client addElement thang, khong kiem trung. CO Y thay dong cung taskId: cmd 97/98 cua client
        // cung chi tim dong DAU TIEN theo taskId, nen dong trung la dong chet - bang ta khong nhan doi.
        private static TaskOrderInfo[] UpsertTaskOrder(TaskOrderInfo[] ds, TaskOrderInfo o)
        {
            ds = ds ?? new TaskOrderInfo[0];
            for (int i = 0; i < ds.Length; i++)
            {
                if (ds[i] != null && ds[i].TaskId == o.TaskId)
                {
                    var thay = (TaskOrderInfo[])ds.Clone();
                    thay[i] = o;
                    return thay;
                }
            }
            var them = new TaskOrderInfo[ds.Length + 1];
            Array.Copy(ds, them, ds.Length);
            them[ds.Length] = o;
            return them;
        }

        /// <summary>cmd 97: byte taskId, int count (NinjaSchool_251_src Controller.cs:2799).</summary>
        private void HandleTaskOrderUpdate(NsoMessage msg)
        {
            try
            {
                int taskId = msg.Reader.ReadByte() & 0xFF;
                int count = msg.Reader.ReadInt();
                var c = _mapHandler.State.MyChar;
                var ds = c != null ? c.TaskOrders : null;
                if (ds == null) return;
                for (int i = 0; i < ds.Length; i++)
                {
                    if (ds[i] != null && ds[i].TaskId == taskId)
                    {
                        var moi = (TaskOrderInfo[])ds.Clone();
                        moi[i] = ds[i].WithCount(count);
                        c.TaskOrders = moi;
                        return;
                    }
                }
            }
            catch { }
        }

        /// <summary>cmd 98: byte taskId -> xoa dong dau tien trung (NinjaSchool_251_src Controller.cs:2817).</summary>
        private void HandleTaskOrderRemove(NsoMessage msg)
        {
            try
            {
                int taskId = msg.Reader.ReadByte() & 0xFF;
                var c = _mapHandler.State.MyChar;
                var ds = c != null ? c.TaskOrders : null;
                if (ds == null) return;
                for (int i = 0; i < ds.Length; i++)
                {
                    if (ds[i] != null && ds[i].TaskId == taskId)
                    {
                        var moi = new TaskOrderInfo[ds.Length - 1];
                        Array.Copy(ds, 0, moi, 0, i);
                        Array.Copy(ds, i + 1, moi, i, ds.Length - i - 1);
                        c.TaskOrders = moi;
                        return;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// cmd 47 TASK_GET: nap nhiem vu chinh tuyen. Clone NinjaSchool_251_src Controller.cs:1647-1685
        /// (MODGAME Controller.java:1415-1443 giong tung field). Wire: SERVER_FACTS §1d.
        /// Phan count + counts client doc trong try{} (server co the khong gui) -> doc theo Available,
        /// thieu thi giu -1 y nhu client.
        /// </summary>
        private void HandleTaskGet(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                short taskId = r.ReadShort();
                int index = r.ReadSignedByte();
                string name = r.ReadUTF();
                string detail = r.ReadUTF();
                int n = r.ReadSignedByte();
                if (n < 0) n = 0;
                var subNames = new string[n];
                var counts = new short[n];
                for (int i = 0; i < n; i++)
                {
                    string s = r.ReadUTF();
                    counts[i] = -1;
                    if (s.Length > 0) subNames[i] = s;   // chuoi rong -> null = buoc noi tiep
                }
                short count = -1;
                if (r.Available >= 2)
                {
                    count = r.ReadShort();
                    for (int i = 0; i < n && r.Available >= 2; i++) counts[i] = r.ReadShort();
                }

                var c = _mapHandler.State.MyChar;
                if (c != null) c.MainTask = new MainTask(taskId, index, count, name, detail, subNames, counts);
                Log(string.Format("[Task] NV chinh tuyen: taskId={0} ctaskId={1} buoc={2}/{3} count={4} len={5}",
                    taskId, c != null ? c.TaskId : -1, index, n, count, msg.DataLength));
            }
            catch (Exception ex)
            {
                Log(string.Format("[Task] LOI doc cmd 47 len={0}: {1} hex={2}", msg.DataLength, ex.Message, HexHead(msg)));
            }
        }

        /// <summary>
        /// cmd 30: server MO 1 man hinh. Payload tren server nay = 1 byte typeUI (verify MODGAME
        /// Controller.java:1220). Ban game 2.5.1 con gui them 2 UTF (svTitle/svAction) nen ta thu
        /// doc trong try/catch - an toan ca hai.
        /// typeUI 10 = man NANG CAP (Tho ren) - engine dap do cho tin hieu nay truoc khi gui cmd 21.
        /// </summary>
        private void HandleOpenUi(NsoMessage msg)
        {
            try
            {
                int typeUi = msg.Reader.ReadByte() & 0xFF;
                try { msg.Reader.ReadUTF(); msg.Reader.ReadUTF(); } catch { } // 2.5.1 co, server nay khong
                var st = _mapHandler.State;
                st.LastUiType = typeUi;
                st.LastUiAtTicks = System.DateTime.UtcNow.Ticks;
                Log("[UI] Server mo man hinh typeUI=" + typeUi);
            }
            catch { }
        }

        /// <summary>
        /// cmd 36 OPEN_ZONE_LIST - server tra danh sach khu. Wire da chot (SERVER_FACTS.md §22):
        ///     byte count | count x { byte danSo, byte soNhom }
        /// tuc 2 BYTE moi khu, va CHI SO MANG CHINH LA zoneId.
        ///
        /// SUA 2026-09-06: ban cu doc `count` byte DON -> mang dan xen [danSo0, soNhom0, danSo1, ...]
        /// va chi phu duoc nua dau so khu. Log that da to cao: "30 zones, players: [9,2,6,1,3,1,...]"
        /// - vi tri chan bien thien rong (dan so), vi tri le luon 0-2 (so nhom). Hau qua: moi phep
        /// "chon khu vang nhat" deu roi vao mot vi tri LE (so nhom ~ 0) => chon bua.
        /// 3 nguon doc lap khop nhau: MODGAME GameScr.java:1450-1463, NinjaSchool_251_src
        /// GameScr.cs:2256-2278, NSOCHIP dh.java:1508-1523.
        ///
        /// count == 0 la HOP LE (map khong chia khu - 299/566 goi trong log): ban 251 boc `if (b > 0)`
        /// nen KHONG dung toi mang cu. Ta lam y vay, neu khong thi moi lan di ngang mot map khong-khu
        /// la xoa sach so lieu vua lay.
        /// </summary>
        private void HandleZoneList(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                int count = r.ReadByte() & 0xFF;
                if (count <= 0)
                {
                    Log("[Zone] List: 0 khu (map khong chia khu) - giu nguyen so lieu cu");
                    return;
                }

                var pop = new int[count];
                var party = new int[count];
                for (int i = 0; i < count; i++)
                {
                    pop[i] = r.ReadByte() & 0xFF;
                    party[i] = r.ReadByte() & 0xFF;
                }

                var st = _mapHandler.State;
                st.ZonePlayerCounts = pop;
                st.ZonePartyCounts = party;
                st.ZoneCountsMapId = st.CurrentMap != null ? st.CurrentMap.MapId : -1;
                st.ZoneCountsAtUtc = System.DateTime.UtcNow;

                int best = -1, bestPop = int.MaxValue;
                for (int i = 0; i < count; i++)
                    if (pop[i] < bestPop) { bestPop = pop[i]; best = i; }

                Log(string.Format("[Zone] List: {0} khu, nguoi=[{1}] -> khu vang nhat = {2} ({3} nguoi)",
                    count, string.Join(",", System.Array.ConvertAll(pop, x => x.ToString())), best, bestPop));
            }
            catch { }
        }

        /// <summary>
        /// 12 byte dau cua goi, dang hex — de nhan dang goi la ma chua co handler. Vi du PNG bat
        /// dau bang 89 50 4E 47, nen chi liec log la biet server co dang tra ANH qua cmd nay khong.
        /// </summary>
        private static string HexHead(Core.NsoMessage msg)
        {
            try
            {
                var d = msg.GetData();
                if (d == null || d.Length == 0) return "(rong)";
                int n = d.Length < 12 ? d.Length : 12;
                var sb = new System.Text.StringBuilder(n * 3);
                for (int i = 0; i < n; i++) sb.Append(d[i].ToString("X2")).Append(' ');
                return sb.ToString().TrimEnd();
            }
            catch { return "(khong doc duoc)"; }
        }

        private void Log(string text)
        {
            if (!NSOKHODO.Logging.Logger.Enabled) return;   // cong tac tong: tat -> khong sinh log
            var h = OnLog;
            if (h != null) h(text);
        }
    }
}
