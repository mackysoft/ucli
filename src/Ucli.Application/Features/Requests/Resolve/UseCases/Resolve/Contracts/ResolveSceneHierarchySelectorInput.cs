namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a GameObject by scene and hierarchy path. </summary>
internal sealed record ResolveSceneHierarchySelectorInput (
    string Scene,
    string HierarchyPath) : ResolveSelectorInput;
