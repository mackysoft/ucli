using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Storage;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Resolves normalized plan-token configuration values from shared <c>.ucli/config.json</c>. </summary>
    internal static class PlanTokenConfigResolver
    {
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
                var configPath = UcliStoragePathResolver.ResolveConfigPath(repositoryRoot);
                if (!File.Exists(configPath))
                {
                    return FallbackSnapshot;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = document.RootElement;
                if (!UcliConfigJsonContractReader.TryReadPlanTokenLoose(root, out var config, out _))
                {
                    return FallbackSnapshot;
                }

                var planTokenModeLiteral = NormalizeOrFallback(config.PlanTokenMode);
                var operationPolicy = NormalizeOrFallback(config.OperationPolicy);
                var operationAllowlist = config.OperationAllowlist ?? FallbackAllowlist;

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
            if (PlanTokenModeCodec.TryParse(modeLiteral, out var planTokenMode))
            {
                return planTokenMode;
            }

            return PlanTokenMode.Optional;
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
