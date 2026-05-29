$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcRoot = Join-Path $root 'src'
$srcFiles = Get-ChildItem -LiteralPath $srcRoot -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName }
$dist = Join-Path $env:APPDATA 'BombClient\Build'
$assets = Join-Path $root 'assets'
$exe = Join-Path $dist 'Bomb Client.exe'
$zip = Join-Path $dist 'BombClient-Windows.zip'
$readme = Join-Path $root 'README.md'
$readmeOut = Join-Path $dist 'README.md'
$changelog = Join-Path $root 'CHANGELOG.md'
$changelogOut = Join-Path $dist 'CHANGELOG.md'
$dev = Join-Path $root 'DEVELOPMENT.md'
$devOut = Join-Path $dist 'DEVELOPMENT.md'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$icon = Join-Path $assets 'BombClient.ico'
$logoResource = (Join-Path $assets 'BombClientLogo.png') + ',BombClientLogo.png'

New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $compiler `
  /nologo `
  /target:winexe `
  /platform:anycpu `
  /optimize+ `
  "/out:$exe" `
  "/win32icon:$icon" `
  "/resource:$logoResource" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.IO.Compression.dll `
  /reference:System.IO.Compression.FileSystem.dll `
  $srcFiles

if ($LASTEXITCODE -ne 0) {
  throw "Compiler failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $readme -Destination $readmeOut -Force
if (Test-Path $changelog) {
  Copy-Item -LiteralPath $changelog -Destination $changelogOut -Force
}
if (Test-Path $dev) {
  Copy-Item -LiteralPath $dev -Destination $devOut -Force
}
if (Test-Path $zip) {
  Remove-Item -LiteralPath $zip -Force
}
$packageFiles = @($exe, $readmeOut)
if (Test-Path $changelogOut) { $packageFiles += $changelogOut }
if (Test-Path $devOut) { $packageFiles += $devOut }
Compress-Archive -LiteralPath $packageFiles -DestinationPath $zip

Write-Host "Built $exe"
Write-Host "Packaged $zip"
