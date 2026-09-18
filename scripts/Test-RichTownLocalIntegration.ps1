param([switch]$SkipBuild, [switch]$WritablePreview)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw '此显式本机检查需要 Windows 桌面与显卡，不是 Windows CI。' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $repo ('artifacts/rich-town-tests/G5/local-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $evidence | Out-Null
Push-Location $repo
$originalAcl = $null
$preview = $null
try {
    if (-not $SkipBuild) {
        & dotnet build src/ClassicGamePlugin.Standalone/ClassicGamePlugin.Standalone.csproj -c Debug -warnaserror -p:SkipPluginDeploy=true 2>&1 |
            Tee-Object -FilePath (Join-Path $evidence 'build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Debug 本机检查构建失败。' }
    }
    # 这是独立开发预览程序的中文/空格路径副本，不是插件暂存目录，不声称通过插件 ALC 或部署协议。
    # 仅复制当前平台需要的输出；原目录、NuGet 缓存和日常 Host 不变。
    $source = Join-Path $repo 'src/ClassicGamePlugin.Standalone/bin/Debug/net10.0'
    $previewRoot = [IO.Path]::GetFullPath((Join-Path $evidence $(if ($WritablePreview) { '中文 可写预览' } else { '中文 只读预览' })))
    if (-not $previewRoot.StartsWith($evidence + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '预览路径越界。' }
    New-Item -ItemType Directory -Path $previewRoot | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
        if ($relative -match '^runtimes[\\/](?!win-x64[\\/])') { continue }
        $destination = Join-Path $previewRoot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    $emptyPackages = Join-Path $evidence 'empty-nuget-cache'
    New-Item -ItemType Directory -Path $emptyPackages | Out-Null
    # 仅在新建副本上临时拒绝当前用户的写入/删除；finally 恢复原 ACL，异常也不会锁住用户文件。
    if (-not $WritablePreview) {
        $originalAcl = Get-Acl -LiteralPath $previewRoot
        $readOnlyAcl = Get-Acl -LiteralPath $previewRoot
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity,
            [Security.AccessControl.FileSystemRights]'Write,Delete,DeleteSubdirectoriesAndFiles',
            [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',
            [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Deny)
        $readOnlyAcl.AddAccessRule($rule)
        Set-Acl -LiteralPath $previewRoot -AclObject $readOnlyAcl
    }
    $reportPath = Join-Path $evidence 'local-result.json'
    $preview = Start-Process -FilePath (Join-Path $previewRoot 'ClassicGamePlugin.Standalone.exe') `
        -ArgumentList @('--rich-town-integration-check', ('"' + $reportPath + '"')) -WorkingDirectory $evidence `
        -WindowStyle Hidden -PassThru -Environment @{ NUGET_PACKAGES = $emptyPackages } `
        -RedirectStandardOutput (Join-Path $evidence 'stdout.log') -RedirectStandardError (Join-Path $evidence 'stderr.log')
    $deadline = [DateTime]::UtcNow.AddMinutes(14)
    while (-not $preview.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -ge $deadline) { throw '本机重复窗口检查超时。' }
    }
    $preview.WaitForExit()
    if ($preview.ExitCode -ne 0) { throw "本机窗口进程失败：$($preview.ExitCode)，证据：$evidence" }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $result.passed -or $result.completedCycles -ne 20 -or (-not $WritablePreview -and -not $result.installDirectoryReadOnly)) {
        throw "本机循环/只读检查未通过：$($result.error)，证据：$evidence"
    }
    if (@($result.loadedAssemblies | Where-Object { $_ -match '[\\/]\.nuget[\\/]' }).Count -ne 0) { throw '预览进程从 NuGet 缓存加载了程序集。' }
    Write-Output "Standalone 中文路径、SDK 保存恢复及 20 次真实开关通过；只读=$($result.installDirectoryReadOnly)。资源曲线需审查，不代表 Host/净机离线验收。证据：$evidence"
}
finally {
    if ($null -ne $preview -and -not $preview.HasExited) { $preview.Kill(); $preview.WaitForExit() }
    if ($null -ne $originalAcl) { Set-Acl -LiteralPath $previewRoot -AclObject $originalAcl }
    Pop-Location
}
