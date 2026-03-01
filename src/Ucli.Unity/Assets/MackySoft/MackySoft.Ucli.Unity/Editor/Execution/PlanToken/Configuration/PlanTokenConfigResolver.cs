using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Resolves normalized plan-token configuration values from shared <c>.ucli/config.json</c>. </summary>
    internal static class PlanTokenConfigResolver
    {
        private const string PlanTokenModeRequired = "required";

        private const string UcliDirectoryName = ".ucli";

        private const string ConfigFileName = "config.json";

        private const string NaLiteral = "na";

        private static readonly IReadOnlyList<string> FallbackAllowlist = new[]
        {
            NaLiteral,
        };

        private static readonly PlanTokenConfigSnapshot FallbackSnapshot = new(
            Mode: PlanTokenMode.Optional,
            PlanTokenModeLiteral: NaLiteral,
            OperationPolicy: NaLiteral,
            OperationAllowlist: FallbackAllowlist);

        /// <summary> Resolves one normalized configuration snapshot. </summary>
        /// <param name="repositoryRoot"> The repository root path. </param>
        /// <returns> The resolved configuration snapshot. </returns>
        public static PlanTokenConfigSnapshot Resolve (string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                return FallbackSnapshot;
            }

            try
            {
                var configPath = Path.Combine(repositoryRoot, UcliDirectoryName, ConfigFileName);
                if (!File.Exists(configPath))
                {
                    return FallbackSnapshot;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return FallbackSnapshot;
                }

                var planTokenModeLiteral = NormalizeOrFallback(PlanTokenJsonUtilities.TryReadString(root, "planTokenMode"));
                var operationPolicy = NormalizeOrFallback(PlanTokenJsonUtilities.TryReadString(root, "operationPolicy"));
                var operationAllowlist = ReadAllowlist(root);

                return new PlanTokenConfigSnapshot(
                    Mode: ResolveMode(planTokenModeLiteral),
                    PlanTokenModeLiteral: planTokenModeLiteral,
                    OperationPolicy: operationPolicy,
                    OperationAllowlist: operationAllowlist);
            }
            catch
            {
                // NOTE:
                // Invalid or unreadable config is treated as missing and mapped to fallback values.
                return FallbackSnapshot;
            }
        }

        /// <summary> Resolves runtime mode from one normalized mode literal. </summary>
        /// <param name="modeLiteral"> The normalized mode literal. </param>
        /// <returns> The resolved runtime mode. </returns>
        private static PlanTokenMode ResolveMode (string modeLiteral)
        {
            return string.Equals(modeLiteral, PlanTokenModeRequired, StringComparison.OrdinalIgnoreCase)
                ? PlanTokenMode.Required
                : PlanTokenMode.Optional;
        }

        /// <summary> Reads normalized allowlist values from config root. </summary>
        /// <param name="root"> The config root object. </param>
        /// <returns> The normalized allowlist values. </returns>
        private static IReadOnlyList<string> ReadAllowlist (JsonElement root)
        {
            if (!root.TryGetProperty("operationAllowlist", out var allowlistElement)
                || allowlistElement.ValueKind != JsonValueKind.Array)
            {
                return FallbackAllowlist;
            }

            var values = new List<string>();
            foreach (var allowlistValue in allowlistElement.EnumerateArray())
            {
                if (allowlistValue.ValueKind != JsonValueKind.String)
                {
                    return FallbackAllowlist;
                }

                var pattern = allowlistValue.GetString();
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                values.Add(pattern.Trim());
            }

            return values.ToArray();
        }

        /// <summary> Normalizes one string value or returns fallback literal when missing. </summary>
        /// <param name="value"> The input value. </param>
        /// <returns> The normalized value. </returns>
        private static string NormalizeOrFallback (string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? NaLiteral : value.Trim();
        }
    }
}
