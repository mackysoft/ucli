using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Handles one typed Play Mode exit start binding and returns an unencoded
    /// action outcome to its delivery adapter.
    /// </summary>
    internal interface IPlayExitLifecycleExecutionHandler
    {
        ValueTask<PlayExitLifecycleExecutionOutcome> ExecuteAsync (
            LifecycleExecutionStartBinding requestedStart);
    }
}
