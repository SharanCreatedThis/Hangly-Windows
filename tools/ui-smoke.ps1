# UI smoke test for the Customize window.
#
# Run inside the Windows guest, against a published build:
#     powershell -ExecutionPolicy Bypass -File tools\ui-smoke.ps1 -App C:\hangly\app
#
# XAML cannot be unit-tested from Hangly.Core.Tests -- it needs a desktop, a message pump
# and a real window -- so this drives the built application through UI Automation instead
# and checks the settings file afterwards. It exists because the first version of this
# window destroyed the user's settings simply by opening: building a slider raises
# ValueChanged, and every control here writes straight through to the store. A screenshot
# would not have caught it. This does.
param(
    [string]$App = 'C:\hangly\app',
    [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
Add-Type -Namespace UI -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
[DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="FindWindowW")] public static extern IntPtr FindWindowByClass(string c, IntPtr n);
[DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="FindWindowW")] public static extern IntPtr FindWindowTitled(string c, string n);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
'@
[UI.N]::SetProcessDPIAware() | Out-Null

$settingsPath = Join-Path $env:APPDATA 'Hangly\settings.json'
$failures = [System.Collections.Generic.List[string]]::new()
function Check($what, $condition) {
    if ($condition) { "  PASS  $what" } else { "  FAIL  $what"; $failures.Add($what) }
}
function Settings { Get-Content $settingsPath -Raw | ConvertFrom-Json }

# A known starting point, so every assertion below is about what the window did.
Stop-Process -Name Hangly -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
@'
{
  "schemaVersion": 1, "launchAtLogin": false, "hasSeenWelcome": true,
  "overlay": {
    "isEnabled": true, "opacity": 1, "charmSize": 1, "ropeLength": 1,
    "anchor": "TopCenter", "offsetX": 0, "offsetY": 0,
    "ropeStyle": "Thread", "displayIndex": 0,
    "soundEnabled": true, "soundVolume": 0.5,
    "charmIds": [ "nazar" ]
  }
}
'@ | Set-Content $settingsPath -Encoding UTF8

Start-Process (Join-Path $App 'Hangly.exe')
Start-Sleep -Seconds 10
Check 'the app starts' ([bool](Get-Process Hangly -ErrorAction SilentlyContinue))
Check 'the overlay exists' ([UI.N]::FindWindowByClass('HanglyOverlay', [IntPtr]::Zero) -ne [IntPtr]::Zero)

# --- open Customize from the tray -------------------------------------------------
$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$root = [System.Windows.Automation.AutomationElement]::RootElement
function Buttons { $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Button))) }
function ClickPoint($x, $y, $right) {
  [UI.N]::SetCursorPos([int]$x, [int]$y) | Out-Null; Start-Sleep -Milliseconds 350
  if ($right) { [UI.N]::mouse_event(0x0008,0,0,0,[IntPtr]::Zero); [UI.N]::mouse_event(0x0010,0,0,0,[IntPtr]::Zero) }
  else { [UI.N]::mouse_event(0x0002,0,0,0,[IntPtr]::Zero); [UI.N]::mouse_event(0x0004,0,0,0,[IntPtr]::Zero) } }
function TrayIcon {
  foreach ($e in Buttons) { $b = $e.Current.BoundingRectangle
    if ($e.Current.Name -match '^Hangly' -and $b.Y -gt ($vs.Height - 320)) { return $e } }
  return $null }

$icon = TrayIcon
if (-not $icon) {
  foreach ($e in Buttons) { if ($e.Current.Name -match 'Show Hidden') {
    $r = $e.Current.BoundingRectangle; ClickPoint ($r.X+$r.Width/2) ($r.Y+$r.Height/2) $false
    Start-Sleep -Milliseconds 900; break } }
  $icon = TrayIcon
}
Check 'the tray icon is present' ([bool]$icon)
if ($icon) {
  $b = $icon.Current.BoundingRectangle
  ClickPoint ($b.X+$b.Width/2) ($b.Y+$b.Height/2) $true
  Start-Sleep -Milliseconds 1000
  # Hide Charm, then Customize. Separators are skipped by the keyboard.
  [System.Windows.Forms.SendKeys]::SendWait('{DOWN}{DOWN}{ENTER}')
  Start-Sleep -Seconds 8
}

$window = [UI.N]::FindWindowTitled('WinUIDesktopWin32WindowClass', 'Hangly')
Check 'Customize opens' ($window -ne [IntPtr]::Zero)
if ($window -eq [IntPtr]::Zero) { $failures | ForEach-Object { }; "`n$($failures.Count) failure(s)"; exit 1 }
[UI.N]::SetForegroundWindow($window) | Out-Null
Start-Sleep -Seconds 2

# --- the regression this file exists for -----------------------------------------
$after = Settings
Check 'opening Customize leaves opacity alone'    ($after.overlay.opacity -eq 1)
Check 'opening Customize leaves charm size alone' ($after.overlay.charmSize -eq 1)
Check 'opening Customize leaves rope length alone'($after.overlay.ropeLength -eq 1)
Check 'opening Customize leaves the cord alone'   ($after.overlay.ropeStyle -eq 'Thread')
Check 'opening Customize leaves the charms alone' ($after.overlay.charmIds.Count -eq 1 -and $after.overlay.charmIds[0] -eq 'nazar')

# --- the window does what it is for ------------------------------------------------
function FindIn($type, $pattern) {
  $win = [System.Windows.Automation.AutomationElement]::FromHandle($window)
  $cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
  foreach ($e in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
    if ($e.Current.Name -match $pattern) { return $e } }
  return $null }
function ClickElement($e) {
  $r = $e.Current.BoundingRectangle
  ClickPoint ($r.X + $r.Width/2) ($r.Y + $r.Height/2) $false
  Start-Sleep -Milliseconds 900 }

$three = FindIn ([System.Windows.Automation.ControlType]::RadioButton) '^3$'
Check 'the charm count picker offers three' ([bool]$three)
if ($three) {
  ClickElement $three
  Check 'choosing three charms persists' ((Settings).overlay.charmIds.Count -eq 3)
}

$appearance = FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Appearance$'
Check 'the Appearance page is reachable' ([bool]$appearance)
if ($appearance) {
  ClickElement $appearance
  Check 'the cord description is shown' ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'Twisted gold'))
  Check 'the sliders read their stored values, not their minimums' `
    ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'Charm size . 100%'))
}

# --- About and the analytics inspector -------------------------------------------
$about = FindIn ([System.Windows.Automation.ControlType]::ListItem) '^About$'
Check 'the About page is reachable' ([bool]$about)
if ($about) {
    ClickElement $about
    Start-Sleep -Milliseconds 800

    Check 'About shows the version and build'  ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'Version \d+\.\d+\.\d+ \(build'))
    Check 'About shows the copyright'          ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'Copyright'))
    # A HyperlinkButton is a Hyperlink to UI Automation, not a Button.
    foreach ($link in 'Website', 'GitHub', 'Release notes', 'Instagram') {
        Check "About links to $link" ([bool](FindIn ([System.Windows.Automation.ControlType]::Hyperlink) "^$link$"))
    }
    Check 'About offers the coffee button' ([bool](FindIn ([System.Windows.Automation.ControlType]::Button) 'Buy the creator a coffee'))

    # The analytics section is collapsed until asked for.
    $section = FindIn ([System.Windows.Automation.ControlType]::Group) 'Anonymous analytics'
    if (-not $section) { $section = FindIn ([System.Windows.Automation.ControlType]::Button) 'Anonymous analytics' }
    Check 'the analytics section exists' ([bool]$section)
    if ($section) { ClickElement $section; Start-Sleep -Milliseconds 900 }

    $toggle = FindIn ([System.Windows.Automation.ControlType]::Button) 'Share anonymous analytics'
    Check 'the analytics toggle exists' ([bool]$toggle)

    # PRIVACY.md promises each of these is shown. They are checked by reading them.
    Check 'the inspector shows a masked identifier' `
        ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) '\u2022\u2022\u2022\u2022'))
    Check 'the inspector shows the destination' `
        ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'No destination configured|Connected'))
    # Any event name and a time, not app_launch specifically: the checks above this one
    # change settings, and changing a setting is itself an event.
    Check 'the inspector shows the last event' `
        ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) '^[a-z_]+ \u2014 \d\d:\d\d|nothing sent'))

    if ($toggle) {
        # A ToggleSwitch's clickable part is the switch, not the header its name comes
        # from, so the pattern is used rather than a click in the middle of its box.
        $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Start-Sleep -Seconds 2
        $off = Settings
        Check 'switching analytics off is recorded'        ($off.privacy.analyticsEnabled -eq $false)
        Check 'switching off discards the identifier'      ($null -eq $off.privacy.anonymousId)
        Check 'the inspector forgets the last event'       ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'nothing sent'))

        $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        Start-Sleep -Seconds 2
        $on = Settings
        Check 'switching analytics back on is recorded'    ($on.privacy.analyticsEnabled -eq $true)
        Check 'switching back on mints a new identifier'   ($null -ne $on.privacy.anonymousId)
    }
}

Check 'the launch was counted' ((Settings).milestones.launchCount -ge 1)
Check 'the overlay survived all of it' ([UI.N]::FindWindowByClass('HanglyOverlay', [IntPtr]::Zero) -ne [IntPtr]::Zero)
Check 'the app survived all of it' ([bool](Get-Process Hangly -ErrorAction SilentlyContinue))

if (-not $KeepOpen) { Stop-Process -Name Hangly -Force -ErrorAction SilentlyContinue }

""
if ($failures.Count -gt 0) { "$($failures.Count) failure(s)"; exit 1 }
'all checks passed'
