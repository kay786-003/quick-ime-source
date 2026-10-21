// ============================================================================
//  Composer.cs - 速成組字狀態機（與 UI / 鍵盤掛鉤完全解耦，可獨立測試）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace QuickIME
{
    internal enum KeyAction
    {
        /// <summary>不是輸入法的鍵，交回系統處理。</summary>
        Pass = 0,
        /// <summary>已消耗，不要交回系統。</summary>
        Consumed,
        /// <summary>需要把文字送到目標應用程式。</summary>
        Commit
    }

    internal sealed class Composer
    {
        private readonly Dict _dict;
        private readonly StringBuilder _code = new StringBuilder(2);

        private List<string> _candidates = new List<string>();
        private int _page;          // 目前候選頁（0-based）

        /// <summary>
        /// 一頁顯示幾個候選。
        /// 【硬規則】微軟速成一頁 9 個、用 1-9 選，`0` 不選字（使用者實測確認）。
        /// 不要把這裡改成 10 —— 見 AGENTS.md「已否決的建議」。
        /// </summary>
        private const int PageSize = 9;

        /// <summary>最近一次 Commit 要送出的文字。</summary>
        public string PendingText { get; private set; }

        /// <summary>狀態變更（組字碼 / 候選 / 頁碼）時觸發，讓 UI 重畫。</summary>
        public event EventHandler Changed;

        public Composer(Dict dict)
        {
            _dict = dict;
        }

        /// <summary>
        /// 按下空白鍵。
        /// * 有多頁 → 下一頁；到尾頁再按 → <b>繞返第 1 頁</b>（循環翻頁，不涉及上屏）
        /// * 只有一頁、而且唔止一個候選 → <b>完全冇反應</b>（不上屏、不換頁）
        /// * 全份清單只有 <b>1 個</b>候選 → 上屏那一個（冇嘢可以揀，同微軟速成一樣）
        ///
        /// 【為什麼是「候選數 == 1」而不是「只有一頁」】
        /// 這是用家實測微軟速成的結論（2026-10）：
        ///   - `b`（月，只有 1 個候選）按空白 → <b>上屏</b>
        ///   - `a`（日／曰，2 個候選、仍然只有一頁）按空白 → <b>不上屏</b>，
        ///     因為仲有兩個字可以揀，空白鍵唔應該替用家決定
        /// 舊版用 `TotalPages &lt;= 1` 去判斷，會令 `a` + 空白直接打出「日」——
        /// 同微軟速成唔一致，而且係「畫面有兩個選擇但程式自己揀咗」的打錯字。
        ///
        /// 點解尾頁要繞返第 1 頁：微軟速成用家習慣「一路按空白鍵掃候選字」，
        /// 掃到尾頁時手指仍然會再按一下。若果呢一下就上屏，
        /// 用家就會在「只係想再睇一次」嘅情況下打出一個字（而且係錯嘅字）。
        /// 所以空白鍵只負責翻頁；要上屏就按數字鍵（或 Enter／標點）。
        /// </summary>
        public bool PressSpace()
        {
            if (!IsComposing) return false;

            // 全份清單只有一個候選 → 冇嘢可揀，直接上屏（微軟速成行為）
            if (_candidates.Count <= 1) return CommitFirst();

            // 2-9 個候選（只有一頁）→ 空白鍵唔應該有任何作用
            if (TotalPages <= 1) return false;

            int np = (_page + 1) % TotalPages;
            if (np == _page) return false;
            _page = np;
            Raise();
            return true;
        }

        public bool IsComposing { get { return _code.Length > 0; } }
        public string Code { get { return _code.ToString(); } }
        public int Page { get { return _page; } }
        public int CandidateCount { get { return _candidates.Count; } }
        public int TotalPages { get { return _candidates.Count == 0 ? 1 : (_candidates.Count + PageSize - 1) / PageSize; } }

        /// <summary>目前頁的候選字（最多 9 個）。</summary>
        public IList<string> CurrentPage()
        {
            var page = new List<string>(PageSize);
            int start = _page * PageSize;
            for (int i = start; i < start + PageSize && i < _candidates.Count; i++)
                page.Add(_candidates[i]);
            return page;
        }

        /// <summary>
        /// 目前頁的第一個候選字（空白鍵單頁上屏 / Enter / 標點 / 連打用）。
        ///
        /// 【重要】這裡一定要用「當前頁」的第 1 個，不是整份清單的第 1 個。
        /// 舊版回傳 <c>_candidates[0]</c>，結果在尾頁按 Enter 會送出第 1 頁的字 ——
        /// 畫面顯示「笂」卻打出「凡」，是會直接打錯字的那種 bug
        /// （與 v2.3 修過的 PressSpace 同一個錯誤，當時只修了空白鍵那條路徑）。
        /// </summary>
        public string FirstCandidate()
        {
            int start = _page * PageSize;
            if (start < 0 || start >= _candidates.Count) return null;
            return _candidates[start];
        }

        /// <summary>整份候選清單的第 1 個（只有測試 / 診斷會用，不要拿去上屏）。</summary>
        public string FirstCandidateOverall()
        {
            return _candidates.Count > 0 ? _candidates[0] : null;
        }

        public void Reset()
        {
            if (_code.Length == 0 && _page == 0) return;
            _code.Length = 0;
            _candidates = new List<string>();
            _page = 0;
            Raise();
        }

        private void Raise()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------------
        // 鍵盤輸入
        // ------------------------------------------------------------------
        /// <summary>
        /// 處理一個字元鍵（僅 a-z）。回傳是否消耗。
        /// </summary>
        public bool TypeLetter(char lower)
        {
            if (lower < 'a' || lower > 'z') return false;

            if (_code.Length >= 2)
            {
                // 已滿兩碼：直接把目前首選上屏，再用這個字母開新一輪（連打）
                //
                // 【重要】這段一定要放在 z 檢查之前。
                // 舊版先檢查 z，結果 `ab` 之後按 z：z 被拒 → 但碼已經被清成空，
                // 之後又 append 了一個 z，留下「碼 = z、零個候選」的死碼，
                // 下一個字母就變成 `za`（打出「　」而不是你想要的字）。
                // 正確次序：先上屏「明」，再講 z 唔接受。
                string first = FirstCandidate();
                if (!string.IsNullOrEmpty(first))
                {
                    Commit(first);
                }
                else
                {
                    Reset();
                }
                _code.Length = 0;
                _candidates = new List<string>();
                _page = 0;
            }

            // 'z' 唔係倉頡字根，任何位置都唔接受（第一碼、第二碼、或者滿兩碼之後都一樣）。
            // 交回系統讓 z 照常打出來 —— 見 AGENTS.md 硬規則 7。
            if (lower == 'z') return false;

            _code.Append(lower);
            RefreshCandidates();
            Raise();
            return true;
        }

        private void RefreshCandidates()
        {
            _candidates = new List<string>(_dict.Lookup(_code.ToString(), _code.Length));
            _page = 0;
        }

        /// <summary>直接把目前的第一候選上屏（Enter／標點／單頁空白用）。</summary>
        public bool CommitFirst()
        {
            if (!IsComposing) return false;
            string first = FirstCandidate();
            if (!string.IsNullOrEmpty(first)) Commit(first);
            else Reset();
            return true;
        }

        /// <summary>以序號（0-based，於目前頁）選字。</summary>
        public bool SelectOnPage(int indexOnPage)
        {
            if (!IsComposing) return false;
            int idx = _page * PageSize + indexOnPage;
            if (idx < 0 || idx >= _candidates.Count) return false;
            Commit(_candidates[idx]);
            return true;
        }

        public void Commit(string text)
        {
            _code.Length = 0;
            _candidates = new List<string>();
            _page = 0;
            PendingText = text;
            Raise();
        }

        public void ClearPending()
        {
            PendingText = null;
        }

        /// <summary>翻頁。dir = +1 下一頁 / -1 上一頁。回傳是否有變化。</summary>
        public bool PageMove(int dir)
        {
            if (!IsComposing) return false;
            int np = _page + dir;
            if (np < 0) np = 0;
            if (np >= TotalPages) np = TotalPages - 1;
            if (np == _page) return false;
            _page = np;
            Raise();
            return true;
        }

        public bool PageSelect(int pageIndex)
        {
            if (!IsComposing) return false;
            if (pageIndex < 0 || pageIndex >= TotalPages) return false;
            _page = pageIndex;
            Raise();
            return true;
        }

        /// <summary>退格：刪一個碼；若無碼則回傳 false（讓系統處理）。</summary>
        public bool Backspace()
        {
            if (_code.Length == 0) return false;
            _code.Length = _code.Length - 1;
            if (_code.Length == 0)
            {
                _candidates = new List<string>();
                _page = 0;
            }
            else
            {
                RefreshCandidates();
            }
            Raise();
            return true;
        }

        /// <summary>Esc：取消組字。</summary>
        public bool Cancel()
        {
            if (!IsComposing) return false;
            Reset();
            return true;
        }

        /// <summary>目前組字碼的候選總數。</summary>
        public int Count { get { return _candidates.Count; } }

        /// <summary>目前頁是否已經係最後一頁（候選窗底部提示用）。</summary>
        public bool IsLastPage { get { return _page >= TotalPages - 1; } }

        /// <summary>所有候選（測試用）。</summary>
        public IList<string> AllCandidates { get { return _candidates; } }

        public const int CandidatesPerPage = PageSize;
    }
}
