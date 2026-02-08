param(
    [string]$UnityExe = "",
    [string]$ProjectDir = "",
    [string]$LogPath = ""
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectDir)) {
    $ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$projectName = Split-Path $ProjectDir -Leaf
$mirrorDir = Join-Path (Split-Path $ProjectDir -Parent) ($projectName + "_mirror")
if (-not (Test-Path $mirrorDir)) {
    throw "镜像目录不存在: $mirrorDir，请先执行 ci/sync_mirror.ps1"
}

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $candidates = @(
        "D:\Tool\DevTools\Unity\Unity6\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Unity.exe"
    )

    foreach ($item in $candidates) {
        if (Test-Path $item) {
            $UnityExe = $item
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($UnityExe) -or -not (Test-Path $UnityExe)) {
    throw "未找到 Unity.exe，请通过 -UnityExe 指定路径"
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $mirrorDir "Builds\build_windows_debug.log"
}

Write-Host "[build] Unity: $UnityExe"
Write-Host "[build] Project: $mirrorDir"
Write-Host "[build] Log: $LogPath"

& $UnityExe -batchmode -quit -projectPath $mirrorDir -executeMethod BuildWindowsDebug.Build -logFile $LogPath
$exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
if ($exitCode -ne 0) {
    throw "Unity 调试打包失败，退出码: $exitCode"
}

$outputExe = Join-Path $mirrorDir "Builds\WindowsClient_Debug\Doudizhu_Debug.exe"
if (-not (Test-Path $outputExe)) {
    throw "打包未产出可执行文件: $outputExe"
}

Write-Host "[build] 调试包完成: $outputExe"
