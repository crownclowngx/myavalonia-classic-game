param(
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw '使用新的证据目录，不能覆盖旧结果。' }
$metadata = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
if ($metadata.pluginId -ne 'myavalonia.plugin.classic.game' -or $metadata.runtimeIdentifier -ne 'win-x64') { throw '插件身份或平台错误。' }
if ((Get-FileHash -LiteralPath $Package).Hash -ne $metadata.archive.sha256) { throw 'ZIP 摘要与外置清单不符。' }

# 解压前验证路径及唯一性；不能只检查解压后的文件，否则重复 ZIP 条目可能被覆盖后漏检。
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
try {
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        $path = $entry.FullName.Replace('\', '/')
        if (-not $path.StartsWith('Controls/ClassicGamePlugin/', [StringComparison]::Ordinal) -or
            $path -match '(^|/)\.\.(/|$)|:' -or -not $paths.Add($path)) { throw "ZIP 路径越界或重复：$path" }
    }
    if ($paths.Count -ne $metadata.files.Count) { throw 'ZIP 与清单文件数量不同。' }
} finally { $archive.Dispose() }
New-Item -ItemType Directory -Path $output | Out-Null
Expand-Archive -LiteralPath $Package -DestinationPath $output
foreach ($file in $metadata.files) {
    if (-not $paths.Contains($file.path)) { throw "清单包含 ZIP 不存在的文件：$($file.path)" }
    $actual = Join-Path $output $file.path
    if ((Get-Item -LiteralPath $actual).Length -ne $file.length -or
        (Get-FileHash -LiteralPath $actual).Hash -ne $file.sha256) { throw "文件与清单不符：$($file.path)" }
}
$plugin = Join-Path $output 'Controls/ClassicGamePlugin'
$manifestValue = Get-Content -LiteralPath (Join-Path $plugin 'plugin.manifest.json') -Raw | ConvertFrom-Json
if ($manifestValue.pluginVersion -ne $metadata.pluginVersion) { throw '内外版本不符。' }

# 依赖审计从锁定图追到真正交付文件，不能用预览 bin 的成功替代 ZIP 的依赖闭包。
& "$PSScriptRoot/Test-RichTownDependencies.ps1" -PluginDirectory $plugin -OutputDirectory (Join-Path $output 'dependency-audit')
$extensions = @(Get-ChildItem -LiteralPath $plugin -Filter 'Microsoft.Extensions.*.dll' -Recurse)
if ($extensions.Count -ne 1 -or $extensions[0].Name -ne 'Microsoft.Extensions.DependencyModel.dll') { throw 'Extensions 私有交付边界错误。' }
if (@(Get-ChildItem -LiteralPath $plugin -Filter '*.dll' -Recurse | Where-Object {
    $_.Name -match '^(Avalonia|Dock\.|MyAvaloniaManagement|CommunityToolkit|Semi\.|Ursa|Newtonsoft\.)'
}).Count -ne 0) { throw 'ZIP 包含宿主共享或禁带程序集。' }

# 资源映射回归：两类内容分别核对路径集合与逐文件摘要，捕获 MSBuild 非限定元数据导致的交叉映射。
$built = Join-Path $repo 'src/ClassicGamePlugin.Plugin/bin/Release/net10.0'
$assetCount = 0
foreach ($relative in @('Assets/RichTown', 'Licenses/RichTown')) {
    $source = Join-Path $built $relative
    $destination = Join-Path $plugin $relative
    $expected = @(Get-ChildItem -LiteralPath $source -Recurse -File)
    $actual = @(Get-ChildItem -LiteralPath $destination -Recurse -File)
    if ($expected.Count -eq 0 -or $expected.Count -ne $actual.Count) { throw "资源数量不符：$relative" }
    foreach ($file in $expected) {
        $target = Join-Path $destination ([IO.Path]::GetRelativePath($source, $file.FullName))
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            throw "资源路径或摘要不符：$target"
        }
    }
    $assetCount += $expected.Count
}
if (@(Get-ChildItem -LiteralPath (Join-Path $plugin 'Assets/RichTown/Shaders') -File).Count -ne 400) { throw '着色器集不完整。' }
[ordered]@{ passed = $true; pluginVersion = $metadata.pluginVersion; files = $paths.Count; assetsAndLicenses = $assetCount;
    archiveSha256 = $metadata.archive.sha256; pluginDirectory = $plugin } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'package-check.json') -Encoding utf8
Write-Output "发布包检查通过：$plugin"
