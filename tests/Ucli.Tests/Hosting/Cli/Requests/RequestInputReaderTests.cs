using System.Security;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Tests;

public sealed class RequestInputReaderTests
{
    private static readonly (Exception Exception, ExecutionErrorKind ExpectedErrorKind)[] RequestPathReadExceptionCases =
    [
        (new ArgumentException("bad path"), ExecutionErrorKind.InvalidArgument),
        (new NotSupportedException("bad path"), ExecutionErrorKind.InvalidArgument),
        (new PathTooLongException("bad path"), ExecutionErrorKind.InvalidArgument),
        (new FileNotFoundException("missing"), ExecutionErrorKind.InvalidArgument),
        (new DirectoryNotFoundException("missing"), ExecutionErrorKind.InvalidArgument),
        (new UnauthorizedAccessException("denied"), ExecutionErrorKind.InternalError),
        (new IOException("io failure"), ExecutionErrorKind.InternalError),
        (new SecurityException("denied"), ExecutionErrorKind.InternalError),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenRequestPathAndRedirectedStandardInputAreBothProvided ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult("""{"from":"stdin"}"""),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"from":"file"}"""));

        var result = await reader.ReadAsync("request.json");

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReadsRequestPath_WhenOnlyRequestPathIsSpecified ()
    {
        const string expectedJson = """{"source":"file"}""";
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => false,
            readStandardInputAsync: static _ => Task.FromResult("""{"source":"stdin"}"""),
            readRequestFileAsync: static (_, _) => Task.FromResult(expectedJson));

        var result = await reader.ReadAsync("request.json");

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedJson, result.Json);
        Assert.Equal(RequestInputSource.RequestPath, result.Source);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReadsStandardInput_WhenRedirectedAndRequestPathIsNotSpecified ()
    {
        const string expectedJson = """{"source":"stdin"}""";
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult(expectedJson),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"source":"file"}"""));

        var result = await reader.ReadAsync(null);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedJson, result.Json);
        Assert.Equal(RequestInputSource.StandardInput, result.Source);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenInputSourceIsMissing ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => false,
            readStandardInputAsync: static _ => Task.FromResult("""{"source":"stdin"}"""),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"source":"file"}"""));

        var result = await reader.ReadAsync(null);

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenRequestJsonIsEmpty ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => false,
            readStandardInputAsync: static _ => Task.FromResult("""{"source":"stdin"}"""),
            readRequestFileAsync: static (_, _) => Task.FromResult("   "));

        var result = await reader.ReadAsync("request.json");

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenRequestJsonIsMalformed ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult("{"),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"source":"file"}"""));

        var result = await reader.ReadAsync(null);

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsExpectedErrorKind_WhenReadingRequestFileThrows ()
    {
        foreach ((Exception exception, ExecutionErrorKind expectedErrorKind) in RequestPathReadExceptionCases)
        {
            var reader = new RequestInputReader(
                isStandardInputRedirected: static () => false,
                readStandardInputAsync: static _ => Task.FromResult("""{"source":"stdin"}"""),
                readRequestFileAsync: (_, _) => Task.FromException<string>(exception));

            RequestInputReadResult result = await reader.ReadAsync("request.json");

            AssertFailure(result, expectedErrorKind);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInternalError_WhenStandardInputReadFails ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromException<string>(new IOException("stdin failure")),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"source":"file"}"""));

        var result = await reader.ReadAsync(null);

        AssertFailure(result, ExecutionErrorKind.InternalError);
    }

    private static void AssertFailure (
        RequestInputReadResult result,
        ExecutionErrorKind expectedErrorKind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Json);
        Assert.Null(result.Source);

        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(expectedErrorKind, error.Kind);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }
}