namespace MackySoft.Ucli.Application.Tests.Execution;

public sealed class ApplicationFailureTests
{
    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DefaultApplicationFailureValues))]
    public void Create_UsesDefaultCodeAndOutcome (
        int failureKind,
        string expectedCode,
        int expectedOutcome)
    {
        var kind = (ApplicationFailureKind)failureKind;
        var failure = ApplicationFailure.Create(kind, "Failure message.");

        Assert.Equal(kind, failure.Kind);
        Assert.Equal(expectedCode, failure.Code.Value);
        Assert.Equal((ApplicationOutcome)expectedOutcome, failure.Outcome);
        Assert.Equal("Failure message.", failure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromCode_WithUnknownCode_PreservesCodeAndOpId ()
    {
        var futureCode = new UcliErrorCode("FUTURE_TRANSPORT_FAILURE");

        var failure = ApplicationFailure.FromCode(futureCode, "Future transport failed.", "step-1");

        Assert.Equal(ApplicationFailureKind.ContractViolation, failure.Kind);
        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
        Assert.Equal(futureCode, failure.Code);
        Assert.Equal("Future transport failed.", failure.Message);
        Assert.Equal("step-1", failure.OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCodeIsMissing_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ApplicationFailure(
            ApplicationFailureKind.InternalError,
            ApplicationOutcome.ToolError,
            default,
            "Failure message."));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenKindIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationFailure.Create(
            (ApplicationFailureKind)999,
            "Failure message."));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenMessageIsMissing_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => ApplicationFailure.InternalError(""));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenOutcomeIsSuccess_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ApplicationFailure(
            ApplicationFailureKind.InternalError,
            ApplicationOutcome.Success,
            UcliCoreErrorCodes.InternalError,
            "Failure message."));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithToolFailureKindAndInvalidArgumentCode_UsesKindOutcome ()
    {
        var failure = ApplicationFailure.Create(
            ApplicationFailureKind.ContractViolation,
            "Invalid contract.",
            UcliCoreErrorCodes.InvalidArgument);

        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, failure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenExplicitOutcomeDoesNotMatchKind_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => ApplicationFailure.Create(
            ApplicationFailureKind.InvalidInput,
            "Invalid input.",
            UcliCoreErrorCodes.InvalidArgument,
            outcome: ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => ApplicationFailure.Create(
            ApplicationFailureKind.ExternalProcessFailure,
            "External process failed.",
            outcome: ApplicationOutcome.TestFailure));
    }

    public static TheoryData<int, string, int> DefaultApplicationFailureValues ()
    {
        return new TheoryData<int, string, int>
        {
            { (int)ApplicationFailureKind.InvalidInput, UcliCoreErrorCodes.InvalidArgument.Value, (int)ApplicationOutcome.InvalidArgument },
            { (int)ApplicationFailureKind.ConfigurationError, UcliCoreErrorCodes.InvalidArgument.Value, (int)ApplicationOutcome.InvalidArgument },
            { (int)ApplicationFailureKind.EnvironmentError, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.UnityIpcFailure, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.ExternalProcessFailure, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.ContractViolation, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.Timeout, ExecutionErrorCodes.IpcTimeout.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.Canceled, ExecutionErrorCodes.Canceled.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.InternalError, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
        };
    }
}
