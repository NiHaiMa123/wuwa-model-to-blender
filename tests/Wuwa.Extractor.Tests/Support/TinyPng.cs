using System.IO.Compression;

namespace Wuwa.Extractor.Tests;

internal static class TinyPng
{
    public static byte[] SolidRgb(byte r, byte g, byte b)
    {
        var scanline = new byte[] { 0, r, g, b };
        var compressed = ZlibCompress(scanline);
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR"u8, [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        WriteChunk(ms, "IDAT"u8, compressed);
        WriteChunk(ms, "IEND"u8, []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        length[0] = (byte)(data.Length >> 24);
        length[1] = (byte)(data.Length >> 16);
        length[2] = (byte)(data.Length >> 8);
        length[3] = (byte)data.Length;
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        var crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        crcBytes[0] = (byte)(crc >> 24);
        crcBytes[1] = (byte)(crc >> 16);
        crcBytes[2] = (byte)(crc >> 8);
        crcBytes[3] = (byte)crc;
        stream.Write(crcBytes);
    }

    private static byte[] ZlibCompress(byte[] raw)
    {
        using var deflateOut = new MemoryStream();
        using (var ds = new DeflateStream(deflateOut, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            ds.Write(raw);
        }

        var deflated = deflateOut.ToArray();
        var adler = Adler32(raw);
        var result = new byte[2 + deflated.Length + 4];
        result[0] = 0x78;
        result[1] = 0x01;
        Buffer.BlockCopy(deflated, 0, result, 2, deflated.Length);
        result[^4] = (byte)(adler >> 24);
        result[^3] = (byte)(adler >> 16);
        result[^2] = (byte)(adler >> 8);
        result[^1] = (byte)adler;
        return result;
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }
}
