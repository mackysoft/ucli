using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Infrastructure.Execution;

namespace MackySoft.Ucli.Infrastructure.Tests.Execution;

public sealed class ProcessLivenessProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CaptureCurrentProcess_ThenIsSameProcess_ReturnsTrue ()
    {
        var identity = ProcessLivenessProbe.CaptureCurrentProcess();

        Assert.True(ProcessLivenessProbe.IsSameProcess(identity));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsSameProcess_WhenGenerationDiffers_ReturnsFalse ()
    {
        var current = ProcessLivenessProbe.CaptureCurrentProcess();
        var differentGeneration = new ProcessIdentity(
            current.ProcessId,
            current.Generation == ulong.MaxValue ? current.Generation - 1 : current.Generation + 1);

        Assert.False(ProcessLivenessProbe.IsSameProcess(differentGeneration));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseLinuxProcessStartGeneration_WhenCommandContainsClosingParenthesis_ReturnsFieldTwentyTwo ()
    {
        const ulong expectedGeneration = 987654321;
        var stat = CreateLinuxProcessStat("ucli worker)", expectedGeneration.ToString());

        var parsed = ProcessLivenessProbe.TryParseLinuxProcessStartGeneration(stat, out var generation);

        Assert.True(parsed);
        Assert.Equal(expectedGeneration, generation);
    }

    [Theory]
    [InlineData("123 (ucli) S 1 2")]
    [InlineData("123 (ucli) S 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 invalid")]
    [InlineData("123 (ucli) S 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 0")]
    [Trait("Size", "Small")]
    public void TryParseLinuxProcessStartGeneration_WhenFieldTwentyTwoIsInvalid_ReturnsFalse (string stat)
    {
        Assert.False(ProcessLivenessProbe.TryParseLinuxProcessStartGeneration(stat, out _));
    }

    private static string CreateLinuxProcessStat (
        string command,
        string startGeneration)
    {
        return $"123 ({command}) S 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 {startGeneration} 999";
    }
}
