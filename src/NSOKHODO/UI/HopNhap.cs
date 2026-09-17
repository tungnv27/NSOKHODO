using System;
using System.Drawing;
using System.Windows.Forms;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Hộp nhập nhỏ dựng bằng code (WinForms không có InputBox). Dùng cho các thao tác chuột phải:
    /// theo dõi món, thêm món rác, gán kệ tay, rút gói.
    /// </summary>
    public static class HopNhap
    {
        /// <summary>Hỏi một chuỗi. Trả null nếu bấm Huỷ.</summary>
        public static string HoiChu(IWin32Window chu, string tieuDe, string nhan, string macDinh)
        {
            using (var f = TaoForm(tieuDe))
            {
                var lbl = new Label { Text = nhan, Left = 12, Top = 12, Width = 360, Height = 36 };
                var txt = new TextBox { Left = 12, Top = 50, Width = 360, Text = macDinh ?? "" };
                f.Controls.Add(lbl);
                f.Controls.Add(txt);
                ThemNut(f, 84);
                return f.ShowDialog(chu) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        /// <summary>Hỏi một số trong khoảng [min, max]. Trả null nếu bấm Huỷ.</summary>
        public static int? HoiSo(IWin32Window chu, string tieuDe, string nhan, int macDinh, int min, int max)
        {
            using (var f = TaoForm(tieuDe))
            {
                var lbl = new Label { Text = nhan, Left = 12, Top = 12, Width = 360, Height = 36 };
                var num = new NumericUpDown
                {
                    Left = 12, Top = 50, Width = 140, Minimum = min, Maximum = max,
                    Value = Math.Max(min, Math.Min(max, macDinh)),
                };
                f.Controls.Add(lbl);
                f.Controls.Add(num);
                ThemNut(f, 84);
                return f.ShowDialog(chu) == DialogResult.OK ? (int?)(int)num.Value : null;
            }
        }

        /// <summary>Hỏi chọn một mục trong danh sách (có thể gõ tay nếu <paramref name="choGo"/>).</summary>
        public static string HoiChon(IWin32Window chu, string tieuDe, string nhan, string[] muc, string macDinh, bool choGo)
        {
            using (var f = TaoForm(tieuDe))
            {
                var lbl = new Label { Text = nhan, Left = 12, Top = 12, Width = 360, Height = 36 };
                var cb = new ComboBox
                {
                    Left = 12, Top = 50, Width = 360,
                    DropDownStyle = choGo ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
                };
                cb.Items.AddRange(muc ?? new string[0]);
                if (!string.IsNullOrEmpty(macDinh))
                {
                    int i = cb.Items.IndexOf(macDinh);
                    if (i >= 0) cb.SelectedIndex = i;
                    else if (choGo) cb.Text = macDinh;
                }
                else if (!choGo && cb.Items.Count > 0) cb.SelectedIndex = 0;
                f.Controls.Add(lbl);
                f.Controls.Add(cb);
                ThemNut(f, 84);
                if (f.ShowDialog(chu) != DialogResult.OK) return null;
                return choGo ? cb.Text.Trim() : (cb.SelectedItem as string);
            }
        }

        private static Form TaoForm(string tieuDe)
        {
            var f = new Form
            {
                Text = tieuDe,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(386, 124),
            };
            return f;
        }

        private static void ThemNut(Form f, int y)
        {
            var ok = new Button { Text = "OK", Left = 206, Top = y, Width = 80, DialogResult = DialogResult.OK };
            var huy = new Button { Text = "Huỷ", Left = 292, Top = y, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.Add(ok);
            f.Controls.Add(huy);
            f.AcceptButton = ok;
            f.CancelButton = huy;
        }
    }
}
