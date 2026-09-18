param(
    [ValidateSet('G1', 'G2')]
    [string]$Phase = 'G2'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $repo ('artifacts/rich-town-tests/' + $Phase + '/development-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $evidence | Out-Null

# 此入口只执行 Debug 开发检查，没有发布配置、部署参数、Host 启动或统一 Gate 调用。
# 每步即时检查退出码；证据使用新目录，既不覆盖历史失败，也不把窗口未验证当作通过。
function Invoke-Dotnet([string[]]$Command, [string]$Log) {
    & dotnet @Command 2>&1 | Tee-Object -FilePath (Join-Path $evidence $Log)
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Command -join ' ') 失败，退出码 $LASTEXITCODE；证据：$evidence" }
}

$previousSimulationReport = $env:RICH_TOWN_SIMULATION_REPORT
$env:RICH_TOWN_SIMULATION_REPORT = Join-Path $evidence 'simulation.json'
Push-Location $repo
try {
    Invoke-Dotnet -Command @('restore', 'ClassicGamePlugin.slnx', '--locked-mode', '-p:SkipPluginDeploy=true') -Log 'restore.log'
    Invoke-Dotnet -Command @('build', 'ClassicGamePlugin.slnx', '-c', 'Debug', '--no-restore', '-warnaserror', '-p:SkipPluginDeploy=true') -Log 'build.log'

    # 现有魔方绑定用例直接排空进程级 Avalonia Dispatcher。与其他无窗口用例混跑会有线程归属竞争。
    # 独立进程执行该原始用例，保留其全部断言；其余既有测试和小镇测试也全部执行，不跳过测试。
    # 精确过滤 + TRX 数量检查防止用例改名后悄悄漏测。该限制与 G1 集成问题均写入专项记录。
    $bindingTest = 'ClassicGamePlugin.Tests.RubiksCubeGeometryAndDocumentTests.Document包装绑定标题与工作台重置仅作用于当前实例'
    $suites = @(
        @{ Name = 'existing'; Project = 'tests/ClassicGamePlugin.Tests/ClassicGamePlugin.Tests.csproj'; Filter = "FullyQualifiedName!=$bindingTest" },
        @{ Name = 'existing-binding'; Project = 'tests/ClassicGamePlugin.Tests/ClassicGamePlugin.Tests.csproj'; Filter = "FullyQualifiedName=$bindingTest" },
        @{ Name = 'rich-town'; Project = 'tests/ClassicGamePlugin.RichTown.Tests/ClassicGamePlugin.RichTown.Tests.csproj'; Filter = '' }
    )
    $results = @()
    foreach ($suite in $suites) {
        $directory = Join-Path $evidence $suite.Name
        $command = @('test', $suite.Project, '-c', 'Debug', '--no-build', '--no-restore', '-p:SkipPluginDeploy=true',
            '--collect:XPlat Code Coverage', '--logger', 'trx;LogFileName=results.trx', '--results-directory', $directory)
        if ($suite.Filter) { $command += @('--filter', $suite.Filter) }
        Invoke-Dotnet -Command $command -Log ($suite.Name + '.log')
        [xml]$trx = Get-Content (Join-Path $directory 'results.trx') -Raw
        $counts = $trx.TestRun.ResultSummary.Counters
        if ([int]$counts.total -le 0 -or [int]$counts.passed -ne [int]$counts.total) { throw "$($suite.Name) 存在失败/跳过/零测试。" }
        if ($suite.Name -eq 'existing-binding' -and [int]$counts.total -ne 1) { throw 'Dispatcher 独立用例数量不符。' }
        $results += [ordered]@{ suite = $suite.Name; passed = [int]$counts.passed; failed = [int]$counts.failed; trx = "$($suite.Name)/results.trx" }
    }

    # G2 必须实际运行完整模拟，不能只有一个名称相似的空测试；报告在全部断言成功后才会写入。
    $simulation = Get-Content (Join-Path $evidence 'simulation.json') -Raw | ConvertFrom-Json
    if ($simulation.games -ne 1000 -or $simulation.seedStart -ne 0 -or $simulation.seedEnd -ne 999 -or
        $simulation.commandLimit -ne 2000 -or $simulation.maxCommands -gt $simulation.commandLimit -or
        $simulation.commands -lt 1000 -or $simulation.restoredCommands -le 0 -or
        ($simulation.roundLimitGames + $simulation.lastSurvivorGames) -ne 1000 -or
        $simulation.finalSnapshotsSha256 -notmatch '^[0-9A-F]{64}$') { throw 'G2 完整对局/恢复证据缺失或不合格。' }

    # 只约束本轮纯规则及会话，不用整个含 GPU 的插件覆盖率稀释规则缺口；类型缺失也必须失败。
    $coverageFiles = @(Get-ChildItem (Join-Path $evidence 'rich-town') -Recurse -Filter 'coverage.cobertura.xml')
    # VSTest 的 TRX 收集器可能把同一覆盖率附件复制到运行目录与 In 目录，允许字节完全相同的副本。
    # 不合并不同报告或任取最大覆盖率；无报告或存在不同内容均拒绝。
    $coverageHashes = @($coverageFiles | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash } | Sort-Object -Unique)
    if ($coverageFiles.Count -eq 0 -or $coverageHashes.Count -ne 1) { throw '小镇覆盖率缺失或包含不同内容的报告。' }
    [xml]$coverage = Get-Content -LiteralPath $coverageFiles[0].FullName -Raw
    $criticalTypes = @('Domain.RichTownBoard', 'Domain.RichTownSnapshot', 'Domain.RichTownSnapshotValidator',
        'Domain.RichTownRandom', 'Domain.RichTownRules', 'Domain.RichTownRules/Turn', 'Domain.RichTownComputerPlayer', 'Application.RichTownSession')
    $coverageResults = @()
    foreach ($type in $criticalTypes) {
        $classes = @($coverage.coverage.packages.package.classes.class | Where-Object { $_.name -eq "ClassicGamePlugin.Features.RichTown.$type" })
        if ($classes.Count -ne 1) { throw "覆盖率缺少或重复关键类型：$type" }
        $line = [double]::Parse($classes[0].'line-rate', [Globalization.CultureInfo]::InvariantCulture)
        $branch = [double]::Parse($classes[0].'branch-rate', [Globalization.CultureInfo]::InvariantCulture)
        if ($line -lt 0.95 -or $branch -lt 0.95) { throw "$type 行/分支覆盖率未达到 95%：$line / $branch" }
        $coverageResults += [ordered]@{ type = $type; lineRate = $line; branchRate = $branch }
    }
    $coverageResults | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $evidence 'rules-coverage.json') -Encoding utf8

    # 在证据目录重新转换，不覆盖仓库中已经审查过的二进制文件。
    $converted = Join-Path $evidence 'converted'
    & "$PSScriptRoot/Convert-RichTownPrototypeAsset.ps1" -OutputDirectory $converted
    $manifest = Get-Content src/ClassicGamePlugin.RichTown.Stride/Assets/asset-manifest.json -Raw | ConvertFrom-Json -AsHashtable
    foreach ($name in @('house.mesh', 'palette.rgba')) {
        if ((Get-FileHash (Join-Path $converted $name)).Hash -ne $manifest.files["Compiled/$name"]) { throw "转换结果摘要发生变化：$name" }
    }

    $sourcePaths = @('src/ClassicGamePlugin.RichTown.Stride', 'src/ClassicGamePlugin.Plugin/Features/RichTown',
        'src/ClassicGamePlugin.Standalone/Features/RichTown', 'tests/ClassicGamePlugin.RichTown.Tests')
    Invoke-Dotnet -Command (@('format', 'whitespace', 'ClassicGamePlugin.slnx', '--no-restore', '--verify-no-changes', '--include') + $sourcePaths) -Log 'format.log'

    $documents = @('README.md', 'docs/README.md', 'docs/rich-town.md', 'docs/rich-town-roadmap.md', 'docs/rich-town-assets-and-dependencies.md',
        'docs/project-and-window-responsibilities.md', 'docs/deployment-and-release.md',
        'docs/plan-history/rich-town/g0-design-and-development-plan.md', 'docs/plan-history/rich-town/g1-stride-integration.md',
        'docs/plan-history/rich-town/g2-deterministic-rules.md')
    $links = 0
    foreach ($document in $documents) {
        $fullPath = Join-Path $repo $document
        $text = [IO.File]::ReadAllText($fullPath)
        # 新专项文档可能尚未纳入 Git；直接检查行尾空白，避免只靠 git diff 漏过新文件。
        if ([regex]::IsMatch($text, '(?m)[\t ]+\r?$')) { throw "$document 存在行尾空白。" }
        foreach ($match in [regex]::Matches($text, '\[[^\]]*\]\(([^)]+)\)')) {
            $link = $match.Groups[1].Value.Trim('<', '>')
            if ($link -match '^(https?://|#|mailto:)') { continue }
            $relative = [Uri]::UnescapeDataString(($link -split '#')[0])
            if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $fullPath) $relative))) { throw "$document 中本地链接失效：$link" }
            $links++
        }
    }
    # 同时检查暂存和未暂存的第一方差异，防止提交前暂存后漏检新增文件。认可既有 CRLF。
    # 原始第三方素材及 Stride 许可保持来源字节；其中上游空白不改写，素材完整性由摘要测试核对。
    $diffPaths = @('.', ':(exclude)src/ClassicGamePlugin.RichTown.Stride/Assets/Source/**',
        ':(exclude)src/ClassicGamePlugin.RichTown.Stride/Licenses/Stride.txt')
    & git -c core.whitespace=cr-at-eol diff --check -- @diffPaths
    if ($LASTEXITCODE -ne 0) { throw '未暂存第一方差异检查失败。' }
    & git -c core.whitespace=cr-at-eol diff --cached --check -- @diffPaths
    if ($LASTEXITCODE -ne 0) { throw '已暂存第一方差异检查失败。' }
    [ordered]@{ phase = $Phase; configuration = 'Debug'; testSuites = $results; localLinks = $links;
        deterministicRules = 'passed'; simulation = $simulation; rulesCoverage = $coverageResults;
        assetConversion = 'matching SHA-256'; integration = 'NOT SIGNED: see g1-stride-integration.md'; release = 'not executed' } |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidence 'summary.json') -Encoding utf8
    Write-Output "Debug 开发检查及 G2 规则门禁通过；G1 集成结论单独记录，不能据此签署 G1。证据：$evidence"
}
finally {
    $env:RICH_TOWN_SIMULATION_REPORT = $previousSimulationReport
    Pop-Location
}
