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
        internal const string Version = "1.0";

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
        private readonly Settings _settings;

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

            _cand = new CandidateForm();
            _cand.Attach(_composer);
            Program.Log("ctor: candidate form created");

            _modeItem = new ToolStripMenuItem("中文模式（速成）", null, (s, e) => ToggleMode());

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem(Program.AppName + "  " + Program.Version) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_modeItem);
            menu.Items.Add(new ToolStripMenuItem("輸入法說明 / 速成碼表", null, (s, e) => ShowHelp()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("暫停輸入法（還原鍵盤）", null, (s, e) => TogglePause()));
            menu.Items.Add(new ToolStripMenuItem("結束", null, (s, e) => ExitApp()));

            _tray = new NotifyIcon
            {
                Icon = BuildIcon(),
                Text = Program.AppName + " " + Program.Version + "（速成）",
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => ToggleMode();
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleMode(); };
            Program.Log("ctor: tray created");

            UpdateTray();
            Program.Log("ctor: done");
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

        private void ToggleMode()
        {
            _hook.ChineseMode = !_hook.ChineseMode;
            if (!_hook.ChineseMode) _composer.Reset();
            UpdateTray();
        }

        private bool _paused;
        private void TogglePause()
        {
            _paused = !_paused;
            _hook.Enabled = !_paused;
            if (_paused) { _composer.Reset(); _cand.Hide(); }
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
            _tray.Icon = BuildIcon();
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
            sb.AppendLine("  空白鍵      下一頁（微軟速成慣例；到最後一頁再按 = 選第 1 個字）");
            sb.AppendLine("  數字 1-9    選第 1-9 個候選字");
            sb.AppendLine("  PageUp / -   上一頁");
            sb.AppendLine("  PageDown / + 下一頁（與空白鍵相同）");
            sb.AppendLine("  退格鍵      刪一個輸入碼");
            sb.AppendLine("  Esc         取消這次輸入");
            sb.AppendLine("  Shift（單按）切換中／英文");
            sb.AppendLine();
            sb.AppendLine("【連打】打滿兩個碼後再打字母，會自動送出第一個字，然後繼續打，");
            sb.AppendLine("        所以你可以一路打「abcdef...」連續出字。");
            sb.AppendLine();
            sb.AppendLine("【系統匣圖示】");
            sb.AppendLine("  按一下 / 雙擊  = 中英文切換");
            sb.AppendLine("  右鍵          = 選單（切換模式、暫停、結束）");
            sb.AppendLine();
            sb.AppendLine("【如果打不到字】");
            sb.AppendLine("  - 遊戲若以「管理員身分」執行，本程式也要用管理員身分執行才能攔到鍵盤。");
            sb.AppendLine("  - 全螢幕獨佔（DirectX exclusive）遊戲可能攔不到，請用視窗模式。");
            sb.AppendLine("  - 防毒軟體可能詢問鍵盤掛鉤，請選「允許」。");
            return sb.ToString();
        }

        private void ExitApp()
        {
            _hook.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _cand.Dispose();
            _settings.Save();
            ExitThread();
        }

        /// <summary>動態畫一個「速」字圖示（快取，避免每次重建造成 GDI handle 洩漏）。</summary>
        private static Icon _iconCache;
        private static Icon BuildIcon()
        {
            if (_iconCache != null) return _iconCache;
            try
            {
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var bg = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        g.FillEllipse(bg, 0, 0, 31, 31);
                    using (var f = new Font("Microsoft JhengHei", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var fg = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("速", f, fg, new RectangleF(0, 0, 32, 32), sf);
                    }
                    IntPtr h = bmp.GetHicon();
                    _iconCache = (Icon)Icon.FromHandle(h).Clone();
                    DestroyIcon(h);
                    return _iconCache;
                }
            }
            catch
            {
                _iconCache = SystemIcons.Application;
                return _iconCache;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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

        private static string PathFile
        {
            get
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickIME");
                System.IO.Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "settings.ini");
            }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                string p = PathFile;
                if (File.Exists(p))
                {
                    foreach (string line in File.ReadAllLines(p))
                    {
                        int i = line.IndexOf('=');
                        if (i <= 0) continue;
                        string k = line.Substring(0, i).Trim();
                        string v = line.Substring(i + 1).Trim();
                        if (k == "ChineseMode") s.ChineseMode = (v != "0");
                        else if (k == "LastVersionSeen") s.LastVersionSeen = v;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(PathFile,
                    "ChineseMode=" + (ChineseMode ? "1" : "0") + "\r\n" +
                    "LastVersionSeen=" + LastVersionSeen + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        public void CheckVersionNotice()
        {
            if (LastVersionSeen == Program.Version) return;
            LastVersionSeen = Program.Version;
            Save();
            ShellNotify.Show(
                Program.AppName + " 已啟動",
                "按右下角「速」圖示可切換中／英。按此看使用說明。");
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
