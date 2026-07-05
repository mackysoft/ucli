namespace MackySoft.Ucli.Application.Tests.Execution;

public sealed class ApplicationFailureTests
{
    private static readonly DefaultApplicationFailureCase[] DefaultApplicationFailureCases =
    [
        new(ApplicationFailureKind.InvalidInput, UcliCoreErrorCodes.InvalidArgument, ApplicationOutcome.InvalidArgument),
        new(ApplicationFailureKind.ConfigurationError, UcliCoreErrorCodes.InvalidArgument, ApplicationOutcome.InvalidArgument),
        new(ApplicationFailureKind.EnvironmentError, UcliCoreErrorCodes.InternalError, ApplicationOutcome.ToolError),
        new(ApplicationFailureKind.UnityIpcFailure, UcliCoreErrorCodes.InternalError, ApplicationOutcome.ToolError),
        new(ApplicationFailureKind.ExternalProcessFailure, UcliCoreErrorCodes.InternalError, ApplicationOutcome.ToolError),
        new(ApplicationFailureKind.ContractViolation, UcliCoreErrorCodes.InternalError, ApplicationOutcome.ToolError),
        new(ApplicationFailureKind.Timeout, ExecutionErrorCodes.IpcTimeout, ApplicationOutcome.ToolError),
        new(ApplicationFailureKind.Canceled, ExecutionErrorCodes.Canceled, ApplicationOutcome.ToolError),
        new(ApplicationFailureKind.InternalError, UcliCoreErrorCodes.InternalError, ApplicationOutcome.ToolError),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Create_UsesDefaultCodeAndOutcome ()
    {
        foreach (var testCase in DefaultApplicationFailureCases)
        {
            var failure = ApplicationFailure.Create(testCase.Kind, "Failure message.");

            Assert.Equal(testCase.Kind, failure.Kind);
            Assert.Equal(testCase.ExpectedCode, failure.Code);
            Assert.Equal(testCase.ExpectedOutcome, failure.Outcome);
            Assert.Equal("Failure message.", failure.Message);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromCode_WithUnknownCode_PreservesCodeAndOpId ()
    {
        var futureCode = new UcliCode("FUTURE_TRANSPORT_FAILURE");

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

    private sealed record DefaultApplicationFailureCase (
        ApplicationFailureKind Kind,
        UcliCode ExpectedCode,
        ApplicationOutcome ExpectedOutcome);
}
