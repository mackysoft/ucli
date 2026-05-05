using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Resolves Unity editor executable paths that satisfy target Unity version constraints. </summary>
internal sealed class UnityEditorPathResolver : IUnityEditorPathResolver
{
    private readonly IUnityEditorSearchRootProvider searchRootProvider;

    private readonly UnityEditorExecutablePathLocator executablePathLocator;

    private readonly UnityEditorVersionConsistencyValidator versionConsistencyValidator;

    /// <summary> Initializes a new instance of the <see cref="UnityEditorPathResolver" /> class. </summary>
    /// <param name="searchRootProvider"> The search-root provider dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootProvider" /> is <see langword="null" />. </exception>
    public UnityEditorPathResolver (IUnityEditorSearchRootProvider searchRootProvider)
        : this(
            searchRootProvider,
            new UnityEditorExecutablePathLocator(),
            new UnityEditorVersionConsistencyValidator())
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UnityEditorPathResolver" /> class. </summary>
    /// <param name="searchRootProvider"> The search-root provider dependency. </param>
    /// <param name="executablePathLocator"> The executable-path locator dependency. </param>
    /// <param name="versionConsistencyValidator"> The version-consistency validator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    internal UnityEditorPathResolver (
        IUnityEditorSearchRootProvider searchRootProvider,
        UnityEditorExecutablePathLocator executablePathLocator,
        UnityEditorVersionConsistencyValidator versionConsistencyValidator)
    {
        this.searchRootProvider = searchRootProvider ?? throw new ArgumentNullException(nameof(searchRootProvider));
        this.executablePathLocator = executablePathLocator ?? throw new ArgumentNullException(nameof(executablePathLocator));
        this.versionConsistencyValidator = versionConsistencyValidator ?? throw new ArgumentNullException(nameof(versionConsistencyValidator));
    }

    /// <summary> Resolves an editor executable path that matches the specified Unity version. </summary>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <param name="preferredUnityEditorPath"> The preferred editor path value. </param>
    /// <returns> The editor-path resolution result. </returns>
    public UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        string? preferredUnityEditorPath)
    {
        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                "Unity version must not be null, empty, or whitespace."));
        }

        var normalizedUnityVersion = unityVersion.Trim();
        var executablePathResult = executablePathLocator.Resolve(
            normalizedUnityVersion,
            preferredUnityEditorPath,
            searchRootProvider.GetSearchRoots());
        if (!executablePathResult.IsSuccess)
        {
            return executablePathResult;
        }

        return versionConsistencyValidator.Validate(
            executablePathResult.UnityEditorPath!,
            normalizedUnityVersion);
    }
}
