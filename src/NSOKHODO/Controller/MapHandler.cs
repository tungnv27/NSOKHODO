using System;
using System.Collections.Generic;
using System.Threading;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.GameData;
using NSOKHODO.Models;

namespace NSOKHODO.Controller
{
    public class MapHandler
    {
        public GameStateManager State { get; private set; }

        /// <summary>
        /// Map change event - signaled when MAP_INFO is received.
        /// Equivalent to TileMap.i() notify() in game source.
        /// Per-instance (not static) so 18 accounts don't interfere with each other.
        /// </summary>
        public readonly ManualResetEvent MapChangeEvent = new ManualResetEvent(false);

        /// <summary>
        /// NPC menu event - signaled when cmd=40 (openMenu response) arrives.
        /// </summary>
        public readonly ManualResetEvent NpcMenuEvent = new ManualResetEvent(false);

        /// <summary>
        /// Menu options từ server (cmd=40). Index trong list này = menuId cần gửi qua SendNpcMenu.
        /// </summary>
        public readonly List<string> NpcMenuOptions = new List<string>();

        // Chan spam log lop phu "o nen server bom" (xem cuoi HandleMapInfo). Chi log lai khi doi
        // map hoac khi so o thay doi - khong log moi lan vao lai cung mot khu.
        private int _lastOverlayMapId = -1;
        private int _lastOverlayCount = -1;

        public MapHandler(GameStateManager state)
        {
            State = state;
        }

        /// <summary>
        /// Tra 2 handle kernel cua ManualResetEvent. MapHandler duoc tao lai MOI lan InitSession
        /// (tuc moi lan reconnect); khong tra thi handle cu dong lai theo so lan reconnect x so account.
        /// </summary>
        public void Dispose()
        {
            try { MapChangeEvent.Close(); } catch { }
            try { NpcMenuEvent.Close(); } catch { }
        }

        /// <summary>
        /// Phan loai Tinh Anh/Thu Linh theo maxHp - CLONE chinh xac MODGAME Auto.a(Mob) (Auto.java:41-58):
        ///   maxHp == 10  x tpl.hp  -> Tinh Anh (levelBoss 1)
        ///   maxHp == 100 x tpl.hp  -> Thu Linh (levelBoss 2)
        /// Server private NAY KHONG danh dau elite qua field levelBoss cua packet (MAP_INFO=0, cmd -1
        /// khong co levelBoss, cmd -5 gui 0) - giong client goc: TU TINH tu maxHp so HP-goc-template
        /// (tpl.hp = arrMobTemplate[id].hp tu DataSync, doc dung sau khi fix mobCount=ubyte).
        /// Truoc day heuristic nay "phantom" la vi tpl.Hp bi RAC (DataSync doc mobCount = short -> lech
        /// 1 byte -> moi template hong) - bug do da fix, nen heuristic gio chuan.
        /// Chi xet khi chua phai boss/elite (levelBoss==0, !isBoss); maxHp == HP-goc -> quai thuong.
        /// Goi tu ca MAP_INFO va cmd -5 respawn (respawn reset maxHp + ghi de levelBoss=0).
        /// </summary>
        public static void ClassifyElite(MobState mob, MobTemplateStore store)
        {
            if (mob == null || store == null || mob.IsBoss || mob.LevelBoss != 0) return;
            // MODGAME Auto.a dong 42: chi phan loai khi status != 0 (&& levelBoss != 3 && maxHp != d().hp).
            // THIEU chot nay -> mob "status==0" (bong ma luc chuyen map, vi du tpl=116 hien 1 tick roi
            // bien mat) bi nhan nham la Tinh Anh/Thu Linh. MODGAME bo qua chung.
            if (mob.Status == 0) return;
            var tpl = store.Get(mob.TemplateId);
            if (tpl == null || tpl.Hp <= 0) return;
            long max = mob.MaxHp;
            if (max == tpl.Hp) return;                  // maxHp == HP goc -> quai thuong
            if (max == 10L * tpl.Hp) mob.LevelBoss = 1; // Tinh Anh
            else if (max == 100L * tpl.Hp) mob.LevelBoss = 2; // Thu Linh
        }

        /// <summary>
        /// Game Controller.cs:1440 case 40 - parse openMenu response: list of UTF strings
        /// (mỗi string là caption của 1 menu option do server gửi).
        /// </summary>
        public void HandleNpcOpenMenuResponse(NsoMessage msg)
        {
            lock (NpcMenuOptions)
            {
                NpcMenuOptions.Clear();
                try
                {
                    while (true)
                    {
                        NpcMenuOptions.Add(msg.Reader.ReadUTF());
                    }
                }
                catch { /* EOF expected when no more strings */ }
            }
            NpcMenuEvent.Set();
        }

        public void HandlePreMapChange(NsoMessage msg)
        {
            State.CurrentMap.Reset();
        }


        /// <summary>
        /// Parse MAP_INFO (cmd=-18) exactly matching source code:
        /// Controller.java line 331+ and loadInfoMap()
        /// </summary>
        public void HandleMapInfo(NsoMessage msg)
        {
            var r = msg.Reader;
            var map = State.CurrentMap;
            map.Reset();

            // Header
            map.MapId = r.ReadUnsignedByte();
            map.TileId = r.ReadSignedByte();
            map.BgId = r.ReadSignedByte();
            map.TypeMap = r.ReadByte();

            // Load tile collision data (like game's loadMapFromResource + a(tileId))
            int actualTileId = map.TileId < 0 ? (map.TileId + 256) : map.TileId; // unsigned
            State.Tiles.LoadMap(map.MapId, actualTileId);
            map.MapName = r.ReadUTF();
            map.ZoneId = r.ReadByte();

            // loadInfoMap() - Player position
            State.MyChar.Cx = r.ReadShort();
            State.MyChar.Cy = r.ReadShort();
            // ec = j; ed = k (NSOTRUNGDUC Class_by.java:420-425) - xem MovementService.SyncLastSent.
            State.RaiseServerSetPosition(State.MyChar.Cx, State.MyChar.Cy);

            // Waypoints - doc het ra list tam roi nap MOT LAN trong khoa, de luong auto khong bao
            // gio nhin thay danh sach dang nap do dang (xem MapState.Reset).
            int wpCount = r.ReadByte() & 0xFF;
            var wpNew = new System.Collections.Generic.List<Waypoint>(wpCount);
            for (int i = 0; i < wpCount; i++)
            {
                var wp = new Waypoint();
                wp.MinX = r.ReadShort();
                wp.MinY = r.ReadShort();
                wp.MaxX = r.ReadShort();
                wp.MaxY = r.ReadShort();
                wpNew.Add(wp);
            }
            lock (map.Waypoints) map.Waypoints.AddRange(wpNew);

            // Mobs - exact format from movement_detail.md
            int mobCount = r.ReadByte() & 0xFF;
            map.Mobs = new MobState[mobCount];
            for (int i = 0; i < mobCount; i++)
            {
                var mob = new MobState();
                mob.Id = i;

                // Format THỰC TẾ của server này - xác nhận bằng hex dump 2026-06-13
                // (xem WORKLOG + hexmap.txt, decode khớp 489/489 byte):
                // 5 bool, templateId BYTE (client 217/251 là short - server này KHÁC),
                // sys byte, hp int, level ubyte, maxhp int, x short, y short,
                // status byte, levelBoss byte, isBos bool. Entry = 23 byte.
                bool isDisable = r.ReadBoolean();
                bool isDontMove = r.ReadBoolean();
                bool isFire = r.ReadBoolean();
                bool isIce = r.ReadBoolean();
                bool isWind = r.ReadBoolean();

                mob.TemplateId = (short)r.ReadUnsignedByte();
                mob.Sys = r.ReadByte();
                mob.Hp = r.ReadInt();
                mob.Level = (byte)r.ReadUnsignedByte();
                mob.MaxHp = r.ReadInt();
                mob.X = r.ReadShort();
                mob.Y = r.ReadShort();
                byte status = r.ReadByte();
                mob.Status = status;
                mob.LevelBoss = r.ReadByte();
                mob.IsBoss = r.ReadBoolean(); // boss thật (tpl=141 Rừng già) có LevelBoss=0 nhưng isBos=1

                // Phan loai Tinh Anh/Thu Linh theo maxHp (clone MODGAME Auto.a). Server nay gui
                // levelBoss=0 cho ca Tinh Anh/Thu Linh - client TU TINH theo maxHp/HP-goc-template.
                ClassifyElite(mob, State.MobStore);
                mob.IsActive = !isDisable && mob.Hp > 0;
                mob.IsDead = (mob.Hp <= 0);
                map.Mobs[i] = mob;
            }

            // Ghi lai level quai cua map nay vao bang tu hoc (GameData/MapLevels.cs) - o chon map
            // hien duoc nhan "Lv 92 - Lv 91". Chi ton dia lan DAU thay level moi cua map.
            NSOKHODO.GameData.MapLevels.Observe(map.MapId, map.Mobs);

            // BuNhin (mannequins) - skip
            try
            {
                int buNhinCount = r.ReadByte() & 0xFF;
                for (int i = 0; i < buNhinCount; i++)
                {
                    r.ReadUTF();    // name
                    r.ReadShort();  // x
                    r.ReadShort();  // y
                }
            }
            catch { }

            // NPCs - from Npc constructor: Npc(status, x, y, templateIndex)
            // new Npc(readByte(), readShort(), readShort(), readByte())
            try
            {
                int npcCount = r.ReadByte() & 0xFF;
                for (int i = 0; i < npcCount; i++)
                {
                    var npc = new NpcState();
                    npc.Status = r.ReadByte();           // var1 = statusMe
                    npc.X = r.ReadShort();               // var2 = cx
                    npc.Y = r.ReadShort();               // var3 = cy
                    npc.TemplateId = (short)(r.ReadByte() & 0xFF);  // var4 = template index
                    // Note: TemplateId here is arrNpcTemplate index,
                    // which equals npcTemplateId in NpcTemplate
                    map.Npcs.Add(npc);
                }
            }
            catch { }

            // Items on map
            try
            {
                int itemCount = r.ReadByte() & 0xFF;
                for (int i = 0; i < itemCount; i++)
                {
                    var item = new ItemOnMap();
                    item.ItemMapId = r.ReadShort();
                    item.TemplateId = r.ReadShort();
                    item.X = r.ReadShort();
                    item.Y = r.ReadShort();
                    map.AddItemOnMap(item); // dedupe theo itemMapId (clone MODGAME loadInfoMap)
                }
            }
            catch { }

            // Log NPCs for debugging
            if (map.Npcs.Count > 0)
            {
                var sb = new System.Text.StringBuilder("[Map] NPCs: ");
                foreach (var n in map.Npcs)
                    sb.AppendFormat("id={0}({1},{2}) ", n.TemplateId, n.X, n.Y);
                // Will be visible in log via router
            }

            // Map name override (optional)
            try { map.MapName = r.ReadUTF(); } catch { }

            // O NEN DO SERVER BOM ("location") - clone MODGAME Controller.loadInfoMap:3039-3046
            // + TileMap.a():359. Server dinh kem danh sach o tile ma no muon BIEN THANH NEN dung
            // duoc, bat ke o do la gi trong file .bin - vd tan CAY o map 139 (Quy Son): anh cay chi
            // la tile TRANG TRI khong dac, phan dung duoc do server bom. Truoc day ta doc dung so
            // byte roi VUT DI -> TryFindStandable tra false -> TrainMode bo sach quai dung tren cay.
            // Ap SAU khi LoadMap (dong 119) da dung xong bang va cham - dung nhu ban goc goi
            // TileMap.a(tileID) sau khi nap w (Controller.java:3051).
            try
            {
                int numLoc = r.ReadUnsignedByte();
                if (numLoc > 0)
                {
                    var cells = new byte[numLoc * 2];
                    for (int i = 0; i < numLoc; i++)
                    {
                        cells[i * 2] = (byte)r.ReadUnsignedByte();      // tileX (cot)
                        cells[i * 2 + 1] = (byte)r.ReadUnsignedByte();  // tileY (hang)
                    }

                    int applied = State.Tiles.ApplyGroundOverlay(cells, numLoc);

                    // Log MOT lan cho moi (map, so o) - tan sat doi khu lien tuc, khong duoc spam.
                    // CO Y khong loc "applied > 0": server gui o ma ap duoc 0 chinh la ca PARSE LECH
                    // can keu to nhat - loc di la tu bit mat.
                    if (map.MapId != _lastOverlayMapId || applied != _lastOverlayCount)
                    {
                        _lastOverlayMapId = map.MapId;
                        _lastOverlayCount = applied;
                        // Chuong bao PARSE LECH: byte doc duoc ma tro ra ngoai luoi o cua map thi
                        // gan nhu chac chan ta dang doc nham khoi khac -> bot se moc "be ma" giua
                        // troi. Ban goc khong kiem tra (tileY*width+tileX tu cuon vong), ta giu
                        // nguyen hanh vi nhung PHAI hien ra log chu khong im lang.
                        int wT = State.Tiles.WidthTiles, hT = State.Tiles.HeightTiles;
                        int outGrid = 0;
                        for (int i = 0; i < numLoc; i++)
                            if ((cells[i * 2] & 0xFF) >= wT || (cells[i * 2 + 1] & 0xFF) >= hT) outGrid++;

                        var sbLoc = new System.Text.StringBuilder();
                        sbLoc.AppendFormat("[Map] map {0}: server bom {1} o nen ->", map.MapId, applied);
                        for (int i = 0; i < numLoc && i < 8; i++)
                            sbLoc.AppendFormat(" ({0},{1})",
                                (cells[i * 2] & 0xFF) * 24, (cells[i * 2 + 1] & 0xFF) * 24);
                        if (numLoc > 8) sbLoc.Append(" ...");
                        if (outGrid > 0)
                            sbLoc.AppendFormat(" | CANH BAO: {0} o NGOAI luoi {1}x{2} - nghi parse lech",
                                outGrid, wT, hT);
                        State.RaiseDebugLog(sbLoc.ToString());
                    }
                }
            }
            catch { }

            // Signal map change event (like TileMap.i() notify in game)
            MapChangeEvent.Set();
        }

        /// <summary>
        /// cmd=122 sub=0: THEM mob moi vao map giua chung (clone MODGAME Controller addMob:5882).
        /// Day la cach Tinh Anh/Thu Linh/Ta Thu "XUAT HIEN" giua map - mang san levelBoss != 0.
        /// Format: count byte; moi mob: mobId(unsignedByte), 5 bool, tpl(unsignedByte), sys(byte),
        /// hp(int), level(unsignedByte), maxHp(int), x(short), y(short), status(byte), levelBoss(byte),
        /// isBoss(bool). Dat vao Mobs[mobId], noi mang neu can (giu index==mobId cho cmd -1/-4/-5).
        /// </summary>
        public void HandleAddMob(NsoMessage msg)
        {
            var r = msg.Reader;
            var map = State.CurrentMap;
            int count = r.ReadByte() & 0xFF;
            for (int n = 0; n < count; n++)
            {
                int mobId = r.ReadUnsignedByte();
                bool isDisable = r.ReadBoolean();
                r.ReadBoolean(); r.ReadBoolean(); r.ReadBoolean(); r.ReadBoolean(); // isDontMove/fire/ice/wind

                var mob = new MobState();
                mob.Id = mobId;
                mob.TemplateId = (short)r.ReadUnsignedByte();
                mob.Sys = r.ReadByte();
                mob.Hp = r.ReadInt();
                mob.Level = (byte)r.ReadUnsignedByte();
                mob.MaxHp = r.ReadInt();
                mob.X = r.ReadShort();
                mob.Y = r.ReadShort();
                mob.Status = r.ReadByte();
                mob.LevelBoss = r.ReadByte();
                mob.IsBoss = r.ReadBoolean();
                ClassifyElite(mob, State.MobStore); // Tinh Anh/Thu Linh theo maxHp (MODGAME Auto.a)
                mob.IsActive = !isDisable && mob.Hp > 0;
                mob.IsDead = mob.Hp <= 0;

                var arr = map.Mobs;
                if (mobId >= arr.Length)
                {
                    var bigger = new MobState[mobId + 1];
                    System.Array.Copy(arr, bigger, arr.Length);
                    map.Mobs = arr = bigger;
                }
                arr[mobId] = mob;
            }
        }

        /// <summary>
        /// Parse other player's char info (cmd=3 PLAYER_ADD).
        /// CLONE CHINH XAC MODGAME Controller.readCharInfo (Controller.java:5545) - server nay theo
        /// chuan MODGAME, KHAC HAN client 251 (mob trong MAP_INFO tpl byte, char cung khac).
        /// Bug cu (port tu 251/NSOAFK): doc X/Y short NGAY DAU + 16 body item + ca MP -> LECH ngay byte
        /// dau (server gui cClanName UTF truoc) -> ReadCharInfo nem exception -> HandlePlayerAdd nuot ->
        /// nguoi choi KHONG vao OtherPlayers -> PK am thay "players=0" -> khong danh ai.
        ///
        /// Format THAT (sau khi handler da doc charID int o ngoai - o day doc luon cho gon):
        ///   charID int | cClanName UTF | [ctypeClan byte neu clan != ""] | isInvisible bool |
        ///   cTypePk byte | nClass byte | cgender byte | head short | cName UTF | cHp int | cMaxHp int |
        ///   clevel ubyte | wp short | body short | leg short | mobMe byte(-1=none) |
        ///   cx short | cy short | eff5BuffHp short | eff5BuffMp short |
        ///   nEff byte { type byte, int, int, short }
        /// Server KHONG gui MP nguoi khac, KHONG gui cPk (karma) o goi nay.
        /// </summary>
        public static PlayerInfo ReadCharInfo(BigEndianBinaryReader r)
        {
            var p = new PlayerInfo();

            p.CharId = r.ReadInt();

            p.ClanName = r.ReadUTF();
            if (!string.IsNullOrEmpty(p.ClanName))
                p.ClanType = r.ReadByte();

            r.ReadBoolean();                       // isInvisible
            p.TypePk = r.ReadByte();               // cTypePk
            p.ClassId = r.ReadByte();              // nClass index
            p.Gender = r.ReadByte();               // cgender
            p.Head = r.ReadShort();                // head
            p.Name = r.ReadUTF();                  // cName
            p.Hp = r.ReadInt();                    // cHp
            p.MaxHp = r.ReadInt();                 // cMaxHp
            p.Level = (byte)r.ReadUnsignedByte();  // clevel
            p.Weapon = r.ReadShort();              // wp (vu khi) - truoc day doc roi vut
            p.Body = r.ReadShort();                // body
            p.Leg = r.ReadShort();                 // leg
            r.ReadByte();                          // mobMe template (byte; -1 = khong co)

            p.X = r.ReadShort();                   // cx (vi tri THAT - nam SAU trang bi)
            p.Y = r.ReadShort();                   // cy

            // eff5BuffHp/Mp + danh sach effect - doc cho het goi (khong dung cho PK), bao ve bang try.
            try
            {
                r.ReadShort();                     // eff5BuffHp
                r.ReadShort();                     // eff5BuffMp
                int nEff = r.ReadByte() & 0xFF;
                for (int i = 0; i < nEff; i++)
                {
                    r.ReadByte(); r.ReadInt(); r.ReadInt(); r.ReadShort();
                }
            }
            catch { }

            return p;
        }
    }
}
