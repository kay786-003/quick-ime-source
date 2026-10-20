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
        private const int PageSize = 9;

        /// <summary>最近一次 Commit 要送出的文字。</summary>
        public string PendingText { get; private set; }

        /// <summary>狀態變更（組字碼 / 候選 / 頁碼）時觸發，讓 UI 重畫。</summary>
        public event EventHandler Changed;

        /// <summary>
        /// 空格鍵行為（預設：微軟速成習慣 = 下一頁，到尾頁就繞返第 1 頁）。
        /// 關掉之後空格變成「直接上屏目前反白嘅候選」。
        /// </summary>
        public bool SpaceIsNextPage { get; set; }

        public Composer(Dict dict)
        {
            _dict = dict;
            SpaceIsNextPage = true;   // 預設跟微軟速成：空格 = 下一頁
        }

        /// <summary>
        /// 按下空白鍵。
        /// * 有多頁 → 下一頁；到尾頁再按 → <b>繞返第 1 頁</b>（循環翻頁，永遠唔會偷偷上屏）
        /// * 只有一頁 → 上屏第 1 個（根本冇頁可翻，同微軟速成一樣）
        ///
        /// 點解尾頁要繞返第 1 頁：微軟速成用家習慣「一路按空白鍵掃候選字」，
        /// 掃到尾頁時手指仍然會再按一下。若果呢一下就上屏，
        /// 用家就會在「只係想再睇一次」嘅情況下打出一個字（而且係錯嘅字）。
        /// 所以空白鍵只負責翻頁；要上屏就按數字鍵（或 Enter／標點）。
        /// </summary>
        public bool PressSpace()
        {
            if (!IsComposing) return false;
            if (!SpaceIsNextPage) return CommitFirst();
            if (TotalPages <= 1) return CommitFirst();

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

        /// <summary>目前頁第一個候選字（空白鍵直接上屏用）。</summary>
        public string FirstCandidate()
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

            // 'z' 唔係倉頡字根，唔可以用嚟開新碼（避免一堆「無此碼」）
            if (lower == 'z' && _code.Length == 0) return false;

            if (_code.Length >= 2)
            {
                // 已滿兩碼：直接把目前首選上屏，再用這個字母開新一輪（連打）
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

            // 第二碼係 'z' 亦無意義（z 非字根），只在第一碼時才接受
            if (lower == 'z' && _code.Length > 0) return false;

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
