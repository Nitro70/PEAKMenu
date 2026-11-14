# Puts the old menu back (the patched Fengy AIO + DearImGuiInjection) and takes PEAKMenu out.
# Run with PEAK closed.
$ErrorActionPreference = 'Stop'
if (Get-Process PEAK -ErrorAction SilentlyContinue) { throw 'Close PEAK first.' }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$bep = Split-Path -Parent $here
$plugins = Join-Path $bep 'plugins'
Copy-Item (Join-Path $here 'PEAK-AIO.dll') (Join-Path $plugins 'PEAK-AIO.dll') -Force
if (-not (Test-Path (Join-Path $plugins 'DearImGuiInjection'))) {
    Copy-Item (Join-Path $here 'DearImGuiInjection') (Join-Path $plugins 'DearImGuiInjection') -Recurse
}
$menu = Join-Path $plugins 'PEAKMenu\PEAKMenu.dll'
if (Test-Path $menu) { Move-Item $menu (Join-Path $here 'PEAKMenu.dll.removed') -Force }
Write-Host 'Old AIO menu is back; PEAKMenu taken out.'
