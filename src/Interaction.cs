using NativeFileDialogNET;

namespace MarkOfTigerPatcher;

internal static class Interaction
{
    public static void PrintBanner()
    {
        Console.WriteLine("Mark of Tiger Save Patcher");
        Console.WriteLine(new string('=', 40));
        Console.WriteLine();
        Console.WriteLine("""
            This tool fixes a soft-lock on the map "Znak Tygrysa" / "Mark of the Tiger"
            in Heroes of Might and Magic IV.

            If troll armies flee from battle, the Orc Tower gate script can break and
            block progress. The patch changes the Orc Gate condition so Elwin can proceed
            even when trolls are no longer present.

            A timestamped backup is always created before patching.
            The patched save is written as a new file ending in _patched.h4s.
            Your original save is never modified.
            """);
        Console.WriteLine();
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Usage:
              MarkOfTigerPatcher <path-to-save.h4s>
              MarkOfTigerPatcher
              dotnet run -- <path-to-save.h4s>

            With no path, the app opens a file picker (or lets you type a path).
            """);
    }

    public static string? PromptForSavePath()
    {
        Console.WriteLine("Opening file picker...");
        var picked = TryPickSaveFile();
        if (!string.IsNullOrWhiteSpace(picked))
            return H4SaveHelpers.NormalizePath(picked);

        Console.WriteLine("No file selected in picker. You can type a path instead.");
        return PromptForSavePathManual();
    }

    private static string? TryPickSaveFile()
    {
        try
        {
            using var dialog = new NativeFileDialog()
                .SelectFile()
                .AddFilter("Heroes IV saves", "h4s");

            string[]? paths = null;
            var result = dialog.Open(out paths, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (result == DialogResult.Okay && paths is { Length: > 0 })
                return paths[0];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Native file picker unavailable: {ex.Message}");
        }

        return null;
    }

    private static string? PromptForSavePathManual()
    {
        while (true)
        {
            Console.Write("Enter path to your .h4s save file (or press Enter to quit): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Cancelled.");
                return null;
            }

            var path = H4SaveHelpers.NormalizePath(input);
            if (File.Exists(path))
                return path;

            Console.WriteLine($"File not found: {path}");
        }
    }
}
