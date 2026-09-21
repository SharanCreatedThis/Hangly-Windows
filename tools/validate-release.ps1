# Checks a draft release's artefacts against what the release was supposed to produce.
#
# Run after the release workflow has built a draft and before anybody publishes it. The
# assets are downloaded separately — `gh release download <tag> --dir <dir>` — because
# this runs on Windows, where the pieces that can only be read on Windows are: the
# package's own nuspec, and the FileVersion stamped into the executable inside it.
#
#   gh release download v0.9.0 --dir .\release --pattern "releases.*.json" --pattern "*full.nupkg"
#   ./tools/validate-release.ps1 -Tag v0.9.0 -Dir .\release
#
# Exits non-zero if anything is wrong. It does not publish and cannot publish.

param([string] $Tag = 'v0.9.0', [string] $Dir = '.\release')

$ErrorActionPreference = 'Stop'
$script:bad = 0
function Check($name, $ok, $detail) {
  if ($ok) { "  PASS  $name - $detail" } else { $script:bad++; "  FAIL  $name - $detail" }
}
# The assets are downloaded outside this script; gh is not installed in the VM.
$files = Get-ChildItem $Dir -File
"downloaded $($files.Count) assets"
$files | ForEach-Object { "   $($_.Name)  $([math]::Round($_.Length/1MB,2)) MB" }

"== feeds and channel separation =="
Check 'arm64 feed present' (Test-Path "$Dir\releases.win-arm64.json") 'releases.win-arm64.json'
Check 'x64 feed present'   (Test-Path "$Dir\releases.win-x64.json")   'releases.win-x64.json'
Check 'no shared win feed' (-not (Test-Path "$Dir\releases.win.json")) 'an ARM64 machine cannot be offered an x64 package'

foreach ($rid in 'win-arm64','win-x64') {
  "== $rid =="
  $feed = Get-Content "$Dir\releases.$rid.json" -Raw | ConvertFrom-Json
  $asset = $feed.Assets | Select-Object -First 1
  Check "$rid feed names one package" ($feed.Assets.Count -ge 1) "$($feed.Assets.Count) asset(s): $($asset.FileName)"
  Check "$rid version"    ($asset.Version -eq ($Tag -replace '^v','')) "package version $($asset.Version)"
  Check "$rid file is $rid" ($asset.FileName -like "*$rid*") $asset.FileName
  Check "$rid notes carried" ($asset.NotesMarkdown.Length -gt 100) "$($asset.NotesMarkdown.Length) chars of NotesMarkdown"
  Check "$rid notes are the changelog" ($asset.NotesMarkdown -match 'first Hangly for Windows') 'first line of the 0.9.0 section'

  # The nuspec inside the package is Velopack's own metadata.
  $nupkg = Get-ChildItem $Dir -Filter "*$rid-full.nupkg" | Select-Object -First 1
  $extract = Join-Path 'C:\hangly\validate' "x-$rid"
  if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
  New-Item -ItemType Directory -Force -Path (Split-Path $extract) | Out-Null
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg.FullName, $extract)
  $nuspec = Get-ChildItem $extract -Filter '*.nuspec' | Select-Object -First 1
  [xml] $meta = Get-Content $nuspec.FullName
  $m = $meta.package.metadata
  Check "$rid nuspec id"      ($m.id -eq 'Hangly') "id=$($m.id)"
  Check "$rid nuspec version" ($m.version -eq ($Tag -replace '^v','')) "version=$($m.version)"
  Check "$rid nuspec title"   ($m.title -eq 'Hangly') "title=$($m.title)"
  Check "$rid nuspec authors" ($m.authors -eq 'sharancreatedthis') "authors=$($m.authors)"
  $rt = $m.runtimeDependencies; if (-not $rt) { $rt = '(none)' }
  "        channel in nuspec : $($m.channel)  machineArchitecture: $($m.machineArchitecture)"

  $exe = Get-ChildItem $extract -Recurse -Filter 'Hangly.exe' | Select-Object -First 1
  if ($exe) {
    $v = (Get-Item $exe.FullName).VersionInfo
    Check "$rid exe FileVersion" ($v.FileVersion -like "$($Tag -replace '^v','')*") "FileVersion=$($v.FileVersion) ProductVersion=$($v.ProductVersion)"
  } else { Check "$rid exe present" $false 'Hangly.exe not found in the package' }
}

""
if ($script:bad -eq 0) { "RELEASE VALIDATION PASSED"; exit 0 }
"$script:bad CHECK(S) FAILED"; exit 1
