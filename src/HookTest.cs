// ============================================================================
//  HookTest.cs - 按鍵流程重播測試（--hooktest）
//
//  為什麼要有這個檔：
//    v2.3 的 --selftest 有 148 項，全部通過，但「連打吞字」「z 被吞」
//    「數字超界漏字」「尾頁 Enter 送錯字」四個 bug 一個都找不到 ——
//    因為那些測試只驗 Composer 的方法，沒有照真實掛鉤的**按鍵序列**走一遍。
//
//  這裡的做法：
//    KeyboardHook.ProcessKeyDown / ProcessKeyUp 被抽成純邏輯（不碰 Win32），
//    所以可以逐個餵「使用者按了甚麼」並檢查「最後送出甚麼字」。
//    這正是使用者日常打字的單位，也是 bug 出現的地方。
//
//  用法：QuickIME.exe --hooktest
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace QuickIME
{
    internal static class HookTest
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

        /// <summary>
        /// 一個「模擬鍵盤」：把按鍵餵給 KeyboardHook，並收集真正送出的文字。
        ///
        /// 送字有兩條路：
        ///   1. <see cref="KeyboardHook.Flush"/> → <see cref="Typist.SendText"/>
        ///   2. <see cref="KeyboardHook.CommitAssoc"/> 直接呼叫 <see cref="Typist.SendText"/>
        /// 兩條都會經過 <see cref="Typist.SendSink"/>，所以只要裝上 sink 就收得到，
        /// 而且**完全不會真的把字注入使用者的視窗**。
        /// </summary>
        private sealed class Sim
        {
            private readonly Composer _composer;
            private readonly KeyboardHook _hook;
            private int _now;

            public readonly StringBuilder Sent = new StringBuilder();

            public Sim(Dict dict)
            {
                _composer = new Composer(dict);
                _hook = new KeyboardHook(_composer);
                _hook.InitAssoc(null, null);   // 預設：不啟用聯想字（純速成流程）
                _hook.AssocEnabled = false;
                _hook.AssocLearnEnabled = false;

                // 攔截所有送字：測試不可以真的打進系統
                Typist.SendSink = delegate(string s) { Sent.Append(s); };
            }

            public Composer Composer { get { return _composer; } }
            public KeyboardHook Hook { get { return _hook; } }

            /// <summary>按一個字母（a-z）。</summary>
            public void Letter(char c)
            {
                Press(Key(c));
            }

            /// <summary>
            /// 按一個鍵（VK 值）。回傳 HookResult（0=Pass / 1=Consumed / 2=CommitPending / 3=CommitPendingPass）。
            /// </summary>
            /// <param name="shiftPhysicallyDown">
            /// 模擬「系統層面 Shift 真的按住」。必須同 <see cref="KeyboardHook.Gate"/> 的狀態一致，
            /// 否則掛鉤的漏收 keyup 安全網會把 Shift 狀態重置掉，測不到「按住 Shift」那條路徑。
            /// </param>
            public int Press(uint vk, bool ctrl = false, bool shiftPhysicallyDown = false)
            {
                var k = new KeyInput
                {
                    Vk = vk,
                    Ctrl = ctrl,
                    Alt = false,
                    Win = false,
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = _now += 40,
                    ShiftPhysicallyDown = shiftPhysicallyDown
                };

                // 【重要】一定要走 CallbackCore —— 即真實掛鉤 HookCallback 用的同一條路徑。
                //
                // 舊版 Sim 自己實作「r==2||r==3 就 Flush」，於是「回傳值 → 送字 → 吞鍵」
                // 這條接線完全冇測試覆蓋：把 HookCallback 改回 v2.4 錯版，
                // 141 項測試依然全過（實測，兩次獨立審查都證實）。
                // 現在 Sim 只透過 CallbackCore 這一個入口，改壞它就會即刻 FAIL。
                int result;
                IntPtr ret = _hook.CallbackCore(k, out result);
                _swallowed = ret == KeyboardHook.Swallow;
                return result;
            }

            /// <summary>上一次按鍵有沒有被吞掉（true = 沒有交回系統）。</summary>
            public bool LastSwallowed { get { return _swallowed; } }

            private bool _swallowed;

            /// <summary>目前組字碼（沒有組字就回傳空字串）。</summary>
            public string Code { get { return _composer.IsComposing ? _composer.Code : ""; } }

            public void Clear() { Sent.Length = 0; }
            public string Take() { string s = Sent.ToString(); Sent.Length = 0; return s; }

            public void Dispose()
            {
                Typist.SendSink = null;
                _hook.DisposeAssoc();
                _hook.Dispose();
            }
        }

        /// <summary>字母鍵的 VK 就是大寫 ASCII。</summary>
        private static uint Key(char c)
        {
            return c >= 'a' && c <= 'z' ? (uint)('A' + (c - 'a')) : (uint)c;
        }

        public static int Run()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.WriteLine("=== 按鍵流程重播測試（--hooktest）===");
            Console.WriteLine();

            Dict dict;
            try
            {
                dict = Program.LoadDictAny();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL  無法載入字典: " + ex.Message);
                return 1;
            }

            TestTypeLetterFlush(dict);       // v2.3 bug 1：連打吞字
            TestZNotSwallowed(dict);         // v2.3 bug 2：z 被吞
            TestZAsThirdCode(dict);          // v2.5 bug：z 做第三碼時留死碼
            TestDigitOverflow(dict);         // v2.3 bug 3：數字超界漏字
            TestPageFirstCandidate(dict);    // v2.3 bug 4：尾頁 Enter 送錯字
            TestCtrlPunctuation(dict);       // 新功能：Ctrl + 符號 = 全形標點
            TestCtrlPunctuationTail(dict);   // 對照：Ctrl+. 出全形 / 英文模式透明
            TestCtrlShiftNotHijacked(dict);  // v2.5 bug：Ctrl+Shift+符號 被吞
            TestSpaceRule(dict);             // 空白鍵：1 個候選上屏、2 個以上不上屏
            TestSpaceStillCycles(dict);      // 護欄：多頁時空白鍵永遠不上屏
            TestHookResultContract(dict);    // 護欄：HookResult 四種值的語意
            TestHookResultWiring(dict);      // 護欄：回傳值 → Flush/吞鍵 的接線
            TestShiftGateDesync(dict);       // 護欄：Gate 失步時 Shift 仍然要被尊重
            TestIsMappedScope();             // 護欄：Ctrl+Shift+符號 的例外範圍

            Console.WriteLine();
            Console.WriteLine(_fail == 0
                ? "*** 按鍵流程全部 " + _total + " 項通過 ***"
                : ("*** 有 " + _fail + " / " + _total + " 項失敗 ***"));
            return _fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------
        //  v2.3 bug 1：連打吞字
        // ------------------------------------------------------------------
        private static void TestTypeLetterFlush(Dict dict)
        {
            Console.WriteLine("--- 連打（v2.3 bug：第二個字被覆蓋）---");

            // ab 之後打 o：TypeLetter 會把「明」上屏並開新碼 o
            var s = new Sim(dict);
            try
            {
                s.Letter('a'); s.Letter('b');
                Eq("明", s.Composer.FirstCandidate(), "'ab' 首選 = 明");

                s.Letter('o');   // 連打：上屏「明」，開新碼 o
                Eq("明", s.Take(), "連打 a b o -> 立刻送出「明」（舊版會吞掉）");
                Eq("o", s.Code, "連打之後新組字碼 = o");

                // 再打 f 組成 of，然後用數字選字
                s.Letter('f');
                Eq("of", s.Code, "再打 f -> 組字碼 = of");
                s.Press((uint)'1');
                string got = s.Take();
                Check(got.Length == 1 && got != "明", "選字第 1 個 -> 送出 of 的首選（不是「明」）");
            }
            finally { s.Dispose(); }

            // 連續連打：a b o f 1 -> 應該送出「明」+「伙」兩個字
            var s2 = new Sim(dict);
            try
            {
                s2.Letter('a'); s2.Letter('b');
                s2.Letter('o');            // 送出「明」
                s2.Letter('f');            // 組字 of
                s2.Press((uint)'1');       // 送出 of 首選
                string all = s2.Take();
                Eq("2", all.Length.ToString(), "連打 a b o f 1 -> 共送出 2 個字（舊版只送出 1 個）");
                Eq("明", all.Substring(0, 1), "第一個字是「明」");
            }
            finally { s2.Dispose(); }

            // Count = 0（滿兩碼後再打字母，但沒候選）也要正常
            var s3 = new Sim(dict);
            try
            {
                s3.Letter('a'); s3.Letter('b');
                s3.Composer.Reset();
                s3.Letter('a'); s3.Letter('b');
                Eq("ab", s3.Code, "Reset 之後重新組字正常");
            }
            finally { s3.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  v2.3 bug 2：z 被靜默吞掉
        // ------------------------------------------------------------------
        private static void TestZNotSwallowed(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- z 不應被吞（z 不是字根，也沒有特殊用途）---");

            var s = new Sim(dict);
            try
            {
                // 空碼時按 z：TypeLetter 回傳 false -> 應該 Pass（交回系統）
                int r = s.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)'Z',
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1000
                });
                Eq("0", r.ToString(), "空碼按 z -> 交回系統（Pass=0），不會被吞");
                Eq("", s.Code, "空碼按 z -> 不進入組字");

                // 組字中按 z：也應該 Pass
                s.Letter('a');
                Eq("a", s.Code, "先打 a 組字");
                int r2 = s.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)'Z',
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1100
                });
                Eq("0", r2.ToString(), "組字中按 z -> 交回系統（Pass=0）");
                Eq("a", s.Code, "組字中按 z -> 原來的組字碼不變");
            }
            finally { s.Dispose(); }

            // 對照：有效的字母仍然要被消耗
            var s2 = new Sim(dict);
            try
            {
                int r = s2.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)'A',
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1000
                });
                Eq("1", r.ToString(), "有效字母 a -> 已消耗（Consumed=1）");
                Eq("a", s2.Code, "有效字母 a -> 進入組字");
            }
            finally { s2.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  v2.3 bug 3：數字超界漏字
        // ------------------------------------------------------------------
        private static void TestDigitOverflow(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- 數字超出當頁（舊版會讓數字漏進文件）---");

            // 單碼 'a' 只有 2 個候選，按 9 應該被吞掉（不選字、也不漏字）
            var s = new Sim(dict);
            try
            {
                s.Letter('a');
                int onPage = s.Composer.CurrentPage().Count;
                Check(onPage < 9, "單碼 'a' 的候選少於 9 個（實際 " + onPage + "）");

                int r = s.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)'9',
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1000
                });
                Eq("1", r.ToString(), "按超出範圍的 9 -> 已消耗（不會漏進文件）");
                Eq("", s.Take(), "按超出範圍的 9 -> 沒有送出任何字");
                Eq("a", s.Code, "按超出範圍的 9 -> 組字狀態保持（不會偷偷上屏）");
            }
            finally { s.Dispose(); }

            // '0' 不選字：組字中按 0 應該被吞掉，不是選第 10 個
            var s2 = new Sim(dict);
            try
            {
                s2.Letter('h'); s2.Letter('i');   // hi 有多頁
                int r = s2.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)'0',
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1000
                });
                Eq("1", r.ToString(), "組字中按 0 -> 已消耗（0 不選字）");
                Eq("", s2.Take(), "組字中按 0 -> 沒有送出任何字");
                Eq("hi", s2.Code, "組字中按 0 -> 仍然在組字");
            }
            finally { s2.Dispose(); }

            // 正常選字仍然要工作
            var s3 = new Sim(dict);
            try
            {
                s3.Letter('h'); s3.Letter('i');
                string second = s3.Composer.CurrentPage()[1];
                s3.Press((uint)'2');
                Eq(second, s3.Take(), "1-9 正常選字：按 2 -> 送出當頁第 2 個");
            }
            finally { s3.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  v2.3 bug 4：尾頁 Enter 送第 1 頁的字
        // ------------------------------------------------------------------
        private static void TestPageFirstCandidate(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- 尾頁上屏要用「當前頁」的第 1 個 ---");

            var c = new Composer(dict);
            c.TypeLetter('h'); c.TypeLetter('i');
            Check(c.TotalPages > 1, "'hi' 有多頁（" + c.TotalPages + " 頁）");

            string page1First = c.FirstCandidate();
            c.PageSelect(c.TotalPages - 1);
            string lastPageFirst = c.CurrentPage()[0];

            Check(lastPageFirst != page1First, "尾頁第 1 個與第 1 頁第 1 個不同（測試才有意義）");
            Eq(lastPageFirst, c.FirstCandidate(),
                "FirstCandidate() 在尾頁 = 當前頁第 1 個（舊版會回傳第 1 頁的字）");
            Eq(page1First, c.FirstCandidateOverall(), "FirstCandidateOverall() 才是整份清單第 1 個");

            // 走真實掛鉤：尾頁按 Enter 應該送出當前頁的字
            var s = new Sim(dict);
            try
            {
                s.Letter('h'); s.Letter('i');
                for (int i = 0; i < 3; i++) s.Press((uint)Native.VK_NEXT);
                string expected = s.Composer.CurrentPage()[0];

                // Enter = VK_RETURN，不是字母 -> 先上屏首選再放行
                s.Press((uint)Native.VK_RETURN);
                Eq(expected, s.Take(), "尾頁按 Enter -> 送出當前頁的第 1 個（不是第 1 頁的）");
            }
            finally { s.Dispose(); }

            // 中途頁也一樣
            var s2 = new Sim(dict);
            try
            {
                s2.Letter('h'); s2.Letter('i');
                s2.Press((uint)Native.VK_NEXT);   // 第 2 頁
                string expected = s2.Composer.CurrentPage()[0];
                s2.Press((uint)Native.VK_RETURN);
                Eq(expected, s2.Take(), "第 2 頁按 Enter -> 送出第 2 頁的第 1 個");
            }
            finally { s2.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  新功能：Ctrl + 符號鍵 = 全形標點
        // ------------------------------------------------------------------
        private static void TestCtrlPunctuation(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- Ctrl + 符號鍵 -> 全形標點（使用者習慣）---");

            var s = new Sim(dict);
            try
            {
                s.Press((uint)Native.VK_OEM_COMMA, true);
                Eq("，", s.Take(), "Ctrl+, -> 「，」");

                s.Press((uint)Native.VK_OEM_PERIOD, true);
                Eq("。", s.Take(), "Ctrl+. -> 「。」");

                s.Press((uint)Native.VK_OEM_1, true);
                Eq("；", s.Take(), "Ctrl+; -> 「；」");

                s.Press((uint)Native.VK_OEM_7, true);
                Eq("：", s.Take(), "Ctrl+' -> 「：」");

                s.Press((uint)Native.VK_OEM_4, true);
                Eq("「", s.Take(), "Ctrl+[ -> 「「」");

                s.Press((uint)Native.VK_OEM_6, true);
                Eq("」", s.Take(), "Ctrl+] -> 「」」");

                s.Press((uint)Native.VK_OEM_5, true);
                Eq("、", s.Take(), "Ctrl+\\ -> 「、」");

                // ? 與 ! 必須保持半形：不能被接管
                string slash = FullWidthPunct.Lookup(Native.VK_OEM_2, false);
                Check(slash == null, "Ctrl+/ 不接管（? 保持半形）");
                string slashShift = FullWidthPunct.Lookup(Native.VK_OEM_2, true);
                Check(slashShift == null, "Ctrl+Shift+/ 不接管（? 保持半形）");

                // 數字與字母不能被符號表接管
                Check(FullWidthPunct.Lookup((uint)'1', false) == null, "符號表不含數字鍵");
                Check(FullWidthPunct.Lookup((uint)'A', false) == null, "符號表不含字母鍵");
            }
            finally { s.Dispose(); }

            // 組字中按 Ctrl+, -> 先上屏首選，再出「，」
            var s2 = new Sim(dict);
            try
            {
                s2.Letter('a'); s2.Letter('b');
                string first = s2.Composer.FirstCandidate();
                s2.Press((uint)Native.VK_OEM_COMMA, true);
                string got = s2.Take();
                Eq(first + "，", got, "組字中按 Ctrl+, -> 先上屏「" + first + "」再出「，」");
            }
            finally { s2.Dispose(); }

            // 沒有 Ctrl 時，, 仍然是普通半形逗號（不接管）
            var s3 = new Sim(dict);
            try
            {
                int r = s3.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)Native.VK_OEM_COMMA,
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1000
                });
                Eq("0", r.ToString(), "沒有 Ctrl 的 , -> 交回系統（半形逗號）");
            }
            finally { s3.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  護欄：空白鍵永遠不上屏（AGENTS.md 硬規則 1）
        // ------------------------------------------------------------------
        private static void TestSpaceStillCycles(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- 護欄：空白鍵只翻頁，永遠不上屏 ---");

            var s = new Sim(dict);
            try
            {
                s.Letter('h'); s.Letter('i');
                int pages = s.Composer.TotalPages;
                Check(pages > 1, "'hi' 有多頁，空白鍵測試才有意義");

                // 一直按空白，掃完所有頁再繞一圈，期間不應送出任何字
                bool anySent = false;
                for (int i = 0; i < pages + 2; i++)
                {
                    s.Press((uint)Native.VK_SPACE);
                    if (s.Take().Length != 0) anySent = true;
                }
                Check(!anySent, "連按空白 " + (pages + 2) + " 次 -> 全程沒有上屏任何字");
                Check(s.Composer.IsComposing, "連按空白之後仍然在組字");

                int expectedPage = (pages + 2) % pages;
                Eq(expectedPage.ToString(), s.Composer.Page.ToString(),
                    "尾頁再按空白 -> 繞返第 1 頁（不是上屏）");
            }
            finally { s.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  空白鍵真正規則（用家 2026-10 實測微軟速成）
        //    * 只有 1 個候選 → 空白上屏（冇嘢可揀）
        //    * 2 個或以上候選 → 空白永遠不上屏（即使只有一頁）
        // ------------------------------------------------------------------
        private static void TestSpaceRule(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- 空白鍵：1 個候選上屏、2 個以上一律唔上屏 ---");

            // 只有 1 個候選的單碼（'b' = 月）。字典裡這種碼有 23 個。
            var s = new Sim(dict);
            try
            {
                Check(dict.Lookup("b", 1).Count == 1,
                    "單碼 'b' 只有 1 個候選（實測基準）");
                s.Letter('b');
                Eq("月", s.Composer.FirstCandidate(), "'b' 首選 = 月");
                s.Press((uint)Native.VK_SPACE);
                Eq("月", s.Take(), "只有 1 個候選時按空白 -> 上屏「月」（同微軟速成一致）");
                Check(!s.Composer.IsComposing, "上屏之後不再組字");
            }
            finally { s.Dispose(); }

            // 2 個候選、只有一頁（'a' = 日/曰）。舊版這裡會直接打出「日」。
            var s2 = new Sim(dict);
            try
            {
                Check(dict.Lookup("a", 1).Count == 2,
                    "單碼 'a' 有 2 個候選（日、曰）");
                s2.Letter('a');
                Check(s2.Composer.TotalPages == 1, "'a' 只有一頁");
                s2.Press((uint)Native.VK_SPACE);
                Eq("", s2.Take(), "2 個候選時按空白 -> 不上屏（舊版會直接打出「日」）");
                Check(s2.Composer.IsComposing, "2 個候選時按空白 -> 仍然在組字");
                Eq("0", s2.Composer.Page.ToString(), "2 個候選時按空白 -> 頁碼不變（冇頁可翻）");

                // 連按多次都不應該上屏
                for (int i = 0; i < 5; i++) s2.Press((uint)Native.VK_SPACE);
                Eq("", s2.Take(), "連按空白 5 次 -> 依然沒有上屏任何字");

                // 這個時候按數字鍵才選得到
                s2.Press((uint)'2');
                Eq("曰", s2.Take(), "2 個候選時按 2 -> 選第 2 個「曰」");
            }
            finally { s2.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  v2.5 bug：z 做「第三碼」時留死碼
        //
        //  v2.4 修好了「空碼按 z」，但漏了「滿兩碼之後按 z」：
        //  TypeLetter 會先 Commit 首選、把碼清空，然後才檢查 z ——
        //  結果 z 被拒但已經 append 了一個 z，留下「碼 = z、零候選」的死碼，
        //  下一個字母就變成 `za`，打出「　」而不是用家想要的字。
        // ------------------------------------------------------------------
        private static void TestZAsThirdCode(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- z 做第三碼（打完兩碼再按 z）---");

            var s = new Sim(dict);
            try
            {
                s.Letter('a'); s.Letter('b');
                Eq("明", s.Composer.FirstCandidate(), "'ab' 首選 = 明");

                int r = s.Press((uint)'Z');
                Eq("明", s.Take(), "ab 之後按 z -> 依然要送出「明」（不能因為 z 被拒而丟字）");
                Eq("3", r.ToString(), "ab 之後按 z -> CommitPendingPass（送字 + 放行 z）");
                Eq("", s.Code, "ab 之後按 z -> 不能留下任何組字碼（舊版會留下死碼 z）");

                // 關鍵：之後打 a 應該係「a」而唔係「za」
                s.Letter('a');
                Eq("a", s.Code, "接著打 a -> 組字碼 = a（舊版會變成 za，打出「　」）");
                s.Press((uint)'1');
                Eq("日", s.Take(), "接著按 1 -> 送出「日」（舊版會送出「　」）");
            }
            finally { s.Dispose(); }

            // 對照：第一碼 z 的行為不變
            var s2 = new Sim(dict);
            try
            {
                int r = s2.Press((uint)'Z');
                Eq("0", r.ToString(), "空碼按 z -> Pass（原行為不變）");
                Eq("", s2.Code, "空碼按 z -> 不進入組字");
            }
            finally { s2.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  v2.5 bug：Ctrl+Shift+符號 被當成全形標點
        // ------------------------------------------------------------------
        private static void TestCtrlShiftNotHijacked(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- Ctrl+Shift+符號 不應被當成全形 ---");

            // 表本身不應該有 Shift 版本
            Check(FullWidthPunct.Lookup(Native.VK_OEM_PERIOD, true) == null,
                "Ctrl+Shift+. 不接管（用家想打 >）");
            Check(FullWidthPunct.Lookup(Native.VK_OEM_COMMA, true) == null,
                "Ctrl+Shift+, 不接管（用家想打 <）");
            Check(FullWidthPunct.Lookup(Native.VK_OEM_1, true) == null,
                "Ctrl+Shift+; 不接管（用家想打 :）");

            // 走真實掛鉤：Ctrl+Shift+. 應該 Pass，而且不上屏任何嘢
            var s = new Sim(dict);
            try
            {
                // 先真的按住 Shift（Gate 狀態 + 系統狀態兩邊都要一致）
                s.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)Native.VK_RSHIFT,
                    ShiftSide = ToggleGate.RightSide,
                    NowMs = 0,
                    ShiftPhysicallyDown = true
                });
                Check(s.Hook.Gate.IsShiftHeld, "測試前提：Shift 已按住");

                int r = s.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: true);
                Eq("0", r.ToString(), "Ctrl+Shift+. -> 交回系統（舊版會變成「。」）");
                Eq("", s.Take(), "Ctrl+Shift+. -> 沒有上屏任何字");
            }
            finally { s.Dispose(); }

            // 組字中按 Ctrl+Shift+.：要「先上屏首選，再放行 >」，而且不能殘留組字
            var sc = new Sim(dict);
            try
            {
                sc.Letter('a'); sc.Letter('b');
                string first = sc.Composer.FirstCandidate();
                sc.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)Native.VK_RSHIFT,
                    ShiftSide = ToggleGate.RightSide,
                    NowMs = 0,
                    ShiftPhysicallyDown = true
                });
                int r = sc.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: true);
                Eq("0", r.ToString(), "組字中 Ctrl+Shift+. -> 仍然交回系統（> 打得出來）");
                Eq(first, sc.Take(), "組字中 Ctrl+Shift+. -> 先上屏當前首選（" + first + "）");
                Eq("", sc.Code, "組字中 Ctrl+Shift+. -> 組字狀態已清（不會殘留死碼）");
            }
            finally { sc.Dispose(); }

            // 對照：組字中 Ctrl+C 不應該上屏（系統快捷鍵要保留）
            var sv = new Sim(dict);
            try
            {
                sv.Letter('a'); sv.Letter('b');
                int r = sv.Press((uint)'C', true);
                Eq("0", r.ToString(), "組字中 Ctrl+C -> 交回系統（複製快捷鍵）");
                Eq("", sv.Take(), "組字中 Ctrl+C -> 不上屏任何字（保留系統快捷鍵）");
            }
            finally { sv.Dispose(); }

            // 【真實風險】組字中「按住 Shift」再按 Ctrl+字母 / Esc / Tab / 數字。
            // 這條路徑才會行到「長按 Shift 期間」那段（Keyboard.cs 的
            // `if (_gate.IsShiftHeld || k.ShiftPhysicallyDown)`），
            // 也是 `!k.Ctrl || isPunctKey` 這個守衛真正發揮作用的地方。
            // 舊版測試冇按住 Shift，所以完全行唔到那段 —— 就算把守衛刪走都全過（實測）。
            //
            // 【一定要每個情境開新 Sim】
            // 之前四組共用同一個 Sim，第一組 Ctrl+Shift+C 一旦（在改壞版本下）上屏清空了組字，
            // 之後 Esc / Tab / 1 就因為「唔再組字」而走唔到被改壞那段，
            // 六條斷言完全冇承載力（實測：刪守衛只 FAIL 3 條，Esc/Tab/1 零 FAIL）。
            AssertCtrlShiftWhileComposing(dict, (uint)'C', "字母 C");
            AssertCtrlShiftWhileComposing(dict, (uint)Native.VK_ESCAPE, "Esc");
            AssertCtrlShiftWhileComposing(dict, (uint)Native.VK_TAB, "Tab");
            AssertCtrlShiftWhileComposing(dict, (uint)'1', "數字 1");
            AssertCtrlShiftWhileComposing(dict, (uint)'0', "數字 0");

            // 對照組：符號鍵就要上屏（證明例外只優待符號鍵）
            var sp = new Sim(dict);
            try
            {
                sp.Letter('a'); sp.Letter('b');
                string first = sp.Composer.FirstCandidate();
                HoldShift(sp);
                sp.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: true);
                Eq(first, sp.Take(), "對照：組字中 Ctrl+Shift+. 仍然要上屏首選（" + first + "）");
                Eq("", sp.Code, "對照：組字中 Ctrl+Shift+. -> 組字狀態已清");
            }
            finally { sp.Dispose(); }
        }

        /// <summary>在 Sim 上真的按住右 Shift（Gate 與系統狀態一致）。</summary>
        private static void HoldShift(Sim s)
        {
            s.Hook.ProcessKeyDown(new KeyInput
            {
                Vk = (uint)Native.VK_RSHIFT,
                ShiftSide = ToggleGate.RightSide,
                NowMs = 0,
                ShiftPhysicallyDown = true
            });
        }

        /// <summary>
        /// 組字中按住 Shift 再按 Ctrl+<paramref name="vk"/>，必須「交回系統、唔上屏、組字碼不變」。
        /// 每次都開新 Sim，確保走得到「長按 Shift」那段守衛。
        /// </summary>
        private static void AssertCtrlShiftWhileComposing(Dict dict, uint vk, string name)
        {
            var s = new Sim(dict);
            try
            {
                s.Letter('a'); s.Letter('b');
                Eq("ab", s.Code, "測試前提：組字碼 = ab");
                HoldShift(s);
                Check(s.Hook.Gate.IsShiftHeld, "測試前提：Shift 已按住（" + name + "）");

                int r = s.Press(vk, true, shiftPhysicallyDown: true);
                Eq("0", r.ToString(), "組字中 Ctrl+Shift+" + name + " -> 交回系統");
                Eq("", s.Take(), "組字中 Ctrl+Shift+" + name + " -> 唔可以偷偷上屏");
                Eq("ab", s.Code, "組字中 Ctrl+Shift+" + name + " -> 組字碼保持 ab");
            }
            finally { s.Dispose(); }
        }


        // ------------------------------------------------------------------
        //  新功能：Ctrl + 符號鍵 = 全形標點（續）
        // ------------------------------------------------------------------
        private static void TestCtrlPunctuationTail(Dict dict)
        {
            // 對照：沒有 Shift 的 Ctrl+. 仍然要出「。」
            var s2 = new Sim(dict);
            try
            {
                s2.Press((uint)Native.VK_OEM_PERIOD, true);
                Eq("。", s2.Take(), "Ctrl+. （沒有 Shift）-> 「。」（原功能不受影響）");
            }
            finally { s2.Dispose(); }

            // 英文模式：Ctrl+, 應該完全透明
            var s3 = new Sim(dict);
            try
            {
                s3.Hook.ChineseMode = false;
                int r = s3.Press((uint)Native.VK_OEM_COMMA, true);
                Eq("0", r.ToString(), "英文模式 Ctrl+, -> 交回系統（不應出全形）");
                Eq("", s3.Take(), "英文模式 Ctrl+, -> 沒有上屏任何字");
            }
            finally { s3.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  護欄：HookResult 的語意契約
        //
        //  為什麼要單獨測這個：<see cref="Sim"/> 為了重播流程，自己實作了
        //  「遇到 CommitPending / CommitPendingPass 就 Flush」這條規則。
        //  好處是測得到「字有沒有真的送出去」，壞處是 ——
        //  就算把 HookCallback 的消費判斷改壞（例如只認 Consumed），
        //  上面所有測試依然會全部通過。這正是 v2.4 遺留下來的
        //  「按數字選字，數字漏進文件」的成因：ProcessKeyDown 回傳
        //  CommitPending，但 HookCallback 只當 Consumed 才吞鍵。
        //
        //  所以這裡直接驗「ProcessKeyDown 回傳值的語意」，
        //  任何人改動 HookCallback 的對應關係都會被這幾條擋住。
        // ------------------------------------------------------------------
        private static void TestHookResultContract(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- HookResult 語意契約（防止 ProcessKeyDown 與 HookCallback 脫鈎）---");

            // 選字：必須「送字」而且「吞鍵」，否則數字會漏進文件
            var s = new Sim(dict);
            try
            {
                s.Letter('h'); s.Letter('i');
                int r = s.Press((uint)'2');
                Eq("2", r.ToString(), "數字選字 -> CommitPending（會被 Flush 而且吞鍵）");
                Eq("么", s.Take(), "數字選字 -> 只送出「么」，沒有數字漏出");
            }
            finally { s.Dispose(); }

            // 空白（單候選上屏）：同樣要「送字 + 吞鍵」
            var s2 = new Sim(dict);
            try
            {
                s2.Letter('b');
                int r = s2.Press((uint)Native.VK_SPACE);
                Eq("2", r.ToString(), "單候選空白 -> CommitPending（會被 Flush 而且吞鍵）");
                Eq("月", s2.Take(), "單候選空白 -> 只送出「月」，空白鍵本身沒有漏出");
            }
            finally { s2.Dispose(); }

            // 連打：要「送字 + 吞鍵」
            var s3 = new Sim(dict);
            try
            {
                s3.Letter('a'); s3.Letter('b');
                int r = s3.Press((uint)'O');
                Eq("1", r.ToString(), "連打（ab 之後打 o）-> Consumed（送字由 Flush 負責）");
                Eq("明", s3.Take(), "連打 -> 送出「明」");
            }
            finally { s3.Dispose(); }

            // 只有「送字 + 放行」才用 CommitPendingPass
            var s4 = new Sim(dict);
            try
            {
                s4.Letter('a'); s4.Letter('b');
                int r = s4.Press((uint)'Z');
                Eq("3", r.ToString(), "ab 之後按 z -> CommitPendingPass（送字但放行 z）");
            }
            finally { s4.Dispose(); }

            // 純放行：不可以送任何字
            var s5 = new Sim(dict);
            try
            {
                int r = s5.Press((uint)'Z');
                Eq("0", r.ToString(), "空碼按 z -> Pass");
                Eq("", s5.Take(), "空碼按 z -> 沒有送字");
            }
            finally { s5.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  護欄：HookResult → Flush / 吞鍵 的接線
        //
        //  【為什麼一定要有這一組】
        //  上面所有測試都經 <see cref="Sim"/>，而 Sim 自己實作了
        //  「遇到 CommitPending / CommitPendingPass 就 Flush」這條規則。
        //  結果就係：就算把 <see cref="KeyboardHook.ShouldSwallowKey"/> 或
        //  <see cref="KeyboardHook.ShouldFlush"/> 改壞，全部測試依然全過 ——
        //  v2.4 的「按數字選字但數字漏進文件」正是這樣活了下來的。
        //  所以這裡直接驗那兩個純函式的真值表，不經 Sim。
        // ------------------------------------------------------------------
        private static void TestHookResultWiring(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- HookResult → Flush / 吞鍵 的真值表 ---");

            // 四種值的期望：(會不會送字, 會不會吞鍵)
            //   Pass              0  送 no / 吞 no
            //   Consumed          1  送 no / 吞 YES
            //   CommitPending     2  送 YES / 吞 YES   ← 選字；數字絕不可以漏
            //   CommitPendingPass 3  送 YES / 吞 no    ← 送字但按鍵照樣打出來
            AssertResult(KeyboardHook.HookResult.Pass,              false, false, "Pass");
            AssertResult(KeyboardHook.HookResult.Consumed,          false, true,  "Consumed");
            AssertResult(KeyboardHook.HookResult.CommitPending,     true,  true,  "CommitPending（選字）");
            AssertResult(KeyboardHook.HookResult.CommitPendingPass, true,  false, "CommitPendingPass（送字+放行）");

            // 「送字」一定要涵蓋「選字」那條路徑，否則數字會漏進文件
            Check(KeyboardHook.ShouldSwallowKey(KeyboardHook.HookResult.CommitPending),
                "CommitPending 一定要吞鍵（唔係就會出現「么2」）");
            Check(KeyboardHook.ShouldFlush(KeyboardHook.HookResult.CommitPending),
                "CommitPending 一定要送字（唔係選字就冇反應）");

            // 只有 CommitPendingPass 係「送字但唔吞鍵」
            Check(!KeyboardHook.ShouldSwallowKey(KeyboardHook.HookResult.CommitPendingPass),
                "CommitPendingPass 唔可以吞鍵（否則 z 打完冇反應）");
            Check(KeyboardHook.ShouldFlush(KeyboardHook.HookResult.CommitPendingPass),
                "CommitPendingPass 一定要送字（否則「明」會被吞）");

            // 接線：ApplyHookResult 真的會把字送出去
            var s = new Sim(dict);
            try
            {
                s.Letter('h'); s.Letter('i');
                int r = s.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)'2',
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = 1000
                });
                Eq("2", r.ToString(), "hi 按 2 -> CommitPending");
                s.Hook.ApplyHookResult(r);
                Eq("么", s.Take(), "ApplyHookResult(CommitPending) -> 真的送出「么」");
                Check(KeyboardHook.ShouldSwallowKey(r), "而且個 2 會被吞（唔會漏進文件）");
            }
            finally { s.Dispose(); }

            // 【最關鍵】經真正的接線（Sim.Press → KeyboardHook.CallbackCore）驗一次，
            // 確保「回傳值 → 送字 → 吞鍵」三件事真的一齊發生。
            // 這條才會擋住「CallbackCore 接線被改壞」的回歸。
            var w = new Sim(dict);
            try
            {
                w.Letter('h'); w.Letter('i');
                w.Clear();
                w.Press((uint)'2');
                Eq("么", w.Take(), "經接線：hi 按 2 -> 送出「么」");
                Check(w.LastSwallowed, "經接線：hi 按 2 -> 數字 2 被吞（唔會漏進文件）");
            }
            finally { w.Dispose(); }

            // 經接線：CommitPendingPass 要送字但唔吞鍵
            var w2 = new Sim(dict);
            try
            {
                w2.Letter('a'); w2.Letter('b');
                w2.Clear();
                w2.Press((uint)'Z');
                Eq("明", w2.Take(), "經接線：ab 按 z -> 送出「明」");
                Check(!w2.LastSwallowed, "經接線：ab 按 z -> z 仍然交回系統（唔會被吞）");
            }
            finally { w2.Dispose(); }

            // 經接線：Pass 要放行而且唔送字
            var w3 = new Sim(dict);
            try
            {
                w3.Clear();
                w3.Press((uint)'Z');
                Eq("", w3.Take(), "經接線：空碼按 z -> 冇送字");
                Check(!w3.LastSwallowed, "經接線：空碼按 z -> 放行");
            }
            finally { w3.Dispose(); }

            // 【最後一環】CallbackCore 的結果 → 掛鉤最終回傳值 的對應關係。
            //
            // 這是 HookCallback 對外的最後一行。之前它寫死在 HookCallback 裡面
            // （要真掛鉤才執行到），所以「吞鍵結果被當成放行」這種改動測試碰不到。
            // 現在 ConvertCoreResult 是純函式（除了 CallNextHookEx），可以直接驗兩邊。
            var cw = new Sim(dict);
            try
            {
                // 吞鍵 → 回 (IntPtr)1
                IntPtr swallowed = cw.Hook.ConvertCoreResult(KeyboardHook.Swallow, 0, IntPtr.Zero, IntPtr.Zero);
                Eq("1", swallowed.ToString(), "CallbackCore 回 Swallow -> 掛鉤回 1（吞掉按鍵）");

                // 放行 → 必須回 0（不可以只斷言「唔係 1」，否則回 2 / 垃圾值都會 PASS）
                IntPtr passed = cw.Hook.ConvertCoreResult(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
                Eq("0", passed.ToString(), "CallbackCore 回非 Swallow -> 掛鉤回 0（放行）");
            }
            finally { cw.Dispose(); }

            // 【最外層】用假的 Win32 接縫，真正驅動 OnHookMessage 走完整條回呼。
            //
            // 這條是整個測試套件最關鍵的一條：之前所有測試都只到 ProcessKeyDown，
            // 從沒有一條會執行到「掛鉤讀完鍵盤事件之後的決策」。
            // 所以把 OnHookMessage 改回 v2.4 錯版，159 項依然全過（實測）。
            TestRealCallbackPath(dict);
        }

        /// <summary>
        /// 用假的 Win32 接縫驅動真正的掛鉤外殼 <see cref="KeyboardHook.InvokeHookCallback"/>，
        /// 驗證「真實掛鉤那條路」的決策與吞鍵結果。
        ///
        /// 【為什麼要走 InvokeHookCallback 而唔係直接叫 OnHookMessage】
        /// 只叫 OnHookMessage 的話，就算把 HookCallback 外殼改成永遠放行（＝輸入法失效），
        /// 全套測試依然全過（實測）。必須經外殼才測得到「外殼有冇正確委派」。
        ///
        /// 【為什麼每個情境都開新 hook】
        /// 共用同一個 hook 會令情境序列相依：前一個情境一旦上了屏、清了組字，
        /// 後面的情境就會「因為唔再組字」而走唔到目標分支，斷言變成假過。
        /// 上一輪就係咁令 Esc/Tab/1 三組空轉（實測）。
        /// </summary>
        private static void TestRealCallbackPath(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- 真實掛鉤回呼路徑（假 Win32 接縫 + 真 HookCallback 外殼）---");

            // (1) 按數字選字：必須送出「么」而且吞掉「2」
            CallbackCase(dict, "hi+2 選字",
                new[] { 'h', 'i' }, (uint)'2',
                "么", "1",
                "送出「么」", "吞掉數字（數字唔會漏進文件）");

            // (2) 單候選 + 空白：必上屏而且必吞鍵（另一種 CommitPending）
            CallbackCase(dict, "b+空白",
                new[] { 'b' }, (uint)Native.VK_SPACE,
                "月", "1",
                "送出「月」", "吞掉空白鍵");

            // (3) 多候選 + 空白：只翻頁，唔送字，但吞鍵（Consumed）
            CallbackCase(dict, "hi+空白",
                new[] { 'h', 'i' }, (uint)Native.VK_SPACE,
                "", "1",
                "唔送字（只翻頁）", "吞掉空白鍵");

            // (4) CommitPendingPass：送字但放行（ab + z）
            CallbackCase(dict, "ab+z",
                new[] { 'a', 'b' }, (uint)'Z',
                "明", "0",
                "送出「明」", "唔吞鍵（z 照打）");

            // (5) 純 Pass：空碼按 z，唔送字而且放行
            CallbackCase(dict, "空碼+z",
                new char[0], (uint)'Z',
                "", "0",
                "唔送字", "放行");

            // (6) 上屏但放行：Enter 換行
            CallbackCase(dict, "ab+Enter",
                new[] { 'a', 'b' }, (uint)Native.VK_RETURN,
                "明", "0",
                "先上屏「明」", "Enter 交回系統（換行）");
        }

        /// <summary>
        /// 一個獨立情境：新開 hook（避免序列相依），用假 Win32 餵鍵，
        /// 經真正的 <see cref="KeyboardHook.InvokeHookCallback"/> 走完整條掛鉤路徑。
        /// </summary>
        /// <param name="expectSent">期望送出的文字。</param>
        /// <param name="expectRet">期望掛鉤回傳值（"1" = 吞鍵 / "0" = 放行）。</param>
        private static void CallbackCase(Dict dict, string name, char[] prefix, uint vk,
                                         string expectSent, string expectRet,
                                         string sentWhat, string retWhat)
        {
            var composer = new Composer(dict);
            var hook = new KeyboardHook(composer);
            hook.InitAssoc(null, null);
            hook.AssocEnabled = false;
            hook.ChineseMode = true;

            var sent = new StringBuilder();
            Typist.SendSink = delegate(string s) { sent.Append(s); };

            // 假 Win32：完全唔碰系統
            var fake = new KeyboardHook.Win32Seam();
            fake.IsDown = delegate(int v) { return false; };
            fake.ShiftActuallyDown = delegate { return false; };
            var pending = new Native.KBDLLHOOKSTRUCT();
            fake.ReadKeyEvent = delegate(IntPtr p) { return pending; };
            hook.Win = fake;

            bool passedToSystem = false;
            hook.CallNext = delegate(IntPtr h, int n, IntPtr w, IntPtr l) { passedToSystem = true; return IntPtr.Zero; };

            try
            {
                // 先打前綴（雙碼）
                foreach (char c in prefix)
                {
                    pending = new Native.KBDLLHOOKSTRUCT { vkCode = (uint)('A' + (c - 'a')) };
                    hook.InvokeHookCallback(0, (IntPtr)Native.WM_KEYDOWN, IntPtr.Zero);
                }

                // 量度目標按鍵這一下
                sent.Length = 0;
                passedToSystem = false;
                pending = new Native.KBDLLHOOKSTRUCT { vkCode = vk };
                IntPtr ret = hook.InvokeHookCallback(0, (IntPtr)Native.WM_KEYDOWN, IntPtr.Zero);

                Eq(expectSent, sent.ToString(), name + " -> " + sentWhat);
                Eq(expectRet, ret.ToString(), name + " -> 回 " + expectRet + "（" + retWhat + "）");

                // 【獨立 oracle】唔可以只靠上面兩條。
                //
                // 理由（上一輪審查實測）：如果把 OnHookMessage 改回 v2.4 錯版
                //   int rr = ProcessKeyDown(input); ApplyHookResult(rr);
                //   return rr == Consumed ? 1 : CallNext(...);
                // 咁「文字」其實照樣送得出（ApplyHookResult 仍有跑），
                // 所以 `Eq(expectSent, sent)` 會 PASS —— 文字斷言捉唔到數字漏字。
                // 真正捉得到的是「有沒有交回系統」：吞鍵場景一旦交回系統，
                // 個數字就會漏進文件（＝v2.4 的「么2」）。
                // 所以這裡加一條**方向明確**的斷言，唔係「自洽就算」那種。
                if (expectRet == "1")
                    Check(!passedToSystem, name + " -> 吞鍵場景：絕對唔可以交回系統（否則按鍵會漏進文件）");
                else
                    Check(passedToSystem, name + " -> 放行場景：一定要交回系統");
            }
            finally
            {
                hook.Win = KeyboardHook.RealWin;
                hook.CallNext = Native.CallNextHookEx;
                Typist.SendSink = null;
                hook.DisposeAssoc();
                hook.Dispose();
            }
        }

        /// <summary>驗一種 HookResult 的 (送字, 吞鍵) 期望值。</summary>
        private static void AssertResult(int result, bool flush, bool swallow, string name)
        {
            Eq(flush ? "True" : "False", KeyboardHook.ShouldFlush(result).ToString(),
                name + " -> ShouldFlush = " + flush);
            Eq(swallow ? "True" : "False", KeyboardHook.ShouldSwallowKey(result).ToString(),
                name + " -> ShouldSwallowKey = " + swallow);
        }

        // ------------------------------------------------------------------
        //  護欄：Gate 失步時，Shift 仍然要被尊重
        //
        //  Gate 只靠 Shift keydown 更新，以下情況佢收唔到：
        //    * 程式啟動時 Shift 已經按住
        //    * 暫停輸入法期間按住 Shift（Enabled=false 時直接 return）
        //    * 掛鉤被系統移除再重裝
        //  若只信 Gate，用家想打 >（Ctrl+Shift+.）就會變成「。」。
        // ------------------------------------------------------------------
        private static void TestShiftGateDesync(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- Gate 失步時 Shift 仍然要被尊重 ---");

            // 情境：Gate 唔知 Shift 按住，但系統實際按住（ShiftPhysicallyDown = true）
            var s = new Sim(dict);
            try
            {
                Check(!s.Hook.Gate.IsShiftHeld, "測試前提：Gate 唔知 Shift 按住");
                int r = s.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: true);
                Eq("0", r.ToString(), "Gate 失步時 Ctrl+Shift+. -> 仍然交回系統（唔會變「。」）");
                Eq("", s.Take(), "Gate 失步時 Ctrl+Shift+. -> 沒有上屏全形標點");
            }
            finally { s.Dispose(); }

            // 組字中同樣要成立，而且組字唔可以殘留
            var s2 = new Sim(dict);
            try
            {
                s2.Letter('a'); s2.Letter('b');
                string first = s2.Composer.FirstCandidate();
                int r = s2.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: true);
                Eq("0", r.ToString(), "Gate 失步 + 組字中 Ctrl+Shift+. -> 交回系統");
                Eq(first, s2.Take(), "Gate 失步 + 組字中 -> 先上屏當前首選（" + first + "）");
                Eq("", s2.Code, "Gate 失步 + 組字中 -> 組字狀態已清");
            }
            finally { s2.Dispose(); }

            // 模擬「暫停期間按住 Shift」：Enabled=false 時 Gate 收唔到 keydown
            var s3 = new Sim(dict);
            try
            {
                s3.Hook.Enabled = false;
                s3.Hook.ProcessKeyDown(new KeyInput
                {
                    Vk = (uint)Native.VK_RSHIFT,
                    ShiftSide = ToggleGate.RightSide,
                    NowMs = 0,
                    ShiftPhysicallyDown = true
                });
                Check(!s3.Hook.Gate.IsShiftHeld, "暫停期間按 Shift -> Gate 收唔到（真實存在的失步路徑）");
                s3.Hook.Enabled = true;
                int r = s3.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: true);
                Eq("0", r.ToString(), "恢復後 Ctrl+Shift+. -> 仍然交回系統（唔會出「。」）");
                Eq("", s3.Take(), "恢復後 Ctrl+Shift+. -> 沒有上屏全形標點");
            }
            finally { s3.Dispose(); }

            // 對照：真的沒按 Shift，Ctrl+. 仍然要出「。」
            var s4 = new Sim(dict);
            try
            {
                int r = s4.Press((uint)Native.VK_OEM_PERIOD, true, shiftPhysicallyDown: false);
                Eq("2", r.ToString(), "沒按 Shift 的 Ctrl+. -> CommitPending（照出全形）");
                Eq("。", s4.Take(), "沒按 Shift 的 Ctrl+. -> 送出「。」");
            }
            finally { s4.Dispose(); }
        }

        // ------------------------------------------------------------------
        //  護欄：Ctrl+Shift+符號 的例外範圍
        //
        //  「長按 Shift 期間先上屏」只應該優待符號表裡面的鍵。
        //  其他鍵（Esc、字母）一定要維持「唔上屏」，否則 Ctrl+Shift+Esc
        //  （工作管理員）之類的快捷鍵會被輸入法干擾。
        // ------------------------------------------------------------------
        private static void TestIsMappedScope()
        {
            Console.WriteLine();
            Console.WriteLine("--- Ctrl+Shift+符號 的例外範圍 ---");

            Check(FullWidthPunct.IsMapped(Native.VK_OEM_PERIOD), "符號表包含 .");
            Check(FullWidthPunct.IsMapped(Native.VK_OEM_COMMA), "符號表包含 ,");
            Check(FullWidthPunct.IsMapped(Native.VK_OEM_1), "符號表包含 ;");

            // 非符號鍵：絕對唔可以落在例外內
            Check(!FullWidthPunct.IsMapped(Native.VK_ESCAPE), "符號表不含 Esc（Ctrl+Shift+Esc 要直通）");
            Check(!FullWidthPunct.IsMapped((uint)'A'), "符號表不含字母");
            Check(!FullWidthPunct.IsMapped((uint)'C'), "符號表不含字母 C");
            Check(!FullWidthPunct.IsMapped((uint)0x74), "符號表不含 F5");
            Check(!FullWidthPunct.IsMapped(Native.VK_TAB), "符號表不含 Tab");

            // 數字鍵：Shift+數字 要留給聯想字，所以都唔可以算符號鍵
            Check(!FullWidthPunct.IsMapped((uint)'1'), "符號表不含數字 1");
            Check(!FullWidthPunct.IsMapped((uint)'0'), "符號表不含數字 0");
        }
    }
}
