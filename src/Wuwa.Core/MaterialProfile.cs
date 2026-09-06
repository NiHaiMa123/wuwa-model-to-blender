using System.Text.Json;

namespace Wuwa.Core;

public sealed class MaterialProfile
{
    public string ProfileId { get; init; } = "";
    public string GameVersionRange { get; init; } = "";
    public string Status { get; init; } = "";
    public MaterialImportOptions Import { get; init; } = new();
    public Dictionary<string, List<string>> TextureRoles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool NormalYFlip { get; init; } = true;
    public List<SlotKindRule> SlotKinds { get; init; } = [];
    public SlotKindRule DefaultSlotKind { get; init; } = new();

    public static MaterialProfile Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Material profile not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        return Parse(json);
    }

    public static MaterialProfile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<MaterialProfile>(json, ConfigLoader.JsonOptions)
            ?? throw new InvalidDataException("Failed to parse material profile JSON.");
        if (string.IsNullOrWhiteSpace(profile.ProfileId))
        {
            throw new InvalidDataException("Material profile is missing profileId.");
        }

        return profile;
    }

    public string? RoleForParameter(string parameterName)
    {
        foreach (var (role, names) in TextureRoles)
        {
            if (names.Any(n => n.Equals(parameterName, StringComparison.OrdinalIgnoreCase)))
            {
                return role;
            }
        }

        return null;
    }

    public SlotKindRule KindForName(string materialOrSlotName)
    {
        foreach (var rule in SlotKinds)
        {
            if (rule.Match.Any(token => materialOrSlotName.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return rule;
            }
        }

        return DefaultSlotKind;
    }
}

public sealed class MaterialImportOptions
{
    public double ScaleFactor { get; init; } = 0.01;
    public int TargetLod { get; init; }
    public bool ImportMorphTargets { get; init; } = true;
    public bool ImportSockets { get; init; } = true;
    public bool ReorientBones { get; init; }
    public double BoneLength { get; init; } = 4.0;
}

public sealed class SlotKindRule
{
    public string Kind { get; init; } = "body";
    public List<string> Match { get; init; } = [];
    public string Alpha { get; init; } = "OPAQUE";
    public bool UseBaseColorAlpha { get; init; }
    public bool Skip { get; init; }
}
