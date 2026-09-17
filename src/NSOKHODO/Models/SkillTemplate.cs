namespace NSOKHODO.Models
{
    /// <summary>
    /// Template skill (1 ô skill trên bảng skill) - khớp game SkillTemplate.cs.
    /// Id là BYTE trong wire format (Controller.cs:3173) và là giá trị gửi đi
    /// trong SELECT_SKILL cmd 41 (Service.cs:730 writeShort(skillTemplateId)).
    /// Type==1 = skill tấn công click-use (điều kiện để Char.isAttack đánh được).
    /// </summary>
    public class SkillTemplate
    {
        public short Id { get; set; }
        public string Name { get; set; }
        public byte MaxPoint { get; set; }
        public byte Type { get; set; }
        public short IconId { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Lop so huu chieu nay (= chi so vong lap class trong DataSync, dung he so voi
        /// <c>CharacterState.ClassId</c> va cmd 93). Wire KHONG gui truong nay — no la vi tri
        /// trong cay du lieu, phai ghi lai luc parse.
        ///
        /// Can vi tab "Ky nang" bay TOAN BO bang chieu cua lop (ke ca chieu chua hoc) y nhu game
        /// (`GameScr.paintKyNang` duyet `nClass.skillTemplates`), ma id template la DUY NHAT TOAN
        /// CUC (do bang skill that cua server: lop 1 = 1..9,55,61,67,73,79; lop 2 = 10..18,56,...)
        /// nen khong the suy ra lop tu id.
        /// </summary>
        public int ClassId { get; set; }

        public override string ToString()
        {
            return string.Format("Tpl[{0}] {1} type={2}", Id, Name ?? "", Type);
        }
    }
}
