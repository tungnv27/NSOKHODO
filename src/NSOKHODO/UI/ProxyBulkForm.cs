using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using NSOKHODO.Client;
using NSOKHODO.Config;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Hop thoai "Chia proxy": dan MOT DANH SACH proxy roi chia cho cac dong dang chon
    /// (vd 100 proxy cho 600 acc, 4-6 acc/proxy). Khac nut "Proxy" cu - cai do gan DUNG MOT proxy
    /// cho ca vung chon, va van giu nguyen.
    ///
    /// Luat chia + doc danh sach nam o <see cref="ProxyBulk"/> (dung chung voi ban Android).
    /// O day chi lo giao dien + bang XEM TRUOC: bang cap nhat theo tung thao tac, bam "Gán" la
    /// dung nhung gi dang thay - user khong phai doan.
    ///
    /// Hai thu user chot 2026-09-12:
    ///   - Hai kieu chia dung CHUNG mot o so; chon "Chia đều" thi o so tu dien ma KHONG bi khoa
    ///     (sua tay -> tu nhay ve kieu "N acc / 1 proxy").
    ///   - Chia khong het thi phai co TEXT CANH BAO + noi ro ACC NAO khong co proxy.
    /// </summary>
    public class ProxyBulkForm : Form
    {
        // ===== Dau vao =====
        private readonly List<AccountConfig> _sel;      // acc dang chon, theo dung thu tu dong tren luoi
        private readonly List<int> _rows;               // chi so dong tren luoi, cung thu tu voi _sel

        // ===== Ket qua tra ve cho MainForm =====

        /// <summary>Chi so DONG tren luoi cua nhung acc vua doi proxy (de ve lai dung may o do).</summary>
        public readonly List<int> ChangedRows = new List<int>();

        /// <summary>Chi so DONG tren luoi cua nhung acc khong duoc gan (cho nut "Chọn trên lưới").</summary>
        public readonly List<int> UnassignedRows = new List<int>();

        /// <summary>User bam "Chọn các acc này trên lưới" -> MainForm boi den lai vung chon.</summary>
        public bool SelectUnassigned { get; private set; }

        /// <summary>Cau log gop cho khung log cua MainForm.</summary>
        public string LogLine { get; private set; }

        // ===== Giao dien =====
        private TextBox _txtList;
        private Label _lblParsed, _lblEven, _lblMissing;
        private RadioButton _rbPerProxy, _rbEven;
        private NumericUpDown _numN;
        private ComboBox _cboType;
        private CheckBox _chkRepeat, _chkOnlyEmpty, _chkUnassignedOnly;
        private ListView _lv;
        private Label _lblStatus;
        private Button _btnOk, _btnCopyMissing, _btnSelectMissing;
        private Timer _debounce;

        // Chan vong lap su kien khi CHINH code dat lai gia tri o so / radio.
        private bool _suppress;

        // Ket qua tinh gan nhat (dung luc bam Gán - khong tinh lai de khong lech voi bang dang hien).
        private ProxyBulk.ParseResult _parsed;
        private ProxyBulk.Plan _plan;

        public ProxyBulkForm(List<AccountConfig> selected, List<int> rowIndexes, bool defaultIsHttp)
        {
            _sel = selected ?? new List<AccountConfig>();
            _rows = rowIndexes ?? new List<int>();

            AppIcon.Apply(this);
            Text = string.Format("Chia proxy cho {0} tài khoản đã chọn", _sel.Count);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(824, 604);

            BuildLeft(defaultIsHttp);
            BuildRight();
            BuildBottom();

            _debounce = new Timer { Interval = 220 };
            _debounce.Tick += (s, e) => { _debounce.Stop(); Recompute(); };

            Recompute();
        }

        // ==================== DUNG GIAO DIEN ====================

        private void BuildLeft(bool defaultIsHttp)
        {
            const int X = 12, W = 380;

            Controls.Add(new Label { Text = "Danh sách proxy (mỗi dòng 1 proxy)", Left = X, Top = 10, Width = W });

            _txtList = new TextBox
            {
                Left = X, Top = 30, Width = W, Height = 226,
                Multiline = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9f)
            };
            _txtList.TextChanged += (s, e) => { _debounce.Stop(); _debounce.Start(); };
            Controls.Add(_txtList);

            var btnPaste = new Button { Text = "Dán từ clipboard", Left = X, Top = 262, Width = 126, Height = 26 };
            btnPaste.Click += (s, e) =>
            {
                try
                {
                    if (Clipboard.ContainsText()) _txtList.Text = Clipboard.GetText();
                    else MessageBox.Show(this, "Clipboard không có chữ nào.", "Chia proxy",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Không đọc được clipboard: " + ex.Message, "Chia proxy",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            Controls.Add(btnPaste);

            var btnFile = new Button { Text = "Nạp từ file…", Left = X + 132, Top = 262, Width = 110, Height = 26 };
            btnFile.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog
                {
                    Title = "Chọn file danh sách proxy",
                    Filter = "File chữ (*.txt)|*.txt|Tất cả (*.*)|*.*"
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    try { _txtList.Text = System.IO.File.ReadAllText(dlg.FileName); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Không đọc được file: " + ex.Message, "Chia proxy",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            };
            Controls.Add(btnFile);

            _lblParsed = new Label { Left = X, Top = 294, Width = W, Height = 18, ForeColor = SystemColors.GrayText };
            Controls.Add(_lblParsed);

            // --- Cach chia ---
            var grp = new GroupBox { Text = "Cách chia", Left = X, Top = 316, Width = W, Height = 84 };

            _rbPerProxy = new RadioButton { Text = "N acc / 1 proxy", Left = 12, Top = 22, Width = 128, Checked = true };
            _numN = new NumericUpDown
            {
                Left = 146, Top = 20, Width = 62,
                Minimum = 1, Maximum = 10000,
                Value = ProxyBulk.DEFAULT_PER_PROXY
            };
            _rbEven = new RadioButton { Text = "Chia đều, tự tính", Left = 12, Top = 50, Width = 128 };
            _lblEven = new Label { Left = 146, Top = 52, Width = 222, ForeColor = SystemColors.GrayText };

            _rbPerProxy.CheckedChanged += (s, e) => { if (!_suppress && _rbPerProxy.Checked) Recompute(); };
            _rbEven.CheckedChanged += (s, e) => { if (!_suppress && _rbEven.Checked) Recompute(); };
            _numN.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                // Sua tay o so trong khi dang o kieu "Chia đều" -> chuyen han ve kieu N acc/proxy
                // (user chot: o so KHONG bi khoa). Doi radio se tu goi Recompute.
                if (_rbEven.Checked) _rbPerProxy.Checked = true;
                else Recompute();
            };

            grp.Controls.Add(_rbPerProxy);
            grp.Controls.Add(_numN);
            grp.Controls.Add(_rbEven);
            grp.Controls.Add(_lblEven);
            Controls.Add(grp);

            // --- Loai proxy + cong tac ---
            Controls.Add(new Label { Text = "Loại proxy", Left = X, Top = 412, Width = 70 });
            _cboType = new ComboBox
            {
                Left = X + 74, Top = 408, Width = 104,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboType.Items.Add("SOCKS5");
            _cboType.Items.Add("HTTP");
            _cboType.SelectedIndex = defaultIsHttp ? 1 : 0;
            _cboType.SelectedIndexChanged += (s, e) => Recompute();
            Controls.Add(_cboType);

            var tip = new ToolTip();
            tip.SetToolTip(_cboType, "Chỉ áp cho dòng KHÔNG có tiền tố. Dòng ghi http:// hoặc socks5:// thì lấy theo dòng.");

            _chkRepeat = new CheckBox { Text = "Lặp lại danh sách nếu thiếu proxy", Left = X, Top = 436, Width = W };
            _chkRepeat.CheckedChanged += (s, e) => Recompute();
            Controls.Add(_chkRepeat);

            // 462 chu khong phai 458: o tich cao 24px (do that, Segoe UI 9pt) nen dat cach 22px la
            // hai o CHONG nhau 2px - chua nhin ra vi chu can giua, nhung la mam cho loi ve lai.
            _chkOnlyEmpty = new CheckBox { Text = "Chỉ gán cho acc CHƯA có proxy", Left = X, Top = 462, Width = W };
            _chkOnlyEmpty.CheckedChanged += (s, e) => Recompute();
            Controls.Add(_chkOnlyEmpty);

        }

        private void BuildRight()
        {
            const int X = 404, W = 408;

            _chkUnassignedOnly = new CheckBox { Text = "Chỉ hiện acc chưa được gán", Left = X, Top = 8, Width = W };
            _chkUnassignedOnly.CheckedChanged += (s, e) => Recompute();
            Controls.Add(_chkUnassignedOnly);

            _lv = new ListView
            {
                Left = X, Top = 34, Width = W, Height = 296,   // 34: o tich tren cao 24px (8..32)
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                MultiSelect = false
            };
            _lv.Columns.Add("#", 42, HorizontalAlignment.Right);
            _lv.Columns.Add("Tài khoản", 108);
            _lv.Columns.Add("Proxy cũ", 112);
            _lv.Columns.Add("Proxy mới", 126);
            Controls.Add(_lv);

            _lblStatus = new Label
            {
                Left = X, Top = 332, Width = W, Height = 46,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 4, 0)
            };
            Controls.Add(_lblStatus);

            _lblMissing = new Label { Left = X, Top = 384, Width = W, Height = 76, ForeColor = Color.FromArgb(150, 60, 0) };
            Controls.Add(_lblMissing);

            _btnCopyMissing = new Button { Text = "Copy danh sách", Left = X, Top = 462, Width = 130, Height = 26 };
            _btnCopyMissing.Click += (s, e) =>
            {
                var sb = new StringBuilder();
                foreach (int i in MissingPositions()) sb.AppendLine(_sel[i].Username);
                try
                {
                    if (sb.Length > 0) Clipboard.SetText(sb.ToString());
                    MessageBox.Show(this, "Đã copy danh sách acc chưa được gán proxy.", "Chia proxy",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }
            };
            Controls.Add(_btnCopyMissing);

            // Gan truoc roi moi boi den phan con lai: neu chi boi den ma khong gan thi cong user vua
            // lam (dan danh sach, chon kieu chia) mat trang.
            _btnSelectMissing = new Button
            {
                Text = "Gán & chọn acc chưa gán trên lưới",
                Left = X + 136, Top = 462, Width = 240, Height = 26
            };
            _btnSelectMissing.Click += (s, e) =>
            {
                if (!ApplyPlan()) return;
                SelectUnassigned = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            var tip2 = new ToolTip();
            tip2.SetToolTip(_btnSelectMissing,
                "Gán như nút \"Gán\", sau đó bôi đen các acc chưa được gán để bạn dán tiếp danh sách khác cho chúng.");
            Controls.Add(_btnSelectMissing);
        }

        private void BuildBottom()
        {
            _btnOk = new Button { Text = "Gán", Left = 596, Top = 562, Width = 132, Height = 28 };
            _btnOk.Click += (s, e) =>
            {
                if (!ApplyPlan()) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_btnOk);

            var btnCancel = new Button
            {
                Text = "Hủy", Left = 734, Top = 562, Width = 78, Height = 28,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = _btnOk;
            CancelButton = btnCancel;
        }

        // ==================== TINH + VE LAI ====================

        /// <summary>Vi tri (trong <see cref="_sel"/>) cua cac acc thuoc dien duoc chia.</summary>
        private List<int> Candidates()
        {
            var list = new List<int>();
            for (int i = 0; i < _sel.Count; i++)
            {
                if (_chkOnlyEmpty.Checked && !string.IsNullOrEmpty(_sel[i].Proxy)) continue;
                list.Add(i);
            }
            return list;
        }

        /// <summary>Vi tri (trong <see cref="_sel"/>) cua cac acc KHONG duoc gan theo ke hoach hien tai.</summary>
        private List<int> MissingPositions()
        {
            var miss = new List<int>();
            if (_plan == null || _plan.ProxyOfAcc == null) return miss;
            var cand = Candidates();
            for (int k = 0; k < cand.Count && k < _plan.ProxyOfAcc.Length; k++)
                if (_plan.ProxyOfAcc[k] < 0) miss.Add(cand[k]);
            return miss;
        }

        private void Recompute()
        {
            _parsed = ProxyBulk.Parse(_txtList.Text);
            var cand = Candidates();

            // Dong trang thai cua o van ban
            var sb = new StringBuilder();
            sb.Append("Đã đọc: ").Append(_parsed.Count).Append(" proxy");
            if (_parsed.Duplicates > 0) sb.Append(" · ").Append(_parsed.Duplicates).Append(" trùng");
            if (_parsed.BadLines.Count > 0)
            {
                sb.Append(" · ").Append(_parsed.BadLines.Count).Append(" dòng lỗi (dòng ");
                for (int i = 0; i < _parsed.BadLines.Count && i < 5; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_parsed.BadLines[i]);
                }
                if (_parsed.BadLines.Count > 5) sb.Append("…");
                sb.Append(")");
            }
            _lblParsed.Text = sb.ToString();
            _lblParsed.ForeColor = _parsed.BadLines.Count > 0 ? Color.FromArgb(180, 40, 40) : SystemColors.GrayText;

            // Kieu chia + o so
            int even = ProxyBulk.EvenPerProxy(cand.Count, _parsed.Count);
            _lblEven.Text = _parsed.Count > 0 && cand.Count > 0
                ? string.Format("{0} ÷ {1} = {2} acc/proxy", cand.Count, _parsed.Count, even)
                : "(chưa đủ dữ liệu)";

            var mode = _rbEven.Checked ? ProxyBulk.Mode.Even : ProxyBulk.Mode.PerProxy;
            if (mode == ProxyBulk.Mode.Even && even >= 1)
            {
                _suppress = true;                     // dien san ma KHONG khoa o nhap
                try { _numN.Value = Math.Min(_numN.Maximum, even); } catch { }
                _suppress = false;
            }

            _plan = ProxyBulk.Divide(cand.Count, _parsed.Count, mode, (int)_numN.Value, _chkRepeat.Checked);

            FillPreview(cand);
            FillStatus(cand);
        }

        private void FillPreview(List<int> cand)
        {
            _lv.BeginUpdate();
            _lv.Items.Clear();
            try
            {
                for (int k = 0; k < cand.Count; k++)
                {
                    int pos = cand[k];
                    int p = k < _plan.ProxyOfAcc.Length ? _plan.ProxyOfAcc[k] : -1;
                    if (_chkUnassignedOnly.Checked && p >= 0) continue;

                    string oldHost = AccountConfig.ProxyHost(_sel[pos].Proxy);
                    string newText;
                    Color fore = SystemColors.ControlText;

                    if (p < 0) { newText = "⚠ không gán"; fore = Color.FromArgb(190, 90, 0); }
                    else
                    {
                        string h = _parsed.Items[p].Host;
                        bool same = string.Equals(h, oldHost, StringComparison.OrdinalIgnoreCase);
                        newText = same ? h + "  (giữ)" : h;
                        if (same) fore = SystemColors.GrayText;
                    }

                    var it = new ListViewItem(new[]
                    {
                        (pos + 1).ToString(),
                        NameMask.Apply(_sel[pos].Username),
                        oldHost.Length == 0 ? "(trống)" : oldHost,
                        newText
                    });
                    it.ForeColor = fore;
                    _lv.Items.Add(it);
                }
            }
            finally { _lv.EndUpdate(); }
        }

        private void FillStatus(List<int> cand)
        {
            bool ok = _plan.NotAssigned == 0 && _plan.Warning.Length == 0;

            if (cand.Count == 0)
            {
                _lblStatus.Text = "Không có acc nào thuộc diện chia (kiểm tra ô \"Chỉ gán cho acc CHƯA có proxy\").";
                _lblStatus.BackColor = Color.FromArgb(255, 244, 214);
            }
            else if (ok)
            {
                _lblStatus.Text = string.Format("✅ {0} acc ← {1} proxy × {2}.  Mỗi proxy đúng {2} acc.",
                    _plan.Assigned, _parsed.Count, _plan.MaxPerProxy);
                _lblStatus.BackColor = Color.FromArgb(226, 248, 226);
            }
            else
            {
                _lblStatus.Text = (_plan.NotAssigned > 0 ? "⛔ " : "⚠️ ") + _plan.Warning;
                _lblStatus.BackColor = _plan.NotAssigned > 0
                    ? Color.FromArgb(255, 228, 225)
                    : Color.FromArgb(255, 244, 214);
            }

            // Danh sach ACC KHONG CO PROXY (user chot: phai noi ro acc nao)
            var miss = MissingPositions();
            if (miss.Count == 0)
            {
                _lblMissing.Text = "";
                _btnCopyMissing.Enabled = false;
                _btnSelectMissing.Enabled = false;
            }
            else
            {
                var sb = new StringBuilder("Acc chưa có proxy: ");
                for (int i = 0; i < miss.Count && i < 8; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(NameMask.Apply(_sel[miss[i]].Username));
                }
                if (miss.Count > 8) sb.Append(string.Format(" … và {0} acc khác", miss.Count - 8));
                _lblMissing.Text = sb.ToString();
                _btnCopyMissing.Enabled = true;
                _btnSelectMissing.Enabled = true;
            }

            // (Da bo han dong nhac "N > tran cong IP": tu 2026-09-12 cong dem theo MAY CHU, khong
            // con dem theo proxy, nen so acc/proxy khong con lien quan gi den tran cua cong.)

            _btnOk.Text = _plan.Assigned > 0 ? ("Gán " + _plan.Assigned + " acc") : "Gán";
            _btnOk.Enabled = _plan.Assigned > 0;
        }

        // ==================== AP DUNG ====================

        /// <summary>Ghi ke hoach dang hien vao AccountConfig. false = khong co gi de ghi.</summary>
        private bool ApplyPlan()
        {
            if (_plan == null || _plan.Assigned <= 0) return false;

            var cand = Candidates();

            // CHOT danh sach acc thieu proxy TRUOC khi ghi.
            // Candidates() doc Proxy HIEN TAI cua acc, nen khi o "Chỉ gán cho acc CHƯA có proxy"
            // dang bat thi goi lai SAU khi ghi se ra mot danh sach NGAN HON (acc vua duoc gan da
            // roi khoi dien) - ma _plan.ProxyOfAcc[k] lai danh chi so theo danh sach CU => lech
            // hang. Vi du 10 acc trang / 1 proxy / N=4: dung phai bao 6 acc thieu (4..9), tinh lai
            // sau khi ghi thi chi con bao 2 acc va con bao SAI TEN (8, 9).
            var missBefore = MissingPositions();

            var accs = new List<AccountConfig>(cand.Count);
            foreach (int pos in cand) accs.Add(_sel[pos]);

            var changedInCand = new List<int>();
            int changed = ProxyBulk.Apply(accs, _parsed.Items, _plan,
                _cboType.SelectedIndex == 1, changedInCand);

            ChangedRows.Clear();
            foreach (int k in changedInCand)
            {
                int pos = cand[k];
                if (pos >= 0 && pos < _rows.Count) ChangedRows.Add(_rows[pos]);
            }

            UnassignedRows.Clear();
            foreach (int pos in missBefore)
                if (pos >= 0 && pos < _rows.Count) UnassignedRows.Add(_rows[pos]);

            LogLine = string.Format(
                "[System] Chia {0} proxy cho {1} acc ({2}) — {3} đổi, {4} không gán",
                _parsed.Count, _plan.Assigned,
                _rbEven.Checked ? "chia đều " + _plan.MaxPerProxy + " acc/proxy"
                                : (int)_numN.Value + " acc/proxy",
                changed, _plan.NotAssigned);
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _debounce != null) { _debounce.Dispose(); _debounce = null; }
            base.Dispose(disposing);
        }
    }
}
