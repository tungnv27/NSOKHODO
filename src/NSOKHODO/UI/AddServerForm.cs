using System;
using System.Drawing;
using System.Windows.Forms;
using NSOKHODO.Protocol;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Dialog them MAY CHU tu chon. Luu vao Data/servers_custom.txt (khong bi refresh URL ghi de).
    /// Port mac dinh 14444. "Server login" la byte phan biet cac server TRUNG IP:Port (vd Tessen=1
    /// cung IP voi Shuriken=0) - dien sai = vao nham server.
    /// </summary>
    public class AddServerForm : Form
    {
        private TextBox _txtName, _txtHost, _txtPort, _txtLogin;

        /// <summary>Ten server vua them (de form cha chon lai trong combo).</summary>
        public string AddedName { get; private set; }

        public AddServerForm()
        {
            AppIcon.Apply(this);   // icon app cho MOI cua so (xem UI/AppIcon.cs)
            Text = "Thêm máy chủ";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 210);
            Font = new Font("Segoe UI", 9f);

            int y = 14;
            const int lblX = 14, inX = 150, inW = 210, rowH = 30;

            _txtName = Row("Tên máy chủ", ref y, lblX, inX, inW, rowH);
            _txtHost = Row("Địa chỉ (IP/host)", ref y, lblX, inX, inW, rowH);
            _txtPort = Row("Cổng (port)", ref y, lblX, inX, inW, rowH);
            _txtPort.Text = ServerList.DefaultPort.ToString();   // mac dinh 14444
            _txtLogin = Row("Server login (0 nếu không rõ)", ref y, lblX, inX, inW, rowH);
            _txtLogin.Text = "0";
            y += 8;

            var btnOk = new Button { Text = "Thêm", Left = inX + 30, Top = y, Width = 85, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Hủy", Left = inX + 125, Top = y, Width = 85, DialogResult = DialogResult.Cancel };
            btnOk.Click += OnOk;
            Controls.Add(btnOk); Controls.Add(btnCancel);
            AcceptButton = btnOk; CancelButton = btnCancel;
        }

        private TextBox Row(string label, ref int y, int lblX, int inX, int inW, int rowH)
        {
            Controls.Add(new Label { Text = label, Left = lblX, Top = y + 3, Width = inX - lblX - 4, AutoSize = false });
            var tb = new TextBox { Left = inX, Top = y, Width = inW };
            Controls.Add(tb);
            y += rowH;
            return tb;
        }

        private void OnOk(object sender, EventArgs e)
        {
            int port; if (!int.TryParse(_txtPort.Text.Trim(), out port)) port = ServerList.DefaultPort;
            byte login; byte.TryParse(_txtLogin.Text.Trim(), out login);

            string err;
            if (!ServerList.AddCustom(_txtName.Text, _txtHost.Text, port, login, out err))
            {
                MessageBox.Show(err ?? "Không thêm được máy chủ.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            AddedName = _txtName.Text.Trim();
        }
    }
}
