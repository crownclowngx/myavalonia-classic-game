$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $repo ('artifacts/rich-town-tests/G5/deploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $evidence | Out-Null
$project = Join-Path $repo 'src/ClassicGamePlugin.Plugin/ClassicGamePlugin.Plugin.csproj'
[xml]$definition = Get-Content -LiteralPath $project -Raw
$directoryName = $definition.SelectSingleNode('//ManagedPluginDirectoryName').InnerText
if ($directoryName -notmatch '^[A-Za-z0-9_.-]+$' -or $directoryName -in @('.', '..')) { throw '部署子目录必须是简单相对名称。' }
$root = [IO.Path]::GetFullPath((Join-Path $evidence 'staging'))
$target = [IO.Path]::GetFullPath((Join-Path $root $directoryName))
if (-not $root.StartsWith($evidence + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '部署路径越界。' }

# Build target 可能先清空其部署目录，所以在调用前核对最终绝对路径。只对本次 artifacts 子目录操作。
# 不覆盖日常 Host，不调用打包目标、不放宽协议。失败仍写报告并抛错，不能作为默认门禁的“预期成功”。
Push-Location $repo
try {
    & dotnet build $project -c Debug --no-restore -warnaserror -p:SkipPluginDeploy=false "-p:ManagedPluginDeployRoot=$root" 2>&1 |
        Tee-Object -FilePath (Join-Path $evidence 'deploy.log')
    $code = $LASTEXITCODE
    [ordered]@{ exitCode = $code; configuration = 'Debug'; deployRoot = $root; deployDirectory = $target;
        directoryExists = (Test-Path -LiteralPath $target); release = 'not executed' } |
        ConvertTo-Json | Set-Content (Join-Path $evidence 'deploy-result.json') -Encoding utf8
    if ($code -ne 0) { throw "隔离 Debug 部署失败（$code），证据：$evidence" }
    Write-Output "隔离 Debug 部署生成目录：$target；尚需真实 Host 与依赖闭包验收。"
}
finally { Pop-Location }
