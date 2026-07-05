namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

public sealed class ProcessTerminatorTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task TerminateAsync_WithForceKill_WaitsUntilProcessExits ()
    {
        using var process = TestProcessInvocations.StartLongRunningProcess();

        try
        {
            var result = await ProcessTerminator.TerminateAsync(
                process,
                new ProcessTerminationPolicy(
                    ProcessTerminationMode.ForceKill,
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromSeconds(5)),
                CancellationToken.None);

            Assert.Equal(ProcessTerminationResult.ForceKilled, result);
            Assert.True(process.HasExited);
        }
        finally
        {
            TestProcessAwaiter.TerminateBestEffort(process);
        }
    }
}
