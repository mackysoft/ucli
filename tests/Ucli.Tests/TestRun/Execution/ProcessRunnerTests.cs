using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task RunAsync_WithInvalidExecutable_ReturnsStartFailed ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            new ProcessRunRequest(
                FileName: "__ucli_missing_executable__",
                Arguments: Array.Empty<string>(),
                TimeoutSeconds: 1),
            CancellationToken.None);

        Assert.Equal(ProcessRunStatus.StartFailed, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}