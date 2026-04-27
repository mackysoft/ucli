namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents a selector that resolves a project-relative asset path. </summary>
internal sealed record ResolveProjectAssetPathSelectorInput (string ProjectAssetPath) : ResolveSelectorInput;
