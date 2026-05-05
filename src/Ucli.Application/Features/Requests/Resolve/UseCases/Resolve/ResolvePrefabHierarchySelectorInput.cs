namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents a selector that resolves a prefab child by prefab path and hierarchy path. </summary>
internal sealed record ResolvePrefabHierarchySelectorInput (
    string Prefab,
    string HierarchyPath) : ResolveSelectorInput;
