using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class SearchAliasFileTests
{
    [Fact]
    public void Load_CommittedAliasesExpandJinhsiAndChineseName()
    {
        var aliases = SearchAliasLoader.Load(RepoPaths.SearchAliases());
        var jinhsi = SearchQuery.Expand("Jinhsi", aliases);
        Assert.Contains("Jinhsi", jinhsi);
        Assert.Contains("jinxi", jinhsi, StringComparer.OrdinalIgnoreCase);

        var chinese = SearchQuery.Expand("今汐", aliases);
        Assert.Contains("今汐", chinese);
        Assert.Contains("jinxi", chinese, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("R2T1JinxiMd10011", chinese);
    }

    [Fact]
    public void Load_MissingFileReturnsEmpty()
    {
        var aliases = SearchAliasLoader.Load(Path.Combine(Path.GetTempPath(), "wuwa-missing-aliases.json"));
        Assert.Empty(aliases);
        Assert.Single(SearchQuery.Expand("Jinhsi", aliases));
    }
}
