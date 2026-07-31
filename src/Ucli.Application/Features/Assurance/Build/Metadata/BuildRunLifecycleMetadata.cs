using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents lifecycle snapshots captured around BuildPipeline execution. </summary>
internal sealed record BuildRunLifecycleMetadata (
    UnityEditorObservation Before,
    UnityEditorObservation After);
