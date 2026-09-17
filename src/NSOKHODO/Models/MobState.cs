namespace NSOKHODO.Models
{
    public class MobState
    {
        public int Id { get; set; }
        public short TemplateId { get; set; }
        public byte Level { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public byte Sys { get; set; }
        /// <summary>Byte status truoc levelBoss trong MAP_INFO. MODGAME Auto.a phan loai elite khi status != 0.</summary>
        public byte Status { get; set; }
        public byte LevelBoss { get; set; }
        /// <summary>Cờ isBos cuối entry mob (boss thật như tpl=141 có LevelBoss=0 nhưng IsBoss=1).</summary>
        public bool IsBoss { get; set; }
        public bool IsDead { get; set; }
        public bool IsActive { get; set; }

        public override string ToString()
        {
            return string.Format("Mob[{0}] HP={1}/{2} ({3},{4})", Id, Hp, MaxHp, X, Y);
        }
    }
}
