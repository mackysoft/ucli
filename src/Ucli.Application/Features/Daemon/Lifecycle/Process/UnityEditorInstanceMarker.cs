namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Represents one Unity <c>Library/EditorInstance.json</c> marker candidate. </summary>
/// <param name="MarkerPath"> The absolute marker file path. </param>
/// <param name="ProcessId"> The process identifier recorded by Unity. </param>
/// <param name="UpdatedAtUtc"> The marker file update timestamp. </param>
/// <param name="Version"> The optional Unity version value recorded by Unity. </param>
/// <param name="AppPath"> The optional Unity application path recorded by Unity. </param>
/// <param name="AppContentsPath"> The optional Unity application contents path recorded by Unity. </param>
internal sealed record UnityEditorInstanceMarker (
    string MarkerPath,
    int ProcessId,
    DateTimeOffset UpdatedAtUtc,
    string? Version,
    string? AppPath,
    string? AppContentsPath);
