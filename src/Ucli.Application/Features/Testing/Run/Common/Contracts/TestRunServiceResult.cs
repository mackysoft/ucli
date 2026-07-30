using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Represents one normalized Test Run service outcome. </summary>
internal abstract record TestRunServiceResult
{
    protected TestRunServiceResult (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
    }

    /// <summary> Gets the user-facing execution message. </summary>
    public string Message { get; }

    /// <summary> Creates a completed Test Run with an established verdict. </summary>
    public static TestRunCompletedServiceResult Completed (
        UnityResultsConversionSuccess conversion,
        ArtifactsSession artifactsSession)
    {
        return new TestRunCompletedServiceResult(conversion, artifactsSession);
    }

    /// <summary> Creates an invalid-input command error before a Test Run exists. </summary>
    public static TestRunBeforeCreationCommandErrorServiceResult InvalidInput (
        string message,
        UcliCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        return new TestRunBeforeCreationCommandErrorServiceResult(
            ApplicationFailure.InvalidInput(
                message,
                errorCode,
                instancePath: null,
                startupFailure: null));
    }

    /// <summary> Creates an infrastructure command error before a Test Run exists. </summary>
    public static TestRunBeforeCreationCommandErrorServiceResult InfraError (
        string message,
        UcliCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        return new TestRunBeforeCreationCommandErrorServiceResult(
            ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                message,
                errorCode,
                instancePath: null,
                outcome: ApplicationOutcome.InfrastructureError,
                startupFailure: null));
    }

    /// <summary> Creates a tool command error before a Test Run exists. </summary>
    public static TestRunBeforeCreationCommandErrorServiceResult ToolError (ApplicationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Outcome != ApplicationOutcome.ToolError)
        {
            throw new ArgumentException(
                "A Test Run tool error requires a tool-error application outcome.",
                nameof(failure));
        }

        return new TestRunBeforeCreationCommandErrorServiceResult(failure);
    }

    /// <summary> Creates a command error after a Test Run artifacts session was established. </summary>
    public static TestRunAfterCreationPrimaryCommandErrorServiceResult AfterRunCreationError (
        ApplicationFailure failure,
        ArtifactsSession artifactsSession)
    {
        return new TestRunAfterCreationPrimaryCommandErrorServiceResult(
            failure,
            artifactsSession);
    }

    /// <summary>
    /// Creates a command error whose primary failure was followed by artifacts-finalization failure.
    /// </summary>
    public static TestRunAfterCreationCommandErrorWithFinalizationServiceResult
        AfterRunCreationErrorWithFinalizationFailure (
            ApplicationFailure primaryFailure,
            ApplicationFailure finalizationFailure,
            ArtifactsSession artifactsSession)
    {
        return new TestRunAfterCreationCommandErrorWithFinalizationServiceResult(
            primaryFailure,
            finalizationFailure,
            artifactsSession);
    }
}

/// <summary> Represents a completed Test Run with a verdict derived from its normalized result set. </summary>
internal sealed record TestRunCompletedServiceResult : TestRunServiceResult
{
    public TestRunCompletedServiceResult (
        UnityResultsConversionSuccess conversion,
        ArtifactsSession artifactsSession)
        : base(CreateCompletedMessage(conversion))
    {
        Conversion = conversion ?? throw new ArgumentNullException(nameof(conversion));
        ArtifactsSession = artifactsSession ?? throw new ArgumentNullException(nameof(artifactsSession));
    }

    /// <summary> Gets the verdict established from the complete normalized result set. </summary>
    public Verdict Verdict => Conversion.Verdict;

    /// <summary> Gets the completed Test Run identifier. </summary>
    public Guid RunId => ArtifactsSession.RunId;

    /// <summary> Gets the directory containing the completed Test Run artifacts. </summary>
    public AbsolutePath ArtifactsDir => ArtifactsSession.Paths.ArtifactsDir;

    /// <summary> Gets the completed summary JSON path. </summary>
    public AbsolutePath SummaryJsonPath => ArtifactsSession.Paths.SummaryJsonPath;

    private UnityResultsConversionSuccess Conversion { get; }

    private ArtifactsSession ArtifactsSession { get; }

    private static string CreateCompletedMessage (UnityResultsConversionSuccess conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        return conversion.Verdict switch
        {
            Verdict.Pass => "Unity test execution completed.",
            Verdict.Fail => "Unity test execution completed with failed tests.",
            Verdict.Incomplete =>
                "Unity test execution completed without establishing a complete test result.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(conversion),
                conversion.Verdict,
                "A completed Test Run must contain a defined verdict."),
        };
    }
}

/// <summary> Represents a command error that occurred before a Test Run was created. </summary>
internal abstract record TestRunCommandErrorServiceResult : TestRunServiceResult
{
    protected TestRunCommandErrorServiceResult (IReadOnlyList<ApplicationFailure> failures)
        : base(GetPrimaryFailure(failures).Message)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            throw new ArgumentException(
                "A Test Run command error must contain a primary failure.",
                nameof(failures));
        }

        var copiedFailures = new ApplicationFailure[failures.Count];
        for (var i = 0; i < failures.Count; i++)
        {
            copiedFailures[i] = failures[i]
                ?? throw new ArgumentException(
                    "A Test Run command error must not contain a null failure.",
                    nameof(failures));
        }

        Failures = Array.AsReadOnly(copiedFailures);
        SupplementalFailures = copiedFailures.Length == 1
            ? Array.Empty<ApplicationFailure>()
            : Array.AsReadOnly(copiedFailures[1..]);
        ErrorKind = PrimaryFailure.Outcome switch
        {
            ApplicationOutcome.InvalidArgument => TestRunErrorKind.InvalidInput,
            ApplicationOutcome.InfrastructureError => TestRunErrorKind.InfraError,
            ApplicationOutcome.ToolError => TestRunErrorKind.ToolError,
            _ => throw new ArgumentException(
                "A Test Run command error requires a non-success application outcome.",
                nameof(failures)),
        };
    }

    private static ApplicationFailure GetPrimaryFailure (IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            throw new ArgumentException(
                "A Test Run command error must contain a primary failure.",
                nameof(failures));
        }

        return failures[0]
            ?? throw new ArgumentException(
                "A Test Run command error must not contain a null primary failure.",
                nameof(failures));
    }

    /// <summary> Gets the command error payload classification. </summary>
    public TestRunErrorKind ErrorKind { get; }

    /// <summary> Gets the primary failure that determines command outcome and exit code. </summary>
    public ApplicationFailure PrimaryFailure => Failures[0];

    /// <summary> Gets the primary failure followed by any later diagnostic failures. </summary>
    public IReadOnlyList<ApplicationFailure> Failures { get; }

    /// <summary> Gets diagnostic failures observed after the primary command failure. </summary>
    public IReadOnlyList<ApplicationFailure> SupplementalFailures { get; }
}

/// <summary> Represents a command error that occurred before a Test Run artifacts session existed. </summary>
internal sealed record TestRunBeforeCreationCommandErrorServiceResult : TestRunCommandErrorServiceResult
{
    public TestRunBeforeCreationCommandErrorServiceResult (ApplicationFailure failure)
        : base([failure])
    {
    }
}

/// <summary>
/// Represents a command error after a Test Run artifacts session existed, without claiming a
/// recovered terminal Test Run state.
/// </summary>
internal abstract record TestRunAfterCreationCommandErrorServiceResult : TestRunCommandErrorServiceResult
{
    protected TestRunAfterCreationCommandErrorServiceResult (
        IReadOnlyList<ApplicationFailure> failures,
        ArtifactsSession artifactsSession)
        : base(failures)
    {
        ArtifactsSession = artifactsSession ?? throw new ArgumentNullException(nameof(artifactsSession));
    }

    /// <summary> Gets the Test Run identifier established before the command error. </summary>
    public Guid RunId => ArtifactsSession.RunId;

    /// <summary> Gets the artifacts directory established before the command error. </summary>
    public AbsolutePath ArtifactsDir => ArtifactsSession.Paths.ArtifactsDir;

    private ArtifactsSession ArtifactsSession { get; }
}

/// <summary> Represents one primary command error after a Test Run artifacts session existed. </summary>
internal sealed record TestRunAfterCreationPrimaryCommandErrorServiceResult
    : TestRunAfterCreationCommandErrorServiceResult
{
    public TestRunAfterCreationPrimaryCommandErrorServiceResult (
        ApplicationFailure primaryFailure,
        ArtifactsSession artifactsSession)
        : base([primaryFailure], artifactsSession)
    {
    }
}

/// <summary>
/// Represents a primary command error followed by a separate artifacts-finalization failure.
/// </summary>
internal sealed record TestRunAfterCreationCommandErrorWithFinalizationServiceResult
    : TestRunAfterCreationCommandErrorServiceResult
{
    public TestRunAfterCreationCommandErrorWithFinalizationServiceResult (
        ApplicationFailure primaryFailure,
        ApplicationFailure finalizationFailure,
        ArtifactsSession artifactsSession)
        : base(
            [
                primaryFailure,
                finalizationFailure,
            ],
            artifactsSession)
    {
    }

    /// <summary> Gets the later artifacts-finalization failure. </summary>
    public ApplicationFailure FinalizationFailure => Failures[1];
}
