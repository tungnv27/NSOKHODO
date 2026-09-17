using System;
using System.Drawing;
using System.Windows.Forms;
using NSOKHODO.Client;
using NSOKHODO.Protocol;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Dialog thêm/sửa 1 tài khoản — giống NSOManager: CHỈ thông tin đăng nhập
    /// (tài khoản / mật khẩu / máy chủ / proxy). Mọi cấu hình train chỉnh ở
    /// tab Train của MainForm sau khi thêm.
    /// </summary>
    public class AddAccountForm : Form
    {
        private TextBox _txtUser, _txtPass, _txtProxy;
        private ComboBox _cboServer, _cboProxyType;

        /// <summary>Tài khoản kết quả (config mới hoặc đã cập nhật).</summary>
        public AccountConfig Result { get; private set; }

        public AddAccountForm(AccountConfig editing = null)
        {
            AppIcon.Apply(this);   // icon app cho MOI cua so (xem UI/AppIcon.cs)
            Result = editing ?? new AccountConfig();
            BuildUi();
            LoadFrom(Result);
            Text = editing == null ? "Thêm tài khoản" : "Sửa tài khoản";
        }

        private void BuildUi()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, 226);
            Font = new Font("Segoe UI", 9f);

            int y = 14;
            const int lblX = 14, inX = 140, inW = 270, rowH = 30;

            _txtUser = AddRow("Tài khoản", ref y, lblX, inX, inW, rowH);
            _txtPass = AddRow("Mật khẩu", ref y, lblX, inX, inW, rowH);
            _txtPass.UseSystemPasswordChar = true;

            AddLabel("Máy chủ", lblX, y + 3);
            _cboServer = new ComboBox
            {
                Left = inX, Top = y, Width = inW - 64,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            ReloadServers(null);
            Controls.Add(_cboServer);
            var btnAddServer = new Button { Text = "＋", Left = inX + inW - 58, Top = y - 1, Width = 58, Height = 25 };
            btnAddServer.Click += OnAddServer;
            Controls.Add(btnAddServer);
            y += rowH;

            _txtProxy = AddRow("Proxy (host:port:user:pass)", ref y, lblX, inX, inW, rowH);

            AddLabel("Loại proxy", lblX, y + 3);
            _cboProxyType = new ComboBox
            {
                Left = inX, Top = y, Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboProxyType.Items.Add("SOCKS5");
            _cboProxyType.Items.Add("HTTP");
            _cboProxyType.SelectedIndex = 0; // mac dinh SOCKS5
            Controls.Add(_cboProxyType);
            y += rowH;
            y += 10;

            var btnOk = new Button { Text = "Lưu", Left = inX + 90, Top = y, Width = 85, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Hủy", Left = inX + 185, Top = y, Width = 85, DialogResult = DialogResult.Cancel };
            btnOk.Click += (s, e) => { if (ValidateInput()) SaveTo(); else DialogResult = DialogResult.None; };
            Controls.Add(btnOk); Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private TextBox AddRow(string label, ref int y, int lblX, int inX, int inW, int rowH)
        {
            AddLabel(label, lblX, y + 3);
            var tb = new TextBox { Left = inX, Top = y, Width = inW };
            Controls.Add(tb);
            y += rowH;
            return tb;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, Left = x, Top = y, Width = 126, AutoSize = false });
        }

        /// <summary>Nap lai combo tu danh sach hien tai; chon server theo TEN neu co.</summary>
        private void ReloadServers(string selectName)
        {
            _cboServer.Items.Clear();
            int sel = 0, i = 0;
            foreach (var s in ServerList.Servers)
            {
                _cboServer.Items.Add(s);
                if (selectName != null && s.Name.Equals(selectName, StringComparison.OrdinalIgnoreCase)) sel = i;
                i++;
            }
            if (_cboServer.Items.Count > 0) _cboServer.SelectedIndex = sel;
        }

        private void OnAddServer(object sender, EventArgs e)
        {
            using (var dlg = new AddServerForm())
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadServers(dlg.AddedName);   // chon ngay server vua them
        }

        private void LoadFrom(AccountConfig a)
        {
            _txtUser.Text = a.Username ?? "";
            _txtPass.Text = a.Password ?? "";
            // Uu tien chon theo ten; account cu (chua co ServerName) -> dich qua Resolve roi chon ten do.
            var s = ServerList.Resolve(a.ServerName, a.ServerIndex);
            ReloadServers(s != null ? s.Name : a.ServerName);
            bool isHttp; string bare;
            AccountConfig.SplitProxy(a.Proxy, out isHttp, out bare);
            _txtProxy.Text = bare;
            _cboProxyType.SelectedIndex = isHttp ? 1 : 0;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(_txtUser.Text) || string.IsNullOrWhiteSpace(_txtPass.Text))
            {
                MessageBox.Show("Nhập tài khoản và mật khẩu.", "Thiếu thông tin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void SaveTo()
        {
            var a = Result;
            a.Username = _txtUser.Text.Trim();
            a.Password = _txtPass.Text.Trim();
            var srv = _cboServer.SelectedItem as Models.ServerInfo;
            a.ServerName = srv != null ? srv.Name : null;   // dinh danh ben
            a.ServerIndex = _cboServer.SelectedIndex;        // legacy/hien thi
            a.Proxy = AccountConfig.JoinProxy(_cboProxyType.SelectedIndex == 1, _txtProxy.Text);
        }
    }
}
