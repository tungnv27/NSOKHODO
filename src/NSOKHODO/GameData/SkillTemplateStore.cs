using System.Collections.Generic;
using NSOKHODO.Models;

namespace NSOKHODO.GameData
{
    /// <summary>
    /// Lưu skill data từ DataSync UPDATE_SKILL. Hai map:
    ///  - template theo template id (bảng skill mỗi class)
    ///  - skill theo per-level skillId (tương đương game Skills.get(skillId))
    /// Char-info (-125) chỉ gửi danh sách per-level skillId -> phải tra qua đây
    /// mới biết template/type/point/dx/dy của skill char sở hữu.
    /// </summary>
    public class SkillTemplateStore
    {
        private readonly Dictionary<short, SkillTemplate> _templates = new Dictionary<short, SkillTemplate>();
        private readonly Dictionary<short, Skill> _skillsByLevelId = new Dictionary<short, Skill>();
        private readonly object _lock = new object();

        // Nap xong -> dong bang -> luot DOC khong can khoa nua.
        // Can thiet vi kho nay duoc DUNG CHUNG giua nhieu account (SharedGameData): TrainMode tra
        // skill moi tick, 600 account x 20 tick/s x nhieu skill = hang tram nghin luot doc/giay.
        // Neu van khoa thi 600 thread tranh nhau DUNG MOT khoa (truoc day moi account 1 kho rieng
        // nen khong ai tranh ai). Sau Freeze khong con ghi nua nen doc Dictionary tran la an toan;
        // _frozen la volatile nen dam bao thay day du du lieu da ghi truoc do.
        private volatile bool _frozen;

        /// <summary>Chot kho lai sau khi DataSync xong (goi truoc khi chia se cho account khac).</summary>
        public void Freeze() { _frozen = true; }

        // Ten cac LOP (Kiem/Kunai/Cung/...) theo dung thu tu DataSync gui - chi so o day chinh la
        // `classId` server dung trong cmd 93 (game: GameScr.nClasss[i].classId = i). Chi ghi 1 lan
        // luc parse DataSync, truoc Freeze; sau do chi doc.
        private string[] _classNames = new string[0];

        public void SetClassNames(string[] names)
        {
            _classNames = names ?? new string[0];
        }

        /// <summary>Ten lop theo classId; chuoi rong neu chua co du lieu.</summary>
        public string ClassName(int classId)
        {
            var a = _classNames;
            return (classId >= 0 && classId < a.Length) ? a[classId] : "";
        }

        // Ten cac option skill (co dau '#' de thay param) - chi so = optionTemplateId trong wire.
        // Cung khuon ItemTemplateStore._options. Truoc 2026-09-07 khoi nay bi doc roi VUT.
        private readonly List<string> _optionNames = new List<string>();

        // Bang chieu theo LOP, GIU NGUYEN THU TU server gui - tab "Ky nang" bay dung thu tu do
        // (game duyet nClass.skillTemplates tuan tu). Khong dung _templates.Values vi Dictionary
        // khong bao dam thu tu.
        private readonly Dictionary<int, List<SkillTemplate>> _byClass = new Dictionary<int, List<SkillTemplate>>();

        public void AddOption(string name)
        {
            lock (_lock) _optionNames.Add(name);
        }

        /// <summary>Ten option theo id; null neu ngoai bang.</summary>
        public string GetOptionName(int id)
        {
            var a = _optionNames;
            if (_frozen) return (id >= 0 && id < a.Count) ? a[id] : null;
            lock (_lock) return (id >= 0 && id < a.Count) ? a[id] : null;
        }

        /// <summary>
        /// Toan bo bang chieu cua 1 lop, dung thu tu server gui. Tra mang rong neu chua co du lieu
        /// (khong tra null de cho goi khoi phai kiem tra).
        /// </summary>
        public List<SkillTemplate> TemplatesOfClass(int classId)
        {
            List<SkillTemplate> list;
            if (_frozen)
                return _byClass.TryGetValue(classId, out list) ? list : new List<SkillTemplate>();
            lock (_lock)
                return _byClass.TryGetValue(classId, out list) ? new List<SkillTemplate>(list) : new List<SkillTemplate>();
        }

        public void AddTemplate(SkillTemplate t)
        {
            lock (_lock)
            {
                _templates[t.Id] = t;
                List<SkillTemplate> list;
                if (!_byClass.TryGetValue(t.ClassId, out list))
                {
                    list = new List<SkillTemplate>();
                    _byClass[t.ClassId] = list;
                }
                list.Add(t);
            }
        }

        // Giữ tên Add cũ để tương thích chỗ gọi khác (nếu có).
        public void Add(SkillTemplate t) { AddTemplate(t); }

        public void AddSkill(Skill s)
        {
            lock (_lock)
            {
                _skillsByLevelId[s.SkillId] = s;
                if (s.Template != null && s.Point <= 255)
                    _skillByTplPoint[TplPointKey(s.Template.Id, s.Point)] = s;
            }
        }

        public SkillTemplate Get(short templateId)
        {
            SkillTemplate t;
            if (_frozen)
            {
                _templates.TryGetValue(templateId, out t);
                return t;
            }
            lock (_lock)
            {
                _templates.TryGetValue(templateId, out t);
                return t;
            }
        }

        /// <summary>Tra skill theo per-level skillId - tương đương game Skills.get(skillId).</summary>
        public Skill GetSkill(short skillId)
        {
            Skill s;
            if (_frozen)
            {
                _skillsByLevelId.TryGetValue(skillId, out s);
                return s;
            }
            lock (_lock)
            {
                _skillsByLevelId.TryGetValue(skillId, out s);
                return s;
            }
        }

        /// <summary>
        /// Level NHÂN VẬT cần để HỌC skill (lv gốc) = LEVELNEED của entry học đầu tiên
        /// (point>=1, lấy min level) trong bảng per-level của template.
        /// Game lưu mỗi point 1 entry với level riêng (GameScr.cs:12342 paintSkillInfo);
        /// entry point cao có level cao = "lv cần để cộng thêm điểm", KHÔNG phải lv học.
        /// Với 7 skill đánh là 10/20/30/50/70/80/100. Trả 0 nếu không có dữ liệu.
        /// </summary>
        public int GetLearnLevel(short templateId)
        {
            if (_frozen) return LearnLevelCore(templateId);
            lock (_lock) return LearnLevelCore(templateId);
        }

        // Tra nhanh "chieu X cap Y" -> Skill. Khoa = (templateId << 8) | point; an toan vi
        // templateId <= 84 va point <= maxPoint (~20) trong bang skill that cua server.
        // Dung Dictionary chu khong quet tuyen tinh vi luoi tab "Ky nang" hoi CAP KE cho ca 14
        // chieu moi lan ve (400 ms/lan khi popup mo) - quet ~1.000 ban ghi x 14 la lang phi vo co.
        private readonly Dictionary<int, Skill> _skillByTplPoint = new Dictionary<int, Skill>();

        private static int TplPointKey(short templateId, int point)
        {
            return (templateId << 8) | (point & 0xFF);
        }

        /// <summary>
        /// Ban ghi cap co dung <paramref name="point"/> diem cua mot template; null neu khong co.
        /// Tab "Ky nang" dung de lay chi so cua CAP KE (game luon bay "cap ke: ..." de biet nang len
        /// duoc gi).
        /// </summary>
        public Skill FindSkillByPoint(short templateId, int point)
        {
            if (point < 0 || point > 255) return null;
            Skill s;
            int k = TplPointKey(templateId, point);
            if (_frozen) { _skillByTplPoint.TryGetValue(k, out s); return s; }
            lock (_lock) { _skillByTplPoint.TryGetValue(k, out s); return s; }
        }

        private int LearnLevelCore(short templateId)
        {
            int min = 0;
            foreach (var s in _skillsByLevelId.Values)
            {
                if (s == null || s.Template == null || s.Template.Id != templateId) continue;
                if (s.Point < 1) continue; // bỏ entry point=0 (placeholder, chưa học)
                if (min == 0 || s.Level < min) min = s.Level;
            }
            return min;
        }

        public int Count
        {
            get { if (_frozen) return _templates.Count; lock (_lock) return _templates.Count; }
        }

        public int SkillCount
        {
            get { if (_frozen) return _skillsByLevelId.Count; lock (_lock) return _skillsByLevelId.Count; }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _templates.Clear();
                _skillsByLevelId.Clear();
                _optionNames.Clear();
                _byClass.Clear();
                _skillByTplPoint.Clear();
            }
        }
    }
}
