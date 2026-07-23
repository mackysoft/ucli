using MackySoft.FileSystem;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class UnexpectedUnityVersionResolver : IUnityVersionResolver
{
    private readonly string reason;

    public UnexpectedUnityVersionResolver (string reason)
    {
        this.reason = reason;
    }

    public UnityVersionResolutionResult Resolve (
        AbsolutePath projectPath,
        string? preferredUnityVersion)
    {
        throw new InvalidOperationException(reason);
    }
}
