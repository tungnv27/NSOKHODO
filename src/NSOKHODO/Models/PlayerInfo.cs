using System.Collections.Generic;

namespace NSOKHODO.Models
{
    public class PlayerInfo
    {
        public int CharId { get; set; }
        public string Name { get; set; }
        public string ClanName { get; set; }
        public byte ClanType { get; set; }
        public byte Gender { get; set; }
        public short Head { get; set; }
        public byte Speed { get; set; }
        public byte ClassId { get; set; }
        public byte Pk { get; set; }
        public byte TypePk { get; set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int MaxMp { get; set; }
        public int Mp { get; set; }
        public byte Level { get; set; }
        public short X { get; set; }
        public short Y { get; set; }

        // Chi so PART than/chan/vu khi - server gui san trong cmd 3 PLAYER_ADD, truoc day doc roi VUT
        // (MapHandler.ReadCharInfo). Giu lai de cua so "Xem game" ve duoc quan ao nguoi khac.
        // 0 goi tin phat sinh: chi la thoi nem di du lieu dang co. -1 = khong biet.
        public short Weapon { get; set; }
        public short Body { get; set; }
        public short Leg { get; set; }
        public short[] Fashion { get; set; }
        public List<Item> BodyItems { get; set; }

        public PlayerInfo()
        {
            Fashion = new short[4];
            BodyItems = new List<Item>();
        }
    }
}
