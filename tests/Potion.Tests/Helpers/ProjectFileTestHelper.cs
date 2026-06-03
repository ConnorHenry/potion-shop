using System;
using System.IO;

internal static class ProjectFileTestHelper
{
    public static string ReadProjectFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath);
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing project file: {relativePath}");

        return File.ReadAllText(path);
    }
}
