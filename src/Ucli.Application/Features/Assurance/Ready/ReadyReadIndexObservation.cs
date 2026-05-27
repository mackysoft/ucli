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
            new ReadyReadIndexOutput(ReadIndexModeValues.Disabled, []),
            HasFailure: false,
            IsDisabled: true);
    }

    /// <summary> Creates a failed read-index observation before mode resolution completes. </summary>
    public static ReadyReadIndexObservation Failed (
        ReadIndexMode? inputMode,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var mode = inputMode.HasValue ? ReadIndexModeCodec.ToValue(inputMode.Value) : "unknown";
        return new ReadyReadIndexObservation(
            new ReadyReadIndexOutput(
                mode,
                [
                    new ReadyReadIndexArtifactOutput(
                        Name: "readIndex.mode",
                        Status: ReadyReadIndexArtifactStatusValues.Failed,
                        Code: UcliCoreErrorCodes.InvalidArgument.Value,
                        Message: message),
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
            new ReadyReadIndexOutput(ReadIndexModeCodec.ToValue(mode), artifacts),
            artifacts.Any(static artifact => artifact.Required
                && string.Equals(
                    artifact.Status,
                    ReadyReadIndexArtifactStatusValues.Failed,
                    StringComparison.Ordinal)),
            IsDisabled: false);
    }
}
