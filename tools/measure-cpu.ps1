# Measures the installed Hangly's CPU and memory, the way M3 measured it in the ARM64 VM,
# so the numbers can be compared on real hardware.
#
#   powershell -ExecutionPolicy Bypass -File tools\measure-cpu.ps1
#
# It restarts Hangly (which swings the charm, as every launch does), then reports:
#   swing    CPU over the first 45 s, while the rope is moving
#   settled  CPU from 90 to 150 s, once it has come to rest
#   memory   private bytes and working set at the end
# Your settings are not touched. Percentages are of one core.

$exe = Join-Path $env:LOCALAPPDATA 'Hangly\current\Hangly.exe'
if (-not (Test-Path $exe)) { Write-Error "No installed Hangly at $exe"; exit 1 }

Get-Process Hangly -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
$gpu = (Get-CimInstance Win32_VideoController | Select-Object -First 1).Name
$p = Start-Process $exe -PassThru
Start-Sleep 1
$p.Refresh(); $c0 = $p.TotalProcessorTime.TotalSeconds; Start-Sleep 45
$p.Refresh(); $c1 = $p.TotalProcessorTime.TotalSeconds; Start-Sleep 45
$p.Refresh(); $c2 = $p.TotalProcessorTime.TotalSeconds; Start-Sleep 60
$p.Refresh(); $c3 = $p.TotalProcessorTime.TotalSeconds

$log = Join-Path $env:APPDATA 'Hangly\hangly.log'
$path = if (Select-String -Path $log -Pattern 'presenting through DirectComposition' -Quiet) { 'DirectComposition' } else { 'UpdateLayeredWindow' }
"Hangly {0}, {1}, {2}" -f $p.MainModule.FileVersionInfo.ProductVersion, $env:PROCESSOR_ARCHITECTURE, $gpu
"present path: $path"
"swing    {0,5:N2} s over 45 s = {1,5:N1}% of one core" -f ($c1 - $c0), (($c1 - $c0) / 45 * 100)
"settled  {0,5:N2} s over 60 s = {1,5:N2}% of one core" -f ($c3 - $c2), (($c3 - $c2) / 60 * 100)
"memory   private {0:N1} MB, working set {1:N1} MB" -f ($p.PrivateMemorySize64 / 1MB), ($p.WorkingSet64 / 1MB)
"Hangly is still running; nothing else was changed."
