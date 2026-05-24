using System.Text;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Input;

/// <summary> Reads source text for the <c>eval</c> command from options or redirected standard input. </summary>
internal sealed class EvalSourceInputReader : IEvalSourceInputReader
{
    internal const int MaxSourceUtf8ByteCount = 4 * 1024 * 1024;

    internal const int ReadBufferLength = 4096;

    private readonly Func<bool> isStandardInputRedirected;

    private readonly Func<TextReader> openStandardInputReader;

    private readonly Func<string, bool> fileExists;

    private readonly Func<string, long?> getFileByteLength;

    private readonly Func<string, TextReader> openFileReader;

    /// <summary> Initializes a new instance of the <see cref="EvalSourceInputReader" /> class. </summary>
    public EvalSourceInputReader ()
        : this(
            isStandardInputRedirected: static () => Console.IsInputRedirected,
            openStandardInputReader: static () => Console.In,
            fileExists: static path => File.Exists(path),
            getFileByteLength: static path => new FileInfo(path).Length,
            openFileReader: static path => new StreamReader(
                File.OpenRead(path),
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="EvalSourceInputReader" /> class for tests. </summary>
    internal EvalSourceInputReader (
        Func<bool> isStandardInputRedirected,
        Func<TextReader> openStandardInputReader,
        Func<string, bool> fileExists,
        Func<string, long?> getFileByteLength,
        Func<string, TextReader> openFileReader)
    {
        this.isStandardInputRedirected = isStandardInputRedirected ?? throw new ArgumentNullException(nameof(isStandardInputRedirected));
        this.openStandardInputReader = openStandardInputReader ?? throw new ArgumentNullException(nameof(openStandardInputReader));
        this.fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        this.getFileByteLength = getFileByteLength ?? throw new ArgumentNullException(nameof(getFileByteLength));
        this.openFileReader = openFileReader ?? throw new ArgumentNullException(nameof(openFileReader));
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

        try
        {
            if (!fileExists(file))
            {
                return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
                    $"--file does not exist: {file}."));
            }

            var fileByteLength = getFileByteLength(file);
            if (fileByteLength > MaxSourceUtf8ByteCount)
            {
                return CreateSourceTooLargeResult("--file");
            }

            using var reader = openFileReader(file);
            return await ReadBoundedSourceAsync(reader, "--file", cancellationToken).ConfigureAwait(false);
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
            return await ReadBoundedSourceAsync(openStandardInputReader(), "standard input", cancellationToken).ConfigureAwait(false);
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

        if (Encoding.UTF8.GetByteCount(source) > MaxSourceUtf8ByteCount)
        {
            return CreateSourceTooLargeResult(sourceName);
        }

        return EvalSourceInputReadResult.Success(source);
    }

    private static async ValueTask<EvalSourceInputReadResult> ReadBoundedSourceAsync (
        TextReader reader,
        string sourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var builder = new StringBuilder();
        var buffer = new char[ReadBufferLength];
        var byteCount = 0;
        char? pendingHighSurrogate = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readCount = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (readCount == 0)
            {
                if (pendingHighSurrogate.HasValue)
                {
                    byteCount += GetUtf8ByteCount(pendingHighSurrogate.Value);
                    if (byteCount > MaxSourceUtf8ByteCount)
                    {
                        return CreateSourceTooLargeResult(sourceName);
                    }
                }

                return CreateSourceResult(builder.ToString(), sourceName);
            }

            var span = buffer.AsSpan(0, readCount);
            if (pendingHighSurrogate.HasValue)
            {
                if (span.Length != 0 && char.IsLowSurrogate(span[0]))
                {
                    byteCount += GetUtf8ByteCount(pendingHighSurrogate.Value, span[0]);
                    span = span[1..];
                }
                else
                {
                    byteCount += GetUtf8ByteCount(pendingHighSurrogate.Value);
                }

                pendingHighSurrogate = null;
            }

            if (span.Length != 0 && char.IsHighSurrogate(span[^1]))
            {
                pendingHighSurrogate = span[^1];
                span = span[..^1];
            }

            byteCount += Encoding.UTF8.GetByteCount(span);
            if (byteCount > MaxSourceUtf8ByteCount)
            {
                return CreateSourceTooLargeResult(sourceName);
            }

            builder.Append(buffer, 0, readCount);
        }
    }

    private static int GetUtf8ByteCount (char value)
    {
        Span<char> chars = stackalloc char[1];
        chars[0] = value;
        return Encoding.UTF8.GetByteCount(chars);
    }

    private static int GetUtf8ByteCount (
        char highSurrogate,
        char lowSurrogate)
    {
        Span<char> chars = stackalloc char[2];
        chars[0] = highSurrogate;
        chars[1] = lowSurrogate;
        return Encoding.UTF8.GetByteCount(chars);
    }

    private static EvalSourceInputReadResult CreateSourceTooLargeResult (string sourceName)
    {
        return EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument(
            $"Eval source from {sourceName} must be {MaxSourceUtf8ByteCount} UTF-8 bytes or less."));
    }
}
