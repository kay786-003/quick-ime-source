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
        private Native.LowLevelKeyboardProc _proc;   // 必須保留參考，否則會被 GC 回收
        private IntPtr _hook = IntPtr.Zero;
        private bool _shiftAloneDown;                // Shift 按下且未與其他鍵同按

        public bool Enabled { get; set; }
        public bool ChineseMode { get; set; }
        public event EventHandler ModeChanged;

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
            Uninstall();
            GC.SuppressFinalize(this);
        }

        private static bool IsDown(int vk)
        {
            return (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
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
            if (nCode < 0 || !Enabled)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            int msg = (int)wParam;

            // ---- Shift 單按放開 = 中英切換 ----
            // 注意：一定要放行這個 keyup，否則 Shift+字母（打大寫）會卡住 Shift 狀態。
            if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP)
            {
                var upInfo = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                uint upVk = upInfo.vkCode;
                if (upVk == Native.VK_SHIFT || upVk == Native.VK_LSHIFT || upVk == Native.VK_RSHIFT)
                {
                    if (_shiftAloneDown)
                    {
                        _shiftAloneDown = false;
                        ChineseMode = !ChineseMode;
                        RaiseModeChanged();
                        Program.Log("shift alone -> toggle cn=" + ChineseMode);
                    }
                }
                else
                {
                    _shiftAloneDown = false;
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
            Program.Log("hook: vk=" + vk + " composing=" + _composer.IsComposing + " cn=" + ChineseMode);

            // 有 Ctrl / Alt / Win 鍵按著 -> 交回系統（保留快捷鍵）
            bool ctrl = IsDown(Native.VK_CONTROL) || IsDown(Native.VK_LCONTROL) || IsDown(Native.VK_RCONTROL);
            bool alt = IsDown(Native.VK_MENU) || IsDown(Native.VK_LMENU) || IsDown(Native.VK_RMENU);
            bool win = IsDown(0x5B) || IsDown(0x5C);
            if (ctrl || alt || win)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            // ---- Shift 單按 = 中英切換（用 keyup 偵測，避免與 Shift+字母 衝突）----
            bool shiftDown = IsDown(Native.VK_SHIFT) || IsDown(Native.VK_LSHIFT) || IsDown(Native.VK_RSHIFT);
            if (vk == Native.VK_SHIFT || vk == Native.VK_LSHIFT || vk == Native.VK_RSHIFT)
            {
                _shiftAloneDown = true;
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
            }
            // 只要有其他鍵在 Shift 按住期間按下，就取消「單按」判定
            if (shiftDown) _shiftAloneDown = false;

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

        /// <summary>把 Composer 產生的文字實際送出去。</summary>
        public void Flush()
        {
            string text = _composer.PendingText;
            if (text == null) return;
            _composer.ClearPending();
            Typist.SendText(text);
        }

        public void RaiseModeChanged()
        {
            var h = ModeChanged;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
