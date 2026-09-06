using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class SearchQueryTests
{
    private const string GoldenPackage =
        "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.uasset";

    [Fact]
    public void Expand_JinhsiAliasIncludesJinxi()
    {
        var aliases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["jinhsi"] = ["jinxi"]
        };

        var terms = SearchQuery.Expand("Jinhsi", aliases);
        Assert.Contains("Jinhsi", terms);
        Assert.Contains("jinxi", terms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CharacterGrouping_ExtractsRoleFolder()
    {
        var grouping = CharacterGrouping.FromPackagePath(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011");
        Assert.Equal("Character/Role/FemaleM/Jinxi", grouping);
    }

    [Fact]
    public void Score_GoldenMeshBeatsTextureAndMaterialInSameFolder()
    {
        var terms = new[] { "jinxi", "Jinhsi" };
        var mesh = new IndexedAsset(
            GoldenPackage,
            "R2T1JinxiMd10011",
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model",
            true);
        var texture = new IndexedAsset(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/T_R2T1JinxiMd10011Face_D.uasset",
            "T_R2T1JinxiMd10011Face_D",
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model",
            true);
        var material = new IndexedAsset(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/MI_R2T1JinxiMd10011Face.uasset",
            "MI_R2T1JinxiMd10011Face",
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model",
            true);

        Assert.True(SearchQuery.Score(mesh, terms) > SearchQuery.Score(texture, terms));
        Assert.True(SearchQuery.Score(mesh, terms) > SearchQuery.Score(material, terms));
        Assert.True(SearchQuery.Matches(GoldenPackage, SearchQuery.Expand("Jinhsi", new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["jinhsi"] = ["jinxi"]
        })));
    }

    [Fact]
    public void ExportTypeNames_PrefersSkeletalMeshOverMorphTarget()
    {
        var primary = ExportTypeNames.Primary(["MorphTarget", "SkeletalMesh", "MorphTarget"]);
        Assert.Equal("SkeletalMesh", primary);
    }
}
