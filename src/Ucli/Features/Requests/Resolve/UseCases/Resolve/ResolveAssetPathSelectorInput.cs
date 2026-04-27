namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents a selector that resolves an asset path. </summary>
internal sealed record ResolveAssetPathSelectorInput (string AssetPath) : ResolveSelectorInput;
