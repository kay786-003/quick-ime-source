// ============================================================================
//  CandidateForm.cs - 候選字視窗
//  設計重點：
//    * WS_EX_NOACTIVATE + ShowWithoutActivation：永不被搶焦點
//    * 事件驅動重繪（唔用 timer 輪詢，減低 CPU）
//    * 跟隨文字插入點（用 GetGUIThreadInfo 取得真實 caret 位置）
//    * 同一個視窗兼任「聯想字」顯示（打完字之後列出下一個字）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickIME
{
    /// <summary>視窗目前正在顯示什麼。</summary>
    internal enum CandidateMode
    {
        /// <summary>速成組字中的候選字。</summary>
        Compose = 0,
        /// <summary>上一個字之後的聯想字。</summary>
        Associate
    }

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

        /// <summary>聯想字最多顯示幾個（與 Assoc.MaxPerChar 無關，純粹係畫面闊度）。</summary>
        public const int AssocPerPage = 9;

        private Composer _composer;
        private readonly Font _fontChar;
        private readonly Font _fontSmall;
        private readonly StringFormat _centered;

        // ---- 聯想字狀態（由 KeyboardHook 餵入）----
        private CandidateMode _mode = CandidateMode.Compose;
        private string _assocPrev;                 // 上一個上屏的字
        private string _assocPending = "";         // 已打的聯想前綴（用括號顯示）
        private IList<string> _assocItems = new List<string>();

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

            // WS_EX_NOACTIVATE 只在 Form 未建立前設定才有效（CreateParams 已加），
            // 另外用 ShowWithoutActivation 保證 Show() 都唔會搶焦點。
            SetStyle(ControlStyles.Selectable, false);

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

        /// <summary>目前顯示模式（給測試 / 日誌用）。</summary>
        public CandidateMode Mode { get { return _mode; } }

        public void Attach(Composer composer)
        {
            _composer = composer;
            _composer.Changed += OnComposerChanged;
        }

        private void OnComposerChanged(object sender, EventArgs e)
        {
            // 組字一起，就即刻切返去候選字模式（聯想字自動讓位）
            if (_composer != null && _composer.IsComposing) ClearAssoc();
            Render();
        }

        // ------------------------------------------------------------------
        //  聯想字 API（由 KeyboardHook 使用）
        // ------------------------------------------------------------------
        /// <summary>顯示聯想字。<paramref name="prev"/> = 上一個上屏的字。</summary>
        public void ShowAssoc(string prev, string pending, IList<string> items)
        {
            _mode = CandidateMode.Associate;
            _assocPrev = prev;
            _assocPending = pending ?? "";
            _assocItems = items ?? new List<string>();
            Render();
        }

        /// <summary>
        /// 只按目前內容重畫，不重新定位 / 不改尺寸。
        /// 給離線截圖用（--uishop 自己控制視窗大小）。
        /// </summary>
        public void RepaintOnly()
        {
            Invalidate();
        }

        /// <summary>隱藏聯想字（例如按過 Esc、或者已選字）。</summary>
        public void ClearAssoc()
        {
            _mode = CandidateMode.Compose;
            _assocPrev = null;
            _assocPending = "";
            _assocItems = new List<string>();
        }

        /// <summary>目前顯示中的聯想字（回傳副本，測試用）。</summary>
        public IList<string> AssocItems { get { return new List<string>(_assocItems); } }

        /// <summary>點選聯想字時回報（由 KeyboardHook 掛上）。</summary>
        public event EventHandler AssocCommitted;

        /// <summary>隱藏視窗（暫停輸入法 / 逾時時用）。</summary>
        public void HideNow()
        {
            ClearAssoc();
            if (Visible) Hide();
        }
        /// <summary>依組字狀態重畫並重新定位。</summary>
        public void Render()
        {
            if (_mode == CandidateMode.Associate)
            {
                RenderAssoc();
                return;
            }

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
            PlaceAt(x, y, w, h);
            if (!Visible) Show();
            Invalidate();
        }

        private void RenderAssoc()
        {
            int count = Math.Min(_assocItems.Count, AssocPerPage);
            if (count == 0) { if (Visible) Hide(); return; }

            int w = PadX * 2 + CellW * count;
            int h = PadY * 2 + CodeH + CellH + FootH;

            int x, y;
            GetCaretPoint(out x, out y);
            PlaceAt(x, y, w, h);
            if (!Visible) Show();
            Invalidate();
        }

        /// <summary>依插入點定位，並確保不會超出螢幕。</summary>
        private void PlaceAt(int x, int y, int w, int h)
        {
            Rectangle scr = Screen.FromPoint(new Point(x, y)).WorkingArea;
            if (x + w > scr.Right) x = scr.Right - w;
            if (y + h > scr.Bottom) y = y - h - 8;
            if (x < scr.Left) x = scr.Left;
            if (y < scr.Top) y = scr.Top;
            Bounds = new Rectangle(x, y, w, h);
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
            if (_mode == CandidateMode.Associate) { PaintAssoc(e); return; }
            PaintCompose(e);
        }

        private void PaintCompose(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.Default;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawFrame(g);

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

        /// <summary>聯想字畫面：標題顯示上一個字，下面一排可以按數字選。</summary>
        private void PaintAssoc(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.Default;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawFrame(g);

            int count = Math.Min(_assocItems.Count, AssocPerPage);
            if (count == 0) return;

            // 標題：「聯想　你（好）」
            string title = "聯想　" + (_assocPrev ?? "");
            if (!string.IsNullOrEmpty(_assocPending))
                title += "（" + _assocPending + "）";
            using (var b = new SolidBrush(CodeColor))
                g.DrawString(title, _fontSmall, b, PadX + 1, PadY + 3);

            for (int i = 0; i < count; i++)
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
                    g.DrawString(_assocItems[i], _fontChar, b,
                        new RectangleF(cx, cy, CellW, CellH), _centered);
            }

            // 底部：提示
            string foot = "1-" + count + " 選字　空格 = 取消　Esc = 取消";
            using (var b = new SolidBrush(DimColor))
                g.DrawString(foot, _fontSmall, b, PadX + 1, Height - FootH - 1);
        }

        private void DrawFrame(Graphics g)
        {
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var bg = new SolidBrush(BgColor)) g.FillRectangle(bg, rect);
            using (var pen = new Pen(BorderColor)) g.DrawRectangle(pen, rect);
        }

        /// <summary>聯想字被選中（由 KeyboardHook 呼叫，然後由 Program 送出文字）。</summary>
        internal void RaiseAssocCommitted()
        {
            var h = AssocCommitted;
            if (h != null) h(this, EventArgs.Empty);
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
