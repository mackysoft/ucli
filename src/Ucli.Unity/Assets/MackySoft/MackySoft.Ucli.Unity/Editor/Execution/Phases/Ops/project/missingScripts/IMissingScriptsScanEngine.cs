using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Defines the saved asset scan performed by the missing script query operation. </summary>
    internal interface IMissingScriptsScanEngine
    {
        /// <summary> Scans the requested saved assets. </summary>
        /// <param name="args"> The validated query arguments. </param>
        /// <returns> The complete scan result. </returns>
        MissingScriptsCheckResult Scan (MissingScriptsCheckArgs args, CancellationToken cancellationToken);
    }
}
