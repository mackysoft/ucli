namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents read-index readiness evidence emitted by ready. </summary>
internal sealed record ReadyReadIndexOutput (
    string Mode,
    IReadOnlyList<ReadyReadIndexArtifactOutput> Artifacts);
