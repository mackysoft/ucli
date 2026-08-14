using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Infrastructure.Configuration;
using MackySoft.Ucli.Unity.Execution.PlanToken;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Resolves fail-closed eval enablement from the canonical project configuration. </summary>
    internal static class EvalConfigResolver
    {
        public static async ValueTask<EvalConfigResolution> ResolveAsync (IPlanTokenEnvironment environment, CancellationToken cancellationToken)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            var load = await new UcliConfigFileLoader().LoadAsync(environment.Capture().RepositoryRoot, cancellationToken);
            return load.State switch
            {
                UcliConfigFileLoadState.Default or UcliConfigFileLoadState.File when load.Snapshot!.EvalEnabled => EvalConfigResolution.Enabled,
                UcliConfigFileLoadState.Default or UcliConfigFileLoadState.File => EvalConfigResolution.Disabled,
                UcliConfigFileLoadState.Invalid => EvalConfigResolution.Invalid,
                UcliConfigFileLoadState.Unavailable => EvalConfigResolution.Unavailable,
                _ => throw new InvalidOperationException("Config loader returned an unsupported state."),
            };
        }
    }

    internal enum EvalConfigResolution
    {
        Enabled = 0,
        Disabled = 1,
        Invalid = 2,
        Unavailable = 3,
    }
}
