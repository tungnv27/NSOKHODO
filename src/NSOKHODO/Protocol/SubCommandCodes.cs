namespace NSOKHODO.Protocol
{
    public static class SubCmd
    {
        // cmd=-29 (NOT_LOGIN) sub-commands
        public static class NotLogin
        {
            public const sbyte LOGIN = -127;
            public const sbyte SET_CLIENT_TYPE = -125;
            public const sbyte REGISTER = -126;
            public const sbyte LOGIN_RESULT = -124;
        }

        // cmd=-28 (NOT_MAP) sub-commands
        public static class NotMap
        {
            public const sbyte CHAR_LIST = -126;
            public const sbyte CREATE_CHAR = -125;
            public const sbyte DATA_VERSION = -123;
            public const sbyte UPDATE_DATA = -122;
            public const sbyte UPDATE_MAP = -121;
            public const sbyte UPDATE_SKILL = -120;
            public const sbyte UPDATE_ITEM = -119;
            /// <summary>
            /// GUI: TACH CHONG <c>{byte oTui, int soLuong}</c> (NINJAPC Cmd.cs:125 ITEM_SPLIT,
            /// Service.inputNumSplit). KHAC HAN cmd 22 (tach TRANG BI). Client goc bo qua goi -85
            /// chieu nhan (Controller.cs:4331).
            /// </summary>
            public const sbyte ITEM_SPLIT = -85;
            // Xin/nhan ANH ROI cua 1 sprite khong nam trong atlas (clone Service.requestIcon).
            // Gui: int spriteId. Nhan: int spriteId + int len + byte[len] PNG.
            // Xem SERVER_FACTS.md muc 3 va docs/features/ICON_HANH_TRANG.md.
            public const sbyte REQUEST_ICON = -115;
            // Xin/nhan ANH QUAI theo templateId. Gui: byte templateId (writeByte cua MODGAME la DUNG,
            // writeShort cua ban 251 KHONG co tac dung tren server nay). Nhan: xem SERVER_FACTS §19.2.
            // ⚠ Chi tra loi khi setClientType khai graphicsMode != 0 (§19.1).
            public const sbyte MOB_TEMPLATE = -108;
            public const sbyte CLIENT_OK = -101;
            // Huy nhiem vu chinh tuyen: khong payload, Char.clearTask() va KHONG doi ctaskId
            // (NinjaSchool_251_src Controller.cs:3689). ⚠ -98 trong nhom -30 la PLAYER_ADD_EFFECT.
            public const sbyte CLEAR_TASK = -98;
            public const sbyte ADMIN_CMD = -78;
        }

        // cmd=-30 (SUB_COMMAND) sub-commands
        public static class Sub
        {
            // Doi che do PK (MODGAME Service.changePk): sub -93 + byte typePk (0=hoa binh, 1, 3=PK tat ca).
            public const sbyte CHANGE_PK = -93;
            // Broadcast: 1 char (bat ky) DOI co PK -> int charId + byte cTypePk. MODGAME messageSubCommand
            // case -92 (Controller.java:4963). CAN cho Danh PK: target bat PK SAU khi vao map thi phai
            // cap nhat cTypePk cua ho (readCharInfo chi doc luc PLAYER_ADD).
            public const sbyte PK_TYPE_UPDATE = -92;
            public const sbyte MY_CHAR_INFO = -127;
            // sub 115: "UPDATE INFO ME" - server re-gui TOAN BO thong tin nhan vat (gom tui/body).
            // Server NAY gui goi nay sau khi Sap xep tui -> phai parse de UI cap nhat. (MODGAME case 115)
            public const sbyte UPDATE_INFO_ME = 115;
            public const sbyte STAT_UPDATE = -128;

            // ⚠️ -126 KHONG PHAI "len cap" (ten cu LEVEL_UP la SAI, sua 2026-09-07).
            // Hai nguon doc lap deu noi -126 = ME_LOAD_CLASS (DOI LOP), mo dau bang readParam
            // (byte cspeed, int cMaxHP, int cMaxMP): NinjaSchool_251_src Controller.cs:4398 +
            // MODGAME Controller.java:3991 (trong messageSubCommand, ham bat dau :3759, chi co
            // DUNG MOT switch o :3780 nen khong the doc nham ngu canh).
            // Ban cu doc 1 byte gan thang vao Level => moi lan doi lop, Level bi gan bang TOC DO.
            // "Len cap" that la -124 (ME_LOAD_LEVEL) - truoc day KHONG AI xu ly, ma do chinh la luc
            // sPoint/pPoint tang => tab Ky nang/Tiem nang se hien so cu cho toi lan login sau.
            public const sbyte ME_LOAD_CLASS = -126;   // doi lop
            public const sbyte ME_LOAD_LEVEL = -124;   // len cap
            public const sbyte ME_LOAD_SKILL = -125;   // tra loi hoc/nang ky nang
            public const sbyte POTENTIAL_UP = -109;    // tra loi cong diem tiem nang (VA la ma GUI)
            public const sbyte SKILL_UP = -108;        // ma GUI: nang/hoc ky nang
            // Thu cuoi: 5 o (4 trang suc + con thu). Hai nguon khop tung byte:
            // NinjaSchool_251_src Controller.cs:5577 + MODGAME Controller.java:5049.
            public const sbyte LOAD_THU_CUOI = -54;

            public const sbyte RESOURCE_UPDATE = -123;
            public const sbyte HP_UPDATE = -122;
            public const sbyte MP_UPDATE = -121;
            public const sbyte SPEED_UPDATE = -120;
            // He hieu ung (clone MODGAME Controller.messageSubCommand case -101/-100/-99):
            //   -101 = THEM effect moi vao vEff  (byte id, int elapsedSec, int durationMs, short param)
            //   -100 = REFRESH effect cung id    (cung wire nhu -101) - KHONG phai remove
            //   -99  = REMOVE effect theo id     (byte id)
            // Ban cu gan nham EFFECT_REMOVE=-100 (that ra la refresh) + bo sot -99 -> hieu ung khong
            // bao gio bi go. Da sua dung + them luoi het-han cuc bo (Effect.ExpiresAt).
            public const sbyte EFFECT_ADD = -101;
            public const sbyte EFFECT_REFRESH = -100;
            public const sbyte EFFECT_REMOVE = -99;

            // Karma (cPk / diem hieu chien) cap nhat luc PK. MODGAME Controller case -117/-81 nam DUOI
            // cmd -30 (messageSubCommand), KHONG phai top-level. -117 = "Diem hieu chien hien tai la X";
            // -81 con clear charFocus (bo qua o headless). Doc 1 byte = cPk moi.
            public const sbyte PK_POINT_UPDATE = -117;
            public const sbyte PK_POINT_CLEAR_FOCUS = -81;

            // Party sub-commands
            public const sbyte CREATE_PARTY = -88;
            public const sbyte CHANGE_LEADER = -87;
            public const sbyte KICK_MEMBER = -86;
            public const sbyte FIND_PARTY = -77;
            public const sbyte LOCK_PARTY = -76;

            // Item sub-commands
            // Xin danh sach 1 kho item theo typeUI (MODGAME Service.requestItem): 4 = RUONG.
            // Server tra ve bang cmd 31 (BOX_ITEM_LIST).
            public const sbyte REQUEST_ITEM = -103;
            public const sbyte BOX_IN = -105;
            public const sbyte BOX_OUT = -104;
            public const sbyte SORT_BAG = -107;   // gui: sap xep tui (Service 251 bagSort)
            public const sbyte SORT_BOX = -106;   // gui: sap xep hop (Service 251 boxSort)

            // ===== Bon sub CHIEU NHAN lam DOI TUI ma truoc 2026-09-12 ta khong he doc =====
            // (xem docs/features/GIAO_TIEP_VAT_PHAM.md §3 - day la mot trong cac goc re cua
            //  "dung vat pham xong no van con trong hanh trang").

            /// <summary>
            /// sub -102: dung SACH KY NANG xong (`NinjaSchool_251_src/Controller.cs:4500`):
            /// <c>byte slot, short skillId</c> → <b>xoa han o tui</b> + hoc chieu do.
            /// Server KHONG gui kem cmd 10/18 cho ca nay.
            /// </summary>
            public const sbyte USE_BOOK_SKILL = -102;

            /// <summary>
            /// sub -91: MO RONG TUI (`Controller.cs:5750`): <c>ubyte soOMoi, ubyte slotXoa</c>.
            /// Cap lai mang tui theo size moi (giu item cu) roi xoa o chua chinh mon mo rong.
            /// </summary>
            public const sbyte BAG_EXPAND = -91;

            /// <summary>sub -80: xoa 1 o TRANG BI (`Controller.cs:5747`): <c>byte slot</c>.</summary>
            public const sbyte BODY_ITEM_CLEAR = -80;

            /// <summary>sub -75: xoa 1 o RUONG (`Controller.cs:5741`): <c>byte slot</c>.</summary>
            public const sbyte BOX_ITEM_CLEAR = -75;

            // Buff/hoi sinh xa (phai Quat): buffLive(charId) = hoi sinh 1 member tu xa. MODGAME 251
            // Service.buffLive -> messageSubCommand(-79) + writeInt(charId). ⚠️ CHUA VERIFY HEX tren
            // server nay (co the lech nhu vut do/sap xep tui - xem SERVER_FACTS §4). Coi la TAM.
            public const sbyte BUFF_LIVE = -79;

            // Misc
            public const sbyte VIEW_INFO = -110;
            public const sbyte CLAN_INFO = -95;

            // ⚠️ CUNG BYTE -95, HAI NGHIA KHAC NHAU O HAI DISPATCHER KHAC NHAU (da doi chieu
            // thang source MODGAME 2026-09-08 - cho nay rat de tra nham):
            //   * Controller.loadInfoMap()      sub -95 -> Char.be.alert = readUTF()  (bao GIA TOC)
            //   * Controller.messageSubCommand() sub -95 -> GameScr.dx = readInt();
            //                                               GameScr.dy = now/1000     (DONG HO TRAN)
            // SubCommandHandler cua ta la ban sao cua messageSubCommand (cmd -30), nen o day
            // -95 = DONG HO TRAN. Ta chua co doi tuong gia toc nen ten CLAN_INFO chi la hang so
            // cho, khong co dong code nao dung - chan -95 cho loi dai la vo hai.
            // cmd -16 (bat dau doi map) xoa ca dx lan dy ve 0.
            public const sbyte DUEL_CLOCK = -95;
        }
    }
}
