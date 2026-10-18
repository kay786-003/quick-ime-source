// ============================================================================
//  Assoc.cs - 聯想字（下一個字預測）
//
//  設計目標：
//    * 打完一個字，立即列出「最常接住落去」的字
//    * 學用家自己嘅習慣 —— 邊個字接得最多就排最前（似拼音輸入法）
//    * 可以一鍵清空記憶，返去出廠狀態
//
//  三個資料層（由弱到強）：
//    1. 通用常用字    —— 任何字都有得揀（保證唔會「乜都冇」）
//    2. 內建接續表    —— assoc-seed.txt 的出廠預設（雙向學習）
//    3. 個人學習記憶  —— assoc.txt，次數越多排得越前
//
//  排序：個人學習（次數多→少） > 內建接續表 > 通用常用字
//
//  重要：唔會影響速成候選字次序（見 Dict.cs 的警告）。
//
//  記憶檔格式（UTF-8 純文字，人手睇得明、改得動）：
//      QIME-ASSOC 1
//      >你
//      好=12
//      嗎=3
//      >我
//      係=8
//  「>X」= 上一個字，「Y=N」= 接住落去嘅字同出現次數。
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace QuickIME
{
    /// <summary>一組聯想：上一個字 -> 下一個字（附學習次數）。</summary>
    internal sealed class AssocEntry
    {
        public string Char;
        public int Count;

        public AssocEntry(string ch, int count)
        {
            Char = ch;
            Count = count;
        }
    }

    /// <summary>
    /// 聯想字引擎：個人學習 + 內建接續表 + 通用常用字。
    ///
    /// 全部方法都執行緒安全（掛鉤執行緒會寫、UI 會讀）。
    /// </summary>
    internal sealed class Assoc
    {
        /// <summary>內建詞表檔名（由 build.ps1 嵌入）。</summary>
        public const string SeedResource = "assoc-seed.txt";

        /// <summary>記憶檔名（與 settings.ini 放在同一個資料夾）。</summary>
        public const string MemoryFileName = "assoc.txt";

        /// <summary>
        /// 每個字最多顯示幾個聯想。
        /// 9 個剛好對應數字鍵 1-9，多過就要翻頁 —— 聯想字唔應該令人要翻頁。
        /// </summary>
        public const int MaxPerChar = 9;

        /// <summary>記憶檔的 magic 行，用來認出格式。</summary>
        public const string MemoryHeader = "QIME-ASSOC 1";

        private readonly object _lock = new object();

        /// <summary>上一個字 -> 聯想清單（Count = 0 代表出廠預設）。</summary>
        private readonly Dictionary<string, List<AssocEntry>> _learned =
            new Dictionary<string, List<AssocEntry>>(StringComparer.Ordinal);

        /// <summary>內建接續表（唔會被學習改動，清空記憶亦唔會影響它）。</summary>
        private readonly Dictionary<string, List<string>> _seed =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>通用常用字：完全冇線索時嘅補底。</summary>
        private static readonly string[] GenericChars = new string[]
        {
            "的了係唔我你有在人都就好要可以這那我們他她說會個上到下得"
        };

        /// <summary>已學習的組合數（給選單顯示）。</summary>
        public int LearnedPairs { get; private set; }

        // ------------------------------------------------------------------
        //  載入
        // ------------------------------------------------------------------
        /// <summary>載入內建接續表。傳 null / 空字串都安全。</summary>
        public void LoadSeed(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var parsed = ParseSeed(text);
            lock (_lock)
            {
                foreach (var kv in parsed)
                {
                    List<string> list;
                    if (!_seed.TryGetValue(kv.Key, out list))
                    {
                        list = new List<string>();
                        _seed[kv.Key] = list;
                    }
                    foreach (string ch in kv.Value)
                    {
                        if (string.IsNullOrEmpty(ch)) continue;
                        if (IndexOfString(list, ch) >= 0) continue;
                        if (list.Count >= MaxPerChar) break;
                        list.Add(ch);
                    }
                }
            }
        }

        /// <summary>由組件內嵌資源載入接續表；找唔到就靜靜咁跳過。</summary>
        public void LoadSeedFromAssembly(Assembly asm)
        {
            if (asm == null) return;
            try
            {
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (!name.EndsWith(SeedResource, StringComparison.OrdinalIgnoreCase)) continue;
                    using (Stream s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) continue;
                        using (var sr = new StreamReader(s, Encoding.UTF8))
                            LoadSeed(sr.ReadToEnd());
                    }
                    return;
                }
            }
            catch
            {
                // 接續表壞掉唔應該令輸入法開唔到
            }
        }

        /// <summary>
        /// 由文字載入個人記憶（與檔案解耦，方便 --selftest 驗證格式）。
        /// 壞資料一律忽略，唔會拋例外。
        /// </summary>
        public void LoadMemoryText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            lock (_lock)
            {
                string cur = null;
                string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                foreach (string raw in lines)
                {
                    string line = raw == null ? "" : raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith(">", StringComparison.Ordinal))
                    {
                        cur = line.Substring(1).Trim();
                        if (cur.Length == 0) cur = null;
                        continue;
                    }
                    if (cur == null) continue;   // 「>X」之前嘅內容一律忽略
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string ch = line.Substring(0, eq).Trim();
                    int n;
                    if (!int.TryParse(line.Substring(eq + 1).Trim(),
                                      NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) continue;
                    if (ch.Length != 1 || !IsHanChar(ch[0]) || n <= 0) continue;
                    AddOrBump(cur, ch, n);
                }
                RecountLearned();
            }
        }

        /// <summary>
        /// 載入個人記憶。檔案唔存在 / 壞掉都唔會拋例外
        /// （寧願冇記憶，都唔好開唔到 —— 網吧／還原卡環境重點）。
        /// </summary>
        public void LoadMemory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (!File.Exists(path)) return;
                LoadMemoryText(File.ReadAllText(path, Encoding.UTF8));
            }
            catch
            {
                // 記憶檔壞掉就當冇記憶
            }
        }

        /// <summary>把整份記憶寫成文字（與檔案解耦，方便 --selftest 驗證）。</summary>
        public string ToMemoryText()
        {
            var sb = new StringBuilder();
            sb.Append(MemoryHeader).Append("\r\n");
            lock (_lock)
            {
                foreach (var kv in _learned)
                {
                    var list = new List<AssocEntry>();
                    foreach (var e in kv.Value) if (e.Count > 0) list.Add(e);
                    if (list.Count == 0) continue;   // 未學過嘅唔需要寫入

                    list.Sort(CompareLearned);
                    sb.Append('>').Append(kv.Key).Append("\r\n");
                    foreach (var e in list)
                        sb.Append(e.Char).Append('=').Append(e.Count).Append("\r\n");
                }
            }
            return sb.ToString();
        }

        /// <summary>寫入個人記憶。失敗（例如 AppData 被鎖）都唔會拋例外。</summary>
        public bool SaveMemory(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, ToMemoryText(), new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------
        //  查詢
        // ------------------------------------------------------------------
        /// <summary>
        /// 取得聯想字清單（已排序、已去重、已限量）。
        /// </summary>
        /// <param name="prev">上一個上屏的字（可以係空 = 開頭）。</param>
        /// <param name="pending">用家已打的聯想前綴（冇就傳空字串）。</param>
        public IList<string> Lookup(string prev, string pending)
        {
            var result = new List<string>();
            pending = pending ?? "";
            lock (_lock)
            {
                // 1) 個人學習優先（次數多嘅排前）
                var learned = new List<AssocEntry>();
                if (!string.IsNullOrEmpty(prev))
                {
                    List<AssocEntry> list;
                    if (_learned.TryGetValue(prev, out list))
                        foreach (var e in list) if (e.Count > 0) learned.Add(e);
                }
                learned.Sort(CompareLearned);
                foreach (var e in learned)
                {
                    if (!MatchesPrefix(e.Char, pending)) continue;
                    AddUnique(result, e.Char);
                    if (result.Count >= MaxPerChar) return result;
                }

                // 2) 內建接續表（出廠預設）
                if (!string.IsNullOrEmpty(prev))
                {
                    List<string> seed;
                    if (_seed.TryGetValue(prev, out seed))
                    {
                        foreach (string ch in seed)
                        {
                            if (!MatchesPrefix(ch, pending)) continue;
                            AddUnique(result, ch);
                            if (result.Count >= MaxPerChar) return result;
                        }
                    }
                }

                // 3) 通用常用字（保證一定有嘢揀）
                //    GenericChars 係一整個字串，要逐個字拆開，唔可以整串當一個候選。
                foreach (string blob in GenericChars)
                {
                    foreach (char gc in blob)
                    {
                        string ch = gc.ToString();
                        if (!MatchesPrefix(ch, pending)) continue;
                        AddUnique(result, ch);
                        if (result.Count >= MaxPerChar) return result;
                    }
                }
            }
            return result;
        }

        /// <summary>呢個字有冇聯想（用於決定要唔要顯示）。</summary>
        public bool HasAny(string prev)
        {
            return Lookup(prev, "").Count > 0;
        }

        // ------------------------------------------------------------------
        //  學習 / 清空
        // ------------------------------------------------------------------
        /// <summary>
        /// 記錄「打了 prev 之後接住打 next」。次數越多，下次排得越前。
        /// 只接受「單一個中文字」——標點 / 英文 / 數字一律唔學，
        /// 否則記憶會被垃圾污染（例如打完字之後打逗號）。
        /// </summary>
        public void Learn(string prev, string next)
        {
            if (!IsLearnable(prev) || !IsLearnable(next)) return;
            lock (_lock)
            {
                AddOrBump(prev, next, 1);
                RecountLearned();
            }
        }

        /// <summary>只有單一中文字才值得學。</summary>
        private static bool IsLearnable(string s)
        {
            return !string.IsNullOrEmpty(s) && s.Length == 1 && IsHanChar(s[0]);
        }


        /// <summary>清空所有個人學習記憶（內建接續表同通用字會保留）。</summary>
        public void ClearMemory()
        {
            lock (_lock)
            {
                _learned.Clear();
                LearnedPairs = 0;
            }
        }

        /// <summary>清空記憶並立刻寫入檔案。</summary>
        public bool ClearAndSave(string path)
        {
            ClearMemory();
            return SaveMemory(path);
        }

        /// <summary>
        /// 呢個字係唔係「純中文字」——只有中文才觸發聯想，
        /// 標點、英文、數字一律唔學（否則會污染記憶）。
        /// 注意：CJK 擴展 B 之後嘅字元超出 UTF-16 char 範圍，
        /// 要用 surrogate pair 才表示得到，所以 char 版本只覆蓋基本區。
        /// </summary>
        public static bool IsHanChar(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF)     // CJK 統一漢字
                || (c >= 0x3400 && c <= 0x4DBF)     // 擴展 A
                || (c >= 0xF900 && c <= 0xFAFF)     // 相容漢字
                || (c >= 0x3005 && c <= 0x3005);    // 々（日文重複符，用在中文詞亦常見）
        }

        // ------------------------------------------------------------------
        //  內部
        // ------------------------------------------------------------------
        private static bool MatchesPrefix(string ch, string pending)
        {
            if (string.IsNullOrEmpty(ch)) return false;
            if (pending.Length == 0) return true;
            return ch.StartsWith(pending, StringComparison.Ordinal);
        }

        private void AddOrBump(string prev, string ch, int delta)
        {
            List<AssocEntry> list;
            if (!_learned.TryGetValue(prev, out list))
            {
                list = new List<AssocEntry>();
                _learned[prev] = list;
            }
            int i = IndexOfEntry(list, ch);
            if (i >= 0) { list[i].Count += delta; return; }
            list.Add(new AssocEntry(ch, delta));
            if (list.Count > MaxPerChar * 4)
            {
                // 極端情況下防止無限增長：只留最高分嘅一批
                list.Sort(CompareLearned);
                list.RemoveRange(MaxPerChar * 2, list.Count - MaxPerChar * 2);
            }
        }

        private void RecountLearned()
        {
            int n = 0;
            foreach (var kv in _learned)
                foreach (var e in kv.Value) if (e.Count > 0) n++;
            LearnedPairs = n;
        }

        private static int IndexOfEntry(List<AssocEntry> list, string ch)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].Char, ch, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int IndexOfString(List<string> list, string ch)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], ch, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static void AddUnique(List<string> list, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], s, StringComparison.Ordinal)) return;
            list.Add(s);
        }

        /// <summary>學習過嘅：次數多嘅排前；同次數就跟字符次序（結果穩定可重現）。</summary>
        private static int CompareLearned(AssocEntry a, AssocEntry b)
        {
            int c = b.Count.CompareTo(a.Count);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Char, b.Char);
        }

        // ------------------------------------------------------------------
        //  內建詞表解析
        // ------------------------------------------------------------------
        /// <summary>
        /// 解析內建詞表。格式很寬鬆，方便人手編輯：
        ///     上一字 之後嘅字
        /// 分隔可用任何空白 / 逗號 / 頓號 / 冒號 / 豎線；
        /// 以 # 開頭嘅行係註解；第一欄必須剛好一個字（唔是就跳過）。
        /// </summary>
        internal static List<KeyValuePair<string, List<string>>> ParseSeed(string text)
        {
            var result = new List<KeyValuePair<string, List<string>>>();
            if (string.IsNullOrEmpty(text)) return result;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var seen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;

                // 找第一組「分隔符」
                int sp = IndexOfSeparator(line, 0);
                if (sp <= 0) continue;

                string head = line.Substring(0, sp).Trim();
                if (head.Length != 1) continue;          // 只接受單字做「上一個字」

                HashSet<string> got;
                List<string> list;
                // 同一個 head 出現多次時，合併到同一個清單（保留首次出現次序）
                List<string> existing = FindExisting(result, head);
                if (existing != null) { list = existing; got = seen[head]; }
                else { list = new List<string>(); got = new HashSet<string>(StringComparer.Ordinal); seen[head] = got; }

                int i = sp + 1;
                while (i < line.Length)
                {
                    int next = IndexOfSeparator(line, i);
                    int end = next < 0 ? line.Length : next;
                    string token = line.Substring(i, end - i).Trim();
                    i = next < 0 ? line.Length : next + 1;

                    if (token.Length == 0) continue;
                    // 一個 token 可能係多個字（例如「早晨」），逐個字加入
                    foreach (char c in token)
                        if (IsHanChar(c) && got.Add(c.ToString()) && list.Count < MaxPerChar)
                            list.Add(c.ToString());
                }

                if (list.Count > 0 && FindExisting(result, head) == null)
                    result.Add(new KeyValuePair<string, List<string>>(head, list));
            }
            return result;
        }

        private static List<string> FindExisting(List<KeyValuePair<string, List<string>>> result, string head)
        {
            foreach (var kv in result)
                if (string.Equals(kv.Key, head, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        private static bool IsSeparator(char c)
        {
            return c == ' ' || c == '\t' || c == ',' || c == '，' || c == '、'
                || c == ':' || c == '：' || c == '=' || c == '|' || c == '\u3000';
        }

        private static int IndexOfSeparator(string s, int from)
        {
            for (int i = from; i < s.Length; i++) if (IsSeparator(s[i])) return i;
            return -1;
        }
    }
}
