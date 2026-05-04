using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Tests;

public sealed class RequestInputReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReadsStandardInput_WhenRedirected ()
    {
        const string expectedJson = """{"source":"stdin"}""";
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult(expectedJson));

        var result = await reader.ReadAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedJson, result.Json);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenStandardInputIsMissing ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => false,
            readStandardInputAsync: static _ => Task.FromResult("""{"source":"stdin"}"""));

        var result = await reader.ReadAsync();

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenRequestJsonIsEmpty ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult("   "));

        var result = await reader.ReadAsync();

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInvalidArgument_WhenRequestJsonIsMalformed ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult("{"));

        var result = await reader.ReadAsync();

        AssertFailure(result, ExecutionErrorKind.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReturnsInternalError_WhenStandardInputReadFails ()
    {
        var reader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromException<string>(new IOException("stdin failure")));

        var result = await reader.ReadAsync();

        AssertFailure(result, ExecutionErrorKind.InternalError);
    }

    private static void AssertFailure (
        RequestInputReadResult result,
        ExecutionErrorKind expectedErrorKind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Json);

        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(expectedErrorKind, error.Kind);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }
}
