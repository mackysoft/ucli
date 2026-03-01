using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Resolves phase-execution command from execute request command name. </summary>
    internal static class ExecuteRequestCommandResolver
    {
        /// <summary> Attempts to resolve one phase-execution command from protocol command name. </summary>
        /// <param name="commandName"> The execute request command name. </param>
        /// <param name="command"> The resolved phase command. </param>
        /// <returns> <see langword="true" /> when command is supported in the dispatcher; otherwise <see langword="false" />. </returns>
        public static bool TryResolve (
            string commandName,
            out PhaseExecutionCommand command)
        {
            switch (commandName)
            {
                case IpcExecuteCommandNames.Plan:
                    command = PhaseExecutionCommand.Plan;
                    return true;

                case IpcExecuteCommandNames.Call:
                    command = PhaseExecutionCommand.Call;
                    return true;

                case IpcExecuteCommandNames.Resolve:
                case IpcExecuteCommandNames.Query:
                case IpcExecuteCommandNames.Refresh:
                    command = default;
                    return false;

                default:
                    command = default;
                    return false;
            }
        }
    }
}
