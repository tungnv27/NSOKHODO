using System.Collections.Generic;

namespace NSOKHODO.Auto
{
    /// <summary>
    /// Map connection graph from TileMap.java bb[] array.
    /// Used for BFS pathfinding between maps (like "gm" command).
    /// </summary>
    public static class MapGraph
    {
        // bb[mapId] = connected map IDs (from TileMap.java lines 84-237)
        private static readonly short[][] Connections = new short[160][];

        static MapGraph()
        {
            for (int i = 0; i < 160; i++) Connections[i] = new short[0];

            Connections[0] = new short[] { 27 };
            Connections[1] = new short[] { 2, 3, 27, 72, 91, 94, 105, 114, 125, 157, 139, 113, 80 };
            Connections[2] = new short[] { 6, 1 };
            Connections[3] = new short[] { 1, 4 };
            Connections[4] = new short[] { 3, 5 };
            Connections[5] = new short[] { 7, 4 };
            Connections[6] = new short[] { 7, 2, 20, 21 };
            Connections[7] = new short[] { 6, 5, 8 };
            Connections[8] = new short[] { 7, 9 };
            Connections[9] = new short[] { 8, 10 };
            Connections[10] = new short[] { 9, 11, 17, 22, 32, 38, 43, 48, 139 };
            Connections[11] = new short[] { 12, 10 };
            Connections[12] = new short[] { 11, 57 };
            Connections[13] = new short[] { 57, 14 };
            Connections[14] = new short[] { 13, 15 };
            Connections[15] = new short[] { 14, 16 };
            Connections[16] = new short[] { 15, 17 };
            Connections[17] = new short[] { 16, 18, 10, 22, 32, 38, 43, 48, 139 };
            Connections[18] = new short[] { 17, 19 };
            Connections[19] = new short[] { 18, 58 };
            Connections[20] = new short[] { 6 };
            Connections[21] = new short[] { 22, 6 };
            Connections[22] = new short[] { 23, 21, 10, 17, 32, 38, 43, 48, 139 };
            Connections[23] = new short[] { 22, 69, 25 };
            Connections[24] = new short[] { 59, 36 };
            Connections[25] = new short[] { 23, 26 };
            Connections[26] = new short[] { 27, 25 };
            Connections[27] = new short[] { 26, 28, 1, 72, 91, 94, 105, 114, 125, 157, 139, 113, 80 };
            Connections[28] = new short[] { 27, 60 };
            Connections[29] = new short[] { 60, 30 };
            Connections[30] = new short[] { 29, 31 };
            Connections[31] = new short[] { 32, 30 };
            Connections[32] = new short[] { 31, 61, 10, 17, 22, 38, 43, 48, 139 };
            Connections[33] = new short[] { 61, 34 };
            Connections[34] = new short[] { 35, 33 };
            Connections[35] = new short[] { 34, 66 };
            Connections[36] = new short[] { 37, 24 };
            Connections[37] = new short[] { 36 };
            Connections[38] = new short[] { 67, 68, 10, 17, 22, 32, 43, 48, 139 };
            Connections[39] = new short[] { 72, 46, 40 };
            Connections[40] = new short[] { 39, 65, 41 };
            Connections[41] = new short[] { 42, 40, 43 };
            Connections[42] = new short[] { 62, 41 };
            Connections[43] = new short[] { 41, 44, 10, 17, 22, 32, 38, 48, 139 };
            Connections[44] = new short[] { 43, 45 };
            Connections[45] = new short[] { 44, 53 };
            Connections[46] = new short[] { 63, 39, 47 };
            Connections[47] = new short[] { 46, 48 };
            Connections[48] = new short[] { 47, 50, 10, 17, 22, 32, 38, 43, 139 };
            Connections[49] = new short[] { 50, 51 };
            Connections[50] = new short[] { 48, 49 };
            Connections[51] = new short[] { 52, 49 };
            Connections[52] = new short[] { 51, 64 };
            Connections[53] = new short[] { 54, 45 };
            Connections[54] = new short[] { 55, 53 };
            Connections[55] = new short[] { 54 };
            Connections[56] = new short[] { 72 };
            Connections[57] = new short[] { 12, 13 };
            Connections[58] = new short[] { 19 };
            Connections[59] = new short[] { 68, 24 };
            Connections[60] = new short[] { 28, 29 };
            Connections[61] = new short[] { 33, 32 };
            Connections[62] = new short[] { 42 };
            Connections[63] = new short[] { 46 };
            Connections[64] = new short[] { 52 };
            Connections[65] = new short[] { 40 };
            Connections[66] = new short[] { 67, 35 };
            Connections[67] = new short[] { 66, 38 };
            Connections[68] = new short[] { 59, 38 };
            Connections[69] = new short[] { 70, 23 };
            Connections[70] = new short[] { 69, 71 };
            Connections[71] = new short[] { 72, 70 };
            Connections[72] = new short[] { 71, 39, 1, 27, 91, 94, 105, 114, 125, 157, 139, 113, 80 };
            Connections[73] = new short[] { 1 };
            Connections[80] = new short[] { 81, 82, 83 };
            Connections[81] = new short[] { 80, 84 };
            Connections[82] = new short[] { 80, 85 };
            Connections[83] = new short[] { 80, 86 };
            Connections[84] = new short[] { 81, 87 };
            Connections[85] = new short[] { 82, 88 };
            Connections[86] = new short[] { 83, 89 };
            Connections[87] = new short[] { 84, 90 };
            Connections[88] = new short[] { 85, 90 };
            Connections[89] = new short[] { 86, 90 };
            Connections[91] = new short[] { 92 };
            Connections[92] = new short[] { 91, 93 };
            Connections[93] = new short[] { 92 };
            Connections[94] = new short[] { 95 };
            Connections[95] = new short[] { 94, 96 };
            Connections[96] = new short[] { 95, 97 };
            Connections[97] = new short[] { 96 };
            Connections[98] = new short[] { 99 };
            Connections[99] = new short[] { 98, 101, 100, 102 };
            Connections[100] = new short[] { 99, 103 };
            Connections[101] = new short[] { 99, 103 };
            Connections[102] = new short[] { 99, 103 };
            Connections[103] = new short[] { 101, 102, 104, 100 };
            Connections[104] = new short[] { 103 };
            Connections[105] = new short[] { 107, 106, 108 };
            Connections[106] = new short[] { 105, 109 };
            Connections[107] = new short[] { 105, 109 };
            Connections[108] = new short[] { 105, 109 };
            Connections[109] = new short[] { 106, 107, 108 };
            Connections[112] = new short[] { 113 };
            Connections[113] = new short[] { 112 };
            Connections[114] = new short[] { 115 };
            Connections[115] = new short[] { 114, 116 };
            Connections[116] = new short[] { 115 };
            // ===== BO SUNG 2026-09-06 (A4) - doi chieu bang ke ZangVPS `av_0.java:1643-2285` =====
            // Bang cua ta va cua Zang TRUNG KHIT 127/127 map, ca gia tri lan THU TU (thu tu moi la
            // thu quan trong: vi tri trong mang chinh la chi so waypoint). Ta chi thieu dung 8 map.
            // Them vao KHONG THE lam hong tuyen dang chay: da kiem tra tung map, 120-124 hien
            // KHONG map nao tro toi (khong the toi duoc) nen chung chi la nhanh cut duoc noi them.
            // Loi ich that: neu nhan vat dang DUNG SAN trong mot map thuoc cum nay (map su kien),
            // truoc day BuildPath luon tra null - khong co duong ra. Nay ra duoc qua 98 / 104.
            Connections[120] = new short[] { 121, 122, 123, 98 };
            Connections[121] = new short[] { 120, 124 };
            Connections[122] = new short[] { 120, 124 };
            Connections[123] = new short[] { 120, 124 };
            Connections[124] = new short[] { 123, 122, 121, 104 };

            Connections[125] = new short[] { 126 };
            Connections[126] = new short[] { 125, 127 };
            Connections[127] = new short[] { 126, 128 };
            Connections[128] = new short[] { 127 };
            Connections[134] = new short[] { 138 };
            Connections[135] = new short[] { 138 };
            Connections[136] = new short[] { 138 };
            Connections[137] = new short[] { 138 };
            Connections[138] = new short[] { 134, 135, 136, 137 };
            Connections[139] = new short[] { 140 };
            Connections[140] = new short[] { 139, 141 };
            Connections[141] = new short[] { 140, 142 };
            Connections[142] = new short[] { 141, 143 };
            Connections[143] = new short[] { 142, 144 };
            Connections[144] = new short[] { 143, 145 };
            Connections[145] = new short[] { 144, 146 };
            Connections[146] = new short[] { 145, 147 };
            Connections[147] = new short[] { 146, 148 };
            Connections[148] = new short[] { 147 };

            // GO BAY MOT CHIEU o map 157 (A4). Connections[1]/[27]/[72] DEU liet ke 157, tuc BFS
            // vach duong VAO duoc, nhung truoc day khong co hang Connections[157] -> vao roi khong
            // ra, BuildPath luon tra null. Bay nay da duoc ghi nhan 2026-09-05 khi doi chieu
            // NSOTRUNGDUC (`bh[157] = {158,159}`) nhung khi do hoan lai vi so "them canh server
            // khong co". Nay co NGUON DOC LAP THU HAI xac nhan cung so lieu: ZangVPS 2024
            // (`av_0.java`, `157: 158, 159` / `158: 157, 159` / `159: 158, 157`).
            // Da kiem tra: cum 157-158-159 la vong KIN, khong noi ra map nao khac, va 158/159 hien
            // khong map nao tro toi. Nen them vao khong the rut ngan hay doi bat ky tuyen nao dang
            // chay - chi bien mot ngo cut thanh co duong ra.
            Connections[157] = new short[] { 158, 159 };
            Connections[158] = new short[] { 157, 159 };
            Connections[159] = new short[] { 158, 157 };
        }

        /// <summary>Check if character can enter a map based on taskId (from TileMap.k)</summary>
        public static bool CanEnterMap(int mapId, int taskId, bool isHuman)
        {
            if (!isHuman) return true;
            if ((mapId == 1 || mapId == 27 || mapId == 72) && taskId < 6) return false;
            if ((mapId == 10 || mapId == 32 || mapId == 48) && taskId < 17) return false;
            if (mapId == 38 && taskId < 28) return false;
            if (mapId == 43 && taskId < 33) return false;
            if (mapId == 17 && taskId < 38) return false;
            if (mapId == 7 && taskId < 15) return false;
            return true;
        }

        /// <summary>
        /// Check village-to-village task requirement (taskId >= 9).
        /// From TileMap.k source: village transitions need taskId >= 9.
        /// </summary>
        public static bool CanTravelBetweenVillages(int taskId, bool isHuman)
        {
            if (!isHuman) return true;
            return taskId >= 9;
        }

        /// <summary>
        /// "Di map khi CHUA LAM NHIEM VU" - nguong <c>taskId</c> de DICH CHUYEN toi tung diem mo.
        /// Nguong trung tung so voi bang gate o <see cref="CanEnterMap"/> (= ZangVPS
        /// <c>av_0.java:689-736</c>). Xem docs/features/BAN_DO.md phan "di chuyen".
        ///
        /// Y nghia: server CHAN NPC dich chuyen toi cac map nay khi chua du nhiem vu, nhung KHONG
        /// chan di bo qua cong. Nen cach di duoc la: nhay toi diem GAN DICH NHAT ma minh DA MO,
        /// roi di bo not phan con lai - xuyen qua chinh nhung map dang bi khoa dich chuyen.
        ///
        /// 2026-09-13: danh sach nay CHI gom nhung map NPC dua toi duoc (bang NPC cua Zang):
        /// BO Rung Mishima 7 (khong NPC nao dua toi - "nhay toi 7" thuc chat la di bo) va THEM
        /// Lang Tone 22 (NPC 7 dua toi, khong chan nhiem vu).
        /// </summary>
        private static readonly Dictionary<int, int> TeleThreshold = new Dictionary<int, int>
        {
            { 1, 6 }, { 27, 6 }, { 72, 6 },     // 3 Truong
            { 22, 0 },                          // Lang Tone - khong chan
            { 10, 17 }, { 32, 17 }, { 48, 17 }, // Lang Kojin / Lang chai / Lang Oshin
            { 38, 28 },                         // Lang Chakumi
            { 43, 33 },                         // Lang Echigo
            { 17, 38 }                          // Lang Sanzu
        };

        /// <summary>
        /// So chang ĐI BỘ tu <paramref name="from"/> toi <paramref name="to"/>, -1 neu khong co
        /// duong đi bộ. Dung do thi CHI-DI-BO (<see cref="FindPathWaypointOnly"/>) nen KHONG an
        /// gian qua canh NPC dich chuyen - dung thu server thuc su cho di.
        /// </summary>
        public static int WalkHops(int from, int to)
        {
            if (from == to) return 0;
            var p = FindPathWaypointOnly(from, to, 0);
            return (p == null || p.Count < 2) ? -1 : p.Count - 1;
        }

        /// <summary>
        /// Chang ĐI BỘ ke tiep tren duong tu <paramref name="from"/> toi <paramref name="target"/>,
        /// -1 neu khong co duong. Khong dinh dang gi toi nhiem vu - day la buoc "gate OFF".
        /// </summary>
        public static int NextWalkHop(int from, int target)
        {
            if (from == target) return -1;
            var p = FindPathWaypointOnly(from, target, 0);
            return (p == null || p.Count < 2) ? -1 : p[1];
        }

        /// <summary>
        /// Diem DA MO (theo <paramref name="taskId"/>) gan <paramref name="target"/> nhat tinh theo
        /// so chang di bo. Tra -1 khi chua mo diem nao, hoac moi diem da mo deu khong co duong di bo
        /// toi dich. Clone <c>ModMove.bestTeleHub</c>.
        /// <para><paramref name="boQua"/> = cac diem NPC vua dich chuyen HONG (dang bi chan 120s) -
        /// khong chon lai dung diem do de khoi lap lai chang hong.</para>
        /// </summary>
        public static int BestTeleHub(int target, int taskId, bool isHuman, ICollection<int> boQua = null)
        {
            int best = -1, bestD = int.MaxValue;
            foreach (var kv in TeleThreshold)
            {
                if (isHuman && taskId < kv.Value) continue;   // chua mo -> server chan dich chuyen
                if (boQua != null && boQua.Contains(kv.Key)) continue;
                int d = WalkHops(kv.Key, target);
                if (d < 0 || d >= bestD) continue;
                bestD = d;
                best = kv.Key;
            }
            return best;
        }

        /// <summary>
        /// BFS shortest path from sourceMap to targetMap.
        /// Returns list of map IDs to traverse, or null if no path.
        /// Mirrors TileMap.k() from source.
        ///
        /// <para><paramref name="ignoreTaskGate"/> = bo qua chot nhiem vu (clone hook22 cua MODGAME:
        /// <c>ModMove.walking</c>). CHI duoc bat khi dang ĐI BỘ tung chang - luc DICH CHUYEN phai de
        /// nguyen chot, khong thi BFS vach duong xuyen canh NPC va server chan giua duong.
        /// La THAM SO chu khong phai co tinh: mot tien trinh chay toi 600 account, co tinh se bi
        /// account nay bat trong luc account kia dang dich chuyen.</para>
        /// </summary>
        public static List<int> FindPath(int source, int target, HashSet<string> brokenLinks = null,
            int taskId = 99, bool isHuman = true, int questMapId = -1, bool ignoreTaskGate = false,
            int canCuDiaMap = -1)
        {
            if (source < 0 || source >= 160 || target < 0 || target >= 160)
                return null;
            if (source == target)
                return new List<int> { source };
            if (Connections[source].Length == 0)
                return null;

            int[] dist = new int[160];
            int[] prev = new int[160];
            bool[] visited = new bool[160];

            for (int i = 0; i < 160; i++)
            {
                dist[i] = -1;
                prev[i] = -1;
            }

            dist[source] = 0;

            while (true)
            {
                int minDist = -1;
                int minNode = -1;
                for (int i = 0; i < 160; i++)
                {
                    if (!visited[i] && dist[i] != -1 && (dist[i] < minDist || minDist == -1))
                    {
                        minDist = dist[i];
                        minNode = i;
                    }
                }

                if (minNode == -1) return null;

                if (minNode == target)
                {
                    var path = new List<int>();
                    int cur = target;
                    while (cur != -1)
                    {
                        path.Insert(0, cur);
                        cur = prev[cur];
                    }
                    return path;
                }

                visited[minNode] = true;

                foreach (short neighbor in Connections[minNode])
                {
                    // Skip ONLY permanent broken waypoint links (not NPC blacklist)
                    // NPC blacklist only affects execution method, not pathfinding
                    if (brokenLinks != null && brokenLinks.Contains(minNode + ":" + neighbor))
                        continue;

                    // Task restrictions (from TileMap.k BFS) - tat khi dang di bo (xem ignoreTaskGate).
                    if (!ignoreTaskGate)
                    {
                        if (!CanEnterMap(neighbor, taskId, isHuman))
                            continue;

                        // Village-to-village requires taskId >= 9
                        if (IsVillage(minNode) && IsVillage(neighbor) &&
                            !CanTravelBetweenVillages(taskId, isHuman))
                            continue;
                    }

                    if (!visited[neighbor] && (dist[neighbor] == -1 || dist[neighbor] > dist[minNode] + 1))
                    {
                        dist[neighbor] = dist[minNode] + 1;
                        prev[neighbor] = minNode;
                    }
                }

                // Bom canh tat NPC 25: Truong (1/27/72) -> map nhiem vu hang ngay, 1 buoc.
                // Clone TileMap.k() MODGAME (var10=f(var6) && var8.mapId): khi mo rong 1 node Truong,
                // them canh Truong->questMap voi dist+1 (NPC 25 teleport). Chi inject khi dang toi questMap.
                // 2026-09-13 (S6): canh bom CUNG phai tuan danh sach chan. Truoc day no bo qua
                // brokenLinks => NPC 25 hong thi tick sau BFS lai chon dung chang hong do.
                if (questMapId > 0 && questMapId < 160 && IsVillage(minNode) && !visited[questMapId]
                    && (brokenLinks == null || !brokenLinks.Contains(minNode + ":" + questMapId))
                    && (dist[questMapId] == -1 || dist[questMapId] > dist[minNode] + 1))
                {
                    dist[questMapId] = dist[minNode] + 1;
                    prev[questMapId] = minNode;
                }

                // Bom canh tat NPC 25: Truong -> CAN CU DIA cua phe minh (98 hoac 104).
                // Clone ZangVPS av_0.java:747-761 - Zang bom canh nay o MOI node Truong, chon 98 hay 104
                // theo co i_0.aD (= cTypePk 4/5 cua minh, xem GameStateManager.CanCuDiaMap).
                if ((canCuDiaMap == 98 || canCuDiaMap == 104) && IsVillage(minNode) && !visited[canCuDiaMap]
                    && (brokenLinks == null || !brokenLinks.Contains(minNode + ":" + canCuDiaMap))
                    && (dist[canCuDiaMap] == -1 || dist[canCuDiaMap] > dist[minNode] + 1))
                {
                    dist[canCuDiaMap] = dist[minNode] + 1;
                    prev[canCuDiaMap] = minNode;
                }
            }
        }

        // Hub maps where connections beyond index 1 are NPC teleporters, not physical waypoints
        // From source: d() = {10,17,22,32,38,43,48,139}, f() = {1,27,72}
        private static readonly HashSet<int> HubMaps = new HashSet<int>
        {
            1, 10, 17, 22, 27, 32, 38, 43, 48, 72, 139
        };

        /// <summary>
        /// Get the number of physical waypoint connections for a map.
        /// Hub maps (villages/crossroads) have first 2 as waypoints, rest are NPC teleporters.
        /// </summary>
        private static int GetPhysicalWaypointCount(int mapId)
        {
            if (!HubMaps.Contains(mapId)) return Connections[mapId].Length;
            return System.Math.Min(2, Connections[mapId].Length);
        }

        /// <summary>
        /// BFS path using ONLY physical waypoint connections.
        /// Excludes NPC teleporter connections on all hub maps.
        /// </summary>
        public static List<int> FindPathWaypointOnly(int source, int target, int unusedParam)
        {
            if (source < 0 || source >= 160 || target < 0 || target >= 160)
                return null;

            int[] dist = new int[160];
            int[] prev = new int[160];
            bool[] visited = new bool[160];

            for (int i = 0; i < 160; i++) { dist[i] = -1; prev[i] = -1; }
            dist[source] = 0;

            while (true)
            {
                int minDist = -1, minNode = -1;
                for (int i = 0; i < 160; i++)
                {
                    if (!visited[i] && dist[i] != -1 && (dist[i] < minDist || minDist == -1))
                    { minDist = dist[i]; minNode = i; }
                }
                if (minNode == -1) return null;
                if (minNode == target)
                {
                    var path = new List<int>();
                    int cur = target;
                    while (cur != -1) { path.Insert(0, cur); cur = prev[cur]; }
                    return path;
                }

                visited[minNode] = true;
                short[] neighbors = Connections[minNode];
                int limit = GetPhysicalWaypointCount(minNode);

                for (int i = 0; i < limit; i++)
                {
                    short n = neighbors[i];
                    if (!visited[n] && (dist[n] == -1 || dist[n] > dist[minNode] + 1))
                    {
                        dist[n] = dist[minNode] + 1;
                        prev[n] = minNode;
                    }
                }
            }
        }

        /// <summary>
        /// Find which waypoint index leads to targetMap.
        /// Uses bb[] index: indexOf(targetMap in bb[currentMap])
        /// </summary>
        /// <summary>
        /// Danh sach map ke cua mot map (chi doc). Tra mang rong neu map ngoai bang.
        /// Dung de chan doan "map khong co canh ra" - bay MOT CHIEU: co canh TRO TOI map do nhung
        /// khong co hang Connections[...] tuong ung -> vao duoc, ra khong duoc, BuildPath tra null.
        /// Phat hien 2026-09-05 qua bang bh[][] cua NSOTRUNGDUC (bh[157] = {158,159}), khi do hoan
        /// lai vi so "them canh server khong co se lam BFS vach duong vao ngo cut".
        /// DA VA 2026-09-06: ZangVPS 2024 (`av_0.java`) xac nhan cung so lieu tu mot nguon doc lap,
        /// va da kiem tra tung map la cac canh them vao khong the doi tuyen dang chay (xem chu
        /// thich tai cho khai bao Connections[120..124] va Connections[157..159]).
        /// Xem docs/features/DI_CHUYEN.md §E.17.
        /// </summary>
        public static short[] GetConnections(int mapId)
        {
            if (mapId < 0 || mapId >= 160) return new short[0];
            return Connections[mapId];
        }

        public static int FindWaypointIndex(int currentMap, int nextMap)
        {
            if (currentMap < 0 || currentMap >= 160) return -1;
            short[] conns = Connections[currentMap];
            for (int i = 0; i < conns.Length; i++)
            {
                if (conns[i] == nextMap)
                    return i;
            }
            return -1;
        }

        /// <summary>Transition action info for navigation.</summary>
        public class TransitionAction
        {
            public bool IsNPC;           // true = NPC teleport, false = waypoint
            public int NpcTemplateId;    // NPC template ID for GameScr.b()
            public int MenuParam1;       // p1 for Service.menu()
            public int MenuParam2;       // p2 for Service.menu()
            public int WaypointIndex;    // bb[] index for waypoint transition
            /// <summary>
            /// Khac null = chon menu THEO CHU ("cap1$cap2") thay vi gui thang MenuParam1/2 - clone
            /// ZangVPS <c>cF.a(npc, "...")</c> (av_0.java:807-878). Khop hong = chang hong, KHONG lui
            /// ve so (Zang cung khong lui).
            /// </summary>
            public string MenuText;
            /// <summary>
            /// Roi Nha thi dau (map 0/56/73) bang cach noi chuyen voi NPC DAU TIEN trong map - clone
            /// ZangVPS av_0.java:1089-1098. Khong co NpcTemplateId co dinh.
            /// </summary>
            public bool IsArenaExit;
        }

        /// <summary>NPC teleport action decoded from TileMap.k() path encoding.</summary>
        public class NpcTransition
        {
            public int NpcTemplateId;
            public int MenuParam1;
            public int MenuParam2;
            public string MenuText;
        }

        /// <summary>
        /// Gia tri <c>i_0.bE</c> cua ZangVPS = menu cap 1 cua NPC 25. Zang khoi tao = 1
        /// (<c>i_0.java:25193</c>) va KHONG gan lai o dau khac (hai khoi <c>bE = 0</c> o
        /// i_0.java:13543/15289 la ham xoa trang do obfuscator sinh ra). User chot 2026-09-13
        /// "dung theo Zang" thay cho cach dem so dong menu server (<c>GameScr.fi</c>).
        /// </summary>
        private const int ZANG_BE = 1;

        /// <summary>
        /// Get transition action for navigating fromMap -> toMap.
        /// Determines if it's NPC teleport or waypoint based on bb[] structure.
        /// Returns TransitionAction with all info needed to execute.
        /// </summary>
        public static TransitionAction GetTransitionAction(int fromMap, int toMap, int questMapId = -1)
        {
            // NPC 25 (nhiem vu hang ngay): Truong -> map nhiem vu. Zang ma hoa (25, bE, 3)
            // (av_0.java:536-543); vi bE == 1 nen Zang LUON di nhanh chon THEO CHU
            // "Nhiệm vụ mỗi ngày$Đi làm NV" (av_0.java:871-878).
            if (questMapId > 0 && toMap == questMapId && IsVillage(fromMap))
            {
                return new TransitionAction
                {
                    IsNPC = true,
                    NpcTemplateId = 25,
                    MenuParam1 = ZANG_BE,
                    MenuParam2 = 3,
                    MenuText = "Nhiệm vụ mỗi ngày$Đi làm NV",
                    WaypointIndex = -1
                };
            }

            // Check NPC transition first
            var npc = GetNpcAction(fromMap, toMap);
            if (npc != null)
            {
                return new TransitionAction
                {
                    IsNPC = true,
                    NpcTemplateId = npc.NpcTemplateId,
                    MenuParam1 = npc.MenuParam1,
                    MenuParam2 = npc.MenuParam2,
                    MenuText = npc.MenuText,
                    WaypointIndex = -1
                };
            }

            // Nha thi dau: KHONG di bo qua cong ma noi chuyen voi NPC dau tien trong map.
            // Zang: `else if (from != 0 && from != 56 && from != 73) { waypoint } else { NPC dau }`.
            if (IsArena(fromMap))
            {
                return new TransitionAction { IsNPC = true, IsArenaExit = true, WaypointIndex = -1 };
            }

            // Waypoint transition: use bb[] index
            int wpIdx = FindWaypointIndex(fromMap, toMap);
            return new TransitionAction
            {
                IsNPC = false,
                WaypointIndex = wpIdx
            };
        }

        /// <summary>
        /// Get NPC action for transitioning between maps.
        /// GameScr.b(npcTemplateId, menuParam1, menuParam2).
        ///
        /// 2026-09-13: bang ma hoa lay theo ZangVPS <c>av_0.java:454-636</c> (khop 240_Tungvz
        /// eh.java:2151-2217 va NINJAPC TileMap.mod.cs:597-703). Ban cu DAO cho so NPC voi menu cap 1
        /// cho cac map dac biet (vd 91 gui NPC 2 menu 0 thay vi NPC 0 menu 2) va sai han map 113.
        /// </summary>
        public static NpcTransition GetNpcAction(int fromMap, int toMap)
        {
            // f() maps: villages {1, 27, 72}
            // Transition between villages uses NPC 8
            // GameScr.b(8, villageIndex, 0)
            if (IsVillage(fromMap) && IsVillage(toMap))
            {
                return new NpcTransition { NpcTemplateId = 8, MenuParam1 = VillageIndex(toMap), MenuParam2 = 0 };
            }

            // f() to special maps via NPC
            if (IsVillage(fromMap))
            {
                switch (toMap)
                {
                    case 80:  return new NpcTransition { NpcTemplateId = 0, MenuParam1 = 1, MenuParam2 = 1 };
                    // Hang dong sau truong: Zang chon THEO CHU (av_0.java:807-864), so chi de tham khao.
                    case 91:  return Hang(1, "Cấp 35");
                    case 94:  return Hang(2, "Cấp 45");
                    case 105: return Hang(3, "Cấp 55");
                    case 114: return Hang(4, "Cấp 65");
                    case 125: return Hang(5, "Cấp 75");
                    case 157: return Hang(6, "Cấp 95");
                    case 139: return new NpcTransition { NpcTemplateId = 5, MenuParam1 = 2, MenuParam2 = 0 };
                    // NPC 25 so (khong co nhanh chu o Zang): 98/104 = (bE+2, 0/1), 113 = (bE+3, 0).
                    case 98:  return new NpcTransition { NpcTemplateId = 25, MenuParam1 = ZANG_BE + 2, MenuParam2 = 0 };
                    case 104: return new NpcTransition { NpcTemplateId = 25, MenuParam1 = ZANG_BE + 2, MenuParam2 = 1 };
                    case 113: return new NpcTransition { NpcTemplateId = 25, MenuParam1 = ZANG_BE + 3, MenuParam2 = 0 };
                }
            }

            // d() maps: crossroads {10,17,22,32,38,43,48}
            // Transition between crossroads uses NPC 7. Zang loai dich 138 (av_0.java:456).
            if (IsCrossroad(fromMap) && IsCrossroad(toMap) && toMap != 138)
            {
                return new NpcTransition { NpcTemplateId = 7, MenuParam1 = CrossroadIndex(toMap), MenuParam2 = 0 };
            }

            // d() to 139
            if (IsCrossroad(fromMap) && toMap == 139)
            {
                return new NpcTransition { NpcTemplateId = 5, MenuParam1 = 2, MenuParam2 = 0 };
            }

            return null;
        }

        /// <summary>Hang dong sau truong - NPC 0, menu (2, k), chon theo chu "Hang động sau trường$Cấp NN".</summary>
        private static NpcTransition Hang(int k, string cap)
        {
            return new NpcTransition
            {
                NpcTemplateId = 0, MenuParam1 = 2, MenuParam2 = k,
                MenuText = "Hang động sau trường$" + cap
            };
        }

        /// <summary>Nha thi dau Haruna 0 / Ookaza 56 / Hirosaki 73.</summary>
        public static bool IsArena(int map) { return map == 0 || map == 56 || map == 73; }

        private static bool IsVillage(int map) { return map == 1 || map == 27 || map == 72; }
        private static bool IsCrossroad(int map)
        {
            return map == 10 || map == 17 || map == 22 || map == 32 ||
                   map == 38 || map == 43 || map == 48 || map == 138;
        }

        private static int VillageIndex(int map)
        {
            if (map == 1) return 0;
            if (map == 27) return 1;
            if (map == 72) return 2;
            return 0;
        }

        private static int CrossroadIndex(int map)
        {
            switch (map)
            {
                case 10: return 1;
                case 17: return 2;
                case 22: return 3;
                case 32: return 4;
                case 38: return 5;
                case 43: return 6;
                case 48: return 7;
                default: return 1;
            }
        }
    }
}
