using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Provides read/write access to <c>.ucli/config.json</c>. </summary>
internal interface IUcliConfigStore
{
    /// <summary> Resolves the absolute path to the config file under a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path used as the base directory. </para>
    /// <para> Must not be <see langword="null" />. </para>
    /// </param>
    /// <returns> The absolute config path. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />. </exception>
    AbsolutePath GetConfigPath (AbsolutePath storageRoot);

    /// <summary> Loads config values for a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path from command context. </para>
    /// <para> <see langword="null" />, empty, and whitespace values return an invalid-argument result. </para>
    /// </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-load result. When <c>.ucli/config.json</c> does not exist, default config values are returned with <see cref="ConfigSource.Default" />. </returns>
    ValueTask<UcliConfigLoadResult> LoadAsync (
        AbsolutePath storageRoot,
        CancellationToken cancellationToken = default);

    /// <summary> Saves config values for a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path from command context. </para>
    /// <para> <see langword="null" />, empty, and whitespace values return an invalid-argument result. </para>
    /// </param>
    /// <param name="config"> The config values to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-save result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    ValueTask<UcliConfigSaveResult> SaveAsync (
        AbsolutePath storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default);
}
