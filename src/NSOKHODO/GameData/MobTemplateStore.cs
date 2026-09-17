using System.Collections.Generic;
using NSOKHODO.Models;

namespace NSOKHODO.GameData
{
    public class MobTemplateStore
    {
        private readonly Dictionary<short, MobTemplate> _mobs = new Dictionary<short, MobTemplate>();

        public void Add(MobTemplate mob)
        {
            _mobs[mob.Id] = mob;
        }

        public MobTemplate Get(short id)
        {
            MobTemplate t;
            _mobs.TryGetValue(id, out t);
            return t;
        }

        public int Count { get { return _mobs.Count; } }

        public void Clear()
        {
            _mobs.Clear();
        }
    }
}
