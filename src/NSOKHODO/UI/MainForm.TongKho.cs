using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NSOKHODO.Kho;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Tab <b>Tổng kho</b> (mở app là thấy): bảng món bên trái (tìm, lọc kệ, sức chứa) + khung
    /// <b>Điều phối</b> bên phải (D21): món → số lượng → người nhận → Giao; chọn gói → Rút gói.
    /// </summary>
    public partial class MainForm
    {
        private DataGridView _gridTon;
        private TextBox _txtTim;
        private ComboBox _cbLocKe;
        private CheckBox _chkHienKhoa;
        private Label _lblSucChua;
        private SplitContainer _chiaTong;
        private List<TonMon> _tonHien = new List<TonMon>();
        private List<TonMon> _tonNguonCu;
        private string _locCu;

        // điều phối
        private Label _lblMonChon;
        private NumericUpDown _numDpId, _numDpCap, _numDpSl, _numDpKhu;
        private CheckBox _chkDpHet;
        private ComboBox _cbDpCho, _cbDpGoi;
        private Label _lblDpGhiChu;
        private ListBox _lstDon;
        private Label _lblDon;
        private readonly List<DongLenh> _donSoan = new List<DongLenh>();

        private enum CotTon { Id, Ten, Ke, Cap, Han, Khoa, Tong, Giu, Con, SoO, PhanBo, Co }

        private void DungTabTongKho(TabPage tab)
        {
            // ---- thanh lọc ----
            var loc = new Panel { Dock = DockStyle.Top, Height = 32 };
            loc.Controls.Add(new Label { Text = "Tìm:", Left = 6, Top = 8, Width = 34 });
            _txtTim = new TextBox { Left = 42, Top = 5, Width = 180 };
            _txtTim.TextChanged += (s, e) => LamMoiTongKho();
            loc.Controls.Add(_txtTim);
            loc.Controls.Add(new Label { Text = "Kệ:", Left = 234, Top = 8, Width = 26 });
            _cbLocKe = new ComboBox { Left = 262, Top = 5, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbLocKe.Items.Add("Tất cả");
            foreach (var k in KeHang.TatCa) _cbLocKe.Items.Add(KeHang.TenHienThi(k));
            _cbLocKe.SelectedIndex = 0;
            _cbLocKe.SelectedIndexChanged += (s, e) => LamMoiTongKho();
            loc.Controls.Add(_cbLocKe);
            _chkHienKhoa = new CheckBox { Text = "Hiện món khoá", Left = 414, Top = 6, Width = 110 };
            _chkHienKhoa.CheckedChanged += (s, e) => LamMoiTongKho();
            loc.Controls.Add(_chkHienKhoa);
            _lblSucChua = new Label { Left = 530, Top = 8, Width = 700, ForeColor = Color.FromArgb(40, 70, 130), AutoSize = false };
            loc.Controls.Add(_lblSucChua);

            // ---- bảng món ----
            _gridTon = TaoLuoiAo();
            _gridTon.MultiSelect = false;
            _gridTon.CellValueNeeded += GridTonValueNeeded;
            _gridTon.SelectionChanged += (s, e) => { if (!_dangVeTon) ChonMonDieuPhoi(); };
            ThemCot(_gridTon, "ID", 56);
            ThemCot(_gridTon, "Tên", 190);
            ThemCot(_gridTon, "Kệ", 100);
            ThemCot(_gridTon, "+", 34);
            ThemCot(_gridTon, "Hạn", 40);
            ThemCot(_gridTon, "Khoá", 44);
            ThemCot(_gridTon, "Tổng", 60);
            ThemCot(_gridTon, "Giữ", 50);
            ThemCot(_gridTon, "Còn", 60);
            ThemCot(_gridTon, "Ô", 40);
            ThemCot(_gridTon, "Phân bố (túi/rương)", 260);
            ThemCot(_gridTon, "Cờ", 150);
            var menu = new ContextMenuStrip();
            menu.Opening += (s, e) => DungMenuTon(menu);
            _gridTon.ContextMenuStrip = menu;
            _gridTon.CellMouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                    _gridTon.CurrentCell = _gridTon.Rows[e.RowIndex].Cells[0];
            };

            var trai = new Panel { Dock = DockStyle.Fill };
            trai.Controls.Add(_gridTon);
            trai.Controls.Add(loc);

            _chiaTong = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            _chiaTong.Panel1.Controls.Add(trai);
            _chiaTong.Panel2.Controls.Add(DungKhungDieuPhoi());
            tab.Controls.Add(_chiaTong);
        }

        /// <summary>Gọi trong Shown (kích thước thật mới có — xem bẫy SplitContainer).</summary>
        private void DatKichThuocTongKho()
        {
            try
            {
                _chiaTong.SplitterDistance = Math.Max(400, _chiaTong.Width - 300);
                _chiaTong.Panel2MinSize = 260;
            }
            catch { }
        }

        private Control DungKhungDieuPhoi()
        {
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), AutoScroll = true };
            int y = 6;
            host.Controls.Add(new Label
            {
                Text = "ĐIỀU PHỐI", Left = 6, Top = y, Width = 250, Height = 20,
                Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(40, 70, 130),
            });
            y += 26;
            _lblMonChon = new Label { Left = 6, Top = y, Width = 260, Height = 34, Text = "(chọn một món bên trái)" };
            host.Controls.Add(_lblMonChon);
            y += 38;

            host.Controls.Add(new Label { Text = "ID món:", Left = 6, Top = y + 3, Width = 60 });
            _numDpId = new NumericUpDown { Left = 90, Top = y, Width = 80, Minimum = 0, Maximum = 32000 };
            _numDpId.ValueChanged += (s, e) => CapNhatNhanMon();
            host.Controls.Add(_numDpId);
            y += 28;

            host.Controls.Add(new Label { Text = "Cấp +:", Left = 6, Top = y + 3, Width = 60 });
            _numDpCap = new NumericUpDown { Left = 90, Top = y, Width = 80, Minimum = -1, Maximum = 16, Value = -1 };
            host.Controls.Add(_numDpCap);
            host.Controls.Add(new Label { Text = "(-1 = bất kỳ)", Left = 176, Top = y + 3, Width = 90, ForeColor = Color.DimGray });
            y += 28;

            host.Controls.Add(new Label { Text = "Số lượng:", Left = 6, Top = y + 3, Width = 70 });
            _numDpSl = new NumericUpDown { Left = 90, Top = y, Width = 80, Minimum = 1, Maximum = 100000, Value = 1 };
            host.Controls.Add(_numDpSl);
            _chkDpHet = new CheckBox { Text = "Hết", Left = 180, Top = y + 1, Width = 60 };
            _chkDpHet.CheckedChanged += (s, e) => _numDpSl.Enabled = !_chkDpHet.Checked;
            host.Controls.Add(_chkDpHet);
            y += 28;

            host.Controls.Add(new Label { Text = "Cho:", Left = 6, Top = y + 3, Width = 60 });
            _cbDpCho = new ComboBox { Left = 90, Top = y, Width = 170, DropDownStyle = ComboBoxStyle.DropDown };
            _cbDpCho.DropDown += (s, e) => NapChuKhoVaoCombo();
            host.Controls.Add(_cbDpCho);
            y += 30;

            // Khu giao (D79): -1 = khu chính trong Cài đặt. Áp cho Giao ngay, Giao đơn và Rút gói.
            host.Controls.Add(new Label { Text = "Khu giao:", Left = 6, Top = y + 3, Width = 70 });
            _numDpKhu = new NumericUpDown { Left = 90, Top = y, Width = 80, Minimum = -1, Maximum = LenhChat.KHU_TOI_DA, Value = -1 };
            host.Controls.Add(_numDpKhu);
            host.Controls.Add(new Label { Text = "(-1 = khu chính)", Left = 176, Top = y + 3, Width = 90, ForeColor = Color.DimGray });
            y += 30;

            var btnGiao = new Button { Text = "Giao ngay", Left = 90, Top = y, Width = 82, Height = 28 };
            btnGiao.Click += BtnGiao;
            host.Controls.Add(btnGiao);
            var btnThem = new Button { Text = "+ Vào đơn", Left = 176, Top = y, Width = 84, Height = 28 };
            btnThem.Click += (s, e) => ThemVaoDon();
            host.Controls.Add(btnThem);
            y += 36;

            // Đơn nhiều món: soạn từng dòng rồi giao một lệnh (clone giao chung lượt, như gói rút).
            _lblDon = new Label { Left = 6, Top = y, Width = 254, Height = 16, Text = "Đơn đang soạn: trống" };
            host.Controls.Add(_lblDon);
            y += 18;
            _lstDon = new ListBox { Left = 6, Top = y, Width = 254, Height = 82, IntegralHeight = false };
            host.Controls.Add(_lstDon);
            y += 86;
            var btnGiaoDon = new Button { Text = "Giao đơn", Left = 6, Top = y, Width = 100, Height = 26 };
            btnGiaoDon.Click += (s, e) => GiaoDon();
            host.Controls.Add(btnGiaoDon);
            var btnXoaDong = new Button { Text = "Xoá dòng", Left = 110, Top = y, Width = 72, Height = 26 };
            btnXoaDong.Click += (s, e) =>
            {
                int i = _lstDon.SelectedIndex;
                if (i >= 0 && i < _donSoan.Count) { _donSoan.RemoveAt(i); VeDon(); }
            };
            host.Controls.Add(btnXoaDong);
            var btnXoaHet = new Button { Text = "Xoá hết", Left = 186, Top = y, Width = 74, Height = 26 };
            btnXoaHet.Click += (s, e) => { _donSoan.Clear(); VeDon(); };
            host.Controls.Add(btnXoaHet);
            y += 40;

            host.Controls.Add(new Label { Text = "Gói:", Left = 6, Top = y + 3, Width = 60 });
            _cbDpGoi = new ComboBox { Left = 90, Top = y, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbDpGoi.DropDown += (s, e) => NapGoiVaoCombo(_cbDpGoi);
            host.Controls.Add(_cbDpGoi);
            var btnGoi = new Button { Text = "Rút gói", Left = 204, Top = y - 1, Width = 56, Height = 24 };
            btnGoi.Click += (s, e) => RutGoi(_cbDpGoi.SelectedItem as string, _cbDpCho.Text);
            host.Controls.Add(btnGoi);
            y += 36;

            _lblDpGhiChu = new Label { Left = 6, Top = y, Width = 260, Height = 120, ForeColor = Color.DimGray };
            host.Controls.Add(_lblDpGhiChu);
            return host;
        }

        private void NapChuKhoVaoCombo()
        {
            string dang = _cbDpCho.Text;
            _cbDpCho.Items.Clear();
            foreach (var ck in _cfg.ChuKho) _cbDpCho.Items.Add(ck);
            _cbDpCho.Text = dang;
        }

        private void NapGoiVaoCombo(ComboBox cb)
        {
            var dang = cb.SelectedItem as string;
            cb.Items.Clear();
            foreach (var g in _cfg.TenGoi) cb.Items.Add(g);
            if (dang != null && cb.Items.Contains(dang)) cb.SelectedItem = dang;
            else if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }

        private void CapNhatNhanMon()
        {
            short tpl = (short)_numDpId.Value;
            var d = _dp.BangTon.Where(x => x.Khoa.Tpl == tpl && !x.Khoa.Khoa).ToList();
            int con = d.Sum(x => x.KhaDung);
            var caps = d.Where(x => x.KhaDung > 0).Select(x => x.Khoa.Up).Distinct().OrderBy(x => x).ToList();
            _lblMonChon.Text = tpl + " " + BangMon.Ten(tpl) + "\nCòn " + con
                               + (caps.Count > 1 ? " · nhiều cấp: " + string.Join(", ", caps.Select(c => "+" + c)) + " → chọn cấp" : "");
        }

        private void ChonMonDieuPhoi()
        {
            var d = MonDangChon();
            if (d == null) return;
            _numDpId.Value = Math.Max(_numDpId.Minimum, Math.Min(_numDpId.Maximum, d.Khoa.Tpl));
            _numDpCap.Value = d.Khoa.Up > 0 ? d.Khoa.Up : -1;
            CapNhatNhanMon();
        }

        private TonMon MonDangChon()
        {
            var r = _gridTon.CurrentRow;
            if (r == null || r.Index < 0 || r.Index >= _tonHien.Count) return null;
            return _tonHien[r.Index];
        }

        private void BtnGiao(object sender, EventArgs e)
        {
            string cho = (_cbDpCho.Text ?? "").Trim();
            if (cho.Length == 0) { MessageBox.Show(this, "Nhập tên người nhận.", "Giao"); return; }
            short tpl = (short)_numDpId.Value;
            int sl = _chkDpHet.Checked ? -1 : (int)_numDpSl.Value;
            int cap = (int)_numDpCap.Value;
            int khu = (int)_numDpKhu.Value;
            _dp.RutTuTool(tpl, cap, sl, cho, khu);
            XepLog(string.Format("Đã gửi lệnh rút: {0} x {1} {2}{3} cho {4}{5} → xem tab Hàng chờ.",
                sl < 0 ? "hết" : sl.ToString(), tpl, BangMon.Ten(tpl), cap >= 0 ? " +" + cap : "", cho, ChuKhuGiao(khu)), false);
        }

        private void ThemVaoDon()
        {
            short tpl = (short)_numDpId.Value;
            if (tpl <= 0 || BangMon.Lay(tpl) == null) { MessageBox.Show(this, "ID món không hợp lệ.", "Đơn"); return; }
            int sl = _chkDpHet.Checked ? -1 : (int)_numDpSl.Value;
            int cap = (int)_numDpCap.Value;
            var cu = _donSoan.Find(x => x.Tpl == tpl && x.Cap == cap);
            if (cu == null) _donSoan.Add(new DongLenh { Tpl = tpl, Cap = cap, SoXin = sl });
            else cu.SoXin = cu.SoXin < 0 || sl < 0 ? -1 : cu.SoXin + sl;   // cùng món + cấp: cộng dồn
            VeDon();
        }

        private void VeDon()
        {
            _lstDon.BeginUpdate();
            _lstDon.Items.Clear();
            int tong = 0;
            foreach (var d in _donSoan)
            {
                _lstDon.Items.Add(string.Format("{0} {1}{2} × {3}", d.Tpl, BangMon.Ten(d.Tpl),
                    d.Cap >= 0 ? " +" + d.Cap : "", d.SoXin < 0 ? "hết" : d.SoXin.ToString()));
                if (d.SoXin > 0) tong += d.SoXin;
            }
            _lstDon.EndUpdate();
            _lblDon.Text = _donSoan.Count == 0 ? "Đơn đang soạn: trống"
                : string.Format("Đơn đang soạn: {0} dòng, {1} món", _donSoan.Count, tong);
        }

        private void GiaoDon()
        {
            if (_donSoan.Count == 0) { MessageBox.Show(this, "Đơn trống: chọn món → số lượng → \"+ Vào đơn\".", "Giao đơn"); return; }
            string cho = (_cbDpCho.Text ?? "").Trim();
            if (cho.Length == 0) { MessageBox.Show(this, "Nhập tên người nhận.", "Giao đơn"); return; }
            int khu = (int)_numDpKhu.Value;
            _dp.RutNhieuTuTool(_donSoan, cho, khu);
            XepLog("Đã gửi lệnh rút " + _donSoan.Count + " món cho " + cho + ChuKhuGiao(khu) + ": "
                   + string.Join(", ", _donSoan.Select(d => (d.SoXin < 0 ? "hết" : d.SoXin.ToString()) + " x " + d.Tpl).ToArray())
                   + " → xem tab Hàng chờ.", false);
            _donSoan.Clear();
            VeDon();
        }

        private void RutGoi(string goi, string cho)
        {
            if (string.IsNullOrEmpty(goi)) { MessageBox.Show(this, "Chưa chọn gói (tạo ở Cài đặt → Gói rút).", "Rút gói"); return; }
            cho = (cho ?? "").Trim();
            if (cho.Length == 0)
            {
                cho = HopNhap.HoiChon(this, "Rút gói " + goi, "Giao cho ai (tên nhân vật bất kỳ):", _cfg.ChuKho.ToArray(), null, true);
                if (string.IsNullOrEmpty(cho)) return;
            }
            int khu = (int)_numDpKhu.Value;
            _dp.RutGoiTuTool(goi, cho, khu);
            XepLog("Đã gửi lệnh rút gói " + goi + " cho " + cho + ChuKhuGiao(khu) + " → xem tab Hàng chờ.", false);
        }

        private string ChuKhuGiao(int khu)
        {
            return " ở khu " + (khu >= 0 ? khu : _cfg.KhuChinh) + (khu >= 0 && khu != _cfg.KhuChinh ? "" : " (chính)");
        }

        // ---------------- làm mới ----------------

        private void LamMoiTongKho()
        {
            if (_gridTon == null) return;
            var nguon = _dp.BangTon;
            string loc = (_txtTim.Text ?? "") + "|" + _cbLocKe.SelectedIndex + "|" + _chkHienKhoa.Checked + "|" + NameMask.Enabled;
            var sc = _dp.SucChuaKho;
            var rac = _dp.SucChuaRac;
            int on, tong;
            _dp.DemClone(out on, out tong);
            long xuTran = (long)_cfg.XuTran * Math.Max(1, Accounts.Count);
            DatChu(_lblSucChua, string.Format("Kho {0}{1}/{2} ô (trống {3}){4} · {5}/{6} clone online · Xu {7} (còn chỗ {8}){9}",
                sc.ChuaDu ? "~" : "", sc.Dung, sc.Tong, sc.Trong,
                rac.Tong > 0 ? " · Rác " + rac.Dung + "/" + rac.Tong : "", on, tong,
                KhoDieuPhoi.XuGon(_dp.TongXu), KhoDieuPhoi.XuGon(Math.Max(0, xuTran - _dp.TongXu)),
                sc.ChuaDu ? " · ~ = có clone chưa đọc rương" : ""));
            int khuChon = (int)_numDpKhu.Value;
            DatChu(_lblDpGhiChu, "Lệnh vào tab Hàng chờ. Người nhận phải tới "
                                 + MapNames.NameOf(_cfg.Map) + " khu "
                                 + (khuChon >= 0 ? khuChon.ToString() : (_cfg.KhuChinh < 0 ? "(chưa cài)" : _cfg.KhuChinh.ToString()))
                                 + ".\n\nChuột phải vào món: đánh dấu rác, theo dõi, đổi kệ, xem phân bố.");

            // Bộ điều phối chỉ thay bảng khi nội dung đổi -> cùng tham chiếu + cùng bộ lọc = không vẽ gì.
            if (ReferenceEquals(nguon, _tonNguonCu) && loc == _locCu) return;
            _tonNguonCu = nguon;
            _locCu = loc;

            string tim = ChuVan.ChuanHoa(_txtTim.Text ?? "");
            string ke = _cbLocKe.SelectedIndex <= 0 ? null : KeHang.TatCa[_cbLocKe.SelectedIndex - 1];
            var moi = new List<TonMon>();
            foreach (var d in nguon)
            {
                if (d.Khoa.Khoa && !_chkHienKhoa.Checked) continue;
                if (ke != null && d.Nhom != ke) continue;
                if (tim.Length > 0 && !d.Khoa.Tpl.ToString().StartsWith(tim) && !ChuVan.ChuanHoa(d.Ten).Contains(tim)) continue;
                if (d.Tong == 0 && !d.TrenAccNha) continue;
                moi.Add(d);
            }

            var chon = MonDangChon();
            bool doiSoDong = _gridTon.RowCount != moi.Count;
            // Đổi dòng / chọn lại bằng code: KHÔNG để SelectionChanged ghi đè ô ID / cấp người dùng đang nhập,
            // và giữ nguyên chỗ đang cuộn.
            _dangVeTon = true;
            try
            {
                int dau = _gridTon.FirstDisplayedScrollingRowIndex;
                if (doiSoDong)
                {
                    var cur = _gridTon.CurrentRow;
                    if (cur != null && cur.Index >= moi.Count) _gridTon.ClearSelection();
                    _tonHien = moi;
                    _gridTon.RowCount = moi.Count;
                }
                else _tonHien = moi;
                if (chon != null)
                {
                    int i = moi.FindIndex(x => x.Khoa.Equals(chon.Khoa));
                    if (i >= 0 && (_gridTon.CurrentRow == null || _gridTon.CurrentRow.Index != i))
                    {
                        try { _gridTon.CurrentCell = _gridTon.Rows[i].Cells[0]; } catch { }
                    }
                }
                if (doiSoDong && dau >= 0 && moi.Count > 0)
                {
                    try { _gridTon.FirstDisplayedScrollingRowIndex = Math.Min(dau, moi.Count - 1); } catch { }
                }
            }
            finally { _dangVeTon = false; }
            if (doiSoDong) _gridTon.Invalidate();
            else VeLaiTon();
            if (_numDpId.Value > 0) CapNhatNhanMon();   // "Còn N" theo số mới
        }

        private bool _dangVeTon;

        private static void DatChu(Control c, string s)
        {
            if (c.Text != s) c.Text = s;
        }

        private void VeLaiTon()
        {
            int dau = _gridTon.FirstDisplayedScrollingRowIndex;
            if (dau < 0) return;
            int het = Math.Min(dau + _gridTon.DisplayedRowCount(true), _gridTon.RowCount);
            for (int i = dau; i < het; i++)
            {
                try { _gridTon.InvalidateRow(i); } catch { return; }
            }
        }

        private void GridTonValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var ds = _tonHien;
            if (e.RowIndex < 0 || e.RowIndex >= ds.Count) return;
            var d = ds[e.RowIndex];
            switch ((CotTon)e.ColumnIndex)
            {
                case CotTon.Id: e.Value = d.Khoa.Tpl.ToString(); break;
                case CotTon.Ten: e.Value = d.Ten; break;
                case CotTon.Ke: e.Value = KeHang.TenHienThi(d.Nhom); break;
                case CotTon.Cap: e.Value = d.Khoa.Up > 0 ? "+" + d.Khoa.Up : ""; break;
                case CotTon.Han: e.Value = d.Khoa.Han ? "có" : ""; break;
                case CotTon.Khoa: e.Value = d.Khoa.Khoa ? "khoá" : ""; break;
                case CotTon.Tong: e.Value = d.Tong.ToString(); break;
                case CotTon.Giu: e.Value = d.Giu > 0 ? d.Giu.ToString() : ""; break;
                case CotTon.Con: e.Value = d.Khoa.Khoa ? "—" : d.KhaDung.ToString(); break;
                case CotTon.SoO: e.Value = d.SoO.ToString(); break;
                case CotTon.PhanBo:
                    if (d.UiPhanBo == null || d.UiPhanBoAnTen != NameMask.Enabled)
                    {
                        d.UiPhanBo = string.Join(", ", d.PhanBo.OrderByDescending(kv => kv.Value[0] + kv.Value[1])
                            .Select(kv => NameMask.Apply(kv.Key) + " " + kv.Value[0] + "/" + kv.Value[1]).ToArray());
                        d.UiPhanBoAnTen = NameMask.Enabled;
                    }
                    e.Value = d.UiPhanBo;
                    break;
                case CotTon.Co:
                    {
                        var p = new List<string>();
                        if (d.LaRac) p.Add("Rác");
                        if (d.TheoDoi) p.Add("Theo dõi");
                        if (d.TrenAccNha) p.Add("có trên clone đang nhả");
                        e.Value = string.Join(" · ", p.ToArray());
                        break;
                    }
            }
        }

        // ---------------- menu chuột phải ----------------

        private void DungMenuTon(ContextMenuStrip menu)
        {
            menu.Items.Clear();
            var d = MonDangChon();
            if (d == null)
            {
                menu.Items.Add(new ToolStripMenuItem("(chưa chọn món)") { Enabled = false });
                return;
            }
            short tpl = d.Khoa.Tpl;
            bool rac = _cfg.LaRac(tpl);
            var mRac = new ToolStripMenuItem(rac ? "Bỏ đánh dấu rác" : "Đánh dấu rác");
            mRac.Click += (s, e) =>
            {
                _cfg.DatRac(tpl, !rac);
                HenLuuCfg();
                NapDanhSachRac();
                XepLog((rac ? "Bỏ rác: " : "Đánh dấu rác: ") + tpl + " " + BangMon.Ten(tpl), false);
            };
            menu.Items.Add(mRac);

            var mTheo = new ToolStripMenuItem("Theo dõi…");
            mTheo.Click += (s, e) => ThemTheoDoi(tpl);
            menu.Items.Add(mTheo);
            if (d.TheoDoi)
            {
                var mBo = new ToolStripMenuItem("Bỏ theo dõi (mọi Chủ kho)");
                mBo.Click += (s, e) =>
                {
                    _cfg.BoTheoDoi(tpl, null);
                    HenLuuCfg();
                    NapBangTheoDoi();
                };
                menu.Items.Add(mBo);
            }

            var mKe = new ToolStripMenuItem("Đổi kệ cho loại món này");
            string tay = _cfg.KeCuaMon(tpl);
            var tuDoan = new ToolStripMenuItem("(tự đoán theo loại)") { Checked = string.IsNullOrEmpty(tay) };
            tuDoan.Click += (s, e) => { _cfg.DatKeCuaMon(tpl, null); HenLuuCfg(); NapBangKe(); };
            mKe.DropDownItems.Add(tuDoan);
            foreach (var ke in KeHang.TatCa)
            {
                if (ke == KeHang.RAC) continue;   // rác đi qua "Đánh dấu rác"
                string k = ke;
                var it = new ToolStripMenuItem(KeHang.TenHienThi(k)) { Checked = tay == k };
                it.Click += (s, e) => { _cfg.DatKeCuaMon(tpl, k); HenLuuCfg(); NapBangKe(); };
                mKe.DropDownItems.Add(it);
            }
            menu.Items.Add(mKe);

            var mPhanBo = new ToolStripMenuItem("Xem phân bố");
            mPhanBo.Click += (s, e) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(tpl + " " + d.Ten + (d.Khoa.Up > 0 ? " +" + d.Khoa.Up : "") + (d.Khoa.Han ? " (có hạn)" : "") + (d.Khoa.Khoa ? " [khoá]" : ""));
                sb.AppendLine("Tổng " + d.Tong + " · giữ " + d.Giu + " · còn " + d.KhaDung + " · " + d.SoO + " ô");
                sb.AppendLine();
                foreach (var kv in d.PhanBo.OrderBy(x => x.Key))
                {
                    var t = _dp.So.Lay(kv.Key);
                    sb.AppendLine(NameMask.Apply(kv.Key) + (t != null ? " (" + NameMask.Apply(t.TenNV) + ")" : "")
                                  + ": túi " + kv.Value[0] + ", rương " + kv.Value[1]);
                }
                if (d.TrenAccNha) sb.AppendLine("+ một phần trên clone đang nhả (không tính)");
                MessageBox.Show(this, sb.ToString(), "Phân bố");
            };
            menu.Items.Add(mPhanBo);
        }

        private void ThemTheoDoi(short tpl)
        {
            var ck = _cfg.ChuKho;
            if (ck.Count == 0) { MessageBox.Show(this, "Chưa có Chủ kho nào (Cài đặt → Kho). Theo dõi báo qua tin nhắn cho Chủ kho.", "Theo dõi"); return; }
            string ai = ck.Count == 1 ? ck[0] : HopNhap.HoiChon(this, "Theo dõi " + tpl, "Báo cho Chủ kho nào?", ck.ToArray(), ck[0], false);
            if (string.IsNullOrEmpty(ai)) return;
            var n = HopNhap.HoiSo(this, "Theo dõi " + tpl + " " + BangMon.Ten(tpl),
                "Báo khi tổng khả dụng xuống dưới N (và khi lên lại ≥ N).\n-1 = báo mỗi khi món này về kho.", -1, -1, 1000000);
            if (n == null) return;
            _cfg.DatTheoDoi(tpl, n.Value, ai);
            HenLuuCfg();
            NapBangTheoDoi();
            XepLog("Theo dõi " + tpl + " cho " + ai + (n.Value >= 0 ? ", ngưỡng " + n.Value : ", báo mỗi lần về"), false);
        }
    }
}
