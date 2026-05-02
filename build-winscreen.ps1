$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$project = Join-Path $PSScriptRoot "SLSnap.csproj"

if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe not found"
}

if (-not (Test-Path $project)) {
    throw "SLSnap.csproj not found"
}

$dist = Join-Path $PSScriptRoot "dist\winscreen"
$outFile = Join-Path $dist "SLSnap.exe"
if (Test-Path $dist) {
    Remove-Item -Recurse -Force $dist
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $msbuild `
    $project `
    /nologo `
    /t:Rebuild `
    /p:Configuration=Release `
    /p:Platform=AnyCPU

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Get-ChildItem -Path $dist -Force | Where-Object { $_.Name -ne "SLSnap.exe" } | Remove-Item -Recurse -Force

Write-Host ""
Write-Host "Build complete:"
Write-Host "  $dist\\SLSnap.exe"
