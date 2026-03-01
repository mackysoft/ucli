using System;
using System.IO;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Resolves plan-token mode from shared configuration. </summary>
    internal static class PlanTokenModeResolver
    {
        private const string PlanTokenModeOptional = "optional";

        private const string PlanTokenModeRequired = "required";

        private const string UcliDirectoryName = ".ucli";

        private const string ConfigFileName = "config.json";

        /// <summary> Resolves configured plan-token mode from shared config. </summary>
        /// <param name="repositoryRoot"> The repository root path. </param>
        /// <returns> The resolved plan-token mode. </returns>
        public static PlanTokenMode Resolve (string repositoryRoot)
        {
            try
            {
                var configPath = Path.Combine(repositoryRoot, UcliDirectoryName, ConfigFileName);
                if (!File.Exists(configPath))
                {
                    return PlanTokenMode.Optional;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = document.RootElement;
                var modeValue = PlanTokenJsonUtilities.TryReadString(root, "planTokenMode");
                if (string.Equals(modeValue, PlanTokenModeRequired, StringComparison.OrdinalIgnoreCase))
                {
                    return PlanTokenMode.Required;
                }

                if (string.Equals(modeValue, PlanTokenModeOptional, StringComparison.OrdinalIgnoreCase))
                {
                    return PlanTokenMode.Optional;
                }
            }
            catch
            {
                // NOTE:
                // Invalid or unreadable config falls back to optional mode by design.
            }

            return PlanTokenMode.Optional;
        }
    }
}
