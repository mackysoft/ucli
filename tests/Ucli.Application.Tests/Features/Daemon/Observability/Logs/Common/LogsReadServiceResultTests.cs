using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Features.Daemon.Observability.Logs.Common;

public sealed class LogsReadServiceResultTests
{
    [Theory]
    [InlineData((int)LogsReadCompletionReason.Completed, "completed")]
    [InlineData((int)LogsReadCompletionReason.IdleTimeout, "idleTimeout")]
    [InlineData((int)LogsReadCompletionReason.UntilReached, "untilReached")]
    [InlineData((int)LogsReadCompletionReason.Canceled, "canceled")]
    [InlineData((int)LogsReadCompletionReason.Error, "error")]
    [Trait("Size", "Small")]
    public void CompletionReason_HasExpectedContractLiteral (
        int completionReasonValue,
        string expected)
    {
        var completionReason = (LogsReadCompletionReason)completionReasonValue;

        Assert.Equal(expected, ContractLiteralCodec.ToValue(completionReason));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCompletionReasonIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogsReadServiceResult(
            error: null,
            count: 0,
            nextCursor: null,
            completionReason: (LogsReadCompletionReason)0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCountIsNegative_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogsReadServiceResult(
            error: null,
            count: -1,
            nextCursor: null,
            completionReason: LogsReadCompletionReason.Completed));
    }

    [Theory]
    [InlineData((int)LogsReadCompletionReason.Completed)]
    [InlineData((int)LogsReadCompletionReason.IdleTimeout)]
    [InlineData((int)LogsReadCompletionReason.UntilReached)]
    [Trait("Size", "Small")]
    public void Constructor_WhenSuccessfulReasonHasError_Throws (int completionReasonValue)
    {
        var error = ExecutionError.InternalError("unexpected");
        var completionReason = (LogsReadCompletionReason)completionReasonValue;

        Assert.Throws<ArgumentException>(() => new LogsReadServiceResult(
            error,
            count: 0,
            nextCursor: null,
            completionReason));
    }

    [Theory]
    [InlineData((int)LogsReadCompletionReason.Error)]
    [InlineData((int)LogsReadCompletionReason.Canceled)]
    [Trait("Size", "Small")]
    public void Constructor_WhenFailureReasonHasNoError_Throws (int completionReasonValue)
    {
        var completionReason = (LogsReadCompletionReason)completionReasonValue;

        Assert.Throws<ArgumentException>(() => new LogsReadServiceResult(
            error: null,
            count: 0,
            nextCursor: null,
            completionReason));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCanceledErrorUsesErrorReason_Throws ()
    {
        var error = ExecutionError.InternalError("canceled", ExecutionErrorCodes.Canceled);

        Assert.Throws<ArgumentException>(() => new LogsReadServiceResult(
            error,
            count: 0,
            nextCursor: null,
            LogsReadCompletionReason.Error));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCanceledReasonUsesNonCanceledError_Throws ()
    {
        var error = ExecutionError.InternalError("unexpected");

        Assert.Throws<ArgumentException>(() => new LogsReadServiceResult(
            error,
            count: 0,
            nextCursor: null,
            LogsReadCompletionReason.Canceled));
    }
}
