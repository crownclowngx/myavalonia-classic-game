using System.Text;

namespace ClassicGamePlugin.Features.RichTown.Rendering;

/// <summary>
/// 读取构建期转换的小镇专用网格，不接受任意 OBJ/FBX，也不依赖 GPU。
/// 在创建设备资源前完整校验长度、数量和有限坐标，避免损坏文件产生半个场景或巨大分配。
/// </summary>
internal sealed record RichTownMeshData(float[] Values)
{
    public int VertexCount => Values.Length / 8;

    public static RichTownMeshData Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RTM1") throw new InvalidDataException("房屋网格格式不受支持。");
        var count = reader.ReadInt32();
        if (count <= 0 || count > 1_000_000 || count % 3 != 0) throw new InvalidDataException("房屋顶点数量无效。");
        if (stream.Length - stream.Position != count * 32L) throw new InvalidDataException("房屋网格长度与顶点数量不一致。");
        var values = new float[count * 8];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = reader.ReadSingle();
            if (!float.IsFinite(values[i])) throw new InvalidDataException("房屋网格包含非有限坐标。");
        }
        return new RichTownMeshData(values);
    }

    public static (int Width, int Height, byte[] Pixels) ReadPalette(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RTR1") throw new InvalidDataException("房屋调色板格式不受支持。");
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        if (width <= 0 || width > 4096 || height <= 0 || height > 4096) throw new InvalidDataException("调色板尺寸无效。");
        var length = checked(width * height * 4);
        if (stream.Length - stream.Position != length) throw new InvalidDataException("调色板像素长度错误。");
        return (width, height, reader.ReadBytes(length));
    }
}
