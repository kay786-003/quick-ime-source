// ============================================================================
//  IconShot.cs - 把系統匣圖示的三種狀態渲染成 PNG，方便離線檢查顏色
//  用法：QuickIME.exe --iconshot 輸出資料夾
// ============================================================================
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace QuickIME
{
    internal static class IconShot
    {
        public static int Run(string outDir)
        {
            IconShotAccessor.Render(outDir);
            return 0;
        }
    }

    /// <summary>透過反射拿 ImeApplication 裡私有的 BuildIcon，避免為了測試而公開它。</summary>
    internal static class IconShotAccessor
    {
        public static void Render(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var t = typeof(ImeApplication);
            var m = t.GetMethod("BuildIcon",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m == null) { Console.WriteLine("找不到 BuildIcon"); return; }

            var states = new[]
            {
                new object[] { true,  false, "icon-cn.png",     "中文模式（藍）" },
                new object[] { false, false, "icon-en.png",     "英文模式（灰）" },
                new object[] { false, true,  "icon-paused.png", "已暫停（深灰）" }
            };
            foreach (var s in states)
            {
                var icon = (Icon)m.Invoke(null, new[] { s[0], s[1] });
                using (var bmp = icon.ToBitmap())
                {
                    bmp.Save(Path.Combine(outDir, (string)s[2]), ImageFormat.Png);
                }
                Console.WriteLine(s[3] + " -> " + s[2]);
            }
        }
    }
}
