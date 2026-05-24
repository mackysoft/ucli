namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves an asset path. </summary>
internal sealed record ResolveAssetPathSelectorInput (string AssetPath) : ResolveSelectorInput;
