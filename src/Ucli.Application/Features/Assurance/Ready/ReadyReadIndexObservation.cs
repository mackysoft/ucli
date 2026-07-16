using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents read-index readiness summary before it is merged into the primary ready claim. </summary>
internal readonly record struct ReadyReadIndexObservation (
    ReadyReadIndexOutput? Output,
    bool HasFailure,
    bool IsDisabled)
{
    /// <summary> Creates a disabled read-index observation. </summary>
    public static ReadyReadIndexObservation Disabled ()
    {
        return new ReadyReadIndexObservation(
            new ReadyReadIndexOutput(ReadyReadIndexMode.Disabled, []),
            HasFailure: false,
            IsDisabled: true);
    }

    /// <summary> Creates a failed read-index observation before mode resolution completes. </summary>
    public static ReadyReadIndexObservation Failed (
        ReadIndexMode? inputMode,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var mode = inputMode.HasValue
            ? ToOutputMode(inputMode.Value)
            : ReadyReadIndexMode.Unknown;
        return new ReadyReadIndexObservation(
            new ReadyReadIndexOutput(
                mode,
                [
                    ReadyReadIndexArtifactOutput.Failed(
                        ReadyReadIndexArtifactName.Mode,
                        required: true,
                        UcliCoreErrorCodes.InvalidArgument,
                        message,
                        actionRequired: null),
                ]),
            HasFailure: true,
            IsDisabled: false);
    }

    /// <summary> Creates a read-index observation from artifact observations. </summary>
    public static ReadyReadIndexObservation FromArtifacts (
        ReadIndexMode mode,
        IReadOnlyList<ReadyReadIndexArtifactOutput> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        return new ReadyReadIndexObservation(
            new ReadyReadIndexOutput(ToOutputMode(mode), artifacts),
            artifacts.Any(static artifact => artifact.Required
                && artifact.Status == ReadyReadIndexArtifactStatus.Failed),
            IsDisabled: false);
    }

    private static ReadyReadIndexMode ToOutputMode (ReadIndexMode mode)
    {
        return mode switch
        {
            ReadIndexMode.Disabled => ReadyReadIndexMode.Disabled,
            ReadIndexMode.AllowStale => ReadyReadIndexMode.AllowStale,
            ReadIndexMode.RequireFresh => ReadyReadIndexMode.RequireFresh,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported read-index mode."),
        };
    }
}
