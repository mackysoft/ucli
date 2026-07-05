namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingUnityVersionResolver : IUnityVersionResolver
{
    public const string DefaultUnityVersion = "2023.2.22f1";

    private readonly List<Invocation> invocations = [];

    public RecordingUnityVersionResolver ()
        : this(UnityVersionResolutionResult.Success(DefaultUnityVersion))
    {
    }

    public RecordingUnityVersionResolver (UnityVersionResolutionResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public UnityVersionResolutionResult Result { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public UnityVersionResolutionResult Resolve (
        string unityProjectRoot,
        string? preferredUnityVersion)
    {
        invocations.Add(new Invocation(
            unityProjectRoot,
            preferredUnityVersion));

        return Result;
    }

    internal readonly record struct Invocation (
        string UnityProjectRoot,
        string? PreferredUnityVersion);
}
