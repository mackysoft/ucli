using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Handles one typed compile start binding and returns an unencoded action
    /// outcome to its delivery adapter.
    /// </summary>
    internal interface ICompileLifecycleExecutionHandler
    {
        ValueTask<CompileLifecycleExecutionOutcome> ExecuteAsync (
            LifecycleExecutionStartBinding requestedStart);
    }
}
