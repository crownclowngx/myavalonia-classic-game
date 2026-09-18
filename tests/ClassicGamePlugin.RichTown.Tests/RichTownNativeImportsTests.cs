using System.Text;
using ClassicGamePlugin.LocalChecks.RichTown;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownNativeImportsTests
{
    [Fact]
    public void 普通与延迟导入去重且不需要执行原生代码()
    {
        var bytes = Fixture();
        Check(bytes, names => Assert.Equal(new[] { "KERNEL32.dll", "VCRUNTIME140.dll" }, names));
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("rva")]
    [InlineData("terminator")]
    [InlineData("name")]
    [InlineData("empty")]
    [InlineData("long")]
    [InlineData("attributes")]
    [InlineData("machine")]
    [InlineData("directory-size")]
    public void 坏导入目录不能被当成无依赖(string failure)
    {
        var bytes = Fixture();
        switch (failure)
        {
            case "truncated": bytes = bytes[..580]; break;
            case "rva": Put(bytes, 512 + 12, 0x9000); break;
            case "terminator": Put(bytes, 0x98 + 112 + 8 + 4, 20); break;
            case "name": bytes[768] = (byte)'/'; break;
            case "empty": bytes[768] = 0; break;
            case "long": Array.Fill(bytes, (byte)'a', 768, 260); break;
            case "attributes": Put(bytes, 640, 2); break;
            case "machine": Put16(bytes, 0x84, 0x14c); break;
            case "directory-size": Put(bytes, 0x98 + 112 + 12, 1); break;
        }
        var path = Path.GetTempFileName();
        try { File.WriteAllBytes(path, bytes); Assert.Throws<InvalidDataException>(() => NativeImports.Read(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void 没有导入表时返回空集合()
    {
        var bytes = Fixture();
        Array.Clear(bytes, 0x98 + 112, 16 * 8);
        Check(bytes, names => Assert.Empty(names));
    }

    [Fact]
    public void 托管元数据提取真实DllImport而不加载目标库()
    {
        Assert.Contains("rich-town-test-never-load.dll", NativeImports.ReadPlatformInvokes(typeof(RichTownNativeImportsTests).Assembly.Location));
        var path = Path.GetTempFileName();
        try { File.WriteAllBytes(path, Fixture()); Assert.Empty(NativeImports.ReadPlatformInvokes(path)); }
        finally { File.Delete(path); }
    }
    [System.Runtime.InteropServices.DllImport("rich-town-test-never-load.dll")]
    private static extern void AuditMarker();

    // 构造只有一个 .rdata 节的 PE32+ 文件，显式写入导入 RVA，以免测试依赖开发机 DLL 版本。
    private static byte[] Fixture()
    {
        var bytes = new byte[1536];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z'; Put(bytes, 0x3C, 0x80);
        bytes[0x80] = (byte)'P'; bytes[0x81] = (byte)'E';
        Put16(bytes, 0x84, 0x8664); Put16(bytes, 0x86, 1); Put16(bytes, 0x94, 240);
        const int optional = 0x98;
        Put16(bytes, optional, 0x20b); Put(bytes, optional + 32, 4096); Put(bytes, optional + 36, 512);
        Put(bytes, optional + 56, 8192); Put(bytes, optional + 60, 512); Put(bytes, optional + 108, 16);
        Put(bytes, optional + 112 + 8, 0x1000); Put(bytes, optional + 112 + 12, 60);
        Put(bytes, optional + 112 + 13 * 8, 0x1080); Put(bytes, optional + 112 + 13 * 8 + 4, 64);
        const int section = optional + 240;
        Encoding.ASCII.GetBytes(".rdata").CopyTo(bytes, section);
        Put(bytes, section + 8, 1024); Put(bytes, section + 12, 0x1000);
        Put(bytes, section + 16, 1024); Put(bytes, section + 20, 512);
        Put(bytes, 512 + 12, 0x1100); Put(bytes, 532 + 12, 0x1100); Put(bytes, 640, 1); Put(bytes, 644, 0x1120);
        Encoding.ASCII.GetBytes("KERNEL32.dll").CopyTo(bytes, 768);
        Encoding.ASCII.GetBytes("VCRUNTIME140.dll").CopyTo(bytes, 800);
        return bytes;
    }
    private static void Put(byte[] bytes, int at, uint value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
    private static void Put16(byte[] bytes, int at, ushort value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
    private static void Check(byte[] bytes, Action<string[]> assertion)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllBytes(path, bytes); assertion(NativeImports.Read(path)); }
        finally { File.Delete(path); }
    }
}
