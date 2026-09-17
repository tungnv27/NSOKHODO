using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NSOKHODO.Kho;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Tab <b>Nhật ký</b>: đọc <c>Logs/&lt;danh sách&gt;/&lt;ngày&gt;/</c>, lọc theo chữ (acc / đối tác / món)
    /// — SPEC §8.1, §10.
    /// </summary>
    public partial class MainForm
    {
        private DateTimePicker _dtNgay;
        private ComboBox _cbTepLog;
        private TextBox _txtLocLog;
        private TextBox _xemLog;
        private Label _lblTepLog;

        private static readonly string[][] TEP_LOG =
        {
            new[] { "Giao dịch (giaodich.csv)", NhatKy.GIAO_DICH },
            new[] { "Lệnh rút (lenh.csv)", NhatKy.LENH },
            new[] { "Chat (chat.log)", NhatKy.CHAT },
            new[] { "Mọi thứ (app.log)", NhatKy.APP },
            new[] { "Hex gói tin (hex.log)", NhatKy.HEX },
        };

        private const int DONG_TOI_DA = 5000;

        private void DungTabNhatKy(TabPage tab)
        {
            var thanh = new Panel { Dock = DockStyle.Top, Height = 34 };
            thanh.Controls.Add(new Label { Text = "Ngày:", Left = 6, Top = 9, Width = 40 });
            _dtNgay = new DateTimePicker { Left = 48, Top = 6, Width = 110, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            thanh.Controls.Add(_dtNgay);
            thanh.Controls.Add(new Label { Text = "Loại:", Left = 168, Top = 9, Width = 36 });
            _cbTepLog = new ComboBox { Left = 206, Top = 6, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var t in TEP_LOG) _cbTepLog.Items.Add(t[0]);
            _cbTepLog.SelectedIndex = 0;
            thanh.Controls.Add(_cbTepLog);
            thanh.Controls.Add(new Label { Text = "Lọc:", Left = 386, Top = 9, Width = 32 });
            _txtLocLog = new TextBox { Left = 420, Top = 6, Width = 180 };
            _txtLocLog.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { TaiNhatKy(); e.SuppressKeyPress = true; } };
            thanh.Controls.Add(_txtLocLog);
            var btnTai = new Button { Text = "Tải", Left = 606, Top = 5, Width = 60, Height = 24 };
            btnTai.Click += (s, e) => TaiNhatKy();
            thanh.Controls.Add(btnTai);
            var btnMoNgay = new Button { Text = "Mở thư mục ngày", Left = 672, Top = 5, Width = 120, Height = 24 };
            btnMoNgay.Click += (s, e) => MoThuMuc(ThuMucNgay());
            thanh.Controls.Add(btnMoNgay);
            var btnMoGoc = new Button { Text = "Mở thư mục log", Left = 798, Top = 5, Width = 110, Height = 24 };
            btnMoGoc.Click += (s, e) => MoThuMuc(NhatKy.ThuMucGoc);
            thanh.Controls.Add(btnMoGoc);
            _lblTepLog = new Label { Left = 916, Top = 9, Width = 420, ForeColor = Color.DimGray };
            thanh.Controls.Add(_lblTepLog);

            _xemLog = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false,
                ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 9f),
            };
            tab.Controls.Add(_xemLog);
            tab.Controls.Add(thanh);
            tab.Enter += (s, e) => { if (_xemLog.TextLength == 0) TaiNhatKy(); };
        }

        private string ThuMucNgay()
        {
            return Path.Combine(NhatKy.ThuMucGoc, _dtNgay.Value.ToString("yyyy-MM-dd"));
        }

        private void TaiNhatKy()
        {
            NhatKy.XaNgay();   // hôm nay: ghi nốt hàng đợi trước khi đọc
            string thuMuc = ThuMucNgay();
            string tep = TEP_LOG[Math.Max(0, _cbTepLog.SelectedIndex)][1];
            string ten = Path.GetFileNameWithoutExtension(tep);
            string duoi = Path.GetExtension(tep);
            var cacPhan = new List<string>();
            string p = Path.Combine(thuMuc, tep);
            for (int i = 2; File.Exists(p); i++)
            {
                cacPhan.Add(p);
                p = Path.Combine(thuMuc, ten + "_" + i + duoi);
            }
            if (cacPhan.Count == 0)
            {
                _xemLog.Text = "(không có " + tep + " cho ngày " + _dtNgay.Value.ToString("dd/MM/yyyy") + ")\r\n" + thuMuc;
                _lblTepLog.Text = "";
                return;
            }
            string loc = ChuVan.ChuanHoa(_txtLocLog.Text ?? "");
            var dong = new List<string>();
            string tieuDe = null;
            int tong = 0;
            try
            {
                foreach (var f in cacPhan)
                {
                    // FileShare.ReadWrite: luồng NhatKy có thể đang ghi đúng file này.
                    using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string s;
                        bool dau = true;
                        while ((s = sr.ReadLine()) != null)
                        {
                            if (dau && duoi == ".csv") { if (tieuDe == null) tieuDe = s; dau = false; continue; }
                            dau = false;
                            tong++;
                            if (loc.Length > 0 && !ChuVan.ChuanHoa(s).Contains(loc)) continue;
                            dong.Add(s);
                            if (dong.Count > DONG_TOI_DA * 2) dong.RemoveRange(0, DONG_TOI_DA);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _xemLog.Text = "Đọc log lỗi: " + ex.Message;
                return;
            }
            if (dong.Count > DONG_TOI_DA) dong.RemoveRange(0, dong.Count - DONG_TOI_DA);
            var sb = new StringBuilder();
            if (tieuDe != null) sb.AppendLine(tieuDe);
            foreach (var s in dong) sb.AppendLine(s);
            _xemLog.Text = sb.ToString();
            _xemLog.SelectionStart = _xemLog.TextLength;
            _xemLog.ScrollToCaret();
            _lblTepLog.Text = dong.Count + " dòng" + (loc.Length > 0 ? " khớp / " + tong : "")
                              + (dong.Count >= DONG_TOI_DA ? " (chỉ hiện " + DONG_TOI_DA + " dòng cuối)" : "")
                              + " · " + cacPhan.Count + " phần";
        }

        private void MoThuMuc(string p)
        {
            try
            {
                Directory.CreateDirectory(p);
                Process.Start("explorer.exe", "\"" + p + "\"");
            }
            catch (Exception ex) { XepLog("Không mở được thư mục: " + ex.Message, false); }
        }
    }
}
