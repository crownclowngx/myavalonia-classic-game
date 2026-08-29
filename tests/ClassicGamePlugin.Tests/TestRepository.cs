namespace ClassicGamePlugin.Tests;

internal static class TestRepository
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ClassicGamePlugin.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("未找到 ClassicGamePlugin 解决方案根目录。");
    }
}
