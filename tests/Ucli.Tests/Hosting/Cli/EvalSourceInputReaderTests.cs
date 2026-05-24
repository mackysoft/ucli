using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class EvalSourceInputReaderTests
{
    private const string DirectSource = "return 1;";

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenSourceProvided_ReturnsSource ()
    {
        var reader = CreateReader();

        var result = await reader.ReadAsync(DirectSource, file: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectSource, result.Source);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenSourceAndFileProvided_ReturnsInvalidArgument ()
    {
        var reader = CreateReader();

        var result = await reader.ReadAsync(DirectSource, "eval.cs", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("--source and --file", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenFileProvided_ReadsFile ()
    {
        var capturedPath = string.Empty;
        var reader = CreateReader(
            fileExists: path => string.Equals(path, "eval.cs", StringComparison.Ordinal),
            readFileAsync: (path, _) =>
            {
                capturedPath = path;
                return Task.FromResult(DirectSource);
            });

        var result = await reader.ReadAsync(source: null, "eval.cs", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("eval.cs", capturedPath);
        Assert.Equal(DirectSource, result.Source);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenNoOptionAndStandardInputRedirected_ReadsStandardInput ()
    {
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult(DirectSource));

        var result = await reader.ReadAsync(source: null, file: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectSource, result.Source);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenNoOptionAndStandardInputMissing_ReturnsInvalidArgument ()
    {
        var reader = CreateReader(isStandardInputRedirected: static () => false);

        var result = await reader.ReadAsync(source: null, file: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("Eval source was not provided", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_WhenSourceIsEmpty_ReturnsInvalidArgument (string source)
    {
        var reader = CreateReader();

        var result = await reader.ReadAsync(source, file: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("must not be empty", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenFileDoesNotExist_ReturnsInvalidArgument ()
    {
        var reader = CreateReader(fileExists: static _ => false);

        var result = await reader.ReadAsync(source: null, "missing.cs", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("--file does not exist", result.Error.Message, StringComparison.Ordinal);
    }

    private static EvalSourceInputReader CreateReader (
        Func<bool>? isStandardInputRedirected = null,
        Func<CancellationToken, Task<string>>? readStandardInputAsync = null,
        Func<string, bool>? fileExists = null,
        Func<string, CancellationToken, Task<string>>? readFileAsync = null)
    {
        return new EvalSourceInputReader(
            isStandardInputRedirected ?? (static () => false),
            readStandardInputAsync ?? (static _ => Task.FromResult(string.Empty)),
            fileExists ?? (static _ => false),
            readFileAsync ?? (static (_, _) => Task.FromResult(string.Empty)));
    }
}
