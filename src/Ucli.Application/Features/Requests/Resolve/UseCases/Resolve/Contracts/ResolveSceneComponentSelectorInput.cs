namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a component by scene, hierarchy path, and component type. </summary>
internal sealed record ResolveSceneComponentSelectorInput (
    string Scene,
    string HierarchyPath,
    string ComponentType) : ResolveSelectorInput;
