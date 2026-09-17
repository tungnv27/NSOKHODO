namespace NSOKHODO.GameData
{
    /// <summary>
    /// Bản đồ thế giới của game — ảnh + vị trí từng map, dùng cho hộp thoại "Bản đồ" ở tab Train.
    /// Xem <c>docs/features/BAN_DO.md</c>.
    ///
    /// TẤT CẢ số trong file này là CHÉP NGUYÊN VĂN từ màn hình bản đồ của client gốc
    /// (<c>NINJAPC\NinjaSchool_251_src\MapScr.cs:51-97</c>, giống hệt bản Java 180
    /// <c>MODGAME\src\MapScr.java:41-42</c>) — KHÔNG tự chế, KHÔNG nội suy. Đã kiểm chứng bằng
    /// cách vẽ 73 điểm đè lên ảnh gốc: mọi điểm rơi trúng dấu ✕ đã vẽ sẵn trong ảnh.
    ///
    /// Ảnh nằm ở <c>GameData/Gfx/wm.png</c> (327×301, bung từ <c>x1/wm.png</c> của
    /// <c>NSO180_Tungvz.jar</c>), nhúng thẳng vào EXE/APK — xem 2 file csproj.
    ///
    /// ⚠️ CHỈ map 0..73 có toạ độ. Trong mảng gốc index 74..192 đều là (1,1) = "không có vị trí",
    /// nghĩa là toàn bộ hang cấp cao vào bằng NPC (91 Động bàn tơ, 94 Hang tổ ong, 105 Tam hợp
    /// sơn động, 114, 125, 139 Quỷ Sơn, 140-148, 157-159 Tam Nhân Quan) KHÔNG nằm trên bản đồ.
    /// Đây là giới hạn của chính game, không phải của ta — hộp thoại bù bằng danh sách đủ 160 map.
    /// </summary>
    public static class WorldMap
    {
        /// <summary>Kích thước ảnh wm.png, điểm ảnh.</summary>
        public const int ImgW = 327;
        public const int ImgH = 301;

        /// <summary>
        /// Bán kính bắt điểm khi bấm, chép từ <c>MapScr.findMapNearestPoint</c>:
        /// trúng khi <c>|mfx - x[i]| &lt; 10 &amp;&amp; |mfy - y[i]| &lt; 10</c>.
        /// </summary>
        public const int HitRadius = 10;

        /// <summary>Hoành độ tâm map trên ảnh. Chỉ số mảng = mapId.</summary>
        public static readonly int[] X =
        {
              1, 156, 140, 174, 196, 195, 125, 148, 156, 173,
            199, 203, 222, 264, 283, 277, 298, 307, 311, 315,
            116,  90,  59,  31, 252,  55,  81, 111, 148, 187,
            219, 253, 278, 304, 311, 310, 284, 309, 294,  62,
             92, 117,  99, 134, 154, 175,  34,  52,  40,  78,
             59,  82, 114, 179, 158, 142,   1, 215, 291, 242,
            147, 301,  71,  23, 116, 126, 305, 286, 264,  20,
             46,  70,  78,   2
        };

        /// <summary>Tung độ tâm map trên ảnh. Chỉ số mảng = mapId.</summary>
        public static readonly int[] Y =
        {
              1,  68,  75,  88,  80, 107,  87, 114, 136, 160,
            168, 196, 216, 219, 248, 265, 276, 260, 232, 204,
            111,  82,  79,  59, 168,  33,  28,  34,  45,  20,
             54,  44,  19,  40,  60, 100, 175, 165, 134, 181,
            199, 208, 221, 220, 219, 221, 195, 217, 246, 244,
            250, 263, 262, 241, 252, 244,   2, 240, 197, 139,
             16,  18, 208, 223, 239, 186, 120, 119, 135, 107,
            125, 126, 148,   3
        };

        /// <summary>
        /// Map này có chỗ đứng trên ảnh bản đồ không.
        ///
        /// Client gốc chỉ loại đúng (1,1) (<c>MapScr.cs:331</c> <c>if (x[id] != 1 || y[id] != 1)</c>),
        /// nên nó VẪN vẽ map 56 tại (1,2) và map 73 tại (2,3) — chồng lên nhau ở góc trên trái,
        /// rõ ràng là rác dữ liệu chứ không phải vị trí thật. Ta loại luôn cả 3 (0, 56, 73):
        /// đều là "Nhà thi đấu", không có quái, và vẫn chọn được từ danh sách bên cạnh.
        /// Đây là chỗ DUY NHẤT ta cố ý làm khác client.
        /// </summary>
        public static bool HasPos(int mapId)
        {
            if (mapId < 0 || mapId >= X.Length) return false;
            if (mapId == 0 || mapId == 56 || mapId == 73) return false;
            return !(X[mapId] == 1 && Y[mapId] == 1);
        }

        /// <summary>
        /// Map nào bị bấm trúng tại điểm (px, py) tính theo ĐIỂM ẢNH GỐC của wm.png.
        /// Quét tuyến tính lấy map đầu tiên khớp — y hệt <c>findMapNearestPoint</c>
        /// (tên hàm gốc nói "nearest" nhưng thực chất là "first hit", giữ nguyên để khớp hành vi).
        /// Trả về -1 khi không trúng map nào.
        /// </summary>
        public static int HitTest(int px, int py)
        {
            for (int i = 0; i < X.Length; i++)
            {
                if (!HasPos(i)) continue;
                int dx = px - X[i]; if (dx < 0) dx = -dx;
                int dy = py - Y[i]; if (dy < 0) dy = -dy;
                if (dx < HitRadius && dy < HitRadius) return i;
            }
            return -1;
        }

        /// <summary>Số map thật sự có mặt trên ảnh (dùng cho nhãn phụ của hộp thoại).</summary>
        public static int PlacedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < X.Length; i++) if (HasPos(i)) n++;
                return n;
            }
        }

        /// <summary>
        /// Cách căn chữ tên map, chép <c>MapScr.cs:333</c>: x &lt; 100 căn TRÁI, 100..200 căn GIỮA,
        /// &gt; 200 căn PHẢI (để chữ không tràn ra ngoài mép ảnh). 0 = trái, 1 = giữa, 2 = phải.
        /// </summary>
        public static int LabelAlign(int mapId)
        {
            if (mapId < 0 || mapId >= X.Length) return 1;
            int x = X[mapId];
            if (x < 100) return 0;
            if (x <= 200) return 1;
            return 2;
        }
    }
}
