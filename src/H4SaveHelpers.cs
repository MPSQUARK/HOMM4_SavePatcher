using System.Text;

namespace MarkOfTigerPatcher;

internal static class H4SaveHelpers
{
    public const int ContextSearchBeforeBytes = 24;
    public const int ContextSearchAfterBytes = 64;

    public static readonly byte[] AndToken = [0x03, 0x00, 0x61, 0x6E, 0x64];
    public static readonly byte[] OrToken = [0x02, 0x00, 0x6F, 0x72];
    public static readonly byte[] SeqToken = [0x03, 0x00, 0x73, 0x65, 0x71];
    public static readonly byte[] HasHeroToken = [0x08, 0x00, 0x68, 0x61, 0x73, 0x5F, 0x68, 0x65, 0x72, 0x6F];
    public static readonly byte[] ElwinToken = [0x05, 0x00, 0x45, 0x6C, 0x77, 0x69, 0x6E];
    public static readonly byte[] IsEliminatedToken =
    [
        0x0D, 0x00, 0x69, 0x73, 0x5F, 0x65, 0x6C, 0x69, 0x6D, 0x69, 0x6E, 0x61, 0x74, 0x65, 0x64
    ];

    public static readonly string[] MapNameMarkers = ["Znak Tygrysa", "Mark of the Tiger"];

    public static int FindGzipOffset(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i <= bytes.Length - 2; i++)
        {
            if (bytes[i] == 0x1F && bytes[i + 1] == 0x8B)
                return i;
        }

        throw new InvalidDataException("No gzip header (1F 8B) found in save file.");
    }

    public static bool LooksLikeMarkOfTigerSave(ReadOnlySpan<byte> payload)
    {
        foreach (var marker in MapNameMarkers)
        {
            if (ContainsAscii(payload, marker))
                return true;
        }

        return false;
    }

    public static PatchState GetPatchState(ReadOnlySpan<byte> payload, out int patchOffset)
    {
        patchOffset = -1;

        if (!LooksLikeMarkOfTigerSave(payload))
            return PatchState.NotApplicable;

        var andMatches = FindOrcGateMatches(payload, AndToken);
        var orMatches = FindOrcGateMatches(payload, OrToken);

        if (orMatches.Count == 1 && andMatches.Count == 0)
        {
            patchOffset = orMatches[0];
            return PatchState.AlreadyPatched;
        }

        if (andMatches.Count == 1 && orMatches.Count == 0)
        {
            patchOffset = andMatches[0];
            return PatchState.NeedsPatch;
        }

        return PatchState.NotApplicable;
    }

    public static string DescribePatchContext(ReadOnlySpan<byte> payload, int patchOffset, int tokenLength)
    {
        var start = Math.Max(0, patchOffset - 12);
        var end = Math.Min(payload.Length, patchOffset + tokenLength + 16);
        return Readable(payload[start..end]);
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

    private static List<int> FindOrcGateMatches(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> token)
    {
        var matches = new List<int>();
        var searchStart = 0;

        while (searchStart <= payload.Length - token.Length)
        {
            var index = payload.Slice(searchStart).IndexOf(token);
            if (index < 0)
                break;

            var absoluteIndex = searchStart + index;
            if (IsOrcGateScriptToken(payload, absoluteIndex, token))
                matches.Add(absoluteIndex);

            searchStart = absoluteIndex + 1;
        }

        return matches;
    }

    private static bool IsOrcGateScriptToken(ReadOnlySpan<byte> payload, int tokenOffset, ReadOnlySpan<byte> token)
    {
        if (tokenOffset < 0 || tokenOffset + token.Length > payload.Length)
            return false;

        if (!payload.Slice(tokenOffset, token.Length).SequenceEqual(token))
            return false;

        var beforeStart = Math.Max(0, tokenOffset - ContextSearchBeforeBytes);
        if (!ContainsToken(payload.Slice(beforeStart, tokenOffset - beforeStart), SeqToken))
            return false;

        var afterEnd = Math.Min(payload.Length, tokenOffset + token.Length + ContextSearchAfterBytes);
        var after = payload.Slice(tokenOffset + token.Length, afterEnd - (tokenOffset + token.Length));

        return ContainsToken(after, HasHeroToken)
            && ContainsToken(after, ElwinToken)
            && ContainsToken(after, IsEliminatedToken);
    }

    private static bool ContainsToken(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        return haystack.IndexOf(needle) >= 0;
    }

    private static bool ContainsAscii(ReadOnlySpan<byte> haystack, string value)
    {
        return haystack.IndexOf(Encoding.ASCII.GetBytes(value)) >= 0;
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
