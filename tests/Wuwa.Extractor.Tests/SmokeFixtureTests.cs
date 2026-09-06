using System.Text;
using System.Text.Json;
using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class SmokeFixtureTests
{
    [Fact]
    public void Writer_EmitsUeFormatV10Header()
    {
        var bytes = SyntheticUeModelWriter.Write();
        Assert.True(bytes.Length > 64);
        Assert.Equal("UEFORMAT", Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Equal(7, BitConverter.ToInt32(bytes, 8));
        Assert.Equal("UEMODEL", Encoding.ASCII.GetString(bytes, 12, 7));
        Assert.Equal(SyntheticUeModelWriter.FileVersion, bytes[19]);
        Assert.True(ContainsAscii(bytes, "LODS"));
        Assert.True(ContainsAscii(bytes, "SKELETON"));
        Assert.True(ContainsAscii(bytes, "MI_SmokeHair"));
        Assert.False(ContainsAscii(bytes, "R2T1Jinxi"));
    }

    [Fact]
    public void Writer_IsDeterministic()
    {
        Assert.Equal(SyntheticUeModelWriter.Write(), SyntheticUeModelWriter.Write());
    }

    [Fact]
    public void CommittedFixture_ExistsAndMatchesWriter()
    {
        var dest = RepoPaths.SmokeFixtureDir();
        if (TestGates.FlagEnabled("WUWA_WRITE_SMOKE_FIXTURE"))
        {
            SmokeFixture.Write(dest);
        }

        Assert.True(Directory.Exists(dest), $"Missing {dest}. Run tests with WUWA_WRITE_SMOKE_FIXTURE=1 once to emit the self-authored fixture.");
        var generated = Directory.CreateTempSubdirectory("wuwa-smoke-");
        try
        {
            SmokeFixture.Write(generated.FullName);
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(generated.FullName, SmokeFixture.UeModelFile)),
                File.ReadAllBytes(Path.Combine(dest, SmokeFixture.UeModelFile)));
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(generated.FullName, SmokeFixture.DiffuseFile)),
                File.ReadAllBytes(Path.Combine(dest, SmokeFixture.DiffuseFile)));
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(generated.FullName, SmokeFixture.NormalFile)),
                File.ReadAllBytes(Path.Combine(dest, SmokeFixture.NormalFile)));
        }
        finally
        {
            generated.Delete(recursive: true);
        }

        var manifestJson = File.ReadAllText(Path.Combine(dest, SmokeFixture.ManifestFile));
        var manifest = JsonSerializer.Deserialize<ExportManifest>(manifestJson, ConfigLoader.JsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal(SmokeFixture.JobId, manifest.JobId);
        Assert.Equal(SyntheticUeModelWriter.ObjectPath, manifest.SourceObjectPath);
        Assert.Equal(SyntheticUeModelWriter.VertexCount, manifest.Mesh!.Lods[0].Vertices);
        Assert.Equal(2, manifest.Textures.Count);
        Assert.Equal(2, manifest.Materials.Count);
        Assert.Contains("schemaVersion", manifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("0x", manifestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jinxi", manifestJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(dest, manifest.Mesh.UeModel!)));
        foreach (var texture in manifest.Textures)
        {
            Assert.True(File.Exists(Path.Combine(dest, texture.File!.Replace('/', Path.DirectorySeparatorChar))));
        }
    }

    [Fact]
    public void TinyPng_IsValidSignature()
    {
        var png = TinyPng.SolidRgb(220, 40, 40);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.True(png.Length > 40);
    }

    private static bool ContainsAscii(byte[] haystack, string ascii)
        => haystack.AsSpan().IndexOf(Encoding.ASCII.GetBytes(ascii)) >= 0;
}
