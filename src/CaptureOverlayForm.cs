using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class CaptureOverlayForm : Form
    {
        private enum DragMode
        {
            None,
            Create,
            Move,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private const int HandleSize = 10;
        private const int MinSelectionSize = 8;

        private readonly Panel _toolbar;
        private Label _hintLabel;
        private Button _confirmButton;
        private Button _cancelButton;

        private Bitmap _snapshot;
        private Rectangle _snapshotBounds;
        private Rectangle _selection;
        private Rectangle _anchorSelection;
        private Point _dragStartScreen;
        private DragMode _dragMode;
        private double _aspectRatio;

        public event EventHandler<Bitmap> CaptureConfirmed;
        public event EventHandler CaptureCancelled;

        public CaptureOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Cursor = Cursors.Cross;
            Bounds = SystemInformation.VirtualScreen;
            DoubleBuffered = true;
            KeyPreview = true;

            _toolbar = BuildToolbar();
            Controls.Add(_toolbar);
            _toolbar.Visible = false;
        }

        public void BeginCapture(Bitmap snapshot, Rectangle snapshotBounds, AppLanguage language)
        {
            if (_snapshot != null)
            {
                _snapshot.Dispose();
            }

            _snapshot = snapshot;
            _snapshotBounds = snapshotBounds;
            _selection = Rectangle.Empty;
            _anchorSelection = Rectangle.Empty;
            _dragMode = DragMode.None;
            _aspectRatio = 1d;
            _toolbar.Visible = false;
            RefreshLanguage(language);
            Bounds = snapshotBounds;
            Cursor = Cursors.Cross;
            Show();
            Activate();
            Invalidate();
        }

        public void RefreshLanguage(AppLanguage language)
        {
            _hintLabel.Text = Localization.Get(language, "CaptureHint");
            _confirmButton.Text = Localization.Get(language, "ButtonConfirm");
            _cancelButton.Text = Localization.Get(language, "ButtonCancel");
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                base.OnMouseDown(e);
                return;
            }

            Point screenPoint = PointToScreen(e.Location);
            DragMode hitMode = HitTest(e.Location);

            if (_selection != Rectangle.Empty && hitMode != DragMode.None)
            {
                _dragMode = hitMode;
                _anchorSelection = _selection;
                _dragStartScreen = screenPoint;
                _aspectRatio = _selection.Height == 0 ? 1d : (double)_selection.Width / _selection.Height;
            }
            else
            {
                _dragMode = DragMode.Create;
                _dragStartScreen = screenPoint;
                _selection = new Rectangle(screenPoint.X, screenPoint.Y, 0, 0);
                _anchorSelection = _selection;
                _aspectRatio = 1d;
                _toolbar.Visible = false;
            }

            UpdateCursorForMode(_dragMode);
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point screenPoint = PointToScreen(e.Location);

            if (_dragMode == DragMode.None)
            {
                UpdateCursorForMode(HitTest(e.Location));
                base.OnMouseMove(e);
                return;
            }

            if (_dragMode == DragMode.Create)
            {
                _selection = RectangleFromPoints(_dragStartScreen, screenPoint);
            }
            else if (_dragMode == DragMode.Move)
            {
                _selection = MoveSelection(screenPoint);
            }
            else
            {
                _selection = ResizeSelection(screenPoint, (ModifierKeys & Keys.Shift) == Keys.Shift);
            }

            RepositionToolbar();
            Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_selection.Width >= MinSelectionSize && _selection.Height >= MinSelectionSize)
                {
                    _toolbar.Visible = true;
                    RepositionToolbar();
                }
                else
                {
                    _selection = Rectangle.Empty;
                    _toolbar.Visible = false;
                }

                _dragMode = DragMode.None;
                UpdateCursorForMode(HitTest(e.Location));
                Invalidate();
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left &&
                _selection != Rectangle.Empty &&
                ClientSelection.Contains(e.Location))
            {
                ConfirmCapture();
                return;
            }

            base.OnMouseDoubleClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                CancelCapture();
            }
            else if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return) && _selection.Width >= MinSelectionSize && _selection.Height >= MinSelectionSize)
            {
                ConfirmCapture();
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (_snapshot != null)
            {
                e.Graphics.DrawImage(_snapshot, ClientRectangle);
            }

            using (Brush overlayBrush = new SolidBrush(Color.FromArgb(92, 6, 23, 49)))
            {
                if (_selection == Rectangle.Empty)
                {
                    e.Graphics.FillRectangle(overlayBrush, ClientRectangle);
                }
                else
                {
                    Rectangle clientSelection = ClientSelection;
                    e.Graphics.FillRectangle(overlayBrush, 0, 0, Width, Math.Max(0, clientSelection.Top));
                    e.Graphics.FillRectangle(overlayBrush, 0, clientSelection.Top, Math.Max(0, clientSelection.Left), clientSelection.Height);
                    e.Graphics.FillRectangle(overlayBrush, clientSelection.Right, clientSelection.Top, Math.Max(0, Width - clientSelection.Right), clientSelection.Height);
                    e.Graphics.FillRectangle(overlayBrush, 0, clientSelection.Bottom, Width, Math.Max(0, Height - clientSelection.Bottom));
                }
            }

            if (_selection != Rectangle.Empty)
            {
                Rectangle clientSelection = InflateForPaint(ClientSelection, 0);
                using (Pen outerPen = new Pen(Color.White, 3f))
                {
                    outerPen.DashStyle = DashStyle.Dash;
                    outerPen.DashPattern = new float[] { 3f, 3f };
                    outerPen.Alignment = PenAlignment.Inset;
                    e.Graphics.DrawRectangle(outerPen, clientSelection);
                }

                using (Pen borderPen = new Pen(Color.FromArgb(37, 99, 235), 1.5f))
                {
                    borderPen.DashStyle = DashStyle.Dash;
                    borderPen.DashPattern = new float[] { 3f, 3f };
                    borderPen.Alignment = PenAlignment.Inset;
                    e.Graphics.DrawRectangle(borderPen, clientSelection);
                }

                foreach (Rectangle handle in GetHandleRects())
                {
                    Rectangle paintedHandle = InflateForPaint(handle, 0);
                    using (Brush fill = new SolidBrush(Color.FromArgb(59, 130, 246)))
                    using (Pen border = new Pen(Color.White, 2f))
                    {
                        e.Graphics.FillRectangle(fill, paintedHandle);
                        e.Graphics.DrawRectangle(border, paintedHandle);
                    }
                }
            }

            base.OnPaint(e);
        }

        private Panel BuildToolbar()
        {
            Panel panel = new Panel
            {
                Size = new Size(380, 74),
                BackColor = Color.FromArgb(19, 28, 46)
            };

            _hintLabel = new Label
            {
                AutoSize = false,
                Size = new Size(356, 28),
                Location = new Point(12, 8),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _confirmButton = BuildToolbarButton();
            _confirmButton.Location = new Point(196, 40);
            _confirmButton.Click += delegate { ConfirmCapture(); };

            _cancelButton = BuildToolbarButton();
            _cancelButton.Location = new Point(286, 40);
            _cancelButton.Click += delegate { CancelCapture(); };

            panel.Controls.Add(_hintLabel);
            panel.Controls.Add(_confirmButton);
            panel.Controls.Add(_cancelButton);
            return panel;
        }

        private Button BuildToolbarButton()
        {
            Button button = new Button
            {
                Size = new Size(82, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Rectangle ClientSelection
        {
            get
            {
                return new Rectangle(
                    _selection.X - Left,
                    _selection.Y - Top,
                    _selection.Width,
                    _selection.Height);
            }
        }

        private DragMode HitTest(Point clientPoint)
        {
            if (_selection == Rectangle.Empty)
            {
                return DragMode.None;
            }

            Rectangle[] handles = GetHandleRects();
            DragMode[] modes = new[]
            {
                DragMode.TopLeft,
                DragMode.Top,
                DragMode.TopRight,
                DragMode.Left,
                DragMode.Right,
                DragMode.BottomLeft,
                DragMode.Bottom,
                DragMode.BottomRight
            };

            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i].Contains(clientPoint))
                {
                    return modes[i];
                }
            }

            if (ClientSelection.Contains(clientPoint))
            {
                return DragMode.Move;
            }

            return DragMode.None;
        }

        private Rectangle[] GetHandleRects()
        {
            Rectangle rect = ClientSelection;
            int half = HandleSize / 2;
            int midX = rect.Left + rect.Width / 2;
            int midY = rect.Top + rect.Height / 2;

            return new[]
            {
                new Rectangle(rect.Left - half, rect.Top - half, HandleSize, HandleSize),
                new Rectangle(midX - half, rect.Top - half, HandleSize, HandleSize),
                new Rectangle(rect.Right - half, rect.Top - half, HandleSize, HandleSize),
                new Rectangle(rect.Left - half, midY - half, HandleSize, HandleSize),
                new Rectangle(rect.Right - half, midY - half, HandleSize, HandleSize),
                new Rectangle(rect.Left - half, rect.Bottom - half, HandleSize, HandleSize),
                new Rectangle(midX - half, rect.Bottom - half, HandleSize, HandleSize),
                new Rectangle(rect.Right - half, rect.Bottom - half, HandleSize, HandleSize)
            };
        }

        private static Rectangle InflateForPaint(Rectangle rect, int amount)
        {
            return new Rectangle(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);
        }

        private void UpdateCursorForMode(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.Move:
                    Cursor = Cursors.SizeAll;
                    break;
                case DragMode.Left:
                case DragMode.Right:
                    Cursor = Cursors.SizeWE;
                    break;
                case DragMode.Top:
                case DragMode.Bottom:
                    Cursor = Cursors.SizeNS;
                    break;
                case DragMode.TopLeft:
                case DragMode.BottomRight:
                    Cursor = Cursors.SizeNWSE;
                    break;
                case DragMode.TopRight:
                case DragMode.BottomLeft:
                    Cursor = Cursors.SizeNESW;
                    break;
                default:
                    Cursor = Cursors.Cross;
                    break;
            }
        }

        private Rectangle MoveSelection(Point screenPoint)
        {
            int dx = screenPoint.X - _dragStartScreen.X;
            int dy = screenPoint.Y - _dragStartScreen.Y;
            Rectangle moved = new Rectangle(
                _anchorSelection.X + dx,
                _anchorSelection.Y + dy,
                _anchorSelection.Width,
                _anchorSelection.Height);

            if (moved.Left < _snapshotBounds.Left)
            {
                moved.X = _snapshotBounds.Left;
            }
            if (moved.Top < _snapshotBounds.Top)
            {
                moved.Y = _snapshotBounds.Top;
            }
            if (moved.Right > _snapshotBounds.Right)
            {
                moved.X -= moved.Right - _snapshotBounds.Right;
            }
            if (moved.Bottom > _snapshotBounds.Bottom)
            {
                moved.Y -= moved.Bottom - _snapshotBounds.Bottom;
            }

            return moved;
        }

        private Rectangle ResizeSelection(Point screenPoint, bool keepRatio)
        {
            Rectangle rect = _anchorSelection;
            int left = rect.Left;
            int top = rect.Top;
            int right = rect.Right;
            int bottom = rect.Bottom;

            switch (_dragMode)
            {
                case DragMode.Left:
                case DragMode.TopLeft:
                case DragMode.BottomLeft:
                    left = screenPoint.X;
                    break;
                case DragMode.Right:
                case DragMode.TopRight:
                case DragMode.BottomRight:
                    right = screenPoint.X;
                    break;
            }

            switch (_dragMode)
            {
                case DragMode.Top:
                case DragMode.TopLeft:
                case DragMode.TopRight:
                    top = screenPoint.Y;
                    break;
                case DragMode.Bottom:
                case DragMode.BottomLeft:
                case DragMode.BottomRight:
                    bottom = screenPoint.Y;
                    break;
            }

            Rectangle resized = NormalizeRectangle(left, top, right, bottom);
            resized = EnforceMinimum(resized);

            if (keepRatio && _anchorSelection.Width > 0 && _anchorSelection.Height > 0)
            {
                resized = ApplyAspectRatio(resized);
            }

            return ClampToBounds(resized);
        }

        private Rectangle ApplyAspectRatio(Rectangle current)
        {
            double ratio = _aspectRatio <= 0.01d ? 1d : _aspectRatio;
            Rectangle anchor = _anchorSelection;
            int width = current.Width;
            int height = current.Height;

            if (_dragMode == DragMode.Left || _dragMode == DragMode.Right)
            {
                height = Math.Max(MinSelectionSize, (int)Math.Round(width / ratio));
                int centerY = anchor.Top + anchor.Height / 2;
                return NormalizeRectangle(current.Left, centerY - height / 2, current.Right, centerY + height / 2);
            }

            if (_dragMode == DragMode.Top || _dragMode == DragMode.Bottom)
            {
                width = Math.Max(MinSelectionSize, (int)Math.Round(height * ratio));
                int centerX = anchor.Left + anchor.Width / 2;
                return NormalizeRectangle(centerX - width / 2, current.Top, centerX + width / 2, current.Bottom);
            }

            if ((double)width / height > ratio)
            {
                width = Math.Max(MinSelectionSize, (int)Math.Round(height * ratio));
            }
            else
            {
                height = Math.Max(MinSelectionSize, (int)Math.Round(width / ratio));
            }

            switch (_dragMode)
            {
                case DragMode.TopLeft:
                    return NormalizeRectangle(anchor.Right - width, anchor.Bottom - height, anchor.Right, anchor.Bottom);
                case DragMode.TopRight:
                    return NormalizeRectangle(anchor.Left, anchor.Bottom - height, anchor.Left + width, anchor.Bottom);
                case DragMode.BottomLeft:
                    return NormalizeRectangle(anchor.Right - width, anchor.Top, anchor.Right, anchor.Top + height);
                default:
                    return NormalizeRectangle(anchor.Left, anchor.Top, anchor.Left + width, anchor.Top + height);
            }
        }

        private Rectangle EnforceMinimum(Rectangle rect)
        {
            if (rect.Width < MinSelectionSize)
            {
                rect.Width = MinSelectionSize;
            }
            if (rect.Height < MinSelectionSize)
            {
                rect.Height = MinSelectionSize;
            }
            return rect;
        }

        private Rectangle ClampToBounds(Rectangle rect)
        {
            if (rect.Left < _snapshotBounds.Left)
            {
                rect.X = _snapshotBounds.Left;
            }
            if (rect.Top < _snapshotBounds.Top)
            {
                rect.Y = _snapshotBounds.Top;
            }
            if (rect.Right > _snapshotBounds.Right)
            {
                rect.X -= rect.Right - _snapshotBounds.Right;
            }
            if (rect.Bottom > _snapshotBounds.Bottom)
            {
                rect.Y -= rect.Bottom - _snapshotBounds.Bottom;
            }

            rect.Width = Math.Min(rect.Width, _snapshotBounds.Width);
            rect.Height = Math.Min(rect.Height, _snapshotBounds.Height);
            return rect;
        }

        private Rectangle RectangleFromPoints(Point a, Point b)
        {
            return NormalizeRectangle(a.X, a.Y, b.X, b.Y);
        }

        private Rectangle NormalizeRectangle(int left, int top, int right, int bottom)
        {
            int normalizedLeft = Math.Min(left, right);
            int normalizedTop = Math.Min(top, bottom);
            int normalizedRight = Math.Max(left, right);
            int normalizedBottom = Math.Max(top, bottom);
            return Rectangle.FromLTRB(normalizedLeft, normalizedTop, normalizedRight, normalizedBottom);
        }

        private void RepositionToolbar()
        {
            if (_selection == Rectangle.Empty)
            {
                return;
            }

            Rectangle rect = ClientSelection;
            int x = Math.Max(12, Math.Min(rect.Right - _toolbar.Width, Width - _toolbar.Width - 12));
            int y = rect.Top - _toolbar.Height - 12;
            if (y < 12)
            {
                y = Math.Min(rect.Bottom + 12, Height - _toolbar.Height - 12);
            }
            _toolbar.Location = new Point(x, y);
        }

        private void ConfirmCapture()
        {
            if (_selection.Width < MinSelectionSize || _selection.Height < MinSelectionSize)
            {
                return;
            }

            Bitmap bmp = new Bitmap(_selection.Width, _selection.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(
                    _snapshot,
                    new Rectangle(0, 0, _selection.Width, _selection.Height),
                    new Rectangle(_selection.X - _snapshotBounds.X, _selection.Y - _snapshotBounds.Y, _selection.Width, _selection.Height),
                    GraphicsUnit.Pixel);
            }

            CleanupSnapshot();
            Hide();
            _toolbar.Visible = false;
            _selection = Rectangle.Empty;
            _dragMode = DragMode.None;

            EventHandler<Bitmap> handler = CaptureConfirmed;
            if (handler != null)
            {
                handler(this, bmp);
            }
        }

        private void CancelCapture()
        {
            CleanupSnapshot();
            Hide();
            _toolbar.Visible = false;
            _selection = Rectangle.Empty;
            _dragMode = DragMode.None;

            EventHandler handler = CaptureCancelled;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void CleanupSnapshot()
        {
            if (_snapshot != null)
            {
                _snapshot.Dispose();
                _snapshot = null;
            }
        }
    }
}
