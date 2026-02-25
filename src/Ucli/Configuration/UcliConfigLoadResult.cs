using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Configuration
{
    /// <summary> Represents the result of loading <see cref="UcliConfig" /> values. </summary>
    /// <param name="Config"> The loaded config, or <see langword="null" /> on failure. </param>
    /// <param name="Source"> The source used for config values. </param>
    /// <param name="Error"> The structured load error, or <see langword="null" /> on success. </param>
    internal sealed record UcliConfigLoadResult (
        UcliConfig? Config,
        ConfigSource Source,
        ExecutionError? Error)
    {
        /// <summary> Gets a value indicating whether config load succeeded. </summary>
        public bool IsSuccess => Config is not null && Error is null;

        /// <summary> Creates a successful config-load result. </summary>
        /// <param name="config"> The loaded config values. </param>
        /// <param name="source"> The source used for the loaded values. </param>
        /// <returns> The successful result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
        public static UcliConfigLoadResult Success (UcliConfig config, ConfigSource source)
        {
            ArgumentNullException.ThrowIfNull(config);
            return new UcliConfigLoadResult(config, source, null);
        }

        /// <summary> Creates a failed config-load result. </summary>
        /// <param name="error"> The structured load error. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
        public static UcliConfigLoadResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new UcliConfigLoadResult(null, ConfigSource.Default, error);
        }
    }
}
