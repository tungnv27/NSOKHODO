namespace NSOKHODO.Models
{
    /// <summary>
    /// 1 dong option cua MOT CAP skill (vd "Tan cong ngoai: +25%").
    /// Cung khuon <see cref="ItemOption"/>: <c>OptionId</c> tro vao bang ten (co dau '#'),
    /// <c>Param</c> la gia tri thay vao '#'.
    ///
    /// Wire: duoi moi cap skill trong DataSync UPDATE_SKILL co `byte nOption` roi
    /// `nOption x { short param, byte optionTemplateId }`. Truoc 2026-09-07 hai field nay bi
    /// DOC ROI VUT (DataSyncParser.cs:101-107) nen khong hien duoc dong sat thuong cua chieu —
    /// y het loi da vap ben item, cho do da vot lai tu lau (`store.AddOption`).
    /// </summary>
    public class SkillOption
    {
        public int OptionId { get; set; }
        public int Param { get; set; }

        public SkillOption(int optionId, int param)
        {
            OptionId = optionId;
            Param = param;
        }
    }
}
