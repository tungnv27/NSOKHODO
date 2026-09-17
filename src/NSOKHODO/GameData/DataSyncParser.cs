using System.Collections.Generic;
using NSOKHODO.Core;
using NSOKHODO.Models;

namespace NSOKHODO.GameData
{
    public static class DataSyncParser
    {
        /// <summary>
        /// Parse UPDATE_ITEM - chính xác theo game src Controller.cs:3134-3151 createItem().
        /// Lưu ý khác bản cũ (port sai từ NSOAFK): optCount = unsigned BYTE (không phải short),
        /// mỗi option đọc name TRƯỚC type; item count = SHORT; item id = chỉ số vòng lặp
        /// (KHÔNG đọc từ stream); field order theo ctor ItemTemplate(id, type, gender, name,
        /// description, level, iconID, part, isUpToUp).
        /// </summary>
        public static void ParseItemData(BigEndianBinaryReader r, ItemTemplateStore store)
        {
            try
            {
                r.ReadByte(); // vcItem (version)

                int optCount = r.ReadUnsignedByte();
                for (int i = 0; i < optCount; i++)
                {
                    string optName = r.ReadUTF();   // optionName (co dau '#' de thay param)
                    byte optType = r.ReadByte();    // optionType (9 = phan tram)
                    store.AddOption(optName, optType); // truoc day BO -> nay giu de hien chi tiet item
                }

                int count = r.ReadShort();
                for (int i = 0; i < count; i++)
                {
                    var t = new ItemTemplate();
                    t.Id = (short)i;
                    t.Type = r.ReadByte();
                    t.Gender = r.ReadByte();
                    t.Name = r.ReadUTF();
                    t.Description = r.ReadUTF();
                    t.Level = r.ReadByte();
                    t.IconId = r.ReadShort();
                    t.Part = r.ReadShort();
                    t.IsUpToUp = r.ReadBoolean();
                    store.Add(t);
                }
            }
            catch { }
        }

        /// <summary>
        /// Parse UPDATE_SKILL - chính xác theo game src Controller.cs:3153-3208 createSkill().
        /// Bản cũ (port sai từ NSOAFK) thiếu block sOptionTemplates, đọc template id là short
        /// (game dùng BYTE), bỏ qua description và TOÀN BỘ mảng skills[] per-level
        /// (skillId/point/level/manaUse/coolDown/dx/dy/maxFight/options) -> store rỗng
        /// -> bot không biết skill thật của char -> đánh không có sát thương.
        /// </summary>
        public static void ParseSkillData(BigEndianBinaryReader r, SkillTemplateStore store)
        {
            try
            {
                r.ReadByte(); // vcSkill (version)

                int optCount = r.ReadSignedByte();
                if (optCount < 0) optCount += 256;
                for (int i = 0; i < optCount; i++)
                    store.AddOption(r.ReadUTF()); // truoc day BO -> nay giu de hien chi tiet chieu

                int classCount = r.ReadUnsignedByte();
                var classNames = new string[classCount];
                for (int c = 0; c < classCount; c++)
                {
                    // className - giu lai de tab "Thong tin 1" hien duoc dong "Lop: ..."
                    classNames[c] = r.ReadUTF();
                    int tplCount = r.ReadSignedByte();
                    if (tplCount < 0) tplCount += 256;
                    for (int i = 0; i < tplCount; i++)
                    {
                        var t = new SkillTemplate();
                        t.Id = (short)r.ReadUnsignedByte(); // BYTE trong wire format!
                        t.Name = r.ReadUTF();
                        t.MaxPoint = r.ReadByte();
                        t.Type = r.ReadByte();
                        t.IconId = r.ReadShort();
                        t.Description = r.ReadUTF();
                        t.ClassId = c;   // KHONG co tren day - la vi tri trong cay, phai ghi tay
                        store.AddTemplate(t);

                        int levelCount = r.ReadSignedByte();
                        if (levelCount < 0) levelCount += 256;
                        for (int l = 0; l < levelCount; l++)
                        {
                            var s = new Skill();
                            s.SkillId = r.ReadShort();
                            s.Template = t;
                            s.Point = r.ReadByte();
                            s.Level = r.ReadByte();
                            s.ManaUse = r.ReadShort();
                            s.CoolDown = r.ReadInt();
                            s.Dx = r.ReadShort();
                            s.Dy = r.ReadShort();
                            s.MaxFight = r.ReadByte();

                            int nOption = r.ReadSignedByte();
                            if (nOption < 0) nOption += 256;
                            if (nOption > 0) s.Options = new List<SkillOption>(nOption);
                            for (int o = 0; o < nOption; o++)
                            {
                                short param = r.ReadShort();      // option param
                                int optId = r.ReadUnsignedByte(); // option template index
                                s.Options.Add(new SkillOption(optId, param));
                            }
                            store.AddSkill(s);
                        }
                    }
                }
                store.SetClassNames(classNames);
            }
            catch { }
        }

        /// <summary>
        /// Parse UPDATE_MAP data sync - chính xác theo game src Controller.cs:3210-3248 createMap().
        /// Trước đây bot dùng sai kiểu (ReadUnsignedShort thay ReadUnsignedByte) → exception bị nuốt
        /// → mapStore/npcStore trống → bot không match được tên map và menu NPC.
        /// </summary>
        public static void ParseMapData(BigEndianBinaryReader r, MapTemplateStore mapStore,
            MobTemplateStore mobStore, NpcTemplateStore npcStore)
        {
            try
            {
                r.ReadByte(); // vcMap

                // Map names: count = unsigned byte
                int mapCount = r.ReadUnsignedByte();
                for (int i = 0; i < mapCount; i++)
                {
                    string name = r.ReadUTF();
                    mapStore.Add(i, name);
                }

                // NPC templates: count = byte (signed)
                int npcCount = r.ReadSignedByte();
                if (npcCount < 0) npcCount += 256; // treat as unsigned
                for (int i = 0; i < npcCount; i++)
                {
                    var t = new NpcTemplate();
                    t.Id = (short)i;
                    t.Name = r.ReadUTF();
                    t.HeadId = r.ReadShort();
                    t.BodyId = r.ReadShort();
                    t.LegId = r.ReadShort();
                    int menuCount = r.ReadUnsignedByte();
                    t.Menu = new List<string[]>(menuCount);
                    for (int j = 0; j < menuCount; j++)
                    {
                        int subCount = r.ReadUnsignedByte();
                        var sub = new string[subCount];
                        for (int k = 0; k < subCount; k++)
                            sub[k] = r.ReadUTF();
                        t.Menu.Add(sub);
                    }
                    npcStore.Add(t);
                }

                // Mob templates: count = UNSIGNED BYTE (xac nhan MODGAME Controller.java:3092
                // `new MobTemplate[readUnsignedByte()]` - server NAY dung 1 byte, KHONG phai short).
                // Bug cu (doc short = 2 byte) lam le 1 byte -> toan bo mob template thanh RAC ->
                // tpl.Hp sai -> phan loai Tinh Anh/Thu Linh (maxHp == 10x/100x tpl.Hp) khong bao gio khop.
                int mobCount = r.ReadUnsignedByte();
                for (int i = 0; i < mobCount; i++)
                {
                    var t = new MobTemplate();
                    t.Id = (short)i;
                    t.Type = r.ReadByte();
                    t.Name = r.ReadUTF();
                    t.Hp = r.ReadInt();
                    r.ReadByte(); // rangeMove
                    r.ReadByte(); // speed
                    mobStore.Add(t);
                }
            }
            catch { }
        }
    }
}
