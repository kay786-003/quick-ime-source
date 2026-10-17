# ============================================================================
#  package-repo.ps1 - 把專案打包成可直接上傳 GitHub 的 zip
# ============================================================================
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path          # tools\
$root = Split-Path -Parent $root                                 # repo root
$out  = Join-Path (Split-Path -Parent $root) 'dist'              # 與 repo 同層的 dist\
New-Item -ItemType Directory -Force -Path $out | Out-Null

$zip = Join-Path $out 'quick-ime-source.zip'
Remove-Item $zip -ErrorAction SilentlyContinue

# 只打包要上 GitHub 的東西（排除建置產物）
$stage = Join-Path $env:TEMP ('qime-stage-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$include = @(
    '.github', '.gitignore', 'LICENSE', 'NOTICE', 'README.md',
    'build.ps1', 'src', 'tools', 'data', 'docs', 'releases'
)
foreach ($item in $include) {
    $p = Join-Path $root $item
    if (Test-Path $p) { Copy-Item $p -Destination $stage -Recurse -Force }
}

# 移除不該上傳的產物（但保留 releases/ 裡的現成 exe）
Remove-Item (Join-Path $stage 'src\qdict.bin') -ErrorAction SilentlyContinue
Get-ChildItem $stage -Recurse -Directory -Filter 'build'  | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $stage -Recurse -Directory -Filter 'dist'   | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $stage -Recurse -Filter '*.log' | Remove-Item -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $stage 'releases\QuickIME.log') -ErrorAction SilentlyContinue

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal -Force
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "打包完成: $zip" -ForegroundColor Green
Write-Host "大小: $([math]::Round((Get-Item $zip).Length/1KB,1)) KB"
