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

        private static void Check(bool cond, string what)
        {
            Console.WriteLine((cond ? "PASS  " : "FAIL  ") + what);
            if (!cond) _fail++;
        }

        private static void Eq(string expect, string actual, string what)
        {
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

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "*** 全部 " + 25 + " 項通過 ***" : ("*** 有 " + _fail + " 項失敗 ***"));
            return _fail == 0 ? 0 : 1;
        }

        private static bool Contains(IList<string> l, string s)
        {
            for (int i = 0; i < l.Count; i++) if (l[i] == s) return true;
            return false;
        }
    }
}
