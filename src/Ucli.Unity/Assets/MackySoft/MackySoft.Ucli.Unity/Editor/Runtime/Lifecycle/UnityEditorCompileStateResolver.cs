using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Resolves the compile state from observed Unity compilation activity. </summary>
    internal static class UnityEditorCompileStateResolver
    {
        /// <summary> Resolves one compile state from the current compilation flags. </summary>
        public static IpcCompileState Resolve (
            bool isCompiling,
            bool hasCompileFailure)
        {
            if (isCompiling)
            {
                return IpcCompileState.Compiling;
            }

            return hasCompileFailure
                ? IpcCompileState.Failed
                : IpcCompileState.Ready;
        }
    }
}
