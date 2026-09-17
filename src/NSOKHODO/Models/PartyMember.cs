namespace NSOKHODO.Models
{
    public class PartyMember
    {
        public int CharId { get; set; }
        public byte ClassId { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return string.Format("{0} (class={1})", Name, ClassId);
        }
    }
}
