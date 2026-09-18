param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw '此入口需要本机 Windows 桌面与可用显卡；它不是无窗口单元测试。' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $repo ('artifacts/rich-town-tests/G3/window-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $evidence | Out-Null
Push-Location $repo
try {
    # 只检查开发预览程序的真实窗口，不部署插件、不启动日常 Host，也不运行发布或 Windows CI 流程。
    if (-not $SkipBuild) {
        & dotnet build src/ClassicGamePlugin.Standalone/ClassicGamePlugin.Standalone.csproj -c Debug -warnaserror -p:SkipPluginDeploy=true 2>&1 |
            Tee-Object -FilePath (Join-Path $evidence 'build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Debug 开发窗口构建失败。' }
    }
    $executable = Join-Path $repo 'src/ClassicGamePlugin.Standalone/bin/Debug/net10.0/ClassicGamePlugin.Standalone.exe'
    $reportPath = Join-Path $evidence 'window-result.json'
    $preview = Start-Process -FilePath $executable -ArgumentList @('--rich-town-play-check', ('"' + $reportPath + '"')) `
        -WorkingDirectory $repo -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $evidence 'stdout.log') -RedirectStandardError (Join-Path $evidence 'stderr.log')
    # 只等待/结束本入口刚创建的进程；超时不扫描或关闭用户其他窗口。
    $deadline = [DateTime]::UtcNow.AddMinutes(7)
    while (-not $preview.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -ge $deadline) { $preview.Kill(); throw "窗口检查超时，日志：$evidence" }
    }
    $preview.WaitForExit()
    if ($preview.ExitCode -ne 0) { throw "窗口检查进程异常退出：$($preview.ExitCode)；证据：$evidence" }
    if (-not (Test-Path -LiteralPath $reportPath)) { throw "未生成窗口结果，查看 stderr.log：$evidence" }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $result.passed -or $result.phase -ne 'Finished' -or $result.humanCommands -le 0 -or -not $result.documentDisposed) {
        throw "窗口完整对局未通过：$($result.error)；证据：$evidence"
    }
    Write-Output "本机 Standalone 真实窗口完整对局通过；不代表真实 Host/G1 验收。证据：$evidence"
}
finally { Pop-Location }
