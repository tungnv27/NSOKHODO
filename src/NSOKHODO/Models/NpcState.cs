namespace NSOKHODO.Models
{
    public class NpcState
    {
        public short TemplateId { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public byte Status { get; set; }
        public byte HeadId { get; set; }
        public byte BodyId { get; set; }
        public byte LegId { get; set; }
    }
}
