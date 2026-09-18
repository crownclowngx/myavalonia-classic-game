using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace ClassicGamePlugin.LocalChecks.RichTown;

/// <summary>
/// 仅供本地开发审计的 PE 导入读取器，不进入插件。使用 BCL 读取普通/延迟导入，不加载 DLL、不执行入口。
/// RVA 必须映射到实际文件字节；损坏目录、无终止符和截断文件均失败，不能把无法解析误报成没有依赖。
/// 导入表无法发现 GetProcAddress/LoadLibrary 动态名称，报告必须同时列出这一边界。
/// </summary>
public static class NativeImports
{
    /// <summary>只读取托管元数据中的 DllImport 模块名；由字符串构造的动态加载仍需实际环境验证。</summary>
    public static string[] ReadPlatformInvokes(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) return Array.Empty<string>();
        var metadata = pe.GetMetadataReader();
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
            var import = method.GetImport();
            names.Add(metadata.GetString(metadata.GetModuleReference(import.Module).Name));
        }
        var output = new string[names.Count];
        names.CopyTo(output);
        return output;
    }

    public static string[] Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var headers = reader.PEHeaders;
        var pe = headers.PEHeader ?? throw new InvalidDataException("文件没有 PE 头。");
        if (headers.CoffHeader.Machine != Machine.Amd64) throw new InvalidDataException("小镇当前只审计 win-x64 原生文件。");
        var bytes = File.ReadAllBytes(path);
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int Offset(long rva, int length)
        {
            foreach (var section in headers.SectionHeaders)
            {
                var relative = rva - section.VirtualAddress;
                if (relative < 0 || relative + length > section.SizeOfRawData) continue;
                var offset = (long)section.PointerToRawData + relative;
                if (offset < 0 || offset + length > bytes.Length) break;
                return checked((int)offset);
            }
            throw new InvalidDataException("PE 导入 RVA 超出文件节。");
        }
        string Name(long rva)
        {
            var result = new StringBuilder();
            for (var i = 0; i < 260; i++)
            {
                var value = bytes[Offset(rva + i, 1)];
                if (value == 0)
                {
                    if (result.Length == 0) throw new InvalidDataException("PE 导入 DLL 名称为空。");
                    return result.ToString();
                }
                if (value < 32 || value > 126 || value == '/' || value == '\\')
                    throw new InvalidDataException("PE 导入 DLL 名称不是文件名。");
                result.Append((char)value);
            }
            throw new InvalidDataException("PE 导入 DLL 名称未终止。");
        }
        void ReadDirectory(DirectoryEntry directory, bool delay)
        {
            if (directory.RelativeVirtualAddress == 0 && directory.Size == 0) return;
            var size = delay ? 32 : 20;
            if (directory.RelativeVirtualAddress <= 0 || directory.Size < size)
                throw new InvalidDataException("PE 导入目录长度无效。");
            for (var position = 0; position <= directory.Size - size; position += size)
            {
                var offset = Offset((long)directory.RelativeVirtualAddress + position, size);
                var empty = true;
                for (var i = 0; i < size; i++) empty &= bytes[offset + i] == 0;
                if (empty) return;
                var address = (long)BitConverter.ToUInt32(bytes, offset + (delay ? 4 : 12));
                if (delay)
                {
                    var attributes = BitConverter.ToUInt32(bytes, offset);
                    if (attributes > 1) throw new InvalidDataException("PE 延迟导入属性未知。");
                    if (attributes == 0) address -= checked((long)pe.ImageBase);
                }
                names.Add(Name(address));
            }
            throw new InvalidDataException("PE 导入目录缺少终止项。");
        }
        ReadDirectory(pe.ImportTableDirectory, false);
        ReadDirectory(pe.DelayImportTableDirectory, true);
        var output = new string[names.Count];
        names.CopyTo(output);
        return output;
    }
}
