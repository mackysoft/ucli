using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;

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
            openFileReader: path =>
            {
                capturedPath = path;
                return new StringReader(DirectSource);
            });

        var result = await reader.ReadAsync(source: null, "eval.cs", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("eval.cs", capturedPath);
        Assert.Equal(DirectSource, result.Source);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenFileProvidedAndStandardInputRedirected_UsesFileOnly ()
    {
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            openStandardInputReader: static () => throw new InvalidOperationException("Standard input should not be read."),
            fileExists: static _ => true,
            openFileReader: static _ => new StringReader(DirectSource));

        var result = await reader.ReadAsync(source: null, "eval.cs", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectSource, result.Source);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenNoOptionAndStandardInputRedirected_ReadsStandardInput ()
    {
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            openStandardInputReader: static () => new StringReader(DirectSource));

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
    public async Task ReadAsync_WhenSourceIsTooLarge_ReturnsInvalidArgument ()
    {
        var reader = CreateReader();
        var source = new string('a', EvalSourceInputReader.MaxSourceUtf8ByteCount + 1);

        var result = await reader.ReadAsync(source, file: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("UTF-8 bytes or less", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_WhenFilePathIsEmpty_ReturnsInvalidArgument (string file)
    {
        var reader = CreateReader();

        var result = await reader.ReadAsync(source: null, file, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("--file must not be empty", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_WhenFileSourceIsEmpty_ReturnsInvalidArgument (string source)
    {
        var reader = CreateReader(
            fileExists: static _ => true,
            openFileReader: _ => new StringReader(source));

        var result = await reader.ReadAsync(source: null, "eval.cs", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("must not be empty", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_WhenStandardInputSourceIsEmpty_ReturnsInvalidArgument (string source)
    {
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            openStandardInputReader: () => new StringReader(source));

        var result = await reader.ReadAsync(source: null, file: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("must not be empty", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenSourceProvidedAndStandardInputRedirected_UsesSourceOnly ()
    {
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            openStandardInputReader: static () => throw new InvalidOperationException("Standard input should not be read."));

        var result = await reader.ReadAsync(DirectSource, file: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectSource, result.Source);
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenFileIsTooLarge_ReturnsInvalidArgumentWithoutReadingFile ()
    {
        var reader = CreateReader(
            fileExists: static _ => true,
            getFileByteLength: static _ => EvalSourceInputReader.MaxSourceUtf8ByteCount + 1,
            openFileReader: static _ => throw new InvalidOperationException("File should not be read."));

        var result = await reader.ReadAsync(source: null, "large.cs", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("UTF-8 bytes or less", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenStandardInputIsTooLarge_ReturnsInvalidArgument ()
    {
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            openStandardInputReader: static () => new StringReader(new string('a', EvalSourceInputReader.MaxSourceUtf8ByteCount + 1)));

        var result = await reader.ReadAsync(source: null, file: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("UTF-8 bytes or less", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenStandardInputSurrogatePairCrossesBufferBoundary_CountsUtf8BytesByScalar ()
    {
        var source = CreateMaxByteSourceWithSurrogatePairAtBufferBoundary();
        var reader = CreateReader(
            isStandardInputRedirected: static () => true,
            openStandardInputReader: () => new StringReader(source));

        var result = await reader.ReadAsync(source: null, file: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(source, result.Source);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WhenFileSurrogatePairCrossesBufferBoundary_CountsUtf8BytesByScalar ()
    {
        var source = CreateMaxByteSourceWithSurrogatePairAtBufferBoundary();
        var reader = CreateReader(
            fileExists: static _ => true,
            getFileByteLength: static _ => EvalSourceInputReader.MaxSourceUtf8ByteCount,
            openFileReader: _ => new StringReader(source));

        var result = await reader.ReadAsync(source: null, "eval.cs", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(source, result.Source);
    }

    private static string CreateMaxByteSourceWithSurrogatePairAtBufferBoundary ()
    {
        var prefix = new string('a', EvalSourceInputReader.ReadBufferLength - 1);
        var emoji = char.ConvertFromUtf32(0x1F600);
        var suffix = new string(
            'a',
            EvalSourceInputReader.MaxSourceUtf8ByteCount - prefix.Length - 4);

        return prefix + emoji + suffix;
    }

    private static EvalSourceInputReader CreateReader (
        Func<bool>? isStandardInputRedirected = null,
        Func<TextReader>? openStandardInputReader = null,
        Func<string, bool>? fileExists = null,
        Func<string, long?>? getFileByteLength = null,
        Func<string, TextReader>? openFileReader = null)
    {
        return new EvalSourceInputReader(
            isStandardInputRedirected ?? (static () => false),
            openStandardInputReader ?? (static () => new StringReader(string.Empty)),
            fileExists ?? (static _ => false),
            getFileByteLength ?? (static _ => null),
            openFileReader ?? (static _ => new StringReader(string.Empty)));
    }
}
