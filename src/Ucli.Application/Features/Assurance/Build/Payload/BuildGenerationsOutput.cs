using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents generation validity for build artifacts. </summary>
internal sealed record BuildGenerationsOutput (
    UnityEditorGenerationSnapshot? Before,
    UnityEditorGenerationSnapshot? After,
    UnityEditorGenerationSnapshot? ValidFor);
