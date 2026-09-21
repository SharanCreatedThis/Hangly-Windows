# The two public-beta blockers, checked on a running copy.
#
# Both were found by measurement rather than by reading, and both are the kind of thing
# that comes back: a single-instance guard is one line away from being bypassed by a new
# entry point, and a tray icon that does not re-register fails silently. So they have a
# script rather than a paragraph in a document.
#
#   ./tools/hardening-checks.ps1 -Exe C:\hangly\app\Hangly.exe
#
# Exits non-zero if any check fails. Restarts Explorer as part of check 2, so it is a
# deliberate thing to run, not something to wire into CI.

param(
    [string] $Exe = 'C:\hangly\app\Hangly.exe',
    [string] $ShotDir = "$env:TEMP\hangly-hardening"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type -Namespace HC -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
[HC.N]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force -Path $ShotDir | Out-Null

$script:failures = 0
function Check($name, $condition, $detail) {
    if ($condition) { "  PASS  $name — $detail" }
    else { $script:failures++; "  FAIL  $name — $detail" }
}
function Hangly { @(Get-Process Hangly -ErrorAction SilentlyContinue) }
function Log { Get-Content "$env:APPDATA\Hangly\hangly.log" -ErrorAction SilentlyContinue }

# The notification area, photographed. The tray is drawn by Explorer and reports nothing
# useful through UI Automation on Windows 11 — the icon lives behind an overflow chevron
# that is itself only present when something is hidden — so the evidence is a picture.
function TrayShot($name) {
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $w = 560; $h = 90
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(($vs.X + $vs.Width - $w), ($vs.Y + $vs.Height - $h), 0, 0, $bmp.Size)
    $path = Join-Path $ShotDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $path
}

"== 1. Single instance =="
Get-Process Hangly -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Start-Process $Exe -WorkingDirectory (Split-Path $Exe)
Start-Sleep -Seconds 20

$first = Hangly
Check 'a copy starts' ($first.Count -eq 1) "$($first.Count) running, pid $($first[0].Id)"
$settingsPath = "$env:APPDATA\Hangly\settings.json"
$before = (Get-FileHash $settingsPath -Algorithm SHA256).Hash
$logBefore = (Log).Count

$second = Start-Process $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
$second.WaitForExit(30000) | Out-Null
Start-Sleep -Seconds 3

$after = Hangly
Check 'the second copy exits'      ($second.HasExited) "exit code $($second.ExitCode)"
Check 'one process remains'        ($after.Count -eq 1) "$($after.Count) running"
Check 'it is the original process' ($after.Count -eq 1 -and $after[0].Id -eq $first[0].Id) "pid $($first[0].Id) -> $(($after | ForEach-Object Id) -join ',')"
Check 'settings are untouched'     ((Get-FileHash $settingsPath -Algorithm SHA256).Hash -eq $before) 'sha256 unchanged'
Check 'the log was not truncated'  ((Log).Count -ge $logBefore) "$logBefore lines before, $((Log).Count) after"
Check 'the refusal is recorded'    ([bool]((Log) -match 'already running')) 'log names the second copy'
"  shot: $(TrayShot 'tray-single-instance')"

"== 2. Explorer restart recovery =="
$pidBefore = (Hangly)[0].Id
"  before: $(TrayShot 'tray-before-explorer-restart')"
Stop-Process -Name explorer -Force
Start-Sleep -Seconds 25
Check 'explorer came back'   ([bool](Get-Process explorer -ErrorAction SilentlyContinue)) 'explorer.exe running'
$still = Hangly
Check 'hangly survived'      ($still.Count -eq 1 -and $still[0].Id -eq $pidBefore) "pid $pidBefore still running"
Check 'the icon was re-added' ([bool]((Log) -match 'tray icon re-added')) 'Shell_NotifyIcon(NIM_ADD) succeeded after TaskbarCreated'
Check 'nothing failed'       (-not [bool]((Log) -match '^\S+ FAIL')) 'no FAIL lines in the log'
"  after: $(TrayShot 'tray-after-explorer-restart')"

""
if ($script:failures -eq 0) { "ALL CHECKS PASSED"; exit 0 }
"$($script:failures) CHECK(S) FAILED"
exit 1
