using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NSOKHODO.Kho;

namespace NSOKHODO.UI
{
    /// <summary>Tab <b>Hàng chờ</b>: các lệnh rút, nút Tiếp / Huỷ (SPEC §10).</summary>
    public partial class MainForm
    {
        private DataGridView _gridHang;
        private CheckBox _chkHienXong;
        private List<LenhRut> _lenhHien = new List<LenhRut>();

        private enum CotHang { So, TaoLuc, Nguon, NguoiNhan, Mon, Xin, DaGiao, AccGiao, TrangThai, LyDo }

        private void DungTabHangCho(TabPage tab)
        {
            var thanh = new Panel { Dock = DockStyle.Top, Height = 34 };
            var btnTiep = new Button { Text = "Tiếp", Left = 6, Top = 5, Width = 80, Height = 24 };
            btnTiep.Click += (s, e) =>
            {
                var l = LenhDangChon();
                if (l == null) return;
                _dp.TiepLenhTuTool(l.So);
                XepLog("Đã gửi Tiếp lệnh #" + l.So, false);
            };
            var btnHuy = new Button { Text = "Huỷ", Left = 92, Top = 5, Width = 80, Height = 24 };
            btnHuy.Click += (s, e) =>
            {
                var l = LenhDangChon();
                if (l == null || !l.DangMo) return;
                if (MessageBox.Show(this, "Huỷ lệnh #" + l.So + " (" + l.MoTaMon() + " cho " + l.NguoiNhan + ")?",
                        "Huỷ lệnh", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _dp.HuyLenhTuTool(l.So);
            };
            _chkHienXong = new CheckBox { Text = "Hiện lệnh đã kết thúc (200 gần nhất)", Left = 190, Top = 8, Width = 260 };
            _chkHienXong.CheckedChanged += (s, e) => LamMoiHangCho();
            thanh.Controls.Add(btnTiep);
            thanh.Controls.Add(btnHuy);
            thanh.Controls.Add(_chkHienXong);
            thanh.Controls.Add(new Label
            {
                Left = 460, Top = 8, Width = 700, ForeColor = Color.DimGray,
                Text = "Tạm dừng = thiếu hàng / người nhận thiếu ô / hỏng 2 lần. Tiếp = giao phần đang có. Lệnh tạm dừng quá 30 phút tự huỷ.",
            });

            _gridHang = TaoLuoiAo();
            _gridHang.MultiSelect = false;
            _gridHang.CellValueNeeded += GridHangValueNeeded;
            ThemCot(_gridHang, "Số", 50);
            ThemCot(_gridHang, "Tạo lúc", 110);
            ThemCot(_gridHang, "Nguồn", 120);
            ThemCot(_gridHang, "Người nhận", 120);
            ThemCot(_gridHang, "Món", 260);
            ThemCot(_gridHang, "Xin", 50);
            ThemCot(_gridHang, "Đã giao", 60);
            ThemCot(_gridHang, "Acc đang giao", 120);
            ThemCot(_gridHang, "Trạng thái", 100);
            ThemCot(_gridHang, "Lý do", 360);

            tab.Controls.Add(_gridHang);
            tab.Controls.Add(thanh);
        }

        private LenhRut LenhDangChon()
        {
            var r = _gridHang.CurrentRow;
            if (r == null || r.Index < 0 || r.Index >= _lenhHien.Count) return null;
            return _lenhHien[r.Index];
        }

        private void LamMoiHangCho()
        {
            if (_gridHang == null) return;
            var ds = _dp.Hang.DangMo;
            if (_chkHienXong.Checked) ds.AddRange(_dp.Hang.GanDay);
            var chon = LenhDangChon();
            _lenhHien = ds;
            bool doiSoDong = _gridHang.RowCount != ds.Count;
            if (doiSoDong)
            {
                _gridHang.ClearSelection();
                _gridHang.RowCount = ds.Count;
            }
            if (chon != null)
            {
                int i = ds.FindIndex(x => x.So == chon.So);
                if (i >= 0 && (_gridHang.CurrentRow == null || _gridHang.CurrentRow.Index != i))
                {
                    try { _gridHang.CurrentCell = _gridHang.Rows[i].Cells[0]; } catch { }
                }
            }
            if (doiSoDong) _gridHang.Invalidate();
            else VeLaiDongThay(_gridHang);
        }

        /// <summary>Vẽ lại đúng các dòng đang nhìn thấy (Invalidate() cả lưới mỗi giây là nguồn giật).</summary>
        private static void VeLaiDongThay(DataGridView g)
        {
            int dau = g.FirstDisplayedScrollingRowIndex;
            if (dau < 0) return;
            int het = Math.Min(dau + g.DisplayedRowCount(true), g.RowCount);
            for (int i = dau; i < het; i++)
            {
                try { g.InvalidateRow(i); } catch { return; }
            }
        }

        private void GridHangValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var ds = _lenhHien;
            if (e.RowIndex < 0 || e.RowIndex >= ds.Count) return;
            var l = ds[e.RowIndex];
            switch ((CotHang)e.ColumnIndex)
            {
                case CotHang.So: e.Value = "#" + l.So; break;
                case CotHang.TaoLuc: e.Value = l.TaoLuc.ToString("dd/MM HH:mm:ss"); break;
                case CotHang.Nguon: e.Value = l.Nguon == "chat" ? "chat · " + NameMask.Apply(l.ChuKho) : "tool"; break;
                case CotHang.NguoiNhan:
                    e.Value = NameMask.Apply(l.NguoiNhan) + (l.Khu >= 0 && l.Khu != _cfg.KhuChinh ? " · khu " + l.Khu : "");
                    break;
                case CotHang.Mon: e.Value = l.MoTaMon(); break;
                case CotHang.Xin:
                    e.Value = l.Dong.Exists(d => d.SoXin < 0 && d.SoChot <= 0) ? "hết" : l.TongXin.ToString();
                    break;
                case CotHang.DaGiao: e.Value = l.TongDaGiao.ToString(); break;
                case CotHang.AccGiao:
                    e.Value = l.CloneDangGiao != null ? NameMask.Apply(l.CloneDangGiao)
                            // ToArray() trước: luồng điều phối có thể đang thêm vào CacClone.
                            : string.Join(", ", l.CacClone.ToArray().Select(NameMask.Apply).ToArray());
                    break;
                case CotHang.TrangThai: e.Value = ChuTrangThaiLenh(l.TrangThai); break;
                case CotHang.LyDo:
                    e.Value = l.TrangThai == TrangThaiLenh.ChoCoMat
                        ? "chờ " + l.NguoiNhan + " có mặt tới " + l.ChoCoMatDen.ToString("HH:mm") + (string.IsNullOrEmpty(l.LyDo) ? "" : " · " + l.LyDo)
                        : l.LyDo;
                    break;
            }
        }

        private static string ChuTrangThaiLenh(TrangThaiLenh t)
        {
            switch (t)
            {
                case TrangThaiLenh.ChoCoMat: return "Chờ";
                case TrangThaiLenh.DangGiao: return "Đang giao";
                case TrangThaiLenh.TamDung: return "TẠM DỪNG";
                case TrangThaiLenh.Xong: return "Xong";
                default: return "Huỷ";
            }
        }
    }
}
