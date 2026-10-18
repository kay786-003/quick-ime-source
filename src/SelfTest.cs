// ============================================================================
//  SelfTest.cs - headless verification of dict + composer (no UI involved)
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuickIME
{
    internal static class SelfTest
    {
        private static int _fail;
        private static int _total;

        private static void Check(bool cond, string what)
        {
            _total++;
            Console.WriteLine((cond ? "PASS  " : "FAIL  ") + what);
            if (!cond) _fail++;
        }

        private static void Eq(string expect, string actual, string what)
        {
            _total++;
            bool ok = string.Equals(expect, actual, StringComparison.Ordinal);
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what +
                              (ok ? "" : "   (expect=[" + expect + "] actual=[" + actual + "])"));
            if (!ok) _fail++;
        }

        public static int Run(string dictPath)
        {
            Dict dict;
            try { dict = Dict.Load(dictPath); }
            catch (Exception ex)
            {
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
                Console.WriteLine("FAIL  無法載入字典: " + ex.Message);
                return 1;
            }
            return RunOn(dict);
        }

        public static int RunOn(Dict dict)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.WriteLine("=== 速成輸入法 自我測試 ===");
            Console.WriteLine();

            // ---- 字典完整性 ----
            int nonEmpty = 0, grand = 0;
            for (int i = 0; i < Dict.BucketCount; i++)
            {
                int cnt;
                if (i < 26) { cnt = dict.Lookup(((char)('a' + i)).ToString(), 1).Count; if (cnt > 0) nonEmpty++; }
                else
                {
                    int x = i - 26;
                    string code = new string(new[] { (char)('a' + x / 26), (char)('a' + x % 26) });
                    cnt = dict.Lookup(code, 2).Count;
                }
                grand += cnt;
            }
            // 倉頡 24 個字根 + 難(x) = 25 個字母有字；'z' 唔係字根，正常地冇單碼字。
            Check(nonEmpty == 25, "25 個字根字母有單碼字 (實際 " + nonEmpty + ")");
            Check(dict.Lookup("z", 1).Count == 0, "單碼 'z' 冇字（正確，z 非倉頡字根）");
            Check(grand == 21701, "字典總字數 = 21701 (實際 " + grand + ")");

            // ---- 已知速成碼（依微軟速成排位表）----
            Eq("日", dict.Lookup("a", 1)[0], "單碼 'a' 首選 = 日");
            Eq("人", dict.Lookup("o", 1)[0], "單碼 'o' 首選 = 人");
            Eq("中", dict.Lookup("l", 1)[0], "單碼 'l' 首選 = 中");
            Eq("明", dict.Lookup("ab", 2)[0], "碼 'ab' 首選 = 明");
            Eq("文", dict.Lookup("yk", 2)[0], "碼 'yk' 首選 = 文");

            Check(Contains(dict.Lookup("of", 2), "你"), "碼 'of' 包含「你」");
            Check(Contains(dict.Lookup("hi", 2), "我"), "碼 'hi' 包含「我」");
            Check(Contains(dict.Lookup("vd", 2), "好"), "碼 'vd' 包含「好」");
            Check(Contains(dict.Lookup("ao", 2), "是"), "碼 'ao' 包含「是」");
            Check(Contains(dict.Lookup("ha", 2), "香"), "碼 'ha' 包含「香」");
            Check(Contains(dict.Lookup("eu", 2), "港"), "碼 'eu' 包含「港」");

            // ---- 組字器 ----
            var c = new Composer(dict);
            string committed = null;
            c.Changed += delegate
            {
                if (c.PendingText != null) { committed = c.PendingText; c.ClearPending(); }
            };

            c.TypeLetter('a');
            Check(c.IsComposing, "鍵入 'a' -> 進入組字");
            Eq("a", c.Code, "組字碼 = a");
            Eq("日", c.FirstCandidate(), "'a' 首選 = 日");

            c.TypeLetter('b');
            Eq("ab", c.Code, "組字碼 = ab");
            Eq("明", c.FirstCandidate(), "'ab' 首選 = 明");

            c.CommitFirst();
            Eq("明", committed, "上屏「明」");
            Check(!c.IsComposing, "上屏後重置");

            // ---- 空格 = 下一頁（微軟速成習慣）----
            c.Reset();
            c.TypeLetter('h'); c.TypeLetter('i');
            int pagesHi = c.TotalPages;
            Check(pagesHi > 1, "'hi' 有多頁");
            int beforePage = c.Page;
            c.PressSpace();
            Check(c.IsComposing, "空格未上屏（因為要翻頁）");
            Eq((beforePage + 1).ToString(), c.Page.ToString(), "空格 = 下一頁");

            // 最後一頁再按空格 -> 上屏首選
            committed = null;
            while (c.Page < c.TotalPages - 1) c.PressSpace();
            string firstOnLast = c.FirstCandidate();
            c.PressSpace();
            Eq(firstOnLast, committed, "最後一頁按空格上屏首選");

            // 單頁時空格直接上屏
            committed = null;
            c.Reset();
            c.TypeLetter('a');           // 單碼 'a' 只有 2 個字，1 頁
            if (c.TotalPages == 1)
            {
                string a1 = c.FirstCandidate();
                c.PressSpace();
                Eq(a1, committed, "單頁時空格直接上屏");
            }

            // 連打：滿兩碼後再輸入字母 -> 上屏首選並開新碼
            committed = null;
            c.Reset();
            c.TypeLetter('a'); c.TypeLetter('b');
            c.TypeLetter('o');
            Eq("明", committed, "連打：第三個字母令「明」自動上屏");
            Eq("o", c.Code, "連打：新組字碼 = o");

            // 數字選字
            committed = null;
            c.Reset();
            c.TypeLetter('h'); c.TypeLetter('i');
            string second = c.CurrentPage().Count > 1 ? c.CurrentPage()[1] : null;
            Check(second != null, "'hi' 至少兩個候選");
            c.SelectOnPage(1);
            Eq(second, committed, "數字鍵「2」選第二候選");

            // 翻頁
            c.Reset();
            c.TypeLetter('h'); c.TypeLetter('i');
            int pages = c.TotalPages;
            Check(pages > 1, "'hi' 有多頁 (共 " + pages + " 頁)");
            string p1 = c.CurrentPage()[0];
            c.PageMove(1);
            Eq("1", c.Page.ToString(), "翻頁 -> 頁碼 1");
            Check(c.CurrentPage()[0] != p1, "第二頁與第一頁不同");

            // 退格
            c.Reset();
            c.TypeLetter('h'); c.TypeLetter('i');
            c.Backspace();
            Eq("h", c.Code, "退格刪一碼 -> h");

            // Esc 取消
            c.TypeLetter('a');
            c.Cancel();
            Check(!c.IsComposing, "Esc 取消組字");

            // 空碼查詢安全
            Check(dict.Lookup("", 0).Count == 0, "空碼查詢安全傳回空");
            Check(dict.Lookup("zz", 2).Count >= 0, "不存在的碼安全傳回");

            // ---- 中英切換鍵（ToggleGate）----
            Console.WriteLine();
            Console.WriteLine("--- 中英切換鍵（Shift）---");

            var g = new ToggleGate();
            Check(g.Sides == ShiftToggleSides.Right, "預設只有右 Shift 切換（左 Shift 留給遊戲）");
            Check(g.IsSideEnabled(ToggleGate.RightSide), "右 Shift 已啟用");
            Check(!g.IsSideEnabled(ToggleGate.LeftSide), "左 Shift 未啟用");

            // 右 Shift 單按 -> 切換
            g.ShiftDown(ToggleGate.RightSide, 1000);
            Check(g.ShiftUp(ToggleGate.RightSide, 1080), "右 Shift 單按 80ms -> 切換");

            // 左 Shift 單按 -> 不切換（遊戲長按情境）
            g.ShiftDown(ToggleGate.LeftSide, 2000);
            Check(!g.ShiftUp(ToggleGate.LeftSide, 2080), "左 Shift 單按 -> 不切換（遊戲黨重點）");

            // 右 Shift 單按但期間按過其他鍵 -> 不切換
            g.ShiftDown(ToggleGate.RightSide, 3000);
            g.NoteOtherKey();                       // 期間按了 W / A / S / D
            Check(!g.ShiftUp(ToggleGate.RightSide, 3100), "Shift 期間按過其他鍵 -> 不切換");

            // 右 Shift 長按（>700ms）-> 不切換
            g.ShiftDown(ToggleGate.RightSide, 4000);
            Check(!g.ShiftUp(ToggleGate.RightSide, 4000 + ToggleGate.ShiftTapMaxMs + 1),
                "長按 Shift 超過 " + ToggleGate.ShiftTapMaxMs + "ms -> 不切換");

            // 剛好在邊界（700ms）仍然算單按
            g.ShiftDown(ToggleGate.RightSide, 5000);
            Check(g.ShiftUp(ToggleGate.RightSide, 5000 + ToggleGate.ShiftTapMaxMs),
                "長按 " + ToggleGate.ShiftTapMaxMs + "ms（邊界）仍算單按");

            // 左右都開
            g.Sides = ShiftToggleSides.Both;
            g.ShiftDown(ToggleGate.LeftSide, 6000);
            Check(g.ShiftUp(ToggleGate.LeftSide, 6080), "設定為左右都開 -> 左 Shift 亦可切換");
            g.ShiftDown(ToggleGate.RightSide, 6200);
            Check(g.ShiftUp(ToggleGate.RightSide, 6280), "設定為左右都開 -> 右 Shift 亦可切換");

            // 兩邊都關
            g.Sides = ShiftToggleSides.None;
            g.ShiftDown(ToggleGate.RightSide, 7000);
            Check(!g.ShiftUp(ToggleGate.RightSide, 7080), "設定為關閉 -> 任何 Shift 都不切換");
            Eq("已關閉", ToggleGate.Describe(ShiftToggleSides.None), "選單描述 = 已關閉");
            Eq("左 + 右 Shift", ToggleGate.Describe(ShiftToggleSides.Both), "選單描述 = 左 + 右 Shift");

            // 同一個 Shift 重複按下不會重設計時（避免連發洗掉判定）
            g.Sides = ShiftToggleSides.Right;
            g.Reset();
            g.ShiftDown(ToggleGate.RightSide, 8000);
            g.ShiftDown(ToggleGate.RightSide, 8010);       // 系統連發
            Check(g.ShiftUp(ToggleGate.RightSide, 8070), "按住時的連發不會重設計時（仍算單按）");

            // IsShiftHeld：長按期間唔組字（遊戲跑步重點）
            g.Reset();
            Check(!g.IsShiftHeld, "放開後 IsShiftHeld = false");
            g.ShiftDown(ToggleGate.LeftSide, 9000);
            Check(g.IsShiftHeld, "按住左 Shift -> IsShiftHeld = true（期間唔組字）");
            g.ShiftUp(ToggleGate.LeftSide, 9500);
            Check(!g.IsShiftHeld, "放開後 IsShiftHeld = false");

            // 兩邊一齊按：兩邊都放開都唔會切換（避免混亂）
            g.Reset();
            g.Sides = ShiftToggleSides.Both;
            g.ShiftDown(ToggleGate.LeftSide, 10000);
            g.ShiftDown(ToggleGate.RightSide, 10020);
            bool r1 = g.ShiftUp(ToggleGate.LeftSide, 10060);
            bool r2 = g.ShiftUp(ToggleGate.RightSide, 10080);
            Check(!(r1 && r2), "兩邊 Shift 同時按 -> 唔會連環切換兩次");
            Check(!g.IsShiftHeld, "兩邊都放開後 IsShiftHeld = false");

            // ---- Ctrl+空白 攔截 ----
            Console.WriteLine();
            Console.WriteLine("--- Ctrl+空白（全形符號）---");

            var cg = new ToggleGate();
            Check(cg.CtrlSpaceBlocked, "預設攔住 Ctrl+空白");
            Check(cg.CtrlSpace(true), "Ctrl+空白 按下 -> 攔住");
            Check(cg.CtrlSpaceHeld, "按住不放（連發）-> 仍然攔住");
            Check(!cg.CtrlSpace(true), "連發事件不會重複觸發");
            Check(!cg.CtrlSpace(false), "Ctrl+空白 放開 -> 交回系統");

            cg.CtrlSpaceBlocked = false;
            Check(!cg.CtrlSpace(true), "關閉攔截 -> Ctrl+空白 完全唔干預");
            Check(!cg.CtrlSpaceHeld, "關閉攔截 -> 連發亦唔干預");

            // ---- 設定檔（settings.ini）----
            Console.WriteLine();
            Console.WriteLine("--- 設定檔持久化 ---");

            var st = new Settings();
            Check(st.ShiftSides == ShiftToggleSides.Right, "預設設定：只用右 Shift");
            Check(st.BlockCtrlSpace, "預設設定：攔住 Ctrl+空白");

            st.ShiftSides = ShiftToggleSides.Both;
            st.BlockCtrlSpace = false;
            st.ChineseMode = false;
            st.LastVersionSeen = "2.0";
            string ini = st.ToIni();
            Check(ini.Contains("ShiftSides=Both"), "寫出 ShiftSides=Both");
            Check(ini.Contains("BlockCtrlSpace=0"), "寫出 BlockCtrlSpace=0");

            var st2 = new Settings();
            st2.Apply(ini.Split(new[] { "\r\n" }, StringSplitOptions.None));
            Check(st2.ShiftSides == ShiftToggleSides.Both, "讀回 ShiftSides = Both");
            Check(!st2.BlockCtrlSpace, "讀回 BlockCtrlSpace = false");
            Check(!st2.ChineseMode, "讀回 ChineseMode = false");
            Eq("2.0", st2.LastVersionSeen, "讀回 LastVersionSeen");

            // 人手改設定檔（大小寫 / 數字 / 亂值）
            var st3 = new Settings();
            st3.Apply(new[] { "ShiftSides=left", "BlockCtrlSpace=1" });
            Check(st3.ShiftSides == ShiftToggleSides.Left, "人手寫 left（細楷）都讀得明");
            st3.Apply(new[] { "ShiftSides=NONE" });
            Check(st3.ShiftSides == ShiftToggleSides.None, "人手寫 NONE 都讀得明");
            st3.Apply(new[] { "ShiftSides=1" });
            Check(st3.ShiftSides == ShiftToggleSides.Left, "舊式數字格式 (1) = 左");
            st3.Apply(new[] { "ShiftSides=垃圾" });
            Check(st3.ShiftSides == ShiftToggleSides.Right, "亂值 -> 安全回預設（右）");

            Console.WriteLine();
            Console.WriteLine(_fail == 0
                ? "*** 全部 " + _total + " 項通過 ***"
                : ("*** 有 " + _fail + " / " + _total + " 項失敗 ***"));
            return _fail == 0 ? 0 : 1;
        }

        private static bool Contains(IList<string> l, string s)
        {
            for (int i = 0; i < l.Count; i++) if (l[i] == s) return true;
            return false;
        }
    }
}
