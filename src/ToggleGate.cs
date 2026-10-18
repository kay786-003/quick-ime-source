// ============================================================================
//  ToggleGate.cs - 中英切換 / Ctrl+空白 的判定邏輯
//  刻意做成「純邏輯、唔碰 Win32」：掛鉤只負責餵事件，判斷全部集中在這裡，
//  所以可以用 --selftest 在無視窗環境暴測（見 SelfTest.cs）。
// ============================================================================
using System;

namespace QuickIME
{
    /// <summary>邊一邊（或兩邊）的 Shift 單按可以切換中／英。</summary>
    [Flags]
    internal enum ShiftToggleSides
    {
        None = 0,
        Left = 1,
        Right = 2,
        Both = Left | Right
    }

    /// <summary>
    /// 決定「邊個 Shift 單按」才真的切換中英，以及 Ctrl+空白 要唔要攔住。
    ///
    /// 點解要咁講究：打機時長按 Shift 跑步係常態，
    /// 若果長按完放開就切換成英文，個角色就會突然唔受控。
    /// 所以要同時滿足三個條件才算「單按」：
    ///   1. 呢邊 Shift 已經設定為切換鍵（預設只有右 Shift）
    ///   2. 按住期間冇同其他鍵一齊按過（長按 Shift + WASD 會被取消）
    ///   3. 按住時間 &lt;= <see cref="ShiftTapMaxMs"/>（按住超過就當長按，唔切換）
    ///
    /// 呢個類別亦自己記住「Shift 按緊」的狀態：低階鍵盤掛鉤入面
    /// GetAsyncKeyState 有時未反映最新按鍵，唔可以靠它判斷「有冇按住 Shift」。
    /// </summary>
    internal sealed class ToggleGate
    {
        /// <summary>冇任何 Shift 按緊（或已經處理完）。</summary>
        public const int NoSide = -1;
        public const int LeftSide = 0;
        public const int RightSide = 1;
        private const int SideCount = 2;

        /// <summary>按住超過 700ms 就當「長按」，唔觸發切換。</summary>
        public const int ShiftTapMaxMs = 700;

        private readonly bool[] _sideDown = new bool[SideCount];
        private readonly bool[] _sideArmed = new bool[SideCount];
        private readonly int[] _sideTick = new int[SideCount];
        private bool _ctrlSpaceDown;          // Ctrl+空白 上次見到的狀態（做上升緣）

        /// <summary>邊幾邊 Shift 可以切換。預設只用右邊，左邊留給遊戲長按跑步。</summary>
        public ShiftToggleSides Sides { get; set; }

        /// <summary>
        /// 攔住 Ctrl+空白（預設開）：按了完全冇反應，
        /// 唔會切換全形符號、亦唔會通知系統輸入法。
        /// </summary>
        public bool CtrlSpaceBlocked { get; set; }

        public ToggleGate()
        {
            Sides = ShiftToggleSides.Right;
            CtrlSpaceBlocked = true;
        }

        /// <summary>有冇任何一邊 Shift 正按住（用於「長按期間唔組字」）。</summary>
        public bool IsShiftHeld
        {
            get { return _sideDown[LeftSide] || _sideDown[RightSide]; }
        }

        /// <summary>Shift 按下。同一邊重複按（系統連發）唔會重設計時。</summary>
        public void ShiftDown(int side, int nowMs)
        {
            if (side != LeftSide && side != RightSide) return;
            if (_sideDown[side]) return;

            // 另一邊已經按住 -> 兩邊都唔算「單按」。
            // 打機時「擦到」另一邊 Shift 是常事，寧願唔切換都唔好突然轉英文。
            int other = side == LeftSide ? RightSide : LeftSide;
            if (_sideDown[other])
            {
                _sideArmed[other] = false;
                _sideArmed[side] = false;
            }

            _sideDown[side] = true;
            _sideArmed[side] = true;
            _sideTick[side] = nowMs;
        }

        /// <summary>
        /// 有其他鍵（字母 / 數字 / 方向鍵……）按下 → 兩邊 Shift 都唔再算單按。
        /// 打機長按 Shift 跑步時一定會同時按其他鍵，所以就唔會誤切換。
        /// </summary>
        public void NoteOtherKey()
        {
            _sideArmed[LeftSide] = false;
            _sideArmed[RightSide] = false;
        }

        /// <summary>Shift 放開。回傳 true = 今次應該切換中／英。</summary>
        public bool ShiftUp(int side, int nowMs)
        {
            if (side != LeftSide && side != RightSide) return false;
            bool toggle = false;
            if (_sideDown[side])
            {
                long held = (long)nowMs - _sideTick[side];
                toggle = _sideArmed[side]
                         && IsSideEnabled(side)
                         && held >= 0 && held <= ShiftTapMaxMs;
            }
            _sideDown[side] = false;
            _sideArmed[side] = false;
            return toggle;
        }

        /// <summary>
        /// Ctrl+空白 狀態更新。回傳 true = 要攔住（唔交回系統）。
        /// 只認「按下」的上升緣，所以按住不放 / 連發都唔會重複觸發。
        /// </summary>
        public bool CtrlSpace(bool down)
        {
            bool rising = down && !_ctrlSpaceDown;
            _ctrlSpaceDown = down;
            return rising && CtrlSpaceBlocked;
        }

        /// <summary>
        /// Ctrl+空白 按緊期間（用於連發事件）：只要功能開住就一律攔住，
        /// 否則按住時系統的連發會漏出去。
        /// </summary>
        public bool CtrlSpaceHeld
        {
            get { return _ctrlSpaceDown && CtrlSpaceBlocked; }
        }

        public bool IsSideEnabled(int side)
        {
            if (side != LeftSide && side != RightSide) return false;
            ShiftToggleSides bit = side == LeftSide ? ShiftToggleSides.Left : ShiftToggleSides.Right;
            return (Sides & bit) != 0;
        }

        /// <summary>暫停輸入法 / 切換模式時清掉未完成的狀態，避免殘留。</summary>
        public void Reset()
        {
            _sideDown[LeftSide] = false;
            _sideDown[RightSide] = false;
            _sideArmed[LeftSide] = false;
            _sideArmed[RightSide] = false;
            _ctrlSpaceDown = false;
        }

        /// <summary>
        /// 只清「Shift 按住」的狀態。掛鉤偶爾會漏收 keyup
        /// （例如程式啟動時 Shift 已按住、或遊戲切走焦點），
        /// 若唔校正就會永遠當「長按 Shift」而完全打不到字。
        /// </summary>
        public void ResetShift()
        {
            _sideDown[LeftSide] = false;
            _sideDown[RightSide] = false;
            _sideArmed[LeftSide] = false;
            _sideArmed[RightSide] = false;
        }

        /// <summary>給系統匣選單 / 提示文字用。</summary>
        public static string Describe(ShiftToggleSides sides)
        {
            if (sides == ShiftToggleSides.Both) return "左 + 右 Shift";
            if (sides == ShiftToggleSides.Left) return "左 Shift";
            if (sides == ShiftToggleSides.Right) return "右 Shift";
            return "已關閉";
        }
    }
}
