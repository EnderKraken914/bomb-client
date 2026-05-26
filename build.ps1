$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src\BombClient.cs'
$dist = Join-Path $env:APPDATA 'BombClient\Build'
$assets = Join-Path $root 'assets'
$exe = Join-Path $dist 'Bomb Client.exe'
$zip = Join-Path $dist 'BombClient-Windows.zip'
$readme = Join-Path $root 'README.md'
$readmeOut = Join-Path $dist 'README.md'
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
  $src

if ($LASTEXITCODE -ne 0) {
  throw "Compiler failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $readme -Destination $readmeOut -Force
if (Test-Path $zip) {
  Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -LiteralPath $exe, $readmeOut -DestinationPath $zip

Write-Host "Built $exe"
Write-Host "Packaged $zip"
