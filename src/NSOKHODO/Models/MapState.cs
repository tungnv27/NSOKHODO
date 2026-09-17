using System;
using System.Collections.Generic;

namespace NSOKHODO.Models
{
    public class ItemOnMap
    {
        public short ItemMapId { get; set; }
        public short TemplateId { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        // Clone MODGAME ItemMap.k/.l: da gui lenh nhat -> khong thu lai NGAY.
        // Truoc day cai nay VINH VIEN: item nhat hut (dang bao ho nguoi khac, char chua toi kip,
        // tui vua day roi trong lai) mat luon vi ItemsOnMap chi bi xoa khi server bao (-15/-13)
        // hoac doi map. Ninja2 (Class_dh.java:85-88) mo lai sau 10s -> clone co che do.
        public bool Tried { get; set; }
        /// <summary>Moc thoi diem danh dau Tried (UTC) - de mo lai sau RETRY_AFTER_MS.</summary>
        public DateTime TriedAt { get; set; }

        /// <summary>Cua so khoa sau 1 lan nhat hut (clone ninja2 Class_dh: 10 giay).</summary>
        public const int RETRY_AFTER_MS = 10000;

        /// <summary>Danh dau da thu nhat (kem moc thoi gian de con mo lai).</summary>
        public void MarkTried()
        {
            Tried = true;
            TriedAt = DateTime.UtcNow;
        }

        /// <summary>Con trong cua so khoa khong? Het 10s thi tu mo de thu lai.</summary>
        public bool IsLocked()
        {
            if (!Tried) return false;
            if ((DateTime.UtcNow - TriedAt).TotalMilliseconds < RETRY_AFTER_MS) return true;
            Tried = false;   // het cua so -> mo lai
            return false;
        }
    }

    public class MapState
    {
        public int MapId { get; set; }
        public int TileId { get; set; }
        public int BgId { get; set; }
        public byte TypeMap { get; set; }
        public string MapName { get; set; }
        public byte ZoneId { get; set; }

        public List<Waypoint> Waypoints { get; set; }
        public MobState[] Mobs { get; set; }
        public List<NpcState> Npcs { get; set; }
        public List<ItemOnMap> ItemsOnMap { get; set; }
        public List<PlayerInfo> OtherPlayers { get; set; }

        public MapState()
        {
            Waypoints = new List<Waypoint>();
            Mobs = new MobState[0];
            Npcs = new List<NpcState>();
            ItemsOnMap = new List<ItemOnMap>();
            OtherPlayers = new List<PlayerInfo>();
        }

        // ItemsOnMap bi ghi tu thread nhan packet + doc tu thread auto -> moi truy cap qua
        // 3 helper nay (lock chung 1 object). Dedupe theo ItemMapId (clone MODGAME loadInfoMap).
        public void AddItemOnMap(ItemOnMap item)
        {
            if (item == null) return;
            lock (ItemsOnMap)
            {
                for (int i = 0; i < ItemsOnMap.Count; i++)
                    if (ItemsOnMap[i].ItemMapId == item.ItemMapId) return;
                ItemsOnMap.Add(item);
            }
        }

        public void RemoveItemOnMap(short itemMapId)
        {
            lock (ItemsOnMap)
                ItemsOnMap.RemoveAll(i => i.ItemMapId == itemMapId);
        }

        public List<ItemOnMap> SnapshotItems()
        {
            lock (ItemsOnMap)
                return new List<ItemOnMap>(ItemsOnMap);
        }

        public bool ContainsItem(short itemMapId)
        {
            lock (ItemsOnMap)
            {
                for (int i = 0; i < ItemsOnMap.Count; i++)
                    if (ItemsOnMap[i].ItemMapId == itemMapId) return true;
                return false;
            }
        }

        public void Reset()
        {
            // Khoa tren chinh List: luong AUTO duyet Waypoints (Navigator.TryLearnWaypoint /
            // DoWaypointWalk) trong khi luong NHAN goi Reset() luc doi map -> TOCTOU. Ben doc chup
            // snapshot trong cung khoa nay. Quy tac "snapshot mang truoc khi lap" cua CLAUDE.md.
            lock (Waypoints) Waypoints.Clear();
            Mobs = new MobState[0];
            Npcs.Clear();
            lock (ItemsOnMap) ItemsOnMap.Clear();
            OtherPlayers.Clear();
        }
    }
}
