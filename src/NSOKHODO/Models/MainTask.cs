namespace NSOKHODO.Models
{
    /// <summary>
    /// Nhiem vu CHINH TUYEN dang lam - clone <c>Task</c> (NinjaSchool_251_src/Task.cs), nap tu cmd 47.
    /// Wire: SERVER_FACTS §1d.
    ///
    /// BAT BIEN: cmd 48/50 tao ban moi (<see cref="With"/>) roi gan lai <c>CharacterState.MainTask</c>
    /// bang MOT phep gan tham chieu -> luong UI doc khong bao gio thay Index moi + Count cu.
    /// Client gom sua tai cho vi no chi co 1 luong.
    /// </summary>
    public sealed class MainTask
    {
        public readonly short TaskId;
        /// <summary>Buoc hien tai, tinh tu 0.</summary>
        public readonly int Index;
        /// <summary>Tien do buoc hien tai. -1 = server khong gui phan dem.</summary>
        public readonly short Count;
        public readonly string Name;
        public readonly string Detail;
        /// <summary>Ten tung buoc. Server gui chuoi rong -> null = buoc NOI TIEP buoc truoc.</summary>
        public readonly string[] SubNames;
        /// <summary>So can dat tung buoc. -1 = buoc khong dem.</summary>
        public readonly short[] Counts;

        public MainTask(short taskId, int index, short count, string name, string detail,
            string[] subNames, short[] counts)
        {
            TaskId = taskId;
            Index = index;
            Count = count;
            Name = name ?? "";
            Detail = detail ?? "";
            SubNames = subNames ?? new string[0];
            Counts = counts ?? new short[0];
        }

        public MainTask With(int index, short count)
        {
            return new MainTask(TaskId, index, count, Name, Detail, SubNames, Counts);
        }
    }
}
