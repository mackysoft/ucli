
namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents read-index readiness evidence emitted by ready. </summary>
internal sealed record ReadyReadIndexOutput
{
    public ReadyReadIndexOutput (
        ReadyReadIndexMode Mode,
        IReadOnlyList<ReadyReadIndexArtifactOutput> Artifacts)
    {
        if (!TextVocabulary.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Read-index mode must be defined by the configuration contract.");
        }

        ArgumentNullException.ThrowIfNull(Artifacts);
        if (Artifacts.Any(static artifact => artifact is null))
        {
            throw new ArgumentException("Read-index artifacts must not contain null.", nameof(Artifacts));
        }

        this.Mode = Mode;
        this.Artifacts = Array.AsReadOnly(Artifacts.ToArray());
    }

    public ReadyReadIndexMode Mode { get; }

    public IReadOnlyList<ReadyReadIndexArtifactOutput> Artifacts { get; }
}
