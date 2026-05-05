using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Parsing;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;
using MackySoft.Ucli.Hosting.Composition.Common;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Runs the public CLI command pipeline and preserves the JSON result contract. </summary>
internal sealed class CliExecutionRunner
{
    private const string InternalErrorMessage = "An unexpected internal error occurred.";

    private const string CanceledMessage = "Command execution was canceled.";

    /// <summary> Executes one public CLI invocation. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the hosting environment. </param>
    /// <returns> The process exit code determined by command execution. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public async Task<int> RunAsync (
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        CliParseErrorJsonPolicy.BeginCapture();

        var preDispatchErrorResult = CliPreDispatchErrorPolicy.TryCreateErrorResult(args);
        if (preDispatchErrorResult != null)
        {
            CommandResultWriter.WriteToStandardOutput(preDispatchErrorResult);
            Environment.ExitCode = preDispatchErrorResult.ExitCode;
            return Environment.ExitCode;
        }

        var app = UcliCommandCatalog.RegisterCommands(
            ConsoleApp.Create()
                .ConfigureServices(static services => services.AddUcliServices()));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await app.RunAsync(args).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var canceledResult = CommandResult.Canceled(UcliCommandNames.Root, CanceledMessage);
            CommandResultWriter.WriteToStandardOutput(canceledResult);
            Environment.ExitCode = canceledResult.ExitCode;
            return Environment.ExitCode;
        }
        catch (Exception)
        {
            var internalErrorResult = CommandResult.InternalError(UcliCommandNames.Root, InternalErrorMessage);
            CommandResultWriter.WriteToStandardOutput(internalErrorResult);
            Environment.ExitCode = internalErrorResult.ExitCode;
            return Environment.ExitCode;
        }

        // NOTE:
        // ConsoleAppFramework can fail before command handlers start when parsing options.
        // Emit JSON contract output in that path to keep stdout machine-readable.
        var parseErrorResult = CliParseErrorJsonPolicy.TryCreateParseErrorResult(args);
        if (parseErrorResult != null)
        {
            CommandResultWriter.WriteToStandardOutput(parseErrorResult);
            Environment.ExitCode = parseErrorResult.ExitCode;
        }

        return Environment.ExitCode;
    }
}
