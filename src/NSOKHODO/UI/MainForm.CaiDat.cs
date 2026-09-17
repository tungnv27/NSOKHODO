using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using NSOKHODO.Client;
using NSOKHODO.Kho;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Tab <b>Cài đặt</b> — tab con: Kho · Kệ hàng · Gói rút · Theo dõi · Chat &amp; rao · Log (SPEC §4, §10).
    /// Sửa ô nào là ghi vào <see cref="KhoConfig"/> ngay (bộ điều phối đọc ở nhịp sau), file lưu ở nhịp
    /// 1 giây kế tiếp. <see cref="_dangNapCaiDat"/> chặn sự kiện khi đang nạp giá trị vào ô.
    /// </summary>
    public partial class MainForm
    {
        private bool _dangNapCaiDat;
        private TabControl _tabsCaiDat;
        private TabPage _tcKho, _tcKe, _tcGoi, _tcTheo, _tcChat, _tcLog;

        // Kho
        private NumericUpDown _numMap, _numKhuChinh, _numKhuPhu, _numLx, _numLy, _numNguong, _numChoNguoi, _numChoLa,
                              _numChoBot, _numMoiLai, _numGiuCua, _numChoCoMat, _numXuTran, _numXuNguong;
        private Label _lblTenMap;
        private ComboBox _cbLeader, _cbDuPhong;
        private TextBox _txtChuKho;
        private RadioButton _rdNhanTatCa, _rdNhanChiChu;
        private CheckBox _chkBatNap, _chkBatRut, _chkBatDon, _chkBatCat, _chkBatXu, _chkBatLenh, _chkBatTheo, _chkBatBaoCao;
        private CheckBox _chkGioiHanLogin;
        private NumericUpDown _numGioiHanLogin;
        // Kệ
        private DataGridView _gridKeAcc, _gridKeMon;
        private ListBox _lstRac;
        private NumericUpDown _numRacNguong;
        private Label _lblSucChuaKe;
        // Gói
        private ListBox _lstGoi;
        private TextBox _txtTenGoi, _txtDongGoi;
        // Theo dõi
        private DataGridView _gridTheoDoi;
        private TextBox _txtGioBaoCao;
        // Chat & rao
        private CheckBox _chkRao, _chkBaoCd, _chkNguoiLa;
        private TextBox _txtRaoMau;
        private RadioButton _rdRaoThongMinh, _rdRaoDeu;
        private NumericUpDown _numRaoNhip, _numRaoVang;
        private Label _lblRaoXem;
        // Log
        private CheckBox _chkLogFile, _chkLogHex;
        private NumericUpDown _numGiuLog;

        private const int X_NHAN = 10, X_O = 280;

        private void DungTabCaiDat(TabPage tab)
        {
            _tabsCaiDat = new TabControl { Dock = DockStyle.Fill };
            _tcKho = new TabPage("Kho");
            _tcKe = new TabPage("Kệ hàng");
            _tcGoi = new TabPage("Gói rút");
            _tcTheo = new TabPage("Theo dõi");
            _tcChat = new TabPage("Chat & rao");
            _tcLog = new TabPage("Log");
            _tabsCaiDat.TabPages.AddRange(new[] { _tcKho, _tcKe, _tcGoi, _tcTheo, _tcChat, _tcLog });
            _tabsCaiDat.SelectedIndexChanged += (s, e) => LamMoiCaiDat();
            tab.Controls.Add(_tabsCaiDat);

            _dangNapCaiDat = true;
            try
            {
                DungTrangKho(_tcKho);
                DungTrangKe(_tcKe);
                DungTrangGoi(_tcGoi);
                DungTrangTheoDoi(_tcTheo);
                DungTrangChat(_tcChat);
                DungTrangLog(_tcLog);
            }
            finally { _dangNapCaiDat = false; }
            NapCaiDat();
        }

        // ==================== khung dựng chung ====================

        private static Panel TrangCuon(TabPage tab)
        {
            var p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
            tab.Controls.Add(p);
            return p;
        }

        private int TieuDe(Control host, string text, int y)
        {
            host.Controls.Add(new Label
            {
                Text = text, Left = 4, Top = y, Width = 600, Height = 20,
                Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(40, 70, 130),
            });
            return y + 26;
        }

        private static void GhiChu(Control host, int x, int y, string text, int rong = 520)
        {
            host.Controls.Add(new Label { Text = text, Left = x, Top = y + 3, Width = rong, ForeColor = Color.DimGray });
        }

        private NumericUpDown ONhapSo(Control host, ref int y, string nhan, int min, int max, string ghiChu, Action<int> gan)
        {
            host.Controls.Add(new Label { Text = nhan, Left = X_NHAN, Top = y + 3, Width = X_O - X_NHAN - 6 });
            var n = new NumericUpDown
            {
                Left = X_O, Top = y, Width = 120, Minimum = min, Maximum = max, ThousandsSeparator = max >= 100000,
            };
            n.ValueChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                gan((int)n.Value);
                HenLuuCfg();
            };
            host.Controls.Add(n);
            if (ghiChu != null) GhiChu(host, X_O + 128, y, ghiChu);
            y += 28;
            return n;
        }

        private CheckBox OTick(Control host, ref int y, string text, Action<bool> gan, string ghiChu = null)
        {
            var c = new CheckBox { Text = text, Left = X_NHAN, Top = y, Width = 360 };
            c.CheckedChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                gan(c.Checked);
                HenLuuCfg();
            };
            host.Controls.Add(c);
            if (ghiChu != null) GhiChu(host, 376, y, ghiChu);
            y += 24;
            return c;
        }

        private static decimal Kep(NumericUpDown n, long v)
        {
            if (v < n.Minimum) return n.Minimum;
            if (v > n.Maximum) return n.Maximum;
            return v;
        }

        private static Button NutNho(Control host, string text, int x, int y, int rong, EventHandler bam)
        {
            var b = new Button { Text = text, Left = x, Top = y, Width = rong, Height = 24 };
            b.Click += bam;
            host.Controls.Add(b);
            return b;
        }

        // ==================== Kho ====================

        private void DungTrangKho(TabPage tab)
        {
            var h = TrangCuon(tab);
            int y = 4;
            y = TieuDe(h, "Vị trí (D12, D14)", y);
            _numMap = ONhapSo(h, ref y, "Map kho", 0, 300, null, v => { _cfg.Map = v; CapNhatTenMap(); });
            NutNho(h, "Chọn…", X_O + 128, y - 29, 60, BtnChonMapKho);
            _lblTenMap = new Label { Left = X_O + 194, Top = y - 25, Width = 300, ForeColor = Color.DimGray };
            h.Controls.Add(_lblTenMap);
            _numKhuChinh = ONhapSo(h, ref y, "Khu chính (Leader, giao nhận)", -1, 254, "-1 = chưa cài → bot đứng chờ, không đi đâu", v => _cfg.KhuChinh = v);
            _numKhuPhu = ONhapSo(h, ref y, "Khu phụ (clone đứng chờ)", -1, 254, "phải KHÁC khu chính", v => _cfg.KhuPhu = v);
            y += 6;

            y = TieuDe(h, "Leader (D3, D13, D48)", y);
            _cbLeader = ComboChonAcc(h, ref y, "Leader", v => _cfg.Leader = v);
            _cbDuPhong = ComboChonAcc(h, ref y, "Leader dự phòng", v => _cfg.LeaderDuPhong = v);
            _numLx = ONhapSo(h, ref y, "Chỗ đứng của Leader — X", 0, 30000, "X = 0 và Y = 0: đứng đâu cũng được", v => _cfg.LeaderX = (short)v);
            _numLy = ONhapSo(h, ref y, "Chỗ đứng của Leader — Y", 0, 30000, null, v => _cfg.LeaderY = (short)v);
            NutNho(h, "Lấy chỗ Leader đang đứng", X_O, y, 170, BtnLayChoLeader);
            NutNho(h, "Xoá toạ độ", X_O + 176, y, 90, (s, e) => { _numLx.Value = 0; _numLy.Value = 0; });
            y += 34;

            y = TieuDe(h, "Chủ kho & nhận đồ (D16, D17, D24)", y);
            h.Controls.Add(new Label { Text = "Chủ kho (mỗi dòng một tên nhân vật)", Left = X_NHAN, Top = y + 3, Width = X_O - 16 });
            _txtChuKho = new TextBox { Left = X_O, Top = y, Width = 220, Height = 72, Multiline = true, ScrollBars = ScrollBars.Vertical };
            _txtChuKho.TextChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                _cfg.DatChuKho(_txtChuKho.Lines);
                HenLuuCfg();
            };
            h.Controls.Add(_txtChuKho);
            GhiChu(h, X_O + 228, y, "Chỉ Chủ kho được xem kho / rút đồ qua tin nhắn.");
            y += 80;

            h.Controls.Add(new Label { Text = "Ai được nạp đồ vào Leader", Left = X_NHAN, Top = y + 3, Width = X_O - 16 });
            // RadioButton PHẢI có Panel bọc riêng (bẫy WinForms NSOBAOTATL: chung cha là chung nhóm).
            var nhomNhan = new Panel { Left = X_O, Top = y, Width = 300, Height = 24 };
            _rdNhanTatCa = new RadioButton { Text = "Tất cả", Left = 0, Top = 2, Width = 80 };
            _rdNhanChiChu = new RadioButton { Text = "Chỉ Chủ kho", Left = 90, Top = 2, Width = 120 };
            EventHandler doiNhan = (s, e) =>
            {
                if (_dangNapCaiDat) return;
                _cfg.CheDoNhan = _rdNhanChiChu.Checked ? CheDoNhan.ChiChuKho : CheDoNhan.TatCa;
                HenLuuCfg();
            };
            _rdNhanTatCa.CheckedChanged += doiNhan;
            _rdNhanChiChu.CheckedChanged += doiNhan;
            nhomNhan.Controls.Add(_rdNhanTatCa);
            nhomNhan.Controls.Add(_rdNhanChiChu);
            h.Controls.Add(nhomNhan);
            GhiChu(h, X_O + 228, y, "Tất cả: người lạ có thể đổ rác làm đầy kho (SPEC §14 mục 8).");
            y += 30;

            _numNguong = ONhapSo(h, ref y, "Túi Leader còn dưới … ô thì từ chối nạp", 1, 120, "Chủ kho vẫn nạp được sau lệnh nap", v => _cfg.NguongNhan = v);
            _numChoNguoi = ONhapSo(h, ref y, "Chờ mỗi bước — Chủ kho (giây)", 10, 900, null, v => _cfg.ChoNguoiGiay = v);
            _numChoLa = ONhapSo(h, ref y, "Chờ mỗi bước — người lạ (giây)", 10, 900, null, v => _cfg.ChoNguoiLaGiay = v);
            _numChoBot = ONhapSo(h, ref y, "Chờ mỗi bước — bot với bot (giây)", 5, 300, null, v => _cfg.ChoBotGiay = v);
            _numMoiLai = ONhapSo(h, ref y, "Mời lại sau (giây)", 5, 300, "server khoá lời mời 31 giây (T4)", v => _cfg.MoiLaiGiay = v);
            _numGiuCua = ONhapSo(h, ref y, "Giữ cửa sau lệnh nap (giây)", 10, 900, "chỉ nhận lời mời của Chủ kho vừa nhắn nap", v => _cfg.GiuCuaGiay = v);
            _numChoCoMat = ONhapSo(h, ref y, "Lệnh rút chờ người nhận tới (phút)", 1, 1440, null, v => _cfg.ChoCoMatPhut = v);
            y += 6;

            y = TieuDe(h, "Xu (D45)", y);
            _numXuTran = ONhapSo(h, ref y, "Trần xu mỗi nhân vật", 0, 2000000000, "đo T14: 2 tỷ", v => _cfg.XuTran = v);
            _numXuNguong = ONhapSo(h, ref y, "Leader vượt mức này thì dồn xu sang clone", 0, 2000000000, null, v => _cfg.XuNguong = v);
            y += 6;

            y = TieuDe(h, "Bật / tắt từng tính năng (D50 — khoanh vùng khi có lỗi)", y);
            _chkBatNap = OTick(h, ref y, "Nhận đồ vào Leader", v => _cfg.BatNap = v);
            _chkBatRut = OTick(h, ref y, "Giao lệnh rút", v => _cfg.BatRut = v, "tắt: lệnh vẫn vào hàng chờ nhưng không giao");
            _chkBatDon = OTick(h, ref y, "Dọn kho: Leader chuyển đồ sang clone", v => _cfg.BatDonKho = v);
            _chkBatCat = OTick(h, ref y, "Cất / đọc rương (Leader và clone)", v => _cfg.BatCatRuong = v);
            _chkBatXu = OTick(h, ref y, "Dồn xu từ Leader sang clone", v => _cfg.BatDonXu = v);
            _chkBatLenh = OTick(h, ref y, "Lệnh chat của Chủ kho", v => _cfg.BatLenhChat = v);
            _chkBatTheo = OTick(h, ref y, "Theo dõi món", v => _cfg.BatTheoDoi = v);
            _chkBatBaoCao = OTick(h, ref y, "Báo cáo ngày", v => _cfg.BatBaoCao = v);
            y += 6;

            // Cổng login dùng chung với NSOLITEPRO (Fleet/LoginGate): lưu ở Data/settings.txt, KHÔNG ở cài đặt kho
            // -> không qua OTick/ONhapSo (hai hàm đó ghi KhoConfig).
            y = TieuDe(h, "Đăng nhập (giống NSOLITEPRO)", y);
            _chkGioiHanLogin = new CheckBox { Text = "Giới hạn số acc login cùng lúc vào một máy chủ", Left = X_NHAN, Top = y, Width = 360 };
            h.Controls.Add(_chkGioiHanLogin);
            _numGioiHanLogin = new NumericUpDown { Left = X_O + 100, Top = y, Width = 56, Minimum = 1, Maximum = 100 };
            h.Controls.Add(_numGioiHanLogin);
            GhiChu(h, X_O + 162, y, "acc / máy chủ");
            _chkGioiHanLogin.CheckedChanged += (s, e) =>
            {
                _numGioiHanLogin.Enabled = _chkGioiHanLogin.Checked;
                if (_dangNapCaiDat) return;
                Fleet.LoginGate.Enabled = _chkGioiHanLogin.Checked;
                Fleet.LoginGate.Save();
            };
            _numGioiHanLogin.ValueChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                Fleet.LoginGate.MaxPerGroup = (int)_numGioiHanLogin.Value;
                Fleet.LoginGate.Save();
            };
            y += 26;
            GhiChu(h, X_NHAN, y, "Đếm theo IP máy chủ game (không theo proxy). Acc chưa tới lượt hiện \"CHỜ SLOT\" ở tab Acc; "
                                 + "hai lượt cách nhau ít nhất 1 giây. Áp dụng cả khi rớt mạng vào lại. Đếm riêng từng cửa sổ tool.", 900);
            y += 24;
        }

        private ComboBox ComboChonAcc(Control host, ref int y, string nhan, Action<string> gan)
        {
            host.Controls.Add(new Label { Text = nhan, Left = X_NHAN, Top = y + 3, Width = X_O - 16 });
            var cb = new ComboBox { Left = X_O, Top = y, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cb.SelectedIndexChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                string v = cb.SelectedIndex <= 0 ? "" : (cb.SelectedItem as string ?? "");
                gan(v);
                // Leader và dự phòng không được trùng nhau.
                if (v.Length > 0)
                {
                    if (cb == _cbLeader && string.Equals(_cfg.LeaderDuPhong, v, StringComparison.OrdinalIgnoreCase)) _cfg.LeaderDuPhong = "";
                    if (cb == _cbDuPhong && string.Equals(_cfg.Leader, v, StringComparison.OrdinalIgnoreCase)) _cfg.Leader = "";
                    _cfg.DatKeCuaAcc(v, null);
                }
                HenLuuCfg();
                NapCaiDat();
            };
            host.Controls.Add(cb);
            y += 28;
            return cb;
        }

        private void CapNhatTenMap()
        {
            if (_lblTenMap != null) _lblTenMap.Text = MapNames.LabelWithLevel(_cfg.Map);
        }

        private void BtnChonMapKho(object sender, EventArgs e)
        {
            using (var f = new MapPickerForm(_cfg.Map))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.SelectedMapId < 0) return;
                _numMap.Value = Kep(_numMap, f.SelectedMapId);
            }
        }

        private void BtnLayChoLeader(object sender, EventArgs e)
        {
            string u = !string.IsNullOrEmpty(_dp.LeaderAcc) ? _dp.LeaderAcc : _cfg.Leader;
            var a = Accounts.FirstOrDefault(x => string.Equals(x.Username, u, StringComparison.OrdinalIgnoreCase));
            var c = a != null ? ClientCua(a) : null;
            var mc = c != null ? c.GameState.MyChar : null;
            if (c == null || c.State != ClientState.InGame || mc == null)
            {
                MessageBox.Show(this, "Leader (" + (string.IsNullOrEmpty(u) ? "chưa chọn" : u) + ") chưa vào game nên chưa có toạ độ.", "Lấy chỗ đứng");
                return;
            }
            var m = c.GameState.CurrentMap;
            if (m.MapId != _cfg.Map)
                MessageBox.Show(this, "Leader đang ở " + MapNames.NameOf(m.MapId) + ", không phải map kho (" + MapNames.NameOf(_cfg.Map)
                                + "). Toạ độ chỉ đúng với map đang đứng.", "Lấy chỗ đứng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _numLx.Value = Kep(_numLx, mc.Cx);
            _numLy.Value = Kep(_numLy, mc.Cy);
            XepLog(string.Format("Chỗ đứng Leader = ({0},{1}) — map {2} khu {3}.", mc.Cx, mc.Cy, m.MapId, m.ZoneId), false);
        }

        /// <summary>Nạp lại danh sách acc cho hai ô Leader / dự phòng và bảng kệ.</summary>
        private void NapDanhSachAccCaiDat()
        {
            if (_cbLeader == null) return;
            _dangNapCaiDat = true;
            try
            {
                foreach (var cb in new[] { _cbLeader, _cbDuPhong })
                {
                    cb.Items.Clear();
                    cb.Items.Add("(chưa chọn)");
                    foreach (var a in Accounts)
                        if (!string.IsNullOrEmpty(a.Username) && !cb.Items.Contains(a.Username)) cb.Items.Add(a.Username);
                }
                ChonTrongCombo(_cbLeader, _cfg.Leader);
                ChonTrongCombo(_cbDuPhong, _cfg.LeaderDuPhong);
            }
            finally { _dangNapCaiDat = false; }
            NapBangKe();
        }

        private static void ChonTrongCombo(ComboBox cb, string v)
        {
            int i = string.IsNullOrEmpty(v) ? 0 : cb.Items.IndexOf(v);
            if (i < 0)
            {
                // Acc đã cài nhưng không còn trong danh sách: vẫn hiện để user thấy.
                cb.Items.Add(v);
                i = cb.Items.Count - 1;
            }
            cb.SelectedIndex = i;
        }

        /// <summary>Đổ mọi giá trị từ cài đặt vào ô (không bắn sự kiện ghi).</summary>
        private void NapCaiDat()
        {
            if (_numMap == null) return;
            _dangNapCaiDat = true;
            try
            {
                _numMap.Value = Kep(_numMap, _cfg.Map);
                _numKhuChinh.Value = Kep(_numKhuChinh, _cfg.KhuChinh);
                _numKhuPhu.Value = Kep(_numKhuPhu, _cfg.KhuPhu);
                _numLx.Value = Kep(_numLx, _cfg.LeaderX);
                _numLy.Value = Kep(_numLy, _cfg.LeaderY);
                _txtChuKho.Lines = _cfg.ChuKho.ToArray();
                _rdNhanTatCa.Checked = _cfg.CheDoNhan == CheDoNhan.TatCa;
                _rdNhanChiChu.Checked = _cfg.CheDoNhan == CheDoNhan.ChiChuKho;
                _numNguong.Value = Kep(_numNguong, _cfg.NguongNhan);
                _numChoNguoi.Value = Kep(_numChoNguoi, _cfg.ChoNguoiGiay);
                _numChoLa.Value = Kep(_numChoLa, _cfg.ChoNguoiLaGiay);
                _numChoBot.Value = Kep(_numChoBot, _cfg.ChoBotGiay);
                _numMoiLai.Value = Kep(_numMoiLai, _cfg.MoiLaiGiay);
                _numGiuCua.Value = Kep(_numGiuCua, _cfg.GiuCuaGiay);
                _numChoCoMat.Value = Kep(_numChoCoMat, _cfg.ChoCoMatPhut);
                _numXuTran.Value = Kep(_numXuTran, _cfg.XuTran);
                _numXuNguong.Value = Kep(_numXuNguong, _cfg.XuNguong);
                _chkBatNap.Checked = _cfg.BatNap;
                _chkBatRut.Checked = _cfg.BatRut;
                _chkBatDon.Checked = _cfg.BatDonKho;
                _chkBatCat.Checked = _cfg.BatCatRuong;
                _chkBatXu.Checked = _cfg.BatDonXu;
                _chkBatLenh.Checked = _cfg.BatLenhChat;
                _chkBatTheo.Checked = _cfg.BatTheoDoi;
                _chkBatBaoCao.Checked = _cfg.BatBaoCao;
                _chkGioiHanLogin.Checked = Fleet.LoginGate.Enabled;
                _numGioiHanLogin.Value = Kep(_numGioiHanLogin, Fleet.LoginGate.MaxPerGroup);
                _numGioiHanLogin.Enabled = Fleet.LoginGate.Enabled;

                _numRacNguong.Value = Kep(_numRacNguong, _cfg.RacBaoNguong);
                _txtGioBaoCao.Text = _cfg.BaoCaoGio ?? "";

                _chkRao.Checked = _cfg.RaoBat;
                _txtRaoMau.Text = _cfg.RaoMau ?? "";
                _rdRaoThongMinh.Checked = _cfg.RaoCheDo == CheDoRao.ThongMinh;
                _rdRaoDeu.Checked = _cfg.RaoCheDo == CheDoRao.Deu;
                _numRaoNhip.Value = Kep(_numRaoNhip, _cfg.RaoNhipGiay);
                _numRaoVang.Value = Kep(_numRaoVang, _cfg.RaoVangGiay);
                _chkBaoCd.Checked = _cfg.BaoCongDong;
                _chkNguoiLa.Checked = _cfg.ChatVoiNguoiLa;

                _chkLogFile.Checked = _cfg.LogFile;
                _numGiuLog.Value = Kep(_numGiuLog, _cfg.GiuLogNgay);
                _chkLogHex.Checked = _cfg.LogHexGiaoDich;
            }
            finally { _dangNapCaiDat = false; }
            CapNhatTenMap();
            NapDanhSachAccCaiDat();
            NapDanhSachRac();
            NapBangKeMon();
            NapDanhSachGoi();
            NapBangTheoDoi();
        }

        private void LamMoiCaiDat()
        {
            var t = _tabsCaiDat.SelectedTab;
            if (t == _tcChat) CapNhatXemRao();
            else if (t == _tcKe) CapNhatSucChuaKe();
        }

        // ==================== Kệ hàng ====================

        private void DungTrangKe(TabPage tab)
        {
            var bang = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            bang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            bang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            bang.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            bang.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            tab.Controls.Add(bang);

            // ---- clone -> kệ ----
            var nhomAcc = new GroupBox { Text = "Clone → kệ (Leader và dự phòng không thuộc kệ)", Dock = DockStyle.Fill };
            _gridKeAcc = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, EditMode = DataGridViewEditMode.EditOnEnter,
                BackgroundColor = SystemColors.Window,
            };
            _gridKeAcc.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tài khoản", Width = 130, ReadOnly = true });
            _gridKeAcc.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nhân vật", Width = 120, ReadOnly = true });
            var cotKe = new DataGridViewComboBoxColumn { HeaderText = "Kệ", Width = 140, FlatStyle = FlatStyle.Flat };
            foreach (var k in KeHang.TatCa) cotKe.Items.Add(KeHang.TenHienThi(k));
            _gridKeAcc.Columns.Add(cotKe);
            _gridKeAcc.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Đang dùng / tổng ô", Width = 140, ReadOnly = true });
            _gridKeAcc.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridKeAcc.IsCurrentCellDirty) _gridKeAcc.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _gridKeAcc.CellValueChanged += (s, e) =>
            {
                if (_dangNapCaiDat || e.RowIndex < 0 || e.ColumnIndex != 2) return;
                var row = _gridKeAcc.Rows[e.RowIndex];
                string acc = row.Tag as string;
                string ke = KeHang.TuTenHienThi(row.Cells[2].Value as string);
                _cfg.DatKeCuaAcc(acc, ke == KeHang.KHAC ? null : ke);
                HenLuuCfg();
                XepLog("Kệ của " + acc + " = " + KeHang.TenHienThi(ke), false);
            };
            _gridKeAcc.DataError += (s, e) => { e.ThrowException = false; };
            nhomAcc.Controls.Add(_gridKeAcc);
            bang.Controls.Add(nhomAcc, 0, 0);

            // ---- loại món -> kệ ----
            var nhomMon = new GroupBox { Text = "Loại món gán kệ bằng tay (còn lại: tự đoán theo loại)", Dock = DockStyle.Fill };
            _gridKeMon = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = SystemColors.Window,
            };
            _gridKeMon.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", Width = 60 });
            _gridKeMon.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên", Width = 180 });
            _gridKeMon.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kệ", Width = 120 });
            var nutMon = new Panel { Dock = DockStyle.Bottom, Height = 32 };
            NutNho(nutMon, "Thêm…", 4, 4, 80, (s, e) =>
            {
                var id = HopNhap.HoiSo(this, "Gán kệ cho loại món", "ID món (template):", 0, 0, 32000);
                if (id == null) return;
                var ten = KeHang.TatCa.Where(k => k != KeHang.RAC).Select(KeHang.TenHienThi).ToArray();
                string chon = HopNhap.HoiChon(this, "Gán kệ cho " + id.Value + " " + BangMon.Ten((short)id.Value), "Kệ:", ten, ten[0], false);
                if (chon == null) return;
                _cfg.DatKeCuaMon((short)id.Value, KeHang.TuTenHienThi(chon));
                HenLuuCfg();
                NapBangKeMon();
            });
            NutNho(nutMon, "Xoá", 90, 4, 80, (s, e) =>
            {
                if (_gridKeMon.CurrentRow == null || !(_gridKeMon.CurrentRow.Tag is short)) return;
                _cfg.DatKeCuaMon((short)_gridKeMon.CurrentRow.Tag, null);
                HenLuuCfg();
                NapBangKeMon();
            });
            nhomMon.Controls.Add(_gridKeMon);
            nhomMon.Controls.Add(nutMon);
            bang.Controls.Add(nhomMon, 1, 0);

            // ---- rác ----
            var nhomRac = new GroupBox { Text = "Món rác (tool KHÔNG tự đoán — D43)", Dock = DockStyle.Fill };
            _lstRac = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            var nutRac = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            NutNho(nutRac, "Thêm…", 4, 4, 80, (s, e) =>
            {
                var id = HopNhap.HoiSo(this, "Thêm món rác", "ID món (template) đánh dấu là rác:", 0, 0, 32000);
                if (id == null) return;
                _cfg.DatRac((short)id.Value, true);
                HenLuuCfg();
                NapDanhSachRac();
            });
            NutNho(nutRac, "Xoá", 90, 4, 80, (s, e) =>
            {
                if (_lstRac.SelectedItem == null) return;
                short t;
                if (short.TryParse(((string)_lstRac.SelectedItem).Split(' ')[0], out t)) _cfg.DatRac(t, false);
                HenLuuCfg();
                NapDanhSachRac();
            });
            nutRac.Controls.Add(new Label { Text = "Báo khi kệ Rác đầy (%)", Left = 180, Top = 8, Width = 140 });
            _numRacNguong = new NumericUpDown { Left = 322, Top = 5, Width = 60, Minimum = 1, Maximum = 100 };
            _numRacNguong.ValueChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                _cfg.RacBaoNguong = (int)_numRacNguong.Value;
                HenLuuCfg();
            };
            nutRac.Controls.Add(_numRacNguong);
            nutRac.Controls.Add(new Label
            {
                Left = 4, Top = 34, Width = 520, ForeColor = Color.DimGray,
                Text = "Rác dồn vào clone thuộc kệ Rác. Đầy thì Nhả clone đó, đăng nhập tay dọn, rồi Nhận lại.",
            });
            nhomRac.Controls.Add(_lstRac);
            nhomRac.Controls.Add(nutRac);
            bang.Controls.Add(nhomRac, 0, 1);

            var nhomSc = new GroupBox { Text = "Sức chứa theo kệ", Dock = DockStyle.Fill };
            _lblSucChuaKe = new Label { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };
            nhomSc.Controls.Add(_lblSucChuaKe);
            bang.Controls.Add(nhomSc, 1, 1);
        }

        private bool LaLeaderHoacDuPhong(string u)
        {
            return string.Equals(u, _cfg.Leader, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(u, _cfg.LeaderDuPhong, StringComparison.OrdinalIgnoreCase);
        }

        private void NapBangKe()
        {
            if (_gridKeAcc == null) return;
            _dangNapCaiDat = true;
            try
            {
                _gridKeAcc.Rows.Clear();
                var da = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in Accounts)
                {
                    string u = a.Username;
                    if (string.IsNullOrEmpty(u) || !da.Add(u) || LaLeaderHoacDuPhong(u)) continue;
                    int i = _gridKeAcc.Rows.Add(NameMask.Apply(u), NameMask.Apply(TenNvDaBiet(u) ?? a.CharName ?? ""),
                        KeHang.TenHienThi(KeHang.KeCuaAcc(u, _cfg)), ChuSucChua(u));
                    _gridKeAcc.Rows[i].Tag = u;
                }
            }
            finally { _dangNapCaiDat = false; }
            CapNhatSucChuaKe();
        }

        private string ChuSucChua(string u)
        {
            var t = _dp.So.Lay(u);
            if (t == null) return "chưa có số liệu";
            int dung = t.TuiDung + t.RuongDung;
            int tong = t.SoOTui + Math.Max(0, t.SoORuong);
            return dung + "/" + tong + (t.SoORuong < 0 ? " (chưa đọc rương)" : "") + (_cfg.DaNha(u) ? " · ĐÃ NHẢ" : "");
        }

        private void CapNhatSucChuaKe()
        {
            if (_lblSucChuaKe == null) return;
            var dung = new Dictionary<string, int>();
            var tong = new Dictionary<string, int>();
            var soAcc = new Dictionary<string, int>();
            foreach (var k in KeHang.TatCa) { dung[k] = 0; tong[k] = 0; soAcc[k] = 0; }
            var da = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in Accounts)
            {
                string u = a.Username;
                if (string.IsNullOrEmpty(u) || !da.Add(u) || LaLeaderHoacDuPhong(u) || _cfg.DaNha(u)) continue;
                string k = KeHang.KeCuaAcc(u, _cfg);
                if (!tong.ContainsKey(k)) { dung[k] = 0; tong[k] = 0; soAcc[k] = 0; }
                soAcc[k]++;
                var t = _dp.So.Lay(u);
                if (t == null) continue;
                dung[k] += t.TuiDung + t.RuongDung;
                tong[k] += t.SoOTui + Math.Max(0, t.SoORuong);
            }
            var sb = new System.Text.StringBuilder();
            foreach (var k in tong.Keys)
                sb.AppendLine(string.Format("{0,-18} {1,2} clone   {2,5} / {3,-5} ô", KeHang.TenHienThi(k), soAcc[k], dung[k], tong[k]));
            sb.AppendLine();
            sb.AppendLine("Không tính clone đang nhả, Leader và dự phòng.");
            string s = sb.ToString();
            if (_lblSucChuaKe.Text != s) _lblSucChuaKe.Text = s;
            if (_tabs.SelectedTab == _tabCaiDat && _tabsCaiDat.SelectedTab == _tcKe && !_gridKeAcc.IsCurrentCellInEditMode)
                foreach (DataGridViewRow r in _gridKeAcc.Rows)
                {
                    string u = r.Tag as string;
                    if (u == null) continue;
                    string moi = ChuSucChua(u);
                    if (!Equals(r.Cells[3].Value, moi)) r.Cells[3].Value = moi;
                }
        }

        private void NapBangKeMon()
        {
            if (_gridKeMon == null) return;
            _gridKeMon.Rows.Clear();
            foreach (var kv in _cfg.ChupKeCuaMon().OrderBy(x => x.Key))
            {
                int i = _gridKeMon.Rows.Add(kv.Key.ToString(), BangMon.Ten(kv.Key), KeHang.TenHienThi(kv.Value));
                _gridKeMon.Rows[i].Tag = kv.Key;
            }
        }

        private void NapDanhSachRac()
        {
            if (_lstRac == null) return;
            _lstRac.Items.Clear();
            foreach (var t in _cfg.DanhSachRac.OrderBy(x => x)) _lstRac.Items.Add(t + " " + BangMon.Ten(t));
        }

        // ==================== Gói rút ====================

        private void DungTrangGoi(TabPage tab)
        {
            var trai = new Panel { Dock = DockStyle.Left, Width = 220, Padding = new Padding(6) };
            _lstGoi = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _lstGoi.SelectedIndexChanged += (s, e) => NapGoiDangChon();
            trai.Controls.Add(_lstGoi);
            trai.Controls.Add(new Label { Text = "Các gói", Dock = DockStyle.Top, Height = 20 });
            tab.Controls.Add(trai);

            var h = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            tab.Controls.Add(h);
            h.BringToFront();
            int y = 6;
            h.Controls.Add(new Label { Text = "Tên gói", Left = 8, Top = y + 3, Width = 80 });
            _txtTenGoi = new TextBox { Left = 96, Top = y, Width = 200 };
            h.Controls.Add(_txtTenGoi);
            GhiChu(h, 304, y, "một từ, không dấu cách (Chủ kho nhắn: lay goi <tên>)");
            y += 30;
            h.Controls.Add(new Label { Text = "Món", Left = 8, Top = y + 3, Width = 80 });
            _txtDongGoi = new TextBox
            {
                Left = 96, Top = y, Width = 360, Height = 220, Multiline = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.5f),
            };
            h.Controls.Add(_txtDongGoi);
            h.Controls.Add(new Label
            {
                Left = 466, Top = y, Width = 380, Height = 120, ForeColor = Color.DimGray,
                Text = "Mỗi dòng một món:  <id> <số lượng>\nSố lượng = het để lấy hết.\nPhần sau dấu # là ghi chú.\n\nVí dụ:\n457 10   # Đá cấp 5\n458 het",
            });
            y += 228;
            NutNho(h, "Lưu gói", 96, y, 90, BtnLuuGoi);
            NutNho(h, "Xoá gói", 192, y, 90, (s, e) =>
            {
                string ten = (_txtTenGoi.Text ?? "").Trim();
                if (ten.Length == 0 || _cfg.LayGoi(ten) == null) return;
                if (MessageBox.Show(this, "Xoá gói " + ten + "?", "Gói rút", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                _cfg.DatGoi(ten, null);
                HenLuuCfg();
                NapDanhSachGoi();
            });
            NutNho(h, "Mới", 288, y, 70, (s, e) => { _lstGoi.ClearSelected(); _txtTenGoi.Text = ""; _txtDongGoi.Text = ""; });
            NutNho(h, "Rút gói này…", 364, y, 100, (s, e) =>
            {
                string ten = (_txtTenGoi.Text ?? "").Trim();
                if (_cfg.LayGoi(ten) == null) { MessageBox.Show(this, "Lưu gói trước đã.", "Rút gói"); return; }
                RutGoi(ten, null);
            });
        }

        private void NapDanhSachGoi()
        {
            if (_lstGoi == null) return;
            var dang = _lstGoi.SelectedItem as string;
            _lstGoi.Items.Clear();
            foreach (var g in _cfg.TenGoi) _lstGoi.Items.Add(g);
            if (dang != null && _lstGoi.Items.Contains(dang)) _lstGoi.SelectedItem = dang;
        }

        private void NapGoiDangChon()
        {
            var ten = _lstGoi.SelectedItem as string;
            if (ten == null) return;
            var g = _cfg.LayGoi(ten);
            if (g == null) return;
            _txtTenGoi.Text = ten;
            _txtDongGoi.Lines = g.Select(d => d.Tpl + " " + (d.SoLuong < 0 ? "het" : d.SoLuong.ToString()) + "   # " + BangMon.Ten(d.Tpl)).ToArray();
        }

        private void BtnLuuGoi(object sender, EventArgs e)
        {
            string ten = (_txtTenGoi.Text ?? "").Trim();
            if (ten.Length == 0 || ten.Contains(" ") || ten.Contains("=") || ten.Contains(";"))
            {
                MessageBox.Show(this, "Tên gói phải là một từ (không dấu cách, không = ;).", "Gói rút");
                return;
            }
            var ds = new List<DongGoi>();
            int soDong = 0;
            foreach (var raw in _txtDongGoi.Lines)
            {
                soDong++;
                string s = raw;
                int thang = s.IndexOf('#');
                if (thang >= 0) s = s.Substring(0, thang);
                s = s.Trim();
                if (s.Length == 0) continue;
                var p = s.Split(new[] { ' ', '\t', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
                short tpl;
                int sl;
                if (p.Length != 2 || !short.TryParse(p[0], out tpl) || tpl < 0)
                {
                    MessageBox.Show(this, "Dòng " + soDong + " sai: \"" + raw + "\"\nCách viết: <id> <số lượng|het>", "Gói rút");
                    return;
                }
                if (ChuVan.ChuanHoa(p[1]) == "het") sl = -1;
                else if (!int.TryParse(p[1], out sl) || sl <= 0)
                {
                    MessageBox.Show(this, "Dòng " + soDong + ": số lượng phải > 0 hoặc het.", "Gói rút");
                    return;
                }
                ds.Add(new DongGoi { Tpl = tpl, SoLuong = sl });
            }
            if (ds.Count == 0) { MessageBox.Show(this, "Gói chưa có món nào.", "Gói rút"); return; }
            _cfg.DatGoi(ten, ds);
            HenLuuCfg();
            NapDanhSachGoi();
            _lstGoi.SelectedItem = ten;
            XepLog("Đã lưu gói " + ten + " (" + ds.Count + " món).", false);
        }

        // ==================== Theo dõi ====================

        private void DungTrangTheoDoi(TabPage tab)
        {
            var tren = new Panel { Dock = DockStyle.Top, Height = 36 };
            NutNho(tren, "Thêm…", 6, 6, 80, (s, e) =>
            {
                var id = HopNhap.HoiSo(this, "Theo dõi món", "ID món (template):", 0, 0, 32000);
                if (id != null) ThemTheoDoi((short)id.Value);
            });
            NutNho(tren, "Xoá", 92, 6, 80, (s, e) =>
            {
                var r = _gridTheoDoi.CurrentRow;
                var m = r != null ? r.Tag as MucTheoDoi : null;
                if (m == null) return;
                _cfg.BoTheoDoi(m.Tpl, m.ChuKho);
                HenLuuCfg();
                NapBangTheoDoi();
            });
            tren.Controls.Add(new Label { Text = "Giờ gửi báo cáo ngày (HH:mm, trống = tắt):", Left = 200, Top = 10, Width = 250 });
            _txtGioBaoCao = new TextBox { Left = 452, Top = 7, Width = 60 };
            _txtGioBaoCao.TextChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                string v = (_txtGioBaoCao.Text ?? "").Trim();
                _cfg.BaoCaoGio = v;
                int g, p;
                bool ok = v.Length == 0 || _cfg.LayGioBaoCao(out g, out p);
                _txtGioBaoCao.BackColor = ok ? SystemColors.Window : Color.MistyRose;
                if (ok) HenLuuCfg();
            };
            tren.Controls.Add(_txtGioBaoCao);
            GhiChu(tren, 520, 7, "Leader nhắn riêng mọi Chủ kho: nhập / xuất trong ngày, sức chứa, xu.");

            _gridTheoDoi = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = SystemColors.Window,
            };
            _gridTheoDoi.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", Width = 60 });
            _gridTheoDoi.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên", Width = 200 });
            _gridTheoDoi.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ngưỡng", Width = 160 });
            _gridTheoDoi.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Báo cho Chủ kho", Width = 160 });
            tab.Controls.Add(_gridTheoDoi);
            tab.Controls.Add(tren);
            tab.Controls.Add(new Label
            {
                Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray,
                Text = "  Có ngưỡng N: báo khi khả dụng xuống dưới N và khi lên lại ≥ N. Không ngưỡng: báo mỗi khi món về kho. Tối đa 1 tin / món / 10 phút.",
            });
        }

        private void NapBangTheoDoi()
        {
            if (_gridTheoDoi == null) return;
            _gridTheoDoi.Rows.Clear();
            foreach (var m in _cfg.TheoDoi.OrderBy(x => x.Tpl))
            {
                int i = _gridTheoDoi.Rows.Add(m.Tpl.ToString(), BangMon.Ten(m.Tpl),
                    m.Nguong >= 0 ? "báo khi dưới " + m.Nguong : "báo mỗi lần về", NameMask.Apply(m.ChuKho ?? ""));
                _gridTheoDoi.Rows[i].Tag = m;
            }
        }

        // ==================== Chat & rao ====================

        private void DungTrangChat(TabPage tab)
        {
            var h = TrangCuon(tab);
            int y = 4;
            y = TieuDe(h, "Leader tự rao ở chat khu (D19, D27, D31)", y);
            _chkRao = OTick(h, ref y, "Bật rao", v => _cfg.RaoBat = v);
            h.Controls.Add(new Label { Text = "Mẫu câu rao", Left = X_NHAN, Top = y + 3, Width = X_O - 16 });
            _txtRaoMau = new TextBox { Left = X_O, Top = y, Width = 360 };
            _txtRaoMau.TextChanged += (s, e) =>
            {
                if (_dangNapCaiDat) return;
                _cfg.RaoMau = _txtRaoMau.Text;
                HenLuuCfg();
                CapNhatXemRao();
            };
            h.Controls.Add(_txtRaoMau);
            y += 26;
            GhiChu(h, X_O, y, "Biến: {dung} {tong} {trong} {online} {tongacc} {lenh} {leader}. Viết có dấu cũng được — gửi đi tự bỏ dấu.", 620);
            y += 22;
            h.Controls.Add(new Label { Text = "Xem trước", Left = X_NHAN, Top = y + 3, Width = X_O - 16 });
            _lblRaoXem = new Label { Left = X_O, Top = y + 3, Width = 600, ForeColor = Color.DarkGreen, Font = new Font("Consolas", 9f) };
            h.Controls.Add(_lblRaoXem);
            y += 28;

            h.Controls.Add(new Label { Text = "Nhịp rao", Left = X_NHAN, Top = y + 3, Width = X_O - 16 });
            var nhomRao = new Panel { Left = X_O, Top = y, Width = 360, Height = 24 };
            _rdRaoThongMinh = new RadioButton { Text = "Thông minh", Left = 0, Top = 2, Width = 110 };
            _rdRaoDeu = new RadioButton { Text = "Đều", Left = 120, Top = 2, Width = 80 };
            EventHandler doiRao = (s, e) =>
            {
                if (_dangNapCaiDat) return;
                _cfg.RaoCheDo = _rdRaoDeu.Checked ? CheDoRao.Deu : CheDoRao.ThongMinh;
                HenLuuCfg();
            };
            _rdRaoThongMinh.CheckedChanged += doiRao;
            _rdRaoDeu.CheckedChanged += doiRao;
            nhomRao.Controls.Add(_rdRaoThongMinh);
            nhomRao.Controls.Add(_rdRaoDeu);
            h.Controls.Add(nhomRao);
            y += 26;
            GhiChu(h, X_O, y, "Thông minh: rao mỗi \"nhịp\" khi khu có người ngoài hoặc kho vừa đổi, còn lại mỗi \"nhịp khi vắng\".", 640);
            y += 24;
            _numRaoNhip = ONhapSo(h, ref y, "Nhịp (giây)", 3, 3600, "cũng là khoảng cách tối thiểu giữa hai tin chat khu", v => _cfg.RaoNhipGiay = v);
            _numRaoVang = ONhapSo(h, ref y, "Nhịp khi khu vắng (giây)", 5, 86400, "chỉ dùng cho chế độ Thông minh", v => _cfg.RaoVangGiay = v);
            y += 8;

            y = TieuDe(h, "Báo sự kiện (D20, D24)", y);
            _chkBaoCd = OTick(h, ref y, "Báo sự kiện ra chat khu", v => _cfg.BaoCongDong = v, "nạp xong, lệnh rút, KHO ĐẦY, đổi Leader…");
            _chkNguoiLa = OTick(h, ref y, "Cho phép nhắn riêng người ngoài danh sách Chủ kho", v => _cfg.ChatVoiNguoiLa = v,
                "tắt (mặc định): chỉ Chủ kho nhận tin riêng");
            y += 8;
            GhiChu(h, X_NHAN, y, "Mọi tin gửi đi đều có tem @NNN ở đầu (D18) và không dấu (D46). Chat riêng tối đa 1 tin / 3 giây mỗi bot.", 760);
        }

        private void CapNhatXemRao()
        {
            if (_lblRaoXem == null) return;
            string s = _dp.NoiDungRao();
            string xem = s == null ? "(trống — không rao)" : "@123 " + ChuVan.BoDau(s);
            if (_lblRaoXem.Text != xem) _lblRaoXem.Text = xem;
        }

        // ==================== Log ====================

        private void DungTrangLog(TabPage tab)
        {
            var h = TrangCuon(tab);
            int y = 4;
            y = TieuDe(h, "Log đầy đủ theo ngày (D26, SPEC §8.1)", y);
            _chkLogFile = new CheckBox { Text = "Ghi log (app / giao dịch / chat / lệnh / hex)", Left = X_NHAN, Top = y, Width = 360 };
            _chkLogFile.CheckedChanged += (s, e) =>
            {
                if (_dangNapCaiDat || _chkLogFile.Checked == _cfg.LogFile) return;
                DatGhiLog(_chkLogFile.Checked);
            };
            h.Controls.Add(_chkLogFile);
            GhiChu(h, 376, y, "cùng công tắc với nút \"Ghi log\" trên thanh nút");
            y += 26;
            _numGiuLog = ONhapSo(h, ref y, "Giữ log bao nhiêu ngày", 0, 3650, "0 = giữ hết. Xoá thư mục cũ lúc MỞ app.", v => _cfg.GiuLogNgay = v);
            _chkLogHex = OTick(h, ref y, "Ghi hex gói giao dịch / rương / tin server", v => _cfg.LogHexGiaoDich = v, "hex.log — để soi lỗi giao dịch");
            y += 10;
            h.Controls.Add(new Label { Text = "Thư mục:", Left = X_NHAN, Top = y + 3, Width = 70 });
            h.Controls.Add(new TextBox { Left = 84, Top = y, Width = 560, ReadOnly = true, Text = NhatKy.ThuMucGoc });
            NutNho(h, "Mở", 650, y - 1, 60, (s, e) => MoThuMuc(NhatKy.ThuMucGoc));
            y += 34;
            GhiChu(h, X_NHAN, y, "Mỗi ngày một thư mục yyyy-MM-dd; file quá 50 MB thì sang phần app_2.log… — không bao giờ ghi đè. Không ghi mật khẩu.", 760);
        }
    }
}
