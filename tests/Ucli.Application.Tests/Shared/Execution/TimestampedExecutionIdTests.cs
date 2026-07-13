using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution;

public sealed partial class TimestampedExecutionIdTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithTimestamp_ReturnsCanonicalUtcTimestampAndRandomHexSuffix ()
    {
        var timestamp = new DateTimeOffset(2026, 7, 11, 21, 30, 45, TimeSpan.FromHours(9));

        var executionId = TimestampedExecutionId.Create(timestamp);

        Assert.Matches(ExecutionIdPattern(), executionId);
    }

    [GeneratedRegex("^20260711_123045Z_[0-9a-f]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionIdPattern ();
}
