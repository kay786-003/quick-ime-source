// ============================================================================
//  Dict.cs - 速成 (Quick / Cangjie simplified) dictionary engine
//  Part of the macOS/Win "速成輸入法" standalone IME.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuickIME
{
    /// <summary>
    /// 速成字典：以「首碼 + 尾碼」兩碼查字，另支援單碼。
    /// 索引配置： 0..25  -> 'a'..'z' 單碼
    ///            26..701 -> 兩碼 (c1-'a')*26 + (c2-'a')
    ///
    /// 【極重要】候選字次序 = 微軟 Windows 速成內建排位次序（原表 sort: original）。
    /// 絕對不可以按字頻／常用度重排：用家靠記憶「第幾個位」打，
    /// 一旦改動次序就會打錯字。資料來源 ms_quick.dict.yaml 已按此原則固定。
    /// </summary>
    internal sealed class Dict
    {
        private const int MAGIC = 0x514B494D; // 'QKIM'
        private const int VERSION = 2;
        private const int SINGLE = 26;
        private const int TOTAL = 26 + 26 * 26;

        private readonly string[][] _buckets;

        public Dict(string[][] buckets)
        {
            _buckets = buckets;
        }

        public static int BucketCount { get { return TOTAL; } }

        public static int IndexOf(string code, int len)
        {
            if (len == 1) return code[0] - 'a';
            return SINGLE + (code[0] - 'a') * 26 + (code[1] - 'a');
        }

        /// <summary>回傳指定速成碼的候選字（可能為空）。</summary>
        public IList<string> Lookup(string code, int len)
        {
            if (len <= 0) return new string[0];
            if (len > 2) len = 2;
            int idx = IndexOf(code, len);
            if (idx < 0 || idx >= TOTAL) return new string[0];
            return _buckets[idx];
        }

        public int CountOf(string code, int len)
        {
            if (len <= 0) return 0;
            if (len > 2) len = 2;
            return _buckets[IndexOf(code, len)].Length;
        }

        // ------------------------------------------------------------------
        // IO
        // ------------------------------------------------------------------
        public static Dict Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            return LoadFromBytes(data);
        }

        public static Dict LoadFromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data, false))
            using (var br = new BinaryReader(ms, Encoding.UTF8))
            {
                int magic = br.ReadInt32();
                if (magic != MAGIC) throw new InvalidDataException("字典檔格式不正確 (magic mismatch)");
                int ver = br.ReadInt32();
                if (ver != VERSION) throw new InvalidDataException("字典檔版本不支援: " + ver);
                int nb = br.ReadInt32();
                if (nb != TOTAL) throw new InvalidDataException("字典檔 bucket 數量不正確: " + nb);
                int total = br.ReadInt32();

                var counts = new int[nb];
                for (int i = 0; i < nb; i++) counts[i] = br.ReadInt32();

                var buckets = new string[nb][];
                for (int i = 0; i < nb; i++)
                {
                    var arr = new string[counts[i]];
                    for (int k = 0; k < counts[i]; k++)
                        arr[k] = ((char)br.ReadUInt16()).ToString();
                    buckets[i] = arr;
                }
                return new Dict(buckets);
            }
        }

        /// <summary>內嵌資源版本載入。</summary>
        public static Dict LoadEmbedded(System.Reflection.Assembly asm, string resourceName)
        {
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null) throw new InvalidDataException("找不到內嵌字典資源: " + resourceName);
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return LoadFromBytes(ms.ToArray());
                }
            }
        }

        public void Save(string path)
        {
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8))
            {
                bw.Write(MAGIC);
                bw.Write(VERSION);
                bw.Write(TOTAL);
                int total = 0;
                for (int i = 0; i < TOTAL; i++) total += _buckets[i].Length;
                bw.Write(total);
                for (int i = 0; i < TOTAL; i++) bw.Write(_buckets[i].Length);
                for (int i = 0; i < TOTAL; i++)
                    for (int k = 0; k < _buckets[i].Length; k++)
                        bw.Write((ushort)_buckets[i][k][0]);
            }
        }

        /// <summary>除錯用: 回傳字形 + 倉頡字母說明。</summary>
        public static string CodeToRadicals(string code)
        {
            const string radicals = "日月金木水火土竹戈十大中一弓人心手口尸廿山女田難卜";
            var sb = new StringBuilder();
            foreach (char c in code)
            {
                int i = c - 'a';
                if (i >= 0 && i < radicals.Length) sb.Append(radicals[i]);
                else sb.Append('?');
            }
            return sb.ToString();
        }
    }
}
