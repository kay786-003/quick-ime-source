// ============================================================================
//  UiShot.cs - 把候選窗渲染成 PNG，方便離線檢查外觀（唔需要真人操作）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace QuickIME
{
    internal static class UiShot
    {
        public static int Run(string dictPath, string outPath)
        {
            Dict dict = Dict.Load(dictPath);
            var composer = new Composer(dict);
            var form = new CandidateForm();
            form.Attach(composer);

            // 強制佈局：模擬組字中（hi = 90 個候選，共 10 頁）
            composer.TypeLetter('h');
            composer.TypeLetter('i');

            // 用反射叫 Render 之前的尺寸計算，直接手動設定位置
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(0, 0, 7 * 2 + 44 * 9, 3 * 2 + 24 + 44 + 18);
            form.Show();
            Application.DoEvents();

            Save(form, outPath);
            Console.WriteLine("第一頁 -> " + outPath);

            // 第二頁（驗證翻頁外觀）
            composer.PageMove(1);
            Application.DoEvents();
            string p2 = Path.Combine(Path.GetDirectoryName(outPath), "shot-page2.png");
            Save(form, p2);
            Console.WriteLine("第二頁 -> " + p2);

            // 無候選情況
            composer.Reset();
            composer.TypeLetter('z');
            composer.TypeLetter('z');
            Application.DoEvents();
            form.Bounds = new Rectangle(0, 0, 168, 3 * 2 + 24 + 44 + 18);
            Application.DoEvents();
            string p3 = Path.Combine(Path.GetDirectoryName(outPath), "shot-empty.png");
            Save(form, p3);
            Console.WriteLine("無候選 -> " + p3);

            // 聯想字畫面（打完「你」之後列出下一個字）
            var assoc = new Assoc();
            assoc.LoadSeed("你 好 嗎 係 唔 的 有 會 想\n");
            var items = assoc.Lookup("你", "");
            Console.WriteLine("聯想字候選數 = " + items.Count);
            for (int i = 0; i < items.Count; i++) Console.WriteLine("  [" + i + "] = " + items[i]);
            form.ShowAssoc("你", "", items);
            // 寬度要同 CandidateForm 內部計算完全一致（PadX*2 + CellW*count），
            // 否則右邊會露出一截上一次的畫面。
            int assocW = 7 * 2 + 44 * Math.Min(items.Count, CandidateForm.AssocPerPage);
            int assocH = 3 * 2 + 24 + 44 + 18;
            form.Bounds = new Rectangle(0, 0, assocW, assocH);
            form.RepaintOnly();
            Application.DoEvents();
            string p4 = Path.Combine(Path.GetDirectoryName(outPath), "shot-assoc.png");
            Save(form, p4);
            Console.WriteLine("聯想字 -> " + p4);

            form.Close();
            form.Dispose();
            return 0;
        }

        private static void Save(Form f, string path)
        {
            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
