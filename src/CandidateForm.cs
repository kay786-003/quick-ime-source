// ============================================================================
//  CandidateForm.cs - 候選字視窗
//  設計重點：
//    * WS_EX_NOACTIVATE + ShowWithoutActivation：永不被搶焦點
//    * 事件驅動重繪（唔用 timer 輪詢，減低 CPU）
//    * 跟隨文字插入點（用 GetGUIThreadInfo 取得真實 caret 位置）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickIME
{
    internal sealed class CandidateForm : Form
    {
        private static readonly Color BgColor = Color.FromArgb(252, 252, 252);
        private static readonly Color BorderColor = Color.FromArgb(150, 150, 150);
        private static readonly Color CodeColor = Color.FromArgb(80, 80, 80);
        private static readonly Color SelBack = Color.FromArgb(0, 120, 215);
        private static readonly Color TextColor = Color.FromArgb(20, 20, 20);
        private static readonly Color DimColor = Color.FromArgb(120, 120, 120);

        private const int CellW = 44;
        private const int CellH = 44;
        private const int CodeH = 24;
        private const int FootH = 18;
        private const int PadX = 7;
        private const int PadY = 3;

        private Composer _composer;
        private readonly Font _fontChar;
        private readonly Font _fontSmall;
        private readonly StringFormat _centered;

        public CandidateForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = BgColor;
            DoubleBuffered = true;
            Enabled = false;          // 不接受任何輸入
            Cursor = Cursors.Default;

            _fontChar = new Font("Microsoft JhengHei UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
            _fontSmall = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            _centered = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (滑鼠穿透)
                return cp;
            }
        }

        public void Attach(Composer composer)
        {
            _composer = composer;
            _composer.Changed += OnComposerChanged;
        }

        private void OnComposerChanged(object sender, EventArgs e)
        {
            Render();
        }

        /// <summary>依組字狀態重畫並重新定位。</summary>
        public void Render()
        {
            if (_composer == null || !_composer.IsComposing)
            {
                if (Visible) Hide();
                return;
            }

            IList<string> page = _composer.CurrentPage();
            if (page.Count == 0)
            {
                if (Visible) { Bounds = new Rectangle(Left, Top, Width, Height); Invalidate(); }
                else
                {
                    // 冇候選也要顯示提示，讓用家知道碼打錯了
                    Bounds = new Rectangle(Left, Top, 168, PadY * 2 + CodeH + CellH + FootH);
                    Show();
                }
                Invalidate();
                return;
            }

            int w = PadX * 2 + CellW * page.Count;
            int h = PadY * 2 + CodeH + CellH + FootH;

            int x, y;
            GetCaretPoint(out x, out y);
            Rectangle scr = Screen.FromPoint(new Point(x, y)).WorkingArea;
            if (x + w > scr.Right) x = scr.Right - w;
            if (y + h > scr.Bottom) y = y - h - 8;
            if (x < scr.Left) x = scr.Left;
            if (y < scr.Top) y = scr.Top;

            Bounds = new Rectangle(x, y, w, h);
            if (!Visible) Show();
            Invalidate();
        }

        // ------------------------------------------------------------------
        //  取得真實文字插入點（caret）
        // ------------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(int tid, ref GUITHREADINFO info);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

        [DllImport("user32.dll")]
        private static extern bool GetCaretPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private static void GetCaretPoint(out int x, out int y)
        {
            x = 200; y = 200;
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return;

                int tid = GetWindowThreadProcessId(fg, IntPtr.Zero);
                var gi = new GUITHREADINFO();
                gi.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
                if (GetGUIThreadInfo(tid, ref gi) && gi.hwndCaret != IntPtr.Zero)
                {
                    var pt = new POINT { X = gi.rcCaret.Left, Y = gi.rcCaret.Bottom };
                    if (ClientToScreen(gi.hwndCaret, ref pt))
                    {
                        x = pt.X;
                        y = pt.Y + 4;
                        return;
                    }
                }
            }
            catch { }

            // 退而求其次：用滑鼠位置
            try
            {
                POINT p;
                if (GetCursorPos(out p)) { x = p.X - 8; y = p.Y + 24; }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT pt);

        // ------------------------------------------------------------------
        //  繪製
        // ------------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.Default;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var bg = new SolidBrush(BgColor)) g.FillRectangle(bg, rect);
            using (var pen = new Pen(BorderColor)) g.DrawRectangle(pen, rect);

            if (_composer == null) return;

            string code = _composer.Code;
            string radicals = Dict.CodeToRadicals(code);
            string head = code.Length == 0 ? "" : code + "  " + radicals;

            using (var b = new SolidBrush(CodeColor))
                g.DrawString(head, _fontSmall, b, PadX + 1, PadY + 3);

            IList<string> page = _composer.CurrentPage();

            if (page.Count == 0)
            {
                using (var b = new SolidBrush(DimColor))
                    g.DrawString("（無此碼，請按退格重打）", _fontSmall, b,
                        PadX + 1, PadY + CodeH + CellH / 2 - 8);
                return;
            }

            for (int i = 0; i < page.Count; i++)
            {
                int cx = PadX + i * CellW;
                int cy = PadY + CodeH;
                bool sel = (i == 0);

                if (sel)
                {
                    using (var b = new SolidBrush(SelBack)) g.FillRectangle(b, cx, cy, CellW, CellH);
                }

                using (var b = new SolidBrush(sel ? Color.FromArgb(225, 235, 245) : DimColor))
                    g.DrawString((i + 1).ToString(), _fontSmall, b, cx + 3, cy + 1);

                using (var b = new SolidBrush(sel ? Color.White : TextColor))
                    g.DrawString(page[i], _fontChar, b,
                        new RectangleF(cx, cy, CellW, CellH), _centered);
            }

            // 底部資訊
            int total = _composer.TotalPages;
            string foot;
            if (total <= 1)
            {
                foot = _composer.Count + " 個候選";
            }
            else if (_composer.Page < total - 1)
            {
                foot = (_composer.Page + 1) + "/" + total + "   空格 下一頁";
            }
            else
            {
                foot = total + "/" + total + "   空格 選字第 1 個";
            }
            using (var b = new SolidBrush(DimColor))
                g.DrawString(foot, _fontSmall, b, PadX + 1, Height - FootH - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_composer != null) _composer.Changed -= OnComposerChanged;
                _fontChar.Dispose();
                _fontSmall.Dispose();
                _centered.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
