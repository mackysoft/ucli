using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Reads source text for the <c>eval</c> command from options or redirected standard input. </summary>
internal sealed class EvalSourceInputReader : IEvalSourceInputReader
{
    private readonly Func<bool> isStandardInputRedirected;

    private readonly Func<CancellationToken, Task<string>> readStandardInputAsync;

    private readonly Func<string, bool> fileExists;

    private readonly Func<string, CancellationToken, Task<string>> readFileAsync;

    /// <summary> Initializes a new instance of the <see cref="EvalSourceInputReader" /> class. </summary>
    public EvalSourceInputReader ()
        : this(
            isStandardInputRedirected: static () => Console.IsInputRedirected,
            readStandardInputAsync: static cancellationToken => Console.In.ReadToEndAsync(cancellationToken),
            fileExists: static path => File.Exists(path),
            readFileAsync: static (path, cancellationToken) => File.ReadAllTextAsync(path, cancellationToken))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="EvalSourceInputReader" /> class for tests. </summary>
    internal EvalSourceInputReader (
        Func<bool> isStandardInputRedirected,
        Func<CancellationToken, Task<string>> readStandardInputAsync,
        Func<string, bool> fileExists,
        Func<string, CancellationToken, Task<string>> readFileAsync)
    {
        this.isStandardInputRedirected = isStandardInputRedirected ?? throw new ArgumentNullException(nameof(isStandardInputRedirected));
        this.readStandardInputAsync = readStandardInputAsync ?? throw new ArgumentNullException(nameof(readStandardInputAsync));
        this.fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        this.readFileAsync = readFileAsync ?? throw new ArgumentNullException(nameof(readFileAsync));
    }

    /// <inheritdoc />
    public async ValueTask<EvalSourceInputReadResult> ReadAsync (
        string? source,
        string? file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasSource = source != null;
        var hasFile = file != null;
        if (hasSource && hasFile)
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                "--source and --file cannot be specified together."));
        }

        if (hasSource)
        {
            return CreateSourceResult(source!, "--source");
        }

        if (hasFile)
        {
            return await ReadFileSourceAsync(file!, cancellationToken).ConfigureAwait(false);
        }

        if (!isStandardInputRedirected())
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                "Eval source was not provided. Specify --source, --file, or redirected standard input."));
        }

        return await ReadStandardInputSourceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<EvalSourceInputReadResult> ReadFileSourceAsync (
        string file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                "--file must not be empty."));
        }

        if (!fileExists(file))
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"--file does not exist: {file}."));
        }

        try
        {
            var source = await readFileAsync(file, cancellationToken).ConfigureAwait(false);
            return CreateSourceResult(source, "--file");
        }
        catch (FileNotFoundException)
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"--file does not exist: {file}."));
        }
        catch (DirectoryNotFoundException)
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"--file does not exist: {file}."));
        }
        catch (IOException exception)
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read eval source file: {file}. {exception.Message}"));
        }
        catch (UnauthorizedAccessException exception)
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read eval source file: {file}. {exception.Message}"));
        }
    }

    private async ValueTask<EvalSourceInputReadResult> ReadStandardInputSourceAsync (CancellationToken cancellationToken)
    {
        try
        {
            var source = await readStandardInputAsync(cancellationToken).ConfigureAwait(false);
            return CreateSourceResult(source, "standard input");
        }
        catch (IOException exception)
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read eval source from standard input. {exception.Message}"));
        }
    }

    private static EvalSourceInputReadResult CreateSourceResult (
        string source,
        string sourceName)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"Eval source from {sourceName} must not be empty."));
        }

        return EvalSourceInputReadResult.Success(source);
    }
}
