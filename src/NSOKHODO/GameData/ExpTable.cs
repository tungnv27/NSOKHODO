namespace NSOKHODO.GameData
{
    public static class ExpTable
    {
        // Bang exps[level] tu DataSync (UPDATE_DATA). exps[0] CO THE = 0 (cap 0->1 mien phi).
        private static long[] _table = new long[0];
        private static bool _loaded;

        public static void SetTable(long[] table)
        {
            if (table == null || table.Length == 0) return;
            bool anyPos = false;
            for (int i = 0; i < table.Length; i++)
                if (table[i] > 0) { anyPos = true; break; }
            if (!anyPos) return; // toan 0 -> bo
            _table = table;
            _loaded = true;
        }

        /// <summary>True khi da nap bang exps thuc.</summary>
        public static bool HasTable { get { return _loaded; } }

        public static long GetExpForLevel(int level)
        {
            if (level >= 0 && level < _table.Length)
                return _table[level];
            return 0;
        }

        /// <summary>
        /// Tong exp tich luy de DUNG O DAU cap `level` (= game getMaxExp(level-1) = sum exps[0..level-1]).
        /// Dung khi clone cmd 72 (PK am mat exp): server keo cEXP ve dau cap hien tai roi gui cExpDown (so thieu).
        /// </summary>
        public static long GetTotalExpForLevelStart(int level)
        {
            if (!_loaded || level <= 0) return 0;
            int n = level < _table.Length ? level : _table.Length;
            long sum = 0;
            for (int i = 0; i < n; i++) sum += _table[i];
            return sum;
        }

        /// <summary>
        /// Tinh level tu total exp - giong GameScr.getLevelExp: tru dan tung cap.
        /// (exps[i] = 0 chi nghia cap do mien phi, van tien toi.)
        /// </summary>
        public static int GetLevelFromExp(long totalExp)
        {
            if (!_loaded) return 0;
            long remaining = totalExp;
            int level = 0;
            for (int i = 0; i < _table.Length; i++)
            {
                if (remaining < _table[i]) break;
                remaining -= _table[i];
                level = i + 1;
            }
            return level;
        }

        /// <summary>
        /// Tach total exp thanh (level, exp con lai trong level, exp can de len cap). Tra false neu chua co bang.
        /// </summary>
        public static bool TryGetProgress(long totalExp, out int level, out long remainder, out long needForLevel)
        {
            level = 0;
            remainder = totalExp;
            needForLevel = 0;
            if (!_loaded) return false;
            for (int i = 0; i < _table.Length; i++)
            {
                if (remainder < _table[i]) { level = i; needForLevel = _table[i]; return true; }
                remainder -= _table[i];
                level = i + 1;
            }
            // Da max level: lay cap cuoi lam mau so
            needForLevel = _table[_table.Length - 1];
            return needForLevel > 0;
        }

        /// <summary>
        /// % EXP trong cap hien tai - clone CHINH XAC cong thuc game (GameScr.cs:6073-6075 NSOTool 251):
        ///   num2 = (cExpDown &lt;= 0) ? cExpR*10000/exps[clevel] : cExpDown*10000/exps[clevel]
        ///   hien thi = (cExpDown &lt;= 0 ? "" : "-") + num2/100 + "." + (num2%100 hai chu so) + "%"
        /// Tuc: khi KHONG am (expDown &lt;= 0, dang train) -> % DUONG = remainder/need.
        /// Khi AM (expDown &gt; 0, vua mat exp khi PK) -> % AM = -expDown/need.
        /// remainder = cExpR, need = exps[clevel] (deu lay tu TryGetProgress = getLevelExp cua game).
        /// Tra so da lam tron 2 chu so giong game (vd 49.99, -48.39).
        /// </summary>
        public static bool TryGetPercent(long totalExp, long expDown, out double percent)
        {
            percent = 0;
            int level; long remainder, need;
            if (!TryGetProgress(totalExp, out level, out remainder, out need) || need <= 0)
                return false;
            long num2 = (expDown <= 0) ? (remainder * 10000L / need) : (expDown * 10000L / need);
            double mag = num2 / 100.0; // giu 2 chu so thap phan kieu truncate giong game
            percent = (expDown <= 0) ? mag : -mag;
            return true;
        }

        /// <summary>
        /// Chuoi % EXP GIONG HET game - clone CHINH XAC 251 GameScr.cs:6072-6075:
        ///   num2 = (cExpDown&lt;=0 ? cExpR : cExpDown) * 10000 / exps[clevel];  (long, chia NGUYEN)
        ///   num3 = num2 % 100;  // 2 chu so thap phan
        ///   hien = (cExpDown&lt;=0 ? "" : "-") + num2/100 + "." + (num3&gt;=10 ? num3 : "0"+num3) + "%"
        /// Truncate (KHONG lam tron), luon 2 chu so thap phan, dau "-" khi dang no exp.
        /// Vd "45.32", "-19.74". Tra null neu chua co bang exps. KHONG kem dau "%".
        /// </summary>
        public static string TryGetPercentText(long totalExp, long expDown)
        {
            int level; long remainder, need;
            if (!TryGetProgress(totalExp, out level, out remainder, out need) || need <= 0)
                return null;
            long num2 = (expDown <= 0) ? (remainder * 10000L / need) : (expDown * 10000L / need);
            long num3 = num2 % 100;
            return (expDown <= 0 ? "" : "-") + (num2 / 100) + "." + (num3 >= 10 ? num3.ToString() : "0" + num3);
        }

        /// <summary>
        /// "Diem tien do" de do TOC DO len exp: <c>cap * 100 + % trong cap</c>. Mot cap = 100 diem.
        ///
        /// Ly do khong lay thang hieu cEXP: cEXP la exp TICH LUY nen hieu cua no la so exp THO,
        /// ma exp moi cap can lai khac nhau -> khong so sanh duoc giua cac cap. Con lay thang hieu
        /// cua % trong cap thi LEN CAP se ra so AM (vi % tut ve ~0). Cong 100 diem moi cap lam
        /// day so nay TANG DEU qua ranh gioi cap: len cap = tu dong cong not phan % con thieu cua
        /// cap cu + phan % da di cua cap moi.
        ///
        /// 🔑 Ca 4 tool tham khao (MODGAME GameScr:5470, NSOTool ManagerServer:1160) deu lay thang
        /// hieu cEXP roi chia exps[cap] -> len cap la ra %/h AM; NSOTool giau bang cach chi hien
        /// khi &gt; 0. Day la cho ta lam KHAC (va dung hon) so voi ban goc.
        ///
        /// KHONG dinh dang gi den <c>expDown</c> (exp am khi PK): diem tien do bam theo cEXP that,
        /// nen luc server keo cEXP ve dau cap thi diem TUT -> ty le ra so am, dung nhu user chot.
        /// </summary>
        public static bool TryGetProgressPoints(long totalExp, out double points)
        {
            points = 0;
            int level; long remainder, need;
            if (!TryGetProgress(totalExp, out level, out remainder, out need) || need <= 0)
                return false;
            points = level * 100.0 + (remainder * 100.0 / need);
            return true;
        }
    }
}
