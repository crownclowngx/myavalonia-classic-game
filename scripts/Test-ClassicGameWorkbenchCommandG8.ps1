[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$ReuseNuGetCache
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resultRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot `
    'artifacts\test-results\ClassicGameWorkbenchCommandG8'))
$allowedResultRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\test-results'))
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\nuget-cache\g8-public'))
$allowedCacheRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\nuget-cache'))
$solution = Join-Path $repositoryRoot 'ClassicGamePlugin.slnx'
$pluginProject = Join-Path $repositoryRoot 'src\ClassicGamePlugin.Plugin\ClassicGamePlugin.Plugin.csproj'
$standaloneProject = Join-Path $repositoryRoot `
    'src\ClassicGamePlugin.Standalone\ClassicGamePlugin.Standalone.csproj'
$testProject = Join-Path $repositoryRoot 'tests\ClassicGamePlugin.Tests\ClassicGamePlugin.Tests.csproj'

function Assert-ChildPath {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Parent,
        [Parameter(Mandatory)][string]$Label)
    $prefix = $Parent.TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $Candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label 目录越界：$Candidate。"
    }
}

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Checked {
    param([Parameter(Mandatory)][string]$FilePath, [Parameter(Mandatory)][string[]]$Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') 失败，退出码：$LASTEXITCODE。"
    }
}

function Get-TrxCounts {
    param([Parameter(Mandatory)][string]$Path)
    [xml]$trx = Get-Content -Raw -LiteralPath $Path
    $counters = $trx.TestRun.ResultSummary.Counters
    return [ordered]@{
        passed = [int]$counters.passed
        failed = [int]$counters.failed
        skipped = [int]$counters.notExecuted
    }
}

function Get-FileLineCoverage {
    param([Parameter(Mandatory)][xml]$Coverage, [Parameter(Mandatory)][string]$FileName)
    $lines = @($Coverage.coverage.packages.package.classes.class |
        Where-Object { $_.filename -ceq $FileName } |
        ForEach-Object { $_.lines.line } |
        Group-Object number |
        ForEach-Object {
            [pscustomobject]@{
                Hits = [int](($_.Group | Measure-Object -Property hits -Maximum).Maximum)
            }
        })
    Assert-True ($lines.Count -gt 0) "覆盖率报告缺少关键文件：$FileName。"
    $covered = @($lines | Where-Object { $_.Hits -gt 0 }).Count
    return [Math]::Round(100 * $covered / $lines.Count, 2)
}

function Read-ZipEntries {
    param([Parameter(Mandatory)][string]$Path)
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try { return @($archive.Entries | ForEach-Object FullName) }
    finally { $archive.Dispose() }
}

function Read-ZipText {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$EntryName)
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry($EntryName)
        if ($null -eq $entry) { throw "ZIP 缺少条目：$EntryName。" }
        $reader = [IO.StreamReader]::new($entry.Open(), [Text.Encoding]::UTF8)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $archive.Dispose() }
}

function Test-MarkdownLinks {
    $markdownFiles = Get-ChildItem -LiteralPath $repositoryRoot -Filter '*.md' -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts|TestResults)[\\/]' }
    foreach ($file in $markdownFiles) {
        $text = Get-Content -Raw -LiteralPath $file.FullName
        foreach ($match in [regex]::Matches(
                $text,
                '\[[^\]]+\]\((?!https?://|#)(?<path>[^)#]+)(?:#[^)]+)?\)')) {
            $relative = [Uri]::UnescapeDataString($match.Groups['path'].Value)
            $target = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $relative))
            Assert-True (Test-Path -LiteralPath $target) `
                "文档链接失效：$($file.FullName) -> $relative。"
        }
    }
}

Assert-ChildPath $resultRoot $allowedResultRoot 'G8 结果'
Assert-ChildPath $cacheRoot $allowedCacheRoot 'G8 NuGet 缓存'
Assert-True ((Split-Path -Leaf $repositoryRoot) -ceq 'myavalonia-classic-game') `
    'G8 必须从独立 myavalonia-classic-game 仓库根执行。'

if (Test-Path -LiteralPath $resultRoot) {
    Remove-Item -LiteralPath $resultRoot -Recurse -Force
}
if ((Test-Path -LiteralPath $cacheRoot) -and -not $ReuseNuGetCache) {
    Remove-Item -LiteralPath $cacheRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $resultRoot, $cacheRoot -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

$previousPackages = [Environment]::GetEnvironmentVariable('NUGET_PACKAGES', 'Process')
try {
    $env:NUGET_PACKAGES = $cacheRoot
    $nugetConfig = Join-Path $resultRoot 'NuGet.G8.config'
    $nugetText = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
'@
    [IO.File]::WriteAllText($nugetConfig, $nugetText, [Text.UTF8Encoding]::new($false))

    Push-Location $repositoryRoot
    try {
        # G8 是本地开发门禁。Release 只表示编译配置；本脚本不读取 AIFLOW，也不调用
        # Windows CI/Smoke、Release Acceptance、发布门禁、签名、上传或 tag。
        $allProductionText = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') `
            -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' } |
            ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
        Assert-True (-not (($allProductionText -join "`n") -match
                'avalonia_dock_simple_test|<ProjectReference[^>]*MyAvaloniaManagement')) `
            'ClassicGame 生产源码出现 Host 路径或源码 ProjectReference。'

        $packagesText = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Packages.props')
        foreach ($fragment in @(
                'MyAvaloniaManagement.PluginSdk" Version="[3.3.0]"',
                'MyAvaloniaManagement.PluginSdk.UI" Version="[3.3.0]"',
                'MyAvaloniaManagement.Plugin.Build" Version="[1.1.2]"')) {
            Assert-True ($packagesText.Contains($fragment, [StringComparison]::Ordinal)) `
                "G8 精确包引用缺失：$fragment。"
        }
        $pluginText = Get-Content -Raw -LiteralPath $pluginProject
        Assert-True ($pluginText -match '<PluginVersion>1\.1\.0</PluginVersion>') `
            'ClassicGame G8 插件版本必须为 1.1.0。'
        Assert-True ($pluginText -match
                '<ManagedPluginSdkMinInclusive>3\.3\.0</ManagedPluginSdkMinInclusive>') `
            'ClassicGame G8 manifest SDK 下界必须为 3.3.0。'
        Assert-True ((rg -n 'registration.AddDocument<' `
                    'src/ClassicGamePlugin.Plugin/Plugin/ClassicGamePluginModule.cs' |
                Measure-Object).Count -eq 13) 'G8 必须保留 13 个 Document 注册。'

        $commandDesignPath = Join-Path $repositoryRoot 'docs\workbench-commands.md'
        $g8HistoryPath = Join-Path $repositoryRoot `
            'docs\plan-history\workbench-command\g8-classic-game-multi-instance-commands.md'
        Assert-True (Test-Path -LiteralPath $commandDesignPath -PathType Leaf) `
            'G8 缺少 ClassicGame Workbench Command 设计文档。'
        Assert-True (Test-Path -LiteralPath $g8HistoryPath -PathType Leaf) `
            'G8 缺少 ClassicGame 专用实施记录。'
        $commandDesign = Get-Content -Raw -LiteralPath $commandDesignPath
        foreach ($fragment in @(
                'myavalonia.plugin.classic.game.command.gomoku.restart',
                'myavalonia.plugin.classic.game.command.gomoku.undo',
                '22 条 Command',
                '13 条 Restart',
                '9 条 Undo',
                'IWorkbenchDocumentCommandTarget',
                'aiflow=false')) {
            Assert-True ($commandDesign.Contains($fragment, [StringComparison]::OrdinalIgnoreCase)) `
                "G8 Command 设计文档缺少事实：$fragment。"
        }

        Invoke-Checked dotnet @(
            'restore', $solution, '--locked-mode', '--configfile', $nugetConfig,
            '--packages', $cacheRoot, '--nologo')
        Invoke-Checked dotnet @(
            'build', $solution, '-c', $Configuration, '--no-restore', '-warnaserror',
            '-m:1', '--nologo')
        Invoke-Checked dotnet @(
            'format', $solution, '--verify-no-changes', '--no-restore', '--verbosity', 'minimal')
        Invoke-Checked dotnet @(
            'build', $standaloneProject, '-c', $Configuration, '--no-restore',
            '-warnaserror', '-m:1', '--nologo')

        $testRoot = Join-Path $resultRoot 'tests'
        Invoke-Checked dotnet @(
            'test', $testProject, '-c', $Configuration, '--no-build', '--no-restore',
            '--collect:XPlat Code Coverage', '--results-directory', $testRoot,
            '--logger', 'trx;LogFileName=ClassicGameWorkbenchCommandG8.trx')
        $tests = Get-TrxCounts (Join-Path $testRoot 'ClassicGameWorkbenchCommandG8.trx')
        Assert-True ($tests.failed -eq 0) 'ClassicGame G8 单元测试存在失败。'
        Assert-True ($tests.skipped -eq 0) 'ClassicGame G8 单元测试存在跳过。'
        Assert-True ($tests.passed -ge 526) 'ClassicGame G8 单元测试低于 526 项全游戏验收值。'

        $coveragePath = (Get-ChildItem -LiteralPath $testRoot -Filter 'coverage.cobertura.xml' `
            -File -Recurse | Select-Object -First 1).FullName
        Assert-True (-not [string]::IsNullOrWhiteSpace($coveragePath)) 'G8 没有生成 Cobertura 覆盖率。'
        [xml]$coverage = Get-Content -Raw -LiteralPath $coveragePath
        $lineCoverage = [Math]::Round([double]$coverage.coverage.'line-rate' * 100, 2)
        $branchCoverage = [Math]::Round([double]$coverage.coverage.'branch-rate' * 100, 2)
        $gomokuDocumentCoverage = Get-FileLineCoverage $coverage `
            'Features\Gomoku\GomokuDocument.cs'
        $adapterCoverage = Get-FileLineCoverage $coverage `
            'Workbench\WorkbenchDocumentCommandAdapter.cs'
        Assert-True ($lineCoverage -ge 70.87) `
            "G8 行覆盖率 $lineCoverage% 低于冻结值 70.87%。"
        Assert-True ($branchCoverage -ge 57.82) `
            "G8 分支覆盖率 $branchCoverage% 低于冻结值 57.82%。"
        Assert-True ($gomokuDocumentCoverage -eq 100) `
            "GomokuDocument 行覆盖率 $gomokuDocumentCoverage% 未达到 100%。"
        Assert-True ($adapterCoverage -eq 100) `
            "WorkbenchDocumentCommandAdapter 行覆盖率 $adapterCoverage% 未达到 100%。"

        $packageRoots = @(
            (Join-Path $resultRoot 'package-1'),
            (Join-Path $resultRoot 'package-2'))
        foreach ($packageRoot in $packageRoots) {
            Invoke-Checked dotnet @(
                'msbuild', $pluginProject, '-t:BuildManagedPluginPackage',
                "-p:Configuration=$Configuration",
                "-p:ManagedPluginPackageOutput=$packageRoot")
        }
        $zip1 = (Get-ChildItem -LiteralPath $packageRoots[0] -Filter '*.zip' -File).FullName
        $zip2 = (Get-ChildItem -LiteralPath $packageRoots[1] -Filter '*.zip' -File).FullName
        Assert-True (-not [string]::IsNullOrWhiteSpace($zip1) -and
            -not [string]::IsNullOrWhiteSpace($zip2)) 'G8 两次包构建没有各生成一个 ZIP。'
        $hash1 = (Get-FileHash -LiteralPath $zip1 -Algorithm SHA256).Hash
        $hash2 = (Get-FileHash -LiteralPath $zip2 -Algorithm SHA256).Hash
        Assert-True ($hash1 -ceq $hash2) 'ClassicGame G8 两次 ZIP 的 SHA-256 不一致。'
        $entries = Read-ZipEntries $zip1
        foreach ($required in @(
                'Controls/ClassicGamePlugin/plugin.manifest.json',
                'Controls/ClassicGamePlugin/ClassicGamePlugin.Plugin.dll',
                'Controls/ClassicGamePlugin/ClassicGamePlugin.Plugin.deps.json')) {
            Assert-True ($entries -ccontains $required) "ClassicGame G8 ZIP 缺少 $required。"
        }
        Assert-True (-not ($entries -match
                'Standalone|Tests|MyAvaloniaManagement\.PluginSdk.*\.dll')) `
            'G8 ZIP 混入 Standalone、Tests 或 Host 共享 SDK。'
        $manifest = Read-ZipText $zip1 `
            'Controls/ClassicGamePlugin/plugin.manifest.json' | ConvertFrom-Json
        Assert-True (
            [int]$manifest.schemaVersion -eq 2 -and
            $manifest.pluginId -ceq 'myavalonia.plugin.classic.game' -and
            $manifest.pluginVersion -ceq '1.1.0' -and
            $manifest.sdk.minInclusive -ceq '3.3.0' -and
            $manifest.sdk.maxExclusive -ceq '4.0.0') `
            'G8 manifest schema、身份、版本或 SDK 区间不正确。'

        $extractRoot = Join-Path $resultRoot 'host-input'
        Expand-Archive -LiteralPath $zip1 -DestinationPath $extractRoot
        Test-MarkdownLinks

        $summary = [ordered]@{
            schemaVersion = 1
            stage = 'WorkbenchCommandG8'
            configuration = $Configuration
            input = [ordered]@{
                commit = '1d11f1689433caf242365480233dd76ff5c8836b'
                tree = '1f3e826735c6ea191bbf68cb658557260a734fc2'
                documentRegistrations = 13
                testDeclarations = 409
                workingTreeEolOnly = $true
                lockedRestoreAtInput = $false
                lockedRestoreInputFailure = 'NU1403'
            }
            tests = $tests
            lineCoverage = $lineCoverage
            branchCoverage = $branchCoverage
            gomokuDocumentLineCoverage = $gomokuDocumentCoverage
            workbenchDocumentCommandAdapterLineCoverage = $adapterCoverage
            deterministicBuilds = 2
            archiveSha256 = $hash1
            packageFiles = $entries.Count
            pluginZip = $zip1
            hostInputRoot = (Join-Path $extractRoot 'Controls')
            manifest = $manifest
            nugetCacheReused = [bool]$ReuseNuGetCache
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
            ($summary | ConvertTo-Json -Depth 10),
            [Text.UTF8Encoding]::new($false))
        Write-Host (
            "ClassicGame G8 门禁通过：$($tests.passed) 项，覆盖率 " +
            "$lineCoverage% / $branchCoverage%，ZIP $hash1。")
    }
    finally {
        Pop-Location
    }
}
finally {
    [Environment]::SetEnvironmentVariable('NUGET_PACKAGES', $previousPackages, 'Process')
    & dotnet build-server shutdown | Out-Null
}
