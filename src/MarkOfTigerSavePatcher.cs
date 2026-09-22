using System.IO.Compression;

namespace MarkOfTigerPatcher;

internal sealed class MarkOfTigerSavePatcher
{
    public int PatchSave(string sourcePath)
    {
        Console.WriteLine($"Selected: {Path.GetFullPath(sourcePath)}");

        try
        {
            var compressed = File.ReadAllBytes(sourcePath);
            var decompressed = Decompress(compressed);

            var state = H4SaveHelpers.GetPatchState(decompressed.Payload, out var patchOffset);
            if (state == PatchState.AlreadyPatched)
            {
                Console.WriteLine("This save is already patched. No changes were made.");
                return 0;
            }

            if (state == PatchState.NotApplicable)
            {
                Console.WriteLine("This save does not look like an unpatched Mark of the Tiger save.");
                Console.WriteLine("Expected the Orc Gate script: seq ... and ... has_hero ... Elwin ... is_eliminated.");
                Console.WriteLine("No backup or patched file was created.");
                return 1;
            }

            var backupPath = Backup(sourcePath);
            Console.WriteLine($"Backup: {backupPath}");

            var before = H4SaveHelpers.DescribePatchContext(
                decompressed.Payload,
                patchOffset,
                H4SaveHelpers.AndToken.Length);
            var patchedPayload = MarkOfTigerPatch(decompressed.Payload, patchOffset);
            var after = H4SaveHelpers.DescribePatchContext(
                patchedPayload,
                patchOffset,
                H4SaveHelpers.OrToken.Length);

            var outputPath = H4SaveHelpers.BuildPatchedOutputPath(sourcePath);
            Compress(decompressed.OriginalCompressed, patchedPayload, outputPath);

            Console.WriteLine($"Before: ...{before}...");
            Console.WriteLine($"After:  ...{after}...");
            Console.WriteLine();
            Console.WriteLine("Patch applied successfully.");
            Console.WriteLine($"Patched save: {outputPath}");
            Console.WriteLine("Load the patched save in Heroes IV to continue.");

            return 0;
        }
        catch (SavePatchException ex)
        {
            Console.WriteLine(ex.Message);
            return ex.ErrorCode == SavePatchErrorCode.AlreadyPatched ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Patch failed: {ex.Message}");
            return 1;
        }
    }

    public string Backup(string sourcePath)
    {
        var backupPath = H4SaveHelpers.BuildBackupPath(sourcePath, DateTime.Now);
        File.Copy(sourcePath, backupPath, overwrite: false);
        return backupPath;
    }

    public DecompressedSave Decompress(byte[] compressed)
    {
        var gzipOffset = H4SaveHelpers.FindGzipOffset(compressed);

        using var input = new MemoryStream(compressed, gzipOffset, compressed.Length - gzipOffset, writable: false);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);

        return new DecompressedSave(compressed, output.ToArray(), gzipOffset);
    }

    public byte[] MarkOfTigerPatch(byte[] payload, int patchOffset)
    {
        return H4SaveHelpers.GetPatchState(payload, out var resolvedOffset) switch
        {
            PatchState.AlreadyPatched => throw new SavePatchException(
                SavePatchErrorCode.AlreadyPatched,
                "This save is already patched."),
            PatchState.NotApplicable => throw new SavePatchException(
                SavePatchErrorCode.NotApplicable,
                "This save does not contain the expected Mark of the Tiger Orc Gate script token."),
            PatchState.NeedsPatch when resolvedOffset == patchOffset => ApplyPatch(payload, patchOffset),
            PatchState.NeedsPatch => throw new SavePatchException(
                SavePatchErrorCode.NotApplicable,
                "Patch location changed while processing the save."),
            _ => throw new InvalidOperationException("Unknown patch state.")
        };
    }

    public void Compress(byte[] originalCompressed, byte[] payload, string outputPath)
    {
        var gzipOffset = H4SaveHelpers.FindGzipOffset(originalCompressed);

        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        if (gzipOffset > 0)
            output.Write(originalCompressed, 0, gzipOffset);

        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(payload, 0, payload.Length);
    }

    private static byte[] ApplyPatch(byte[] data, int patchOffset)
    {
        var patched = new byte[data.Length - 1];
        Array.Copy(data, 0, patched, 0, patchOffset);
        H4SaveHelpers.OrToken.CopyTo(patched, patchOffset);
        Array.Copy(
            data,
            patchOffset + H4SaveHelpers.AndToken.Length,
            patched,
            patchOffset + H4SaveHelpers.OrToken.Length,
            data.Length - (patchOffset + H4SaveHelpers.AndToken.Length));
        return patched;
    }
}

internal readonly record struct DecompressedSave(byte[] OriginalCompressed, byte[] Payload, int GzipOffset);

internal enum SavePatchErrorCode
{
    AlreadyPatched,
    NotApplicable
}

internal sealed class SavePatchException : Exception
{
    public SavePatchException(SavePatchErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public SavePatchErrorCode ErrorCode { get; }
}
