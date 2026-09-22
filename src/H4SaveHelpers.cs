using System.Text;

namespace MarkOfTigerPatcher;

internal static class H4SaveHelpers
{
    public const int PatchOffset = 0xB6D03;

    public static readonly byte[] AndToken = [0x03, 0x00, 0x61, 0x6E, 0x64];
    public static readonly byte[] OrToken = [0x02, 0x00, 0x6F, 0x72];

    public static int FindGzipOffset(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i <= bytes.Length - 2; i++)
        {
            if (bytes[i] == 0x1F && bytes[i + 1] == 0x8B)
                return i;
        }

        throw new InvalidDataException("No gzip header (1F 8B) found in save file.");
    }

    public static PatchState GetPatchState(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < PatchOffset + AndToken.Length)
            return PatchState.NotApplicable;

        if (payload.Slice(PatchOffset, OrToken.Length).SequenceEqual(OrToken))
            return PatchState.AlreadyPatched;

        if (payload.Slice(PatchOffset, AndToken.Length).SequenceEqual(AndToken))
            return PatchState.NeedsPatch;

        return PatchState.NotApplicable;
    }

    public static string DescribePatchContext(ReadOnlySpan<byte> payload, int tokenLength)
    {
        var start = Math.Max(0, PatchOffset - 12);
        var end = Math.Min(payload.Length, PatchOffset + tokenLength + 16);
        return Readable(payload.Slice(start, end - start));
    }

    public static string BuildPatchedOutputPath(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(directory, $"{stem}_patched.h4s");
    }

    public static string BuildBackupPath(string sourcePath, DateTime timestamp)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(directory, $"{stem}_{timestamp:yyyyMMdd_HHmmss}.h4s.bak");
    }

    public static string NormalizePath(string path)
    {
        path = path.Trim().Trim('"');
        return Path.GetFullPath(path);
    }

    private static string Readable(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
            sb.Append(b is >= 32 and <= 126 ? (char)b : '.');
        return sb.ToString();
    }
}

internal enum PatchState
{
    NeedsPatch,
    AlreadyPatched,
    NotApplicable
}
