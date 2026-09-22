namespace MarkOfTigerPatcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args is ["--help" or "-h" or "-?"])
        {
            Interaction.PrintBanner();
            Interaction.PrintUsage();
            return 0;
        }

        string? sourcePath = null;

        if (args.Length == 1)
        {
            sourcePath = H4SaveHelpers.NormalizePath(args[0]);
            if (!File.Exists(sourcePath))
            {
                Console.Error.WriteLine($"File not found: {sourcePath}");
                return 1;
            }
        }
        else if (args.Length == 0)
        {
            Interaction.PrintBanner();
            sourcePath = Interaction.PromptForSavePath();
            if (sourcePath is null)
                return 0;
        }
        else
        {
            Console.Error.WriteLine("Too many arguments.");
            Interaction.PrintUsage();
            return 1;
        }

        return new MarkOfTigerSavePatcher().PatchSave(sourcePath);
    }
}
