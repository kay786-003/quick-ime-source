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
        public const int VK_OEM_1 = 0xBA;      // ; :
        public const int VK_OEM_2 = 0xBF;      // / ?
        public const int VK_OEM_3 = 0xC0;      // ` ~
        public const int VK_OEM_4 = 0xDB;      // [ {
        public const int VK_OEM_5 = 0xDC;      // \ |
        public const int VK_OEM_6 = 0xDD;      // ] }
        public const int VK_OEM_7 = 0xDE;      // ' "
        public const int VK_OEM_COMMA = 0xBC;  // , <
        public const int VK_OEM_PERIOD = 0xBE; // . >

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

    /// <summary>
    /// Ctrl + 符號鍵 → 全形標點。
    ///
    /// 【用家習慣】用家打「，。」的肌肉記憶就是按住 Ctrl 再按那個符號鍵，
    /// 與微軟速成一致。而 <c>? !</c> 保持半形，所以那兩個鍵**不在**這張表裡。
    /// 詳見 AGENTS.md「符號」。
    ///
    /// 表用 VK 而不是掃描碼，所以對 US / 香港常用鍵盤都適用；
    /// 若用家的鍵盤排列不同導致某個鍵對不上，只要改這一個字典就夠。
    /// </summary>
    internal static class FullWidthPunct
    {
        /// <summary>
        /// 按鍵 → 全形符號。<c>,</c>→<c>，</c>、<c>.</c>→<c>。</c> 是最常用的兩個。
        ///
        /// 分做「一級」與「二級」兩張表，原因是同一個鍵要出兩個不同符號：
        /// 用家的鍵盤上 <c>;</c> 與 <c>:</c> 是同一個鍵，用 Shift 區分。
        /// 一級 = 不按 Shift，二級 = 按住 Shift。
        /// </summary>
        private static readonly Dictionary<int, string> MapPlain = new Dictionary<int, string>
        {
            { Native.VK_OEM_COMMA,  "，" },   // ,
            { Native.VK_OEM_PERIOD, "。" },   // .
            { Native.VK_OEM_1,      "；" },   // ;
            { Native.VK_OEM_7,      "：" },   // '   （用家常打「：」）
            { Native.VK_OEM_4,      "「" },   // [
            { Native.VK_OEM_6,      "」" },   // ]
            { Native.VK_OEM_MINUS,  "－" },   // -
            { Native.VK_OEM_PLUS,   "＝" },   // =
            { Native.VK_OEM_5,      "、" },   // \
        };

        /// <summary>按住 Shift 時的版本（<c>Ctrl+Shift+;</c> → <c>：</c>）。</summary>
        private static readonly Dictionary<int, string> MapShift = new Dictionary<int, string>
        {
            { Native.VK_OEM_1,      "：" },   // Ctrl+Shift+;  → ：
            { Native.VK_OEM_7,      "「" },   // Ctrl+Shift+'  → 「
        };

        /// <summary>
        /// 查這對 (vk, shift) 有沒有全形對應。沒有就回傳 null（交回系統）。
        /// 刻意<b>不含</b> <c>/ ?</c>：用家要半形的 <c>?</c>。也不含數字與字母。
        /// </summary>
        public static string Lookup(uint vk, bool shift)
        {
            string s;
            if (shift && MapShift.TryGetValue((int)vk, out s)) return s;
            if (MapPlain.TryGetValue((int)vk, out s)) return s;
            return null;
        }
    }

    /// <summary>送出 Unicode 文字到前景視窗（不經剪貼簿）。</summary>
    internal static class Typist
    {
        /// <summary>
        /// 本程式注入按鍵時使用的 magic 標記。
        /// 掛鉤會認出這個 dwExtraInfo 並直接放行，避免「自己攔自己」的回圈。
        /// </summary>
        public static readonly IntPtr Magic = (IntPtr)0x514B494D; // 'QKIM'

        /// <summary>
        /// 測試用注入點（seam）：設定之後 <see cref="SendText"/> / <see cref="SendBackspace"/>
        /// 只會呼叫這個 delegate，**完全不碰 Win32**。
        ///
        /// 為什麼需要：--hooktest 要重播真實按鍵流程，而那些流程最後會走 SendText。
        /// 若沒有這個接縫，測試就會把中文真的打進使用者正在用的視窗（或直接失控）。
        /// 正式執行時這個值一直是 null。
        /// </summary>
        public static Action<string> SendSink;

        public static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var sink = SendSink;
            if (sink != null) { sink(text); return; }

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

    internal enum AssocDigitAction
    {
        /// <summary>唔關聯想字事，照正常流程處理。</summary>
        Ignore = 0,
        /// <summary>用這個數字選聯想字。</summary>
        Select,
        /// <summary>收起聯想窗，個鍵交回系統（例如超出範圍的數字）。</summary>
        CloseAndPass,
        /// <summary>唔當選字（Shift+數字模式但冇按 Shift）→ 收起聯想窗後當普通數字打。</summary>
        PassAsNumber
    }

    /// <summary>
    /// 一個按鍵事件的「純輸入」：全部修飾鍵狀態都由呼叫者算好，
    /// 所以 <see cref="KeyboardHook.ProcessKeyDown"/> 完全唔碰 Win32，可以離線重播。
    /// 見 --hooktest。
    /// </summary>
    internal struct KeyInput
    {
        public uint Vk;
        public bool Ctrl;
        public bool Alt;
        public bool Win;
        /// <summary>系統層面 Shift 實際是否按住（只用來校正漏收 keyup）。</summary>
        public bool ShiftPhysicallyDown;
        /// <summary>ToggleGate.LeftSide / RightSide / NoSide。</summary>
        public int ShiftSide;
        public int NowMs;
    }

    /// <summary>
    /// 鍵盤掛鉤：攔截 a-z / 空白 / 數字 / 翻頁 / 退格，交由 Composer 組字。
    ///
    /// 【重要】所有判斷都在 <see cref="ProcessKeyDown"/> / <see cref="ProcessKeyUp"/>，
    /// 它們只依賴傳入的 <see cref="KeyInput"/>，不呼叫任何 Win32。
    /// <see cref="HookCallback"/> 只負責「讀 Win32 → 餵事件 → 執行結果」，
    /// 所以 --hooktest 可以完整重播真實按鍵序列（連打／z／數字超界／尾頁 Enter）。
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

        /// <summary>
        /// 數字鍵 → 當頁第幾個候選（0-based，只有 0..8）。
        ///
        /// 【硬規則】微軟速成一頁 9 個、用 1-9 選，<c>0</c> 不選字（用家實測）。
        /// 舊版把 <c>0</c> 映射成第 10 個（index 9），但一頁只有 9 格，
        /// 所以 <c>0</c> 永遠指向不存在的格子 —— 直接落空。
        /// （見 AGENTS.md「已否決的建議」：不要改成一頁 10 個。）
        /// </summary>
        internal static int DigitIndex(uint vk)
        {
            if (vk >= '1' && vk <= '9') return (int)(vk - '1');
            return -1;
        }

        /// <summary>係唔係任何數字鍵（包括 '0'）—— 用來分辨「超界選字」同「根本唔係數字」。</summary>
        internal static bool IsDigitKey(uint vk)
        {
            return vk >= '0' && vk <= '9';
        }

        /// <summary>
        /// 聯想字遇到數字鍵時應該做什麼（純邏輯，方便 --selftest 驗證）。
        ///
        /// <para>shiftDigitsOn = 用家要「Shift + 數字」才選聯想字（Windows 速成習慣）：</para>
        /// * 按住 Shift + 1-9 → 選聯想字
        /// * 冇按 Shift + 1-9   → 唔當選字，收起聯想窗，個數字照常打出來
        ///   （這樣打完中文字想打「第3章」就唔會被聯想窗吞掉）
        ///
        /// <para>shiftDigitsOn = false（v2.2 舊行為）：</para>
        /// * 任何情況下 1-9 都直接選聯想字
        /// </summary>
        internal static AssocDigitAction DecideAssocDigit(
            bool shiftHeld, bool shiftDigitsOn, int digitIndex, int itemCount)
        {
            if (digitIndex < 0) return AssocDigitAction.Ignore;

            if (!shiftDigitsOn)
                return digitIndex < itemCount ? AssocDigitAction.Select : AssocDigitAction.Ignore;

            if (!shiftHeld) return AssocDigitAction.PassAsNumber;
            return digitIndex < itemCount ? AssocDigitAction.Select : AssocDigitAction.CloseAndPass;
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

                // 自己注入的按鍵一律放行
                if (upInfo.dwExtraInfo == Typist.Magic)
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

                if (!Enabled)
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

                var up = new KeyInput
                {
                    Vk = upInfo.vkCode,
                    ShiftSide = ShiftSideOf(upInfo.vkCode),
                    NowMs = NowMs(),
                    ShiftPhysicallyDown = ShiftActuallyDown()
                };
                ProcessKeyUp(up);
                // 注意：一定要放行 keyup，否則 Shift+字母（打大寫）會卡住 Shift 狀態。
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            if (msg != Native.WM_KEYDOWN && msg != Native.WM_SYSKEYDOWN)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            var info = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));

            // 自己注入的按鍵（上屏文字）一律放行，否則會無限回圈。
            if (info.dwExtraInfo == Typist.Magic)
            {
                // 掛鉤對延遲極敏感（LowLevelHooksTimeout 預設 300ms，逾時系統會靜默移除掛鉤），
                // 所以日誌一律惰性求值：Verbose=false 時連字串都不會組。
                Program.Log(() => "hook: injected vk=" + info.vkCode + " -> pass");
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            var input = new KeyInput
            {
                Vk = info.vkCode,
                Ctrl = IsDown(Native.VK_CONTROL) || IsDown(Native.VK_LCONTROL) || IsDown(Native.VK_RCONTROL),
                Alt = IsDown(Native.VK_MENU) || IsDown(Native.VK_LMENU) || IsDown(Native.VK_RMENU),
                Win = IsDown(0x5B) || IsDown(0x5C),
                ShiftSide = ShiftSideOf(info.vkCode),
                NowMs = NowMs(),
                ShiftPhysicallyDown = ShiftActuallyDown()
            };

            int result = ProcessKeyDown(input);

            if (result == HookResult.CommitPending) Flush();

            return result == HookResult.Consumed
                ? (IntPtr)1
                : Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        /// <summary>ProcessKeyDown 的回傳值。</summary>
        internal static class HookResult
        {
            /// <summary>交回系統。</summary>
            public const int Pass = 0;
            /// <summary>已消耗，不要交回系統。</summary>
            public const int Consumed = 1;
            /// <summary>已消耗，而且要立刻把 Composer 的 PendingText 送出去。</summary>
            public const int CommitPending = 2;
        }

        /// <summary>
        /// 【純邏輯】處理一次 keydown。
        ///
        /// 完全不碰 Win32：所有修飾鍵、Shift 側、時間都由 <see cref="KeyInput"/> 傳入。
        /// 這樣 --hooktest 才可以重播真實按鍵序列，覆蓋「連打／z／數字超界／尾頁 Enter」。
        /// </summary>
        internal int ProcessKeyDown(KeyInput k)
        {
            uint vk = k.Vk;

            // ---- Ctrl+空白 ----
            // 微軟速成會用它在「全形／半形符號」之間切換，打機緊張時很容易誤按。
            // 預設完全攔死（連系統都收唔到）；關掉之後就完全唔干預，交回系統。
            // 注意：一定要真係有按 Ctrl 才算，否則普通空白鍵會被吞掉。
            if (vk == Native.VK_SPACE)
            {
                if (k.Ctrl)
                {
                    if (_gate.CtrlSpace(true) || _gate.CtrlSpaceHeld)
                    {
                        Program.Log("hook: ctrl+space blocked");
                        return HookResult.Consumed;
                    }
                }
                _gate.CtrlSpace(false);
            }
            else
            {
                _gate.CtrlSpace(false);
            }

            if (!Enabled)
                return HookResult.Pass;

            Program.Log(() => "hook: vk=" + vk + " composing=" + _composer.IsComposing + " cn=" + ChineseMode);

            // ---- Shift 按下：記錄時間，等放開時判斷係「單按」定「長按」----
            if (k.ShiftSide != ToggleGate.NoSide)
            {
                _gate.ShiftDown(k.ShiftSide, k.NowMs);
                return HookResult.Pass;
            }

            // 任何其他鍵按下 → 呢次 Shift 就唔再算「單按」
            _gate.NoteOtherKey();

            // 安全網：若系統實際已經冇按住 Shift（掛鉤漏收 keyup），
            // 就校正狀態，否則會永遠當「長按 Shift」而完全打不到中文字。
            if (_gate.IsShiftHeld && !k.ShiftPhysicallyDown)
            {
                _gate.ResetShift();
                Program.Log("hook: shift state desync -> reset");
            }

            // ---- Ctrl + 符號鍵 → 全形標點 ----
            // 用家習慣：打「，。」就是按住 Ctrl 再按 ,  . （與微軟速成一致）。
            // 必須放在「組字中」之前：組字時按 Ctrl+, 也應該先上屏首選、再出「，」。
            // 必須放在「有 Ctrl 就放行」之前，否則 Ctrl+, 會直接漏給系統（等於打不出來）。
            // 刻意不接管 ? / ! —— 用家要半形。
            if (k.Ctrl && !k.Alt && !k.Win)
            {
                bool shiftHeld = _gate.IsShiftHeld;
                string punct = FullWidthPunct.Lookup(vk, shiftHeld);
                if (punct != null)
                {
                    // 若正在組字，先上屏首選（與 Enter／標點的慣例一致）。
                    // 注意：不能用 Commit() 兩次 —— Commit 會覆寫 PendingText，
                    // 所以要先 Flush 掉首選，再 Commit 標點。
                    if (_composer.IsComposing)
                    {
                        string first = _composer.FirstCandidate();
                        if (!string.IsNullOrEmpty(first)) { _composer.Commit(first); Flush(); }
                        else _composer.Cancel();
                    }
                    _composer.Commit(punct);
                    return HookResult.CommitPending;
                }
            }

            // ---- 聯想字：Shift + 數字鍵 = 選聯想字（Windows 速成習慣）----
            // 微軟速成的聯想字要按住 Shift 再按 1-9 才選得到，
            // 所以這裡一定要放在「長按 Shift 期間唔組字」之前處理，
            // 否則 Shift 會被當成長按而直接放行，聯想字永遠選不到。
            // 但組字中永遠優先處理組字，所以加 !IsComposing。
            if (_assocActive && !_composer.IsComposing && !k.Ctrl && !k.Alt && !k.Win)
            {
                int digitIdx = DigitIndex(vk);
                var act = DecideAssocDigit(_gate.IsShiftHeld, AssocShiftDigits,
                                           digitIdx, _assocItems.Count);
                if (act == AssocDigitAction.Select)
                {
                    CommitAssoc(_assocItems[digitIdx]);
                    return HookResult.Consumed;
                }
                if (act == AssocDigitAction.CloseAndPass)
                {
                    // 超出聯想字範圍（例如只有 5 個卻按了 7）→ 收起聯想窗並放行，
                    // 用家可能真的想打 Shift+7 或普通數字。
                    EndAssoc();
                    return HookResult.Pass;
                }
                // PassAsNumber（Shift+數字 已開但冇按 Shift）→ 落到下面收起聯想窗後
                // 繼續當普通數字處理，唔會吞掉用家的數字。
            }

            // ---- 長按 Shift 期間：完全唔組字，鍵盤交回原生 ----
            // 打機長按 Shift 跑步時，WASD 一定要傳到遊戲，所以這裡直接放行。
            // （Shift 長期按住亦唔會切換中英，見 ToggleGate 的長按判定。）
            if (_gate.IsShiftHeld)
            {
                if (!k.Ctrl && !k.Alt && !k.Win && _composer.IsComposing)
                {
                    string first = _composer.FirstCandidate();
                    if (!string.IsNullOrEmpty(first)) { _composer.Commit(first); Flush(); }
                    else _composer.Cancel();
                }
                return HookResult.Pass;
            }

            // 有 Ctrl / Alt / Win 鍵按著 -> 交回系統（保留快捷鍵）
            if (k.Ctrl || k.Alt || k.Win)
                return HookResult.Pass;

            // ---- 組字中 ----
            if (_composer.IsComposing)
            {
                // 數字選字（只有 1-9；'0' 不選字 —— 見 AGENTS.md）
                int di = DigitIndex(vk);
                if (di >= 0)
                {
                    int onPage = _composer.CurrentPage().Count;
                    if (di < onPage)
                    {
                        _composer.SelectOnPage(di);
                        return HookResult.CommitPending;
                    }

                    // 超出當頁範圍（例如 2 個候選卻按了 5）：
                    // 舊版這裡會「數字漏進文件、候選窗又仍然開著」。
                    // 現在一律吞掉、不出字、不改狀態 —— 打字時最不意外。
                    return HookResult.Consumed;
                }

                // '0' 在組字中沒有作用（不選字），吞掉避免漏進文件
                if (IsDigitKey(vk))
                    return HookResult.Consumed;

                // 空白：微軟速成習慣 → 下一頁；到尾頁再按 = 繞返第 1 頁（循環，
                // 永遠不會偷偷上屏）。只有單頁（冇頁可翻）時才直接上屏第一個。
                if (vk == Native.VK_SPACE) { _composer.PressSpace(); return HookResult.CommitPending; }

                // 翻頁（PageDown/PageUp 仍然可用，方便用家）
                if (vk == Native.VK_NEXT) { _composer.PageMove(1); return HookResult.Consumed; }
                if (vk == Native.VK_PRIOR) { _composer.PageMove(-1); return HookResult.Consumed; }
                if (vk == Native.VK_OEM_MINUS) { _composer.PageMove(-1); return HookResult.Consumed; }
                if (vk == Native.VK_OEM_PLUS) { _composer.PageMove(1); return HookResult.Consumed; }

                // 退格
                if (vk == Native.VK_BACK) { _composer.Backspace(); return HookResult.Consumed; }

                // Esc 取消
                if (vk == Native.VK_ESCAPE) { _composer.Cancel(); return HookResult.Consumed; }

                // 標點 / Enter 等：先上屏首選（當前頁的第 1 個），再讓原本的鍵生效
                char lc = ToLowerLetter(vk);
                if (lc == '\0')
                {
                    string first = _composer.FirstCandidate();
                    if (!string.IsNullOrEmpty(first)) { _composer.Commit(first); Flush(); }
                    else _composer.Cancel();
                    // 不消耗，交回系統（例如 Enter 換行、標點符號）
                    return HookResult.Pass;
                }
            }

            // ---- 聯想字模式（上一個字之後嘅建議）----
            if (_assocActive)
            {
                if (HandleAssocKey(vk))
                    return HookResult.Consumed;
                // 落到這裡代表呢個鍵唔關聯想字事 → 收起聯想窗，繼續正常處理
                EndAssoc();
            }

            // ---- 中文模式 ----
            if (!ChineseMode)
                return HookResult.Pass;

            char lower = ToLowerLetter(vk);
            if (lower != '\0')
            {
                // 【重要】TypeLetter 回傳 false = 這個字母不是有效碼（例如 z，或第二碼 z）。
                // 舊版不理回傳值、一律消耗 → z 被靜默吞掉。
                // 現在交回系統，讓 z 照常打出來。
                if (!_composer.TypeLetter(lower))
                    return HookResult.Pass;

                // 【重要】連打時 TypeLetter 內部會 Commit 上一個首選字，
                // 舊版沒有 Flush，那個字會留在 PendingText 被下一次 Commit 覆蓋
                // （打 abo 之後「明」整個消失）。所以這裡一定要把待送文字送出去。
                Flush();
                return HookResult.Consumed;
            }

            return HookResult.Pass;
        }

        /// <summary>
        /// 【純邏輯】處理一次 keyup。
        /// 目前只有「Shift 單按放開 = 切換中英」與「Ctrl+空白 放開」會用到。
        /// </summary>
        internal void ProcessKeyUp(KeyInput k)
        {
            // Ctrl+空白：按住不放 / 連發都唔會重複觸發，只認「按完」這一下
            if (k.Vk == Native.VK_SPACE)
                _gate.CtrlSpace(false);

            // ---- Shift 單按放開 = 中英切換 ----
            if (k.ShiftSide != ToggleGate.NoSide && _gate.ShiftUp(k.ShiftSide, k.NowMs))
            {
                ChineseMode = !ChineseMode;
                if (!ChineseMode) _composer.Reset();
                RaiseModeChanged();
                Program.Log("shift(" + (k.ShiftSide == ToggleGate.LeftSide ? "L" : "R") +
                            ") tap -> toggle cn=" + ChineseMode);
            }
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

        /// <summary>
        /// 聯想字要唔要按住 Shift 再按 1-9 才選得到。預設開 = 跟 Windows 速成習慣。
        ///
        /// 開（預設）：Shift+1-9 選聯想字；直接按 1-9 就係打數字。
        ///   這樣打完中文字想打數字（例如「第3章」）就唔會被聯想窗吞掉。
        /// 關：直接按 1-9 選聯想字（v2.2 舊行為）。
        /// </summary>
        public bool AssocShiftDigits { get; set; }

        /// <summary>目前顯示中的聯想字（由 UI 回報，掛鉤唔直接持有 Form）。</summary>
        public IList<string> AssocItems
        {
            get { return _assocItems; }
            set { _assocItems = value ?? new List<string>(); }
        }

        /// <summary>聯想字有變更（UI 要重畫）。</summary>
        public event EventHandler AssocChanged;

        private bool _assocActive;
        private IList<string> _assocItems = new List<string>();

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

            _assocItems = _assoc.Lookup(prev);
            _assocActive = _assocItems.Count > 0;
            RaiseAssocChanged();
        }

        private void EndAssoc()
        {
            bool was = _assocActive;
            _assocActive = false;
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
            // 數字 1-9 選聯想字。
            // 預設（Windows 速成習慣）要按住 Shift 才選 —— 那個情況已經在
            // 掛鉤主流程處理（一定要在「長按 Shift 唔組字」之前）。
            // 落到這裡仍然係聯想模式，就代表冇按 Shift：
            //   * 關掉「Shift+數字選字」→ 直接數字選字（v2.2 舊習慣）
            //   * 開住 → 收起聯想窗，個數字照常打出來
            int di = DigitIndex(vk);
            if (di >= 0)
            {
                var act = DecideAssocDigit(_gate.IsShiftHeld, AssocShiftDigits, di, AssocSelectableCount);
                if (act == AssocDigitAction.Select)
                {
                    CommitAssoc(_assocItems[di]);
                    return true;
                }
                EndAssoc();
                return false;
            }

            // 空格 / Esc：收起聯想窗（唔上屏任何東西）
            if (vk == Native.VK_SPACE || vk == Native.VK_ESCAPE)
            {
                EndAssoc();
                return true;
            }

            // 字母：收起聯想窗，交給速成照常組字。
            //
            // 【已移除】舊版這裡有一段「聯想前綴收窄」：把字母接到 _assocPending，
            // 再呼叫 Assoc.Lookup(prev, next)。但 MatchesPrefix 用
            // 「中文字.StartsWith("a-z")」去比對，永遠是 false ——
            // 所以那段程式碼從上線第一天起就從未生效，只是死碼。
            // 現在明碼寫出真正的行為：收起聯想窗 → 返回 false → 由呼叫者繼續走速成。
            return false;
        }

        /// <summary>
        /// 聯想窗「當頁」實際可選的數量。
        ///
        /// 為什麼要有這個：<c>_assocItems</c> 是引擎回傳的全部（上限 9），
        /// 而畫面一頁只顯示 <see cref="CandidateForm.AssocPerPage"/> 個。
        /// 選字判斷必須用「畫面看得到的數量」，否則兩邊的「頁」概念不一致
        /// （見 AGENTS.md：一頁 9 個）。
        /// </summary>
        private int AssocSelectableCount
        {
            get { return Math.Min(_assocItems.Count, CandidateForm.AssocPerPage); }
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
            AssocShiftDigits = true;   // 預設跟 Windows 速成：Shift+數字選字
            ContextPrev = "";
            _assocItems = new List<string>();
            _assocActive = false;

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

        /// <summary>
        /// 把「聯想窗要重畫」這件事丟回 UI 執行緒。
        ///
        /// 為什麼要做：掛鉤執行緒是系統的輸入路徑，而 <c>RaiseAssocChanged</c> 的
        /// 訂閱者會呼叫 <c>CandidateForm.ShowAssoc()</c> → <c>Show()</c>，
        /// 第一次顯示時要建立視窗（CreateWindowEx + 載入字型）。
        /// 這些全部在掛鉤執行緒同步跑的話，打字會卡，嚴重時超過
        /// LowLevelHooksTimeout（預設 300ms）→ 系統會**靜默移除掛鉤**，
        /// 輸入法整個失效而且沒有任何錯誤訊息。
        ///
        /// 注意：這裡只搬「重畫」，狀態（_assocActive / _assocItems）仍在掛鉤執行緒
        /// 同步更新，因為按鍵判斷要靠它，不可以有延遲。
        /// </summary>
        public Action<Action> UiDispatcher { get; set; }

        private void RaiseAssocChanged()
        {
            var h = AssocChanged;
            if (h == null) return;

            var dispatch = UiDispatcher;
            if (dispatch == null)
            {
                h(this, EventArgs.Empty);   // 測試 / 無 UI 環境：直接同步
                return;
            }
            try { dispatch(delegate { h(this, EventArgs.Empty); }); }
            catch { /* UI 已關閉，忽略 */ }
        }

        public void RaiseModeChanged()
        {
            var h = ModeChanged;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
