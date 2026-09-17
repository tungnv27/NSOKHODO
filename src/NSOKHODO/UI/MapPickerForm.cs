using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using NSOKHODO.Auto;
using NSOKHODO.GameData;
using System.Reflection;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Hộp thoại "Bản đồ" — chọn map đứng canh bằng cách bấm lên **đúng ảnh bản đồ
    /// thế giới của game**. Xem <c>docs/features/BAN_DO.md</c>.
    ///
    /// Ảnh (<c>wm.png</c>) và toạ độ từng map đều là đồ gốc của client, không phải ta vẽ lại —
    /// chi tiết ở <see cref="WorldMap"/>. Level quái lấy từ <see cref="MapLevels"/>.
    ///
    /// <para>Hai lối chọn, vì bản đồ thế giới CHỈ có 73 map: bấm trên ảnh, hoặc chọn trong danh
    /// sách đủ 160 map bên phải (map không có mặt trên ảnh được tô khác màu). Toàn bộ hang cấp
    /// cao vào bằng NPC (91, 94, 105, 114, 125, 139, 140-148, 157-159) chỉ chọn được từ danh sách.</para>
    ///
    /// <para>Hộp thoại này KHÔNG gửi gói tin nào và KHÔNG điều khiển bot — nó chỉ trả về một số
    /// map cho bảng cài đặt, y như việc tự tay gõ số vào ô "Map".</para>
    /// </summary>
    public sealed class MapPickerForm : Form
    {
        // Anh goc 327x301 qua nho tren man hinh PC -> phong nguyen lan (diem gan nhat, giu net
        // pixel). x2 vua man 654x602 va bien vung bam 10px thanh 20px, du chuot.
        private const int ZOOM = 2;

        private readonly MapCanvas _canvas;
        private readonly ListBox _list;
        private readonly TextBox _txtSearch;
        private readonly Label _lblPick;
        private readonly Label _lblCount;
        private readonly CheckBox _chkEdges;
        private readonly List<int> _listIds = new List<int>();

        private int _selected = -1;
        private bool _syncing;

        /// <summary>Map người dùng đã chọn, -1 nếu chưa chọn gì.</summary>
        public int SelectedMapId { get { return _selected; } }

        public MapPickerForm(int currentMapId)
        {
            AppIcon.Apply(this);   // icon app cho MOI cua so (xem UI/AppIcon.cs)
            Text = "Bản đồ — chọn map kho";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            int mapW = WorldMap.ImgW * ZOOM;
            int mapH = WorldMap.ImgH * ZOOM;
            const int SIDE = 236, PAD = 8;

            ClientSize = new Size(PAD * 3 + mapW + SIDE, PAD * 2 + mapH + 66);

            _canvas = new MapCanvas(ZOOM)
            {
                Left = PAD,
                Top = PAD,
                Width = mapW,
                Height = mapH
            };
            _canvas.OnPick += CanvasPicked;
            _canvas.OnConfirm += delegate { Accept(); };
            Controls.Add(_canvas);

            _chkEdges = new CheckBox
            {
                Text = "Hiện đường nối giữa các map",
                Left = PAD,
                Top = PAD + mapH + 6,
                Width = 230
            };
            _chkEdges.CheckedChanged += delegate
            {
                _canvas.ShowEdges = _chkEdges.Checked;
                _canvas.Invalidate();
            };
            new ToolTip().SetToolTip(_chkEdges,
                "Nét liền = đi bộ qua cổng. Nét đứt = NPC dịch chuyển (làng/ngã tư)."
                + Environment.NewLine + "Lấy từ bảng kề MapGraph, chỉ vẽ được cạnh mà HAI đầu đều nằm trên bản đồ.");
            Controls.Add(_chkEdges);

            var lblHint = new Label
            {
                Text = "Bấm một điểm trên bản đồ — bấm đúp là chọn luôn.",
                Left = PAD + 240,
                Top = PAD + mapH + 8,
                Width = mapW - 236,
                ForeColor = Color.Gray
            };
            Controls.Add(lblHint);

            // ===== Cot phai: tim + danh sach du 160 map =====
            _txtSearch = new TextBox
            {
                Left = PAD * 2 + mapW,
                Top = PAD,
                Width = SIDE
            };
            Controls.Add(_txtSearch);
            // Placeholder tu ve: net452 khong co TextBox.PlaceholderText.
            // ⚠️ Dat chu goi y TRUOC khi noi TextChanged. Noi truoc thi chinh dong SetPlaceholder()
            // ban su kien -> FillList() chay luc _list VAN CHUA DUOC TAO -> NullReference ngay khi
            // mo hop thoai (da dinh 2026-09-09).
            SetPlaceholder();
            _txtSearch.TextChanged += delegate { FillList(_txtSearch.Text); };
            _txtSearch.GotFocus += delegate { if (_txtSearch.ForeColor == Color.Gray) ClearPlaceholder(); };
            new ToolTip().SetToolTip(_txtSearch,
                "Gõ tên map (không cần dấu): ounio, phong an" + Environment.NewLine
                + "Gõ số: 55 = map id 55, hoặc map có quái Lv 55" + Environment.NewLine
                + "Gõ dải level: 80-95 = mọi map có quái trong dải đó");

            _list = new ListBox
            {
                Left = PAD * 2 + mapW,
                Top = PAD + 26,
                Width = SIDE,
                Height = mapH - 44,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 17
            };
            _list.DrawItem += DrawListItem;
            _list.SelectedIndexChanged += ListPicked;
            _list.DoubleClick += delegate { Accept(); };
            Controls.Add(_list);

            _lblCount = new Label
            {
                Left = PAD * 2 + mapW,
                Top = PAD + mapH - 15,
                Width = SIDE,
                ForeColor = Color.Gray,
                Text = ""
            };
            Controls.Add(_lblCount);

            // ===== Chan: nhan dang chon + nut =====
            _lblPick = new Label
            {
                Left = PAD,
                Top = ClientSize.Height - 30,
                Width = ClientSize.Width - 200,
                Text = "Chưa chọn map nào"
            };
            Controls.Add(_lblPick);

            var btnOk = new Button
            {
                Text = "Chọn",
                Left = ClientSize.Width - 180,
                Top = ClientSize.Height - 34,
                Width = 84,
                DialogResult = DialogResult.None
            };
            btnOk.Click += delegate { Accept(); };
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text = "Đóng",
                Left = ClientSize.Width - 92,
                Top = ClientSize.Height - 34,
                Width = 84,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            FillList(null);
            if (currentMapId >= 0) Select(currentMapId, true);
        }

        private void SetPlaceholder()
        {
            _txtSearch.ForeColor = Color.Gray;
            _txtSearch.Text = "Tìm: tên map, số map, hay level quái...";
        }

        private void ClearPlaceholder()
        {
            _txtSearch.ForeColor = SystemColors.WindowText;
            _txtSearch.Text = "";
        }

        private string Query
        {
            get { return _txtSearch.ForeColor == Color.Gray ? "" : _txtSearch.Text.Trim(); }
        }

        private void FillList(string filter)
        {
            // Luoi an toan: moi o nhap deu co the ban su kien trong luc dung hop thoai.
            if (_syncing || _list == null || _lblCount == null) return;
            string q = (Query ?? "").ToLowerInvariant();

            _syncing = true;
            try
            {
                _list.BeginUpdate();
                _list.Items.Clear();
                _listIds.Clear();
                for (int id = 0; id < MapNames.List.Length; id++)
                {
                    if (!MapNames.Matches(id, q)) continue;
                    _listIds.Add(id);
                    _list.Items.Add(id);
                }
                _list.EndUpdate();
                _lblCount.Text = _list.Items.Count + "/" + MapNames.List.Length + " map";

                int sel = _listIds.IndexOf(_selected);
                if (sel >= 0) _list.SelectedIndex = sel;
            }
            finally { _syncing = false; }
        }

        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listIds.Count) return;
            int id = _listIds[e.Index];
            bool onMap = WorldMap.HasPos(id);
            bool sel = (e.State & DrawItemState.Selected) != 0;

            e.DrawBackground();

            // Ve bang TextRenderer (GDI) chu KHONG phai Graphics.DrawString (GDI+): DrawString do
            // chu hep hon thuc te nen ten map dai ("Song bang Yamato") de len mat chu "Lv" cua cot
            // level, doc ra "Yamato10 - Lv 11". TextRenderer do dung va tu cat bang dau ba cham.
            const TextFormatFlags F = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter
                                    | TextFormatFlags.SingleLine;

            string lv = MapLevels.Text(id);
            int lvW = 0;
            if (lv.Length > 0)
            {
                using (var f = new Font(e.Font.FontFamily, 7.5f))
                {
                    lvW = TextRenderer.MeasureText(e.Graphics, lv, f, Size.Empty, F).Width;
                    var box = new Rectangle(e.Bounds.Right - lvW - 4, e.Bounds.Top, lvW, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, lv, f, box,
                        sel ? SystemColors.HighlightText : Color.FromArgb(40, 110, 80), F);
                }
            }

            var nameBox = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top,
                                        e.Bounds.Width - lvW - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, MapNames.List[id], e.Font, nameBox,
                sel ? SystemColors.HighlightText : (onMap ? e.ForeColor : Color.FromArgb(150, 90, 70)),
                F | TextFormatFlags.EndEllipsis);

            e.DrawFocusRectangle();
        }

        private void ListPicked(object sender, EventArgs e)
        {
            if (_syncing) return;
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _listIds.Count) return;
            Select(_listIds[i], false);
        }

        private void CanvasPicked(int mapId)
        {
            Select(mapId, true);
        }

        private void Select(int mapId, bool syncList)
        {
            if (mapId < 0 || mapId >= MapNames.List.Length) return;
            _selected = mapId;
            _canvas.Selected = mapId;
            _canvas.Invalidate();

            _lblPick.Text = "Đang chỉ vào:  " + MapNames.LabelWithLevel(mapId)
                + (WorldMap.HasPos(mapId) ? "" : "   (không có trên bản đồ thế giới)");

            if (!syncList) return;
            int i = _listIds.IndexOf(mapId);
            if (i < 0) return;
            _syncing = true;
            try { _list.SelectedIndex = i; }
            finally { _syncing = false; }
        }

        private void Accept()
        {
            if (_selected < 0)
            {
                _lblPick.Text = "Hãy chỉ vào một map trước.";
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Vùng vẽ bản đồ. Tách thành lớp riêng để bật <c>DoubleBuffered</c> — vẽ 73 điểm + chữ
        /// trên nền ảnh phóng to mà không double buffer thì nháy rất rõ mỗi lần rê chuột.
        /// </summary>
        private sealed class MapCanvas : Panel
        {
            private readonly int _zoom;
            private readonly Image _img;
            private int _hover = -1;

            public int Selected = -1;
            public bool ShowEdges;

            public event Action<int> OnPick;
            public event Action OnConfirm;

            public MapCanvas(int zoom)
            {
                _zoom = zoom;
                DoubleBuffered = true;
                BackColor = Color.FromArgb(28, 30, 36);
                _img = LoadWorldMap();
            }

            /// <summary>
            /// Đọc một ảnh nhúng trong EXE. Bản NSOLITEPRO có cả lớp <c>GameGfx</c> lo việc này cho
            /// hàng chục ảnh của cửa sổ "Xem game"; ở đây chỉ còn đúng MỘT ảnh (bản đồ thế giới)
            /// nên chép lấy phần cần thay vì kéo cả lớp đó sang.
            /// </summary>
            private static byte[] DocAnhNhung(string logicalName)
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var st = asm.GetManifestResourceStream(logicalName))
                {
                    if (st == null) return null;
                    var buf = new byte[st.Length];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = st.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return read == buf.Length ? buf : null;
                }
            }

            private static Image LoadWorldMap()
            {
                try
                {
                    byte[] png = DocAnhNhung("NSOKHODO.Gfx.wm.png");
                    if (png == null || png.Length == 0) return null;
                    // Copy sang bitmap RIENG roi bo stream: Image.FromStream giu tham chieu toi
                    // stream, dong stream som la loi khi ve.
                    using (var ms = new MemoryStream(png))
                    using (var tmp = Image.FromStream(ms))
                        return new Bitmap(tmp);
                }
                catch { return null; }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _img != null) _img.Dispose();
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                if (_img == null)
                {
                    g.Clear(Color.FromArgb(28, 30, 36));
                    using (var b = new SolidBrush(Color.Gainsboro))
                        g.DrawString("Không nạp được ảnh bản đồ (wm.png).", Font, b, 10, 10);
                    return;
                }

                // Anh pixel art: PHAI la diem gan nhat, khong duoc lam muot.
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_img, 0, 0, WorldMap.ImgW * _zoom, WorldMap.ImgH * _zoom);

                if (ShowEdges) PaintEdges(g);
                PaintPins(g);
            }

            private void PaintEdges(Graphics g)
            {
                using (var walk = new Pen(Color.FromArgb(190, 60, 200, 130), 1.4f))
                using (var npc = new Pen(Color.FromArgb(150, 255, 150, 80), 1.2f))
                {
                    npc.DashStyle = DashStyle.Dash;
                    var done = new HashSet<long>();

                    for (int a = 0; a < WorldMap.X.Length; a++)
                    {
                        if (!WorldMap.HasPos(a)) continue;
                        short[] edges = MapGraph.GetConnections(a);
                        // O map "nga tu/lang", chi 2 canh dau la cong di bo THAT - phan duoi la
                        // NPC dich chuyen. Quy uoc nay lay tu MapGraph.GetPhysicalWaypointCount.
                        int phys = IsHub(a) ? 2 : edges.Length;

                        for (int i = 0; i < edges.Length; i++)
                        {
                            int b = edges[i];
                            if (!WorldMap.HasPos(b)) continue;
                            bool walkEdge = i < phys;
                            long key = ((long)Math.Min(a, b) << 20) | ((long)Math.Max(a, b) << 4)
                                       | (walkEdge ? 1L : 0L);
                            if (!done.Add(key)) continue;

                            g.DrawLine(walkEdge ? walk : npc,
                                WorldMap.X[a] * _zoom, WorldMap.Y[a] * _zoom,
                                WorldMap.X[b] * _zoom, WorldMap.Y[b] * _zoom);
                        }
                    }
                }
            }

            private static bool IsHub(int m)
            {
                return m == 1 || m == 10 || m == 17 || m == 22 || m == 27 || m == 32
                    || m == 38 || m == 43 || m == 48 || m == 72 || m == 139;
            }

            private void PaintPins(Graphics g)
            {
                // Anh goc DA CO san dau ✕ ve tai dung cho tung map (da kiem chung: 73/73 toa do roi
                // trung dau ✕). Nen KHONG ve vong tron cho map thuong nua - user chot 2026-09-09:
                // "co dau X hoac tron la duoc roi, khong can vong tron bao quanh X".
                // Chi danh dau: map DANG CHON (cham do dac) va map dang re chuot (vien mo).
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (_hover >= 0 && _hover != Selected && WorldMap.HasPos(_hover))
                {
                    int hx = WorldMap.X[_hover] * _zoom, hy = WorldMap.Y[_hover] * _zoom;
                    int hr = 7;
                    using (var p = new Pen(Color.FromArgb(170, 255, 255, 255), 1.6f))
                        g.DrawEllipse(p, hx - hr, hy - hr, hr * 2, hr * 2);
                }

                if (Selected >= 0 && WorldMap.HasPos(Selected))
                {
                    int sx = WorldMap.X[Selected] * _zoom, sy = WorldMap.Y[Selected] * _zoom;
                    int sr = 6;
                    using (var b = new SolidBrush(Color.FromArgb(235, 220, 55, 35)))
                        g.FillEllipse(b, sx - sr, sy - sr, sr * 2, sr * 2);
                    using (var p = new Pen(Color.FromArgb(235, 255, 255, 255), 1.8f))
                        g.DrawEllipse(p, sx - sr, sy - sr, sr * 2, sr * 2);
                }

                // Ten map cua diem dang chon / dang re chuot - ve SAU cung de khong bi diem khac de len.
                int show = Selected >= 0 ? Selected : _hover;
                if (show >= 0 && WorldMap.HasPos(show)) PaintName(g, show, true);
                if (_hover >= 0 && _hover != show && WorldMap.HasPos(_hover)) PaintName(g, _hover, false);
            }

            private void PaintName(Graphics g, int id, bool strong)
            {
                string text = MapNames.NameOf(id);
                using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(text, f);
                    int cx = WorldMap.X[id] * _zoom, cy = WorldMap.Y[id] * _zoom;

                    // Can chu theo dung luat cua client (MapScr.cs:333) de chu khong tran mep anh.
                    float tx;
                    switch (WorldMap.LabelAlign(id))
                    {
                        case 0: tx = cx; break;
                        case 2: tx = cx - sz.Width; break;
                        default: tx = cx - sz.Width / 2f; break;
                    }
                    float ty = cy - 13 - sz.Height;
                    if (ty < 0) ty = cy + 12;

                    using (var bg = new SolidBrush(Color.FromArgb(strong ? 190 : 130, 10, 12, 16)))
                        g.FillRectangle(bg, tx - 3, ty - 1, sz.Width + 6, sz.Height + 2);
                    using (var b = new SolidBrush(strong ? Color.FromArgb(255, 224, 138) : Color.Gainsboro))
                        g.DrawString(text, f, b, tx, ty);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int hit = WorldMap.HitTest(e.X / _zoom, e.Y / _zoom);
                if (hit != _hover)
                {
                    _hover = hit;
                    Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                if (_hover >= 0) { _hover = -1; Invalidate(); }
                base.OnMouseLeave(e);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                int hit = WorldMap.HitTest(e.X / _zoom, e.Y / _zoom);
                if (hit >= 0 && OnPick != null) OnPick(hit);
                base.OnMouseDown(e);
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                // Client goc: bam lan hai vao cung mot diem = xac nhan di. Giu nguyen thoi quen do.
                int hit = WorldMap.HitTest(e.X / _zoom, e.Y / _zoom);
                if (hit >= 0 && hit == Selected && OnConfirm != null) OnConfirm();
                base.OnMouseDoubleClick(e);
            }
        }
    }
}
