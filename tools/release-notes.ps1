# Pulls one version's section out of CHANGELOG.md.
#
# The same text has to reach three places — the GitHub release page, the package Velopack
# builds, and the About page inside the app — and three copies of a changelog is three
# chances for them to disagree. This is the one reader; everything else calls it.
#
#   ./tools/release-notes.ps1 -Version 0.9.0 -OutFile notes.md
#
# Exits non-zero when the version has no section, which fails the release rather than
# shipping a release nobody described.

param(
    [Parameter(Mandatory = $true)][string] $Version,
    [string] $OutFile,
    [string] $Changelog = (Join-Path (Split-Path $PSScriptRoot -Parent) 'CHANGELOG.md')
)

$ErrorActionPreference = 'Stop'

$lines = Get-Content -LiteralPath $Changelog
$collected = New-Object System.Collections.Generic.List[string]
$inside = $false

foreach ($line in $lines) {
    if ($line -match '^##\s+(\S+)') {
        # A second version heading ends the section; the first one starts it.
        if ($inside) { break }
        $inside = $Matches[1] -eq $Version
        continue
    }

    if ($inside) { $collected.Add($line) }
}

if (-not $inside -and $collected.Count -eq 0) {
    Write-Error "CHANGELOG.md has no section for $Version."
    exit 1
}

$notes = ($collected -join "`n").Trim()
if ($notes.Length -eq 0) {
    Write-Error "The section for $Version is empty."
    exit 1
}

if ($OutFile) {
    # No BOM: Velopack reads the file as markdown and a BOM becomes a visible character
    # at the top of the notes.
    [System.IO.File]::WriteAllText($OutFile, $notes + "`n", (New-Object System.Text.UTF8Encoding $false))
}

$notes
