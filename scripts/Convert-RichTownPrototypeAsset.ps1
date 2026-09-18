param(
    [string]$SourceDirectory = "$PSScriptRoot/../src/ClassicGamePlugin.RichTown.Stride/Assets/Source",
    [string]$OutputDirectory = "$PSScriptRoot/../src/ClassicGamePlugin.RichTown.Stride/Assets/Compiled"
)
$ErrorActionPreference = 'Stop'

# 只转换经过许可和摘要确认的一栋 G1 模型，不提供运行时任意模型导入能力。
# OBJ 使用右手坐标且纹理原点在左下；输出顶点将 V 翻转以匹配从 PNG 顶行读取的 RGBA。
# 文件结构为 RTM1、顶点数、每顶点八个 float32（位置、法线、UV），小端；不写时间戳，确保可复现。
$culture = [Globalization.CultureInfo]::InvariantCulture
$positions = [Collections.Generic.List[float[]]]::new()
$normals = [Collections.Generic.List[float[]]]::new()
$uvs = [Collections.Generic.List[float[]]]::new()
$vertices = [Collections.Generic.List[float]]::new()
foreach ($line in [IO.File]::ReadLines((Join-Path $SourceDirectory 'building-type-a.obj'))) {
    $parts = $line.Trim() -split '\s+'
    if ($parts[0] -in @('v','vn','vt')) {
        # 此 OBJ 的 v 行末尾还带 RGB；小镇使用作者的调色板纹理，只读取前三个位置分量。
        $componentCount = if ($parts[0] -eq 'vt') { 2 } else { 3 }
        [float[]]$values = $parts[1..$componentCount] | ForEach-Object { [float]::Parse($_, $culture) }
        switch ($parts[0]) { 'v' { $positions.Add($values) }; 'vn' { $normals.Add($values) }; 'vt' { $uvs.Add($values) } }
    }
    elseif ($parts[0] -eq 'f') {
        # 此素材的面为凸多边形；固定扇形剖分。缺失索引、负索引和越界在构建期直接失败。
        for ($i = 2; $i -lt $parts.Length - 1; $i++) {
            foreach ($corner in @($parts[1], $parts[$i], $parts[$i + 1])) {
                $index = $corner.Split('/')
                if ($index.Length -ne 3) { throw "不支持的面索引：$corner" }
                $p = [int]$index[0] - 1; $t = [int]$index[1] - 1; $n = [int]$index[2] - 1
                if ($p -lt 0 -or $p -ge $positions.Count -or $t -lt 0 -or $t -ge $uvs.Count -or $n -lt 0 -or $n -ge $normals.Count) { throw "面索引越界：$corner" }
                $vertices.AddRange($positions[$p]); $vertices.AddRange($normals[$n])
                $vertices.Add($uvs[$t][0]); $vertices.Add(1.0 - $uvs[$t][1])
            }
        }
    }
}
if ($vertices.Count -eq 0 -or $vertices.Count % 24 -ne 0) { throw '模型不是完整的三角形集合。' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $OutputDirectory 'house.mesh')))
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RTM1'))
    $writer.Write([int]($vertices.Count / 8))
    foreach ($value in $vertices) { $writer.Write([float]$value) }
} finally { $writer.Dispose() }

# 原生图像解码仅在开发机执行，玩家运行时直接上传 RGBA，不为 PNG 解码再携带额外导入库。
Add-Type -AssemblyName System.Drawing.Common
$bitmap = [Drawing.Bitmap]::new((Join-Path $SourceDirectory 'colormap.png'))
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $OutputDirectory 'palette.rgba')))
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RTR1'))
    $writer.Write([int]$bitmap.Width); $writer.Write([int]$bitmap.Height)
    for ($y=0; $y -lt $bitmap.Height; $y++) {
        for ($x=0; $x -lt $bitmap.Width; $x++) {
            $pixel=$bitmap.GetPixel($x,$y)
            $writer.Write([byte]$pixel.R); $writer.Write([byte]$pixel.G); $writer.Write([byte]$pixel.B); $writer.Write([byte]$pixel.A)
        }
    }
} finally { $writer.Dispose(); $bitmap.Dispose() }
Write-Output "小镇房屋：$($vertices.Count / 8) 个顶点；资源转换完成。"
