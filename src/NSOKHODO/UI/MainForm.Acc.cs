using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NSOKHODO.Auto;
using NSOKHODO.Client;
using NSOKHODO.Fleet;
using NSOKHODO.Kho;
using NSOKHODO.Protocol;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Tab <b>Acc</b>: lưới VirtualMode của NSOBAOTATL + các cột kho (SPEC §10).
    /// Mọi giá trị lấy theo CHỈ SỐ dòng — không nhét state vào row.Tag.
    /// </summary>
    public partial class MainForm
    {
        private DataGridView _gridAcc;

        private enum CotAcc
        {
            So, TaiKhoan, NhanVat, MayChu, Vai, Ke, KetNoi, Khu, TuiTrong, RuongTrong, Xu, TrangThaiKho, PhienGd,
            MatKn, Chan, LoiDn, Proxy,
        }

        private void DungTabAcc(TabPage tab)
        {
            _gridAcc = TaoLuoiAo();
            _gridAcc.CellValueNeeded += GridAccValueNeeded;
            _gridAcc.CellFormatting += (s, e) =>
            {
                // Chờ lượt login (Cài đặt → Kho → Đăng nhập) = vàng nhạt, như NSOLITEPRO.
                if (e.RowIndex < 0 || e.RowIndex >= Accounts.Count || e.ColumnIndex != (int)CotAcc.KetNoi) return;
                var c = ClientCua(Accounts[e.RowIndex]);
                if (c != null && c.WaitingLoginSlot) e.CellStyle.BackColor = Color.FromArgb(255, 255, 220);
            };
            ThemCot(_gridAcc, "#", 40);
            ThemCot(_gridAcc, "Tài khoản", 120);
            ThemCot(_gridAcc, "Nhân vật", 110);
            ThemCot(_gridAcc, "Máy chủ", 80);
            ThemCot(_gridAcc, "Vai", 90);
            ThemCot(_gridAcc, "Kệ", 100);
            ThemCot(_gridAcc, "Kết nối", 100);
            ThemCot(_gridAcc, "Khu", 90);
            ThemCot(_gridAcc, "Túi trống", 70);
            ThemCot(_gridAcc, "Rương trống", 80);
            ThemCot(_gridAcc, "Xu", 100);
            ThemCot(_gridAcc, "Trạng thái kho", 170);
            ThemCot(_gridAcc, "Phiên GD", 170);
            ThemCot(_gridAcc, "Mất KN", 56);
            ThemCot(_gridAcc, "Chặn", 46);
            ThemCot(_gridAcc, "Lỗi ĐN", 52);
            ThemCot(_gridAcc, "Proxy", 140);

            var menu = new ContextMenuStrip();
            menu.Opening += (s, e) => DungMenuAcc(menu);
            _gridAcc.ContextMenuStrip = menu;
            _gridAcc.CellMouseDown += (s, e) =>
            {
                // Chuột phải vào dòng chưa chọn -> chọn dòng đó (thói quen Explorer).
                if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
                if (!_gridAcc.Rows[e.RowIndex].Selected)
                {
                    _gridAcc.ClearSelection();
                    _gridAcc.Rows[e.RowIndex].Selected = true;
                    _gridAcc.CurrentCell = _gridAcc.Rows[e.RowIndex].Cells[0];
                }
            };

            var ghiChu = new Label
            {
                Dock = DockStyle.Bottom, Height = 20, ForeColor = Color.DimGray,
                Text = "  Chuột phải: đặt Leader / dự phòng, gán kệ, xem túi & rương, nhả / nhận lại. "
                     + "Nút Sửa / Xoá / Chạy / Dừng / Nhả clone… trên thanh nút áp cho các dòng đang chọn ở đây.",
            };
            tab.Controls.Add(_gridAcc);
            tab.Controls.Add(ghiChu);
            LamMoiSoDongAcc();
        }

        private static DataGridView TaoLuoiAo()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoGenerateColumns = false,
                EditMode = DataGridViewEditMode.EditProgrammatically,
                BackgroundColor = SystemColors.Window,
            };
            g.RowsDefaultCellStyle.BackColor = Color.White;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
            // Vẽ đệm đôi: lưới vẽ lại mỗi giây mà không có cái này thì nháy (DataGridView không mở thuộc tính ra ngoài).
            try
            {
                typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(g, true, null);
            }
            catch { }
            return g;
        }

        private static void ThemCot(DataGridView g, string header, int width)
        {
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.True,
            });
        }

        private void LamMoiSoDongAcc()
        {
            int n = Accounts.Count;
            if (_gridAcc.RowCount != n)
            {
                // VirtualMode giữ vùng chọn theo CHỈ SỐ: cắt dòng khi dòng cuối đang chọn thì WinForms ném.
                _gridAcc.ClearSelection();
                _gridAcc.RowCount = n;
            }
            VeLaiAcc();
        }

        private void LamMoiAcc() { LamMoiSoDongAcc(); }

        /// <summary>Vẽ lại đúng các dòng đang nhìn thấy (xem NSOBAOTATL: Invalidate() cả lưới rất tốn).</summary>
        private void VeLaiAcc()
        {
            if (_gridAcc == null || _tabs.SelectedTab != _tabAcc) return;
            int dau = _gridAcc.FirstDisplayedScrollingRowIndex;
            if (dau < 0) return;
            int het = Math.Min(dau + _gridAcc.DisplayedRowCount(true), _gridAcc.RowCount);
            for (int i = dau; i < het; i++)
            {
                try { _gridAcc.InvalidateRow(i); }
                catch { return; }
            }
        }

        private void GridAccValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= Accounts.Count) return;
            var a = Accounts[e.RowIndex];
            var c = ClientCua(a);
            string u = a.Username;
            switch ((CotAcc)e.ColumnIndex)
            {
                case CotAcc.So: e.Value = (e.RowIndex + 1).ToString(); break;
                case CotAcc.TaiKhoan: e.Value = NameMask.Apply(u); break;
                case CotAcc.NhanVat: e.Value = NameMask.Apply(c != null ? c.DisplayCharName : (TenNvDaBiet(u) ?? a.CharName)); break;
                case CotAcc.MayChu: e.Value = TenMayChu(a); break;
                case CotAcc.Vai: e.Value = ChuVai(u); break;
                case CotAcc.Ke:
                    e.Value = _dp.VaiCuaAcc(u) == VaiKho.Clone ? KeHang.TenHienThi(KeHang.KeCuaAcc(u, _cfg)) : "";
                    break;
                case CotAcc.KetNoi: e.Value = ChuKetNoi(c); break;
                case CotAcc.Khu: e.Value = ChuKhu(c); break;
                case CotAcc.TuiTrong:
                    {
                        var t = _dp.So.Lay(u);
                        e.Value = t == null || t.SoOTui <= 0 ? "" : t.TuiTrong + "/" + t.SoOTui;
                        break;
                    }
                case CotAcc.RuongTrong:
                    {
                        var t = _dp.So.Lay(u);
                        e.Value = t == null ? "" : (t.SoORuong < 0 ? "chưa đọc" : t.RuongTrong + "/" + t.SoORuong);
                        break;
                    }
                case CotAcc.Xu:
                    {
                        var t = _dp.So.Lay(u);
                        e.Value = t == null ? "" : ChuVan.SoDep(t.Xu);
                        break;
                    }
                case CotAcc.TrangThaiKho: e.Value = _dp.TrangThaiKho(u, c); break;
                case CotAcc.PhienGd:
                    {
                        var m = c != null ? c.ActiveModeAs<KhoMode>() : null;
                        var p = m != null ? m.PhienHienTai : null;
                        e.Value = p != null && !p.KetThuc ? NameMask.Apply(p.MoTa) : "";
                        break;
                    }
                case CotAcc.MatKn: { var d = DisconnectStats.Lay(u); e.Value = d.RotKhiDangChoi == 0 ? "" : d.RotKhiDangChoi.ToString(); break; }
                case CotAcc.Chan: { var d = DisconnectStats.Lay(u); e.Value = d.ServerChan == 0 ? "" : d.ServerChan.ToString(); break; }
                case CotAcc.LoiDn: { var d = DisconnectStats.Lay(u); e.Value = d.LoiDangNhap == 0 ? "" : d.LoiDangNhap.ToString(); break; }
                case CotAcc.Proxy: e.Value = MoTaProxy(a.Proxy); break;
            }
        }

        private string TenNvDaBiet(string u)
        {
            var t = _dp.So.Lay(u);
            return t != null && !string.IsNullOrEmpty(t.TenNV) ? t.TenNV : null;
        }

        private string ChuVai(string u)
        {
            if (_cfg.DaNha(u)) return "Đã nhả";
            var vai = _dp.VaiCuaAcc(u);
            switch (vai)
            {
                case VaiKho.Leader:
                    return string.Equals(u, _cfg.Leader, StringComparison.OrdinalIgnoreCase) ? "LEADER" : "LEADER (dự phòng)";
                case VaiKho.DuPhong:
                    return string.Equals(u, _cfg.Leader, StringComparison.OrdinalIgnoreCase) ? "Leader (vắng)" : "Dự phòng";
                case VaiKho.Clone: return "Clone";
                default: return "—";
            }
        }

        private static string TenMayChu(AccountConfig a)
        {
            if (!string.IsNullOrEmpty(a.ServerName)) return a.ServerName;
            var s = ServerList.GetByIndex(a.ServerIndex);
            return s != null ? s.Name : ("#" + a.ServerIndex);
        }

        private static string ChuKetNoi(NsoClient c)
        {
            if (c == null) return "—";
            string ket = c.TrangThaiKetNoi();
            if (ket != null) return ket;
            switch (c.State)
            {
                case ClientState.Disconnected: return "TẮT";
                case ClientState.LoggingIn: return "ĐANG VÀO";
                case ClientState.DataSync: return "TẢI DỮ LIỆU";
                case ClientState.SelectingChar: return "CHỌN NV";
                case ClientState.Dead: return "CHẾT";
                case ClientState.Error: return "LỖI";
                case ClientState.InGame: return "ONLINE";
                default: return c.State.ToString().ToUpperInvariant();
            }
        }

        private string ChuKhu(NsoClient c)
        {
            if (c == null || c.State != ClientState.InGame) return "";
            var m = c.GameState.CurrentMap;
            if (m == null) return "";
            if (c.GameState.IsChangingMap) return "đang chuyển";
            if (m.MapId != _cfg.Map) return MapNames.NameOf(m.MapId);
            string k = m.ZoneId.ToString();
            if (m.ZoneId == _cfg.KhuChinh) return k + " (chính)";
            if (m.ZoneId == _cfg.KhuPhu) return k + " (phụ)";
            return k;
        }

        private static string MoTaProxy(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            bool http; string bare;
            AccountConfig.SplitProxy(p, out http, out bare);
            string[] x = bare.Split(':');
            string hostPort = x.Length >= 2 ? (x[0] + ":" + x[1]) : bare;
            return (http ? "http " : "") + hostPort;
        }

        private List<int> DongAccDangChon()
        {
            var r = new List<int>();
            foreach (DataGridViewRow row in _gridAcc.SelectedRows)
                if (row.Index >= 0 && row.Index < Accounts.Count) r.Add(row.Index);
            if (r.Count == 0 && _gridAcc.CurrentRow != null && _gridAcc.CurrentRow.Selected
                && _gridAcc.CurrentRow.Index >= 0 && _gridAcc.CurrentRow.Index < Accounts.Count)
                r.Add(_gridAcc.CurrentRow.Index);
            r.Sort();
            return r;
        }

        private List<AccountConfig> AccDangChon()
        {
            return DongAccDangChon().Select(i => Accounts[i]).ToList();
        }

        // ---------------- menu chuột phải ----------------

        private void DungMenuAcc(ContextMenuStrip menu)
        {
            menu.Items.Clear();
            var sel = AccDangChon();
            if (sel.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem("(chưa chọn dòng nào)") { Enabled = false });
                return;
            }
            var a = sel[0];
            bool mot = sel.Count == 1;

            var mLeader = new ToolStripMenuItem("Đặt làm Leader") { Enabled = mot };
            mLeader.Click += (s, e) => DatVai(a.Username, true);
            var mPhong = new ToolStripMenuItem("Đặt làm Leader dự phòng") { Enabled = mot };
            mPhong.Click += (s, e) => DatVai(a.Username, false);
            var mBoVai = new ToolStripMenuItem("Bỏ vai Leader / dự phòng");
            mBoVai.Click += (s, e) =>
            {
                foreach (var x in sel)
                {
                    if (string.Equals(x.Username, _cfg.Leader, StringComparison.OrdinalIgnoreCase)) _cfg.Leader = "";
                    if (string.Equals(x.Username, _cfg.LeaderDuPhong, StringComparison.OrdinalIgnoreCase)) _cfg.LeaderDuPhong = "";
                }
                HenLuuCfg();
                NapCaiDat();
                XepLog("Đã bỏ vai Leader / dự phòng của " + sel.Count + " acc.", false);
            };
            menu.Items.Add(mLeader);
            menu.Items.Add(mPhong);
            menu.Items.Add(mBoVai);
            menu.Items.Add(new ToolStripSeparator());

            var mKe = new ToolStripMenuItem("Kệ của clone");
            foreach (var ke in KeHang.TatCa)
            {
                string k = ke;
                var it = new ToolStripMenuItem(KeHang.TenHienThi(k))
                {
                    Checked = mot && KeHang.KeCuaAcc(a.Username, _cfg) == k,
                };
                it.Click += (s, e) =>
                {
                    foreach (var x in sel) _cfg.DatKeCuaAcc(x.Username, k == KeHang.KHAC ? null : k);
                    HenLuuCfg();
                    NapBangKe();
                    XepLog("Gán kệ " + KeHang.TenHienThi(k) + " cho " + sel.Count + " acc.", false);
                };
                mKe.DropDownItems.Add(it);
            }
            menu.Items.Add(mKe);

            var mXem = new ToolStripMenuItem("Xem túi && rương…") { Enabled = mot };
            mXem.Click += (s, e) => XemTui(a.Username);
            menu.Items.Add(mXem);
            menu.Items.Add(new ToolStripSeparator());

            var mNha = new ToolStripMenuItem("Nhả clone…");
            mNha.Click += BtnNhaClone;
            var mNhan = new ToolStripMenuItem("Nhận lại…");
            mNhan.Click += BtnNhanLai;
            menu.Items.Add(mNha);
            menu.Items.Add(mNhan);
        }

        private void DatVai(string u, bool laLeader)
        {
            if (_cfg.DaNha(u)) { XepLog("Acc đang nhả — nhận lại trước khi đặt vai.", false); return; }
            if (laLeader)
            {
                if (string.Equals(_cfg.LeaderDuPhong, u, StringComparison.OrdinalIgnoreCase)) _cfg.LeaderDuPhong = "";
                _cfg.Leader = u;
            }
            else
            {
                if (string.Equals(_cfg.Leader, u, StringComparison.OrdinalIgnoreCase)) _cfg.Leader = "";
                _cfg.LeaderDuPhong = u;
            }
            _cfg.DatKeCuaAcc(u, null);
            HenLuuCfg();
            NapCaiDat();
            XepLog((laLeader ? "Leader" : "Leader dự phòng") + " = " + u, false);
        }

        private void XemTui(string u)
        {
            var t = _dp.So.Lay(u);
            if (t == null) { MessageBox.Show(this, "Chưa có số liệu của " + u + " (acc chưa từng vào game).", "Túi & rương"); return; }
            var sb = new StringBuilder();
            sb.AppendLine(NameMask.Apply(u) + " — " + NameMask.Apply(t.TenNV) + (t.Online ? " (online)" : " (offline, thấy lúc " + t.ThayLuc.ToString("dd/MM HH:mm") + ")"));
            sb.AppendLine("Xu: " + ChuVan.SoDep(t.Xu));
            sb.AppendLine("Túi: " + t.TuiDung + "/" + t.SoOTui + " ô");
            foreach (var m in t.Mon.Where(x => !x.TrongRuong).OrderBy(x => x.Slot))
                sb.AppendLine("  [" + m.Slot + "] " + MoTaMon(m.Khoa) + " x" + m.SoLuong);
            sb.AppendLine("Rương: " + (t.SoORuong < 0 ? "chưa đọc" : t.RuongDung + "/" + t.SoORuong + " ô (đọc lúc " + t.DocRuongLuc.ToString("dd/MM HH:mm") + ")"));
            foreach (var m in t.Mon.Where(x => x.TrongRuong).OrderBy(x => x.Slot))
                sb.AppendLine("  [" + m.Slot + "] " + MoTaMon(m.Khoa) + " x" + m.SoLuong);
            using (var f = new Form
            {
                Text = "Túi & rương — " + NameMask.Apply(u), Width = 520, Height = 560,
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            })
            {
                f.Controls.Add(new TextBox
                {
                    Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9f), Text = sb.ToString(),
                });
                f.ShowDialog(this);
            }
        }

        private static string MoTaMon(KhoaMon k)
        {
            return k.Tpl + " " + BangMon.Ten(k.Tpl) + (k.Up > 0 ? " +" + k.Up : "") + (k.Han ? " (hạn)" : "") + (k.Khoa ? " [khoá]" : "");
        }
    }
}
