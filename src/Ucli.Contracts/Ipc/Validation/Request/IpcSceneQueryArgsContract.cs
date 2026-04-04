namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one validated <c>ucli.scene.query</c> argument contract. </summary>
/// <param name="ScenePath"> The required scene asset path for public query operations. <see langword="null" /> for edit-local selection sources. </param>
/// <param name="PathPrefix"> The optional hierarchy path prefix filter. </param>
/// <param name="ComponentType"> The optional component type filter. </param>
internal sealed record IpcSceneQueryArgsContract (
    string? ScenePath,
    string? PathPrefix,
    string? ComponentType);