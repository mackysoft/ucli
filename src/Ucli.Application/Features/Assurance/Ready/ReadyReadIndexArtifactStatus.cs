
namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines the finite observation statuses of read-index artifacts. </summary>
[VocabularyDefinition]
internal enum ReadyReadIndexArtifactStatus
{
    /// <summary> The artifact is available under the requested freshness policy. </summary>
    [VocabularyText("available")]
    Available = 1,

    /// <summary> The artifact could not satisfy the requested readiness check. </summary>
    [VocabularyText("failed")]
    Failed = 2,
}
