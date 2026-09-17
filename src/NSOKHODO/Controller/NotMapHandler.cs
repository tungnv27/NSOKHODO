using System;
using System.Collections.Generic;
using System.Text;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.GameData;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    public class NotMapHandler
    {
        public GameStateManager State { get; private set; }
        public string[] LastCharList { get; private set; }
        public byte[] LastCharLevels { get; private set; }
        public byte[] DataVersions { get; private set; }

        // Sub cua NOT_MAP da tung gap ma khong co case -> chi log lan dau moi loai.
        private static readonly HashSet<sbyte> _subSeen = new HashSet<sbyte>();

        public NotMapHandler(GameStateManager state)
        {
            State = state;
        }

        public void Handle(sbyte sub, NsoMessage msg)
        {
            switch (sub)
            {
                case SubCmd.NotMap.DATA_VERSION:
                    HandleDataVersion(msg);
                    break;

                case SubCmd.NotMap.CHAR_LIST:
                    HandleCharList(msg);
                    break;

                case SubCmd.NotMap.UPDATE_DATA:
                    HandleUpdateData(msg);
                    break;

                case SubCmd.NotMap.UPDATE_MAP:
                    HandleUpdateMap(msg);
                    break;

                case SubCmd.NotMap.UPDATE_SKILL:
                    HandleUpdateSkill(msg);
                    break;

                case SubCmd.NotMap.UPDATE_ITEM:
                    HandleUpdateItem(msg);
                    break;

                case SubCmd.NotMap.REQUEST_ICON:
                    HandleIcon(msg);
                    break;

                case SubCmd.NotMap.MOB_TEMPLATE:
                    HandleMobTemplate(msg);
                    break;

                case SubCmd.NotMap.CLEAR_TASK:
                    // Huy NV chinh tuyen: xoa NV, KHONG doi ctaskId (khac cmd 49). Client con xoa do
                    // nhiem vu type 23/24/25 khoi tui - ta KHONG lam: server se dong bo tui rieng.
                    if (State.MyChar != null) State.MyChar.MainTask = null;
                    State.RaiseDebugLog("[Task] Server huy NV chinh tuyen (sub -98)");
                    break;

                default:
                    // Log MOI sub chua xu ly (moi loai 1 lan). Truoc day nhanh nay IM LANG hoan
                    // toan, nen khi xin anh icon ma khong thay hoi am thi khong biet server co tra
                    // loi bang sub khac hay khong tra loi gi ca - hai chuyen can hai cach sua khac
                    // han. Xem docs/features/ICON_HANH_TRANG.md §4.5.
                    lock (_subSeen)
                    {
                        if (_subSeen.Add(sub))
                            State.RaiseDebugLog(string.Format(
                                "[Server] NOT_MAP sub CHUA XU LY={0} len={1}", sub, msg.DataLength));
                    }
                    break;
            }
        }

        /// <summary>
        /// Ảnh rời của 1 sprite. <b>Bản này KHÔNG dùng đồ hoạ</b> nên chỉ ghi log rồi bỏ gói.
        ///
        /// <para>Giữ lại hàm thay vì xoá case: bot không bao giờ <i>xin</i> ảnh, nhưng nếu một ngày
        /// server tự đẩy thì gói vẫn được tiêu thụ gọn thay vì rơi xuống nhánh "sub lạ" và làm bẩn
        /// log chẩn đoán.</para>
        /// </summary>
        private void HandleIcon(NsoMessage msg)
        {
            try
            {
                int id = msg.Reader.ReadInt();
                int len = msg.Reader.ReadInt();
                State.RaiseDebugLog(string.Format(
                    "[Icon] Bo qua anh sprite {0} ({1} byte) - ban nay khong dung do hoa", id, len));
            }
            catch { }
        }

        /// <summary>
        /// Ảnh của một loại quái. <b>Bản này KHÔNG dùng đồ hoạ</b> - chỉ ghi log rồi bỏ gói.
        /// Cùng lý do với <see cref="HandleIcon"/>.
        /// </summary>
        private void HandleMobTemplate(NsoMessage msg)
        {
            try
            {
                int tpl = msg.Reader.ReadShort();
                State.RaiseDebugLog(string.Format(
                    "[MobImg] Bo qua anh quai tpl {0} (goi dai {1} byte)", tpl, msg.DataLength));
            }
            catch { }
        }

        private void HandleDataVersion(NsoMessage msg)
        {
            DataVersions = new byte[4];
            for (int i = 0; i < 4; i++)
                DataVersions[i] = msg.Reader.ReadByte();
            State.DataSyncVersions = DataVersions;
        }

        private void HandleCharList(NsoMessage msg)
        {
            try
            {
                int count = msg.Reader.ReadUnsignedByte();
                var names = new List<string>();
                var levels = new List<byte>();
                for (int i = 0; i < count; i++)
                {
                    msg.Reader.ReadByte();       // gender
                    string name = msg.Reader.ReadUTF();  // char name
                    msg.Reader.ReadUTF();        // class name (phai)
                    byte charLevel = msg.Reader.ReadByte(); // level
                    msg.Reader.ReadShort();      // partHead
                    msg.Reader.ReadShort();      // partWp
                    msg.Reader.ReadShort();      // partBody
                    msg.Reader.ReadShort();      // partLeg
                    names.Add(name);
                    levels.Add(charLevel);
                }
                LastCharList = names.ToArray();
                LastCharLevels = levels.ToArray();
            }
            catch
            {
                LastCharList = new string[0];
            }
        }

        /// <summary>Nguyên khối byte bảng <c>nj_image</c> server gửi lần đăng nhập này (null nếu chưa có).</summary>
        public byte[] NjImageRaw { get; private set; }
        /// <summary>Số ô trong bảng <c>nj_image</c> SỐNG của server. 0 = chưa biết.</summary>
        public int NjImageCount { get; private set; }
        /// <summary>Nguyên khối byte bảng <c>nj_part</c>.</summary>
        public byte[] NjPartRaw { get; private set; }
        /// <summary>Số part trong bảng <c>nj_part</c> SỐNG của server. 0 = chưa biết.</summary>
        public int NjPartCount { get; private set; }

        /// <summary>Cả 5 bảng đồ hoạ đều mở đầu bằng <c>short count</c> (big-endian).</summary>
        private static int CountShortHeader(byte[] raw)
        {
            if (raw == null || raw.Length < 2) return 0;
            return ((raw[0] & 0xFF) << 8) | (raw[1] & 0xFF);
        }

        private void HandleUpdateData(NsoMessage msg)
        {
            // Clone block Controller.d() cua MODGAME (3110-3134) du de lay bang exps:
            //   byte dh | 5x byteArray(int len + bytes): nj_arrow/effect/image/part/skill
            //   df/dg: byte n -> moi: byte m -> m*2 byte
            //   exps: ubyte count -> count * long
            // Truoc day chi set co; nay parse them exps de hien EXP% + tinh level chuan.
            try
            {
                var r = msg.Reader;
                int dh = r.ReadByte(); // dh
                // 5 bang do hoa: nj_arrow(0) · nj_effect(1) · nj_image(2) · nj_part(3) · nj_skill(4).
                // Truoc day DOC ROI VUT sach ca 5 - trong khi server GUI SAN moi lan dang nhap, khong
                // ton them goi tin nao. Nay giu lai NGUYEN KHOI byte cua nj_image/nj_part de biet
                // bang SONG cua server hom nay (ban nhung trong EXE la ban DONG BANG, da lech: item
                // "Hoa sen trang" dung icon 3197 > 3.191 o cua bang cu).
                // Chi GIU BYTE + dem so muc, KHONG thay bang dang dung -> khong doi hanh vi ve.
                // Wire: SERVER_FACTS §19.1c. Xem docs/features/XEM_GAME_PC.md (Dot 1).
                for (int k = 0; k < 5; k++)
                {
                    int len = r.ReadInt();
                    if (len < 0 || len > 8_000_000) throw new Exception("byteArray#" + k + " len=" + len);
                    if (len <= 0) continue;
                    var raw = r.ReadFully(len);
                    if (k == 2) { NjImageRaw = raw; NjImageCount = CountShortHeader(raw); }
                    else if (k == 3) { NjPartRaw = raw; NjPartCount = CountShortHeader(raw); }
                }
                // Bang NPC/map tung buoc nhiem vu chinh tuyen (GameScr.tasks/mapTasks). Truoc day doc
                // roi vut; nay giu lai cho khoi "Nhiem vu" tab Tong quan. So byte doc KHONG doi.
                int n = r.ReadUnsignedByte();
                var taskNpcs = new sbyte[n][];
                var taskMaps = new sbyte[n][];
                for (int i = 0; i < n; i++)
                {
                    int m = r.ReadUnsignedByte();
                    taskNpcs[i] = new sbyte[m];
                    taskMaps[i] = new sbyte[m];
                    for (int j = 0; j < m; j++)
                    {
                        taskNpcs[i][j] = r.ReadSignedByte();
                        taskMaps[i][j] = r.ReadSignedByte();
                    }
                }
                // Bang nhiem vu: VAN PHAI DOC het byte o tren de khong lech dong du lieu,
                // nhung ban nay khong lam nhiem vu nen khong luu lai.
                int expCount = r.ReadUnsignedByte();
                // PHAI doc het expCount long de KHONG desync stream (con parse tiep toi effTemplates).
                var exps = new long[expCount];
                bool expOk = expCount > 0 && expCount <= 250;
                for (int i = 0; i < expCount; i++)
                {
                    exps[i] = r.ReadLong();
                    if (exps[i] < 0) expOk = false; // chi loai gia tri AM (exps[0]=0 hop le)
                }
                if (expOk) GameData.ExpTable.SetTable(exps);

                // Sau exps: 10 mang int - moi mang = byte count + count*int - roi toi effTemplates.
                // Thu tu (ten GOC tu NinjaSchool_251_src Controller.createData; trong ngoac la ten
                // obfuscate cua MODGAME):
                //   crystals(cn) upClothe(co) upAdorn(cp) upWeapon(cq) coinUpCrystals(cr)
                //   coinUpClothes(cs) coinUpAdorns(ct) coinUpWeapons(cu) goldUps(cw) maxPercents(cv)
                // !! goldUps doc TRUOC maxPercents (nguoc bang chu cai) - de dao nham.
                // Truoc day ta ReadFully bo qua ca 10; nay LUU lai de tinh % dap do.
                var upTables = new int[10][];
                for (int a = 0; a < 10; a++)
                {
                    int c = r.ReadUnsignedByte();
                    var arr = new int[c];
                    for (int i = 0; i < c; i++) arr[i] = r.ReadInt();
                    upTables[a] = arr;
                }
                // Bang nang cap: cung ly do voi bang nhiem vu - doc de khong lech, khong luu.

                // effTemplates: byte count + moi { byte id, byte type, UTF name, short iconId }.
                int effCount = r.ReadUnsignedByte();
                var typesById = new int[256];
                var namesById = new string[256];
                var iconsById = new int[256];
                for (int i = 0; i < typesById.Length; i++) { typesById[i] = -1; iconsById[i] = -1; }
                int nFood = 0; string foodIds = "";
                for (int i = 0; i < effCount; i++)
                {
                    int id = r.ReadUnsignedByte();
                    int type = r.ReadByte();
                    string name = r.ReadUTF();   // truoc day doc roi vut - nay GIU de hien tren "Xem game"
                    // iconId = chi so SmallImage (EffectTemplate.iconId, MODGAME GameScr.java:5332
                    // ve bang SmallImage.drawSmallImageNew). Truoc day vut vi tuong phai xin server;
                    // that ra icon hieu ung nam trong dai id THAP (<1200) von co san trong atlas.
                    int icon = r.ReadShort();
                    if (id >= 0 && id < typesById.Length)
                    {
                        typesById[id] = type;
                        namesById[id] = name;
                        iconsById[id] = icon;
                        if (type == 0) { nFood++; foodIds += (foodIds.Length > 0 ? "," : "") + id; }
                    }
                }
                State.SetEffectTypes(typesById);
                State.SetEffectNames(namesById);
                State.SetEffectIcons(iconsById);
                // In ca dai iconId: "Xem game" ve icon hieu ung bang chinh so nay (SmallImage id).
                // Bang sprite dong kem chi phu id THAP; id cao phai xin server (XEM_GAME.md §13.10)
                // -> dong log nay la cach duy nhat biet co can xin hay khong ma khong phai doan.
                int icMin = int.MaxValue, icMax = -1, icCo = 0;
                for (int i = 0; i < iconsById.Length; i++)
                {
                    if (iconsById[i] < 0) continue;
                    icCo++;
                    if (iconsById[i] < icMin) icMin = iconsById[i];
                    if (iconsById[i] > icMax) icMax = iconsById[i];
                }
                State.RaiseDebugLog(string.Format(
                    "[Auto] EffTemplate: {0} loai, thuc an(type0) ids=[{1}], ten id31={2}, icon: {3} cai, id {4}..{5}",
                    effCount, foodIds, State.GetEffectName(31) ?? "(khong co)",
                    icCo, icCo > 0 ? icMin.ToString() : "-", icCo > 0 ? icMax.ToString() : "-"));
            }
            catch (Exception ex)
            {
                // TRUOC DAY LA catch{} TRONG. Ca khoi parse nay doc TUAN TU mot stream: chi can mot
                // buoc truoc lech la moi thu sau mat sach - ke ca bang TEN HIEU UNG - ma KHONG de lai
                // dau vet nao. Do la ly do man hinh chi hien "Hieu ung #31" thay vi ten that.
                // Log day du de mot lan login la biet hong o dau.
                State.RaiseDebugLog(string.Format(
                    "[Auto] LOI parse DataSync 'data': {0}. HE QUA: mat bang effTemplate (ten + type "
                    + "hieu ung) va bang nang cap -> hieu ung se hien '#id' thay vi ten, auto-thuc-an "
                    + "phai chay theo dong ho thay vi theo type.", ex.Message));
            }

            State.DataSyncDone[0] = true;
        }

        private void HandleUpdateMap(NsoMessage msg)
        {
            try
            {
                DataSyncParser.ParseMapData(msg.Reader, State.MapStore, State.MobStore, State.NpcStore);
            }
            catch { }
            State.DataSyncDone[1] = true;
        }

        private void HandleUpdateSkill(NsoMessage msg)
        {
            try
            {
                DataSyncParser.ParseSkillData(msg.Reader, State.SkillStore);
            }
            catch { }
            State.DataSyncDone[2] = true;
        }

        private void HandleUpdateItem(NsoMessage msg)
        {
            try
            {
                DataSyncParser.ParseItemData(msg.Reader, State.ItemStore);
            }
            catch { }
            State.DataSyncDone[3] = true;
        }
    }
}
