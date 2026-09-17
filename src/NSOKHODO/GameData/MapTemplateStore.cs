using System.Collections.Generic;

namespace NSOKHODO.GameData
{
    public class MapTemplateStore
    {
        private readonly Dictionary<int, string> _mapNames = new Dictionary<int, string>();

        public void Add(int mapId, string name)
        {
            _mapNames[mapId] = name;
        }

        public string GetName(int mapId)
        {
            string name;
            if (_mapNames.TryGetValue(mapId, out name))
                return name;
            return "Map " + mapId;
        }

        public int Count { get { return _mapNames.Count; } }

        public void Clear()
        {
            _mapNames.Clear();
        }
    }
}
