namespace NSOKHODO.Protocol
{
    public static class Cmd
    {
        // Meta commands
        public const sbyte HANDSHAKE = -27;
        public const sbyte NOT_LOGIN = -29;
        public const sbyte NOT_MAP = -28;
        public const sbyte SUB_COMMAND = -30;
        // cmd -65: sub-command dang chuoi (UTF name + byte[] data): "CSkill" (skill dang chon),
        // "KSkill", "OSkill". Xem Java Controller case -65 + GameScr.onCSkill.
        public const sbyte SKILL_SUBCMD = -65;
        public const sbyte LARGE_PACKET = -32;
        public const sbyte SPECIAL_LEN = -31;

        // Chat & Messages
        public const sbyte SERVER_MSG = -26;
        public const sbyte SYSTEM_CHAT = -25;
        public const sbyte INFO_MSG = -24;
        public const sbyte CHAT_PUBLIC = -23;
        public const sbyte CHAT_PRIVATE = -22;
        public const sbyte CHAT_GLOBAL = -21;
        public const sbyte CHAT_PARTY = -20;
        public const sbyte CHAT_CLAN = -19;

        // Map
        public const sbyte MAP_INFO = -18;
        public const sbyte REQUEST_CHANGE_MAP = -17;
        public const sbyte PRE_MAP_CHANGE = -16;
        public const sbyte ITEM_MAP_REMOVE = -15;
        public const sbyte ITEM_MAP_PICK = -14;
        // -13: nguoi KHAC nhat item (MODGAME Controller case -13: itemMapId short, charId int).
        // Thieu case nay -> item bi nguoi khac nhat van nam "ma" trong ItemsOnMap.
        public const sbyte ITEM_MAP_PICK_OTHER = -13;
        public const sbyte ITEM_THROW = -12;
        public const sbyte DEATH = -11;
        public const sbyte WAKE_UP = -10;
        public const sbyte RETURN_TOWN = -9;
        public const sbyte YEN_GAIN = -8; // game case -8: yen += readInt() (nhan yen real-time)

        // Combat results
        public const sbyte MY_ATTACK_RESULT = -1;
        public const sbyte MOB_ATTACK_OTHER = -2;
        public const sbyte MOB_ATTACK_ME = -3;
        public const sbyte MOB_DIE = -4;
        public const sbyte MOB_RESPAWN = -5;
        // -6: item roi tai vi tri 1 char (MODGAME Controller case -6: charId int + itemMap 4 short).
        // Ten cu MOB_ME_DIE (ke thua NSOAFK) la SAI - chua tung duoc route nen khong anh huong.
        public const sbyte ITEM_MAP_ADD_AT_CHAR = -6;
        // 78: quai chet bien the event - KHONG co dmg/crit, LUON kem item (MODGAME case 78).
        public const sbyte MOB_DIE_DROP = 78;
        // 51: quai NE don cua minh - mobId(ubyte) + hp(int), len=5 (MODGAME Controller.java:1484).
        // CHUA XAC MINH bang hex tren server dich, xem CombatHandler.HandleMobDodge.
        public const sbyte MOB_DODGE = 51;

        // Player & Movement
        public const sbyte PLAYER_MOVE = 1;
        public const sbyte PLAYER_LEAVE = 2;
        public const sbyte PLAYER_ADD = 3;
        public const sbyte PLAYER_ATTACK = 4;
        public const sbyte EXP_GAIN = 5;
        public const sbyte ITEM_MAP_ADD = 6;
        public const sbyte EXP_DOWN_ADJUST = 71; // game case 71: cExpDown -= delta (exp am khi PK)
        public const sbyte PK_EXP_LOSS = 72;     // game case 72: MAT exp khi PK am (cap nhat cPk + cExpDown)

        // Items
        public const sbyte BAG_SET_QTY = 7;   // chieu nhan: set so luong 1 o tui (MODGAME case 7)
        public const sbyte BAG_UPDATE = 8;    // chieu nhan: them item vao 1 o tui (MODGAME case 8)
        // cmd 9 = CONG so luong 1 o tui da co (game 251 case 9: byte slot + [short amount, thieu=1] -> quantity += amount).
        // Day la duong server NAY dung khi NHAT item xep chong (binh HP/MP...) da co san trong tui - KHONG phai cmd 8.
        // (Ten cu "BODY_UPDATE" sai - cmd 9 khong lien quan trang bi.)
        public const sbyte BAG_ADD_QTY = 9;
        public const sbyte BAG_ITEM_CHANGE = 10;
        public const sbyte USE_ITEM = 11;
        // GUI: dung item DOI MAP (bua/ve dich chuyen). Wire MODGAME Service.java:436
        // useItemChangeMap: [12][byte bagIndex][byte destIdx]. destIdx la MUC trong danh sach
        // diem den cua bua (engine dap do dung 5 = Lang Tone / map 22 - clone AutoNangCap.toiMapRen).
        public const sbyte USE_ITEM_CHANGE_MAP = 12;
        public const sbyte BUY_ITEM = 13;
        /// <summary>
        /// cmd 14 — BAN cho NPC, lay YEN. Hai chieu.
        /// <para>GUI: <c>byte indexUI</c> [+ so luong] — xem <c>ItemService.SendSaleItem</c>.</para>
        /// <para>NHAN (client goc `NinjaSchool_251_src/Controller.cs:2478`):
        /// <c>byte slot, int yen, [short soLuongDaBan, thieu = 1]</c> → tru so luong, het thi don o,
        /// va <b>day la duong cap nhat Yen sau khi ban</b>.</para>
        /// </summary>
        public const sbyte SALE_ITEM = 14;
        /// <summary>
        /// cmd 102 — chieu NHAN: ban lay XU (`Controller.cs:2451`): <c>byte slot, int xu</c>.
        /// Xoa HAN o tui (khong tru dan nhu cmd 14). Truoc 2026-09-12 ta khong he doc lenh nay.
        /// </summary>
        public const sbyte SALE_ITEM_XU = 102;
        public const sbyte ITEM_BODY_TO_BAG = 15;
        public const sbyte ITEM_BOX_TO_BAG = 16;
        public const sbyte ITEM_BAG_TO_BOX = 17;
        // Chieu NHAN: server tru so luong item trong tui (sau khi dung/tieu hao).
        // Verify tu MODGAME Controller.java case 18: readByte slot + readShort qty (default 1).
        public const sbyte ITEM_QTY_DECREASE = 18;
        // Luyen da (gop 4 vien cung cap -> 1 vien cap+1). Hai chieu, wire tu MODGAME:
        //   GUI (Service.java:580/625): [cmd][byte indexUI]... (chi day slot tui, khong co byte dem).
        //   NHAN (Char.java:7743 crystalCollect): byte status(1=xong) + byte indexUI + short templateId
        //   + bool isLock + bool isExpires + (cmd 19: int xu | cmd 20: int yen [+ int xu]).
        // 19 = luyen thuong (crystalCollect), 20 = luyen KHOA (crystalCollectLock - auto MODGAME dung 20).
        public const sbyte LUYEN_DA = 19;
        public const sbyte LUYEN_DA_LOCK = 20;
        // Dap do / nang cap trang bi. Hai chieu, wire XAC NHAN BOI 2 NGUON DOC LAP
        // (NinjaSchool_251_src Service.cs:998 va MODGAME Service.java:602):
        //   GUI: [21][bool useLuong][byte indexUI mon][byte indexUI tung vien da]...
        //        -> KHONG co byte dem so da; server dem bang do dai goi (giong cmd 19/20).
        //   NHAN: byte result + int luong + int xu + int yen + byte upgradeMoi
        //        -> result 1 = LEN, 5/6 = kham ngoc, con lai = XIT (tut ve moc lv/4*4).
        //        Client goc chi doc byte thu 5 khi dang mo man nang cap, nhung SERVER LUON GUI.
        public const sbyte UPGRADE_ITEM = 21;
        // Server MO 1 man hinh (UI). Chi 1 byte tren server nay; ban 2.5.1 co them 2 UTF
        // (svTitle/svAction) nen ta doc byte roi THU doc 2 UTF trong try/catch.
        // typeUI can nho: 4=ruong 10=NANG CAP 11=luyen da(xu) 12=luyen da(yen) 13=tach/gop
        //                 31=nang cap bang luong 43=luyen thach; 2,6-9,14-29,32,34,35=cac shop.
        public const sbyte OPEN_UI = 30;
        // NHAN: DANH SACH RUONG (tra loi sub -103 requestItem(4)). Wire MODGAME Controller.java:1230:
        //   int xuInBox + ubyte soO + soO x { short templateId (-1 = o trong),
        //   bool isLock, [byte upgrade neu trang bi/ngoc kham], bool isExpires, short quantity }.
        // Server KHONG bao truoc do dai -> engine phai cho CUNG 7s (khong co su kien nao khac).
        public const sbyte BOX_ITEM_LIST = 31;
        public const sbyte SPLIT_ITEM = 22;       // gui: tach 1 o tui (Service 251 splitItem, cmd 22)
        // Hai chieu: client GUI {typeUI, indexUI} xin chi tiet 1 item; server TRA expires/option/gia.
        // Verify Service/Controller 251 case 42 (xem SERVER_FACTS muc Item detail).
        public const sbyte REQUEST_ITEM_INFO = 42;

        // Karma (diem hieu chien / cPk) - server cap nhat RIENG khi PK, KHONG chi luc chet.
        // Verify MODGAME Controller.java: case -117 (line 3589) doc 1 byte cPk + hieu ung 21 (goi
        // moi khi karma doi luc PK); case -81 (line 5113) doc 1 byte cPk + clear charFocus. Thieu 2
        // case nay -> tool khong thay karma tang -> PK am khong bao gio doi sang khu danh PK.
        public const sbyte PK_POINT_UPDATE = -117;
        public const sbyte PK_POINT_CLEAR_FOCUS = -81;

        // CHU Y: 117 DUONG la lenh KHAC HAN -117 o tren (command code la sbyte).
        // 117 = goi "nhieu viec": byte dau == -1 thi la GOI PHU (vat pham/vi thu/dong ho hieu ung),
        // nguoc lai la 3 lop cay trang tri + trung quai + item tren map.
        // Ta CHI doc nhanh goi phu sub 2 (dong ho hieu ung); nhanh con lai bo qua.
        // Verify NinjaSchool_251 Controller.cs:259-268 + Readmsg.cs:11-30.
        // LUU Y NGUON: MODGAME (ban 1.8.0) KHONG co co che nay - xem SERVER_FACTS.
        public const sbyte SUB_MESSAGE = 117;

        // Party
        public const sbyte PARTY_INVITE = 79;
        public const sbyte PARTY_ACCEPT_INVITE = 80;
        public const sbyte PARTY_CANCEL = 81;
        public const sbyte PARTY_UPDATE = 82;
        public const sbyte PARTY_LEAVE = 83;
        public const sbyte PARTY_REQUEST_JOIN = 23;
        public const sbyte PARTY_ACCEPT_JOIN = 24;

        // Ket ban - MOT ma lenh dung CA HAI CHIEU (verify NinjaSchool_251):
        //   GUI : 59 + UTF(ten) = xin ket ban voi <ten>            (Service.cs addFriend)
        //   NHAN: 59 + UTF(ten) = <ten> da them MINH vao ds ban be (Controller.cs case 59
        //         -> vFriendWait, cho minh dong y)
        //   DONG Y ket ban = gui LAI chinh cmd 59 voi ten nguoi do (GameScr.cs case 1107931).
        // Xoa ban be thi KHAC: sub-command -83 (Service.cs removeFriend) - chua dung.
        public const sbyte FRIEND_ADD = 59;
        // Ket qua ket ban (Controller.cs case 84): UTF(ten) + byte(type).
        //   type 0 = da them <ten> vao danh sach  ·  type 1 = hai ben da la ban be.
        // Ta CHI ghi log - khong tra loi gi.
        public const sbyte FRIEND_RESULT = 84;

        // ===== GIAO DICH giua hai nguoi choi (NSOKHODO) =====
        // Wire giong het o ba ban client 148 / 180 / 251 va khop ban 217 (NSOTRUNGDUC) tung goi.
        // Hop dong day du + nguon: docs/GIAO_DICH.md. Tom tat:
        //   43 C->S {int idDoiPhuong} moi  · S->C {int idNguoiMoi} co nguoi moi minh
        //   44 C->S {int idNguoiMoi} nhan loi moi
        //   37 S->C {UTF tenDoiPhuong} mo khung
        //   45 C->S {int xu, byte n, n x byte oTui} dat do + khoa
        //      S->C {int xu, byte n, n x (short tpl, [byte upg neu body/ngoc kham], bool han, short sl)} doi phuong khoa
        //   46 hai chieu, rong: dong y
        //   56 C->S rong: tu choi loi moi
        //   57 hai chieu, rong: huy / don phien (client goc gui 57 ca sau 58)
        //   58 S->C {int xuMoi}: xong
        public const sbyte TRADE_OPEN_UI = 37;
        public const sbyte TRADE_INVITE = 43;
        public const sbyte TRADE_INVITE_ACCEPT = 44;
        public const sbyte TRADE_LOCK_ITEM = 45;
        public const sbyte TRADE_ACCEPT = 46;
        public const sbyte TRADE_INVITE_CANCEL = 56;
        public const sbyte TRADE_CANCEL = 57;
        public const sbyte TRADE_OK = 58;

        // Zone & Map
        public const sbyte CHANGE_ZONE = 28;
        public const sbyte OPEN_ZONE_LIST = 36;

        // NPC
        public const sbyte NPC_SELECT_MENU = 29;
        public const sbyte NPC_MENU_ID = 34;
        public const sbyte NPC_OPEN_MENU = 40;
        // GUI getTask(npc, menu): [47][byte npc][byte menu]. Dung khi ROI NHA THI DAU (map 0/56/73).
        // Nguon: ZangVPS ad_0.java:2334-2343 (2 byte, cung dong client J2ME voi ta). NINJAPC 251 ghi
        // them byte thu 3 (Service.cs:1402). CHUA do hex tren server nay.
        public const sbyte GET_TASK = 47;

        // ===== DANH VONG (clone MODGAME AutoDanhVong) =====
        // Xac nhan mot hop thoai do server mo: GUI [107][byte id]. Danh Vong dung id = 8 de HUY
        // nhiem vu (KHONG di duong cmd 29 - xem DANH_VONG.md §B.3; tai lieu upstream ghi
        // "sub 1 = huy" la SAI, sub 1 la TRA nhiem vu).
        public const sbyte UI_CONFIRM_ID = 107;
        // NHAN: TEXT NHIEM VU. Wire: UTF text1; NEU text1 != "typemoi" thi doc them UTF text2.
        // Toan bo noi dung nhiem vu danh vong nam trong text2 (tach dong bang "\n").
        public const sbyte QUEST_TEXT = 53;
        // NHAN: DANH SACH MOT BANG SHOP (tra loi sub -103 requestItem(typeUI)).
        //   byte typeUI, byte n, n x { ubyte indexUI, short templateId }.
        public const sbyte SHOP_ITEM_LIST = 33;
        // NHAN: danh sach muc menu NPC dang chu (lap UTF toi het goi).
        // ⚠️ Tren server NAY danh sach menu ve theo cmd 40 (MapHandler.HandleNpcOpenMenuResponse)
        // chu KHONG phai 63 nhu MODGAME. Giu hang so de nhan dien neu server co gui 63.
        public const sbyte NPC_MENU_LIST = 63;
        // NHAN: NPC noi. Wire: short npcTemplateId, UTF text.
        // Danh Vong dung de bat cau NPC 5 xac nhan "noi tro ve khi bi trong thuong".
        public const sbyte NPC_SAY = 38;

        // ===== LOI DAI (clone MODGAME AutoLoiDaiWin / AutoLoiDaiLose) =====
        // GUI: [92][short id][UTF text]. id 2 = TEN doi thu, id 3 = XU CUOC (chuoi thap phan).
        // Ca 3 auto ben MODGAME deu gui 92 mà KHONG mo menu NPC truoc - chi can dung DUNG toa do NPC 0.
        public const sbyte TEXT_BOX_ID = 92;
        // Hai chieu. NHAN [99][int charID nguoi moi] = "muon thach dau voi ban o loi dai";
        // acc phu tra loi bang chinh cmd 99 kem charID do.
        // ⚠️ KHONG phai cmd 106 - 106 la thach dau GIA TOC. Port nham opcode nay tung lam
        // acc phu ben MODGAME khong bao gio nhan duoc loi moi (DANH_VONG.md §O.8.1).
        public const sbyte ACCEPT_INVITE_DUEL = 99;
        // NHAN: moi TY THI thuong (khac loi dai). int charID.
        public const sbyte INVITE_TY_THI = 65;
        // NHAN: bat dau so tai. int charA, int charB.
        public const sbyte DUEL_START = 66;
        // NHAN: ket thuc so tai. int idThua, int idThang, [int hp - TRUONG TUY CHON].
        // hp > 0 => idThua thua that su; hp <= 0 hoac vang => hoa / het gio.
        // ⚠️ CHUA XAC MINH 66/67 co phat cho tran trong map 111 hay khong.
        public const sbyte DUEL_END = 67;

        // Skills
        public const sbyte SELECT_SKILL = 41;
        public const sbyte USE_SKILL_BUFF = 74;

        // Player position
        public const sbyte SERVER_SET_POS = 52;

        // Combat
        public const sbyte ATTACK_MOB = 60;
        public const sbyte ATTACK_CHAR = 61;

        // CUNG HAI BYTE 60/61 nhung CHIEU NGUOC LAI: server bao "nguoi choi KHAC vua dung chieu".
        // Dat ten rieng de doc switch cua MessageRouter khong tuong la goi minh gui.
        //   60: int charId ; byte skillTemplateId ; ubyte mobId  x N   (toi da 10, doc toi het goi)
        //   61: int charId ; byte skillTemplateId ; int   charId x N
        // Nguon: MODGAME Controller.java:1589-1706 · NSOPC Controller.cs:2629-2774.
        // Thuan trang tri (hoat canh danh cua cua so "Xem game"), khong doi trang thai game.
        public const sbyte OTHER_CAST_MOB = 60;
        public const sbyte OTHER_CAST_CHAR = 61;
        public const sbyte PLAYER_HP_CHANGE = 62;

        // Misc
        public const sbyte PLAYER_REVIVE = 88;

        // Bang "Thong tin" nhan vat (2 trang trong game). Hai chieu, wire XAC NHAN BOI 2 NGUON
        // DOC LAP (NinjaSchool_251_src Controller.cs case 93/101 + MODGAME Controller.java case
        // 93/101 - giong nhau tung field):
        //   GUI: [93][UTF ten nhan vat]  (Service.viewInfo - hoi duoc CA nhan vat khac)
        //   NHAN 93: int charId + UTF ten + short head + byte gender + byte class + byte pk
        //            + int hp,maxHp,mp,maxMp + byte speed + short resFire,resIce,resWind
        //            + int dame,dameDown + short exactly,miss,fatal,reactDame,sysUp,sysDown
        //            + ubyte level + short diemHoatDong + UTF clan [+ byte typeClan neu clan != ""]
        //            + short diemHoatDong(lan 2, GHI DE) + 10 short danh vong
        //            + 5 byte gioi han ngay + 2 x 16 mon trang bi (ta KHONG doc phan nay).
        //   NHAN 101: int diemTinhTu + byte banhPhongLoi + byte banhBangHoa (goi RIENG, ve sau 93).
        public const sbyte CHAR_VIEW_INFO = 93;
        public const sbyte CHAR_VIEW_INFO_EXTRA = 101;

        /// <summary>
        /// cmd 108: thao 1 o THU CUOI ve tui — `byte indexUI (0..4)`.
        /// O 4 = chinh con thu (thao = thoi cuoi), KHONG bi loai tru — xem ItemService.
        /// ⚠️ KHAC cmd 15 (thao trang bi thuong). Hai nguon khop:
        /// NinjaSchool_251_src Service.cs:595 (itemMonToBag) + MODGAME Service.java:273.
        /// </summary>
        public const sbyte ITEM_MON_TO_BAG = 108;

        /// <summary>
        /// cmd 112 — chieu NHAN: doi CAP NANG CAP cua 1 mon trong tui tai cho
        /// (`NinjaSchool_251_src/Controller.cs:1135`): <c>byte slot, byte upgrade</c>,
        /// kem <c>expires = 0</c> (het han bi xoa). Khong doi o, khong doi so luong.
        /// </summary>
        public const sbyte ITEM_UPGRADE_CHANGE = 112;
    }
}
