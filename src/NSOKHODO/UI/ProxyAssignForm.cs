using System;
using System.Drawing;
using System.Windows.Forms;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Dialog gán proxy cho nhiều tài khoản đã chọn (giống NSOTool: chọn N dòng → gán 1 proxy).
    /// Ô proxy luôn nhập dạng trơn <c>host:port:user:pass</c>; loại (SOCKS5/HTTP) chọn riêng,
    /// mặc định SOCKS5. Để trống ô proxy = bỏ proxy cho các tài khoản đó.
    /// </summary>
    public class ProxyAssignForm : Form
    {
        private TextBox _txtProxy;
        private ComboBox _cboType;

        /// <summary>Chuỗi proxy trơn (host:port:user:pass) — có thể rỗng.</summary>
        public string ProxyText { get; private set; }
        /// <summary>True nếu người dùng chọn HTTP.</summary>
        public bool IsHttp { get; private set; }

        public ProxyAssignForm(string bareProxy, bool isHttp, int count)
        {
            AppIcon.Apply(this);   // icon app cho MOI cua so (xem UI/AppIcon.cs)
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, 168);
            Font = new Font("Segoe UI", 9f);
            Text = string.Format("Gán proxy cho {0} tài khoản", count);

            const int lblX = 14, inX = 120, inW = 296;
            int y = 16;

            Controls.Add(new Label
            {
                Text = "Áp dụng cho " + count + " tài khoản đã chọn. Để trống = bỏ proxy.",
                Left = lblX, Top = y, Width = 400
            });
            y += 28;

            Controls.Add(new Label { Text = "Proxy", Left = lblX, Top = y + 3, Width = 100 });
            _txtProxy = new TextBox { Left = inX, Top = y, Width = inW, Text = bareProxy ?? "" };
            Controls.Add(_txtProxy);
            y += 30;

            Controls.Add(new Label { Text = "host:port:user:pass", Left = inX, Top = y, Width = inW, ForeColor = SystemColors.GrayText });
            y += 24;

            Controls.Add(new Label { Text = "Loại proxy", Left = lblX, Top = y + 3, Width = 100 });
            _cboType = new ComboBox { Left = inX, Top = y, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboType.Items.Add("SOCKS5");
            _cboType.Items.Add("HTTP");
            _cboType.SelectedIndex = isHttp ? 1 : 0;
            Controls.Add(_cboType);
            y += 36;

            var btnOk = new Button { Text = "Gán", Left = inX + 96, Top = y, Width = 90, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Hủy", Left = inX + 192, Top = y, Width = 90, DialogResult = DialogResult.Cancel };
            btnOk.Click += (s, e) =>
            {
                ProxyText = _txtProxy.Text.Trim();
                IsHttp = _cboType.SelectedIndex == 1;
            };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
