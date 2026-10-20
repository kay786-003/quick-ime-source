# ============================================================================
#  build.ps1 - 一鍵建置速成輸入法
#  需要：Windows + .NET Framework 4.x（Windows 10/11 內建）
#  用法：powershell -ExecutionPolicy Bypass -File .\build.ps1
#
#  ⚠️ 本檔必須存成 **UTF-8 with BOM**（CI 會檢查）。
#     沒有 BOM 的話，Windows PowerShell 5.1 會用 ANSI 解析中文而整份腳本壞掉。
# ============================================================================
param(
    [switch]$SkipDict,     # 跳過字典重建（用現成的 qdict.bin）
    [switch]$SkipRelease,  # 不同步到 releases/
    [switch]$RunTests      # 建置完順便跑自我測試
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$bin  = Join-Path $root 'build'

# ---- 版本號：唯一來源 = repo 根的 VERSION 檔 ----
$versionFile = Join-Path $root 'VERSION'
if (-not (Test-Path $versionFile)) { throw "缺少 VERSION 檔（版本號的唯一來源）" }
$version = ([System.IO.File]::ReadAllText($versionFile)).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw "VERSION 格式不正確：'$version'（應為 2.4 或 2.4.1）" }

$parts = @($version.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }
$asmVersion = ($parts[0..3] -join '.')
Write-Host "版本: $version（組件 $asmVersion）" -ForegroundColor DarkGray

# ---- 產生 src\VersionInfo.cs（版本號唯一來源是 VERSION，不要手改這個檔）----
$versionCs = Join-Path $src 'VersionInfo.cs'
$versionSrc = @"
// ============================================================================
//  VersionInfo.cs - **自動產生，請勿手動編輯**
//
//  由 build.ps1 從 repo 根的 VERSION 檔產生。
//  改版本號：改 VERSION，然後重新跑 build.ps1。
// ============================================================================
using System.Reflection;

[assembly: AssemblyVersion("$asmVersion")]
[assembly: AssemblyFileVersion("$asmVersion")]
[assembly: AssemblyProduct("速成輸入法 (Quick IME)")]
[assembly: AssemblyTitle("QuickIME")]
[assembly: AssemblyDescription("免安裝速成輸入法 — 不依賴 Windows 輸入法框架")]

namespace QuickIME
{
    internal static class VersionInfo
    {
        /// <summary>程式版本（例如 "2.4"）。</summary>
        public const string Version = "$version";

        /// <summary>給組件用的四段式版本（例如 "2.4.0.0"）。</summary>
        public const string AssemblyVersion = "$asmVersion";
    }
}
"@
# 只有內容有變才寫入，避免無謂改動時間戳（也讓增量建置更可預期）
$existing = if (Test-Path $versionCs) { [System.IO.File]::ReadAllText($versionCs) } else { '' }
if ($existing -ne $versionSrc) {
    [System.IO.File]::WriteAllText($versionCs, $versionSrc, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "      已更新 src\VersionInfo.cs" -ForegroundColor DarkGray
}

# ---- 找 csc.exe（.NET Framework 4.x）----
function Find-Csc {
    $candidates = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw "找不到 csc.exe（需要 .NET Framework 4.x）"
}

$csc = Find-Csc
Write-Host "使用編譯器: $csc" -ForegroundColor DarkGray

New-Item -ItemType Directory -Force -Path $bin, $dist | Out-Null

# ---- 1. 產生字典 qdict.bin ----
$dictPath = Join-Path $src 'qdict.bin'
if (-not $SkipDict) {
    $gen = Join-Path $root 'tools\gen-dict.ps1'
    if (Test-Path $gen) {
        Write-Host "[1/3] 由 ms_quick.dict.yaml 產生字典..." -ForegroundColor Cyan
        & powershell -NoProfile -ExecutionPolicy Bypass -File $gen
        if ($LASTEXITCODE -ne 0) { throw "字典產生失敗" }
    } else {
        Write-Host "[1/3] 找不到 tools\gen-dict.ps1，改用現成 qdict.bin" -ForegroundColor Yellow
    }
} else {
    Write-Host "[1/3] 跳過字典重建" -ForegroundColor DarkGray
}

if (-not (Test-Path $dictPath)) { throw "缺少 $dictPath（字典檔）" }

# ---- 2. 編譯 QuickIME.exe（字典內嵌）----
Write-Host "[2/3] 編譯 QuickIME.exe..." -ForegroundColor Cyan
$sources = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }
$outExe  = Join-Path $bin 'QuickIME.exe'

$assocSeed = Join-Path $src 'assoc-seed.txt'

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    "/out:$outExe"
    '/optimize+'
    '/platform:anycpu'
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    "/resource:$dictPath,qdict.bin"
)

# Win32 manifest：DPI 感知 + asInvoker（不要求管理員權限）
$manifest = Join-Path $src 'app.manifest'
if (Test-Path $manifest) {
    $cscArgs += "/win32manifest:$manifest"
} else {
    Write-Host "      找不到 src\app.manifest，候選窗在高 DPI 下可能錯位" -ForegroundColor Yellow
}

# 圖示（存在才用；沒有就用預設）
$appIcon = Join-Path $src 'app.ico'
if (Test-Path $appIcon) {
    $cscArgs += "/win32icon:$appIcon"
}

$cscArgs += $sources

# 聯想字出廠詞表（存在才嵌入，唔存在就用通用常用字補底）
if (Test-Path $assocSeed) {
    $cscArgs += "/resource:$assocSeed,assoc-seed.txt"
} else {
    Write-Host "      找不到 src\assoc-seed.txt，聯想字會用通用常用字" -ForegroundColor Yellow
}

& $csc @cscArgs

if ($LASTEXITCODE -ne 0) { throw "編譯失敗" }

Copy-Item $outExe -Destination (Join-Path $dist 'QuickIME.exe') -Force
Copy-Item $dictPath -Destination (Join-Path $dist 'qdict.bin') -Force
Write-Host "      產出: dist\QuickIME.exe ($((Get-Item $outExe).Length) bytes)" -ForegroundColor Green

# ---- 2b. 同步到 releases/（呢個資料夾係提交上 GitHub 嘅，方便直接下載）----
if (-not $SkipRelease) {
    $relDir = Join-Path $root 'releases'
    New-Item -ItemType Directory -Force -Path $relDir | Out-Null
    Copy-Item $outExe -Destination (Join-Path $relDir 'QuickIME.exe') -Force

    # 打包成 zip（exe + 字典 + 說明書 + 示意圖）。
    # 檔名用 $version，不再寫死 v2.3。
    $zipName = "速成輸入法_v$version.zip"
    $zipPath = Join-Path $relDir $zipName
    Remove-Item $zipPath -ErrorAction SilentlyContinue
    $tmp = Join-Path $env:TEMP ('qime-pack-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    $inner = Join-Path $tmp '速成輸入法'
    New-Item -ItemType Directory -Force -Path $inner | Out-Null
    Copy-Item $outExe -Destination $inner -Force
    Copy-Item $dictPath -Destination $inner -Force

    # 文件只有一份（docs/），build 時複製到 releases/ 與 zip 內，
    # 避免 docs\使用說明.txt 與 releases\使用說明.txt 兩份不同步。
    foreach ($extra in @('使用說明.txt', '候選字視窗示意圖.png')) {
        $src2 = Join-Path $root (Join-Path 'docs' $extra)
        if (Test-Path $src2) {
            Copy-Item $src2 -Destination $inner -Force
            Copy-Item $src2 -Destination (Join-Path $relDir $extra) -Force
        }
    }
    Compress-Archive -Path $inner -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "      已同步: releases\QuickIME.exe + $zipName" -ForegroundColor Green
} else {
    Write-Host "      略過 releases/ 同步（-SkipRelease）" -ForegroundColor DarkGray
}

# ---- 3. 測試 ----
if ($RunTests) {
    Write-Host "[3/3] 執行測試..." -ForegroundColor Cyan
    Push-Location $bin
    Copy-Item $dictPath -Destination $bin -Force

    & (Join-Path $bin 'QuickIME.exe') --selftest
    $code = $LASTEXITCODE
    if ($code -ne 0) { Pop-Location; throw "自我測試失敗" }

    & (Join-Path $bin 'QuickIME.exe') --hooktest
    $code = $LASTEXITCODE
    if ($code -ne 0) { Pop-Location; throw "按鍵流程測試失敗" }

    & (Join-Path $bin 'QuickIME.exe') --gametest
    $code = $LASTEXITCODE
    if ($code -ne 0) { Pop-Location; throw "打機情境測試失敗" }

    Pop-Location
    Write-Host "      測試全部通過" -ForegroundColor Green
} else {
    Write-Host "[3/3] 略過測試（加 -RunTests 可執行）" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "建置完成（v$version）。" -ForegroundColor Green
Write-Host "  執行檔: dist\QuickIME.exe"
Write-Host "  字典檔: dist\qdict.bin"
Write-Host "  自我測試: dist\QuickIME.exe --selftest"
Write-Host "  按鍵流程: dist\QuickIME.exe --hooktest"
