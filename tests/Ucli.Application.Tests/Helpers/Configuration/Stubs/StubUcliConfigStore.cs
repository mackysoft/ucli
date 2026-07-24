using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubUcliConfigStore : IUcliConfigStore
{
    private readonly UcliConfigLoadResult loadResult;

    public StubUcliConfigStore ()
        : this(UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default))
    {
    }

    public StubUcliConfigStore (UcliConfigLoadResult loadResult)
    {
        this.loadResult = loadResult;
    }

    public AbsolutePath GetConfigPath (AbsolutePath storageRoot)
    {
        return ContainedPath.Create(
            storageRoot,
            RootRelativePath.Parse(".ucli/config.json")).Target;
    }

    public ValueTask<UcliConfigLoadResult> LoadAsync (
        AbsolutePath storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(loadResult);
    }

    public ValueTask<UcliConfigSaveResult> SaveAsync (
        AbsolutePath storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(UcliConfigSaveResult.Success());
    }
}
