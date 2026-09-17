using System;
using System.IO;

namespace NSOKHODO.GameData
{
    /// <summary>
    /// Tile collision engine - EXACT replica of TileMap from game source.
    /// Collision builder copied byte-for-byte from TileMap.a(int var0).
    /// Ground-finding copied from TileMap.e(int var0, int var1).
    /// </summary>
    public class TileEngine
    {
        private int _width;    // a = map width in tiles
        private int _height;   // b = map height in tiles
        private int[] _tiles;  // f = tile types (char[] in Java)
        private int[] _coll;   // g = collision flags
        private const int TILE = 24; // i = tile size

        public int MapWidthPx { get { return _width * TILE; } }   // c
        public int MapHeightPx { get { return _height * TILE; } }  // d
        public int WidthTiles { get { return _width; } }
        public int HeightTiles { get { return _height; } }
        public bool IsLoaded { get { return _coll != null && _width > 0; } }

        private static readonly string MapsDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Maps");

        /// <summary>
        /// Ban do da dung xong cho 1 cap (mapId, tileId). Bat bien sau khi dung -
        /// _tiles/_coll chi duoc ghi trong LoadMap/BuildCollision, sau do chi doc.
        /// </summary>
        private class BuiltMap
        {
            public int Width;
            public int Height;
            public int[] Tiles;
            public int[] Coll;
        }

        // Cache toan tien trinh: (mapId, tileId) -> ban do da dung. Truoc day MOI account
        // doc lai resource + parse + dung lai bang va cham o MOI lan doi map/khu; 600 account
        // doi map lien tuc -> rac GC lien mien du ket qua luon giong het nhau.
        private static readonly System.Collections.Generic.Dictionary<long, BuiltMap> _builtMaps =
            new System.Collections.Generic.Dictionary<long, BuiltMap>();
        private static readonly object _builtLock = new object();

        /// <summary>Ban do da MERGE them lop o nen server bom. Xem <see cref="ApplyGroundOverlay"/>.</summary>
        private class OverlayMap
        {
            public int[] Coll;
            public int Applied;
        }

        // Cache lop phu: "mapId|tileId|count|hash" -> bang va cham DA merge. Danh sach o server bom
        // gan nhu co dinh theo map, nen 600 account ra vao lien tuc chi ton DUY NHAT mot ban.
        private static readonly System.Collections.Generic.Dictionary<string, OverlayMap> _overlayMaps =
            new System.Collections.Generic.Dictionary<string, OverlayMap>();

        // (mapId, tileId) cua ban do dang nap - ApplyGroundOverlay can de dung khoa cache.
        private int _mapIdBuilt = -1;
        private int _tileIdBuilt = -1;

        // Bang va cham GOC (chua co lop phu) cua ban do dang nap. ApplyGroundOverlay LUON merge tu
        // day chu khong tu _coll, de goi hai lan lien tiep khong chong lop phu len lop phu.
        private int[] _baseColl;

        /// <summary>Load map and build collision. Exact loadMapFromResource() + a(tileId).</summary>
        public bool LoadMap(int mapId, int tileId)
        {
            long key = ((long)mapId << 32) ^ (uint)tileId;
            _mapIdBuilt = mapId;
            _tileIdBuilt = tileId;

            BuiltMap cached;
            lock (_builtLock)
            {
                if (_builtMaps.TryGetValue(key, out cached))
                {
                    _width = cached.Width;
                    _height = cached.Height;
                    _tiles = cached.Tiles;
                    _coll = _baseColl = cached.Coll;
                    return true;
                }
            }

            try
            {
                byte[] data = GetMapBytes(mapId);
                if (data == null || data.Length < 2) return false;

                // loadMapFromResource() - exact
                _width = data[0] & 0xFF;   // a = readUnsignedByte
                _height = data[1] & 0xFF;  // b = readUnsignedByte
                int count = _width * _height;

                _tiles = new int[count];
                _coll = new int[count];

                for (int i = 0; i < count && i + 2 < data.Length; i++)
                    _tiles[i] = data[i + 2] & 0xFF; // f[i] = readUnsignedByte (char in Java)

                // a(tileId) - build collision flags
                BuildCollision(tileId);

                var built = new BuiltMap { Width = _width, Height = _height, Tiles = _tiles, Coll = _coll };
                lock (_builtLock) _builtMaps[key] = built;
                _baseColl = _coll;
                return true;
            }
            catch { _coll = _baseColl = null; return false; }
        }

        /// <summary>
        /// LOP PHU "O NEN DO SERVER BOM" - clone client that (MODGAME NSO180_Tungvz).
        ///
        /// Server gui kem o DUOI goi vao map (cmd -18, sau UTF ten map) mot danh sach o tile;
        /// client nhet vao <c>TileMap.w</c> (Controller.java:3039-3046) roi trong ham dung bang va
        /// cham <c>TileMap.a(tileID)</c>, cau DAU TIEN cua vong lap - TRUOC moi nhanh tileId - la:
        /// <code>
        ///   if (w != null &amp;&amp; w.get(String.valueOf(idx)) != null)   // TileMap.java:359
        ///       g[idx] |= 2;                                           // ep thanh NEN dung duoc
        /// </code>
        /// Nghia la server bien BAT KY o nao thanh nen, khong can o do co trong file .bin. Day chinh
        /// la cach cac "be" ngoai dia hinh dung duoc - ro nhat la CAY o map 139 (Quy Son): anh cay
        /// trong 139.bin la tile TRANG TRI khong dac (tile 30/31/32/35 cua tile4.png), phan dung duoc
        /// hoan toan do server bom. Thieu buoc nay thi <see cref="TryFindStandable"/> tra false ->
        /// Navigator.CharBurstMoveToMob tra false -> TrainMode BO SACH quai dung tren cay, con
        /// <see cref="SnapToGround"/> keo Y cua quai xuong nen that ben duoi cay.
        ///
        /// ZNinjaPro (zangvps) khong tu dung bang va cham - bot va client nam chung mot jar nen goi
        /// thang lop map cua client: av_0.java:3265-3270 (aa[idx] |= 2) + aH.java:4143-4155 (nap
        /// danh sach) + aH.java:4165 (dung lai bang). Tuc no huong co che nay MIEN PHI.
        ///
        /// Wire: <c>ubyte count</c>, roi moi muc <c>ubyte tileX</c> + <c>ubyte tileY</c>;
        /// chi so o = <c>(short)(tileY * widthTiles + tileX)</c>.
        ///
        /// GOI SAU <see cref="LoadMap"/> trong cung mot lan doi map (can <c>_width</c> de tinh chi
        /// so, va can <c>_coll</c> nen da dung xong). Tra ve so o thuc su ap duoc.
        /// </summary>
        public int ApplyGroundOverlay(byte[] cells, int count)
        {
            int[] basis = _baseColl;
            if (basis == null || cells == null || count <= 0) return 0;
            if (count * 2 > cells.Length) count = cells.Length / 2;
            if (count <= 0) return 0;

            // Khoa cache = map + tileset + noi dung danh sach (FNV-1a 32 bit).
            uint hash = 2166136261;
            for (int i = 0; i < count * 2; i++) { hash ^= cells[i]; hash *= 16777619; }
            string key = _mapIdBuilt + "|" + _tileIdBuilt + "|" + count + "|" + hash.ToString("X8");

            OverlayMap hit;
            lock (_builtLock)
            {
                if (_overlayMaps.TryGetValue(key, out hit)) { _coll = hit.Coll; return hit.Applied; }
            }

            // COPY-ON-WRITE BAT BUOC: _baseColl tro THANG vao mang trong _builtMaps - mang DUNG
            // CHUNG cho moi account va moi lan doi map co cung (mapId, tileId). OR co thang vao do
            // se nhiem lop phu cua khu nay sang tat ca account khac, va sang ca nhung lan vao map
            // ma server KHONG gui o nao. Nhan ban truoc khi ghi.
            int[] merged = (int[])basis.Clone();
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                int col = cells[i * 2] & 0xFF;
                int row = cells[i * 2 + 1] & 0xFF;
                short idx = (short)(row * _width + col);   // ep (short) DUNG NHU ban goc
                if (idx < 0 || idx >= merged.Length) continue;
                merged[idx] |= 2;   // GROUND
                applied++;
            }

            var built = new OverlayMap { Coll = merged, Applied = applied };
            lock (_builtLock)
            {
                // Chan phinh vo han neu server doi danh sach theo tung khu/tung lan vao.
                if (_overlayMaps.Count > 512) _overlayMaps.Clear();
                _overlayMaps[key] = built;
            }
            _coll = merged;
            return applied;
        }

        // Cache: mapId -> ten embedded resource (quet 1 lan). Ten resource MSBuild sinh dang
        // "NSOKHODO.GameData.Maps.<id>.bin" - ta trich <id> tu duoi ten (ben prefix) cho ben vung.
        private static System.Collections.Generic.Dictionary<int, string> _embMaps;
        private static readonly object _embLock = new object();

        /// <summary>
        /// Lay byte map: (1) embedded resource trong EXE (de dong goi 1 file), (2) fallback file dia Maps/.
        /// </summary>
        private static byte[] GetMapBytes(int mapId)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            if (_embMaps == null)
            {
                lock (_embLock)
                {
                    if (_embMaps == null)
                    {
                        var d = new System.Collections.Generic.Dictionary<int, string>();
                        foreach (var name in asm.GetManifestResourceNames())
                        {
                            if (!name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) continue;
                            int dot = name.LastIndexOf('.', name.Length - 5); // dau '.' truoc <id>
                            if (dot < 0) continue;
                            string num = name.Substring(dot + 1, name.Length - 4 - (dot + 1));
                            int id;
                            if (int.TryParse(num, out id)) d[id] = name;
                        }
                        _embMaps = d;
                    }
                }
            }

            string res;
            if (_embMaps.TryGetValue(mapId, out res))
            {
                using (var s = asm.GetManifestResourceStream(res))
                {
                    if (s != null)
                    {
                        byte[] buf = new byte[s.Length];
                        int off = 0, r;
                        while (off < buf.Length && (r = s.Read(buf, off, buf.Length - off)) > 0) off += r;
                        return buf;
                    }
                }
            }

            try
            {
                string path = Path.Combine(MapsDir, mapId + ".bin");
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }
            catch { }
            return null;
        }

        /// <summary>TileMap.a(x, y) - get raw collision at pixel pos. Returns g[y/24*a + x/24].</summary>
        public int GetCollision(int px, int py)
        {
            int tx = px / TILE;
            int ty = py / TILE;
            if (tx < 0 || tx >= _width || ty < 0 || ty >= _height)
                return 0;
            return _coll[ty * _width + tx];
        }

        /// <summary>TileMap.a(x, y, mask) - check collision flags.</summary>
        public bool HasFlag(int px, int py, int mask)
        {
            return (GetCollision(px, py) & mask) == mask;
        }

        /// <summary>TileMap.b(y) - align to tile grid = (y/24)*24.</summary>
        public static int AlignToTile(int y) { return y / TILE * TILE; }

        /// <summary>
        /// Kieu tile THO tai o (tx, ty) - la byte goc trong file map, 1-based (0 = o trong).
        /// Tra -1 neu ngoai bien hoac chua nap map.
        ///
        /// Dung de VE map (chi so anh trong tileset = gia tri nay - 1). GetCollision() chi tra co
        /// va cham nen khong ve duoc. Xem docs/features/XEM_GAME.md §11.
        /// CO Y thuan int: file nay vao ca ban APK, khong duoc dinh Color/Bitmap.
        /// </summary>
        public int GetTile(int tx, int ty)
        {
            var t = _tiles;
            if (t == null || tx < 0 || tx >= _width || ty < 0 || ty >= _height) return -1;
            int i = ty * _width + tx;
            if (i < 0 || i >= t.Length) return -1;
            return t[i];
        }

        /// <summary>
        /// TileMap.e(x, y) - find ground Y position.
        /// EXACT copy from source lines 1667-1689.
        /// </summary>
        /// <summary>
        /// Cong VA CHAM "o co NEN dung duoc" - clone NSOTRUNGDUC <c>Class_gj.a(x, y, lllllIIl[2])</c>.
        /// Hang so da giai duoc tu bo khoi tao mang (Class_gj.java:2131):
        /// <c>lllllIIl[2] = " ".length() &lt;&lt; " ".length()</c> = <b>2</b>.
        /// Doi chieu <see cref="BuildCollision"/> cua chinh ta: bit <b>2 = Ground</b> (moi nhanh
        /// tileId deu `_coll[i] |= 2` cho o nen). Vay phep thu dung la <c>coll &amp; 2</c>.
        /// (Ban 2026-09-05 tung dung 16386 = 2|16384 - SAI, do la cong cua FindGround voi y nghia
        /// khac: "than nhan vat dang nam trong dat".)
        /// </summary>
        private const int COLL_GROUND = 2;

        /// <summary>
        /// Tim O DUNG DUOC gan (x, y) - clone NSOTRUNGDUC <c>Class_gj.a(int,int,int[])</c>
        /// (Class_gj.java:1898-1923), hang so da giai:
        /// <code>
        ///   y = alignToTile(y);
        ///   if (coll(x, y) &amp; 2)  { out = (x, y); return true; }        // ngay tai dich
        ///   for (i = 0; i &lt; 5; i++)                                     // lllllIIl[13] = 5
        ///     for (j = 0; j &lt; 5; j++) {
        ///        yy = y + i*24;                                          // CHI quet XUONG DUOI
        ///        xx = x - 48 + j*24;                                     // lllllIIl[36] = 48
        ///        if (yy &lt; mapH &amp;&amp; xx &gt; 24 &amp;&amp; xx &lt; mapW-24 &amp;&amp; coll(xx,yy) &amp; 2)
        ///           { out = (xx, yy); return true; }
        ///     }
        ///   return false;
        /// </code>
        /// O quet la 5 cot (+/-48px) x 5 hang (0..+96px XUONG) - hop ly vi nen bao gio cung o DUOI.
        ///
        /// Tra <c>false</c> chinh la chot ma <c>Class_ba.c</c> dung de <b>tu choi di</b>, keo theo
        /// <c>Class_ad.c(mob)</c> dat <c>me.cs = null</c> (bo con quai do). Do la thu NSOKHODO
        /// thieu han: ta burst move thang toi toa do mob du duoi chan no la khoang khong giua hai
        /// tang da (map 41) -> server ap trong luc -> nhan vat ROI, roi bao lai bang cmd 52
        /// (log 2026-09-05: "(1596,144) -> (1690,312) lech 94,168").
        /// </summary>
        /// <summary>
        /// KEO Y XUONG NEN - clone NSOTRUNGDUC <c>Class_gj.b(int,int)</c> (Class_gj.java:1848):
        /// <code>
        ///   y = alignToTile(y);
        ///   if (!coll(x,y) &amp; 2)
        ///     for (k = 0; k &lt; 7; k++) {            // lllllIIl[17] = 7
        ///        yy = y - 48 + k*24;                // -48,-24,0,+24,+48,+72,+96
        ///        if (yy &gt; 0 &amp;&amp; yy &lt; mapH &amp;&amp; coll(xx,yy) &amp; 2) return yy;
        ///     }
        ///   return y;
        /// </code>
        /// Ban goc goi ham nay TRUOC khi di toi mob: <c>Class_ba.c(mob.h, Class_gj.b(mob.h, mob.i))</c>
        /// (Class_ad.java:553) - tuc Y cua mob bi keo ve nen RUOI khi gui lenh di. NSOKHODO truoc
        /// day gui thang Y THO cua mob -> di ngang trong khoang khong -> server ap trong luc -> ROI.
        /// Bien quet 7 buoc x 24px = 168px, TRUNG KHOP do lech do duoc trong log 2026-09-05:
        /// "[Pos] cmd52: server doi cho (1596,144) -&gt; (1690,312) lech 94,168".
        /// </summary>
        public int SnapToGround(int x, int y)
        {
            if (_coll == null) return y;
            int ay = AlignToTile(y);
            if (HasFlag(x, ay, COLL_GROUND)) return ay;
            for (int k = 0; k < 7; k++)
            {
                int yy = ay - 48 + k * TILE;
                if (yy > 0 && yy < MapHeightPx && HasFlag(x, yy, COLL_GROUND)) return yy;
            }
            return ay;
        }

        public bool TryFindStandable(int x, int y, out int sx, out int sy)
        {
            sx = x; sy = y;
            if (_coll == null) return true;   // chua co du lieu o -> khong chan (giu hanh vi cu)

            int ay = AlignToTile(y);
            if (HasFlag(x, ay, COLL_GROUND)) { sy = ay; return true; }

            for (int i = 0; i < 5; i++)
            {
                int yy = ay + i * TILE;
                if (yy >= MapHeightPx) break;
                for (int j = 0; j < 5; j++)
                {
                    int xx = x - 48 + j * TILE;
                    if (xx <= TILE || xx >= MapWidthPx - TILE) continue;
                    if (HasFlag(xx, yy, COLL_GROUND)) { sx = xx; sy = yy; return true; }
                }
            }
            return false;
        }

        public int FindGround(int x, int y)
        {
            if (_coll == null) return y;

            // if ((a(var0, var1 - 16) & 16386) != 0)
            int flags = GetCollision(x, y - 16);
            if ((flags & 16386) != 0) // 0x4002 = ground+solid
            {
                int alignedY = AlignToTile(y); // var1 = b(var1) = (y/24)*24

                // Search UP first (priority): for (var2 = 24; var2 < 240; var2 += 24)
                for (int dy = TILE; dy < 240; dy += TILE)
                {
                    // Chot bien `y - k > 0` cua ban goc (Class_gj.java:1878) - THIEU truoc day nen
                    // co the tra Y AM roi bi ep (short) thanh toa do rac.
                    if (alignedY - dy <= 0) break;
                    if ((GetCollision(x, alignedY - dy) & 16386) == 0)
                        return alignedY - dy + TILE;
                }

                // Then scan DOWN: for (var2 = 24; var2 < 120; var2 += 24)
                for (int dy = TILE; dy < 120; dy += TILE)
                {
                    // Chot bien `y + k < d` cua ban goc (Class_gj.java:1885).
                    if (alignedY + dy >= MapHeightPx) break;
                    if ((GetCollision(x, alignedY + dy) & 16386) == 0)
                        return alignedY + dy;
                }
            }

            return y;
        }

        /// <summary>
        /// TileMap.a(int var0) - build collision from tile types.
        /// EXACT copy from source lines 352-601.
        /// Java char values converted to int (e.g., '\t'=9, '$'=36, '='=61).
        /// </summary>
        private void BuildCollision(int tileId)
        {
            int a = _width; // used for spike check: g[var1 - a]

            for (int i = 0; i < _tiles.Length; i++)
            {
                int t = _tiles[i];

                // ===== tileId == 4 (lines 364-393) =====
                if (tileId == 4)
                {
                    // Ground (|= 2)
                    if (t==1||t==2||t==3||t==4||t==5||t==6||t==9||t==10||t==79||t==80||t==13||t==14||t==43||t==44||t==45||t==50)
                        _coll[i] |= 2;
                    // Right wall (|= 4)
                    if (t==9||t==11)
                        _coll[i] |= 4;
                    // Left wall (|= 8)
                    if (t==10||t==12)
                        _coll[i] |= 8;
                    // Conveyor (|= 1024)
                    if (t==13||t==14)
                        _coll[i] |= 1024;
                    // Spike (|= 64)
                    if (t==76||t==77) // 'L'=76, 'M'=77
                    {
                        _coll[i] |= 64;
                        if (t==78) // 'N'=78 - note: this is inside if(L||M), so never true. Game bug.
                            _coll[i] |= 4096;
                    }
                }

                // ===== tileId == 1 (lines 395-458) =====
                if (tileId == 1)
                {
                    // Ground (|= 2)
                    if (t==1||t==2||t==3||t==4||t==5||t==6||t==7||t==36||t==37||t==54||t==91||t==92||t==93||t==94||t==73||t==74||t==97||t==98||t==116||t==117||t==118||t==120||t==61)
                        // '$'=36,'%'=37,'6'=54,'['=91,'\\'=92,']'=93,'^'=94,'I'=73,'J'=74,'a'=97,'b'=98,'t'=116,'u'=117,'v'=118,'x'=120,'='=61
                        _coll[i] |= 2;
                    // Ladder (|= 4096)
                    if (t==2||t==3||t==4||t==5||t==6||t==20||t==21||t==22||t==23||t==36||t==37||t==38||t==39||t==61)
                        // '$'=36,'%'=37,'&'=38,'\''=39,'='=61
                        _coll[i] |= 4096;
                    // Slide (|= 16)
                    if (t==8||t==9||t==10||t==12||t==13||t==14||t==30)
                        // '\b'=8,'\t'=9,'\n'=10,'\f'=12,'\r'=13,14,30
                        _coll[i] |= 16;
                    // Jump-through (|= 32)
                    if (t==17) _coll[i] |= 32;
                    // Bounce (|= 128)
                    if (t==18) _coll[i] |= 128;
                    // Right wall (|= 4)
                    if (t==37||t==38||t==61) // '%'=37,'&'=38,'='=61
                        _coll[i] |= 4;
                    // Left wall (|= 8)
                    if (t==36||t==39||t==61) // '$'=36,'\''=39,'='=61
                        _coll[i] |= 8;
                    // Spike (|= 64)
                    if (t==19)
                    {
                        _coll[i] |= 64;
                        if (i >= a && (_coll[i - a] & 4096) == 4096)
                            _coll[i] |= 4096;
                    }
                    // Water (|= 2048)
                    if (t==35) _coll[i] |= 2048; // '#'=35
                    // Conveyor (|= 1024)
                    if (t==7) _coll[i] |= 1024;
                    // Wind (|= 256)
                    if (t==32||t==33||t==34) _coll[i] |= 256; // ' '=32,'!'=33,'"'=34
                }

                // ===== tileId == 2 (lines 460-537) =====
                if (tileId == 2)
                {
                    // Ground (|= 2)
                    if (t==1||t==2||t==3||t==4||t==5||t==6||t==7||t==36||t==37||t==54||t==61||t==73||t==76||t==77||t==78||t==79||t==82||t==83||t==98||t==99||t==100||t==102||t==103||t==108||t==109||t==110||t==112||t==113||t==116||t==117||t==125||t==126||t==127||t==129||t==130)
                        // '$'=36,'%'=37,'6'=54,'='=61,'I'=73,'L'=76,'M'=77,'N'=78,'O'=79,'R'=82,'S'=83,'b'=98,'c'=99,'d'=100,'f'=102,'g'=103,'l'=108,'m'=109,'n'=110,'p'=112,'q'=113,'t'=116,'u'=117,'}'=125,'~'=126,127,129,130
                        _coll[i] |= 2;
                    // Ladder (|= 4096)
                    if (t==1||t==3||t==4||t==5||t==6||t==20||t==21||t==22||t==23||t==36||t==37||t==38||t==39||t==55||t==109||t==111||t==112||t==113||t==114||t==115||t==116||t==127||t==129||t==130)
                        // '$'=36,'%'=37,'&'=38,'\''=39,'7'=55,'m'=109,'o'=111,'p'=112,'q'=113,'r'=114,'s'=115,'t'=116,127,129,130
                        _coll[i] |= 4096;
                    // Slide (|= 16)
                    if (t==8||t==9||t==10||t==12||t==13||t==14||t==30||t==135)
                        _coll[i] |= 16;
                    // Jump-through (|= 32)
                    if (t==17) _coll[i] |= 32;
                    // Bounce (|= 128)
                    if (t==18) _coll[i] |= 128;
                    // Right wall (|= 4)
                    if (t==61||t==37||t==38||t==127||t==130||t==131)
                        // '='=61,'%'=37,'&'=38,127,130,131
                        _coll[i] |= 4;
                    // Left wall (|= 8)
                    if (t==61||t==36||t==39||t==127||t==129||t==132)
                        // '='=61,'$'=36,'\''=39,127,129,132
                        _coll[i] |= 8;
                    // Spike (|= 64)
                    if (t==19)
                    {
                        _coll[i] |= 64;
                        if (i >= a && (_coll[i - a] & 4096) == 4096)
                            _coll[i] |= 4096;
                    }
                    if (t==134)
                    {
                        _coll[i] |= 64;
                        if (i >= a && (_coll[i - a] & 4096) == 4096)
                            _coll[i] |= 4096;
                    }
                    // Water (|= 2048)
                    if (t==35) _coll[i] |= 2048; // '#'=35
                    // Conveyor (|= 1024)
                    if (t==7) _coll[i] |= 1024;
                    // Wind (|= 256)
                    if (t==32||t==33||t==34) _coll[i] |= 256; // ' '=32,'!'=33,'"'=34
                    // Platform edge (|= 8192)
                    if (t==61||t==127) _coll[i] |= 8192; // '='=61,127
                }

                // ===== tileId == 3 (lines 539-601) =====
                if (tileId == 3)
                {
                    // Ground (|= 2)
                    if (t==1||t==2||t==3||t==4||t==5||t==6||t==7||t==11||t==14||t==17||t==43||t==51||t==63||t==65||t==67||t==68||t==71||t==72||t==83||t==84||t==85||t==87||t==91||t==94||t==97||t==98||t==106||t==107||t==111||t==113||t==117||t==118||t==119||t==125||t==126||t==129||t==130||t==131||t==133||t==136||t==138||t==139||t==142)
                        // '+'=43,'3'=51,'?'=63,'A'=65,'C'=67,'D'=68,'G'=71,'H'=72,'S'=83,'T'=84,'U'=85,'W'=87,'['=91,'^'=94,'a'=97,'b'=98,'j'=106,'k'=107,'o'=111,'q'=113,'u'=117,'v'=118,'w'=119,'}'=125,'~'=126,129,130,131,133,136,138,139,142
                        _coll[i] |= 2;
                    // Ladder (|= 4096)
                    if (t==124||t==116||t==123||t==44||t==12||t==15||t==16||t==45||t==10||t==9)
                        // '|'=124,'t'=116,'{'=123,','=44,'\f'=12,15,16,'-'=45,'\n'=10,'\t'=9
                        _coll[i] |= 4096;
                    // Jump-through (|= 32)
                    if (t==23) _coll[i] |= 32;
                    // Bounce (|= 128)
                    if (t==24) _coll[i] |= 128;
                    // Right wall (|= 4)
                    if (t==6||t==15||t==51||t==95||t==97||t==106||t==111||t==123||t==125||t==138||t==140)
                        // 6,15,'3'=51,'_'=95,'a'=97,'j'=106,'o'=111,'{'=123,'}'=125,138,140
                        _coll[i] |= 4;
                    // Left wall (|= 8)
                    if (t==7||t==16||t==51||t==96||t==98||t==107||t==111||t==124||t==126||t==139||t==141)
                        // 7,16,'3'=51,'`'=96,'b'=98,'k'=107,'o'=111,'|'=124,'~'=126,139,141
                        _coll[i] |= 8;
                    // Spike (|= 64)
                    if (t==25)
                    {
                        _coll[i] |= 64;
                        if (i >= a && (_coll[i - a] & 4096) == 4096)
                            _coll[i] |= 4096;
                    }
                    // Water (|= 2048)
                    if (t==34) _coll[i] |= 2048; // '"'=34
                    // Conveyor (|= 1024)
                    if (t==17) _coll[i] |= 1024;
                    // Wind (|= 256)
                    if (t==33||t==103||t==104||t==105||t==26)
                        // '!'=33,'g'=103,'h'=104,'i'=105,26
                        _coll[i] |= 256;
                }
            }
        }
    }
}
