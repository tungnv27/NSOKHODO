namespace NSOKHODO.Models
{
    /// <summary>
    /// Skill theo từng level (1 entry trong skills[] của template) - khớp game Skill.cs.
    /// SkillId là id duy nhất toàn cục (short) - char-info (-125) gửi danh sách các
    /// SkillId này, client tra Skills.get(skillId) để lấy đủ dx/dy/coolDown.
    /// </summary>
    public class Skill
    {
        public short SkillId { get; set; }
        public SkillTemplate Template { get; set; }
        public byte Point { get; set; }
        public byte Level { get; set; }
        public short ManaUse { get; set; }
        public int CoolDown { get; set; }
        public short Dx { get; set; }
        public short Dy { get; set; }
        public byte MaxFight { get; set; }

        /// <summary>
        /// Cac dong option cua RIENG cap nay (vd cap 5 cua "Chieu Hiyoko" -> Tan cong ngoai +25%,
        /// Hoa cong +50, ...). Nap tu DataSync; co the null neu cap do khong co option nao.
        /// </summary>
        public System.Collections.Generic.List<SkillOption> Options { get; set; }

        /// <summary>Skill tấn công dùng được - giống điều kiện vSkillFight Controller.cs:4229.</summary>
        public bool IsUsableAttack
        {
            get
            {
                return Template != null && Template.Type == 1
                    && (Template.MaxPoint == 0 || Point > 0);
            }
        }

        public override string ToString()
        {
            return string.Format("Skill[{0}] {1} lv{2} tpl={3} dx={4} dy={5} cd={6} mf={7}",
                SkillId, Template != null ? Template.Name : "?", Level,
                Template != null ? Template.Id : -1, Dx, Dy, CoolDown, MaxFight);
        }
    }
}
