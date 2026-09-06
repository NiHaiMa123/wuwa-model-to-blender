using System.Security.Cryptography;
using System.Text;

namespace Wuwa.Core;

public static class ContentHashing
{
    public static string Sha256Hex(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static string Sha256Hex(string text)
        => Sha256Hex(Encoding.UTF8.GetBytes(text));

    public static string Sha256File(string path)
        => Sha256Hex(File.ReadAllBytes(path));
}
