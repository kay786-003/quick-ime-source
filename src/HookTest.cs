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

            /// <summary>按一個鍵（VK 值）。</summary>
            public void Press(uint vk, bool ctrl = false)
            {
                var k = new KeyInput
                {
                    Vk = vk,
                    Ctrl = ctrl,
                    Alt = false,
                    Win = false,
                    ShiftSide = ToggleGate.NoSide,
                    NowMs = _now += 40,
                    ShiftPhysicallyDown = false
                };

                int r = _hook.ProcessKeyDown(k);

                // 真實掛鉤在 HookResult.CommitPending 時會呼叫 Flush()，
                // 這裡照做，才會測到「PendingText 有沒有真的送出去」。
                if (r == KeyboardHook.HookResult.CommitPending)
                    _hook.Flush();
            }

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
            TestDigitOverflow(dict);         // v2.3 bug 3：數字超界漏字
            TestPageFirstCandidate(dict);    // v2.3 bug 4：尾頁 Enter 送錯字
            TestCtrlPunctuation(dict);       // 新功能：Ctrl + 符號 = 全形標點
            TestSpaceStillCycles(dict);      // 護欄：空白鍵永遠不上屏

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
                for (int i = 0; i < pages + 2; i++)
                {
                    s.Press((uint)Native.VK_SPACE);
                    if (s.Take().Length != 0)
                    {
                        Check(false, "空白鍵掃描期間送出了字（第 " + (i + 1) + " 次）");
                        return;
                    }
                }
                Check(true, "連按空白 " + (pages + 2) + " 次 -> 全程沒有上屏任何字");
                Check(s.Composer.IsComposing, "連按空白之後仍然在組字");

                int expectedPage = (pages + 2) % pages;
                Eq(expectedPage.ToString(), s.Composer.Page.ToString(),
                    "尾頁再按空白 -> 繞返第 1 頁（不是上屏）");
            }
            finally { s.Dispose(); }
        }
    }
}
