using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Handles one typed Play Mode enter start binding and returns an unencoded
    /// action outcome to its delivery adapter.
    /// </summary>
    internal interface IPlayEnterLifecycleExecutionHandler
    {
        ValueTask<PlayEnterLifecycleExecutionOutcome> ExecuteAsync (
            LifecycleExecutionStartBinding requestedStart);
    }
}
