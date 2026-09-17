using System.Collections.Generic;

namespace NSOKHODO.Models
{
    public class NpcTemplate
    {
        public short Id { get; set; }
        public string Name { get; set; }
        public short HeadId { get; set; }
        public short BodyId { get; set; }
        public short LegId { get; set; }

        // Static menu items từ data sync (game src Controller.cs:3227-3235).
        // Mỗi phần tử ngoài = 1 menu option; phần tử trong = các string của option (chỉ [0] = caption hiển thị).
        public List<string[]> Menu { get; set; }
    }
}
