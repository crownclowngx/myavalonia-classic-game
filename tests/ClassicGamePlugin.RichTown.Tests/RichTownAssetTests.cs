using System.Text;
using ClassicGamePlugin.Features.RichTown.Rendering;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>在上传 GPU 之前验证二进制内容边界，使用明确字节而不是模型导出器作为测试 oracle。</summary>
public sealed class RichTownAssetTests
{
    [Fact]
    public void 三角形读取保持坐标法线和纹理值并且不关闭调用者流()
    {
        using var stream = Mesh(3, Enumerable.Range(0, 24).Select(i => i / 4f).ToArray());
        var mesh = RichTownMeshData.Read(stream);
        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(0, mesh.Values[0]);
        Assert.Equal(5.75f, mesh.Values[23]);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1)]
    [InlineData(1_000_002)]
    public void 无效顶点数量在分配前拒绝(int count)
    {
        using var stream = Mesh(count, []);
        Assert.Throws<InvalidDataException>(() => RichTownMeshData.Read(stream));
    }

    [Theory]
    [InlineData(23)]
    [InlineData(25)]
    public void 网格既不接受截断也不接受尾部多余数据(int floats)
    {
        using var stream = Mesh(3, new float[floats]);
        Assert.Throws<InvalidDataException>(() => RichTownMeshData.Read(stream));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void 非有限坐标不能进入显卡(float value)
    {
        var values = new float[24];
        values[17] = value;
        using var stream = Mesh(3, values);
        Assert.Throws<InvalidDataException>(() => RichTownMeshData.Read(stream));
    }

    [Fact]
    public void 调色板读取保留通道顺序()
    {
        using var stream = Palette(1, 1, [10, 20, 30, 255]);
        var image = RichTownMeshData.ReadPalette(stream);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, image.Pixels);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(4097, 1, 0)]
    [InlineData(1, 4097, 0)]
    [InlineData(1, 1, 3)]
    [InlineData(1, 1, 5)]
    public void 非法尺寸与错误像素长度均拒绝(int width, int height, int bytes)
    {
        using var stream = Palette(width, height, new byte[bytes]);
        Assert.Throws<InvalidDataException>(() => RichTownMeshData.ReadPalette(stream));
    }

    [Theory]
    [InlineData("")]
    [InlineData("RTM2")]
    [InlineData("OBJ ")]
    public void 未知文件头拒绝(string header)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(header));
        Assert.Throws<InvalidDataException>(() => RichTownMeshData.Read(stream));
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => RichTownMeshData.ReadPalette(stream));
    }

    [Theory]
    [InlineData("RTM1")]
    [InlineData("RTR1")]
    public void 截断文件头产生可诊断的读取异常(string header)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(header));
        Assert.Throws<EndOfStreamException>(() => { if (header == "RTM1") RichTownMeshData.Read(stream); else RichTownMeshData.ReadPalette(stream); });
    }

    private static MemoryStream Mesh(int count, float[] values)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RTM1"u8); writer.Write(count);
            foreach (var value in values) writer.Write(value);
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream Palette(int width, int height, byte[] pixels)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RTR1"u8); writer.Write(width); writer.Write(height); writer.Write(pixels);
        }
        stream.Position = 0;
        return stream;
    }
}
