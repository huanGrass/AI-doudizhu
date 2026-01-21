param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,
    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$gitEncodingArgs = @('-c', 'i18n.logOutputEncoding=utf-8', '-c', 'i18n.commitEncoding=utf-8')

$date = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$resultsPath = Join-Path $ProjectDir 'Logs\playmode-results.xml'
$editModeResultsPath = Join-Path $ProjectDir 'Logs\editmode-results.xml'
$uiLog = Join-Path $ProjectDir 'Logs\ui_batch.log'

$status = 'UNKNOWN'
$total = 'UNKNOWN'
$failed = 'UNKNOWN'

if (Test-Path $resultsPath) {
    try {
        [xml]$xml = Get-Content $resultsPath
        $run = $xml.'test-run'
        if ($run -ne $null) {
            $total = $run.total
            $failed = $run.failed
            if ([int]$failed -gt 0) {
                $status = 'FAILED'
            } else {
                $status = 'PASSED'
            }
        }
    } catch {
        $status = 'UNKNOWN'
        $total = 'UNKNOWN'
        $failed = 'UNKNOWN'
    }
}

$startup = 'UNKNOWN'
if (Test-Path $uiLog) {
    if (Select-String -Path $uiLog -Pattern 'Screenshot saved' -Quiet) {
        $startup = 'OK'
    }
}

$editStatus = 'UNKNOWN'
$editTotal = 'UNKNOWN'
$editFailed = 'UNKNOWN'
if (Test-Path $editModeResultsPath) {
    try {
        [xml]$editXml = Get-Content $editModeResultsPath
        $editRun = $editXml.'test-run'
        if ($editRun -ne $null) {
            $editTotal = $editRun.total
            $editFailed = $editRun.failed
            if ([int]$editFailed -gt 0) {
                $editStatus = 'FAILED'
            } else {
                $editStatus = 'PASSED'
            }
        }
    } catch {
        $editStatus = 'UNKNOWN'
        $editTotal = 'UNKNOWN'
        $editFailed = 'UNKNOWN'
    }
}

$gitBranch = 'UNKNOWN'
$gitCommits = @()
try {
    $gitBranch = (git -C $ProjectDir @gitEncodingArgs rev-parse --abbrev-ref HEAD 2>$null).Trim()
    $gitCommits = git -C $ProjectDir @gitEncodingArgs log -n 5 --oneline 2>$null
    if ($gitCommits -is [string]) {
        $gitCommits = $gitCommits -split "`r?`n"
    }
} catch {
    $gitBranch = 'UNKNOWN'
    $gitCommits = @()
}

$mirrorPath = Join-Path (Split-Path $ProjectDir -Parent) ((Split-Path $ProjectDir -Leaf) + '_mirror')
$mirrorReportPath = Join-Path $mirrorPath 'report.md'
$mirrorSynced = if (Test-Path $mirrorReportPath) { 'YES' } elseif (Test-Path $mirrorPath) { 'UNKNOWN' } else { 'NO' }

$playModeExecuted = if (Test-Path $resultsPath) { 'YES' } else { 'NO' }
$editModeExecuted = if (Test-Path $editModeResultsPath) { 'YES' } else { 'NO' }
$screenshotExecuted = if ($startup -eq 'OK') { 'YES' } else { 'NO' }

$screenshotObservationLines = @()
$observationPath = Join-Path $ProjectDir 'ci\screenshot_observations.txt'
$observations = @{
    'ui_shot_1.png' = 'UNKNOWN'
}

if (Test-Path $observationPath) {
    $lines = Get-Content -Path $observationPath -ErrorAction SilentlyContinue
    foreach ($line in $lines) {
        if ($line -match '^\s*ui_shot_1\.png\s*:\s*(.+)\s*$') {
            $observations['ui_shot_1.png'] = $Matches[1].Trim()
        }
    }
}

foreach ($key in @('ui_shot_1.png')) {
    $value = $observations[$key]
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = 'UNKNOWN'
    }
    $screenshotObservationLines += ('- ' + $key + ' 观察点：' + $value)
}

$exceptions = @()
if ($status -eq 'FAILED') {
    $exceptions += '- 出现异常：PlayMode Tests 失败'
}
if ($screenshotExecuted -ne 'YES') {
    $exceptions += '- 出现异常：截图未生成或日志缺失'
}
if ($exceptions.Count -eq 0) {
    $exceptions += '- 是否出现异常：否'
}

$oneClickPassed = if ($status -eq 'PASSED' -and $screenshotExecuted -eq 'YES') { 'YES' } else { 'NO' }
$hasScreenshotObservation = $screenshotObservationLines.Count -eq 1 -and
    ($screenshotObservationLines -notmatch '观察点：UNKNOWN')
$agentsOk = if ($oneClickPassed -eq 'YES' -and $mirrorSynced -eq 'YES' -and (($screenshotExecuted -ne 'YES') -or $hasScreenshotObservation)) { 'YES' } else { 'NO' }
$roundStatus = if ($oneClickPassed -eq 'YES') { '完成' } else { '未完成' }

$report = @(
    '# One Click Execution Report',
    '',
    ('时间：' + $date),
    ('工程路径：' + $ProjectDir),
    ('镜像路径：' + $mirrorPath),
    '执行脚本：ci/one_click_check.bat',
    '',
    '## 本轮目标',
    '',
    '- 目标类型：UNKNOWN',
    '- 目标描述：UNKNOWN',
    '- 是否涉及 UI：UNKNOWN',
    '',
    '## Git 状态',
    '',
    ('- 当前分支：' + $gitBranch),
    '- 本轮提交数：UNKNOWN',
    '- 提交记录：'
)

if ($gitCommits.Count -gt 0) {
    foreach ($line in $gitCommits) {
        $report += ('  - ' + $line)
    }
} else {
    $report += '  - UNKNOWN'
}

$report += @(
    ('- 是否已同步镜像：' + $mirrorSynced),
    '',
    '## 测试执行情况',
    '',
    '### PlayMode Tests',
    ('- 是否执行：' + $playModeExecuted),
    '- 命令：ci/run_playmode_tests.bat',
    '- 结果文件：Logs/playmode-results.xml',
    ('- 用例数：' + $total),
    ('- 失败数：' + $failed),
    '',
    '### EditMode Tests',
    ('- 是否执行：' + $editModeExecuted),
    '- 命令：UNKNOWN',
    '- 结果文件：Logs/editmode-results.xml',
    ('- 用例数：' + $editTotal),
    ('- 失败数：' + $editFailed),
    '',
    '## 测试场景与截图',
    '',
    ('- 是否执行截图脚本：' + $screenshotExecuted),
    '- 使用脚本：ci/capture_screenshots.bat',
    '- 生成截图：',
    '  - Screenshots\ui_shot_1.png',
    '',
    '### 截图用途说明',
    '- ui_shot_1.png：初始状态',
    '',
    '### 截图观察点',
    '（至少列出一个具体观察点，用于满足 AGENTS.md 要求）'
)

$report += $screenshotObservationLines

$report += @(
    '',
    '## 异常记录'
)

$report += $exceptions
$report += @(
    '',
    '## 完成判定',
    ('- 是否通过 one_click_check：' + $oneClickPassed),
    ('- 是否满足 AGENTS.md 所有强约束：' + $agentsOk),
    ('- 本轮状态：' + $roundStatus)
)

Set-Content -Path $ReportPath -Value $report -Encoding UTF8
