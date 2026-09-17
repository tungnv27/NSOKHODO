namespace NSOKHODO.Models
{
    /// <summary>
    /// Mot nhiem vu PHU TUYEN (nhiem vu moi ngay / ta thu / tinh anh / thu linh / ho tong...) -
    /// clone <c>TaskOrder</c> cua client. Nap tu cmd 96, cap nhat cmd 97, xoa cmd 98.
    /// Wire: SERVER_FACTS §1c. Bat bien: cmd 97 tao ban moi.
    /// </summary>
    public sealed class TaskOrderInfo
    {
        /// <summary>0 = moi ngay, 1 = ta thu, 2 = gioi thieu, 3 = tieu diet, 4 = tinh anh, 5 = thu linh, 6 = ho tong.</summary>
        public readonly int TaskId;
        public readonly int Count;
        public readonly int MaxCount;
        public readonly string Name;
        public readonly string Description;
        public readonly int KillId;
        public readonly int MapId;

        public TaskOrderInfo(int taskId, int count, int maxCount, string name, string description,
            int killId, int mapId)
        {
            TaskId = taskId;
            Count = count;
            MaxCount = maxCount;
            Name = name ?? "";
            Description = description ?? "";
            KillId = killId;
            MapId = mapId;
        }

        public TaskOrderInfo WithCount(int count)
        {
            return new TaskOrderInfo(TaskId, count, MaxCount, Name, Description, KillId, MapId);
        }
    }
}
