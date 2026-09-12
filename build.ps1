param(
    [string]$Out,
    [switch]$Console
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'

$refs = @(
    "$fw\WPF\PresentationFramework.dll",
    "$fw\WPF\PresentationCore.dll",
    "$fw\WPF\WindowsBase.dll",
    "$fw\System.Xaml.dll",
    "$fw\System.Web.Extensions.dll",
    "$fw\System.Windows.Forms.dll",
    "$fw\System.Drawing.dll"
)

$srcs = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
if (-not $Out) { $Out = Join-Path $root 'CampusClock.exe' }

$target = if ($Console) { 'exe' } else { 'winexe' }

$cscArgs = @('/nologo', '/codepage:65001', '/utf8output', "/target:$target", '/platform:anycpu', '/optimize+', "/out:$Out")
foreach ($r in $refs) { $cscArgs += "/r:$r" }
$icon = Join-Path $root 'assets\campusclock.ico'
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }
$cscArgs += $srcs

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED ($LASTEXITCODE)"; exit $LASTEXITCODE }
Write-Host "BUILD OK -> $Out"
