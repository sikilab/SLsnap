$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw "csc.exe not found"
}

$dist = Join-Path $PSScriptRoot "dist\winscreen"
$outFile = Join-Path $dist "SLSnap.exe"
$iconFile = Join-Path $PSScriptRoot "resources\app.ico"
if (Test-Path $dist) {
    Remove-Item -Recurse -Force $dist
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$sources = Get-ChildItem -Path (Join-Path $PSScriptRoot "src") -Filter *.cs | ForEach-Object { $_.FullName }

& $csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    ("/out:" + $outFile) `
    ("/win32icon:" + $iconFile) `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Web.Extensions.dll `
    $sources

Write-Host ""
Write-Host "Build complete:"
Write-Host "  $dist\\SLSnap.exe"
