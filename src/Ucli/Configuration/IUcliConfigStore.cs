namespace MackySoft.Ucli.Configuration
{
    /// <summary> Provides read/write access to <c>.ucli/config.json</c>. </summary>
    internal interface IUcliConfigStore
    {
        /// <summary> Resolves the absolute path to the config file under a UnityProject root. </summary>
        /// <param name="unityProjectRoot"> The UnityProject root path used as the base directory. Must not be <see langword="null" />. </param>
        /// <returns> The absolute config path. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProjectRoot" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityProjectRoot" /> contains invalid path characters. </exception>
        /// <exception cref="NotSupportedException"> Thrown when <paramref name="unityProjectRoot" /> uses an unsupported path format. </exception>
        /// <exception cref="PathTooLongException"> Thrown when <paramref name="unityProjectRoot" /> exceeds platform path limits. </exception>
        string GetConfigPath (string unityProjectRoot);

        /// <summary> Loads config values for a UnityProject. </summary>
        /// <param name="unityProjectRoot"> The UnityProject root path from command context. <see langword="null" />, empty, and whitespace values return an invalid-argument result. </param>
        /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
        /// <returns> The config-load result. When <c>.ucli/config.json</c> does not exist, default config values are returned with <see cref="ConfigSource.Default" />. </returns>
        UcliConfigLoadResult Load (
            string unityProjectRoot,
            CancellationToken cancellationToken = default);

        /// <summary> Saves config values for a UnityProject. </summary>
        /// <param name="unityProjectRoot"> The UnityProject root path from command context. <see langword="null" />, empty, and whitespace values return an invalid-argument result. </param>
        /// <param name="config"> The config values to persist. </param>
        /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
        /// <returns> The config-save result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
        UcliConfigSaveResult Save (
            string unityProjectRoot,
            UcliConfig config,
            CancellationToken cancellationToken = default);
    }
}
