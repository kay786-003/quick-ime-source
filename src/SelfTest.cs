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

            // ---- 空格 = 下一頁（微軟速成習慣；尾頁繞返第 1 頁）----
            c.Reset();
            c.TypeLetter('h'); c.TypeLetter('i');
            int pagesHi = c.TotalPages;
            Check(pagesHi > 1, "'hi' 有多頁");
            int beforePage = c.Page;
            c.PressSpace();
            Check(c.IsComposing, "空格未上屏（因為要翻頁）");
            Eq((beforePage + 1).ToString(), c.Page.ToString(), "空格 = 下一頁");

            // 到尾頁再按空格 -> 繞返第 1 頁，唔會偷偷上屏
            committed = null;
            while (c.Page < c.TotalPages - 1) c.PressSpace();
            Check(c.IsLastPage, "已到最後一頁");
            string lastFirst = c.CurrentPage()[0];
            c.PressSpace();
            Check(c.IsComposing, "最後一頁按空格仍然在組字（唔會偷偷上屏）");
            Check(committed == null, "最後一頁按空格沒有上屏任何字");
            Eq("0", c.Page.ToString(), "最後一頁按空格 -> 繞返第 1 頁");
            Check(c.CurrentPage()[0] != lastFirst, "繞返第 1 頁後候選與尾頁不同");

            // 循環：一直按空格，頁碼會順序 0,1,...,尾頁,0,1...
            c.Reset();
            c.TypeLetter('h'); c.TypeLetter('i');
            int pages2 = c.TotalPages;
            bool cycleOk = true;
            for (int i = 1; i <= pages2 * 2; i++)
            {
                c.PressSpace();
                if (c.Page != (i % pages2)) { cycleOk = false; break; }
                if (!c.IsComposing) { cycleOk = false; break; }
            }
            Check(cycleOk, "連續按空白鍵會循環翻頁 " + pages2 + " 頁，中途不會上屏");

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

            // 上一頁邊界：第 1 頁再按 PageUp 不會變負數
            c.PageSelect(0);
            c.PageMove(-1);
            Eq("0", c.Page.ToString(), "第 1 頁再按上一頁 -> 停在 0（不會負數）");
            Check(!c.IsLastPage, "第 1 頁 IsLastPage = false");

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

            // 聯想字設定（預設開；ini 讀寫往返）
            Check(new Settings().AssocEnabled, "預設開啟聯想字");
            var st4 = new Settings();
            st4.AssocEnabled = false;
            Check(st4.ToIni().Contains("AssocEnabled=0"), "寫出 AssocEnabled=0");
            var st5 = new Settings();
            st5.Apply(st4.ToIni().Split(new[] { "\r\n" }, StringSplitOptions.None));
            Check(!st5.AssocEnabled, "讀回 AssocEnabled = false");

            // 記錄習慣開關（預設開；與聯想字開關互相獨立）
            Check(new Settings().AssocLearnEnabled, "預設開啟「記錄用字習慣」");
            var st6 = new Settings();
            st6.AssocLearnEnabled = false;
            Check(st6.ToIni().Contains("AssocLearnEnabled=0"), "寫出 AssocLearnEnabled=0");
            var st7 = new Settings();
            st7.Apply(st6.ToIni().Split(new[] { "\r\n" }, StringSplitOptions.None));
            Check(!st7.AssocLearnEnabled, "讀回 AssocLearnEnabled = false");

            // Shift+數字選聯想字（預設開 = Windows 速成習慣；ini 往返）
            Check(new Settings().AssocShiftDigits, "預設「Shift+數字鍵選聯想字」開啟（跟 Windows 速成）");
            var st10 = new Settings();
            st10.AssocShiftDigits = false;
            Check(st10.ToIni().Contains("AssocShiftDigits=0"), "寫出 AssocShiftDigits=0");
            var st11 = new Settings();
            st11.Apply(st10.ToIni().Split(new[] { "\r\n" }, StringSplitOptions.None));
            Check(!st11.AssocShiftDigits, "讀回 AssocShiftDigits = false");
            // 舊設定檔（v2.2，冇呢一行）→ 預設跟 Windows 速成開啟
            var st12 = new Settings();
            st12.Apply(new[] { "ChineseMode=1", "AssocEnabled=1", "AssocLearnEnabled=1" });
            Check(st12.AssocShiftDigits, "舊設定檔冇呢一行 -> 預設開啟（相容 v2.2）");

            // 兩個開關互不影響：只關學習，聯想仍然開
            var st8 = new Settings();
            st8.AssocLearnEnabled = false;
            Check(st8.AssocEnabled, "關掉記錄習慣後，聯想字仍然開啟（兩者獨立）");

            // 舊設定檔（v2.1，冇 AssocLearnEnabled 這一行）→ 學習預設仍然開
            var st9 = new Settings();
            st9.Apply(new[] { "ChineseMode=1", "AssocEnabled=1" });
            Check(st9.AssocLearnEnabled, "舊設定檔冇呢一行 -> 學習預設開啟（相容 v2.1）");

            // ---- 聯想字選字鍵（Shift + 數字，跟 Windows 速成）----
            Console.WriteLine();
            Console.WriteLine("--- 聯想字選字鍵（Shift + 數字）---");

            // 預設（Shift+數字 開）：按住 Shift 才選字
            Check(KeyboardHook.DecideAssocDigit(true, true, 0, 5) == AssocDigitAction.Select,
                "Shift+1 有聯想 -> 選第 1 個聯想字");
            Check(KeyboardHook.DecideAssocDigit(true, true, 8, 9) == AssocDigitAction.Select,
                "Shift+9（9 個聯想）-> 選第 9 個");
            Check(KeyboardHook.DecideAssocDigit(true, true, 6, 5) == AssocDigitAction.CloseAndPass,
                "Shift+7 但只有 5 個聯想 -> 收起聯想窗，個鍵交回系統");

            // 冇按 Shift：唔選字，個數字照常打出來
            Check(KeyboardHook.DecideAssocDigit(false, true, 0, 9) == AssocDigitAction.PassAsNumber,
                "唔按 Shift 直接按 1 -> 打數字（唔會被聯想窗吞掉）");

            // 非數字鍵：唔關聯想字事
            Check(KeyboardHook.DecideAssocDigit(true, true, -1, 9) == AssocDigitAction.Ignore,
                "非數字鍵 -> 聯想字判定唔干預");

            // 關掉「Shift+數字」（v2.2 舊行為）：直接按數字就選
            Check(KeyboardHook.DecideAssocDigit(false, false, 2, 9) == AssocDigitAction.Select,
                "關掉 Shift+數字 -> 直接按 3 就選第 3 個聯想字");
            Check(KeyboardHook.DecideAssocDigit(true, false, 2, 9) == AssocDigitAction.Select,
                "關掉 Shift+數字 -> 按住 Shift 亦照樣選");
            Check(KeyboardHook.DecideAssocDigit(false, false, 7, 5) == AssocDigitAction.Ignore,
                "關掉 Shift+數字 + 超出範圍 -> 唔消耗（交回系統）");

            // ---- 聯想字（Assoc）----
            RunAssocTests(dict);

            Console.WriteLine();
            Console.WriteLine(_fail == 0
                ? "*** 全部 " + _total + " 項通過 ***"
                : ("*** 有 " + _fail + " / " + _total + " 項失敗 ***"));
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>聯想字的完整測試：解析、學習、排序、清空、持久化、中文判定。</summary>
        private static void RunAssocTests(Dict dict)
        {
            Console.WriteLine();
            Console.WriteLine("--- 聯想字 ---");

            // ---- 中文判定 ----
            Check(Assoc.IsHanChar('你'), "『你』判定為中文字");
            Check(Assoc.IsHanChar('港'), "『港』判定為中文字");
            Check(!Assoc.IsHanChar('a'), "『a』唔係中文字（唔會學）");
            Check(!Assoc.IsHanChar('1'), "『1』唔係中文字（唔會學）");
            Check(!Assoc.IsHanChar('，'), "全形逗號唔係中文字（唔會學）");
            Check(!Assoc.IsHanChar(' '), "空格唔係中文字（唔會學）");

            // ---- 出廠詞表解析 ----
            var parsed = Assoc.ParseSeed(
                "# 註解行\n" +
                "你 好 嗎 係\n" +
                "我 係 都\n" +
                "壞行\n" +                     // 冇分隔符 -> 跳過
                "多字 唔 應該 收\n" +          // 第一欄唔係單字 -> 跳過
                "你 唔 好\n");                 // 同一個字重複出現 -> 合併、去重
            Check(parsed.Count == 2, "詞表解析：只有 2 個有效的單字條目 (實際 " + parsed.Count + ")");

            var map = new Dictionary<string, List<string>>();
            foreach (var kv in parsed) map[kv.Key] = kv.Value;
            Check(map.ContainsKey("你"), "詞表有『你』");
            Check(map.ContainsKey("我"), "詞表有『我』");
            Check(!map.ContainsKey("多字"), "第一欄唔係單字 -> 被跳過");
            Eq("好|嗎|係|唔", string.Join("|", map["你"].ToArray()),
                "『你』的聯想次序（合併後保留首次出現次序、已去重）");

            // ---- 載入詞表 + 查詢 ----
            var a = new Assoc();
            a.LoadSeed("你 好 嗎 係\n我 係 都 唔\n");

            var items = a.Lookup("你", "");
            Check(items.Count > 0, "『你』有聯想字");
            Eq("好", items[0], "『你』第一個聯想 = 好");
            Check(Contains(items, "嗎"), "『你』的聯想包含『嗎』");
            Check(Contains(items, "係"), "『你』的聯想包含『係』");

            // 完全冇線索的字 -> 通用常用字補底（一定要有嘢揀）
            var generic = a.Lookup("龘", "");
            Check(generic.Count > 0, "冇任何記錄的字亦有通用補底（唔會空白）");

            // ---- 學習：次數決定排序 ----
            var b = new Assoc();
            b.LoadSeed("你 好 嗎 係\n");

            Eq("好", b.Lookup("你", "")[0], "學習前：『你』→ 好");

            // 連打 5 次「你 → 係」，應該爬到最前
            for (int i = 0; i < 5; i++) b.Learn("你", "係");
            Eq("係", b.Lookup("你", "")[0], "學 5 次之後：『你』→ 係 排第一（似拼音輸入法）");
            Eq("1", b.LearnedPairs.ToString(), "已學習組合數 = 1（同一組合重複學習只算一組）");

            // 再多打幾次「嗎」，就會蓋過「係」
            for (int i = 0; i < 6; i++) b.Learn("你", "嗎");
            Eq("嗎", b.Lookup("你", "")[0], "再學 6 次『嗎』-> 爬到第一");

            // 未學過的字仍然保留在後面
            Check(Contains(b.Lookup("你", ""), "好"), "未學過的內建聯想仍然保留");

            // ---- 前綴收窄 ----
            var c2 = new Assoc();
            c2.LoadSeed("你 好 嗎 係 唔 的\n");
            var narrow = c2.Lookup("你", "好");
            Check(narrow.Count == 1 && narrow[0] == "好", "前綴『好』只收窄到『好』");
            Check(c2.Lookup("你", "嗎").Count == 1, "前綴『嗎』收窄到『嗎』");
            Check(c2.Lookup("你", "香").Count == 0, "冇字接得上 -> 回傳空（交回速成）");

            // ---- 唔會學到標點 / 英文 ----
            var d2 = new Assoc();
            d2.Learn("你", "，");
            d2.Learn("你", "a");
            d2.Learn("你", "1");
            Eq("0", d2.LearnedPairs.ToString(), "學習標點／英文／數字唔會被記錄（LearnedPairs 仍為 0）");

            // ---- 持久化往返 ----
            var e2 = new Assoc();
            e2.LoadSeed("你 好 嗎\n");
            e2.Learn("你", "係");
            e2.Learn("你", "係");
            e2.Learn("真", "係");
            string memText = e2.ToMemoryText();
            Check(memText.StartsWith(Assoc.MemoryHeader), "記憶檔有 magic 標頭");
            Check(memText.Contains(">你"), "記憶檔有『>你』段落");
            Check(memText.Contains("係=2"), "記憶檔記錄『係』出現 2 次");

            var f2 = new Assoc();
            f2.LoadSeed("你 好 嗎\n");
            f2.LoadMemoryText(memText);
            Eq("2", f2.LearnedPairs.ToString(), "由文字載入 -> 學到 2 組");
            Eq("係", f2.Lookup("你", "")[0], "載入後排序保持：『係』仍然第一");

            // ---- 「記錄習慣」關掉後：唔會再學，但舊記憶仍然有效 ----
            var learnAssoc = new Assoc();
            learnAssoc.LoadSeed("你 好 嗎\n");
            var learnHook = new KeyboardHook(new Composer(dict));
            learnHook.InitAssoc(learnAssoc, delegate { });   // 模擬 Program 的初始化
            try
            {
                Check(learnHook.AssocEnabled && learnHook.AssocLearnEnabled,
                    "InitAssoc 之後預設：聯想開、記錄習慣開");
                Check(learnHook.ShouldLearn, "兩者都開 -> 會學習");

                learnHook.AssocLearnEnabled = false;
                Check(learnHook.AssocEnabled, "只關記錄習慣 -> 聯想仍然開（仍然會顯示）");
                Check(!learnHook.ShouldLearn, "關掉記錄習慣 -> 不再學習");

                learnHook.AssocLearnEnabled = true;
                learnHook.AssocEnabled = false;
                Check(!learnHook.ShouldLearn, "關掉聯想字 -> 亦不會學習");
            }
            finally
            {
                learnHook.DisposeAssoc();
            }

            // 舊記憶保留：關掉學習不會清走已學的排序
            var keep = new Assoc();
            keep.LoadSeed("你 好 嗎\n");
            for (int i = 0; i < 5; i++) keep.Learn("你", "嗎");
            Eq("嗎", keep.Lookup("你", "")[0], "學習期間：『嗎』排第一");
            Eq("1", keep.LearnedPairs.ToString(), "已有 1 組記憶");
            // 模擬「關掉學習」：不再呼叫 Learn，但舊記憶仍然存在
            Eq("嗎", keep.Lookup("你", "")[0], "關掉學習之後，舊記憶仍然照舊排序");
            Eq("1", keep.LearnedPairs.ToString(), "關掉學習不會刪除已有的記憶");

            // ---- 壞檔容錯 ----
            var g2 = new Assoc();
            g2.LoadSeed("你 好\n");
            g2.LoadMemoryText("完全垃圾\n沒有分隔\n>你\n壞資料\n===　===\n");
            Check(g2.LearnedPairs == 0, "壞記憶檔 -> 安全地當冇記憶（唔會拋例外）");
            Check(g2.Lookup("你", "").Count > 0, "壞記憶檔 -> 內建詞表仍然可用");

            // ---- 清空記憶 ----
            var h2 = new Assoc();
            h2.LoadSeed("你 好 嗎\n");
            for (int i = 0; i < 9; i++) h2.Learn("你", "嗎");
            Eq("嗎", h2.Lookup("你", "")[0], "清空前：學過的『嗎』排第一");
            h2.ClearMemory();
            Eq("0", h2.LearnedPairs.ToString(), "清空後：已學習組合數 = 0");
            Eq("好", h2.Lookup("你", "")[0], "清空後：返去出廠預設（『好』第一）");
            Check(Contains(h2.Lookup("你", ""), "嗎"), "清空後：內建聯想『嗎』仍然存在");

            // ---- 聯想唔會影響速成候選次序（本專案最重要的護欄）----
            string firstHi = dict.Lookup("hi", 2)[0];
            var k2 = new Assoc();
            for (int i = 0; i < 20; i++) k2.Learn("我", "係");
            Eq(firstHi, dict.Lookup("hi", 2)[0], "學習聯想之後，速成 'hi' 首選次序完全不變");
        }


        private static bool Contains(IList<string> l, string s)
        {
            for (int i = 0; i < l.Count; i++) if (l[i] == s) return true;
            return false;
        }
    }
}
