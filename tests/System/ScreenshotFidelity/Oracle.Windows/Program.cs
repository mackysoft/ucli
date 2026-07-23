namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class Program
{
    private const string CaptureWindowCommand = "capture-window";
    private const string AnalyzeCurrentCommand = "analyze-current";
    private const string AnalyzeVariantsCommand = "analyze-variants";
    private const string SelfCheckCommand = "self-check";

    private static int Main (string[] args)
    {
        try
        {
            ParsedArguments arguments = ParsedArguments.Parse(args);
            return arguments.Command switch
            {
                CaptureWindowCommand => CaptureWindow(arguments),
                AnalyzeCurrentCommand => AnalyzeCurrent(arguments),
                AnalyzeVariantsCommand => AnalyzeVariants(arguments),
                SelfCheckCommand => SelfCheck(arguments),
                _ => throw new OracleUsageException($"Unknown command '{arguments.Command}'."),
            };
        }
        catch (OracleUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            WriteUsage();
            return 2;
        }
        catch (OracleFailureException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected oracle failure: {exception.Message}");
            return 1;
        }
    }

    private static int CaptureWindow (ParsedArguments arguments)
    {
        arguments.EnsureOnly("--process-id", "--window-title", "--output", "--metadata");

        int processId = arguments.RequiredPositiveInt32("--process-id");
        string windowTitle = arguments.Required("--window-title");
        string outputPath = arguments.Required("--output");
        string metadataPath = arguments.Required("--metadata");
        if (string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(metadataPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new OracleUsageException("The capture output and metadata paths must be different.");
        }

        WindowCaptureOracle.Capture(processId, windowTitle, outputPath, metadataPath);
        Console.WriteLine($"Captured the target window client: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    private static int AnalyzeCurrent (ParsedArguments arguments)
    {
        arguments.EnsureOnly("--artifact", "--reference", "--confirmation-reference", "--output");

        string artifactPath = arguments.Required("--artifact");
        string referencePath = arguments.Required("--reference");
        string confirmationReferencePath = arguments.Required("--confirmation-reference");
        string outputPath = arguments.Required("--output");
        ScreenshotAnalysisOracle.Outcome outcome = ScreenshotAnalysisOracle.AnalyzeCurrent(
            artifactPath,
            referencePath,
            confirmationReferencePath);
        return WriteAnalysisOutcome(outputPath, outcome);
    }

    private static int AnalyzeVariants (ParsedArguments arguments)
    {
        arguments.EnsureOnly("--left-reference", "--right-reference", "--output");

        string leftReferencePath = arguments.Required("--left-reference");
        string rightReferencePath = arguments.Required("--right-reference");
        string outputPath = arguments.Required("--output");
        ScreenshotAnalysisOracle.Outcome outcome = ScreenshotAnalysisOracle.AnalyzeVariants(
            leftReferencePath,
            rightReferencePath);
        return WriteAnalysisOutcome(outputPath, outcome);
    }

    private static int SelfCheck (ParsedArguments arguments)
    {
        arguments.EnsureOnly("--output");

        string outputPath = arguments.Required("--output");
        ScreenshotAnalysisOracle.Outcome outcome = ScreenshotAnalysisOracle.SelfCheck();
        return WriteAnalysisOutcome(outputPath, outcome);
    }

    private static int WriteAnalysisOutcome (
        string outputPath,
        ScreenshotAnalysisOracle.Outcome outcome)
    {
        try
        {
            JsonFile.WriteAtomic(outputPath, outcome.Report);
        }
        catch (Exception exception)
        {
            throw new OracleFailureException($"Could not write the analysis report: {exception.Message}");
        }

        if (!outcome.Passed)
        {
            Console.Error.WriteLine($"Screenshot fidelity analysis failed. Report: {Path.GetFullPath(outputPath)}");
            return 1;
        }

        Console.WriteLine($"Screenshot fidelity analysis passed. Report: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    private static void WriteUsage ()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  screenshot-fidelity-oracle-windows capture-window --process-id <pid> --window-title <exact-title> --output <png> --metadata <json>");
        Console.Error.WriteLine("  screenshot-fidelity-oracle-windows analyze-current --artifact <png> --reference <png> --confirmation-reference <png> --output <json>");
        Console.Error.WriteLine("  screenshot-fidelity-oracle-windows analyze-variants --left-reference <png> --right-reference <png> --output <json>");
        Console.Error.WriteLine("  screenshot-fidelity-oracle-windows self-check --output <json>");
    }
}
