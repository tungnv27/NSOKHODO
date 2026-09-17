using System;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;

namespace NSOKHODO.Controller
{
    public class MiscHandler
    {
        public GameStateManager State { get; private set; }

        public MiscHandler(GameStateManager state)
        {
            State = state;
        }

        /// <summary>
        /// cmd 93: bang "Thong tin" nhan vat (tra loi cho MiscService.SendViewInfoByName).
        /// Doc TUNG FIELD dung thu tu Controller.cs case 93 (251) = Controller.java case 93 (MODGAME);
        /// dung lai o limitKynangso - phan con lai (2 x 16 mon trang bi) tool khong dung nen bo qua.
        ///
        /// Bay: `pointUydanh` bi doc HAI LAN (truoc va sau ten gia toc) - lan sau ghi de lan truoc.
        /// Do la loi cua ban goc nhung SERVER GUI DUNG 2 short do, bo mot cai la lech het khung.
        /// Va byte `typeClan` CHI co khi ten gia toc khac rong.
        /// </summary>
        public void HandleCharViewInfo(NsoMessage msg)
        {
            var r = msg.Reader;
            var v = new CharViewInfo();
            v.CharId = r.ReadInt();
            v.Name = r.ReadUTF();
            v.Head = r.ReadShort();
            v.Gender = r.ReadByte();
            v.ClassId = r.ReadByte();
            v.Pk = r.ReadByte();
            v.Hp = r.ReadInt();
            v.MaxHp = r.ReadInt();
            v.Mp = r.ReadInt();
            v.MaxMp = r.ReadInt();

            v.Speed = r.ReadByte();
            v.ResFire = r.ReadShort();
            v.ResIce = r.ReadShort();
            v.ResWind = r.ReadShort();
            v.Dame = r.ReadInt();
            v.DameDown = r.ReadInt();
            v.Exactly = r.ReadShort();
            v.Miss = r.ReadShort();
            v.Fatal = r.ReadShort();
            v.ReactDame = r.ReadShort();
            v.SysUp = r.ReadShort();
            v.SysDown = r.ReadShort();
            v.Level = r.ReadUnsignedByte();
            v.PointUydanh = r.ReadShort();
            v.ClanName = r.ReadUTF();
            if (!string.IsNullOrEmpty(v.ClanName))
                v.ClanType = r.ReadByte();
            v.PointUydanh = r.ReadShort();   // lan 2 - ghi de, y het ban goc
            v.PointNon = r.ReadShort();
            v.PointAo = r.ReadShort();
            v.PointGangtay = r.ReadShort();
            v.PointQuan = r.ReadShort();
            v.PointGiay = r.ReadShort();
            v.PointVukhi = r.ReadShort();
            v.PointLien = r.ReadShort();
            v.PointNhan = r.ReadShort();
            v.PointNgocboi = r.ReadShort();
            v.PointPhu = r.ReadShort();
            v.CountFinishDay = r.ReadByte();
            v.CountLoopBoos = r.ReadByte();
            v.CountPB = r.ReadByte();
            v.LimitTiemnangso = r.ReadByte();
            v.LimitKynangso = r.ReadByte();
            v.AtTicks = DateTime.UtcNow.Ticks;
            State.ViewInfo = v;
        }

        /// <summary>
        /// cmd 101: 3 field bo sung cua bang thong tin (server gui RIENG, ngay sau cmd 93).
        /// Ban goc ghi thang vao `currentCharViewInfo` dang mo -> ta cung va vao ban ghi 93 gan nhat.
        /// </summary>
        public void HandleCharViewInfoExtra(NsoMessage msg)
        {
            var v = State.ViewInfo;
            if (v == null) return;
            var r = msg.Reader;
            v.PointTinhTu = r.ReadInt();
            v.LimitPhongLoi = r.ReadByte();
            v.LimitBangHoa = r.ReadByte();
            v.HasExtra = true;
        }
    }
}
