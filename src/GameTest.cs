// 模擬「打機長按 Shift + WASD」對 ToggleGate 的完整序列，驗證不會誤切換。
// 用法：QuickIME.exe --gametest
using System;
using System.Collections.Generic;

namespace QuickIME
{
    internal static class GameTest
    {
        public static int Run()
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            int fail = 0;

            // 情境 1：按住左 Shift 跑步 2 秒，期間按 W，之後放開
            {
                var g = new ToggleGate();   // 預設：只有右 Shift
                int t = 1000;
                int toggles = 0;
                g.ShiftDown(ToggleGate.LeftSide, t);
                // 期間按 W 三次（跑步）
                for (int i = 0; i < 3; i++) { t += 500; g.NoteOtherKey(); }
                t += 500;
                if (g.ShiftUp(ToggleGate.LeftSide, t)) toggles++;
                bool ok = toggles == 0;
                Console.WriteLine((ok ? "PASS" : "FAIL") + "  長按左 Shift 2 秒 + WASD -> 不切換");
                if (!ok) fail++;
            }

            // 情境 2：只是擦到左 Shift 一下（沒按其他鍵）
            {
                var g = new ToggleGate();
                g.ShiftDown(ToggleGate.LeftSide, 1000);
                bool t1 = g.ShiftUp(ToggleGate.LeftSide, 1050);
                Console.WriteLine((!t1 ? "PASS" : "FAIL") + "  左 Shift 輕按（85ms）-> 不切換（左邊關閉）");
                if (t1) fail++;
            }

            // 情境 3：右 Shift 輕按 = 正常切換
            {
                var g = new ToggleGate();
                g.ShiftDown(ToggleGate.RightSide, 1000);
                bool t1 = g.ShiftUp(ToggleGate.RightSide, 1060);
                Console.WriteLine((t1 ? "PASS" : "FAIL") + "  右 Shift 輕按（60ms）-> 切換");
                if (!t1) fail++;
            }

            // 情境 4：右 Shift + W（打大寫 W）-> 不切換
            {
                var g = new ToggleGate();
                g.ShiftDown(ToggleGate.RightSide, 1000);
                g.NoteOtherKey();
                bool t1 = g.ShiftUp(ToggleGate.RightSide, 1050);
                Console.WriteLine((!t1 ? "PASS" : "FAIL") + "  Shift+W 打大寫 -> 不切換");
                if (t1) fail++;
            }

            // 情境 5：Shift 按住 3 秒都沒放（掛鉤漏收 keyup 的模擬）-> 安全網校正
            {
                var g = new ToggleGate();
                g.ShiftDown(ToggleGate.RightSide, 1000);
                bool held = g.IsShiftHeld;
                g.ResetShift();                     // 掛鉤發現系統其實冇按住 -> 校正
                bool after = g.IsShiftHeld;
                bool ok = held && !after;
                Console.WriteLine((ok ? "PASS" : "FAIL") + "  漏收 keyup -> ResetShift 可校正（不會永久卡住）");
                if (!ok) fail++;
            }

            // 情境 6：Ctrl+空白 連續誤按 10 次，全部攔住
            {
                var g = new ToggleGate();
                int blocked = 0;
                for (int i = 0; i < 10; i++)
                {
                    if (g.CtrlSpace(true) || g.CtrlSpaceHeld) blocked++;
                    g.CtrlSpace(false);
                }
                bool ok = blocked == 10;
                Console.WriteLine((ok ? "PASS" : "FAIL") + "  連按 Ctrl+空白 10 次 -> 全部攔住 (" + blocked + ")");
                if (!ok) fail++;
            }

            Console.WriteLine();
            Console.WriteLine(fail == 0 ? "*** 打機情境全部通過 ***" : ("*** " + fail + " 項失敗 ***"));
            return fail == 0 ? 0 : 1;
        }
    }
}
