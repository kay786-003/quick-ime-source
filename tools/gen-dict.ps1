# ============================================================================
#  gen-dict.ps1 - 由 rime-ms-quick 的 ms_quick.dict.yaml 產生 qdict.bin
#
#  【設計原則 — 請勿修改】
#  候選字次序 = 微軟 Windows 速成內建排位次序（原表 sort: original）。
#  絕對不可以按字頻／常用度重排：
#  速成用家靠記憶「第幾個位」打字，一改次序就會打錯字。
#
#  字典格式 v2：
#    int32  magic   = 0x514B494D ('QKIM')
#    int32  version = 2
#    int32  buckets = 702
#    int32  total   = 總字數
#    int32[702]     = 每個 bucket 的字數
#    uint16[total]  = 字元（UTF-16 code unit）
#
#  Bucket 索引：
#    0..25   -> 單碼 'a'..'z'
#    26..701 -> 兩碼 (c1-'a')*26 + (c2-'a')
# ============================================================================
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src  = Join-Path $root 'data\ms_quick.dict.yaml'
$out  = Join-Path $root 'src\qdict.bin'

if (-not (Test-Path $src)) { throw "找不到 data\ms_quick.dict.yaml" }

Write-Host "來源: $src" -ForegroundColor DarkGray

$text  = [System.IO.File]::ReadAllText($src, [System.Text.Encoding]::UTF8)
$lines = $text -split "`r?`n"

$SINGLE = 26
$TOTAL  = 26 + 26 * 26
$buckets = New-Object 'System.Collections.Generic.List[string][]' $TOTAL
for ($i = 0; $i -lt $TOTAL; $i++) { $buckets[$i] = New-Object 'System.Collections.Generic.List[string]' }

$seen = @{}
$n = 0; $bad = 0; $multi = 0

foreach ($ln in $lines) {
    if ($ln.Length -lt 3) { continue }
    if ($ln.StartsWith('#')) { continue }
    if ($ln -match '^[A-Za-z_]+:\s') { continue }
    if ($ln -eq '...' -or $ln -eq '---') { continue }
    $parts = $ln -split "`t"
    if ($parts.Count -lt 2) { continue }
    $ch   = $parts[0].Trim()
    $code = $parts[1].Trim()
    if ($ch.Length -eq 0) { continue }
    if ($code -notmatch '^[a-z]{1,2}$') { $bad++; continue }
    if ($ch.Length -gt 1) { $multi++; continue }

    if ($code.Length -eq 1) {
        $idx = [int][char]$code[0] - 97
    } else {
        $idx = $SINGLE + ([int][char]$code[0] - 97) * 26 + ([int][char]$code[1] - 97)
    }
    $key = "$idx|$ch"
    if ($seen.ContainsKey($key)) { continue }
    $seen[$key] = $true
    $buckets[$idx].Add($ch)     # 保留原表次序，不排序
    $n++
}

# ---- 輸出 ----
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([int]0x514B494D)
$bw.Write([int]2)
$bw.Write([int]$TOTAL)
$bw.Write([int]$n)
for ($i = 0; $i -lt $TOTAL; $i++) { $bw.Write([int]$buckets[$i].Count) }
for ($i = 0; $i -lt $TOTAL; $i++) {
    foreach ($c in $buckets[$i]) { $bw.Write([uint16][char]$c) }
}
$bw.Flush()
[System.IO.File]::WriteAllBytes($out, $ms.ToArray())
$bw.Close()

$empty = 0
for ($i = 0; $i -lt $TOTAL; $i++) { if ($buckets[$i].Count -eq 0) { $empty++ } }
$max = ($buckets | ForEach-Object { $_.Count } | Measure-Object -Maximum).Maximum

Write-Host "字數: $n   壞碼: $bad   多字詞(略過): $multi" -ForegroundColor Green
Write-Host "空 bucket: $empty   單 bucket 最多: $max" -ForegroundColor DarkGray
Write-Host "輸出: $out ($((Get-Item $out).Length) bytes)" -ForegroundColor Green
