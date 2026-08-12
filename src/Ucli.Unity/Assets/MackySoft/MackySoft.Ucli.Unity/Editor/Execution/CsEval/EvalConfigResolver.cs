using System;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Execution.PlanToken;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Reads the fail-closed eval enablement flag directly from project configuration. </summary>
    internal static class EvalConfigResolver
    {
        public static bool IsEnabled (IPlanTokenEnvironment environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            try
            {
                var configPath = UcliStoragePathResolver.ResolveConfigPath(environment.Capture().RepositoryRoot);
                if (!File.Exists(configPath.Value))
                {
                    return false;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath.Value));
                return document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("evalEnabled", out var enabled)
                    && enabled.ValueKind == JsonValueKind.True;
            }
            catch
            {
                return false;
            }
        }
    }
}
