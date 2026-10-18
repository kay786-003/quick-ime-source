# ============================================================================
#  build.ps1 - 一鍵建置速成輸入法
#  需要：Windows + .NET Framework 4.x（Windows 10/11 內建）
#  用法：powershell -ExecutionPolicy Bypass -File .\build.ps1
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

$args = @(
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
) + $sources

# 聯想字出廠詞表（存在才嵌入，唔存在就用通用常用字補底）
if (Test-Path $assocSeed) {
    $args += "/resource:$assocSeed,assoc-seed.txt"
} else {
    Write-Host "      找不到 src\assoc-seed.txt，聯想字會用通用常用字" -ForegroundColor Yellow
}

& $csc @args

if ($LASTEXITCODE -ne 0) { throw "編譯失敗" }

Copy-Item $outExe -Destination (Join-Path $dist 'QuickIME.exe') -Force
Copy-Item $dictPath -Destination (Join-Path $dist 'qdict.bin') -Force
Write-Host "      產出: dist\QuickIME.exe ($((Get-Item $outExe).Length) bytes)" -ForegroundColor Green

# ---- 2b. 同步到 releases/（呢個資料夾係提交上 GitHub 嘅，方便直接下載）----
if (-not $SkipRelease) {
    $relDir = Join-Path $root 'releases'
    New-Item -ItemType Directory -Force -Path $relDir | Out-Null
    Copy-Item $outExe -Destination (Join-Path $relDir 'QuickIME.exe') -Force

    # 打包成 zip（exe + 說明書 + 示意圖）
    $zipName = '速成輸入法_v2.1.zip'
    $zipPath = Join-Path $relDir $zipName
    Remove-Item $zipPath -ErrorAction SilentlyContinue
    $tmp = Join-Path $env:TEMP ('qime-pack-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    $inner = Join-Path $tmp '速成輸入法'
    New-Item -ItemType Directory -Force -Path $inner | Out-Null
    Copy-Item $outExe -Destination $inner -Force
    foreach ($extra in @('使用說明.txt', '候選字視窗示意圖.png')) {
        $src2 = Join-Path $relDir $extra
        if (Test-Path $src2) { Copy-Item $src2 -Destination $inner -Force }
    }
    Compress-Archive -Path $inner -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "      已同步: releases\QuickIME.exe + $zipName" -ForegroundColor Green
} else {
    Write-Host "      略過 releases/ 同步（-SkipRelease）" -ForegroundColor DarkGray
}

# ---- 3. 自我測試 ----
if ($RunTests) {
    Write-Host "[3/3] 執行自我測試..." -ForegroundColor Cyan
    Push-Location $bin
    Copy-Item $dictPath -Destination $bin -Force
    & (Join-Path $bin 'QuickIME.exe') --selftest
    $code = $LASTEXITCODE
    Pop-Location
    if ($code -ne 0) { throw "自我測試失敗" }
    Write-Host "      測試通過" -ForegroundColor Green
} else {
    Write-Host "[3/3] 略過測試（加 -RunTests 可執行）" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "建置完成。" -ForegroundColor Green
Write-Host "  執行檔: dist\QuickIME.exe"
Write-Host "  字典檔: dist\qdict.bin"
Write-Host "  自我測試: dist\QuickIME.exe --selftest"
