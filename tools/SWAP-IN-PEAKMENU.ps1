# Waits for PEAK to close, then swaps PEAKMenu in and the old AIO menu out:
#   plugins\PEAKMenu\PEAKMenu.dll.NEW  -> plugins\PEAKMenu\PEAKMenu.dll
#   plugins\PEAK-AIO.dll                -> backup folder
#   plugins\DearImGuiInjection\         -> backup folder (only the AIO menu used it)
# Runs detached; safe to start while the game is open. Logs to swap.log next to this script.
param(
    [int]$WaitPid = 0,
    [string]$ProcessName = 'PEAK',
    [string]$Plugins = '',
    [string]$Backup = ''
)
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Plugins) { $Plugins = Split-Path -Parent $dir }
if (-not $Backup) { $Backup = Join-Path (Split-Path -Parent $Plugins) '_update_backup_2026-11-14b' }
$log = Join-Path $dir 'swap.log'
function Log($m) { "$(Get-Date -Format o)  $m" | Out-File -FilePath $log -Append -Encoding utf8 }

try {
    Log "waiter started (pid $PID); waiting for $ProcessName to exit"
    if ($WaitPid -gt 0) {
        try { Wait-Process -Id $WaitPid -ErrorAction Stop } catch { }
    }
    while (Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 2 }
    Start-Sleep -Seconds 2
    Log "$ProcessName closed"

    $new = Join-Path $dir 'PEAKMenu.dll.NEW'
    $dll = Join-Path $dir 'PEAKMenu.dll'
    if (-not (Test-Path $new)) { Log "no PEAKMenu.dll.NEW staged; nothing to do"; return }

    New-Item -ItemType Directory -Force -Path $Backup | Out-Null

    $aio = Join-Path $Plugins 'PEAK-AIO.dll'
    if (Test-Path $aio) {
        Move-Item $aio (Join-Path $Backup 'PEAK-AIO.dll') -Force
        Log "moved PEAK-AIO.dll to $Backup"
    }

    $imgui = Join-Path $Plugins 'DearImGuiInjection'
    if (Test-Path $imgui) {
        $dest = Join-Path $Backup 'DearImGuiInjection'
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Move-Item $imgui $dest -Force
        Log "moved DearImGuiInjection to $Backup"
    }

    if (Test-Path $dll) {
        $bak = Join-Path $Backup ("PEAKMenu.dll.bak-" + (Get-Date -Format yyyyMMdd-HHmmss))
        Copy-Item $dll $bak -Force
        Log "backed up previous PEAKMenu.dll to $bak"
    }

    Move-Item $new $dll -Force
    Log ("swapped in PEAKMenu.dll, sha256 " + (Get-FileHash $dll -Algorithm SHA256).Hash)
} catch {
    Log ("ERROR: " + $_.Exception.Message)
}
