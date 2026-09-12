param(
    [string]$Source,
    [string]$Dest = 'D:\Code\AIPrograms\CampusClock',
    [string]$Ics = 'C:\Users\Lenovo\Desktop\*.ics',
    [string]$Desktop = 'C:\Users\Lenovo\Desktop',
    [switch]$NoShortcut
)

$ErrorActionPreference = 'Stop'
if (-not $Source) { $Source = $PSScriptRoot }

Write-Host "source : $Source"
Write-Host "target : $Dest"

foreach ($dir in @($Dest, (Join-Path $Dest 'src'), (Join-Path $Dest 'assets'), (Join-Path $Dest 'data'))) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
}

Copy-Item (Join-Path $Source 'CampusClock.exe') $Dest -Force
Copy-Item (Join-Path $Source 'src\*.cs') (Join-Path $Dest 'src') -Force
Copy-Item (Join-Path $Source 'assets\campusclock.ico') (Join-Path $Dest 'assets') -Force
Copy-Item (Join-Path $Source 'build.ps1') $Dest -Force
Copy-Item (Join-Path $Source 'README.md') $Dest -Force
Copy-Item $PSCommandPath $Dest -Force

# drop source files that no longer exist in the source tree
$srcDest = Join-Path $Dest 'src'
Get-ChildItem $srcDest -Filter *.cs -ErrorAction SilentlyContinue | ForEach-Object {
    $sameName = Join-Path $Source ('src\' + $_.Name)
    if (-not (Test-Path $sameName)) {
        Remove-Item $_.FullName -Force
        Write-Host "removed stale source: $($_.Name)"
    }
}

$icsTarget = Join-Path $Dest 'data\timetable.ics'
if (-not (Test-Path $icsTarget)) {
    $found = Get-ChildItem -Path $Ics -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($found) {
        Copy-Item $found.FullName $icsTarget -Force
        Write-Host "timetable imported: $($found.FullName)"
    } else {
        Write-Host "no .ics found at $Ics - import it later from the app"
    }
}

if (-not $NoShortcut) {
    if (-not (Test-Path $Desktop)) { $Desktop = [Environment]::GetFolderPath('Desktop') }
    $lnk = Join-Path $Desktop 'CampusClock.lnk'
    $exe = Join-Path $Dest 'CampusClock.exe'
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($lnk)
    $sc.TargetPath = $exe
    $sc.WorkingDirectory = $Dest
    $sc.Description = 'CampusClock - timetable and homework'
    $sc.IconLocation = "$exe,0"
    $sc.Save()
    Write-Host "shortcut created: $lnk"
}

Write-Host "done."
