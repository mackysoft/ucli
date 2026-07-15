using System.Collections.ObjectModel;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents the compile assurance payload emitted by the <c>compile</c> command. </summary>
internal sealed record CompileExecutionOutput
{
    /// <summary> Initializes a compile assurance payload with a defined verdict. </summary>
    /// <param name="Reports"> The report map to copy with ordinal key semantics. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Reports" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Verdict" /> or <paramref name="SessionKind" /> is not defined by the assurance contract. </exception>
    public CompileExecutionOutput (
        AssuranceVerdict Verdict,
        ProjectIdentityInfo Project,
        IReadOnlyList<CompileVerifierOutput> Verifiers,
        IReadOnlyList<CompileClaimOutput> Claims,
        IReadOnlyDictionary<string, AssuranceReportReference> Reports,
        IReadOnlyList<CompileResidualRiskOutput> ResidualRisks,
        AssuranceRequestedExecutionMode RequestedMode,
        AssuranceResolvedExecutionMode ResolvedMode,
        AssuranceSessionKind SessionKind,
        int TimeoutMilliseconds,
        CompileOutput Compile)
    {
        if (!ContractLiteralCodec.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }
        if (!ContractLiteralCodec.IsDefined(SessionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(SessionKind), SessionKind, "Session kind must be defined by the assurance contract.");
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
        this.RequestedMode = RequestedMode;
        this.ResolvedMode = ResolvedMode;
        this.SessionKind = SessionKind;
        this.TimeoutMilliseconds = TimeoutMilliseconds;
        this.Compile = Compile ?? throw new ArgumentNullException(nameof(Compile));
    }

    public AssuranceVerdict Verdict { get; }

    public ProjectIdentityInfo Project { get; }

    public IReadOnlyList<CompileVerifierOutput> Verifiers { get; }

    public IReadOnlyList<CompileClaimOutput> Claims { get; }

    /// <summary> Gets the immutable ordinal-keyed report snapshot. </summary>
    public IReadOnlyDictionary<string, AssuranceReportReference> Reports { get; }

    public IReadOnlyList<CompileResidualRiskOutput> ResidualRisks { get; }

    public AssuranceRequestedExecutionMode RequestedMode { get; }

    public AssuranceResolvedExecutionMode ResolvedMode { get; }

    public AssuranceSessionKind SessionKind { get; }

    public int TimeoutMilliseconds { get; }

    public CompileOutput Compile { get; }
}
