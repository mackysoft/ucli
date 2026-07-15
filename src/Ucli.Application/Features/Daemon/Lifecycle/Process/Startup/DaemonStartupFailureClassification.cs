using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Represents one classified Unity startup blocker found before daemon bootstrap completed. </summary>
internal sealed record DaemonStartupFailureClassification
{
    public DaemonStartupFailureClassification (
        DaemonStartupBlockingReason startupBlockingReason,
        DaemonDiagnosisReason reason,
        DaemonStartupRetryDisposition retryDisposition,
        string message,
        DaemonDiagnosisStartupPhase startupPhase,
        DaemonDiagnosisActionRequired actionRequired,
        DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        if (!ContractLiteralCodec.IsDefined(startupBlockingReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startupBlockingReason),
                startupBlockingReason,
                "Unsupported startup blocking reason.");
        }

        if (!ContractLiteralCodec.IsDefined(retryDisposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryDisposition),
                retryDisposition,
                "Unsupported retry disposition.");
        }

        if (!ContractLiteralCodec.IsDefined(startupPhase))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startupPhase),
                startupPhase,
                "Unsupported startup phase.");
        }

        if (!ContractLiteralCodec.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unsupported daemon diagnosis reason.");
        }

        if (!ContractLiteralCodec.IsDefined(actionRequired))
        {
            throw new ArgumentOutOfRangeException(nameof(actionRequired), actionRequired, "Unsupported daemon diagnosis action.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        StartupBlockingReason = startupBlockingReason;
        Reason = reason;
        RetryDisposition = retryDisposition;
        Message = message;
        StartupPhase = startupPhase;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
    }

    public DaemonStartupBlockingReason StartupBlockingReason { get; }

    public DaemonDiagnosisReason Reason { get; }

    public DaemonStartupRetryDisposition RetryDisposition { get; }

    public string Message { get; }

    public DaemonDiagnosisStartupPhase StartupPhase { get; }

    public DaemonDiagnosisActionRequired ActionRequired { get; }

    public DaemonPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
