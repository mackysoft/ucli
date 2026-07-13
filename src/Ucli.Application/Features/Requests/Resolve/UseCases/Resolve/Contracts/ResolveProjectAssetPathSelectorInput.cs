using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a ProjectSettings asset path. </summary>
internal sealed record ResolveProjectAssetPathSelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a selector with a validated ProjectSettings asset path. </summary>
    /// <param name="projectAssetPath"> The path below <c>ProjectSettings/</c>. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectAssetPath" /> is <see langword="null" />. </exception>
    public ResolveProjectAssetPathSelectorInput (ProjectSettingsAssetPath projectAssetPath)
    {
        ProjectAssetPath = projectAssetPath ?? throw new ArgumentNullException(nameof(projectAssetPath));
    }

    /// <summary> Gets the validated ProjectSettings asset path. </summary>
    public ProjectSettingsAssetPath ProjectAssetPath { get; }
}
