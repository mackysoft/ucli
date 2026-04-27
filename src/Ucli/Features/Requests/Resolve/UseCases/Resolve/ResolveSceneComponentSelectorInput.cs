namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents a selector that resolves a component by scene, hierarchy path, and component type. </summary>
internal sealed record ResolveSceneComponentSelectorInput (
    string Scene,
    string HierarchyPath,
    string ComponentType) : ResolveSelectorInput;
