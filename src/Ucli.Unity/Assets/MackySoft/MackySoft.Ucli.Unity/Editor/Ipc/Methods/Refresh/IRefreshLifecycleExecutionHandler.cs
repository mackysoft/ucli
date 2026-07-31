using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Handles one typed refresh start binding and returns an unencoded action
    /// outcome to its delivery adapter.
    /// </summary>
    internal interface IRefreshLifecycleExecutionHandler
    {
        ValueTask<RefreshLifecycleExecutionOutcome> ExecuteAsync (
            LifecycleExecutionStartBinding requestedStart);
    }
}
