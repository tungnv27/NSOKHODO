using System.Collections.Generic;

namespace NSOKHODO.GameData
{
    /// <summary>
    /// TIM DUONG "CHI DI NGANG + ROI" trong mot map - buoc dem cho <c>Navigator.CharBurstMove</c> khi dich
    /// nam o TANG NEN THAP HON cho dang dung. Xem docs/features/TRUC_Y.md ty3.
    ///
    /// <para><b>Vi sao can:</b> CharBurstMove chi buoc truc X, Y cua MOI goi trung gian ghim theo tang
    /// cua DICH (<c>FindGround(curX, adjustedY)</c>) => goi dau tien da khai "roi" 240-480 px mot phat,
    /// server bac va keo ve. Client that khong bi vi co vong vat ly (Char.update) cho nhan vat di toi
    /// mep, roi xuong, di tiep. Do tren log 2026-09-13 (dan 120 acc): 10 acc Luu toa do ket o map 35
    /// Hang Meiro, cong ra (36,672) nam o DAY, acc dung o tang 192/312/552 -
    /// <c>[Watchdog] DONG BANG: server keo ve (1580,312)</c>.</para>
    ///
    /// <para><b>Vi sao phai TIM DUONG, khong "di thang toi mep roi tut":</b> tang nen co TUONG. Map 35:
    /// dung (1580,312) thi lo ben trai bi tuong cot 57 chan, phai sang PHAI toi lo x 1704 du cong ra o
    /// ben trai. Di tham lam ve phia dich se dam vao tuong mai mai.</para>
    ///
    /// <para><b>Luat di chuyen - lay tu client goc</b> (NINJAPC <c>NinjaSchool_251_src/Char.cs</c>):</para>
    /// <list type="bullet">
    /// <item>Dung duoc = o duoi chan co <c>T_TOP</c> (2) - Char.cs:2638 (<c>!tileTypeAt(cx, cy, T_TOP)</c> =&gt; roi).</item>
    /// <item>Tuong: sang phai bi chan khi o NGANG THAN <c>(cx + chw, cy - chh)</c> co <c>T_LEFT</c> (4);
    ///   sang trai bi chan khi <c>(cx - chw - 1, cy - chh)</c> co <c>T_RIGHT</c> (8) - Char.cs:3017/3048,
    ///   chh = 16 =&gt; dung hang NGAY TREN o chan.</item>
    /// <item>Roi: xuong toi o <c>T_TOP</c> DAU TIEN ben duoi (ke ca san mot chieu chi co co 2) - Char.cs:3421.</item>
    /// <item><b>KHONG LEO LEN</b> (user chot 2026-09-13: chi sua chieu xuong, chieu len giu nguyen).
    ///   Duong nao can nhay len mot bac la KHONG co duong =&gt; caller giu cach cu.</item>
    /// </list>
    ///
    /// <para>BFS theo O (24 px): nut = o dung duoc, canh = buoc sang cot ben canh (co the kem mot cu roi).
    /// Moi canh ton 1 cot nen BFS cho duong it buoc ngang nhat.</para>
    /// </summary>
    public static class FloorPath
    {
        private const int TILE = 24;
        private const int T_TOP = 2;
        private const int T_LEFT = 4;
        private const int T_RIGHT = 8;

        /// <summary>Diem xuat phat lech tren nen toi da 2 o (vi tri server dat co the khong tron o).</summary>
        private const int SNAP_START_ROWS = 2;

        /// <summary>Dich lech tren nen toi da 4 o = 96 px - cung bien quet xuong cua SnapToGround / TryFindStandable.</summary>
        private const int SNAP_GOAL_ROWS = 4;

        // Bo dem dung lai THEO LUONG: TrainMode.MoveToMob goi burst ~10 lan/giay moi acc, moi acc mot luong;
        // quai di chuyen lam cache o Navigator it trung => cap moi mang w*h moi lan la rac GC deu dan tren
        // ca dan. ThreadStatic de HeNen/KichYen goi tu luong khac khong dung chung mang.
        [System.ThreadStatic] private static int[] _prevBuf;
        [System.ThreadStatic] private static Queue<int> _qBuf;

        /// <summary>Mot diem moc pixel tren duong di.</summary>
        public struct DiemMoc
        {
            public readonly int X;
            public readonly int Y;
            public DiemMoc(int x, int y) { X = x; Y = y; }
        }

        /// <summary>
        /// Tim duong tu (sx, sy) toi (gx, gy) chi gom di ngang va roi xuong.
        /// Tra ve cac diem moc CUA NHUNG CU ROI, theo thu tu: moi cu roi la HAI diem
        /// (x cot lo, Y tang cu) roi (x cot lo, Y tang moi). Caller di ngang toi diem dau, roi thang
        /// xuong diem sau, lap lai; het danh sach thi di ngang toi dich tren tang cuoi (<paramref name="nenDichY"/>).
        /// <c>null</c> = khong co duong (can leo, bi tuong bit, dich khong co nen, hoac dich khong thap hon).
        /// </summary>
        public static List<DiemMoc> TimDuongXuong(TileEngine t, int sx, int sy, int gx, int gy, out int nenDichY)
        {
            nenDichY = gy;
            if (t == null || !t.IsLoaded) return null;
            int w = t.WidthTiles, h = t.HeightTiles;

            int sc = sx / TILE, gc = gx / TILE;
            if (sx < 0 || gx < 0 || sc >= w || gc >= w) return null;
            int sr = NenDuoi(t, sc, sy / TILE, SNAP_START_ROWS, h);
            int gr = NenDuoi(t, gc, gy / TILE, SNAP_GOAL_ROWS, h);
            if (sr < 0 || gr < 0 || gr <= sr) return null;   // chi lo chieu XUONG

            int start = sr * w + sc, goal = gr * w + gc;
            int n = w * h;
            var prev = _prevBuf;   // 0 = chua tham; khac 0 = (o truoc do + 1)
            if (prev == null || prev.Length < n) { prev = new int[n]; _prevBuf = prev; }
            else System.Array.Clear(prev, 0, n);
            var q = _qBuf;
            if (q == null) { q = new Queue<int>(); _qBuf = q; }
            else q.Clear();
            prev[start] = start + 1;
            q.Enqueue(start);

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                if (cur == goal) break;
                int c = cur % w, r = cur / w;

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    int nc = c + dir;
                    if (nc < 0 || nc >= w) continue;

                    // Tuong ngang than (hang r-1) - huong nao chan theo co nay, dung Char.cs:3017/3048.
                    if (r > 0)
                    {
                        int than = t.GetCollision(nc * TILE, (r - 1) * TILE);
                        if (dir > 0 && (than & T_LEFT) != 0) continue;
                        if (dir < 0 && (than & T_RIGHT) != 0) continue;
                    }

                    // Cot ben canh co nen cung hang -> di ngang; khong co -> roi toi nen DAU TIEN ben duoi.
                    int nr = r;
                    if ((t.GetCollision(nc * TILE, r * TILE) & T_TOP) == 0)
                    {
                        nr = -1;
                        for (int rr = r + 1; rr < h; rr++)
                        {
                            if ((t.GetCollision(nc * TILE, rr * TILE) & T_TOP) != 0) { nr = rr; break; }
                        }
                        if (nr < 0) continue;   // vuc khong day - khong di
                    }

                    int nxt = nr * w + nc;
                    if (prev[nxt] != 0) continue;
                    prev[nxt] = cur + 1;
                    q.Enqueue(nxt);
                }
            }
            if (prev[goal] == 0) return null;

            // Dung lai day o tu dich ve xuat phat.
            var cacO = new List<int>();
            for (int o = goal; ; o = prev[o] - 1)
            {
                cacO.Add(o);
                if (o == start) break;
            }
            cacO.Reverse();

            var moc = new List<DiemMoc>();
            for (int i = 1; i < cacO.Count; i++)
            {
                int rTruoc = cacO[i - 1] / w;
                int o = cacO[i];
                int c = o % w, r = o / w;
                if (r == rTruoc) continue;
                int x = c * TILE + TILE / 2;
                moc.Add(new DiemMoc(x, rTruoc * TILE));   // toi mep lo, van o tang cu
                moc.Add(new DiemMoc(x, r * TILE));        // cham nen tang moi
            }
            nenDichY = gr * TILE;
            return moc;
        }

        /// <summary>Hang co nen tai cot c, quet tu hang r xuong toi da <paramref name="soHang"/> hang. -1 = khong co.</summary>
        private static int NenDuoi(TileEngine t, int c, int r, int soHang, int h)
        {
            if (r < 0) r = 0;
            for (int k = 0; k <= soHang && r + k < h; k++)
            {
                if ((t.GetCollision(c * TILE, (r + k) * TILE) & T_TOP) != 0) return r + k;
            }
            return -1;
        }
    }
}
