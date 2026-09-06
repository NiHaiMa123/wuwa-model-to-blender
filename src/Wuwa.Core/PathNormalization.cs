namespace Wuwa.Core;

public static class PathNormalization
{
    public static string NormalizeLocal(string path)
        => path.Replace('\\', '/').TrimEnd('/');

    public static string ToUnrealObjectPath(string packageOrObjectPath)
    {
        var value = packageOrObjectPath.Replace('\\', '/').Trim();
        if (value.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        const string clientContent = "Client/Content/";
        if (value.StartsWith(clientContent, StringComparison.OrdinalIgnoreCase))
        {
            return "/Game/" + value[clientContent.Length..];
        }

        const string content = "Content/";
        if (value.StartsWith(content, StringComparison.OrdinalIgnoreCase))
        {
            return "/Game/" + value[content.Length..];
        }

        return value.StartsWith('/') ? value : "/" + value.TrimStart('/');
    }

    /// <summary>
    /// WuWa archives mount Unreal /Game under Client/Content. Keep this mapping in one place.
    /// </summary>
    public static string ToArchiveObjectPath(string packageOrObjectPath)
    {
        var value = NormalizeLocal(packageOrObjectPath).Trim();
        if (value.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
        {
            return "Client/Content/" + value["/Game/".Length..];
        }

        if (value.StartsWith("Game/", StringComparison.OrdinalIgnoreCase))
        {
            return "Client/Content/" + value["Game/".Length..];
        }

        return value.TrimStart('/');
    }

    public static string ResolveAgainst(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(baseDirectory, expanded));
    }
}
