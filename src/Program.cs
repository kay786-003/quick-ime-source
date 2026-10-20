// ============================================================================
//  Program.cs - 速成輸入法 主程式
//  免安裝、免管理員權限、單一 exe（字典內嵌）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QuickIME
{
    internal static class Program
    {
        internal const string AppName = "速成輸入法";

        /// <summary>
        /// 版本號。<see cref="VersionInfo.Version"/> 由 build.ps1 從 VERSION 檔自動產生，
        /// 所以只有一個地方要改（repo 根的 VERSION），不會再出現
        /// 「Program.Version 是 2.3 但 zip 名還寫著 v2.3」這種漏改。
        /// </summary>
        internal const string Version = VersionInfo.Version;

        internal static readonly string LogPath = ResolveLogPath();

        private static string ResolveLogPath()
        {
            // 依序嘗試可寫位置，避免 Temp 被鎖或 sandbox 限制
            var candidates = new List<string>();
            try { candidates.Add(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "QuickIME.log")); } catch { }
            try { candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickIME.log")); } catch { }
            try { candidates.Add(Path.Combine(Path.GetTempPath(), "QuickIME.log")); } catch { }
            foreach (string c in candidates)
            {
                try
                {
                    File.AppendAllText(c, "", new UTF8Encoding(false));
                    return c;
                }
                catch { }
            }
            return candidates.Count > 0 ? candidates[0] : "QuickIME.log";
        }

        internal static void Log(string msg)
        {
            if (!Verbose) return;
            try
            {
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>
        /// 惰性日誌：<see cref="Verbose"/> = false 時**完全不會組字串**。
        ///
        /// 為什麼要這樣：掛鉤對延遲極敏感（LowLevelHooksTimeout 預設 300ms，
        /// 逾時系統會靜默移除掛鉤，輸入法整個失效）。舊版在掛鉤內寫
        /// <c>Log("hook: vk=" + vk + ...)</c>，即使沒開 log 也會先做字串串接
        /// 與裝箱。改成這個重載之後，關閉日誌時連 lambda 都不會被呼叫。
        /// </summary>
        internal static void Log(Func<string> msg)
        {
            if (!Verbose) return;
            try { Log(msg()); } catch { }
        }

        internal static bool Verbose;

        [STAThread]
        private static int Main(string[] args)
        {
            // 注意：winexe（無主控台）下設定 Console.OutputEncoding 會拋 IOException，
            // 所以主控台相關設定一律包在 try/catch，且只在有主控台時才做。
            bool hasConsole = args.Length > 0;
            if (hasConsole)
            {
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            }
            // 主控台模式（供測試 / 診斷）
            if (hasConsole)
            {
                if (args[0] == "--selftest") return SelfTest.RunOn(LoadDictAny());
                if (args[0] == "--version") { Console.WriteLine(AppName + " " + Version); return 0; }
                if (args[0] == "--uishop")
                {
                    Application.EnableVisualStyles();
                    string outPath = args.Length > 1 ? args[1] : "shot.png";
                    return UiShot.Run(ResolveDictPath(), outPath);
                }
                if (args[0] == "--iconshot")
                {
                    Application.EnableVisualStyles();
                    return IconShot.Run(args.Length > 1 ? args[1] : ".");
                }
                if (args[0] == "--gametest") return GameTest.Run();
                if (args[0] == "--hooktest") return HookTest.Run();
                if (args[0] == "--diag")
                {
                    Verbose = true;
                    try { File.Delete(LogPath); } catch { }
                    Log("diag mode start, log=" + LogPath);
                }
                if (args[0] == "--help" || args[0] == "-h" || args[0] == "/?")
                {
                    Console.WriteLine(AppName + " " + Version);
                    Console.WriteLine();
                    Console.WriteLine("  直接執行           啟動輸入法（常駐系統匣）");
                    Console.WriteLine("  --selftest [字典檔] 執行自我測試");
                    Console.WriteLine("  --hooktest         按鍵流程重播測試（連打／z／數字／Enter／Ctrl 符號）");
                    Console.WriteLine("  --gametest         打機情境測試（長按 Shift 不誤切換）");
                    Console.WriteLine("  --iconshot [資料夾] 產生三種狀態的系統匣圖示");
                    Console.WriteLine("  --uishop [檔案]    產生候選窗 / 聯想窗截圖");
                    Console.WriteLine("  --diag             啟動並寫出診斷日誌");
                    Console.WriteLine("  --version          顯示版本");
                    return 0;
                }
            }

            // 只允許一個實例
            bool created;
            using (var mutex = new Mutex(true, "QuickIME_SingleInstance_v1", out created))
            {
                if (!created)
                {
                    MessageBox.Show(AppName + " 已經在執行中。\n請看右下角系統匣（時鐘旁邊）的圖示。",
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => ShowFatal(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowFatal(e.ExceptionObject as Exception);

                try
                {
                    Program.Log("=== start ===");
                    using (var app = new ImeApplication())
                    {
                        Program.Log("app constructed");
                        app.Start();
                        Program.Log("started, entering message loop");
                        Application.Run(app);
                        Program.Log("message loop exited");
                    }
                }
                catch (Exception ex)
                {
                    Program.Log("FATAL: " + ex);
                    ShowFatal(ex);
                    return 1;
                }
            }
            return 0;
        }

        private static void ShowFatal(Exception ex)
        {
            if (ex == null) return;
            try
            {
                MessageBox.Show("輸入法發生錯誤：\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    AppName + " - 錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        internal static string ResolveDictPath()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string p = Path.Combine(exeDir, "qdict.bin");
            if (File.Exists(p)) return p;
            return "qdict.bin";
        }

        /// <summary>載入字典：先試外部 qdict.bin，再試內嵌資源。</summary>
        internal static Dict LoadDictAny()
        {
            string ext = ResolveDictPath();
            if (File.Exists(ext))
            {
                try { return Dict.Load(ext); }
                catch (InvalidDataException) { /* 外部檔壞，改試內嵌 */ }
            }
            var asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
                if (name.EndsWith("qdict.bin", StringComparison.OrdinalIgnoreCase))
                    return Dict.LoadEmbedded(asm, name);
            throw new FileNotFoundException("找不到 qdict.bin（外部或內嵌都沒有）");
        }
    }

    /// <summary>應用程式主體：托盤圖示 + 掛鉤 + 候選窗 + 設定。</summary>
    internal sealed class ImeApplication : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly Composer _composer;
        private readonly KeyboardHook _hook;
        private readonly CandidateForm _cand;
        private readonly ToolStripMenuItem _modeItem;
        private readonly ToolStripMenuItem _shiftLeftItem;
        private readonly ToolStripMenuItem _shiftRightItem;
        private readonly ToolStripMenuItem _ctrlSpaceItem;
        private readonly ToolStripMenuItem _assocItem;
        private readonly ToolStripMenuItem _assocLearnItem;
        private readonly ToolStripMenuItem _assocShiftDigitsItem;
        private readonly ToolStripMenuItem _assocClearItem;
        private readonly Settings _settings;

        // ---- 聯想字 ----
        private readonly Assoc _assoc;
        private readonly System.Windows.Forms.Timer _assocHideTimer;

        public ImeApplication()
        {
            Program.Log("ctor: load settings");
            _settings = Settings.Load();

            Dict dict;
            try { dict = LoadDict(); }
            catch (Exception ex)
            {
                MessageBox.Show("無法載入字典檔 (qdict.bin)：\n\n" + ex.Message +
                    "\n\n請確認 qdict.bin 與本程式放在同一個資料夾。",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
            Program.Log("ctor: dict loaded");

            _composer = new Composer(dict);
            _hook = new KeyboardHook(_composer);
            _hook.ChineseMode = _settings.ChineseMode;
            _hook.Gate.Sides = _settings.ShiftSides;
            _hook.Gate.CtrlSpaceBlocked = _settings.BlockCtrlSpace;

            _cand = new CandidateForm();
            _cand.Attach(_composer);
            Program.Log("ctor: candidate form created");

            // ---- 聯想字：載入出廠詞表 + 個人記憶 ----
            _assoc = new Assoc();
            _assoc.LoadSeedFromAssembly(Assembly.GetExecutingAssembly());
            _assoc.LoadMemory(Settings.AssocFilePath);
            _hook.InitAssoc(_assoc, delegate { SaveAssoc(); });
            _hook.AssocEnabled = _settings.AssocEnabled;
            _hook.AssocLearnEnabled = _settings.AssocLearnEnabled;
            _hook.AssocShiftDigits = _settings.AssocShiftDigits;
            Program.Log("ctor: assoc loaded, pairs=" + _assoc.LearnedPairs +
                        ", learn=" + _settings.AssocLearnEnabled);

            // 聯想窗跟隨掛鉤狀態重畫
            // 注意：掛鉤會用 UiDispatcher 把這個 delegate 丟回 UI 執行緒才真正重畫，
            // 避免在掛鉤執行緒同步建立視窗（會拖慢輸入，甚至被系統移除掛鉤）。
            _hook.UiDispatcher = delegate(Action a)
            {
                if (_cand.IsDisposed || _cand.Disposing) return;
                try { _cand.BeginInvoke(a); } catch { /* 視窗已關 */ }
            };
            _hook.AssocChanged += (s, e) =>
            {
                if (!_hook.AssocActive) { _cand.HideNow(); return; }
                _cand.AssocShiftDigits = _hook.AssocShiftDigits;
                _cand.ShowAssoc(_hook.ContextPrev, _hook.AssocItems);
                RestartAssocHideTimer();
            };

            // 聯想窗久唔用就自動收埋，唔好阻住畫面
            _assocHideTimer = new System.Windows.Forms.Timer();
            _assocHideTimer.Interval = AssocHideMs;
            _assocHideTimer.Tick += (s, e) => { _assocHideTimer.Stop(); _hook.ResetAssoc(); _cand.HideNow(); };

            _modeItem = new ToolStripMenuItem("中文模式（速成）", null, (s, e) => ToggleMode());

            // ---- 設定：中英切換鍵 / Ctrl+空白 / 聯想字 ----
            _shiftLeftItem = MakeCheck("左邊 Shift 單按切換中／英", false, (s, e) => ToggleShiftSide(ShiftToggleSides.Left));
            _shiftRightItem = MakeCheck("右邊 Shift 單按切換中／英", false, (s, e) => ToggleShiftSide(ShiftToggleSides.Right));
            _ctrlSpaceItem = MakeCheck("攔住 Ctrl+空白（唔會轉全形符號）", false, (s, e) => ToggleCtrlSpace());
            _assocItem = MakeCheck("聯想字（打完字自動提示下一個字）", false, (s, e) => ToggleAssoc());
            _assocLearnItem = MakeCheck("記錄用字習慣（常用的組合排更前）", false, (s, e) => ToggleAssocLearn());
            _assocShiftDigitsItem = MakeCheck("Shift + 數字鍵選聯想字（同 Windows 速成一樣）", false, (s, e) => ToggleAssocShiftDigits());

            var settingsMenu = new ToolStripMenuItem("設定");
            settingsMenu.DropDownItems.Add(new ToolStripMenuItem("中英切換鍵（單按 Shift）") { Enabled = false });
            settingsMenu.DropDownItems.Add(_shiftLeftItem);
            settingsMenu.DropDownItems.Add(_shiftRightItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(_ctrlSpaceItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(_assocItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(new ToolStripMenuItem("符號：直接按半形，Ctrl+符號鍵打全形") { Enabled = false });

            // ---- 聯想字記憶 ----
            _assocClearItem = new ToolStripMenuItem("清空聯想字記憶…", null, (s, e) => ClearAssocMemory());
            var assocMenu = new ToolStripMenuItem("聯想字");
            assocMenu.DropDownItems.Add(new ToolStripMenuItem("打完一個字，列出你接住最常打嘅字") { Enabled = false });
            assocMenu.DropDownItems.Add(new ToolStripSeparator());
            assocMenu.DropDownItems.Add(_assocItem);
            assocMenu.DropDownItems.Add(_assocLearnItem);
            assocMenu.DropDownItems.Add(_assocShiftDigitsItem);
            assocMenu.DropDownItems.Add(new ToolStripSeparator());
            assocMenu.DropDownItems.Add(_assocClearItem);

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem(Program.AppName + "  " + Program.Version) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_modeItem);
            menu.Items.Add(assocMenu);
            menu.Items.Add(settingsMenu);
            menu.Items.Add(new ToolStripMenuItem("輸入法說明 / 速成碼表", null, (s, e) => ShowHelp()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("暫停輸入法（還原鍵盤）", null, (s, e) => TogglePause()));
            menu.Items.Add(new ToolStripMenuItem("結束", null, (s, e) => ExitApp()));

            _tray = new NotifyIcon
            {
                Icon = BuildIcon(_settings.ChineseMode, false),
                Text = Program.AppName + " " + Program.Version + "（速成）",
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => ToggleMode();
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleMode(); };
            Program.Log("ctor: tray created");

            SyncSettingsMenu();
            UpdateTray();
            Program.Log("ctor: done");
        }

        /// <summary>聯想窗自動收起時間（毫秒）。</summary>
        private const int AssocHideMs = 6000;

        private void RestartAssocHideTimer()
        {
            if (_assocHideTimer == null) return;
            _assocHideTimer.Stop();
            if (_hook.AssocActive) _assocHideTimer.Start();
        }

        /// <summary>把記憶寫入檔案（失敗都唔會吵 —— 網吧環境寫唔到係正常）。</summary>
        private void SaveAssoc()
        {
            string path = Settings.AssocFilePath;
            if (path == null) return;
            if (!_assoc.SaveMemory(path))
                Program.Log("assoc: save failed -> " + path);
        }


        private static ToolStripMenuItem MakeCheck(string text, bool value, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            item.Checked = value;
            item.CheckOnClick = false;   // 自己控制，避免點一下就被 Toggle 兩次
            return item;
        }

        private Dict LoadDict()
        {
            return Program.LoadDictAny();
        }

        public void Start()
        {
            _hook.Install();
            _hook.ModeChanged += (s, e) =>
            {
                _settings.ChineseMode = _hook.ChineseMode;
                _settings.Save();
                UpdateTray();
            };
            _settings.CheckVersionNotice();
        }

        // ------------------------------------------------------------------
        //  設定項
        // ------------------------------------------------------------------
        private void ToggleShiftSide(ShiftToggleSides side)
        {
            _hook.Gate.Sides = _hook.Gate.Sides ^ side;
            _settings.ShiftSides = _hook.Gate.Sides;
            _settings.Save();
            SyncSettingsMenu();
            Program.Log("settings: shift sides = " + _hook.Gate.Sides);
        }

        private void ToggleCtrlSpace()
        {
            _hook.Gate.CtrlSpaceBlocked = !_hook.Gate.CtrlSpaceBlocked;
            _settings.BlockCtrlSpace = _hook.Gate.CtrlSpaceBlocked;
            _settings.Save();
            SyncSettingsMenu();
            Program.Log("settings: block ctrl+space = " + _hook.Gate.CtrlSpaceBlocked);
        }

        private void ToggleAssoc()
        {
            _settings.AssocEnabled = !_settings.AssocEnabled;
            _hook.AssocEnabled = _settings.AssocEnabled;
            _settings.Save();
            if (!_settings.AssocEnabled)
            {
                _assocHideTimer.Stop();
                _hook.ResetAssoc();
                _cand.HideNow();
            }
            SyncSettingsMenu();
            Program.Log("settings: assoc = " + _settings.AssocEnabled);
        }

        /// <summary>
        /// 切換「記錄用字習慣」。
        ///
        /// 關掉只是「唔再學新嘅」，已學到的記憶仍然保留、亦仍然會用來排序。
        /// 這樣用家可以「暫停學習」而不失去之前的成果；想真正清走記憶就用「清空」。
        /// </summary>
        private void ToggleAssocLearn()
        {
            _settings.AssocLearnEnabled = !_settings.AssocLearnEnabled;
            _hook.AssocLearnEnabled = _settings.AssocLearnEnabled;
            _settings.Save();

            // 關掉學習時，先把未寫入的記憶存好，避免暫停前最後幾筆遺失
            if (!_settings.AssocLearnEnabled) _hook.FlushAssocSave();

            SyncSettingsMenu();
            Program.Log("settings: assoc learn = " + _settings.AssocLearnEnabled);

            ShellNotify.Show(
                Program.AppName + " - 記錄用字習慣" + (_settings.AssocLearnEnabled ? "已開啟" : "已關閉"),
                _settings.AssocLearnEnabled
                    ? "你打過的組合會重新學習，常用的排更前。"
                    : "已停止學習新習慣；之前學到的記憶仍然保留，聯想仍然照舊排序。");
        }

        /// <summary>
        /// 切換「聯想字要唔要 Shift+數字鍵選」。
        ///
        /// 預設開（Windows 速成習慣）：Shift+1-9 選聯想字，直接按 1-9 就打數字。
        /// 關掉就返回 v2.2 舊行為：直接按 1-9 選聯想字。
        /// </summary>
        private void ToggleAssocShiftDigits()
        {
            _settings.AssocShiftDigits = !_settings.AssocShiftDigits;
            _hook.AssocShiftDigits = _settings.AssocShiftDigits;
            _settings.Save();
            SyncSettingsMenu();
            Program.Log("settings: assoc shift+digits = " + _settings.AssocShiftDigits);

            ShellNotify.Show(
                Program.AppName + " - 聯想字選字鍵",
                _settings.AssocShiftDigits
                    ? "已改為 Shift + 數字鍵選聯想字（跟 Windows 速成）；直接按 1-9 會打數字。"
                    : "已改為直接按 1-9 選聯想字。");
        }

        /// <summary>清空聯想字記憶（先確認，因為呢個動作不可逆）。</summary>
        private void ClearAssocMemory()
        {
            int pairs = _assoc.LearnedPairs;
            if (pairs == 0)
            {
                MessageBox.Show("目前未有任何聯想字記憶（你仲未打過字，或者之前已經清空過）。",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var r = MessageBox.Show(
                "確定要清空聯想字記憶嗎？\n\n" +
                "  • 會刪除你打過嘅 " + pairs + " 組聯想學習記錄\n" +
                "  • 之後會返去出廠預設嘅聯想詞表\n" +
                "  • 速成候選字次序唔會受影響\n\n" +
                "呢個動作無法復原。",
                Program.AppName + " - 清空聯想字記憶",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (r != DialogResult.Yes) return;

            _hook.FlushAssocSave();
            string path = Settings.AssocFilePath;
            bool ok = _assoc.ClearAndSave(path);
            _hook.ResetAssoc();
            _cand.HideNow();
            SyncSettingsMenu();

            Program.Log("assoc: cleared " + pairs + " pairs, saved=" + ok);
            ShellNotify.Show(Program.AppName + " - 聯想字記憶已清空",
                ok ? "已重設為出廠預設。你之後打的字會重新學習。"
                   : "記憶已重設，但寫入檔案失敗（可能係資料夾唔可寫）。");
        }

        private void SyncSettingsMenu()
        {
            _shiftLeftItem.Checked = _hook.Gate.IsSideEnabled(ToggleGate.LeftSide);
            _shiftRightItem.Checked = _hook.Gate.IsSideEnabled(ToggleGate.RightSide);
            _ctrlSpaceItem.Checked = _hook.Gate.CtrlSpaceBlocked;

            _assocItem.Checked = _settings.AssocEnabled;
            _assocLearnItem.Checked = _settings.AssocLearnEnabled;
            _assocShiftDigitsItem.Checked = _settings.AssocShiftDigits;
            // 聯想字關掉時，「記錄習慣」同「Shift+數字」無意義 → 變灰（但保留原本的勾選狀態）
            _assocLearnItem.Enabled = _settings.AssocEnabled;
            _assocShiftDigitsItem.Enabled = _settings.AssocEnabled;
            _assocClearItem.Enabled = _settings.AssocEnabled;
            _assocClearItem.Text = _assoc.LearnedPairs > 0
                ? "清空聯想字記憶…（已學習 " + _assoc.LearnedPairs + " 組）"
                : "清空聯想字記憶…";

            string hint = ToggleGate.Describe(_hook.Gate.Sides);
            _tray.Text = _paused
                ? Program.AppName + "（已暫停）"
                : Program.AppName + "（" + (_hook.ChineseMode ? "速成" : "英文") + "／" + hint + "）";
        }


        private void ToggleMode()
        {
            _hook.ChineseMode = !_hook.ChineseMode;
            if (!_hook.ChineseMode)
            {
                _composer.Reset();
                _assocHideTimer.Stop();
                _hook.ResetAssoc();
                _cand.HideNow();
            }
            UpdateTray();
        }

        private bool _paused;
        private void TogglePause()
        {
            _paused = !_paused;
            _hook.Enabled = !_paused;
            if (_paused)
            {
                _composer.Reset();
                _cand.HideNow();
                _hook.Gate.Reset();
                _assocHideTimer.Stop();
                _hook.ResetAssoc();
            }
            UpdateTray();
        }


        private void UpdateTray()
        {
            if (_paused)
            {
                _tray.Text = Program.AppName + "（已暫停）";
                _modeItem.Text = "繼續輸入法";
            }
            else if (_hook.ChineseMode)
            {
                _tray.Text = Program.AppName + "（速成，中文模式）";
                _modeItem.Text = "切換到英文模式";
            }
            else
            {
                _tray.Text = Program.AppName + "（英文模式）";
                _modeItem.Text = "切換到中文模式（速成）";
            }
            SyncSettingsMenu();
            _tray.Icon = BuildIcon(_hook.ChineseMode, _paused);
        }

        private void ShowHelp()
        {
            string path = Path.Combine(Path.GetTempPath(), "速成輸入法說明.txt");
            try { File.WriteAllText(path, HelpText(), new UTF8Encoding(true)); }
            catch { }
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }

        private static string HelpText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("速成輸入法 " + Program.Version);
            sb.AppendLine();
            sb.AppendLine("【怎麼開始打字】");
            sb.AppendLine("  1. 執行 QuickIME.exe，右下角時鐘旁邊會出現一個「速」字圖示。");
            sb.AppendLine("  2. 在任何程式（記事本、瀏覽器、遊戲聊天）都可以直接打字。");
            sb.AppendLine("  3. 打兩個英文字母，就會出現候選的中文字。");
            sb.AppendLine();
            sb.AppendLine("【速成規則】");
            sb.AppendLine("  速成 = 倉頡取「頭碼」＋「尾碼」兩個字根。");
            sb.AppendLine();
            sb.AppendLine("  倉頡字根對應鍵盤：");
            sb.AppendLine("    a 日   b 月   c 金   d 木   e 水   f 火   g 土");
            sb.AppendLine("    h 竹   i 戈   j 十   k 大   l 中   m 一   n 弓");
            sb.AppendLine("    o 人   p 心   q 手   r 口   s 尸   t 廿   u 山");
            sb.AppendLine("    v 女   w 田   x 難   y 卜   z 重");
            sb.AppendLine();
            sb.AppendLine("  例：");
            sb.AppendLine("    明 = a b   （日 + 月）");
            sb.AppendLine("    你 = o f   （人 + ...）→ 第 2 個候選");
            sb.AppendLine("    好 = v d");
            sb.AppendLine("    香 = h a    港 = e u");
            sb.AppendLine("    中 = l      人 = o      日 = a   （單碼也可以）");
            sb.AppendLine();
            sb.AppendLine("【按鍵】");
            sb.AppendLine("  空白鍵      下一頁（微軟速成慣例；到最後一頁再按 = 繞返第 1 頁，");
            sb.AppendLine("              循環翻頁，永遠唔會偷偷上屏）");
            sb.AppendLine("  數字 1-9    選第 1-9 個候選字（一頁 9 個；0 不選字）");
            sb.AppendLine("  PageUp / -   上一頁");
            sb.AppendLine("  PageDown / + 下一頁（與空白鍵相同）");
            sb.AppendLine("  退格鍵      刪一個輸入碼");
            sb.AppendLine("  Esc         取消這次輸入");
            sb.AppendLine("  右 Shift（單按）切換中／英文");
            sb.AppendLine();
            sb.AppendLine("【全形標點：Ctrl + 符號鍵】");
            sb.AppendLine("  跟微軟速成一樣，按住 Ctrl 再按符號鍵就是全形：");
            sb.AppendLine("    Ctrl+,  → ，      Ctrl+.  → 。      Ctrl+;  → ；");
            sb.AppendLine("    Ctrl+'  → ：      Ctrl+[  → 「      Ctrl+]  → 」");
            sb.AppendLine("    Ctrl+\\  → 、      Ctrl+-  → －      Ctrl+=  → ＝");
            sb.AppendLine("  而 ? ! , . 直接按就是半形，不會變全形。");
            sb.AppendLine("  組字中按 Ctrl+符號鍵，會先上屏目前候選，再出全形標點。");
            sb.AppendLine();
            sb.AppendLine("【中英切換鍵：點解只用右 Shift】");
            sb.AppendLine("  打機時長按 Shift 跑步是常態，若放開 Shift 就切換成英文，");
            sb.AppendLine("  角色就會突然不受控。所以本程式：");
            sb.AppendLine("    * 預設只有「右 Shift」單按才切換中／英（左 Shift 留給遊戲）");
            sb.AppendLine("    * 按住 Shift 期間再按其他鍵（WASD / 方向鍵）→ 一律當長按，不切換");
            sb.AppendLine("    * 按住超過 0.7 秒才放開 → 一律當長按，不切換");
            sb.AppendLine("    * 按住 Shift 期間完全不組字，鍵盤原樣傳給遊戲");
            sb.AppendLine("  想左右都可以切換（或者兩邊都關掉），右鍵圖示 → 設定 →");
            sb.AppendLine("  「左邊 / 右邊 Shift 單按切換中／英」。");
            sb.AppendLine();
            sb.AppendLine("【符號：半形為主，Ctrl 打全形】");
            sb.AppendLine("  直接按 , . ? ! 只會送出半形（, . ? !），不會自動變成全形。");
            sb.AppendLine("  要打全形標點就按住 Ctrl 再按符號鍵（見上面「全形標點」一節）。");
            sb.AppendLine();
            sb.AppendLine("【Ctrl+空白：預設攔住】");
            sb.AppendLine("  Ctrl+空白 在微軟速成是「全形／半形」切換，打機緊張時很容易誤按。");
            sb.AppendLine("  本程式預設把它完全攔住（按了完全沒反應）。");
            sb.AppendLine("  想恢復原本的 Ctrl+空白 行為：右鍵圖示 → 設定 → 取消勾選那一項。");
            sb.AppendLine();
            sb.AppendLine("【聯想字：打完一個字，提示下一個字】");
            sb.AppendLine("  例：你打「你」→ 下面列出「好 嗎 係 唔 的 有 會 想 了」");
            sb.AppendLine("     按住 Shift 再按 1-9 直接選，第二個字之後又會再提示下一次。");
            sb.AppendLine();
            sb.AppendLine("  佢會「學」你嘅習慣：");
            sb.AppendLine("    你打「你」之後成日接「係」，打多幾次，「係」就會排到最前，");
            sb.AppendLine("    好似拼音輸入法咁越用越貼你。");
            sb.AppendLine();
            sb.AppendLine("  聯想窗操作：");
            sb.AppendLine("    Shift + 數字 1-9  選聯想字（跟 Windows 速成習慣）");
            sb.AppendLine("    數字 1-9          直接打數字（唔會被聯想窗吞掉）");
            sb.AppendLine("    空格              收起聯想窗（唔會上屏任何東西）");
            sb.AppendLine("    Esc               收起聯想窗");
            sb.AppendLine("    直接打速成碼      照常打字（聯想窗會自動收起）");
            sb.AppendLine("  （聯想窗 6 秒無用會自動收埋，唔會阻住畫面）");
            sb.AppendLine();
            sb.AppendLine("  聯想字只會建議「單一個中文字」；標點、英文、數字都唔會學。");
            sb.AppendLine("  速成候選字嘅次序完全唔受影響（呢個係刻意設計）。");
            sb.AppendLine();
            sb.AppendLine("  ★ 清空聯想字記憶：右鍵圖示 → 聯想字 → 清空聯想字記憶…");
            sb.AppendLine("    清空之後會返去出廠預設嘅聯想詞表，你再打就會重新學過。");
            sb.AppendLine("    記憶存在 assoc.txt，可以人手刪除（見下面「設定檔」）。");
            sb.AppendLine();
            sb.AppendLine("  ★ 記錄習慣開關：右鍵圖示 → 聯想字 → 「記錄用字習慣」");
            sb.AppendLine("    勾選（預設）：會學你嘅用字習慣，常用組合排更前。");
            sb.AppendLine("    取消勾選：仍然有聯想字，但只用出廠詞表，唔會再學新嘅。");
            sb.AppendLine("              （之前學到嘅記憶會保留，聯想仍然照舊排序）");
            sb.AppendLine();
            sb.AppendLine("  ★ 選字鍵：右鍵圖示 → 聯想字 → 「Shift + 數字鍵選聯想字」");
            sb.AppendLine("    勾選（預設）：Shift+1-9 選聯想字，直接按 1-9 就打數字。");
            sb.AppendLine("    取消勾選：直接按 1-9 選聯想字（v2.2 舊習慣）。");
            sb.AppendLine();
            sb.AppendLine("  唔想用聯想字：右鍵圖示 → 聯想字 → 取消勾選「聯想字」。");
            sb.AppendLine();
            sb.AppendLine("【連打】打滿兩個碼後再打字母，會自動送出第一個字，然後繼續打，");
            sb.AppendLine("        所以你可以一路打「abcdef...」連續出字。");
            sb.AppendLine();
            sb.AppendLine("【系統匣圖示】");
            sb.AppendLine("  按一下 / 雙擊  = 中英文切換");
            sb.AppendLine("  右鍵          = 選單（切換模式、設定、暫停、結束）");
            sb.AppendLine();
            sb.AppendLine("【如果打不到字】");
            sb.AppendLine("  - 遊戲若以「管理員身分」執行，本程式也要用管理員身分執行才能攔到鍵盤。");
            sb.AppendLine("  - 全螢幕獨佔（DirectX exclusive）遊戲可能攔不到，請用視窗模式。");
            sb.AppendLine("  - 防毒軟體可能詢問鍵盤掛鉤，請選「允許」。");
            sb.AppendLine();
            sb.AppendLine("【設定檔】");
            sb.AppendLine("  %APPDATA%\\QuickIME\\settings.ini   設定（刪掉即回覆預設值）");
            sb.AppendLine("  %APPDATA%\\QuickIME\\assoc.txt      聯想字學習記憶");
            sb.AppendLine("  若 AppData 被鎖，兩個檔會改放 exe 旁邊的 QuickIME\\ 資料夾。");
            return sb.ToString();
        }

        private void ExitApp()
        {
            try { _assocHideTimer.Stop(); } catch { }
            _hook.FlushAssocSave();
            _hook.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _cand.Dispose();
            _settings.Save();
            ExitThread();
        }

        /// <summary>
        /// 動態畫一個「速」字圖示。
        /// 顏色代表狀態，打機時用眼角瞄一眼就知：
        ///   藍 = 中文模式　灰 = 英文模式　深灰 = 已暫停
        /// 每個狀態各快取一個 Icon，避免每次重建造成 GDI handle 洩漏。
        /// </summary>
        private static readonly Icon[] _iconCache = new Icon[3];

        private const int IconCn = 0;
        private const int IconEn = 1;
        private const int IconPaused = 2;

        private static Icon BuildIcon(bool chinese, bool paused)
        {
            int slot = paused ? IconPaused : (chinese ? IconCn : IconEn);
            if (_iconCache[slot] != null) return _iconCache[slot];

            Color bg = paused ? Color.FromArgb(128, 128, 128)
                     : (chinese ? Color.FromArgb(0, 120, 215) : Color.FromArgb(150, 155, 160));
            string text = chinese ? "速" : "英";
            try
            {
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(bg))
                        g.FillEllipse(brush, 0, 0, 31, 31);
                    using (var f = new Font("Microsoft JhengHei", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var fg = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(text, f, fg, new RectangleF(0, 0, 32, 32), sf);
                    }
                    IntPtr h = bmp.GetHicon();
                    _iconCache[slot] = (Icon)Icon.FromHandle(h).Clone();
                    DestroyIcon(h);
                    return _iconCache[slot];
                }
            }
            catch
            {
                _iconCache[slot] = SystemIcons.Application;
                return _iconCache[slot];
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_assocHideTimer != null) { _assocHideTimer.Stop(); _assocHideTimer.Dispose(); }
                if (_hook != null) _hook.Dispose();
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_cand != null) _cand.Dispose();
            }
            base.Dispose(disposing);
        }

    }

    /// <summary>簡單設定持久化（放在使用者 AppData，刪掉即重設）。</summary>
    internal sealed class Settings
    {
        public bool ChineseMode = true;
        public string LastVersionSeen = "";

        /// <summary>哪幾邊的 Shift 單按會切換中／英。預設只用右邊。</summary>
        public ShiftToggleSides ShiftSides = ShiftToggleSides.Right;

        /// <summary>攔住 Ctrl+空白（唔會轉全形符號）。預設開。</summary>
        public bool BlockCtrlSpace = true;

        /// <summary>聯想字（打完一個字之後提示下一個字）。預設開。</summary>
        public bool AssocEnabled = true;

        /// <summary>
        /// 記錄用字習慣（把學過的組合排到聯想最前）。預設開。
        ///
        /// 與 <see cref="AssocEnabled"/> 刻意分開：
        ///   * AssocEnabled = false      → 完全唔出聯想窗
        ///   * AssocLearnEnabled = false → 仍然出聯想窗，但只用出廠詞表，唔會學習
        /// 兩者都開（預設）= 完整體驗：有建議，亦會越用越貼你。
        /// </summary>
        public bool AssocLearnEnabled = true;

        /// <summary>
        /// 聯想字要按住 Shift + 數字鍵才選得到（Windows 速成習慣）。預設開。
        ///
        /// 開：Shift+1-9 選聯想字，直接按 1-9 = 打數字。
        /// 關：直接按 1-9 選聯想字（v2.2 舊行為）。
        /// </summary>
        public bool AssocShiftDigits = true;

        /// <summary>
        /// 設定檔路徑。若 AppData 被鎖（網吧 / 受管制電腦常見）就回傳 null，
        /// 之後一律當「冇設定檔」處理 —— 寧願唔記得設定，都唔可以開唔到程式。
        /// </summary>
        private static string PathFile
        {
            get
            {
                string dir = DataDir;
                return dir == null ? null : System.IO.Path.Combine(dir, "settings.ini");
            }
        }

        /// <summary>聯想字記憶檔的完整路徑（與 settings.ini 同一個資料夾）。</summary>
        public static string AssocFilePath
        {
            get
            {
                string dir = DataDir;
                return dir == null ? null : System.IO.Path.Combine(dir, Assoc.MemoryFileName);
            }
        }

        /// <summary>
        /// 資料夾：優先用 AppData，唔可寫就退回 exe 同目錄（USB 手指通常可寫）。
        /// 兩處都唔得就回傳 null。快取結果，避免每次呼叫都試一次。
        /// </summary>
        private static string _cachedDir;
        private static bool _dirTried;

        private static string DataDir
        {
            get
            {
                if (_dirTried) return _cachedDir;
                _dirTried = true;
                try
                {
                    string dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickIME");
                    System.IO.Directory.CreateDirectory(dir);
                    _cachedDir = dir;
                    return _cachedDir;
                }
                catch { }

                // 退而求其次：放在 exe 同一個資料夾（USB 手指情境通常可寫）
                try
                {
                    string dir = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "QuickIME");
                    System.IO.Directory.CreateDirectory(dir);
                    _cachedDir = dir;
                    return _cachedDir;
                }
                catch { }

                _cachedDir = null;
                return null;
            }
        }


        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                string p = PathFile;
                if (p == null) return s;
                if (!File.Exists(p)) return s;
                s.Apply(File.ReadAllLines(p));
            }
            catch { }
            return s;
        }

        /// <summary>
        /// 由文字內容載入設定（與檔案解耦，方便 --selftest 驗證格式）。
        /// </summary>
        public void Apply(string[] lines)
        {
            if (lines == null) return;
            foreach (string line in lines)
            {
                if (line == null) continue;
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i).Trim();
                string v = line.Substring(i + 1).Trim();
                if (k == "ChineseMode") ChineseMode = (v != "0");
                else if (k == "LastVersionSeen") LastVersionSeen = v;
                else if (k == "ShiftSides") ShiftSides = ParseSides(v);
                else if (k == "BlockCtrlSpace") BlockCtrlSpace = (v != "0");
                else if (k == "AssocEnabled") AssocEnabled = (v != "0");
                else if (k == "AssocLearnEnabled") AssocLearnEnabled = (v != "0");
                else if (k == "AssocShiftDigits") AssocShiftDigits = (v != "0");
            }
        }

        /// <summary>序列化成 settings.ini 的內容。</summary>
        public string ToIni()
        {
            return "ChineseMode=" + (ChineseMode ? "1" : "0") + "\r\n" +
                   "ShiftSides=" + FormatSides(ShiftSides) + "\r\n" +
                   "BlockCtrlSpace=" + (BlockCtrlSpace ? "1" : "0") + "\r\n" +
                   "AssocEnabled=" + (AssocEnabled ? "1" : "0") + "\r\n" +
                   "AssocLearnEnabled=" + (AssocLearnEnabled ? "1" : "0") + "\r\n" +
                   "AssocShiftDigits=" + (AssocShiftDigits ? "1" : "0") + "\r\n" +
                   "LastVersionSeen=" + LastVersionSeen + "\r\n";
        }

        public static ShiftToggleSides ParseSides(string v)
        {
            // 寫成 Left / Right / Both / None，人手改都易明
            switch ((v ?? "").Trim().ToLowerInvariant())
            {
                case "left": return ShiftToggleSides.Left;
                case "right": return ShiftToggleSides.Right;
                case "both": return ShiftToggleSides.Both;
                case "none": return ShiftToggleSides.None;
            }
            int n;
            if (int.TryParse(v, out n))
                return (ShiftToggleSides)(n & (int)ShiftToggleSides.Both);
            return ShiftToggleSides.Right;
        }

        public static string FormatSides(ShiftToggleSides sides)
        {
            if (sides == ShiftToggleSides.Both) return "Both";
            if (sides == ShiftToggleSides.Left) return "Left";
            if (sides == ShiftToggleSides.Right) return "Right";
            return "None";
        }

        public void Save()
        {
            try
            {
                string p = PathFile;
                if (p == null) return;
                File.WriteAllText(p, ToIni(), new UTF8Encoding(false));
            }
            catch { }
        }

        public void CheckVersionNotice()
        {
            if (LastVersionSeen == Program.Version) return;
            LastVersionSeen = Program.Version;
            Save();
            ShellNotify.Show(
                Program.AppName + " " + Program.Version + " 已啟動",
                "新增 Ctrl + 符號鍵打全形標點（，。「」等）；" +
                "修正連打吞字、尾頁上屏送錯字、數字超界漏字。");
        }
    }

    internal static class ShellNotify
    {
        public static bool Show(string title, string text)
        {
            try
            {
                using (var icon = new NotifyIcon { Icon = SystemIcons.Information, Visible = true })
                {
                    icon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
                    Thread.Sleep(200);
                    icon.Visible = false;
                }
                return true;
            }
            catch { return false; }
        }
    }
}
