[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$ReuseVerifiedG8
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\test-results'))
$resultRoot = [IO.Path]::GetFullPath((Join-Path $allowedRoot 'ClassicGameWorkbenchCommandG10'))
$g8SummaryPath = Join-Path $allowedRoot 'ClassicGameWorkbenchCommandG8\summary.json'

function Assert-True {
    param([Parameter(Mandatory)] [bool]$Condition, [Parameter(Mandatory)] [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Checked {
    param([Parameter(Mandatory)] [string]$FilePath, [Parameter(Mandatory)] [string[]]$Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') 失败，退出码：$LASTEXITCODE。"
    }
}

$prefix = $allowedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
Assert-True ($resultRoot.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) `
    'ClassicGame G10 结果目录越过 artifacts/test-results。'
if (Test-Path -LiteralPath $resultRoot) {
    Remove-Item -LiteralPath $resultRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $resultRoot | Out-Null

Push-Location $repositoryRoot
try {
    # G10 包装层继续让 G8 叶子脚本拥有十三游戏、覆盖率、确定性 ZIP 与 manifest
    # 规则。本入口只投影稳定事实，不调用 AIFLOW、Windows CI/Smoke 或发布门禁。
    if (-not $ReuseVerifiedG8) {
        Invoke-Checked pwsh @(
            '-NoProfile', '-File',
            (Join-Path $PSScriptRoot 'Test-ClassicGameWorkbenchCommandG8.ps1'),
            '-Configuration', $Configuration)
    }
    Assert-True (Test-Path -LiteralPath $g8SummaryPath -PathType Leaf) `
        'ClassicGame G10 缺少可复用的 G8 summary.json。'
    $g8 = Get-Content -Raw -LiteralPath $g8SummaryPath | ConvertFrom-Json
    Assert-True ($g8.stage -ceq 'WorkbenchCommandG8') 'ClassicGame G8 阶段身份漂移。'
    Assert-True ($g8.configuration -ceq $Configuration) 'ClassicGame G8 编译配置漂移。'
    Assert-True (
        [int]$g8.tests.passed -ge 526 -and
        [int]$g8.tests.failed -eq 0 -and
        [int]$g8.tests.skipped -eq 0) 'ClassicGame G8 测试未达到 526 项、零失败、零跳过。'
    Assert-True ([double]$g8.lineCoverage -ge 70.87 -and [double]$g8.branchCoverage -ge 57.82) `
        'ClassicGame G8 覆盖率低于既有 70.87% / 57.82% 门槛。'
    Assert-True (
        [double]$g8.gomokuDocumentLineCoverage -eq 100 -and
        [double]$g8.workbenchDocumentCommandAdapterLineCoverage -eq 100) `
        'ClassicGame G8 两个 Command 关键文件未达到 100% 行覆盖率。'
    Assert-True (
        [int]$g8.deterministicBuilds -eq 2 -and
        [string]$g8.archiveSha256 -match '^[0-9A-F]{64}$') `
        'ClassicGame G8 缺少两次确定性 ZIP 或规范 SHA-256。'
    Assert-True (
        [int]$g8.manifest.schemaVersion -eq 2 -and
        $g8.manifest.pluginId -ceq 'myavalonia.plugin.classic.game' -and
        $g8.manifest.pluginVersion -ceq '1.1.0' -and
        $g8.manifest.sdk.minInclusive -ceq '3.3.0' -and
        $g8.manifest.sdk.maxExclusive -ceq '4.0.0') `
        'ClassicGame manifest schema、身份、版本或 SDK 区间漂移。'
    foreach ($flag in @(
            'aiflow', 'windowsCi', 'windowsSmoke', 'releaseAcceptance', 'releaseGate',
            'publishable', 'published', 'uploaded', 'signed', 'tagCreated')) {
        Assert-True ($g8.PSObject.Properties[$flag] -and -not [bool]$g8.$flag) `
            "ClassicGame G8 非发布标记 $flag 必须为 false。"
    }

    $history = Join-Path $repositoryRoot `
        'docs\plan-history\workbench-command\g10-classic-game-local-sealing.md'
    Assert-True (Test-Path -LiteralPath $history -PathType Leaf) `
        'ClassicGame 缺少 G10 专项记录。'
    $historyText = Get-Content -Raw -LiteralPath $history
    foreach ($fragment in @(
            'SOLID', 'Test-ClassicGameWorkbenchCommandG10.ps1', 'aiflow=false',
            'windowsSmoke=false', 'releaseGate=false', 'publishable=false')) {
        Assert-True ($historyText.Contains($fragment, [StringComparison]::OrdinalIgnoreCase)) `
            "ClassicGame G10 专项记录缺少：$fragment。"
    }

    $summary = [ordered]@{
        schemaVersion = 1
        stage = 'WorkbenchCommandG10'
        repository = 'ClassicGame'
        configuration = $Configuration
        g8Reused = [bool]$ReuseVerifiedG8
        tests = $g8.tests
        lineCoverage = [double]$g8.lineCoverage
        branchCoverage = [double]$g8.branchCoverage
        gomokuDocumentLineCoverage = [double]$g8.gomokuDocumentLineCoverage
        workbenchDocumentCommandAdapterLineCoverage =
            [double]$g8.workbenchDocumentCommandAdapterLineCoverage
        archiveSha256 = [string]$g8.archiveSha256
        packageFiles = [int]$g8.packageFiles
        deterministicBuilds = [int]$g8.deterministicBuilds
        hostInputRoot = [string]$g8.hostInputRoot
        manifest = $g8.manifest
        commandCount = 22
        documentCount = 13
        passed = $true
        aiflow = $false
        windowsCi = $false
        windowsSmoke = $false
        releaseAcceptance = $false
        releaseGate = $false
        publishable = $false
        published = $false
        uploaded = $false
        signed = $false
        tagCreated = $false
        generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    }
    [IO.File]::WriteAllText(
        (Join-Path $resultRoot 'summary.json'),
        ($summary | ConvertTo-Json -Depth 12),
        [Text.UTF8Encoding]::new($false))
    Write-Host (
        "ClassicGame G10 本地封板通过：$($g8.tests.passed) 项，" +
        "覆盖率 $($g8.lineCoverage)% / $($g8.branchCoverage)%。")
}
finally {
    Pop-Location
}
