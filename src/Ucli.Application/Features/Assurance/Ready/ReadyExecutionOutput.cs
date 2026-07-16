using System.Collections.ObjectModel;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents the ready assurance payload emitted by the <c>ready</c> command. </summary>
internal sealed record ReadyExecutionOutput
{
    /// <summary> Initializes a ready assurance payload with a defined verdict. </summary>
    /// <param name="Reports"> The report map to copy with ordinal key semantics. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Verdict" /> or <paramref name="SessionKind" /> is not defined by the assurance contract. </exception>
    public ReadyExecutionOutput (
        AssuranceVerdict Verdict,
        ProjectIdentityInfo Project,
        IReadOnlyList<ReadyVerifierOutput> Verifiers,
        IReadOnlyList<ReadyClaimOutput> Claims,
        IReadOnlyDictionary<string, AssuranceReportReference> Reports,
        IReadOnlyList<ReadyResidualRiskOutput> ResidualRisks,
        ReadyTarget Target,
        AssuranceRequestedExecutionMode RequestedMode,
        AssuranceResolvedExecutionMode ResolvedMode,
        AssuranceSessionKind SessionKind,
        int TimeoutMilliseconds,
        ReadyLifecycleOutput? Lifecycle,
        ReadyReadIndexOutput? ReadIndex)
    {
        if (!ContractLiteralCodec.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }
        if (!ContractLiteralCodec.IsDefined(SessionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(SessionKind), SessionKind, "Session kind must be defined by the assurance contract.");
        }
        if (!ContractLiteralCodec.IsDefined(Target))
        {
            throw new ArgumentOutOfRangeException(nameof(Target), Target, "Ready target must be defined by the assurance contract.");
        }
        if (!ContractLiteralCodec.IsDefined(RequestedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(RequestedMode), RequestedMode, "Requested execution mode must be defined by the assurance contract.");
        }
        if (!ContractLiteralCodec.IsDefined(ResolvedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(ResolvedMode), ResolvedMode, "Resolved execution mode must be defined by the assurance contract.");
        }
        ArgumentNullException.ThrowIfNull(Reports);
        ArgumentNullException.ThrowIfNull(Project);
        ArgumentNullException.ThrowIfNull(Verifiers);
        ArgumentNullException.ThrowIfNull(Claims);
        ArgumentNullException.ThrowIfNull(ResidualRisks);
        if (Verifiers.Any(static item => item is null))
        {
            throw new ArgumentException("Verifiers must not contain null.", nameof(Verifiers));
        }

        if (Claims.Any(static item => item is null))
        {
            throw new ArgumentException("Claims must not contain null.", nameof(Claims));
        }

        if (Reports.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
        {
            throw new ArgumentException("Reports must contain non-empty keys and non-null references.", nameof(Reports));
        }

        if (ResidualRisks.Any(static item => item is null))
        {
            throw new ArgumentException("Residual risks must not contain null.", nameof(ResidualRisks));
        }

        this.Verdict = Verdict;
        this.Project = Project;
        this.Verifiers = Array.AsReadOnly(Verifiers.ToArray());
        this.Claims = Array.AsReadOnly(Claims.ToArray());
        this.Reports = new ReadOnlyDictionary<string, AssuranceReportReference>(
            new Dictionary<string, AssuranceReportReference>(Reports, StringComparer.Ordinal));
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
        this.Target = Target;
        this.RequestedMode = RequestedMode;
        this.ResolvedMode = ResolvedMode;
        this.SessionKind = SessionKind;
        this.TimeoutMilliseconds = TimeoutMilliseconds;
        this.Lifecycle = Lifecycle;
        this.ReadIndex = ReadIndex;
    }

    public AssuranceVerdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public IReadOnlyList<ReadyVerifierOutput> Verifiers { get; }

    public IReadOnlyList<ReadyClaimOutput> Claims { get; }

    /// <summary> Gets the immutable ordinal-keyed report snapshot. </summary>
    public IReadOnlyDictionary<string, AssuranceReportReference> Reports { get; }

    public IReadOnlyList<ReadyResidualRiskOutput> ResidualRisks { get; }

    public ReadyTarget Target { get; }

    public AssuranceRequestedExecutionMode RequestedMode { get; }

    public AssuranceResolvedExecutionMode ResolvedMode { get; }

    public AssuranceSessionKind SessionKind { get; }

    public int TimeoutMilliseconds { get; }

    public ReadyLifecycleOutput? Lifecycle { get; }

    public ReadyReadIndexOutput? ReadIndex { get; }
}
