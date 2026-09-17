namespace NSOKHODO.Models
{
    public class Waypoint
    {
        public short MinX { get; set; }
        public short MinY { get; set; }
        public short MaxX { get; set; }
        public short MaxY { get; set; }

        public bool Contains(int x, int y)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }

        public int CenterX { get { return (MinX + MaxX) / 2; } }
        public int CenterY { get { return (MinY + MaxY) / 2; } }
    }
}
