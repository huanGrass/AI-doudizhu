$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\")).Path

Write-Host "[hooks] repo: $repoRoot"
git -C $repoRoot config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    throw "设置 core.hooksPath 失败"
}

Write-Host "[hooks] 已设置 core.hooksPath=.githooks"
Write-Host "[hooks] post-commit 将在检测到客户端改动时自动执行:"
Write-Host "        1) ci/sync_mirror.ps1"
Write-Host "        2) ci/build_windows_debug.ps1"
