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

    public string GetConfigPath (string storageRoot)
    {
        return Path.Combine(storageRoot, ".ucli", "config.json");
    }

    public ValueTask<UcliConfigLoadResult> LoadAsync (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(loadResult);
    }

    public ValueTask<UcliConfigSaveResult> SaveAsync (
        string storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(UcliConfigSaveResult.Success());
    }
}
