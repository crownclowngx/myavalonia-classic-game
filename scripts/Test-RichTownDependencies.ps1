param(
    [string]$OutputDirectory,
    [string]$PluginDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts/rich-town-tests/G5/dependencies-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
$evidence = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath (Join-Path $evidence 'dependencies.json')) { throw '依赖报告已存在，请使用新目录。' }
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

# 默认审计 Debug 预览；显式 PluginDirectory 时审计真实包解压目录。
# 两者都按锁定包源核对文件摘要，不复制 bin、不加载原生 DLL、不修改 PATH 或 NuGet 包缓存。
if (-not ('ClassicGamePlugin.LocalChecks.RichTown.NativeImports' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'RichTown.NativeImports.cs')
}
$project = Join-Path $repo 'src/ClassicGamePlugin.Plugin'
$assets = Get-Content (Join-Path $project 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
[xml]$declaration = Get-Content (Join-Path $project 'Features/RichTown/RichTown.Deployment.targets') -Raw
$privatePackages = @($declaration.Project.ItemGroup.ManagedPluginPrivatePackage.Include -split ';' | Sort-Object -Unique)
$target = $assets.targets['net10.0']
$packages = @()
$native = @()
$pinvoke = @()
$output = Join-Path $repo 'src/ClassicGamePlugin.Standalone/bin/Debug/net10.0'
if ($PluginDirectory) {
    $output = [IO.Path]::GetFullPath($PluginDirectory)
    if (-not (Test-Path -LiteralPath (Join-Path $output 'plugin.manifest.json'))) { throw '指定目录缺少插件清单。' }
}
foreach ($id in $privatePackages) {
    $key = @($target.Keys | Where-Object { $_.StartsWith($id + '/', [StringComparison]::OrdinalIgnoreCase) })
    if ($key.Count -ne 1) { throw "私有声明未在锁定图中唯一解析：$id" }
    $library = $assets.libraries[$key[0]]
    $packagePath = @($assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $library.path } | Where-Object { Test-Path -LiteralPath $_ })
    if ($packagePath.Count -ne 1) { throw "包目录不存在或不唯一：$id" }
    $resolved = $target[$key[0]]
    $runtime = @()
    if ($resolved.ContainsKey('runtime')) { $runtime = @($resolved.runtime.Keys | Where-Object { $_ -notmatch '/_\._$' }) }
    $nativePaths = @()
    if ($resolved.ContainsKey('runtimeTargets')) {
        $nativePaths = @($resolved.runtimeTargets.Keys | Where-Object { $resolved.runtimeTargets[$_].assetType -eq 'native' -and $resolved.runtimeTargets[$_].rid -eq 'win-x64' })
    }
    $files = @()
    foreach ($relative in @($runtime + $nativePaths | Sort-Object)) {
        $isNative = $nativePaths -contains $relative
        $destination = if ($isNative) { $relative } else { [IO.Path]::GetFileName($relative) }
        $source = Join-Path $packagePath[0] $relative
        $built = Join-Path $output $destination
        if (-not (Test-Path -LiteralPath $built)) { throw "被审计目录缺文件：$destination" }
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $built -Algorithm SHA256).Hash -ne $hash) { throw "输出与包源不一致：$destination" }
        $files += [ordered]@{ path = $destination; bytes = (Get-Item -LiteralPath $built).Length; sha256 = $hash }
        if ($isNative) {
            $imports = @([ClassicGamePlugin.LocalChecks.RichTown.NativeImports]::Read($built))
            $native += [ordered]@{ file = $destination; imports = $imports }
        } else {
            $imports = @([ClassicGamePlugin.LocalChecks.RichTown.NativeImports]::ReadPlatformInvokes($built))
            if ($imports.Count -gt 0) { $pinvoke += [ordered]@{ file = $destination; modules = $imports } }
        }
    }
    [xml]$nuspec = Get-Content (Join-Path $packagePath[0] ($id.ToLowerInvariant() + '.nuspec')) -Raw
    $license = $nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='license']")
    $licenseText = if ($null -eq $license) { '' } else { $license.InnerText }
    $packages += [ordered]@{ package = $key[0]; nugetContentHash = $library.sha512; license = $licenseText; files = $files }
}
$privateNames = @($native | ForEach-Object { [IO.Path]::GetFileName($_.file) })
$edges = @()
foreach ($entry in $native) {
    foreach ($import in $entry.imports) {
        $classification = if ($privateNames -contains $import) { 'plugin-private' }
        elseif ($import -match '^(api-ms-win-|ext-ms-win-)') { 'Windows API contract' }
        elseif ($import -match '^(msvcp|vcruntime|concrt|msvcr)\d') { 'VC++ runtime prerequisite' }
        elseif (Test-Path -LiteralPath (Join-Path ([Environment]::SystemDirectory) $import)) { 'present in this Windows System32; clean-machine verification pending' }
        else { 'UNRESOLVED on this machine' }
        $edges += [ordered]@{ from = [IO.Path]::GetFileName($entry.file); to = $import; classification = $classification }
    }
}
$vc = @($edges | Where-Object { $_.classification -eq 'VC++ runtime prerequisite' } | ForEach-Object { $_.to } | Sort-Object -Unique)
[ordered]@{ platform = 'win-x64'; sourceDirectory = $output; artifactKind = $(if ($PluginDirectory) { 'plugin-package' } else { 'Standalone-Debug' }); packages = $packages; native = $native; nativeEdges = $edges;
    platformInvokes = $pinvoke; vcRuntimePrerequisites = $vc;
    limitations = @('Static PE imports and DllImport metadata only; dynamic loads require runtime checks.',
        'System32 presence does not establish clean-machine or offline compatibility.',
        'Hashes verify the selected directory against locked package sources; Host loading requires separate runtime checks.',
        'License declarations are inventory metadata, not proof all redistribution notices are delivered.') } |
    ConvertTo-Json -Depth 10 | Set-Content (Join-Path $evidence 'dependencies.json') -Encoding utf8
if (@($edges | Where-Object { $_.classification -eq 'UNRESOLVED on this machine' }).Count -gt 0) {
    throw "发现无法归类/定位的原生二级依赖，查看报告：$evidence"
}
Write-Output "私有包 $($packages.Count)，原生文件 $($native.Count)，VC++ 前提：$($vc -join ', ')。依赖审计：$evidence"
