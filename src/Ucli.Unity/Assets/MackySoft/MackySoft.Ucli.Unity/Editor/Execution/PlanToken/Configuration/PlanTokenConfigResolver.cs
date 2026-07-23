using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

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
        public static PlanTokenConfigSnapshot Resolve (AbsolutePath repositoryRoot)
        {
            if (repositoryRoot == null)
            {
                return FallbackSnapshot;
            }

            try
            {
                var configPath = UcliStoragePathResolver.ResolveConfigPath(repositoryRoot);
                if (!File.Exists(configPath.Value))
                {
                    return FallbackSnapshot;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath.Value));
                var root = document.RootElement;
                if (!UcliConfigJsonContractReader.TryReadPlanTokenLoose(root, out var config))
                {
                    return FallbackSnapshot;
                }

                var planTokenModeLiteral = StringValueNormalizer.TrimOrFallback(config.PlanTokenMode, NaLiteral);
                var operationPolicy = StringValueNormalizer.TrimOrFallback(config.OperationPolicy, NaLiteral);
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
            if (ContractLiteralInputParser.TryParseIgnoreCase<PlanTokenMode>(modeLiteral, out var planTokenMode))
            {
                return planTokenMode;
            }

            return PlanTokenMode.Optional;
        }
    }
}
