using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NSOKHODO.Client;
using NSOKHODO.Protocol;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Dialog thêm nhiều tài khoản một lúc (nút "+Nhiều" - giống NSOTool).
    /// Mỗi dòng: <c>user|pass</c> (chấp nhận cả tab hoặc khoảng trắng làm dấu phân cách).
    /// Máy chủ chọn chung cho cả danh sách.
    /// </summary>
    public class BulkAddForm : Form
    {
        private TextBox _txtList;
        private ComboBox _cboServer;

        /// <summary>Các tài khoản parse được khi DialogResult.OK.</summary>
        public List<AccountConfig> Result { get; private set; }

        public BulkAddForm()
        {
            AppIcon.Apply(this);   // icon app cho MOI cua so (xem UI/AppIcon.cs)
            Result = new List<AccountConfig>();
            Text = "Thêm nhiều tài khoản";
            ClientSize = new Size(420, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            Controls.Add(new Label
            {
                Text = "Mỗi dòng một tài khoản: user|pass",
                Left = 12, Top = 10, Width = 300
            });

            _txtList = new TextBox
            {
                Left = 12, Top = 32, Width = 396, Height = 240,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true, Font = new Font("Consolas", 9f)
            };
            Controls.Add(_txtList);

            Controls.Add(new Label { Text = "Máy chủ", Left = 12, Top = 285, Width = 60 });
            _cboServer = new ComboBox
            {
                Left = 80, Top = 282, Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            ReloadServers(null);
            Controls.Add(_cboServer);
            var btnAddServer = new Button { Text = "＋ Máy chủ", Left = 266, Top = 281, Width = 90, Height = 25 };
            btnAddServer.Click += (s, e) =>
            {
                using (var dlg = new AddServerForm())
                    if (dlg.ShowDialog(this) == DialogResult.OK) ReloadServers(dlg.AddedName);
            };
            Controls.Add(btnAddServer);

            var btnOk = new Button { Text = "Thêm", Left = 240, Top = 315, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Hủy", Left = 328, Top = 315, Width = 80, DialogResult = DialogResult.Cancel };
            btnOk.Click += (s, e) =>
            {
                if (!ParseList())
                {
                    MessageBox.Show("Không parse được dòng nào. Định dạng: user|pass", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

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

        private bool ParseList()
        {
            Result.Clear();
            foreach (var raw in _txtList.Lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0) continue;

                string[] parts = line.Split(new[] { '|', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var srv = _cboServer.SelectedItem as Models.ServerInfo;
                Result.Add(new AccountConfig
                {
                    Username = parts[0].Trim(),
                    Password = parts[1].Trim(),
                    ServerName = srv != null ? srv.Name : null,
                    ServerIndex = _cboServer.SelectedIndex,
                });
            }
            return Result.Count > 0;
        }
    }
}
