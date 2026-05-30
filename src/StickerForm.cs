using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class StickerForm : Form
    {
        private enum AnnotationTool
        {
            Pan,
            Rectangle,
            Circle,
            Freehand,
            Arrow,
            Text,
            Mosaic
        }

        private enum CanvasAction
        {
            None,
            Draw,
            Move,
            Resize,
            Pan
        }

        private enum ResizeHandle
        {
            None,
            TopLeft,
            Top,
            TopRight,
            Left,
            Right,
            BottomLeft,
            Bottom,
            BottomRight,
            ArrowStart,
            ArrowEnd
        }

        private const int StrokeWidth = 4;
        private const int MosaicBlockSize = 12;
        private const int HandleSize = 9;
        private const int ResizeBorderWidth = 8;
        private const float TextPathSizeScale = 1.3333f;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private readonly Bitmap _bitmap;
        private readonly string _saveDirectory;
        private readonly StickerCanvas _canvas;
        private readonly FlowLayoutPanel _topBar;
        private readonly FlowLayoutPanel _toolBar;
        private readonly Button _closeButton;
        private readonly Button _saveButton;
        private readonly Button _copyButton;
        private readonly IconToolButton _primaryColorButton;
        private readonly IconToolButton _outlineColorButton;
        private readonly FontSizeStepper _textSizeInput;
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly List<AnnotationItem> _items = new List<AnnotationItem>();
        private readonly Stack<List<AnnotationItem>> _undoStack = new Stack<List<AnnotationItem>>();
        private readonly Stack<List<AnnotationItem>> _redoStack = new Stack<List<AnnotationItem>>();

        private AnnotationTool _activeTool = AnnotationTool.Pan;
        private Color _primaryColor = Color.FromArgb(239, 68, 68);
        private Color _outlineColor = Color.Empty;
        private PointF _imagePan = PointF.Empty;
        private float _scale = 1f;
        private Point _dragOffset;
        private AppLanguage _language;

        public StickerForm(Bitmap bitmap, string saveDirectory, Icon icon, AppLanguage language)
        {
            _bitmap = bitmap;
            _saveDirectory = saveDirectory;
            _language = language;
            Icon = icon;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            KeyPreview = true;
            MinimumSize = new Size(430, 220);
            BackColor = Color.FromArgb(15, 23, 42);
            Padding = new Padding(10);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Padding = new Padding(6, 6, 6, 4),
                BackColor = Color.FromArgb(30, 41, 59)
            };

            _closeButton = new Button { AutoSize = true };
            _saveButton = new Button { AutoSize = true };
            _copyButton = new Button { AutoSize = true };
            _closeButton.Click += (s, e) => Close();
            _saveButton.Click += (s, e) => SaveImage();
            _copyButton.Click += (s, e) => CopyToClipboard();

            _topBar.Controls.Add(_closeButton);
            _topBar.Controls.Add(_saveButton);
            _topBar.Controls.Add(_copyButton);

            _toolBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 42,
                Padding = new Padding(6, 6, 6, 4),
                BackColor = Color.FromArgb(24, 34, 53),
                WrapContents = true
            };

            AddToolButton(AnnotationTool.Pan);
            AddToolButton(AnnotationTool.Rectangle);
            AddToolButton(AnnotationTool.Circle);
            AddToolButton(AnnotationTool.Freehand);
            AddToolButton(AnnotationTool.Arrow);
            AddToolButton(AnnotationTool.Text);
            AddToolButton(AnnotationTool.Mosaic);

            _primaryColorButton = BuildColorButton(ColorButtonKind.Primary, _primaryColor, delegate(Color c)
            {
                _primaryColor = c;
                ApplyColorToSelection(true, c);
            });
            _outlineColorButton = BuildColorButton(ColorButtonKind.Outline, _outlineColor, delegate(Color c)
            {
                _outlineColor = c;
                ApplyColorToSelection(false, c);
            });
            _toolBar.Controls.Add(_primaryColorButton);
            _toolBar.Controls.Add(_outlineColorButton);

            _toolBar.Controls.Add(new Label
            {
                Text = "T",
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Margin = new Padding(8, 7, 2, 0)
            });
            _textSizeInput = new FontSizeStepper
            {
                Minimum = 12,
                Maximum = 30,
                Value = 14,
                Margin = new Padding(0, 0, 6, 0)
            };
            _textSizeInput.ValueChanged += delegate
            {
                _canvas.ApplyTextSizeToSelection(_textSizeInput.Value);
            };
            _toolBar.Controls.Add(_textSizeInput);
            _toolBar.SizeChanged += (s, e) => UpdateToolBarHeight();

            _canvas = new StickerCanvas(this)
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                TabStop = true
            };

            Controls.Add(_canvas);
            Controls.Add(_toolBar);
            Controls.Add(_topBar);

            ApplyScale();
            UpdateWindowRegion();

            MouseDown += OnDragStart;
            MouseMove += OnDragMove;
            _topBar.MouseDown += OnDragStart;
            _topBar.MouseMove += OnDragMove;
            _toolBar.MouseDown += OnDragStart;
            _toolBar.MouseMove += OnDragMove;
            _canvas.MouseWheel += OnMouseWheelZoom;
            _canvas.MouseEnter += (s, e) =>
            {
                if (!_canvas.IsEditingText)
                {
                    _canvas.Focus();
                }
            };
            StyleActionButton(_closeButton, Color.FromArgb(239, 68, 68));
            StyleActionButton(_saveButton, Color.FromArgb(37, 99, 235));
            StyleActionButton(_copyButton, Color.FromArgb(14, 165, 233));
            RefreshLanguage(language);
        }

        public void RefreshLanguage(AppLanguage language)
        {
            _language = language;
            _closeButton.Text = Localization.Get(language, "ButtonClose");
            _saveButton.Text = Localization.Get(language, "ButtonSave");
            _copyButton.Text = Localization.Get(language, "ButtonCopy");
            SetToolTips(language);
        }

        public void CopyToClipboard()
        {
            using (Bitmap rendered = RenderComposite())
            {
                Clipboard.SetImage((Bitmap)rendered.Clone());
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && !_canvas.IsEditingText)
            {
                _canvas.DeleteSelection();
                e.Handled = true;
            }
            else if (!_canvas.IsEditingText && _canvas.MoveSelectionByKey(e.KeyCode, e.Shift ? 10f : 1f))
            {
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void AddToolButton(AnnotationTool tool)
        {
            IconToolButton button = new IconToolButton(tool)
            {
                Margin = new Padding(3, 0, 0, 0),
                Tag = tool
            };
            button.Click += delegate
            {
                _activeTool = (AnnotationTool)button.Tag;
                _canvas.Focus();
                foreach (Control control in _toolBar.Controls)
                {
                    IconToolButton toolButton = control as IconToolButton;
                    if (toolButton != null && toolButton.Tag is AnnotationTool)
                    {
                        toolButton.Checked = (AnnotationTool)toolButton.Tag == _activeTool;
                    }
                }
            };
            button.DoubleClick += delegate
            {
                if ((AnnotationTool)button.Tag == AnnotationTool.Pan)
                {
                    _imagePan = PointF.Empty;
                    _canvas.HandleImageScaleChanged();
                }
            };
            if (tool == _activeTool)
            {
                button.Checked = true;
            }
            _toolBar.Controls.Add(button);
        }

        private void SetToolTips(AppLanguage language)
        {
            foreach (Control control in _toolBar.Controls)
            {
                IconToolButton button = control as IconToolButton;
                if (button == null)
                {
                    continue;
                }

                if (button.Tag is AnnotationTool)
                {
                    _toolTip.SetToolTip(button, GetToolName(language, (AnnotationTool)button.Tag));
                }
                else if (button.Kind is ColorButtonKind)
                {
                    ColorButtonKind kind = (ColorButtonKind)button.Kind;
                    _toolTip.SetToolTip(button, language == AppLanguage.Chinese
                        ? (kind == ColorButtonKind.Primary ? "\u672c\u8eab\u989c\u8272" : "\u63cf\u8fb9\u989c\u8272")
                        : (kind == ColorButtonKind.Primary ? "Primary color" : "Outline color"));
                }
            }
            _toolTip.SetToolTip(_textSizeInput, language == AppLanguage.Chinese ? "\u6587\u5b57\u5927\u5c0f" : "Text size");
        }

        private static string GetToolName(AppLanguage language, AnnotationTool tool)
        {
            if (language == AppLanguage.Chinese)
            {
                switch (tool)
                {
                    case AnnotationTool.Pan: return "\u79fb\u52a8\u89c6\u56fe";
                    case AnnotationTool.Rectangle: return "\u65b9\u5f62";
                    case AnnotationTool.Circle: return "\u5706\u5f62";
                    case AnnotationTool.Freehand: return "\u624b\u7ed8\u5708";
                    case AnnotationTool.Arrow: return "\u7bad\u5934";
                    case AnnotationTool.Text: return "\u6587\u5b57";
                    case AnnotationTool.Mosaic: return "\u9a6c\u8d5b\u514b";
                }
            }
            switch (tool)
            {
                case AnnotationTool.Pan: return "Pan";
                case AnnotationTool.Rectangle: return "Rectangle";
                case AnnotationTool.Circle: return "Circle";
                case AnnotationTool.Freehand: return "Freehand";
                case AnnotationTool.Arrow: return "Arrow";
                case AnnotationTool.Text: return "Text";
                case AnnotationTool.Mosaic: return "Mosaic";
            }
            return string.Empty;
        }

        private IconToolButton BuildColorButton(ColorButtonKind kind, Color initialColor, Action<Color> onChanged)
        {
            IconToolButton button = new IconToolButton(kind)
            {
                SwatchColor = initialColor,
                Margin = new Padding(3, 0, 0, 0)
            };
            button.Click += delegate
            {
                ShowPalette(button, kind, onChanged);
                _canvas.Focus();
            };
            return button;
        }

        private void ShowPalette(IconToolButton ownerButton, ColorButtonKind kind, Action<Color> onChanged)
        {
            int swatchCount = kind == ColorButtonKind.Outline ? 10 : 9;
            int paletteWidth = swatchCount * 38 + 12;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(6);
            menu.AutoSize = false;
            menu.Width = paletteWidth + 12;
            menu.Height = 52;
            FlowLayoutPanel palette = new FlowLayoutPanel
            {
                AutoSize = false,
                Size = new Size(paletteWidth, 38),
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Color.White,
                WrapContents = false
            };
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(239, 68, 68));
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(249, 115, 22));
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(234, 179, 8));
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(34, 197, 94));
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(59, 130, 246));
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(168, 85, 247));
            AddPaletteButton(palette, ownerButton, onChanged, Color.FromArgb(107, 114, 128));
            AddPaletteButton(palette, ownerButton, onChanged, Color.Black);
            AddPaletteButton(palette, ownerButton, onChanged, Color.White);
            if (kind == ColorButtonKind.Outline)
            {
                AddPaletteButton(palette, ownerButton, onChanged, Color.Empty);
            }
            ToolStripControlHost host = new ToolStripControlHost(palette);
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            menu.Items.Add(host);
            menu.Show(ownerButton, new Point(0, ownerButton.Height));
        }

        private void AddPaletteButton(FlowLayoutPanel palette, IconToolButton ownerButton, Action<Color> onChanged, Color color)
        {
            PaletteColorButton item = new PaletteColorButton(color)
            {
                Margin = new Padding(5, 5, 5, 5)
            };
            item.Click += delegate
            {
                ownerButton.SwatchColor = color;
                onChanged(color);
            };
            palette.Controls.Add(item);
        }

        private void ApplyColorToSelection(bool primary, Color color)
        {
            if (_canvas.SelectedItems.Count == 0)
            {
                return;
            }
            bool changed = false;
            foreach (AnnotationItem item in _canvas.SelectedItems)
            {
                if (!primary && item is MosaicAnnotation)
                {
                    continue;
                }
                if (!changed)
                {
                    PushUndoState();
                    changed = true;
                }
                if (primary)
                {
                    item.SetPrimaryColor(color);
                }
                else
                {
                    item.SetOutlineColor(color);
                }
            }
            if (changed)
            {
                _canvas.Invalidate();
            }
        }

        private void OnDragStart(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragOffset = e.Location;
            }
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var screenPoint = PointToScreen(e.Location);
                Location = new Point(screenPoint.X - _dragOffset.X, screenPoint.Y - _dragOffset.Y);
            }
        }

        private void OnMouseWheelZoom(object sender, MouseEventArgs e)
        {
            _scale += e.Delta > 0 ? 0.1f : -0.1f;
            if (_scale < 0.2f) _scale = 0.2f;
            if (_scale > 3f) _scale = 3f;
            if (_canvas != null)
            {
                _canvas.HandleImageScaleChanged();
            }
        }

        private void ApplyScale()
        {
            Width = Math.Max(430, (int)(_bitmap.Width * _scale) + 20);
            Height = Math.Max(220, (int)(_bitmap.Height * _scale) + 102);
            UpdateToolBarHeight();
            UpdateWindowRegion();
        }

        private void UpdateToolBarHeight()
        {
            if (_toolBar == null)
            {
                return;
            }
            int rowHeight = 36;
            int rows = 1;
            int x = _toolBar.Padding.Left;
            int available = Math.Max(1, _toolBar.ClientSize.Width - _toolBar.Padding.Horizontal);
            foreach (Control control in _toolBar.Controls)
            {
                int width = control.Width + control.Margin.Horizontal;
                if (x > _toolBar.Padding.Left && x + width > available)
                {
                    rows++;
                    x = _toolBar.Padding.Left;
                }
                x += width;
            }
            _toolBar.Height = Math.Max(42, rows * rowHeight + _toolBar.Padding.Vertical);
        }

        private void StyleActionButton(Button button, Color backColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Margin = new Padding(6, 0, 0, 0);
            button.Padding = new Padding(8, 4, 8, 4);
            button.BackColor = backColor;
            button.ForeColor = Color.White;
            button.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
        }

        private void SaveImage()
        {
            Directory.CreateDirectory(_saveDirectory);
            using (var dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = _saveDirectory;
                dialog.FileName = "winscreen-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
                dialog.Filter = "PNG Image (*.png)|*.png";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    using (Bitmap rendered = RenderComposite())
                    {
                        rendered.Save(dialog.FileName, ImageFormat.Png);
                    }
                }
            }
        }

        public string SaveToDefaultDirectory()
        {
            Directory.CreateDirectory(_saveDirectory);
            string filePath = Path.Combine(_saveDirectory, "slsnap-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            using (Bitmap rendered = RenderComposite())
            {
                rendered.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        private Bitmap RenderComposite()
        {
            Bitmap rendered = new Bitmap(_bitmap.Width, _bitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(rendered))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImageUnscaled(_bitmap, 0, 0);
                foreach (AnnotationItem item in _items)
                {
                    item.Draw(g, 1f, Rectangle.Empty, false);
                }
            }
            return rendered;
        }

        private void PushUndoState()
        {
            _undoStack.Push(CloneItems(_items));
            _redoStack.Clear();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                return;
            }
            _redoStack.Push(CloneItems(_items));
            ReplaceItems(_undoStack.Pop());
        }

        private void Redo()
        {
            if (_redoStack.Count == 0)
            {
                return;
            }
            _undoStack.Push(CloneItems(_items));
            ReplaceItems(_redoStack.Pop());
        }

        private void ReplaceItems(List<AnnotationItem> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _canvas.ClearSelection();
            _canvas.Invalidate();
        }

        private static List<AnnotationItem> CloneItems(List<AnnotationItem> source)
        {
            List<AnnotationItem> clone = new List<AnnotationItem>();
            foreach (AnnotationItem item in source)
            {
                clone.Add(item.Clone());
            }
            return clone;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateToolBarHeight();
            if (_canvas != null)
            {
                _canvas.HandleImageScaleChanged();
            }
            UpdateWindowRegion();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                Point clientPoint = PointToClient(new Point((short)((long)m.LParam & 0xffff), (short)(((long)m.LParam >> 16) & 0xffff)));
                bool left = clientPoint.X <= ResizeBorderWidth;
                bool right = clientPoint.X >= ClientSize.Width - ResizeBorderWidth;
                bool top = clientPoint.Y <= ResizeBorderWidth;
                bool bottom = clientPoint.Y >= ClientSize.Height - ResizeBorderWidth;
                if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        private void UpdateWindowRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 18))
            {
                Region = new Region(path);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            int right = bounds.Right - 1;
            int bottom = bounds.Bottom - 1;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(right - diameter, bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRoundRectPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private enum ColorButtonKind
        {
            Primary,
            Outline
        }

        private sealed class IconToolButton : Control
        {
            private readonly object _kind;
            private bool _checked;
            private Color _swatchColor = Color.White;
            private bool _pressed;

            public IconToolButton(object kind)
            {
                _kind = kind;
                Size = new Size(32, 30);
                BackColor = Color.FromArgb(24, 34, 53);
                ForeColor = Color.White;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            }

            public bool Checked
            {
                get { return _checked; }
                set { _checked = value; Invalidate(); }
            }

            public Color SwatchColor
            {
                get { return _swatchColor; }
                set { _swatchColor = value; Invalidate(); }
            }

            public object Kind
            {
                get { return _kind; }
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                base.OnPaint(pevent);
                pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color background = _checked || _pressed ? Color.FromArgb(37, 99, 235) : Color.FromArgb(24, 34, 53);
                using (Brush brush = new SolidBrush(background))
                {
                    if (_checked || _pressed)
                    {
                        using (GraphicsPath path = CreateRoundRectPath(new Rectangle(2, 2, Width - 4, Height - 4), 4))
                        {
                            pevent.Graphics.FillPath(brush, path);
                        }
                    }
                    else
                    {
                        pevent.Graphics.FillRectangle(brush, ClientRectangle);
                    }
                }

                Rectangle rect = new Rectangle(7, 6, Width - 14, Height - 12);
                if (_kind is AnnotationTool)
                {
                    DrawToolIcon(pevent.Graphics, rect, (AnnotationTool)_kind);
                }
                else if (_kind is ColorButtonKind)
                {
                    DrawColorIcon(pevent.Graphics, rect, (ColorButtonKind)_kind);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    _pressed = true;
                    Focus();
                    Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_pressed && e.Button == MouseButtons.Left)
                {
                    _pressed = false;
                    Invalidate();
                    if (ClientRectangle.Contains(e.Location))
                    {
                        OnClick(EventArgs.Empty);
                    }
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_pressed)
                {
                    _pressed = false;
                    Invalidate();
                }
            }

            private void DrawToolIcon(Graphics g, Rectangle rect, AnnotationTool tool)
            {
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    switch (tool)
                    {
                        case AnnotationTool.Pan:
                            using (GraphicsPath hand = new GraphicsPath())
                            {
                                hand.AddLine(rect.Left + 5, rect.Top + 10, rect.Left + 5, rect.Top + 5);
                                hand.AddLine(rect.Left + 5, rect.Top + 5, rect.Left + 8, rect.Top + 5);
                                hand.AddLine(rect.Left + 8, rect.Top + 5, rect.Left + 8, rect.Top + 11);
                                hand.AddLine(rect.Left + 8, rect.Top + 6, rect.Left + 11, rect.Top + 6);
                                hand.AddLine(rect.Left + 11, rect.Top + 6, rect.Left + 11, rect.Top + 12);
                                hand.AddLine(rect.Left + 11, rect.Top + 8, rect.Left + 14, rect.Top + 8);
                                hand.AddLine(rect.Left + 14, rect.Top + 8, rect.Left + 14, rect.Top + 14);
                                hand.AddLine(rect.Left + 14, rect.Top + 10, rect.Left + 17, rect.Top + 10);
                                hand.AddLine(rect.Left + 17, rect.Top + 10, rect.Left + 17, rect.Top + 17);
                                hand.AddLine(rect.Left + 17, rect.Top + 17, rect.Left + 13, rect.Bottom - 1);
                                hand.AddLine(rect.Left + 13, rect.Bottom - 1, rect.Left + 8, rect.Bottom - 1);
                                hand.AddLine(rect.Left + 8, rect.Bottom - 1, rect.Left + 3, rect.Top + 14);
                                hand.AddLine(rect.Left + 3, rect.Top + 14, rect.Left + 5, rect.Top + 10);
                                hand.CloseFigure();
                                g.DrawPath(pen, hand);
                            }
                            break;
                        case AnnotationTool.Rectangle:
                            g.DrawRectangle(pen, rect);
                            break;
                        case AnnotationTool.Circle:
                            g.DrawEllipse(pen, rect);
                            break;
                        case AnnotationTool.Freehand:
                            g.DrawBezier(pen, rect.Left, rect.Bottom - 4, rect.Left + 4, rect.Top, rect.Right - 4, rect.Bottom, rect.Right, rect.Top + 4);
                            break;
                        case AnnotationTool.Arrow:
                            pen.CustomEndCap = new AdjustableArrowCap(4f, 5f);
                            g.DrawLine(pen, rect.Left, rect.Bottom, rect.Right, rect.Top);
                            break;
                        case AnnotationTool.Text:
                            using (Font font = new Font("Segoe UI", 12f, FontStyle.Bold))
                            {
                                TextRenderer.DrawText(g, "T", font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                            }
                            break;
                        case AnnotationTool.Mosaic:
                            for (int y = rect.Top; y < rect.Bottom; y += 6)
                            {
                                for (int x = rect.Left; x < rect.Right; x += 6)
                                {
                                    using (Brush brush = new SolidBrush(((x + y) / 6) % 2 == 0 ? Color.White : Color.FromArgb(156, 163, 175)))
                                    {
                                        g.FillRectangle(brush, x, y, 5, 5);
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            private void DrawColorIcon(Graphics g, Rectangle rect, ColorButtonKind kind)
            {
                using (Brush swatch = new SolidBrush(_swatchColor.IsEmpty ? Color.White : _swatchColor))
                using (Pen border = new Pen(Color.White, 2f))
                {
                    if (kind == ColorButtonKind.Primary)
                    {
                        g.FillEllipse(swatch, rect);
                        g.DrawEllipse(border, rect);
                    }
                    else
                    {
                        using (Pen swatchPen = new Pen(_swatchColor.IsEmpty ? Color.White : _swatchColor, 4f))
                        {
                            g.DrawEllipse(swatchPen, rect);
                        }
                        if (_swatchColor.IsEmpty)
                        {
                            g.DrawLine(Pens.Red, rect.Left, rect.Bottom, rect.Right, rect.Top);
                        }
                    }
                }
            }

        }

        private sealed class FontSizeStepper : Control
        {
            private float _value = 28f;

            public event EventHandler ValueChanged;

            public FontSizeStepper()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Size = new Size(58, 30);
                BackColor = Color.FromArgb(30, 41, 59);
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            }

            public float Minimum { get; set; }
            public float Maximum { get; set; }

            public float Value
            {
                get { return _value; }
                set
                {
                    float clamped = Math.Max(Minimum, Math.Min(Maximum, value));
                    if (Math.Abs(_value - clamped) < 0.001f)
                    {
                        return;
                    }
                    _value = clamped;
                    Invalidate();
                    EventHandler handler = ValueChanged;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 5))
                using (Brush fill = new SolidBrush(BackColor))
                using (Pen border = new Pen(Color.FromArgb(71, 85, 105)))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    ((int)Math.Round(_value)).ToString(),
                    Font,
                    new Rectangle(8, 0, Width - 24, Height),
                    ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 1.6f))
                {
                    Point upA = new Point(Width - 14, 9);
                    Point upB = new Point(Width - 9, 9);
                    Point upC = new Point(Width - 12, 6);
                    e.Graphics.DrawLines(pen, new[] { upA, upC, upB });

                    Point downA = new Point(Width - 14, 20);
                    Point downB = new Point(Width - 9, 20);
                    Point downC = new Point(Width - 12, 23);
                    e.Graphics.DrawLines(pen, new[] { downA, downC, downB });
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }
                if (e.X >= Width - 22)
                {
                    Value += e.Y < Height / 2 ? 1f : -1f;
                }
                Focus();
            }

            protected override bool IsInputKey(Keys keyData)
            {
                if (keyData == Keys.Up || keyData == Keys.Down)
                {
                    return true;
                }
                return base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Up)
                {
                    Value += 1f;
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    Value -= 1f;
                    e.Handled = true;
                }
                base.OnKeyDown(e);
            }
        }

        private sealed class PaletteColorButton : Control
        {
            private readonly Color _color;
            private bool _pressed;

            public PaletteColorButton(Color color)
            {
                _color = color;
                Size = new Size(28, 28);
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
                using (GraphicsPath path = CreateRoundRectPath(rect, 4))
                using (Brush fill = new SolidBrush(_color.IsEmpty ? Color.White : _color))
                using (Pen border = new Pen(Color.FromArgb(209, 213, 219)))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
                if (_color.IsEmpty)
                {
                    using (Pen slash = new Pen(Color.FromArgb(239, 68, 68), 2f))
                    {
                        e.Graphics.DrawLine(slash, 7, Height - 7, Width - 7, 7);
                    }
                }
                if (_pressed)
                {
                    using (Pen pen = new Pen(Color.FromArgb(37, 99, 235), 2f))
                    {
                        e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
                    }
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    _pressed = true;
                    Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_pressed && e.Button == MouseButtons.Left)
                {
                    _pressed = false;
                    Invalidate();
                    if (ClientRectangle.Contains(e.Location))
                    {
                        OnClick(EventArgs.Empty);
                    }
                }
            }
        }

        private sealed class StickerCanvas : Control
        {
            private readonly StickerForm _owner;
            private readonly List<AnnotationItem> _selectedItems = new List<AnnotationItem>();
            private readonly Dictionary<AnnotationItem, RectangleF> _resizeStartBounds = new Dictionary<AnnotationItem, RectangleF>();
            private readonly Dictionary<AnnotationItem, AnnotationItem> _resizeStartItems = new Dictionary<AnnotationItem, AnnotationItem>();
            private AnnotationItem _draftItem;
            private TextBox _textEditor;
            private TextAnnotation _editingText;
            private int _editingTextIndex = -1;
            private PointF _startImagePoint;
            private PointF _lastImagePoint;
            private Point _panStartClientPoint;
            private PointF _panStartOffset;
            private RectangleF _groupStartBounds;
            private CanvasAction _action = CanvasAction.None;
            private ResizeHandle _resizeHandle = ResizeHandle.None;
            private bool _historyPushed;

            public StickerCanvas(StickerForm owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            public IList<AnnotationItem> SelectedItems
            {
                get { return _selectedItems.AsReadOnly(); }
            }

            public bool IsEditingText
            {
                get { return _textEditor != null; }
            }

            public void HandleImageScaleChanged()
            {
                _owner._imagePan = ClampPan(_owner._imagePan);
                PositionTextEditor();
                Invalidate();
            }

            public void ClearSelection()
            {
                CommitTextEditor();
                _selectedItems.Clear();
            }

            public void DeleteSelection()
            {
                CommitTextEditor();
                if (_selectedItems.Count == 0)
                {
                    return;
                }
                _owner.PushUndoState();
                foreach (AnnotationItem item in new List<AnnotationItem>(_selectedItems))
                {
                    _owner._items.Remove(item);
                }
                _selectedItems.Clear();
                Invalidate();
            }

            public bool MoveSelectionByKey(Keys keyCode, float step)
            {
                if (_selectedItems.Count == 0)
                {
                    return false;
                }

                float dx = 0f;
                float dy = 0f;
                switch (keyCode)
                {
                    case Keys.Left:
                        dx = -step;
                        break;
                    case Keys.Right:
                        dx = step;
                        break;
                    case Keys.Up:
                        dy = -step;
                        break;
                    case Keys.Down:
                        dy = step;
                        break;
                    default:
                        return false;
                }

                _owner.PushUndoState();
                foreach (AnnotationItem item in _selectedItems)
                {
                    item.Offset(dx, dy);
                }
                PositionTextEditor();
                Invalidate();
                return true;
            }

            public void ApplyTextSizeToSelection(float fontSize)
            {
                bool changed = false;
                foreach (AnnotationItem item in _selectedItems)
                {
                    TextAnnotation text = item as TextAnnotation;
                    if (text != null && Math.Abs(text.FontSize - fontSize) > 0.01f)
                    {
                        if (!changed)
                        {
                            _owner.PushUndoState();
                            changed = true;
                        }
                        text.FontSize = fontSize;
                    }
                }
                if (_editingText != null)
                {
                    PositionTextEditor();
                }
                if (changed)
                {
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle imageRect = GetImageRectangle();
                e.Graphics.Clear(Color.FromArgb(229, 233, 240));
                if (imageRect != Rectangle.Empty)
                {
                    e.Graphics.DrawImage(_owner._bitmap, imageRect);
                }

                foreach (AnnotationItem item in _owner._items)
                {
                    if (ReferenceEquals(item, _editingText))
                    {
                        continue;
                    }
                    bool selected = _selectedItems.Contains(item);
                    item.Draw(e.Graphics, GetImageScale(imageRect), imageRect, selected);
                }
                if (_draftItem != null)
                {
                    _draftItem.Draw(e.Graphics, GetImageScale(imageRect), imageRect, false);
                }
                DrawSelectionHandles(e.Graphics, imageRect);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                if (e.Button != MouseButtons.Left)
                {
                    base.OnMouseDown(e);
                    return;
                }

                PointF imagePoint;
                if (!TryClientToImage(e.Location, out imagePoint))
                {
                    CommitTextEditor();
                    base.OnMouseDown(e);
                    return;
                }

                bool hadTextEditor = _textEditor != null;
                bool hadSelection = _selectedItems.Count > 0;
                CommitTextEditor();

                _startImagePoint = imagePoint;
                _lastImagePoint = imagePoint;
                _historyPushed = false;
                Capture = true;

                if (_owner._activeTool == AnnotationTool.Pan)
                {
                    _action = CanvasAction.Pan;
                    _panStartClientPoint = e.Location;
                    _panStartOffset = _owner._imagePan;
                    Cursor = Cursors.Hand;
                    _selectedItems.Clear();
                    Invalidate();
                    base.OnMouseDown(e);
                    return;
                }

                AnnotationItem handleItem;
                ResizeHandle handle = HitResizeHandle(imagePoint, out handleItem);
                if (handle != ResizeHandle.None)
                {
                    EnsureHistory();
                    if (handleItem != null && !_selectedItems.Contains(handleItem))
                    {
                        SelectSingle(handleItem);
                    }
                    BeginResize(handle);
                    Invalidate();
                    base.OnMouseDown(e);
                    return;
                }

                AnnotationItem hit = HitTest(imagePoint);
                bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
                if (hit != null)
                {
                    if (shift)
                    {
                        ToggleSelection(hit);
                    }
                    else if (!_selectedItems.Contains(hit))
                    {
                        SelectSingle(hit);
                    }
                    if (_selectedItems.Contains(hit))
                    {
                        EnsureHistory();
                        _action = CanvasAction.Move;
                        Cursor = Cursors.SizeAll;
                    }
                    Invalidate();
                    base.OnMouseDown(e);
                    return;
                }

                if (!shift)
                {
                    _selectedItems.Clear();
                }

                if (_owner._activeTool == AnnotationTool.Text && (hadTextEditor || hadSelection))
                {
                    _action = CanvasAction.None;
                    Capture = false;
                    Invalidate();
                    base.OnMouseDown(e);
                    return;
                }

                if (_owner._activeTool == AnnotationTool.Text)
                {
                    EnsureHistory();
                    TextAnnotation item = new TextAnnotation();
                    item.Location = imagePoint;
                    item.Text = string.Empty;
                    item.PrimaryColor = _owner._primaryColor;
                    item.OutlineColor = _owner._outlineColor;
                    item.FontSize = _owner._textSizeInput.Value;
                    item.BoxWidth = 220f;
                    item.BoxHeight = item.FontSize * 1.4f + 8f;
                    _owner._items.Add(item);
                    SelectSingle(item);
                    BeginEditText(item);
                    _action = CanvasAction.None;
                    Capture = false;
                    Invalidate();
                    base.OnMouseDown(e);
                    return;
                }

                EnsureHistory();
                _draftItem = CreateDraftItem(imagePoint);
                _action = CanvasAction.Draw;
                Invalidate();
                base.OnMouseDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                PointF imagePoint;
                if (_action == CanvasAction.Pan)
                {
                    _owner._imagePan = ClampPan(new PointF(
                        _panStartOffset.X + e.Location.X - _panStartClientPoint.X,
                        _panStartOffset.Y + e.Location.Y - _panStartClientPoint.Y));
                    PositionTextEditor();
                    Invalidate();
                    base.OnMouseMove(e);
                    return;
                }
                if (_action == CanvasAction.Move || _action == CanvasAction.Resize || _action == CanvasAction.Draw)
                {
                    imagePoint = ClientToImageClamped(e.Location);
                }
                else if (!TryClientToImage(e.Location, out imagePoint))
                {
                    base.OnMouseMove(e);
                    return;
                }

                if (_action == CanvasAction.Move)
                {
                    float dx = imagePoint.X - _lastImagePoint.X;
                    float dy = imagePoint.Y - _lastImagePoint.Y;
                    foreach (AnnotationItem item in _selectedItems)
                    {
                        item.Offset(dx, dy);
                    }
                    _lastImagePoint = imagePoint;
                    Invalidate();
                }
                else if (_action == CanvasAction.Resize)
                {
                    ApplyResize(imagePoint);
                    PositionTextEditor();
                    Invalidate();
                }
                else if (_action == CanvasAction.Draw && _draftItem != null)
                {
                    _draftItem.Update(_startImagePoint, imagePoint);
                    Invalidate();
                }
                else
                {
                    AnnotationItem handleItem;
                    ResizeHandle handle = HitResizeHandle(imagePoint, out handleItem);
                    if (handle != ResizeHandle.None)
                    {
                        Cursor = GetCursorForHandle(handle);
                    }
                    else
                    {
                        if (_owner._activeTool == AnnotationTool.Pan)
                        {
                            Cursor = Cursors.Hand;
                        }
                        else
                        {
                            Cursor = HitTest(imagePoint) == null ? Cursors.Cross : Cursors.SizeAll;
                        }
                    }
                }

                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (_action == CanvasAction.Draw && _draftItem != null)
                    {
                        if (_draftItem.HasContent)
                        {
                            _owner._items.Add(_draftItem);
                            SelectSingle(_draftItem);
                        }
                        else if (_historyPushed && _owner._undoStack.Count > 0)
                        {
                            _owner._undoStack.Pop();
                        }
                    }
                    _draftItem = null;
                    _resizeStartBounds.Clear();
                    _resizeStartItems.Clear();
                    _action = CanvasAction.None;
                    _resizeHandle = ResizeHandle.None;
                    _historyPushed = false;
                    Capture = false;
                    Cursor = Cursors.Default;
                    Invalidate();
                }
                base.OnMouseUp(e);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                PositionTextEditor();
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                PointF imagePoint;
                if (TryClientToImage(e.Location, out imagePoint))
                {
                    TextAnnotation text = HitTest(imagePoint) as TextAnnotation;
                    if (text != null)
                    {
                        SelectSingle(text);
                        BeginEditText(text);
                        Invalidate();
                        return;
                    }
                }
                base.OnMouseDoubleClick(e);
            }

            private void EnsureHistory()
            {
                if (!_historyPushed)
                {
                    _owner.PushUndoState();
                    _historyPushed = true;
                }
            }

            private void SelectSingle(AnnotationItem item)
            {
                _selectedItems.Clear();
                _selectedItems.Add(item);
            }

            private void ToggleSelection(AnnotationItem item)
            {
                if (_selectedItems.Contains(item))
                {
                    _selectedItems.Remove(item);
                }
                else
                {
                    _selectedItems.Add(item);
                }
            }

            private AnnotationItem CreateDraftItem(PointF point)
            {
                switch (_owner._activeTool)
                {
                    case AnnotationTool.Rectangle:
                        return new ShapeAnnotation(false, point, _owner._primaryColor, _owner._outlineColor);
                    case AnnotationTool.Circle:
                        return new ShapeAnnotation(true, point, _owner._primaryColor, _owner._outlineColor);
                    case AnnotationTool.Freehand:
                        return new FreehandAnnotation(point, _owner._primaryColor);
                    case AnnotationTool.Arrow:
                        return new ArrowAnnotation(point, _owner._primaryColor);
                    case AnnotationTool.Mosaic:
                        return new MosaicAnnotation(point, _owner._primaryColor);
                    default:
                        return new ShapeAnnotation(false, point, _owner._primaryColor, _owner._outlineColor);
                }
            }

            private AnnotationItem HitTest(PointF imagePoint)
            {
                for (int i = _owner._items.Count - 1; i >= 0; i--)
                {
                    if (_owner._items[i].HitTest(imagePoint))
                    {
                        return _owner._items[i];
                    }
                }
                return null;
            }

            private void BeginResize(ResizeHandle handle)
            {
                _resizeHandle = handle;
                _action = CanvasAction.Resize;
                _resizeStartBounds.Clear();
                _resizeStartItems.Clear();
                foreach (AnnotationItem item in _selectedItems)
                {
                    _resizeStartBounds[item] = item.GetBounds();
                    _resizeStartItems[item] = item.Clone();
                }
                _groupStartBounds = GetSelectionBounds();
                Cursor = GetCursorForHandle(handle);
            }

            private void ApplyResize(PointF imagePoint)
            {
                if (_selectedItems.Count == 1 && _selectedItems[0] is ArrowAnnotation)
                {
                    ArrowAnnotation arrow = (ArrowAnnotation)_selectedItems[0];
                    if (_resizeHandle == ResizeHandle.ArrowStart)
                    {
                        arrow.Start = imagePoint;
                    }
                    else if (_resizeHandle == ResizeHandle.ArrowEnd)
                    {
                        arrow.End = imagePoint;
                    }
                    return;
                }

                RectangleF target = ResizeBounds(_groupStartBounds, imagePoint, _resizeHandle);
                foreach (AnnotationItem item in _selectedItems)
                {
                    RectangleF startBounds = _resizeStartBounds[item];
                    RectangleF itemTarget = MapBounds(startBounds, _groupStartBounds, target);
                    item.CopyFrom(_resizeStartItems[item]);
                    item.Resize(startBounds, itemTarget);
                }
            }

            private RectangleF ResizeBounds(RectangleF bounds, PointF point, ResizeHandle handle)
            {
                float dx = point.X - _startImagePoint.X;
                float dy = point.Y - _startImagePoint.Y;
                float left = bounds.Left;
                float top = bounds.Top;
                float right = bounds.Right;
                float bottom = bounds.Bottom;
                switch (handle)
                {
                    case ResizeHandle.TopLeft:
                        left = bounds.Left + dx;
                        top = bounds.Top + dy;
                        break;
                    case ResizeHandle.Top:
                        top = bounds.Top + dy;
                        break;
                    case ResizeHandle.TopRight:
                        right = bounds.Right + dx;
                        top = bounds.Top + dy;
                        break;
                    case ResizeHandle.Left:
                        left = bounds.Left + dx;
                        break;
                    case ResizeHandle.Right:
                        right = bounds.Right + dx;
                        break;
                    case ResizeHandle.BottomLeft:
                        left = bounds.Left + dx;
                        bottom = bounds.Bottom + dy;
                        break;
                    case ResizeHandle.Bottom:
                        bottom = bounds.Bottom + dy;
                        break;
                    case ResizeHandle.BottomRight:
                        right = bounds.Right + dx;
                        bottom = bounds.Bottom + dy;
                        break;
                }
                if (right - left < 8f)
                {
                    if (handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomLeft)
                    {
                        left = right - 8f;
                    }
                    else
                    {
                        right = left + 8f;
                    }
                }
                if (bottom - top < 8f)
                {
                    if (handle == ResizeHandle.TopLeft || handle == ResizeHandle.TopRight)
                    {
                        top = bottom - 8f;
                    }
                    else
                    {
                        bottom = top + 8f;
                    }
                }
                if ((ModifierKeys & Keys.Shift) == Keys.Shift &&
                    handle != ResizeHandle.Left &&
                    handle != ResizeHandle.Right &&
                    handle != ResizeHandle.Top &&
                    handle != ResizeHandle.Bottom)
                {
                    float ratio = bounds.Height < 0.001f ? 1f : bounds.Width / bounds.Height;
                    float width = right - left;
                    float height = bottom - top;
                    if (height > 0.001f && width / height > ratio)
                    {
                        height = width / ratio;
                    }
                    else
                    {
                        width = height * ratio;
                    }

                    switch (handle)
                    {
                        case ResizeHandle.TopLeft:
                            left = right - width;
                            top = bottom - height;
                            break;
                        case ResizeHandle.TopRight:
                            right = left + width;
                            top = bottom - height;
                            break;
                        case ResizeHandle.BottomLeft:
                            left = right - width;
                            bottom = top + height;
                            break;
                        case ResizeHandle.BottomRight:
                            right = left + width;
                            bottom = top + height;
                            break;
                    }
                }
                return RectangleF.FromLTRB(left, top, right, bottom);
            }

            private RectangleF MapBounds(RectangleF itemStart, RectangleF groupStart, RectangleF groupTarget)
            {
                if (groupStart.Width < 0.001f || groupStart.Height < 0.001f)
                {
                    return groupTarget;
                }
                float scaleX = groupTarget.Width / groupStart.Width;
                float scaleY = groupTarget.Height / groupStart.Height;
                return new RectangleF(
                    groupTarget.Left + (itemStart.Left - groupStart.Left) * scaleX,
                    groupTarget.Top + (itemStart.Top - groupStart.Top) * scaleY,
                    itemStart.Width * scaleX,
                    itemStart.Height * scaleY);
            }

            private ResizeHandle HitResizeHandle(PointF imagePoint, out AnnotationItem handleItem)
            {
                handleItem = null;
                if (_selectedItems.Count == 1 && _selectedItems[0] is ArrowAnnotation)
                {
                    ArrowAnnotation arrow = (ArrowAnnotation)_selectedItems[0];
                    if (Distance(imagePoint, arrow.Start) <= 8f)
                    {
                        handleItem = arrow;
                        return ResizeHandle.ArrowStart;
                    }
                    if (Distance(imagePoint, arrow.End) <= 8f)
                    {
                        handleItem = arrow;
                        return ResizeHandle.ArrowEnd;
                    }
                }

                if (_selectedItems.Count == 0)
                {
                    return ResizeHandle.None;
                }

                RectangleF bounds = GetSelectionBounds();
                if (HitPoint(imagePoint, bounds.Left, bounds.Top)) return ResizeHandle.TopLeft;
                if (HitPoint(imagePoint, bounds.Left + bounds.Width / 2f, bounds.Top)) return ResizeHandle.Top;
                if (HitPoint(imagePoint, bounds.Right, bounds.Top)) return ResizeHandle.TopRight;
                if (HitPoint(imagePoint, bounds.Left, bounds.Top + bounds.Height / 2f)) return ResizeHandle.Left;
                if (HitPoint(imagePoint, bounds.Right, bounds.Top + bounds.Height / 2f)) return ResizeHandle.Right;
                if (HitPoint(imagePoint, bounds.Left, bounds.Bottom)) return ResizeHandle.BottomLeft;
                if (HitPoint(imagePoint, bounds.Left + bounds.Width / 2f, bounds.Bottom)) return ResizeHandle.Bottom;
                if (HitPoint(imagePoint, bounds.Right, bounds.Bottom)) return ResizeHandle.BottomRight;
                return ResizeHandle.None;
            }

            private bool HitPoint(PointF point, float x, float y)
            {
                return Math.Abs(point.X - x) <= 8f && Math.Abs(point.Y - y) <= 8f;
            }

            private RectangleF GetSelectionBounds()
            {
                if (_selectedItems.Count == 0)
                {
                    return RectangleF.Empty;
                }
                RectangleF bounds = _selectedItems[0].GetBounds();
                for (int i = 1; i < _selectedItems.Count; i++)
                {
                    bounds = RectangleF.Union(bounds, _selectedItems[i].GetBounds());
                }
                return bounds;
            }

            private void DrawSelectionHandles(Graphics g, Rectangle imageRect)
            {
                if (_selectedItems.Count == 0 || imageRect == Rectangle.Empty)
                {
                    return;
                }
                float scale = GetImageScale(imageRect);

                if (_selectedItems.Count == 1 && _selectedItems[0] is ArrowAnnotation)
                {
                    ArrowAnnotation arrow = (ArrowAnnotation)_selectedItems[0];
                    DrawHandle(g, AnnotationItem.ToClient(arrow.Start, scale, imageRect));
                    DrawHandle(g, AnnotationItem.ToClient(arrow.End, scale, imageRect));
                    return;
                }

                RectangleF bounds = AnnotationItem.ToClient(GetSelectionBounds(), scale, imageRect);
                DrawHandle(g, new PointF(bounds.Left, bounds.Top));
                DrawHandle(g, new PointF(bounds.Left + bounds.Width / 2f, bounds.Top));
                DrawHandle(g, new PointF(bounds.Right, bounds.Top));
                DrawHandle(g, new PointF(bounds.Left, bounds.Top + bounds.Height / 2f));
                DrawHandle(g, new PointF(bounds.Right, bounds.Top + bounds.Height / 2f));
                DrawHandle(g, new PointF(bounds.Left, bounds.Bottom));
                DrawHandle(g, new PointF(bounds.Left + bounds.Width / 2f, bounds.Bottom));
                DrawHandle(g, new PointF(bounds.Right, bounds.Bottom));
            }

            private void DrawHandle(Graphics g, PointF point)
            {
                RectangleF rect = new RectangleF(point.X - HandleSize / 2f, point.Y - HandleSize / 2f, HandleSize, HandleSize);
                using (Brush fill = new SolidBrush(Color.FromArgb(37, 99, 235)))
                using (Pen border = new Pen(Color.White, 1.5f))
                {
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }

            private void BeginEditText(TextAnnotation text)
            {
                CommitTextEditor();
                _editingText = text;
                _editingTextIndex = _owner._items.IndexOf(text);
                if (_editingTextIndex >= 0)
                {
                    _owner._items.RemoveAt(_editingTextIndex);
                }
                _textEditor = new TextBox
                {
                    Multiline = true,
                    WordWrap = true,
                    BorderStyle = BorderStyle.None,
                    Text = text.Text,
                    BackColor = SampleBitmapColor(text.Location),
                    ForeColor = text.PrimaryColor,
                    AcceptsReturn = true,
                    ScrollBars = ScrollBars.None
                };
                Rectangle initialImageRect = GetImageRectangle();
                float initialScale = GetImageScale(initialImageRect);
                _textEditor.Font = new Font("Microsoft YaHei UI", Math.Max(1f, text.FontSize * initialScale * 0.75f), FontStyle.Bold, GraphicsUnit.Point);
                _textEditor.TextChanged += delegate
                {
                    if (_editingText == null || _textEditor == null)
                    {
                        return;
                    }
                    _editingText.Text = _textEditor.Text;
                    AutoGrowTextEditor();
                    Invalidate();
                };
                Controls.Add(_textEditor);
                PositionTextEditor();
                _textEditor.Focus();
            }

            private void CommitTextEditor()
            {
                if (_textEditor == null)
                {
                    return;
                }
                TextBox editor = _textEditor;
                TextAnnotation text = _editingText;
                int insertIndex = _editingTextIndex;
                _textEditor = null;
                _editingText = null;
                _editingTextIndex = -1;
                if (text != null)
                {
                    text.Text = editor.Text;
                    if (string.IsNullOrWhiteSpace(text.Text))
                    {
                        _owner._items.Remove(text);
                        _selectedItems.Remove(text);
                    }
                    else if (!_owner._items.Contains(text))
                    {
                        if (insertIndex >= 0 && insertIndex <= _owner._items.Count)
                        {
                            _owner._items.Insert(insertIndex, text);
                        }
                        else
                        {
                            _owner._items.Add(text);
                        }
                    }
                }
                Controls.Remove(editor);
                editor.Dispose();
                Invalidate();
            }

            private void PositionTextEditor()
            {
                if (_textEditor == null || _editingText == null)
                {
                    return;
                }
                Rectangle imageRect = GetImageRectangle();
                float scale = GetImageScale(imageRect);
                RectangleF bounds = AnnotationItem.ToClient(_editingText.GetBounds(), scale, imageRect);
                _textEditor.BackColor = SampleBitmapColor(_editingText.Location);
                float pixelSize = Math.Max(1f, _editingText.FontSize * scale * TextPathSizeScale);
                if (_textEditor.Font == null || Math.Abs(_textEditor.Font.Size - pixelSize) > 0.1f || _textEditor.Font.Unit != GraphicsUnit.Pixel)
                {
                    Font oldFont = _textEditor.Font;
                    _textEditor.Font = new Font("Microsoft YaHei UI", pixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    if (oldFont != null)
                    {
                        oldFont.Dispose();
                    }
                }
                _textEditor.SetBounds((int)Math.Round(bounds.Left), (int)Math.Round(bounds.Top), Math.Max(32, (int)Math.Round(bounds.Width)), Math.Max(24, (int)Math.Round(bounds.Height)));
                AutoGrowTextEditor();
            }

            private Color SampleBitmapColor(PointF imagePoint)
            {
                int x = Math.Max(0, Math.Min(_owner._bitmap.Width - 1, (int)Math.Round(imagePoint.X)));
                int y = Math.Max(0, Math.Min(_owner._bitmap.Height - 1, (int)Math.Round(imagePoint.Y)));
                return _owner._bitmap.GetPixel(x, y);
            }

            private void AutoGrowTextEditor()
            {
                if (_textEditor == null || _editingText == null)
                {
                    return;
                }
                int textHeight = TextRenderer.MeasureText(
                    string.IsNullOrEmpty(_textEditor.Text) ? " " : _textEditor.Text,
                    _textEditor.Font,
                    new Size(Math.Max(20, _textEditor.Width - 6), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 8;
                if (textHeight > _textEditor.Height)
                {
                    _textEditor.Height = textHeight;
                    Rectangle imageRect = GetImageRectangle();
                    float scale = GetImageScale(imageRect);
                    _editingText.BoxHeight = _textEditor.Height / scale;
                }
            }

            private Cursor GetCursorForHandle(ResizeHandle handle)
            {
                switch (handle)
                {
                    case ResizeHandle.TopLeft:
                    case ResizeHandle.BottomRight:
                        return Cursors.SizeNWSE;
                    case ResizeHandle.TopRight:
                    case ResizeHandle.BottomLeft:
                        return Cursors.SizeNESW;
                    case ResizeHandle.Left:
                    case ResizeHandle.Right:
                        return Cursors.SizeWE;
                    case ResizeHandle.Top:
                    case ResizeHandle.Bottom:
                        return Cursors.SizeNS;
                    case ResizeHandle.ArrowStart:
                    case ResizeHandle.ArrowEnd:
                        return Cursors.Cross;
                    default:
                        return Cursors.Default;
                }
            }

            private Rectangle GetImageRectangle()
            {
                if (_owner._bitmap.Width <= 0 || _owner._bitmap.Height <= 0 || Width <= 0 || Height <= 0)
                {
                    return Rectangle.Empty;
                }

                int drawWidth = Math.Max(1, (int)(_owner._bitmap.Width * _owner._scale));
                int drawHeight = Math.Max(1, (int)(_owner._bitmap.Height * _owner._scale));
                PointF pan = _owner._imagePan;

                return new Rectangle(
                    (int)Math.Round((Width - drawWidth) / 2f + pan.X),
                    (int)Math.Round((Height - drawHeight) / 2f + pan.Y),
                    drawWidth,
                    drawHeight);
            }

            private PointF ClampPan(PointF pan)
            {
                if (_owner._bitmap.Width <= 0 || _owner._bitmap.Height <= 0 || Width <= 0 || Height <= 0)
                {
                    return PointF.Empty;
                }

                float drawWidth = Math.Max(1f, _owner._bitmap.Width * _owner._scale);
                float drawHeight = Math.Max(1f, _owner._bitmap.Height * _owner._scale);
                float keepVisible = Math.Min(80f, Math.Min(Width, Height) * 0.25f);
                keepVisible = Math.Max(16f, keepVisible);

                float centerX = (Width - drawWidth) / 2f;
                float centerY = (Height - drawHeight) / 2f;
                float minX = keepVisible - drawWidth - centerX;
                float maxX = Width - keepVisible - centerX;
                float minY = keepVisible - drawHeight - centerY;
                float maxY = Height - keepVisible - centerY;

                if (minX > maxX)
                {
                    pan.X = 0f;
                }
                else
                {
                    pan.X = Math.Max(minX, Math.Min(maxX, pan.X));
                }

                if (minY > maxY)
                {
                    pan.Y = 0f;
                }
                else
                {
                    pan.Y = Math.Max(minY, Math.Min(maxY, pan.Y));
                }

                return pan;
            }

            private float GetImageScale(Rectangle imageRect)
            {
                if (_owner._bitmap.Width == 0)
                {
                    return 1f;
                }
                return (float)imageRect.Width / _owner._bitmap.Width;
            }

            private bool TryClientToImage(Point clientPoint, out PointF imagePoint)
            {
                Rectangle imageRect = GetImageRectangle();
                if (imageRect == Rectangle.Empty || !imageRect.Contains(clientPoint))
                {
                    imagePoint = PointF.Empty;
                    return false;
                }

                float scale = GetImageScale(imageRect);
                imagePoint = new PointF(
                    (clientPoint.X - imageRect.X) / scale,
                    (clientPoint.Y - imageRect.Y) / scale);
                return true;
            }

            private PointF ClientToImageClamped(Point clientPoint)
            {
                Rectangle imageRect = GetImageRectangle();
                int x = Math.Max(imageRect.Left, Math.Min(imageRect.Right, clientPoint.X));
                int y = Math.Max(imageRect.Top, Math.Min(imageRect.Bottom, clientPoint.Y));
                float scale = GetImageScale(imageRect);
                return new PointF(
                    (x - imageRect.X) / scale,
                    (y - imageRect.Y) / scale);
            }

            private static float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }

        }

        private abstract class AnnotationItem
        {
            public abstract bool HasContent { get; }
            public abstract void Update(PointF start, PointF current);
            public abstract void Offset(float dx, float dy);
            public abstract bool HitTest(PointF imagePoint);
            public abstract RectangleF GetBounds();
            public abstract void Resize(RectangleF originalBounds, RectangleF newBounds);
            public abstract void Draw(Graphics g, float scale, Rectangle imageRect, bool selected);
            public abstract AnnotationItem Clone();
            public abstract void CopyFrom(AnnotationItem source);

            public virtual void SetPrimaryColor(Color color)
            {
            }

            public virtual void SetOutlineColor(Color color)
            {
            }

            public static RectangleF Normalize(float left, float top, float right, float bottom)
            {
                return RectangleF.FromLTRB(
                    Math.Min(left, right),
                    Math.Min(top, bottom),
                    Math.Max(left, right),
                    Math.Max(top, bottom));
            }

            protected static RectangleF Normalize(PointF a, PointF b)
            {
                return Normalize(a.X, a.Y, b.X, b.Y);
            }

            public static RectangleF ToClient(RectangleF rect, float scale, Rectangle imageRect)
            {
                return new RectangleF(
                    imageRect.X + rect.X * scale,
                    imageRect.Y + rect.Y * scale,
                    rect.Width * scale,
                    rect.Height * scale);
            }

            public static PointF ToClient(PointF point, float scale, Rectangle imageRect)
            {
                return new PointF(imageRect.X + point.X * scale, imageRect.Y + point.Y * scale);
            }

            protected static PointF MapPoint(PointF point, RectangleF source, RectangleF target)
            {
                float xRatio = source.Width < 0.001f ? 0f : (point.X - source.Left) / source.Width;
                float yRatio = source.Height < 0.001f ? 0f : (point.Y - source.Top) / source.Height;
                return new PointF(target.Left + xRatio * target.Width, target.Top + yRatio * target.Height);
            }

            protected static void DrawSelection(Graphics g, RectangleF rect)
            {
                using (Pen pen = new Pen(Color.FromArgb(37, 99, 235), 1f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        private sealed class ShapeAnnotation : AnnotationItem
        {
            private readonly bool _circle;
            private PointF _start;
            private PointF _end;
            private Color _primaryColor;
            private Color _outlineColor;

            public ShapeAnnotation(bool circle, PointF start, Color primaryColor, Color outlineColor)
            {
                _circle = circle;
                _start = start;
                _end = start;
                _primaryColor = primaryColor;
                _outlineColor = outlineColor;
            }

            public override bool HasContent
            {
                get { return GetBounds().Width > 4f && GetBounds().Height > 4f; }
            }

            public override void Update(PointF start, PointF current)
            {
                _start = start;
                if (_circle)
                {
                    float dx = current.X - start.X;
                    float dy = current.Y - start.Y;
                    float side = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    _end = new PointF(start.X + Math.Sign(dx == 0 ? 1f : dx) * side, start.Y + Math.Sign(dy == 0 ? 1f : dy) * side);
                }
                else
                {
                    _end = current;
                }
            }

            public override void Offset(float dx, float dy)
            {
                _start = new PointF(_start.X + dx, _start.Y + dy);
                _end = new PointF(_end.X + dx, _end.Y + dy);
            }

            public override bool HitTest(PointF imagePoint)
            {
                return GetBounds().Contains(imagePoint);
            }

            public override RectangleF GetBounds()
            {
                return Normalize(_start, _end);
            }

            public override void Resize(RectangleF originalBounds, RectangleF newBounds)
            {
                _start = new PointF(newBounds.Left, newBounds.Top);
                _end = new PointF(newBounds.Right, newBounds.Bottom);
            }

            public override void SetPrimaryColor(Color color)
            {
                _primaryColor = color;
            }

            public override void SetOutlineColor(Color color)
            {
                _outlineColor = color;
            }

            public override void Draw(Graphics g, float scale, Rectangle imageRect, bool selected)
            {
                RectangleF rect = GetBounds();
                RectangleF drawRect = imageRect == Rectangle.Empty ? rect : ToClient(rect, scale, imageRect);
                using (Pen pen = new Pen(_primaryColor, Math.Max(1f, StrokeWidth * scale)))
                {
                    if (_circle)
                    {
                        if (!_outlineColor.IsEmpty)
                        {
                            using (Pen shadow = new Pen(_outlineColor, Math.Max(1f, (StrokeWidth + 3) * scale)))
                            {
                                g.DrawEllipse(shadow, drawRect);
                            }
                        }
                        g.DrawEllipse(pen, drawRect);
                    }
                    else
                    {
                        if (!_outlineColor.IsEmpty)
                        {
                            using (Pen shadow = new Pen(_outlineColor, Math.Max(1f, (StrokeWidth + 3) * scale)))
                            {
                                g.DrawRectangle(shadow, drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);
                            }
                        }
                        g.DrawRectangle(pen, drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);
                    }
                }
                if (selected && imageRect != Rectangle.Empty)
                {
                    DrawSelection(g, drawRect);
                }
            }

            public override AnnotationItem Clone()
            {
                ShapeAnnotation clone = new ShapeAnnotation(_circle, _start, _primaryColor, _outlineColor);
                clone._end = _end;
                return clone;
            }

            public override void CopyFrom(AnnotationItem source)
            {
                ShapeAnnotation other = (ShapeAnnotation)source;
                _start = other._start;
                _end = other._end;
                _primaryColor = other._primaryColor;
                _outlineColor = other._outlineColor;
            }
        }

        private sealed class ArrowAnnotation : AnnotationItem
        {
            private PointF _start;
            private PointF _end;
            private Color _color;

            public ArrowAnnotation(PointF start, Color color)
            {
                _start = start;
                _end = start;
                _color = color;
            }

            public PointF Start
            {
                get { return _start; }
                set { _start = value; }
            }

            public PointF End
            {
                get { return _end; }
                set { _end = value; }
            }

            public override bool HasContent
            {
                get { return Distance(_start, _end) > 8f; }
            }

            public override void Update(PointF start, PointF current)
            {
                _start = start;
                _end = current;
            }

            public override void Offset(float dx, float dy)
            {
                _start = new PointF(_start.X + dx, _start.Y + dy);
                _end = new PointF(_end.X + dx, _end.Y + dy);
            }

            public override bool HitTest(PointF imagePoint)
            {
                RectangleF bounds = GetBounds();
                bounds.Inflate(8f, 8f);
                return bounds.Contains(imagePoint);
            }

            public override RectangleF GetBounds()
            {
                return Normalize(_start, _end);
            }

            public override void Resize(RectangleF originalBounds, RectangleF newBounds)
            {
                _start = MapPoint(_start, originalBounds, newBounds);
                _end = MapPoint(_end, originalBounds, newBounds);
            }

            public override void SetPrimaryColor(Color color)
            {
                _color = color;
            }

            public override void SetOutlineColor(Color color)
            {
            }

            public override void Draw(Graphics g, float scale, Rectangle imageRect, bool selected)
            {
                PointF start = imageRect == Rectangle.Empty ? _start : ToClient(_start, scale, imageRect);
                PointF end = imageRect == Rectangle.Empty ? _end : ToClient(_end, scale, imageRect);
                using (Pen pen = new Pen(_color, Math.Max(1f, StrokeWidth * scale)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Custom;
                    pen.CustomEndCap = new AdjustableArrowCap(5f * scale, 6f * scale);
                    g.DrawLine(pen, start, end);
                }
                if (selected && imageRect != Rectangle.Empty)
                {
                    DrawSelection(g, Normalize(start, end));
                }
            }

            public override AnnotationItem Clone()
            {
                ArrowAnnotation clone = new ArrowAnnotation(_start, _color);
                clone._end = _end;
                return clone;
            }

            public override void CopyFrom(AnnotationItem source)
            {
                ArrowAnnotation other = (ArrowAnnotation)source;
                _start = other._start;
                _end = other._end;
                _color = other._color;
            }

            private static float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        private sealed class FreehandAnnotation : AnnotationItem
        {
            private readonly List<PointF> _points = new List<PointF>();
            private Color _color;

            public FreehandAnnotation(PointF start, Color color)
            {
                _points.Add(start);
                _color = color;
            }

            public override bool HasContent
            {
                get { return _points.Count > 2; }
            }

            public override void Update(PointF start, PointF current)
            {
                if (_points.Count == 0)
                {
                    _points.Add(start);
                }
                PointF last = _points[_points.Count - 1];
                if (Math.Abs(last.X - current.X) + Math.Abs(last.Y - current.Y) > 2f)
                {
                    _points.Add(current);
                }
            }

            public override void Offset(float dx, float dy)
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    _points[i] = new PointF(_points[i].X + dx, _points[i].Y + dy);
                }
            }

            public override bool HitTest(PointF imagePoint)
            {
                RectangleF bounds = GetBounds();
                bounds.Inflate(8f, 8f);
                return bounds.Contains(imagePoint);
            }

            public override RectangleF GetBounds()
            {
                float left = _points[0].X;
                float top = _points[0].Y;
                float right = _points[0].X;
                float bottom = _points[0].Y;
                foreach (PointF point in _points)
                {
                    left = Math.Min(left, point.X);
                    top = Math.Min(top, point.Y);
                    right = Math.Max(right, point.X);
                    bottom = Math.Max(bottom, point.Y);
                }
                return RectangleF.FromLTRB(left, top, right, bottom);
            }

            public override void Resize(RectangleF originalBounds, RectangleF newBounds)
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    _points[i] = MapPoint(_points[i], originalBounds, newBounds);
                }
            }

            public override void SetPrimaryColor(Color color)
            {
                _color = color;
            }

            public override void SetOutlineColor(Color color)
            {
            }

            public override void Draw(Graphics g, float scale, Rectangle imageRect, bool selected)
            {
                if (_points.Count < 2)
                {
                    return;
                }

                PointF[] points = new PointF[_points.Count];
                for (int i = 0; i < _points.Count; i++)
                {
                    points[i] = imageRect == Rectangle.Empty ? _points[i] : ToClient(_points[i], scale, imageRect);
                }
                using (Pen pen = new Pen(_color, Math.Max(1f, StrokeWidth * scale)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, points);
                }
                if (selected && imageRect != Rectangle.Empty)
                {
                    DrawSelection(g, ToClient(GetBounds(), scale, imageRect));
                }
            }

            public override AnnotationItem Clone()
            {
                FreehandAnnotation clone = new FreehandAnnotation(_points[0], _color);
                clone._points.Clear();
                clone._points.AddRange(_points);
                return clone;
            }

            public override void CopyFrom(AnnotationItem source)
            {
                FreehandAnnotation other = (FreehandAnnotation)source;
                _points.Clear();
                _points.AddRange(other._points);
                _color = other._color;
            }
        }

        private sealed class TextAnnotation : AnnotationItem
        {
            public PointF Location;
            public string Text;
            public float FontSize;
            public float BoxWidth;
            public float BoxHeight;
            public Color PrimaryColor;
            public Color OutlineColor;

            public override bool HasContent
            {
                get { return !string.IsNullOrEmpty(Text); }
            }

            public override void Update(PointF start, PointF current)
            {
                Location = current;
            }

            public override void Offset(float dx, float dy)
            {
                Location = new PointF(Location.X + dx, Location.Y + dy);
            }

            public override bool HitTest(PointF imagePoint)
            {
                return GetBounds().Contains(imagePoint);
            }

            public override RectangleF GetBounds()
            {
                return new RectangleF(Location.X, Location.Y, Math.Max(40f, BoxWidth), Math.Max(FontSize * 1.4f + 8f, BoxHeight));
            }

            public override void Resize(RectangleF originalBounds, RectangleF newBounds)
            {
                Location = new PointF(newBounds.Left, newBounds.Top);
                BoxWidth = Math.Max(40f, newBounds.Width);
                BoxHeight = Math.Max(FontSize * 1.4f + 8f, newBounds.Height);
            }

            public void AutoGrowHeight(Graphics graphics)
            {
                if (graphics == null)
                {
                    return;
                }
                using (Font font = new Font("Microsoft YaHei UI", Math.Max(1f, FontSize * TextPathSizeScale), FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    Size proposed = new Size(Math.Max(20, (int)BoxWidth), int.MaxValue);
                    Size measured = TextRenderer.MeasureText(
                        string.IsNullOrEmpty(Text) ? " " : Text,
                        font,
                        proposed,
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    BoxHeight = Math.Max(BoxHeight, measured.Height + 8);
                }
            }

            public override void SetPrimaryColor(Color color)
            {
                PrimaryColor = color;
            }

            public override void SetOutlineColor(Color color)
            {
                OutlineColor = color;
            }

            public override void Draw(Graphics g, float scale, Rectangle imageRect, bool selected)
            {
                float size = Math.Max(1f, FontSize * scale * TextPathSizeScale);
                RectangleF bounds = imageRect == Rectangle.Empty ? GetBounds() : ToClient(GetBounds(), scale, imageRect);
                using (FontFamily family = new FontFamily("Microsoft YaHei UI"))
                using (GraphicsPath path = new GraphicsPath())
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Near;
                    format.LineAlignment = StringAlignment.Near;
                    format.Trimming = StringTrimming.None;
                    path.AddString(Text, family, (int)FontStyle.Bold, size, bounds, format);
                    if (!OutlineColor.IsEmpty)
                    {
                        using (Pen outline = new Pen(OutlineColor, Math.Max(1f, 3f * scale)))
                        {
                            outline.LineJoin = LineJoin.Round;
                            g.DrawPath(outline, path);
                        }
                    }
                    using (Brush fill = new SolidBrush(PrimaryColor))
                    {
                        g.FillPath(fill, path);
                    }
                    if (selected && imageRect != Rectangle.Empty)
                    {
                        DrawSelection(g, bounds);
                    }
                }
            }

            public override AnnotationItem Clone()
            {
                return new TextAnnotation
                {
                    Location = Location,
                    Text = Text,
                    FontSize = FontSize,
                    BoxWidth = BoxWidth,
                    BoxHeight = BoxHeight,
                    PrimaryColor = PrimaryColor,
                    OutlineColor = OutlineColor
                };
            }

            public override void CopyFrom(AnnotationItem source)
            {
                TextAnnotation other = (TextAnnotation)source;
                Location = other.Location;
                Text = other.Text;
                FontSize = other.FontSize;
                BoxWidth = other.BoxWidth;
                BoxHeight = other.BoxHeight;
                PrimaryColor = other.PrimaryColor;
                OutlineColor = other.OutlineColor;
            }
        }

        private sealed class MosaicAnnotation : AnnotationItem
        {
            private PointF _start;
            private PointF _end;
            private Color _color;

            public MosaicAnnotation(PointF start, Color color)
            {
                _start = start;
                _end = start;
                _color = color;
            }

            public override bool HasContent
            {
                get { return GetBounds().Width > 6f && GetBounds().Height > 6f; }
            }

            public override void Update(PointF start, PointF current)
            {
                _start = start;
                _end = current;
            }

            public override void Offset(float dx, float dy)
            {
                _start = new PointF(_start.X + dx, _start.Y + dy);
                _end = new PointF(_end.X + dx, _end.Y + dy);
            }

            public override bool HitTest(PointF imagePoint)
            {
                return GetBounds().Contains(imagePoint);
            }

            public override RectangleF GetBounds()
            {
                return Normalize(_start, _end);
            }

            public override void Resize(RectangleF originalBounds, RectangleF newBounds)
            {
                _start = new PointF(newBounds.Left, newBounds.Top);
                _end = new PointF(newBounds.Right, newBounds.Bottom);
            }

            public override void SetPrimaryColor(Color color)
            {
                _color = color;
            }

            public override void SetOutlineColor(Color color)
            {
            }

            public override void Draw(Graphics g, float scale, Rectangle imageRect, bool selected)
            {
                RectangleF rect = GetBounds();
                RectangleF drawRect = imageRect == Rectangle.Empty ? rect : ToClient(rect, scale, imageRect);
                float block = Math.Max(3f, MosaicBlockSize * scale);
                for (float y = drawRect.Top; y < drawRect.Bottom; y += block)
                {
                    for (float x = drawRect.Left; x < drawRect.Right; x += block)
                    {
                        Color blockColor = (((int)((x + y) / block)) % 2 == 0)
                            ? ControlPaint.Light(_color, 0.35f)
                            : _color;
                        using (Brush brush = new SolidBrush(blockColor))
                        {
                            g.FillRectangle(brush, x, y, Math.Min(block, drawRect.Right - x), Math.Min(block, drawRect.Bottom - y));
                        }
                    }
                }
                using (Pen pen = new Pen(_color, Math.Max(1f, scale)))
                {
                    g.DrawRectangle(pen, drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);
                }
                if (selected && imageRect != Rectangle.Empty)
                {
                    DrawSelection(g, drawRect);
                }
            }

            public override AnnotationItem Clone()
            {
                MosaicAnnotation clone = new MosaicAnnotation(_start, _color);
                clone._end = _end;
                return clone;
            }

            public override void CopyFrom(AnnotationItem source)
            {
                MosaicAnnotation other = (MosaicAnnotation)source;
                _start = other._start;
                _end = other._end;
                _color = other._color;
            }
        }
    }
}
