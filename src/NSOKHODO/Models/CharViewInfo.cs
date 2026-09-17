namespace NSOKHODO.Models
{
    /// <summary>
    /// Bang "Thong tin" cua nhan vat - dung y het `GameScr.currentCharViewInfo` cua game.
    ///
    /// Nguon: client GUI cmd 93 + writeUTF(ten nhan vat) (Service.viewInfo), server tra ve
    /// cmd 93 (toan bo bang) roi cmd 101 (3 field bo sung: tinh tu / banh phong loi / banh bang hoa).
    /// Wire format xac nhan boi 2 nguon doc lap: NinjaSchool_251_src Controller.cs case 93/101
    /// va MODGAME Controller.java case 93/101 (giong nhau tung field).
    ///
    /// Game chia bang nay lam 2 trang (GameScr.typeViewInfo, nut "Doi"):
    ///   trang 0 = chi so chien dau, trang 1 = danh vong/gioi han hang ngay.
    /// UI cua tool bay lai dung 2 trang do o 2 tab "Thong tin 1" / "Thong tin 2".
    /// </summary>
    public class CharViewInfo
    {
        // ===== Trang 1 (typeViewInfo == 0) =====
        public int CharId;
        public string Name = "";
        public short Head;
        public byte Gender;
        public byte ClassId;        // chi so vao bang class cua DataSync (SkillTemplateStore.ClassNames)
        public byte Pk;             // "Hieu chien"
        public int Hp, MaxHp, Mp, MaxMp;
        public byte Speed;
        public short ResFire, ResIce, ResWind;
        public int Dame;            // "Tan cong": game hien (dame - dame/10) + "-" + dame
        public int DameDown;        // "Giam sat thuong"
        public short Exactly;       // "Do chinh xac"
        public short Miss;          // "Kha nang ne don"
        public short Fatal;         // "Chi mang"
        public short ReactDame;     // "Phan don can chien"
        public short SysUp;         // "Cuong khac"
        public short SysDown;       // "Ha khac"
        public int Level;

        // ===== Trang 2 (typeViewInfo == 1) =====
        public string ClanName = "";
        public byte ClanType;
        public short PointUydanh;   // "Diem hoat dong"
        public short PointNon, PointAo, PointGangtay, PointQuan, PointGiay;
        public short PointVukhi, PointLien, PointNhan, PointNgocboi, PointPhu;
        public byte CountFinishDay; // "Hoan thanh NV moi ngay" (/20)
        public byte CountLoopBoos;  // "Truy bat Ta Thu con" (lan)
        public byte CountPB;        // "Vao hang dong con" (lan)
        public byte LimitTiemnangso;
        public byte LimitKynangso;

        // ===== cmd 101 (goi rieng, ve ngay sau cmd 93) =====
        public int PointTinhTu;
        public byte LimitPhongLoi;
        public byte LimitBangHoa;
        /// <summary>Da nhan duoc cmd 101 cho lan xem nay chua (3 field tren moi co nghia).</summary>
        public bool HasExtra;

        /// <summary>Thoi diem (UTC ticks) nhan cmd 93 - UI hien "cap nhat luc".</summary>
        public long AtTicks;

        /// <summary>
        /// He phai suy tu ClassId - clone Char.getSys(): 1,2 = Hirosaki; 3,4 = Ookaza;
        /// 5,6 = Haruna; con lai = chua vao truong.
        /// </summary>
        public int GetSys()
        {
            if (ClassId == 1 || ClassId == 2) return 1;
            if (ClassId == 3 || ClassId == 4) return 2;
            if (ClassId == 5 || ClassId == 6) return 3;
            return 0;
        }
    }
}
