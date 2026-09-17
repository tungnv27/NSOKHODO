namespace NSOKHODO.Models
{
    /// <summary>
    /// 1 dong option/tinh nang cua item trang bi (vd "Suc manh +30"). Server tra qua cmd 42:
    /// OptionId tro vao bang option-template (ten co dau '#'), Param la gia tri thay vao '#'.
    /// </summary>
    public class ItemOption
    {
        public int OptionId { get; set; }
        public int Param { get; set; }

        public ItemOption(int optionId, int param)
        {
            OptionId = optionId;
            Param = param;
        }
    }

    /// <summary>Template option: ten co dau '#' + type (9 = phan tram). Nap tu DataSync (UPDATE_ITEM).</summary>
    public class ItemOptionTemplate
    {
        public string Name { get; set; }
        public byte Type { get; set; }
    }
}
