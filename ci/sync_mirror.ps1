param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$parentDir = Split-Path $ProjectDir -Parent
$projectName = Split-Path $ProjectDir -Leaf
$mirrorDir = Join-Path $parentDir ($projectName + "_mirror")

if (-not (Test-Path $mirrorDir)) {
    New-Item -ItemType Directory -Path $mirrorDir | Out-Null
}

$include = @(
    "Assets",
    "ProjectSettings",
    "Packages",
    "ci",
    "report.md",
    "AGENTS.md"
)

foreach ($item in $include) {
    $source = Join-Path $ProjectDir $item
    if (-not (Test-Path $source)) {
        continue
    }

    $dest = Join-Path $mirrorDir $item
    if (Test-Path $dest) {
        Remove-Item -Recurse -Force $dest
    }

    Copy-Item -Recurse -Force $source $dest
}

Write-Host ("Mirror sync complete: " + $mirrorDir)
