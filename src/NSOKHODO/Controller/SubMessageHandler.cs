using System;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;

namespace NSOKHODO.Controller
{
    /// <summary>
    /// cmd 117 — gói "nhiều việc" của server. Byte đầu quyết định nhánh:
    ///
    ///   b == -1  -> GÓI PHỤ, đọc thêm 1 byte sub:
    ///                 0 = thông tin vị thú   (bỏ qua)
    ///                 1 = đổi vị thú         (bỏ qua)
    ///                 2 = ĐỒNG HỒ HIỆU ỨNG   (ta đọc)
    ///   b != -1  -> 3 lớp cây trang trí + trứng quái + item trên map (bỏ qua, để đợt sau)
    ///
    /// Wire xác minh ở NinjaSchool_251: Controller.cs:259-268 (nhánh -1) và
    /// Readmsg.cs:11-30 (bảng sub) + Readmsg.cs:113-140 (onCountDown).
    ///
    /// ⚠️ NGUỒN: MODGAME (bản 1.8.0 — nguồn "thắng" khi hai nguồn lệch) KHÔNG có cơ chế này:
    /// case 117 bên đó đọc thẳng byte đếm rồi dựng cây, không có nhánh -1, và cả cây mã nguồn
    /// không có Readmsg/TimecountDown/VecTime. Nên nhánh này chỉ dựa vào client PC 2.5.1.
    /// Vì thế handler CHỈ ĐỌC, không đổi trạng thái gì ngoài danh sách đồng hồ, và nuốt mọi lỗi:
    /// nếu server này không nói kiểu đó thì cùng lắm là không có đồng hồ nào hiện ra.
    ///
    /// Handler này KHÔNG gửi gói tin nào (icon hiệu ứng phải xin server bằng requestIcon —
    /// cố tình không làm, cửa sổ "Xem game" là chỉ-để-nhìn).
    /// </summary>
    public class SubMessageHandler
    {
        public GameStateManager State { get; private set; }

        // Chan doan: moi "hinh dang" cua goi 117 chi log MOT lan/phien (tranh spam).
        // Can thiet vi khi da bat case 117 thi no khong con roi vao nhanh `default` cua
        // MessageRouter nua -> dong log "cmd CHUA XU LY=117" tat, mat luon tin hieu duy nhat
        // cho biet server co gui kieu goi phu hay khong.
        private readonly System.Collections.Generic.HashSet<int> _seen =
            new System.Collections.Generic.HashSet<int>();

        public SubMessageHandler(GameStateManager state)
        {
            State = state;
        }

        private bool FirstTime(int tag)
        {
            lock (_seen) return _seen.Add(tag);
        }

        /// <summary>Trả về mô tả ngắn để log, hoặc null nếu không có gì đáng nói.</summary>
        public string Handle(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                sbyte b = r.ReadSignedByte();

                if (b != -1)
                {
                    // Nhanh cay trang tri / trung quai / item map - chua dung den.
                    // Van log 1 lan de biet server dang dung nhanh nao.
                    return FirstTime(1000 + b)
                        ? string.Format("[cmd117] nhanh trang tri (byte dau = {0}, len = {1}) - bo qua",
                                        b, msg.DataLength)
                        : null;
                }

                sbyte sub = r.ReadSignedByte();
                if (sub != 2)
                {
                    return FirstTime(2000 + sub)
                        ? string.Format("[cmd117] goi phu sub = {0} (vi thu) - bo qua", sub)
                        : null;
                }

                return ReadCountDown(r);
            }
            catch (Exception ex)
            {
                return FirstTime(9999) ? "[cmd117] doc loi: " + ex.Message : null;
            }
        }

        /// <summary>
        /// Readmsg.cs:113-140 — short id | UTF ten | int soGiay | short idIcon | byte kieu.
        /// soGiay &lt; 0 thi client BO QUA hoan toan (khong tao dong nao) — giu nguyen luat do.
        /// kieu == -2 la lenh XOA dong.
        /// </summary>
        private string ReadCountDown(BigEndianBinaryReader r)
        {
            short id = r.ReadShort();
            string name = r.ReadUTF();
            int seconds = r.ReadInt();
            short iconId = r.ReadShort();
            sbyte kind = r.ReadSignedByte();

            if (seconds < 0)                       // Readmsg.cs:121-124
                return FirstTime(3000 + id)
                    ? string.Format("[cmd117] dong ho id={0} '{1}' soGiay={2} < 0 -> client BO QUA",
                                    id, name, seconds)
                    : null;

            if (kind == -2)
            {
                State.CountdownRemove(id);
                return string.Format("[Hieu ung] het: {0}", name);
            }

            var c = new EffectCountdown
            {
                Id = id,
                Name = name,
                IconId = iconId,
                Kind = kind,
                // client: setsecond() LUON dat moc = bay gio + soGiay (ke ca kieu 0)
                ExpiresAt = DateTime.UtcNow.AddSeconds(seconds)
            };

            bool moi = State.CountdownUpsert(c);
            // Log lan dau cho MOI id (ke ca khi la gia han) de con biet server co gui that khong.
            if (moi || FirstTime(4000 + id))
                return string.Format("[Hieu ung] {0} (id={1} kieu={2} giay={3})",
                                     c.Display(), id, kind, seconds);
            return null;
        }
    }
}
