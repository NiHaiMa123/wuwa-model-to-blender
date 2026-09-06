using System.Text.Json;

namespace Wuwa.Core;

public static class AesKeyDocument
{
    public static AesKeySet Parse(string json, string sourceId)
    {
        using var doc = JsonDocument.Parse(json);
        return Parse(doc.RootElement, sourceId, ContentHashing.Sha256Hex(json));
    }

    public static AesKeySet Parse(JsonElement root, string sourceId, string contentHash)
    {
        var mainKey = ReadMainKey(root);
        var dynamicKeys = ReadDynamicKeys(root);
        if (string.IsNullOrWhiteSpace(mainKey) && dynamicKeys.Count == 0)
        {
            throw new InvalidDataException(
                "AES JSON did not contain mainKey or dynamicKeys. Expected FModel-compatible keys.json.");
        }

        return new AesKeySet(sourceId, contentHash, mainKey, dynamicKeys);
    }

    private static string? ReadMainKey(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("mainKey", out var main) && main.ValueKind == JsonValueKind.String)
        {
            var value = main.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static IReadOnlyList<AesKeyEntry> ReadDynamicKeys(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("dynamicKeys", out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<AesKeyEntry>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var guid = ReadString(item, "guid") ?? ReadString(item, "Guid");
            var key = ReadString(item, "key") ?? ReadString(item, "Key");
            if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            list.Add(new AesKeyEntry(guid.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant(), key.Trim()));
        }

        return list;
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
