namespace NSOKHODO.Auto
{
    /// <summary>
    /// Mot "che do" tu dong loai tru lan nhau (AFK, Train/TanSat, ...).
    /// NsoClient chi giu 1 IAutoMode active tai mot thoi diem - khong can mutex thu cong.
    /// Cac add-on chay song song (AutoAttack/AutoParty/KeepAlive) KHONG implement interface nay.
    /// </summary>
    public interface IAutoMode
    {
        /// <summary>Ten ngan de log (vd "AFK", "TanSat").</summary>
        string Name { get; }

        /// <summary>
        /// Nhan "hoat dong hien tai" de hien o cot Trang thai (tieng Viet co dau, vd "Tan sat",
        /// "PK am", "Di chuyen"). Mode tu cap nhat moi tick. Null = UI dung nhan mac dinh.
        /// </summary>
        string Activity { get; }

        /// <summary>Bat dau chay (spawn background thread).</summary>
        void Start();

        /// <summary>Yeu cau dung.</summary>
        void Stop();

        /// <summary>
        /// Mode nay CO QUYEN dung im rat lau o map dich ma van khoe manh.
        ///
        /// <para>Doc boi <see cref="Client.NsoClient.DungImHopLe"/> de TAT hai bo do "cai xac" trong
        /// <c>KeepAliveController</c> (im lang ba duong / song gia). Hai bo do do sinh ra cho TAN SAT -
        /// noi ma "5 giay khong danh, khong bi danh, khong doi map" chi co the la da chet. Voi
        /// "Thua loi dai" / "Cho PK" / Buff / Danh Vong thi DUNG IM MOI LA VIEC DUNG, va bo do se ban
        /// <c>cmd -9</c> (ve lang) moi 12 giay - do duoc tren log that 2026-09-08: mot acc Thua loi dai
        /// dung o map 72 an 8 phat -9 trong 94 giay.</para>
        /// </summary>
        bool DungImHopLe { get; }

        /// <summary>
        /// NsoClient goi NGAY khi nhan tin char chet, TRUOC khi stop auto systems,
        /// de mode kip snapshot trang thai (vd Train luu diem chet de quay lai).
        /// </summary>
        void OnBeforeDeath();
    }
}
