<#
.SYNOPSIS
    Generates CHANGELOG.md from git history, one section per release tag.

.DESCRIPTION
    Each release section lists the commits between that tag and the previous release tag
    (tags matching vMAJOR.MINOR.PATCH). If -Ref does not point at a release tag, commits since
    the latest release are listed under -Version (or "Unreleased" when -Version is omitted).

    Commit subjects are grouped by their conventional prefix:
      feat              -> Added
      fix               -> Fixed
      ref, refactor,    -> Changed
      perf
      docs              -> Documentation
      ci, build, chore, -> Maintenance
      test, style
      anything else     -> Other

    Optionally writes the notes for the newest section only to -ReleaseNotesPath, which the
    project packs into the NuGet package's release notes.

.EXAMPLE
    ./scripts/Generate-Changelog.ps1

.EXAMPLE
    ./scripts/Generate-Changelog.ps1 -Ref v1.1.0 -ReleaseNotesPath ApplicationLogging/RELEASE_NOTES.md
#>
[CmdletBinding()]
param(
    [string]$Ref = 'HEAD',
    [string]$Version,
    [string]$ChangelogPath,
    [string]$ReleaseNotesPath
)

$ErrorActionPreference = 'Stop'
if (-not $ChangelogPath) { $ChangelogPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\CHANGELOG.md' }
$semver = '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$'

function Invoke-Git {
    $output = & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $args failed with exit code $LASTEXITCODE" }
    return $output
}

function Get-RepositoryUrl {
    $remote = & git remote get-url origin 2>$null
    if (-not $remote) { return $null }
    $remote = $remote -replace '^git@([^:]+):', 'https://$1/' -replace '\.git$', ''
    return $remote
}

function Get-Commits([string]$Range) {
    $separator = [char]0x1f
    Invoke-Git log --no-merges "--format=%h$separator%s" $Range | Where-Object { $_ } | ForEach-Object {
        $hash, $subject = $_ -split $separator, 2
        [pscustomobject]@{ Hash = $hash; Subject = $subject.Trim() }
    }
}

function Get-Category([string]$Type) {
    switch -Regex ($Type) {
        '^feat$' { 'Added' }
        '^fix$' { 'Fixed' }
        '^(ref|refactor|perf)$' { 'Changed' }
        '^docs$' { 'Documentation' }
        '^(ci|build|chore|test|style)$' { 'Maintenance' }
        default { 'Other' }
    }
}

function Format-Section($Section, [string]$RepoUrl, [switch]$NoHeading) {
    $categoryOrder = 'Added', 'Changed', 'Fixed', 'Documentation', 'Maintenance', 'Other'
    $lines = [System.Collections.Generic.List[string]]::new()

    if (-not $NoHeading) {
        $title = if ($RepoUrl -and $Section.CompareRange) { "[$($Section.Name)]($RepoUrl/compare/$($Section.CompareRange))" } else { $Section.Name }
        $date = if ($Section.Date) { " - $($Section.Date)" } else { '' }
        $lines.Add("## $title$date")
        $lines.Add('')
    }

    $entries = foreach ($commit in $Section.Commits) {
        if ($commit.Subject -match '^(?<type>[A-Za-z]+)(\([^)]*\))?!?:\s*(?<text>.+)$') {
            $category = Get-Category $Matches.type.ToLowerInvariant()
            $text = $Matches.text
        }
        else {
            $category = 'Other'
            $text = $commit.Subject
        }
        $text = $text.Substring(0, 1).ToUpperInvariant() + $text.Substring(1)
        $link = if ($RepoUrl) { "[``$($commit.Hash)``]($RepoUrl/commit/$($commit.Hash))" } else { "``$($commit.Hash)``" }
        [pscustomobject]@{ Category = $category; Line = "- $text ($link)" }
    }

    if (-not $entries) {
        $lines.Add('- No notable changes.')
        $lines.Add('')
        return $lines
    }

    foreach ($category in $categoryOrder) {
        $items = @($entries | Where-Object Category -eq $category)
        if ($items.Count -eq 0) { continue }
        $lines.Add("### $category")
        $lines.Add('')
        $items | ForEach-Object { $lines.Add($_.Line) }
        $lines.Add('')
    }

    return $lines
}

$repoUrl = Get-RepositoryUrl
$refCommit = Invoke-Git rev-parse "$Ref^{commit}"

# Release tags reachable from the ref, newest first
$tags = @(Invoke-Git tag --merged $refCommit --sort=-v:refname | Where-Object { $_ -match $semver })

$sections = [System.Collections.Generic.List[object]]::new()

$latestTagCommit = if ($tags.Count -gt 0) { Invoke-Git rev-parse "$($tags[0])^{commit}" } else { $null }
if ($refCommit -ne $latestTagCommit) {
    $range = if ($tags.Count -gt 0) { "$($tags[0])..$refCommit" } else { $refCommit }
    $name = if ($Version) { $Version } else { 'Unreleased' }
    $sections.Add([pscustomobject]@{
        Name         = $name
        Date         = if ($Version) { Get-Date -Format 'yyyy-MM-dd' } else { $null }
        CompareRange = if ($tags.Count -gt 0) { "$($tags[0])...$(if ($Version) { "v$($Version.TrimStart('v'))" } else { 'HEAD' })" } else { $null }
        Commits      = @(Get-Commits $range)
    })
}

for ($i = 0; $i -lt $tags.Count; $i++) {
    $tag = $tags[$i]
    $previous = if ($i + 1 -lt $tags.Count) { $tags[$i + 1] } else { $null }
    $sections.Add([pscustomobject]@{
        Name         = $tag.TrimStart('v')
        Date         = Invoke-Git log -1 --format=%as $tag
        CompareRange = if ($previous) { "$previous...$tag" } else { $null }
        Commits      = @(Get-Commits $(if ($previous) { "$previous..$tag" } else { $tag }))
    })
}

$output = [System.Collections.Generic.List[string]]::new()
$output.Add('# Changelog')
$output.Add('')
$output.Add('All notable changes to this project are documented in this file.')
$output.Add('Each release lists the changes since the previous release tag.')
$output.Add('This file is generated by `Generate-Changelog.ps1`; do not edit it by hand.')
$output.Add('')
foreach ($section in $sections) {
    (Format-Section $section $repoUrl) | ForEach-Object { $output.Add($_) }
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$changelogFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ChangelogPath)
[System.IO.File]::WriteAllText($changelogFullPath, (($output -join "`n").TrimEnd() + "`n"), $utf8NoBom)
Write-Host "Wrote $changelogFullPath"

if ($ReleaseNotesPath -and $sections.Count -gt 0) {
    $notes = Format-Section $sections[0] $null -NoHeading
    $notesFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReleaseNotesPath)
    [System.IO.File]::WriteAllText($notesFullPath, (($notes -join "`n").Trim() + "`n"), $utf8NoBom)
    Write-Host "Wrote $notesFullPath"
}
