using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NSOKHODO.Client;
using NSOKHODO.Config;
using NSOKHODO.Fleet;
using NSOKHODO.Kho;
using NSOKHODO.Logging;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Cửa sổ duy nhất của NSOKHODO — <b>bố cục A: tab theo việc</b> (SPEC §10, D51, user duyệt
    /// 2026-09-16). Muốn thêm/bớt tab hay dời khung thì phải trình user trước.
    ///
    /// <code>
    /// [Thêm][H.loạt][Sửa][Xoá] | [▶Chạy][■Dừng][↻Vào lại] | [Nhả clone][Nhận lại] | [Proxy…][Chia proxy…][Danh sách…] | [Ghi log][Ẩn tên]
    /// ┌Tổng kho┬Acc┬Hàng chờ(2)┬Nhật ký┬Cài đặt┐
    /// ├── Log (thu gọn được) ──┤
    /// └ Kho 70/360 │ Xu │ Chờ │ Leader │ online ┘
    /// </code>
    ///
    /// <para>File này: khung, thanh nút, log, thanh trạng thái, thao tác acc. Từng tab nằm ở các file
    /// <c>MainForm.*.cs</c> cùng thư mục.</para>
    ///
    /// <para><b>Log:</b> log của client (qua fleet) ghi vào <c>app.log</c> ngay khi xếp hàng; log của bộ
    /// điều phối đã tự ghi file nên chỉ lên màn hình. Công tắc "Ghi log" = cài đặt <c>LogFile</c>
    /// (mặc định BẬT, D26) — khác NSOBAOTATL mặc định tắt.</para>
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly FleetManager _fleet = new FleetManager();
        private AppConfig _config;
        private KhoConfig _cfg;
        private KhoDieuPhoi _dp;

        private TabControl _tabs;
        private TabPage _tabTong, _tabAcc, _tabHang, _tabNhatKy, _tabCaiDat;
        private TextBox _log;
        private SplitContainer _chiaLog;
        private int _caoLog = 170;
        private bool _logThuGon;
        private Button _nutThuLog;
        private ToolStripStatusLabel _status;
        private ToolStripButton _nutGhiLog;
        private readonly Timer _tick = new Timer();
        private readonly Timer _tickLog = new Timer();
        private DateTime _luuThongKeLuc = DateTime.MinValue;

        private readonly ConcurrentQueue<string> _hangLog = new ConcurrentQueue<string>();
        private readonly bool _tuChay;

        /// <summary>Bảng account → client, làm mới mỗi giây (xem NSOBAOTATL: đừng gọi Find() từng ô).</summary>
        private Dictionary<AccountConfig, NsoClient> _anhChup = new Dictionary<AccountConfig, NsoClient>();

        private List<AccountConfig> Accounts { get { return _config.Accounts; } }

        private NsoClient ClientCua(AccountConfig a)
        {
            NsoClient c;
            return a != null && _anhChup.TryGetValue(a, out c) ? c : null;
        }

        public MainForm() : this(false) { }

        /// <param name="tuChay"><c>--chay</c>: mở mọi acc (trừ clone đang nhả) ngay khi cửa sổ hiện.</param>
        public MainForm(bool tuChay)
        {
            _tuChay = tuChay;
            Text = "NSOKHODO — kho đồ chung · " + AppPaths.ListName;
            Width = 1380;
            Height = 840;
            StartPosition = FormStartPosition.CenterScreen;
            AppIcon.Apply(this);

            _config = ConfigManager.Load();
            if (ConfigManager.LoadError != null)
                MessageBox.Show(this, ConfigManager.LoadError, "Lỗi đọc danh sách", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            DisconnectStats.Nap();

            _cfg = KhoConfig.Nap();
            NhatKy.Bat = _cfg.LogFile;
            Logger.Enabled = _cfg.LogFile;
            FileLog.SetEnabled(false);   // log cũ một-file của lõi: thay bằng NhatKy (SPEC §8.1)
            NhatKy.KhoiDong(_cfg.GiuLogNgay);
            NhatKy.App("-", "App", "Mo NSOKHODO, danh sach " + AppPaths.ListName + ", " + Accounts.Count + " acc");

            _dp = new KhoDieuPhoi(_fleet, () => _config.Accounts, _cfg);
            _dp.OnLog += s => XepLog(s, false);
            _dp.BatDau();

            BuildUi();

            // CHỈ một đường: FleetManager đã nối OnLog của từng client vào OnLog của fleet.
            _fleet.OnLog += s => XepLog(s, true);

            _tick.Interval = 1000;
            _tick.Tick += (s, e) =>
            {
                LamMoi();
                // Thống kê mất kết nối: ghi file (có mutex chung, chờ tới 3 giây) tối đa 15 giây một lần -
                // lúc cả loạt acc rớt / vào lại nó bẩn mỗi giây và làm khựng giao diện.
                if ((DateTime.UtcNow - _luuThongKeLuc).TotalSeconds >= 15)
                {
                    _luuThongKeLuc = DateTime.UtcNow;
                    DisconnectStats.LuuNeuBan();
                }
                LuuNeuHen();
                LuuCfgNeuHen();
            };
            _tick.Start();
            // Log xả nhịp riêng 300 ms: từng ít dòng một, không dồn cục mỗi giây.
            _tickLog.Interval = 300;
            _tickLog.Tick += (s, e) => RutHangLog();
            _tickLog.Start();

            FormClosing += (s, e) =>
            {
                _tick.Stop();
                _tickLog.Stop();
                DisconnectStats.LuuNeuBan();
                try { _fleet.StopAll(); } catch { }
                try { _dp.Dispose(); } catch { }
                LuuNgay();
                LuuCfgNgay();
                NhatKy.App("-", "App", "Dong NSOKHODO");
                NhatKy.XaNgay();
                FileLog.Close();
            };
        }

        // ==================== DỰNG GIAO DIỆN ====================

        private void BuildUi()
        {
            var thanhNut = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4) };
            ThemNut(thanhNut, "Thêm", BtnThem);
            ThemNut(thanhNut, "Thêm hàng loạt", BtnThemHangLoat);
            ThemNut(thanhNut, "Sửa", BtnSua);
            ThemNut(thanhNut, "Xoá", BtnXoa);
            thanhNut.Items.Add(new ToolStripSeparator());
            ThemNut(thanhNut, "▶ Chạy", BtnChay);
            ThemNut(thanhNut, "■ Dừng", BtnDung);
            ThemNut(thanhNut, "↻ Vào lại", BtnVaoLai);
            thanhNut.Items.Add(new ToolStripSeparator());
            ThemNut(thanhNut, "Nhả clone", BtnNhaClone);
            ThemNut(thanhNut, "Nhận lại", BtnNhanLai);
            thanhNut.Items.Add(new ToolStripSeparator());
            ThemNut(thanhNut, "Proxy…", BtnProxy);
            ThemNut(thanhNut, "Chia proxy…", BtnChiaProxy);
            ThemNut(thanhNut, "Danh sách…", BtnDanhSach);
            thanhNut.Items.Add(new ToolStripSeparator());

            _nutGhiLog = new ToolStripButton("Ghi log") { CheckOnClick = true, Checked = _cfg.LogFile };
            _nutGhiLog.CheckedChanged += (s, e) => { if (_nutGhiLog.Checked != _cfg.LogFile) DatGhiLog(_nutGhiLog.Checked); };
            thanhNut.Items.Add(_nutGhiLog);

            var nutAnTen = new ToolStripButton("Ẩn tên") { CheckOnClick = true, Checked = false };
            nutAnTen.CheckedChanged += (s, e) => { NameMask.Enabled = nutAnTen.Checked; LamMoi(); };
            thanhNut.Items.Add(nutAnTen);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabTong = new TabPage("Tổng kho");
            _tabAcc = new TabPage("Acc");
            _tabHang = new TabPage("Hàng chờ");
            _tabNhatKy = new TabPage("Nhật ký");
            _tabCaiDat = new TabPage("Cài đặt");
            _tabs.TabPages.AddRange(new[] { _tabTong, _tabAcc, _tabHang, _tabNhatKy, _tabCaiDat });
            _tabs.SelectedIndexChanged += (s, e) => LamMoi();

            DungTabTongKho(_tabTong);
            DungTabAcc(_tabAcc);
            DungTabHangCho(_tabHang);
            DungTabNhatKy(_tabNhatKy);
            DungTabCaiDat(_tabCaiDat);

            // ---- khung log (thu gọn được) ----
            var khungLog = new Panel { Dock = DockStyle.Fill };
            var dauLog = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.FromArgb(40, 44, 52) };
            dauLog.Controls.Add(new Label
            {
                Text = "Log", Left = 6, Top = 4, Width = 60, ForeColor = Color.Gainsboro,
                Font = new Font(Font, FontStyle.Bold),
            });
            _nutThuLog = new Button
            {
                Text = "Thu gọn", Width = 80, Height = 20, Top = 2, FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Gainsboro, Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            _nutThuLog.Click += (s, e) => ThuGonLog(!_logThuGon);
            dauLog.Controls.Add(_nutThuLog);
            dauLog.Resize += (s, e) => _nutThuLog.Left = dauLog.Width - _nutThuLog.Width - 6;
            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(28, 30, 34),
                ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 8.5f),
            };
            khungLog.Controls.Add(_log);
            khungLog.Controls.Add(dauLog);

            // ⚠️ SplitContainer: KHÔNG đặt SplitterDistance / Panel*MinSize trong constructor (NSOBAOTATL
            // đã chết vì đúng lỗi này) — chuyển hết xuống Shown.
            _chiaLog = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _chiaLog.Panel1.Controls.Add(_tabs);
            _chiaLog.Panel2.Controls.Add(khungLog);

            var thanhTrangThai = new StatusStrip();
            _status = new ToolStripStatusLabel("") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            thanhTrangThai.Items.Add(_status);

            Controls.Add(_chiaLog);
            Controls.Add(thanhNut);
            Controls.Add(thanhTrangThai);

            Shown += (s, e) =>
            {
                try
                {
                    _chiaLog.SplitterDistance = Math.Max(200, _chiaLog.Height - _caoLog);
                    _chiaLog.Panel2MinSize = 24;
                }
                catch { }
                DatKichThuocTongKho();
                if (_tuChay) ChayTatCa("--chay");
                LamMoi();
            };
        }

        private static void ThemNut(ToolStrip strip, string text, EventHandler onClick)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            b.Click += onClick;
            strip.Items.Add(b);
        }

        private void ThuGonLog(bool thu)
        {
            try
            {
                if (thu)
                {
                    _caoLog = Math.Max(80, _chiaLog.Height - _chiaLog.SplitterDistance);
                    _chiaLog.SplitterDistance = Math.Max(200, _chiaLog.Height - 24 - _chiaLog.SplitterWidth);
                }
                else _chiaLog.SplitterDistance = Math.Max(200, _chiaLog.Height - _caoLog);
                _logThuGon = thu;
                _nutThuLog.Text = thu ? "Mở log" : "Thu gọn";
                if (!thu && _logCanVeLai) VeLaiLog();
            }
            catch { }
        }

        private void DatGhiLog(bool bat)
        {
            _cfg.LogFile = bat;
            NhatKy.Bat = bat;
            Logger.Enabled = bat;
            HenLuuCfg();
            if (_chkLogFile != null && _chkLogFile.Checked != bat) _chkLogFile.Checked = bat;
            if (_nutGhiLog.Checked != bat) _nutGhiLog.Checked = bat;
            XepLog(bat ? "Đã bật ghi log → " + NhatKy.ThuMucGoc : "Đã TẮT ghi log (không còn nhật ký theo ngày).", false);
        }

        // ==================== LÀM MỚI MỖI GIÂY ====================

        private void LamMoi()
        {
            _anhChup = _fleet.SnapshotByAccount();
            CapNhatThanhTrangThai();
            int cho = _dp.Hang.SoCho;
            string tenHang = cho > 0 ? "Hàng chờ (" + cho + ")" : "Hàng chờ";
            if (_tabHang.Text != tenHang) _tabHang.Text = tenHang;

            // Thu nhỏ cửa sổ = không vẽ gì (tool chạy 24/7 trên VPS).
            if (WindowState == FormWindowState.Minimized) return;
            var tab = _tabs.SelectedTab;
            if (tab == _tabTong) LamMoiTongKho();
            else if (tab == _tabAcc) LamMoiAcc();
            else if (tab == _tabHang) LamMoiHangCho();
            else if (tab == _tabCaiDat) LamMoiCaiDat();
        }

        private void CapNhatThanhTrangThai()
        {
            var sc = _dp.SucChuaKho;
            var rac = _dp.SucChuaRac;
            int online = 0;
            foreach (var kv in _anhChup) if (kv.Value.IsOnline) online++;
            string leader = _dp.TenLeader;
            string dong = string.Format(
                "Kho {0}{1}/{2}{3}   │   Xu {4}   │   Chờ {5}   │   Leader: {6}   │   {7}/{8} online   │   mất kết nối {9} lần{10}",
                sc.ChuaDu ? "~" : "", sc.Dung, sc.Tong,
                rac.Tong > 0 ? " (Rác " + rac.Dung + "/" + rac.Tong + ")" : "",
                KhoDieuPhoi.XuGon(_dp.TongXu), _dp.Hang.SoCho,
                string.IsNullOrEmpty(leader) ? "(chưa cài)" : NameMask.Apply(leader),
                online, Accounts.Count, DisconnectStats.TongTatCa(),
                (_dp.KhoDay ? "   │   KHO ĐẦY" : "")
                + (_cfg.KhuChinh < 0 ? "   │   CHƯA CÀI KHU CHÍNH (Cài đặt → Kho)" : ""));
            if (dong != _status.Text) _status.Text = dong;
        }

        // ==================== NÚT ====================

        private void BtnThem(object sender, EventArgs e)
        {
            using (var f = new AddAccountForm())
                if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
                {
                    Accounts.Add(f.Result);
                    LuuAccounts();
                    LamMoiSoDongAcc();
                    NapDanhSachAccCaiDat();
                }
        }

        private void BtnThemHangLoat(object sender, EventArgs e)
        {
            using (var f = new BulkAddForm())
                if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
                {
                    Accounts.AddRange(f.Result);
                    LuuAccounts();
                    LamMoiSoDongAcc();
                    NapDanhSachAccCaiDat();
                    XepLog("Đã thêm " + f.Result.Count + " tài khoản.", false);
                }
        }

        private void BtnSua(object sender, EventArgs e)
        {
            var rows = DongAccDangChon();
            if (rows.Count != 1) { NhacChonAcc("Chọn đúng MỘT dòng ở tab Acc để sửa."); return; }
            var a = Accounts[rows[0]];
            using (var f = new AddAccountForm(a))
                if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
                {
                    Accounts[rows[0]] = f.Result;
                    LuuAccounts();
                    VeLaiAcc();
                    NapDanhSachAccCaiDat();
                }
        }

        private void BtnXoa(object sender, EventArgs e)
        {
            var rows = DongAccDangChon();
            if (rows.Count == 0) { NhacChonAcc(null); return; }
            if (MessageBox.Show(this, "Xoá " + rows.Count + " tài khoản khỏi danh sách?\n\n"
                    + "Đồ trên các acc đó sẽ không còn tính vào kho.",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                _fleet.StopAccount(Accounts[rows[i]]);
                Accounts.RemoveAt(rows[i]);
            }
            LuuAccounts();
            LamMoiSoDongAcc();
            NapDanhSachAccCaiDat();
        }

        private void BtnChay(object sender, EventArgs e)
        {
            var sel = AccDangChon();
            if (sel.Count == 0)
            {
                if (MessageBox.Show(this, "Chưa chọn dòng nào ở tab Acc. Chạy TẤT CẢ tài khoản (trừ clone đang nhả)?",
                        "Chạy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ChayTatCa("nut Chay");
                return;
            }
            ChayDs(sel);
        }

        private void ChayTatCa(string nguon)
        {
            ChayDs(new List<AccountConfig>(Accounts));
            NhatKy.App("-", "App", "Chay tat ca (" + nguon + ")");
        }

        /// <summary>Mở so le; BỎ QUA clone đang nhả (user đang đăng nhập tay — hai bên sẽ đá nhau).</summary>
        private void ChayDs(List<AccountConfig> ds)
        {
            var chay = ds.Where(a => !_cfg.DaNha(a.Username)).ToList();
            int bo = ds.Count - chay.Count;
            if (chay.Count > 0) _fleet.StartStaggered(chay, 2000, StartOptions.EffectiveBatch);
            XepLog("Đang mở " + chay.Count + " tài khoản (" + StartOptions.EffectiveBatch + " acc mỗi 2 giây)"
                   + (bo > 0 ? " — bỏ qua " + bo + " clone đang nhả (bấm Nhận lại để chạy)" : "") + ".", false);
        }

        private void BtnDung(object sender, EventArgs e)
        {
            var sel = AccDangChon();
            if (sel.Count == 0) { NhacChonAcc(null); return; }
            _fleet.StopStagger();
            foreach (var a in sel) _fleet.StopAccount(a);
            XepLog("Đã dừng " + sel.Count + " tài khoản.", false);
        }

        private void BtnVaoLai(object sender, EventArgs e)
        {
            var sel = AccDangChon().Where(a => !_cfg.DaNha(a.Username)).ToList();
            if (sel.Count == 0) { NhacChonAcc("Chọn acc (không phải clone đang nhả) ở tab Acc."); return; }
            foreach (var a in sel)
            {
                _fleet.StopAccount(a);
                _fleet.StartAccount(a);
            }
            XepLog("Vào lại " + sel.Count + " tài khoản.", false);
        }

        private void BtnNhaClone(object sender, EventArgs e)
        {
            var sel = AccDangChon();
            if (sel.Count == 0) { NhacChonAcc("Chọn clone muốn nhả ở tab Acc."); return; }
            if (MessageBox.Show(this,
                    "Nhả " + sel.Count + " clone?\n\n"
                    + "Bot sẽ đợi clone giao dịch / làm việc xong rồi ĐĂNG XUẤT nó. Hàng trên clone không rút được cho tới khi Nhận lại.\n"
                    + "Sau đó bạn đăng nhập tay bằng client thường để dọn.",
                    "Nhả clone", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            foreach (var a in sel)
            {
                string loi = _dp.NhaClone(a);
                if (loi != null) XepLog(loi, false);
            }
        }

        private void BtnNhanLai(object sender, EventArgs e)
        {
            var sel = AccDangChon().Where(a => _cfg.DaNha(a.Username)).ToList();
            if (sel.Count == 0) { NhacChonAcc("Chọn clone đang nhả (trạng thái ĐÃ NHẢ) ở tab Acc."); return; }
            if (MessageBox.Show(this,
                    "Bạn ĐÃ THOÁT client thường của " + sel.Count + " acc này chưa?\n\n"
                    + "Bot đăng nhập lại trong khi client thường còn trong game thì hai bên sẽ đá nhau.",
                    "Nhận lại clone", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            foreach (var a in sel)
            {
                string loi = _dp.NhanLaiClone(a);
                if (loi != null) XepLog(loi, false);
            }
        }

        private void BtnProxy(object sender, EventArgs e)
        {
            var sel = AccDangChon();
            if (sel.Count == 0) { NhacChonAcc(null); return; }
            bool http; string bare;
            AccountConfig.SplitProxy(sel[0].Proxy, out http, out bare);
            using (var f = new ProxyAssignForm(bare, http, sel.Count))
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    string luu = string.IsNullOrEmpty(f.ProxyText) ? "" : (f.IsHttp ? "http://" + f.ProxyText : f.ProxyText);
                    foreach (var a in sel) a.Proxy = luu;
                    LuuAccounts();
                    VeLaiAcc();
                }
        }

        private void BtnChiaProxy(object sender, EventArgs e)
        {
            var rows = DongAccDangChon();
            if (rows.Count == 0) { NhacChonAcc(null); return; }
            var sel = rows.Select(i => Accounts[i]).ToList();
            bool http; string bare;
            AccountConfig.SplitProxy(sel[0].Proxy, out http, out bare);
            using (var f = new ProxyBulkForm(sel, rows, http))
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    LuuAccounts();
                    VeLaiAcc();
                }
        }

        private void BtnDanhSach(object sender, EventArgs e)
        {
            using (var f = new AccountListForm(AppPaths.ListName))
            {
                if (f.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(f.SelectedName)) return;
                MessageBox.Show(this,
                    "Danh sách sẽ được mở ở lần khởi động tới.\n\n"
                    + "Lưu ý: một kho = một tiến trình = một danh sách (SPEC §11). Mỗi danh sách có cài đặt kho, "
                    + "sổ kho và thư mục log riêng.",
                    "Đổi danh sách", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SettingsStore.Set("LastAccountList", f.SelectedName);
            }
        }

        private void NhacChonAcc(string loi)
        {
            XepLog(loi ?? "Chưa chọn tài khoản nào — chọn dòng ở tab Acc.", false);
            if (_tabs.SelectedTab != _tabAcc) _tabs.SelectedTab = _tabAcc;
        }

        // ==================== LƯU ====================

        private bool _henLuu;
        private bool _henLuuCfg;

        /// <summary>Xin ghi accounts.txt — ghi thật ở nhịp 1 giây kế tiếp (gõ phím sinh hàng chục sự kiện).</summary>
        private void LuuAccounts() { _henLuu = true; }

        private void LuuNeuHen()
        {
            if (!_henLuu) return;
            _henLuu = false;
            LuuNgay();
        }

        private void LuuNgay()
        {
            try { ConfigManager.Save(_config); }
            catch (Exception ex) { XepLog("Lưu accounts.txt lỗi: " + ex.Message, false); }
        }

        private void HenLuuCfg() { if (!_dangNapCaiDat) _henLuuCfg = true; }

        private void LuuCfgNeuHen()
        {
            if (!_henLuuCfg) return;
            _henLuuCfg = false;
            LuuCfgNgay();
        }

        private void LuuCfgNgay()
        {
            string loi = _cfg.Luu();
            if (loi != null) XepLog("Lưu cài đặt kho lỗi: " + loi, false);
        }

        // ==================== LOG ====================

        /// <summary>Gọi được từ MỌI luồng — chỉ xếp hàng. <paramref name="ghiFile"/>: dòng chưa có trong app.log.</summary>
        private void XepLog(string s, bool ghiFile)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (ghiFile) NhatKy.AppTho(s);
            // Đóng giờ LÚC XẢY RA (trước đây đóng lúc hiện -> lệch tới 1 giây).
            if (_hangLog.Count < 20000) _hangLog.Enqueue(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
        }

        // Khung log: giữ tối đa LOG_GIU dòng gần nhất ở bộ nhớ; ô chữ chỉ nối thêm, quá LOG_TOI_DA mới dựng lại
        // MỘT lần (tắt vẽ trong lúc dựng). Trước đây mỗi giây tách toàn bộ chữ (_log.Lines) và gán lại -> giật.
        private readonly Queue<string> _dongLog = new Queue<string>();
        private int _soDongTrongO;
        private bool _logCanVeLai;
        private const int LOG_GIU = 300, LOG_TOI_DA = 600;

        private void RutHangLog()
        {
            if (_hangLog.IsEmpty) return;
            var sb = new System.Text.StringBuilder();
            string s;
            int n = 0;
            while (n < 1000 && _hangLog.TryDequeue(out s))
            {
                _dongLog.Enqueue(s);
                if (_dongLog.Count > LOG_GIU) _dongLog.Dequeue();
                sb.AppendLine(s);
                n++;
            }
            if (n == 0) return;
            if (_logThuGon || WindowState == FormWindowState.Minimized) { _logCanVeLai = true; return; }
            if (_logCanVeLai || _soDongTrongO + n > LOG_TOI_DA) VeLaiLog();
            else
            {
                _log.AppendText(sb.ToString());
                _soDongTrongO += n;
            }
        }

        private void VeLaiLog()
        {
            _logCanVeLai = false;
            SendMessage(_log.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                _log.Text = _dongLog.Count == 0 ? "" : string.Join(Environment.NewLine, _dongLog) + Environment.NewLine;
                _soDongTrongO = _dongLog.Count;
                _log.SelectionStart = _log.TextLength;
                _log.ScrollToCaret();
            }
            finally
            {
                SendMessage(_log.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _log.Invalidate();
            }
        }

        private const int WM_SETREDRAW = 0x000B;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
