using System.Reflection;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Resolves uCLI Unity plugin version from assembly metadata. </summary>
    internal sealed class AssemblyServerVersionProvider : IServerVersionProvider
    {
        private const string UnknownServerVersion = "0.0.0";

        /// <summary> Gets the resolved uCLI Unity plugin version. </summary>
        /// <returns> The resolved version string. </returns>
        public string GetVersion ()
        {
            var pluginAssembly = typeof(AssemblyServerVersionProvider).Assembly;

            var informationalVersion = pluginAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var normalizedInformationalVersion = informationalVersion.Trim();
                var buildMetadataSeparatorIndex = normalizedInformationalVersion.IndexOf('+');
                if (buildMetadataSeparatorIndex > 0)
                {
                    return normalizedInformationalVersion.Substring(0, buildMetadataSeparatorIndex);
                }

                return normalizedInformationalVersion;
            }

            if (StringValueNormalizer.TryTrimToNonEmpty(
                    pluginAssembly
                        .GetCustomAttribute<AssemblyFileVersionAttribute>()
                        ?.Version,
                    out var fileVersion))
            {
                return fileVersion;
            }

            var assemblyVersion = pluginAssembly.GetName().Version;
            if (assemblyVersion != null)
            {
                return assemblyVersion.ToString();
            }

            return UnknownServerVersion;
        }
    }
}
