// ============================================================================
//  Keyboard.cs - 低階鍵盤掛鉤 (WH_KEYBOARD_LL)
//  以「免安裝」為前提：不裝 TSF/CTF、不改系統設定，直接在行程內攔鍵並送字。
// ============================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QuickIME
{
    internal static class Native
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int VK_BACK = 0x08;
        public const int VK_TAB = 0x09;
        public const int VK_RETURN = 0x0D;
        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;    // Alt
        public const int VK_CAPITAL = 0x14;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_SPACE = 0x20;
        public const int VK_PRIOR = 0x21;   // PageUp
        public const int VK_NEXT = 0x22;    // PageDown
        public const int VK_END = 0x23;
        public const int VK_HOME = 0x24;
        public const int VK_LEFT = 0x25;
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public const int VK_LMENU = 0xA4;
        public const int VK_RMENU = 0xA5;
        public const int VK_OEM_MINUS = 0xBD;
        public const int VK_OEM_PLUS = 0xBB;

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        /// <summary>
        /// 單調遞增的毫秒計時（由系統啟動起算），用於判斷 Shift 按了多久。
        /// 刻意不用 Environment.TickCount（會溢位成負數，令長按判定出錯）。
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();
    }

    /// <summary>送出 Unicode 文字到前景視窗（不經剪貼簿）。</summary>
    internal static class Typist
    {
        /// <summary>
        /// 本程式注入按鍵時使用的 magic 標記。
        /// 掛鉤會認出這個 dwExtraInfo 並直接放行，避免「自己攔自己」的回圈。
        /// </summary>
        public static readonly IntPtr Magic = (IntPtr)0x514B494D; // 'QKIM'

        public static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var list = new List<Native.INPUT>(text.Length * 2);
            foreach (char ch in text)
            {
                var down = new Native.INPUT();
                down.type = Native.INPUT_KEYBOARD;
                down.u.ki.wVk = 0;
                down.u.ki.wScan = ch;
                down.u.ki.dwFlags = Native.KEYEVENTF_UNICODE;
                down.u.ki.time = 0;
                down.u.ki.dwExtraInfo = Magic;
                list.Add(down);

                var up = down;
                up.u.ki.dwFlags = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP;
                list.Add(up);
            }
            var arr = list.ToArray();
            Native.SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(Native.INPUT)));
        }

        public static void SendBackspace(int n)
        {
            if (n <= 0) return;
            var list = new List<Native.INPUT>(n * 2);
            for (int i = 0; i < n; i++)
            {
                var down = new Native.INPUT();
                down.type = Native.INPUT_KEYBOARD;
                down.u.ki.wVk = (ushort)Native.VK_BACK;
                down.u.ki.wScan = 0;
                down.u.ki.dwFlags = 0;
                down.u.ki.dwExtraInfo = Magic;
                list.Add(down);

                var up = down;
                up.u.ki.dwFlags = Native.KEYEVENTF_KEYUP;
                list.Add(up);
            }
            var arr = list.ToArray();
            Native.SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(Native.INPUT)));
        }
    }

    /// <summary>
    /// 鍵盤掛鉤：攔截 a-z / 空白 / 數字 / 翻頁 / 退格，交由 Composer 組字。
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        private readonly Composer _composer;
        private readonly ToggleGate _gate = new ToggleGate();
        private Native.LowLevelKeyboardProc _proc;   // 必須保留參考，否則會被 GC 回收
        private IntPtr _hook = IntPtr.Zero;

        public bool Enabled { get; set; }
        public bool ChineseMode { get; set; }
        public event EventHandler ModeChanged;

        /// <summary>中英切換 / Ctrl+空白 的設定（由系統匣選單控制）。</summary>
        public ToggleGate Gate { get { return _gate; } }

        public KeyboardHook(Composer composer)
        {
            _composer = composer;
            ChineseMode = true;
            Enabled = true;
        }

        public bool Install()
        {
            if (_hook != IntPtr.Zero) return true;
            _proc = HookCallback;
            IntPtr hMod = Native.GetModuleHandle(null);
            _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, hMod, 0);
            if (_hook == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "無法安裝鍵盤掛鉤 (error " + err + ")");
            }
            return true;
        }

        public void Uninstall()
        {
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            DisposeAssoc();
            Uninstall();
            GC.SuppressFinalize(this);
        }

        private static bool IsDown(int vk)
        {
            return (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        /// <summary>把 VK_SHIFT / VK_LSHIFT / VK_RSHIFT 統一成「左 / 右」。</summary>
        private static int ShiftSideOf(uint vk)
        {
            if (vk == Native.VK_LSHIFT) return ToggleGate.LeftSide;
            if (vk == Native.VK_RSHIFT) return ToggleGate.RightSide;
            if (vk != Native.VK_SHIFT) return ToggleGate.NoSide;
            // 有些鍵盤 / 遠端桌面會送出通用的 VK_SHIFT，用實際按鍵狀態判斷是哪一邊
            if (IsDown(Native.VK_LSHIFT)) return ToggleGate.LeftSide;
            if (IsDown(Native.VK_RSHIFT)) return ToggleGate.RightSide;
            return ToggleGate.NoSide;
        }

        private static int NowMs()
        {
            return (int)(Native.GetTickCount64() & 0x7FFFFFFF);
        }

        /// <summary>
        /// 系統層面「Shift 是否真的按住」。只用來校正漏收 keyup 的情況，
        /// 正常判斷一律靠 ToggleGate 自己記錄（低階掛鉤內 GetAsyncKeyState 時序不可靠）。
        /// </summary>
        private static bool ShiftActuallyDown()
        {
            return IsDown(Native.VK_SHIFT) || IsDown(Native.VK_LSHIFT) || IsDown(Native.VK_RSHIFT);
        }

        private char ToLowerLetter(uint vk)
        {
            if (vk >= 'A' && vk <= 'Z') return (char)('a' + (vk - 'A'));
            return '\0';
        }

        private int DigitIndex(uint vk)
        {
            if (vk >= '1' && vk <= '9') return (int)(vk - '1');
            if (vk >= '0' && vk <= '9') return 9;    // '0' -> 第 10 個
            return -1;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            int msg = (int)wParam;

            // ---- KeyUp ----
            if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP)
            {
                var upInfo = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                uint upVk = upInfo.vkCode;

                // 自己注入的按鍵一律放行
                if (upInfo.dwExtraInfo == Typist.Magic)
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

                if (!Enabled)
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

                // Ctrl+空白：按住不放 / 連發都唔會重複觸發，只認「按完」這一下
                if (upVk == Native.VK_SPACE)
                    _gate.CtrlSpace(false);

                // ---- Shift 單按放開 = 中英切換 ----
                // 注意：一定要放行這個 keyup，否則 Shift+字母（打大寫）會卡住 Shift 狀態。
                int side = ShiftSideOf(upVk);
                if (side != ToggleGate.NoSide && _gate.ShiftUp(side, NowMs()))
                {
                    ChineseMode = !ChineseMode;
                    if (!ChineseMode) _composer.Reset();
                    RaiseModeChanged();
                    Program.Log("shift(" + (side == ToggleGate.LeftSide ? "L" : "R") +
                                ") tap -> toggle cn=" + ChineseMode);
                }
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            if (msg != Native.WM_KEYDOWN && msg != Native.WM_SYSKEYDOWN)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            var info = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));

            // 自己注入的按鍵（上屏文字）一律放行，否則會無限回圈。
            if (info.dwExtraInfo == Typist.Magic)
            {
                Program.Log("hook: injected vk=" + info.vkCode + " -> pass");
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            uint vk = info.vkCode;

            bool ctrl = IsDown(Native.VK_CONTROL) || IsDown(Native.VK_LCONTROL) || IsDown(Native.VK_RCONTROL);
            bool alt = IsDown(Native.VK_MENU) || IsDown(Native.VK_LMENU) || IsDown(Native.VK_RMENU);
            bool win = IsDown(0x5B) || IsDown(0x5C);

            // ---- Ctrl+空白 ----
            // 微軟速成會用它在「全形／半形符號」之間切換，打機緊張時很容易誤按。
            // 預設完全攔死（連系統都收唔到）；關掉之後就完全唔干預，交回系統。
            // 注意：一定要真係有按 Ctrl 才算，否則普通空白鍵會被吞掉。
            if (vk == Native.VK_SPACE)
            {
                if (ctrl)
                {
                    if (_gate.CtrlSpace(true) || _gate.CtrlSpaceHeld)
                    {
                        Program.Log("hook: ctrl+space blocked");
                        return (IntPtr)1;
                    }
                }
                _gate.CtrlSpace(false);
            }
            else
            {
                _gate.CtrlSpace(false);
            }

            if (!Enabled)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            Program.Log("hook: vk=" + vk + " composing=" + _composer.IsComposing + " cn=" + ChineseMode);

            // ---- Shift 按下：記錄時間，等放開時判斷係「單按」定「長按」----
            int shiftSide = ShiftSideOf(vk);
            if (shiftSide != ToggleGate.NoSide)
            {
                _gate.ShiftDown(shiftSide, NowMs());
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            // 任何其他鍵按下 → 呢次 Shift 就唔再算「單按」
            _gate.NoteOtherKey();

            // ---- 長按 Shift 期間：完全唔組字，鍵盤交回原生 ----
            // 打機長按 Shift 跑步時，WASD 一定要傳到遊戲，所以這裡直接放行。
            // （Shift 長期按住亦唔會切換中英，見 ToggleGate 的長按判定。）
            if (_gate.IsShiftHeld)
            {
                // 安全網：若系統實際已經冇按住 Shift（掛鉤漏收 keyup），
                // 就校正狀態，否則會永遠打不到中文字。
                if (!ShiftActuallyDown())
                {
                    _gate.ResetShift();
                    Program.Log("hook: shift state desync -> reset");
                }
                else
                {
                    if (!ctrl && !alt && !win && _composer.IsComposing)
                    {
                        string first = _composer.FirstCandidate();
                        if (!string.IsNullOrEmpty(first)) { _composer.Commit(first); Flush(); }
                        else _composer.Cancel();
                    }
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
                }
            }

            // 有 Ctrl / Alt / Win 鍵按著 -> 交回系統（保留快捷鍵）
            if (ctrl || alt || win)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            // ---- 組字中 ----
            if (_composer.IsComposing)
            {
                // 數字選字
                int di = DigitIndex(vk);
                if (di >= 0)
                {
                    int onPage = _composer.CurrentPage().Count;
                    if (di < onPage) { _composer.SelectOnPage(di); Flush(); return (IntPtr)1; }
                }

                // 空白：微軟速成習慣 → 下一頁（最後一頁 / 單頁則上屏首選）
                if (vk == Native.VK_SPACE) { _composer.PressSpace(); Flush(); return (IntPtr)1; }

                // 翻頁（PageDown/PageUp 仍然可用，方便用家）
                if (vk == Native.VK_NEXT) { _composer.PageMove(1); return (IntPtr)1; }
                if (vk == Native.VK_PRIOR) { _composer.PageMove(-1); return (IntPtr)1; }
                if (vk == Native.VK_OEM_MINUS) { _composer.PageMove(-1); return (IntPtr)1; }
                if (vk == Native.VK_OEM_PLUS) { _composer.PageMove(1); return (IntPtr)1; }

                // 退格
                if (vk == Native.VK_BACK) { _composer.Backspace(); return (IntPtr)1; }

                // Esc 取消
                if (vk == Native.VK_ESCAPE) { _composer.Cancel(); return (IntPtr)1; }

                // 標點 / Enter 等：先上屏首選，再讓原本的鍵生效
                bool letter = ToLowerLetter(vk) != '\0';
                if (!letter)
                {
                    string first = _composer.FirstCandidate();
                    if (!string.IsNullOrEmpty(first)) { _composer.Commit(first); Flush(); }
                    else _composer.Cancel();
                    // 不消耗，交回系統（例如 Enter 換行、標點符號）
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
                }
            }

            // ---- 聯想字模式（上一個字之後嘅建議）----
            if (_assocActive)
            {
                if (HandleAssocKey(vk))
                    return (IntPtr)1;
                // 落到這裡代表呢個鍵唔關聯想字事 → 收起聯想窗，繼續正常處理
                EndAssoc();
            }

            // ---- 中文模式 ----
            if (!ChineseMode)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            char lower = ToLowerLetter(vk);
            if (lower != '\0')
            {
                _composer.TypeLetter(lower);
                return (IntPtr)1;
            }

            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        // ------------------------------------------------------------------
        //  聯想字
        // ------------------------------------------------------------------
        /// <summary>
        /// 上一個上屏的字。空字串 = 未有（開頭 / 換行之後）。
        /// 只在中文模式、而且真係上屏了中文字才會更新。
        /// </summary>
        public string ContextPrev { get; private set; }

        /// <summary>聯想字是否顯示中。</summary>
        public bool AssocActive { get { return _assocActive; } }

        /// <summary>目前已打的聯想前綴（顯示在視窗標題用）。</summary>
        public string AssocPending { get { return _assocPending; } }

        /// <summary>聯想字設定（由 Program 提供）。false = 完全不出聯想窗。</summary>
        public bool AssocEnabled { get; set; }

        /// <summary>
        /// 是否記錄用字習慣（把學過的組合排到最前）。由 Program 提供。
        ///
        /// 與 <see cref="AssocEnabled"/> 分開：
        ///   * AssocEnabled = false      → 不出聯想窗，也不學
        ///   * AssocLearnEnabled = false → 仍然出聯想窗，但只用出廠詞表
        ///
        /// 關掉之後，已有的記憶仍然會被讀取（聯想仍然按舊記憶排），
        /// 只是不再新增／累加。這樣用家「暫停學習」唔會失去之前學到的東西。
        /// </summary>
        public bool AssocLearnEnabled { get; set; }

        /// <summary>聯想前綴上限（打太多字就自動放棄，避免無限長）。</summary>
        private const int MaxAssocPending = 3;

        /// <summary>目前顯示中的聯想字（由 UI 回報，掛鉤唔直接持有 Form）。</summary>
        public IList<string> AssocItems
        {
            get { return _assocItems; }
            set { _assocItems = value ?? new List<string>(); }
        }

        /// <summary>聯想字有變更（UI 要重畫）。</summary>
        public event EventHandler AssocChanged;

        private bool _assocActive;
        private string _assocPending = "";
        private IList<string> _assocItems = new List<string>();

        private void RaiseAssocChanged()
        {
            var h = AssocChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>
        /// 現在是否應該記錄習慣：聯想字要開著，而且「記錄習慣」開關亦要開。
        /// 兩者任何一個關掉都唔會學習。（測試會直接驗證這個判斷。）
        /// </summary>
        public bool ShouldLearn { get { return AssocEnabled && AssocLearnEnabled; } }

        /// <summary>
        /// 由上屏文字更新聯想狀態（掛鉤內部用）。
        /// 只有「純中文字」才學，標點 / 英文 / 數字一律當成「重新開始」。
        /// </summary>
        private void NoteCommitted(string text)
        {
            if (string.IsNullOrEmpty(text)) { EndAssoc(); return; }

            string prev = ContextPrev;
            if (ShouldLearn && _assoc != null && prev != null && prev.Length == 1
                && text.Length == 1 && Assoc.IsHanChar(text[0]) && Assoc.IsHanChar(prev[0]))
            {
                _assoc.Learn(prev, text);
                ScheduleSave();
            }

            if (AssocEnabled && Assoc.IsHanChar(text[text.Length - 1]))
            {
                ContextPrev = text.Substring(text.Length - 1);
                ShowAssocFor();
            }
            else
            {
                ContextPrev = "";
                EndAssoc();
            }
        }

        private void ShowAssocFor()
        {
            if (!AssocEnabled || _assoc == null) { EndAssoc(); return; }
            string prev = ContextPrev;
            if (string.IsNullOrEmpty(prev)) { EndAssoc(); return; }

            _assocPending = "";
            _assocItems = _assoc.Lookup(prev, "");
            _assocActive = _assocItems.Count > 0;
            RaiseAssocChanged();
        }

        private void EndAssoc()
        {
            bool was = _assocActive;
            _assocActive = false;
            _assocPending = "";
            _assocItems = new List<string>();
            if (was) RaiseAssocChanged();
        }

        /// <summary>暫停 / 切模式時清掉聯想狀態（唔清 ContextPrev 之外嘅嘢）。</summary>
        public void ResetAssoc()
        {
            ContextPrev = "";
            EndAssoc();
        }

        /// <summary>
        /// 處理聯想字模式中的按鍵。回傳 true = 已消耗（唔交回系統）。
        /// 唔關事嘅鍵回傳 false，由呼叫者收起聯想窗並繼續正常流程。
        /// </summary>
        private bool HandleAssocKey(uint vk)
        {
            // 數字 1-9 選聯想字
            int di = DigitIndex(vk);
            if (di >= 0 && di < _assocItems.Count)
            {
                CommitAssoc(_assocItems[di]);
                return true;
            }

            // 空格 / Esc：收起聯想窗（唔上屏任何東西）
            if (vk == Native.VK_SPACE || vk == Native.VK_ESCAPE)
            {
                EndAssoc();
                RaiseAssocChanged();
                return true;
            }

            // 字母：當成「繼續打聯想字」的前綴（像拼音輸入法一樣越打越窄）
            char lower = ToLowerLetter(vk);
            if (lower != '\0' && AssocEnabled && _assoc != null)
            {
                string next = _assocPending + lower;
                if (next.Length > MaxAssocPending) { EndAssoc(); RaiseAssocChanged(); return false; }

                var items = _assoc.Lookup(ContextPrev, next);
                if (items.Count > 0)
                {
                    _assocPending = next;
                    _assocItems = items;
                    RaiseAssocChanged();
                    return true;
                }

                // 冇字接得上 → 當普通速成碼處理（收起聯想窗）
                EndAssoc();
                RaiseAssocChanged();
                return false;
            }

            return false;
        }

        /// <summary>選了一個聯想字：學起來、送出、再把新字當成新的上下文。</summary>
        private void CommitAssoc(string ch)
        {
            if (string.IsNullOrEmpty(ch)) { EndAssoc(); return; }

            string prev = ContextPrev;
            if (ShouldLearn && _assoc != null && !string.IsNullOrEmpty(prev))
            {
                _assoc.Learn(prev, ch);
                ScheduleSave();
            }

            EndAssoc();
            RaiseAssocChanged();
            Typist.SendText(ch);
            ContextPrev = ch;
            ShowAssocFor();
        }

        /// <summary>記憶寫檔（延遲寫，避免每打一個字就寫一次硬碟）。</summary>
        private void ScheduleSave()
        {
            _dirty = true;
            if (_saveTimer == null) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private System.Windows.Forms.Timer _saveTimer;
        private bool _dirty;
        private Action<string> _saveAction;

        /// <summary>
        /// 由 Program 注入「儲存記憶」的動作。
        /// 掛鉤本身唔應該知道檔案路徑（保持可測試）。
        /// </summary>
        public void InitAssoc(Assoc assoc, Action<string> saveAction)
        {
            _assoc = assoc;
            _saveAction = saveAction;
            AssocEnabled = assoc != null;
            AssocLearnEnabled = assoc != null;
            ContextPrev = "";
            _assocItems = new List<string>();
            _assocActive = false;
            _assocPending = "";

            _saveTimer = new System.Windows.Forms.Timer();
            _saveTimer.Interval = 4000;
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                FlushAssocSave();
            };
        }

        /// <summary>立刻把未寫入的學習記憶寫檔（結束程式 / 清空前用）。</summary>
        public void FlushAssocSave()
        {
            if (!_dirty || _saveAction == null) return;
            _dirty = false;
            try { _saveAction(null); } catch { }
        }

        public void DisposeAssoc()
        {
            FlushAssocSave();
            if (_saveTimer != null) { _saveTimer.Dispose(); _saveTimer = null; }
        }

        private Assoc _assoc;


        /// <summary>把 Composer 產生的文字實際送出去。</summary>
        public void Flush()
        {
            string text = _composer.PendingText;
            if (text == null) return;
            _composer.ClearPending();
            Typist.SendText(text);
            NoteCommitted(text);
        }

        public void RaiseModeChanged()
        {
            var h = ModeChanged;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
