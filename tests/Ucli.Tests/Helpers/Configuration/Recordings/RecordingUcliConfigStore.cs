using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Helpers.Configuration;

internal sealed class RecordingUcliConfigStore : IUcliConfigStore
{
    private readonly List<LoadInvocation> loadInvocations = [];
    private readonly List<SaveInvocation> saveInvocations = [];

    public Func<string, CancellationToken, ValueTask<UcliConfigLoadResult>> LoadHandler { get; set; } =
        static (_, _) => throw new NotSupportedException("This test config store has no load handler.");

    public Func<string, UcliConfig, CancellationToken, ValueTask<UcliConfigSaveResult>> SaveHandler { get; set; } =
        static (_, _, _) => ValueTask.FromResult(UcliConfigSaveResult.Success());

    public IReadOnlyList<LoadInvocation> LoadInvocations => loadInvocations;

    public IReadOnlyList<SaveInvocation> SaveInvocations => saveInvocations;

    public string GetConfigPath (string storageRoot)
    {
        return UcliStoragePathResolver.ResolveConfigPath(storageRoot);
    }

    public ValueTask<UcliConfigLoadResult> LoadAsync (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        loadInvocations.Add(new LoadInvocation(storageRoot, cancellationToken));
        return LoadHandler(storageRoot, cancellationToken);
    }

    public ValueTask<UcliConfigSaveResult> SaveAsync (
        string storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        saveInvocations.Add(new SaveInvocation(storageRoot, config, cancellationToken));
        return SaveHandler(storageRoot, config, cancellationToken);
    }

    internal readonly record struct LoadInvocation (
        string StorageRoot,
        CancellationToken CancellationToken);

    internal readonly record struct SaveInvocation (
        string StorageRoot,
        UcliConfig Config,
        CancellationToken CancellationToken);
}
