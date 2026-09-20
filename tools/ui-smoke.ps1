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
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
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
  "schemaVersion": 1, "launchAtLogin": false, "hasSeenWelcome": true, "displayName": "Sharan", "isFollowPromptSilenced": true,
  "overlay": {
    "isEnabled": true, "opacity": 1, "charmSize": 1, "ropeLength": 1,
    "anchor": "TopCenter", "offsetX": 0, "offsetY": 0,
    "ropeStyle": "Thread", "displayIndex": 0,
    "charmIds": [ "nazar" ]
  }
}
'@ | Set-Content $settingsPath -Encoding UTF8

# An imported charm, seeded straight into the store. The file dialog is a modal Win32
# window that blocks the app's UI thread, which makes it awkward to drive reliably from
# here; what this test is for is what the Library does with an import once it exists.
$charms = Join-Path $env:APPDATA 'Hangly\Charms'
Remove-Item $charms -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $charms | Out-Null
$importId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
@'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="40" fill="#e67e22"/></svg>
'@ | Set-Content (Join-Path $charms "$importId.svg") -Encoding UTF8
@"
[{"id":"$importId","name":"Smoke Charm","createdAt":"2026-01-01T00:00:00+00:00",
  "imageFileName":"$importId.svg",
  "metrics":{"mass":3,"radiusRatio":0.12,"knotInset":0.9},
  "palette":{"primary":{"red":0.9,"green":0.49,"blue":0.13,"alpha":1},
             "secondary":{"red":0.65,"green":0.35,"blue":0.09,"alpha":1},
             "deep":{"red":0.4,"green":0.22,"blue":0.06,"alpha":1},
             "light":{"red":0.95,"green":0.75,"blue":0.6,"alpha":1}}}]
"@ | Set-Content (Join-Path $charms 'manifest.json') -Encoding UTF8

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
# By AutomationId, for the elements whose text is the thing under test and so cannot
# also be the way to find them.
function ById($id) {
  $win = [System.Windows.Automation.AutomationElement]::FromHandle($window)
  $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
      [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id))) }

# Invoked through UI Automation rather than clicked at coordinates. The filter chips
# scroll horizontally, and an element scrolled out of view reports a bounding rectangle of
# NaN — which is not somewhere a mouse can be moved to.
function ClickElement($e) {
  $done = $false
  foreach ($pattern in @(
      [System.Windows.Automation.InvokePattern]::Pattern,
      [System.Windows.Automation.TogglePattern]::Pattern,
      [System.Windows.Automation.SelectionItemPattern]::Pattern)) {
    if ($done) { break }
    try {
      $p = $e.GetCurrentPattern($pattern)
      if ($p -is [System.Windows.Automation.InvokePattern]) { $p.Invoke() }
      elseif ($p -is [System.Windows.Automation.TogglePattern]) { $p.Toggle() }
      else { $p.Select() }
      $done = $true
    } catch { }
  }
  if (-not $done) {
    $r = $e.Current.BoundingRectangle
    if (-not [double]::IsNaN($r.X)) { ClickPoint ($r.X + $r.Width/2) ($r.Y + $r.Height/2) $false }
  }
  Start-Sleep -Milliseconds 1000 }

# --- the Library --------------------------------------------------------------------
function CharmTiles {
    # Three kinds of ListItem live in this window: the navigation items, the places on
    # the cord, and the charms in the grid. Only the last are being counted, so the other
    # two are excluded by name and by identity — the strip's tiles carry charm names too,
    # so they cannot be told apart by name alone.
    $win = [System.Windows.Automation.AutomationElement]::FromHandle($window)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)

    $strip = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            'Charms on the cord, drag to reorder')))
    $onTheCord = @()
    if ($strip) {
        $onTheCord = @($strip.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { , $_.GetRuntimeId() })
    }

    @($win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond) |
        Where-Object { $_.Current.Name -notin 'Library', 'Create', 'Appearance', 'About' } |
        Where-Object {
            $id = $_.GetRuntimeId()
            -not ($onTheCord | Where-Object { @(Compare-Object $_ $id -SyncWindow 0).Count -eq 0 })
        }).Count
}

Check 'the Library page is the one that opens' ([bool](FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Library$'))
Check 'every charm is shown, the import included' ((CharmTiles) -eq 71)
# The Create tab exists and offers the whole flow.
$createNav = FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Create$'
Check 'the Create tab is offered' ([bool]$createNav)
if ($createNav) {
    ClickElement $createNav
    Check 'Create offers a picture chooser' ([bool](ById 'CreateChooseButton'))
    Check 'Create says which formats it takes' `
        ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'PNG, JPG or SVG'))
    Check 'Create hides its buttons until a picture is chosen' `
        ($null -eq (ById 'CreateAndHangButton'))

    $libraryNav = FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Library$'
    if ($libraryNav) { ClickElement $libraryNav }
}

# Reordering and per-place size: the strip, its move controls and its slider.
Check 'the cord strip is offered'        ([bool](FindIn ([System.Windows.Automation.ControlType]::List) 'Charms on the cord'))
Check 'the size slider is offered'       ([bool](ById 'SlotSizeSlider'))
Check 'moving up is refused at the top'  (-not (ById 'MoveUpButton').Current.IsEnabled)

# The detail panel opens describing whatever is already on the cord.
# Matched on the stem, not the whole name: 'Nazar boncuğu' carries a Turkish g-breve
# and this file is read back by a shell that does not reliably keep it.
Check 'the detail panel names the charm'    ((ById 'DetailName').Current.Name -match '^Nazar')
Check 'the detail panel gives its region'   ((ById 'DetailRegion').Current.Name -match 'Turkey')
Check 'the detail panel describes it'       ((ById 'DetailDescription').Current.Name.Length -gt 40)
Check 'the detail panel says it is hanging' ((ById 'DetailRopeState').Current.Name -eq 'On the rope')
Check 'the detail panel offers favouriting' ([bool](ById 'DetailFavourite'))

# Collections are cards above the grid, and each names its own count.
Check 'the Marvel collection is offered'    ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) '^Marvel$'))
Check 'a collection says how many it holds' ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) '^6 charms$'))
Check 'a collection describes itself'       ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'Mysteries from the Upside Down'))

foreach ($chip in 'All', 'Favourites', 'Recent', 'Protection') {
    Check "the $chip filter is offered" ([bool](FindIn ([System.Windows.Automation.ControlType]::Button) "^Show $chip$"))
}

$search = FindIn ([System.Windows.Automation.ControlType]::Edit) '.*'
Check 'the search box exists' ([bool]$search)
if ($search) {
    $settingsBeforeSearch = Get-Content $settingsPath -Raw
    $search.SetFocus()
    Start-Sleep -Milliseconds 400
    [System.Windows.Forms.SendKeys]::SendWait('glass')
    Start-Sleep -Seconds 2
    $narrowed = CharmTiles
    Check 'searching narrows the grid'        ($narrowed -gt 0 -and $narrowed -lt 82)
    Check 'searching writes nothing to disk'  ((Get-Content $settingsPath -Raw) -eq $settingsBeforeSearch)

    [System.Windows.Forms.SendKeys]::SendWait('^a{BACKSPACE}')
    Start-Sleep -Seconds 2
    Check 'clearing the search restores the grid' ((CharmTiles) -eq 71)
}

$favouriteChip = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show Favourites$'
if ($favouriteChip) {
    ClickElement $favouriteChip
    Check 'an unstarred Library shows an empty state' `
        ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'No favourites yet'))

    $allChip = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show All$'
    if ($allChip) { ClickElement $allChip }

    $star = FindIn ([System.Windows.Automation.ControlType]::Button) 'Add Hamsa to favourites'
    Check 'a charm can be starred' ([bool]$star)
    if ($star) {
        ClickElement $star
        Check 'starring persists'  ((Settings).library.favouriteCharmIds -contains 'hamsa')
        ClickElement $favouriteChip
        Check 'favourites shows what was starred' ((CharmTiles) -eq 1)
        if ($allChip) { ClickElement $allChip }
    }
}

# --- imported charms -----------------------------------------------------------------
Check 'the Library offers Import Charm' ([bool](FindIn ([System.Windows.Automation.ControlType]::Button) 'Import a charm'))

$yours = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show Custom$'
Check 'an import gives the Library a Custom category' ([bool]$yours)
if ($yours) {
    ClickElement $yours
    Check 'Custom shows the imported charm and nothing else' ((CharmTiles) -eq 1)
    Check 'the imported charm is named' `
        ([bool](FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Smoke Charm$'))

    $tile = FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Smoke Charm$'
    if ($tile) {
        ClickElement $tile
        Check 'an imported charm can be hung' ((Settings).overlay.charmIds -contains "custom:$importId")
        Check 'an imported charm becomes recent' ((Settings).library.recentCharmIds -contains "custom:$importId")
        Check 'Delete appears for an imported charm' `
            ([bool](FindIn ([System.Windows.Automation.ControlType]::Button) 'Delete the selected imported charm'))

        $delete = FindIn ([System.Windows.Automation.ControlType]::Button) 'Delete the selected imported charm'
        if ($delete) {
            ClickElement $delete
            Check 'deleting removes it from disk' (-not (Test-Path (Join-Path $charms "$importId.svg")))
            Check 'deleting takes it off the rope' (-not ((Settings).overlay.charmIds -contains "custom:$importId"))

            # The places that are put away name charms too, and a deleted import that
            # survived in one would come back the next time the count grew.
            Check 'deleting empties the hidden places too' `
                (-not (@((Settings).overlay.slots | ForEach-Object { $_.id }) -contains "custom:$importId"))
            Check 'deleting unstars it' `
                (-not ((Settings).library.favouriteCharmIds -contains "custom:$importId"))
            Check 'deleting forgets it' `
                (-not ((Settings).library.recentCharmIds -contains "custom:$importId"))
            Check 'deleting removes the Custom category' `
                ($null -eq (FindIn ([System.Windows.Automation.ControlType]::Button) '^Show Custom$'))
        }
    }

    $allChip = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show All$'
    if ($allChip) { ClickElement $allChip }
}

$categoryChip = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show Protection$'
if ($categoryChip) {
    ClickElement $categoryChip
    $inCategory = CharmTiles
    Check 'a category shows some charms but not all' ($inCategory -gt 0 -and $inCategory -lt 82)
    $allChip = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show All$'
    if ($allChip) { ClickElement $allChip }
}

# Hanging a charm from the Library is what fills the recents.
$daruma = FindIn ([System.Windows.Automation.ControlType]::ListItem) '^Daruma$'
Check 'a charm can be hung from the Library' ([bool]$daruma)
if ($daruma) {
    ClickElement $daruma
    Start-Sleep -Seconds 1
    Check 'hanging a charm changes the rope'   ((Settings).overlay.charmIds -contains 'daruma')
    Check 'hanging a charm records it as recent' ((Settings).library.recentCharmIds -contains 'daruma')
}

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
    Check 'About offers the coffee button' ([bool](FindIn ([System.Windows.Automation.ControlType]::Button) 'Buy Creator a Coffee'))
    Check 'About offers the creator card'  ([bool](FindIn ([System.Windows.Automation.ControlType]::Text) '^Support the Creator$'))
    Check 'About offers suggesting a charm'([bool](FindIn ([System.Windows.Automation.ControlType]::Hyperlink) '^Suggest a charm$'))

    # Milestones, and the secret button that moves one of them.
    foreach ($stat in 'StatLaunches', 'StatCharms', 'StatSwings', 'StatSecrets') {
        Check "About shows $stat" ([bool](ById $stat))
    }

    # The coffee sheet: the QR, the address, the copy, and closing again.
    $coffee = ById 'CoffeeButton'
    if ($coffee) {
        $coffee.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Seconds 3
        Check 'the coffee sheet opens'        ([bool](ById 'CoffeeUpiId'))
        Check 'the sheet shows the UPI id'    ((ById 'CoffeeUpiId').Current.Name -eq '8870786087@yescred')
        Check 'the sheet offers to copy it'   ([bool](ById 'CoffeeCopyButton'))
        Check 'the sheet offers the page'     ([bool](ById 'CoffeeOpenButton'))
        Check 'the QR shipped with the build' (-not [bool](FindIn ([System.Windows.Automation.ControlType]::Text) 'QR code is missing'))

        $copyButton = ById 'CoffeeCopyButton'
        if ($copyButton) {
            $copyButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 800
            Add-Type -AssemblyName PresentationCore
            Check 'copying puts the id on the clipboard' ([Windows.Clipboard]::GetText() -eq '8870786087@yescred')
        }

        $closeButton = FindIn ([System.Windows.Automation.ControlType]::Button) '^Close$'
        if ($closeButton) {
            $closeButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Seconds 2
        }
        Check 'closing the sheet dismisses it' (-not [bool](ById 'CoffeeUpiId'))
    }

    $secretsBefore = [int](ById 'StatSecrets').Current.Name
    $secretButton = ById 'SecretButton'
    Check 'About offers the secret button' ([bool]$secretButton)
    if ($secretButton) {
        Check 'no secret is revealed until asked' ((ById 'SecretText').Current.Name -eq 'No secret revealed yet')
        $secretButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 900
        Check 'pressing it reveals a secret'  ((ById 'SecretText').Current.Name -ne 'No secret revealed yet')
        Check 'and counts it'                 ([int](ById 'StatSecrets').Current.Name -eq $secretsBefore + 1)
    }

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

# Closing hides rather than destroys, so what the Library was showing should survive it.
$protection = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show Protection$'
if ($protection) {
    ClickElement $protection
    [UI.N]::PostMessage($window, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 3
    Check 'the app survives closing Customize' ([bool](Get-Process Hangly -ErrorAction SilentlyContinue))

    $icon = TrayIcon
    if ($icon) {
        $b = $icon.Current.BoundingRectangle
        ClickPoint ($b.X+$b.Width/2) ($b.Y+$b.Height/2) $true
        Start-Sleep -Milliseconds 900
        [System.Windows.Forms.SendKeys]::SendWait('{DOWN}{DOWN}{ENTER}')
        Start-Sleep -Seconds 5
    }
    $reopened = FindIn ([System.Windows.Automation.ControlType]::Button) '^Show Protection$'
    Check 'reopening keeps the filter that was chosen' `
        ($null -ne $reopened -and $reopened.Current.Name -eq 'Show Protection' -and
         $reopened.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'On')
}

Check 'the launch was counted' ((Settings).milestones.launchCount -ge 1)
Check 'the overlay survived all of it' ([UI.N]::FindWindowByClass('HanglyOverlay', [IntPtr]::Zero) -ne [IntPtr]::Zero)
Check 'the app survived all of it' ([bool](Get-Process Hangly -ErrorAction SilentlyContinue))

if (-not $KeepOpen) { Stop-Process -Name Hangly -Force -ErrorAction SilentlyContinue }

""
if ($failures.Count -gt 0) { "$($failures.Count) failure(s)"; exit 1 }
'all checks passed'
