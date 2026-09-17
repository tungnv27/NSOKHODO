using System;
using System.Drawing;
using System.Windows.Forms;
using NSOKHODO.Config;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Hop thoai "Danh sach tai khoan" - mo / tao / doi ten / nhan ban / xoa cac danh sach acc.
    /// Xem docs/features/DANH_SACH_ACC.md §D.2.
    ///
    /// <para>Hop thoai nay KHONG tu doi danh sach. No chi tra ve TEN nguoi dung muon mo qua
    /// <see cref="SelectedName"/>; viec dong game + nap lai do MainForm lam.</para>
    /// </summary>
    internal sealed class AccountListForm : Form
    {
        /// <summary>Ten danh sach user chon MO (null = khong mo gi).</summary>
        public string SelectedName { get; private set; }

        private readonly string _current;
        private readonly ListView _list;
        private Button _btnOpen;

        public AccountListForm(string currentList) : this(currentList, null) { }

        /// <param name="warning">Dòng cảnh báo đỏ hiện trên đầu (null = không có). Dùng khi mở lúc
        /// khởi động vì danh sách muốn mở đang bị cửa sổ khác giữ - gộp vào đây thay vì bắt người
        /// dùng bấm qua một MessageBox riêng rồi mới tới hộp thoại này.</param>
        public AccountListForm(string currentList, string warning)
        {
            AppIcon.Apply(this);   // icon app cho MOI cua so (xem UI/AppIcon.cs)
            _current = currentList;

            Text = "Danh sách tài khoản";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 340);
            Font = new Font("Segoe UI", 9f);

            _list = new ListView
            {
                Left = 10, Top = 10, Width = 540, Height = 240,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            _list.Columns.Add("Tên", 230);
            _list.Columns.Add("Số acc", 70, HorizontalAlignment.Right);
            _list.Columns.Add("Sửa lần cuối", 130);
            _list.Columns.Add("Trạng thái", 100);
            _list.DoubleClick += (s, e) => DoOpen();
            Controls.Add(_list);

            if (!string.IsNullOrEmpty(warning))
            {
                // Đẩy lưới xuống để nhét dòng cảnh báo lên trên.
                _list.Top += 34;
                _list.Height -= 34;
                Controls.Add(new Label
                {
                    Left = 10, Top = 12, Width = 540, Height = 30,
                    ForeColor = Color.Firebrick,
                    Text = "⚠ " + warning
                });
            }

            int y = 260;
            _btnOpen = AddBtn("Mở", 10, y, 70, (s, e) => DoOpen());
            AddBtn("Tạo mới", 86, y, 80, (s, e) => DoCreate());
            AddBtn("Đổi tên", 172, y, 80, (s, e) => DoRename());
            AddBtn("Nhân bản", 258, y, 90, (s, e) => DoDuplicate());
            AddBtn("Xóa", 354, y, 70, (s, e) => DoDelete());

            AddBtn("Tạo shortcut ra Desktop", 10, y + 34, 180, (s, e) => DoShortcut());

            var btnClose = new Button { Text = "Đóng", Left = 470, Top = y, Width = 80, Height = 28 };
            btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnClose);
            CancelButton = btnClose;
            AcceptButton = _btnOpen;   // Enter = Mở danh sách đang chọn

            var hint = new Label
            {
                Left = 200, Top = y + 38, Width = 350, Height = 32,
                ForeColor = Color.DimGray,
                Text = "Mỗi danh sách là một file acc riêng. Shortcut mở thẳng "
                     + "đúng danh sách đó, dùng khi muốn chạy nhiều cửa sổ."
            };
            Controls.Add(hint);

            Reload();
        }

        private Button AddBtn(string text, int x, int y, int w, EventHandler onClick)
        {
            var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 28 };
            b.Click += onClick;
            Controls.Add(b);
            return b;
        }

        private void Reload()
        {
            _list.Items.Clear();
            foreach (var info in AccountListStore.List())
            {
                var it = new ListViewItem(info.Name);
                it.SubItems.Add(info.Count < 0 ? "?" : info.Count.ToString());
                it.SubItems.Add(info.Modified.ToString("dd/MM HH:mm"));

                string st = "";
                if (string.Equals(info.Name, _current, StringComparison.OrdinalIgnoreCase))
                {
                    st = "đang mở";
                    it.Font = new Font(_list.Font, FontStyle.Bold);
                }
                else if (info.HolderPid > 0)
                {
                    st = "cửa sổ khác";
                    it.ForeColor = Color.DimGray;
                }
                else if (info.Count < 0)
                {
                    st = "lỗi đọc";
                    it.ForeColor = Color.Firebrick;
                }
                it.SubItems.Add(st);
                it.Tag = info;
                _list.Items.Add(it);
                if (string.Equals(info.Name, _current, StringComparison.OrdinalIgnoreCase))
                    it.Selected = true;
            }

            // Mở từ lúc khởi động (_current = null) thì KHÔNG dòng nào khớp -> không có dòng nào
            // được chọn, bấm "Mở" sẽ không làm gì và người dùng tưởng nút hỏng. Chọn sẵn dòng đầu.
            if (_list.SelectedItems.Count == 0 && _list.Items.Count > 0)
                _list.Items[0].Selected = true;
            _list.Select();
        }

        private AccountListInfo Current()
        {
            if (_list.SelectedItems.Count == 0) return null;
            return _list.SelectedItems[0].Tag as AccountListInfo;
        }

        private void DoOpen()
        {
            var info = Current();
            if (info == null) return;
            if (string.Equals(info.Name, _current, StringComparison.OrdinalIgnoreCase))
            {
                DialogResult = DialogResult.Cancel;   // dang mo san roi
                Close();
                return;
            }
            if (info.HolderPid > 0)
            {
                MessageBox.Show(this,
                    "Danh sách này đang mở ở cửa sổ khác (PID " + info.HolderPid + ").",
                    "Không mở được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SelectedName = info.Name;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DoCreate()
        {
            string name = Ask("Tên danh sách mới:", "");
            if (name == null) return;
            if (!AccountListStore.Create(name))
            {
                MessageBox.Show(this, "Tên đã tồn tại hoặc không hợp lệ.", "Không tạo được",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Reload();
        }

        private void DoRename()
        {
            var info = Current();
            if (info == null) return;
            if (!CheckNotBusy(info, "đổi tên")) return;

            string name = Ask("Tên mới:", info.Name);
            if (name == null) return;
            if (!AccountListStore.Rename(info.Name, name))
            {
                MessageBox.Show(this, "Tên đã tồn tại hoặc không đổi được.", "Không đổi tên được",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Reload();
        }

        private void DoDuplicate()
        {
            var info = Current();
            if (info == null) return;
            string name = Ask("Tên bản sao:", info.Name + " (2)");
            if (name == null) return;
            if (!AccountListStore.Duplicate(info.Name, name))
            {
                MessageBox.Show(this, "Tên đã tồn tại hoặc không sao chép được.", "Không nhân bản được",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Reload();
        }

        private void DoDelete()
        {
            var info = Current();
            if (info == null) return;
            if (!CheckNotBusy(info, "xóa")) return;

            string n = info.Count < 0 ? "?" : info.Count.ToString();
            if (MessageBox.Show(this,
                    "Xóa danh sách \"" + info.Name + "\" (" + n + " tài khoản)?"
                    + Environment.NewLine + "Không khôi phục lại được.",
                    "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            AccountListStore.Delete(info.Name);
            Reload();
        }

        /// <summary>Chan sua/xoa danh sach dang mo (o day hoac o cua so khac).</summary>
        private bool CheckNotBusy(AccountListInfo info, string what)
        {
            if (string.Equals(info.Name, _current, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Không " + what + " được danh sách đang mở. Hãy mở danh sách khác trước.",
                    "Đang mở", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (info.HolderPid > 0)
            {
                MessageBox.Show(this, "Danh sách đang mở ở cửa sổ khác (PID " + info.HolderPid + ").",
                    "Đang bận", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Tao shortcut Desktop mo thang danh sach dang chon (<c>NSOKHODO.exe --list=Ten</c>).
        /// Day la cach de nhat de chay NHIEU cua so, moi cua so mot nhom acc.
        ///
        /// <para>Dung COM WScript.Shell qua reflection chu khong tham chieu IWshRuntimeLibrary:
        /// khong keo them phu thuoc vao csproj cho MOT tinh nang phu.</para>
        /// </summary>
        private void DoShortcut()
        {
            var info = Current();
            if (info == null) return;
            try
            {
                string exe = Application.ExecutablePath;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = System.IO.Path.Combine(desktop, "NSOKHODO - " + info.Name + ".lnk");

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) throw new NotSupportedException("Không có WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                                           null, shell, new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc,
                                new object[] { exe });
                st.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, sc,
                                new object[] { "--list=\"" + info.Name + "\"" });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc,
                                new object[] { System.IO.Path.GetDirectoryName(exe) });
                st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc,
                                new object[] { exe + ",0" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);

                MessageBox.Show(this, "Đã tạo shortcut trên Desktop:" + Environment.NewLine
                    + System.IO.Path.GetFileName(lnk), "Xong",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không tạo được shortcut: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Hop nhap mot dong - WinForms khong co san InputBox.</summary>
        private string Ask(string prompt, string init)
        {
            using (var dlg = new Form
            {
                Text = "Danh sách tài khoản",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(340, 110),
                Font = Font
            })
            {
                AppIcon.Apply(dlg);
                dlg.Controls.Add(new Label { Text = prompt, Left = 12, Top = 14, Width = 310 });
                var txt = new TextBox { Left = 12, Top = 36, Width = 310, Text = init };
                dlg.Controls.Add(txt);

                var ok = new Button { Text = "OK", Left = 166, Top = 70, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Hủy", Left = 247, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };
                dlg.Controls.Add(ok);
                dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                if (dlg.ShowDialog(this) != DialogResult.OK) return null;
                string s = (txt.Text ?? "").Trim();
                return s.Length == 0 ? null : s;
            }
        }
    }
}
