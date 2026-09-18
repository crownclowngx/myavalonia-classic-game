param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $repo ('artifacts/rich-town-tests/G1/development-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $evidence | Out-Null

# 此入口只执行 Debug 开发检查，没有发布配置、部署参数、Host 启动或统一 Gate 调用。
# 每步即时检查退出码；证据使用新目录，既不覆盖历史失败，也不把窗口未验证当作通过。
function Invoke-Dotnet([string[]]$Command, [string]$Log) {
    & dotnet @Command 2>&1 | Tee-Object -FilePath (Join-Path $evidence $Log)
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Command -join ' ') 失败，退出码 $LASTEXITCODE；证据：$evidence" }
}

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
        'docs/plan-history/rich-town/g0-design-and-development-plan.md', 'docs/plan-history/rich-town/g1-stride-integration.md')
    $links = 0
    foreach ($document in $documents) {
        $fullPath = Join-Path $repo $document
        $text = [IO.File]::ReadAllText($fullPath)
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
    [ordered]@{ configuration = 'Debug'; testSuites = $results; localLinks = $links;
        assetConversion = 'matching SHA-256'; integration = 'NOT SIGNED: see g1-stride-integration.md'; release = 'not executed' } |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidence 'summary.json') -Encoding utf8
    Write-Output "Debug 开发检查通过；G1 集成结论单独记录，不能据此签署 G1。证据：$evidence"
}
finally { Pop-Location }
