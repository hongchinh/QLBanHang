namespace OrderMgmt.IntegrationTests.Fixtures;

internal static class SourceTree
{
    private static readonly string[] SkippedFolders = ["bin", "obj", "Migrations"];

    /// backend/ — the folder that holds OrderMgmt.sln, found by walking up from the test output folder.
    public static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrderMgmt.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("OrderMgmt.sln was not found above " + AppContext.BaseDirectory);
    }

    /// Every .cs file under backend/src, without bin/, obj/ and Migrations/ folders.
    public static IEnumerable<string> SourceFiles()
    {
        var src = Path.Combine(BackendRoot(), "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(src, f)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .SkipLast(1)
                .Any(segment => SkippedFolders.Contains(segment)));
    }
}
