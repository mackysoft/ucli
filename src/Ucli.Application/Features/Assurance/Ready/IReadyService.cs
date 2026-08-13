using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Executes the <c>ready</c> assurance workflow. </summary>
internal interface IReadyService
{
    /// <summary> Executes one ready workflow and returns an assurance packet. </summary>
    ValueTask<ReadyExecutionResult> ExecuteAsync (
        ReadyCommandInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observes readiness through the Program Run's already fixed host. It
    /// never resolves another daemon or oneshot process.
    /// </summary>
    ValueTask<ProgramReadyObservation> ObserveOnFixedHostAsync (
        ProjectContext context,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}

/// <summary> The closed readiness facts observed by one fixed Program host. </summary>
internal sealed record ProgramReadyObservation (
    bool IsReady,
    Verdict? Verdict,
    UnityEditorGenerationSnapshot? Generation,
    ApplicationFailure? Failure)
{
    public static ProgramReadyObservation Ready (UnityEditorGenerationSnapshot generation) =>
        new(true, MackySoft.Ucli.Contracts.Verdict.Pass, generation ?? throw new ArgumentNullException(nameof(generation)), null);

    /// <summary> Creates a contract-valid readiness verdict that does not permit execution. </summary>
    public static ProgramReadyObservation NotReady (Verdict verdict, UnityEditorGenerationSnapshot generation) =>
        verdict is MackySoft.Ucli.Contracts.Verdict.Fail or MackySoft.Ucli.Contracts.Verdict.Incomplete
            ? new(false, verdict, generation ?? throw new ArgumentNullException(nameof(generation)), null)
            : throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "A non-ready observation requires fail or incomplete verdict.");

    /// <summary> Creates an observation whose execution or contract facts could not be established. </summary>
    public static ProgramReadyObservation Failed (UnityEditorGenerationSnapshot? generation, ApplicationFailure failure) =>
        new(false, null, generation, failure ?? throw new ArgumentNullException(nameof(failure)));
}
