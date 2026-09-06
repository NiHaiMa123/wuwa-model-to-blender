using Wuwa.Core;
using Wuwa.Extractor;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class AesAndArchiveTests
{
    [Fact]
    public void AesKeyDocument_ParsesMainAndDynamicKeysWithoutLeakingFormat()
    {
        const string json = """
            {
              "mainKey": "0x0000000000000000000000000000000000000000000000000000000000000001",
              "dynamicKeys": [
                { "guid": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "key": "0x0000000000000000000000000000000000000000000000000000000000000002" }
              ]
            }
            """;

        var keys = AesKeyDocument.Parse(json, "test");
        Assert.Equal(2, keys.KeyCount);
        Assert.Equal("test", keys.SourceId);
        Assert.False(string.IsNullOrWhiteSpace(keys.ContentHash));
        Assert.DoesNotContain("0000000000000001", keys.RedactedSummary(), StringComparison.Ordinal);
        Assert.Contains("dynamic=1", keys.RedactedSummary(), StringComparison.Ordinal);
    }

    [Fact]
    public void AesKeyDocument_RejectsEmptyObject()
    {
        Assert.Throws<InvalidDataException>(() => AesKeyDocument.Parse("{}", "test"));
    }

    [Fact]
    public void ArchiveDiscovery_CountsPakWithoutRequiringIoStore()
    {
        var dir = Directory.CreateTempSubdirectory("wuwa-paks-");
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "pakchunk0-WindowsNoEditor.pak"), [0]);
            File.WriteAllBytes(Path.Combine(dir.FullName, "pakchunk0-WindowsNoEditor.sig"), [0]);
            var inventory = ArchiveDiscovery.Scan(dir.FullName);
            Assert.True(inventory.HasArchives);
            Assert.Equal(1, inventory.PakCount);
            Assert.Equal(1, inventory.SigCount);
            Assert.Equal(0, inventory.UtocCount);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void DoctorResultFactory_FailWinsOverWarn()
    {
        var checks = new List<DoctorCheck>
        {
            new() { Id = "a", Status = DoctorStatus.Pass, Summary = "ok" },
            new() { Id = "b", Status = DoctorStatus.Warn, Summary = "warn" },
            new() { Id = "c", Status = DoctorStatus.Fail, Summary = "fail" }
        };
        Assert.Equal(DoctorStatus.Fail, DoctorResultFactory.Overall(checks));
    }
}
