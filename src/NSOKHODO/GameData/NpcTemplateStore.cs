using System.Collections.Generic;
using NSOKHODO.Models;

namespace NSOKHODO.GameData
{
    public class NpcTemplateStore
    {
        private readonly Dictionary<short, NpcTemplate> _templates = new Dictionary<short, NpcTemplate>();

        public void Add(NpcTemplate t)
        {
            if (t != null) _templates[t.Id] = t;
        }

        public NpcTemplate Get(short id)
        {
            NpcTemplate t;
            return _templates.TryGetValue(id, out t) ? t : null;
        }

        public NpcTemplate Get(int id)
        {
            return Get((short)id);
        }

        public int Count { get { return _templates.Count; } }

        public void Clear()
        {
            _templates.Clear();
        }
    }
}
