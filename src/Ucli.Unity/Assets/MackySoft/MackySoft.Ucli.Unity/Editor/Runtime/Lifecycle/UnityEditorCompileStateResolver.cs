using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Resolves the compile state from observed Unity compilation activity. </summary>
    internal static class UnityEditorCompileStateResolver
    {
        /// <summary> Resolves one compile state from the current compilation flags. </summary>
        public static UnityEditorCompileState Resolve (
            bool isCompiling,
            bool hasCompileFailure)
        {
            if (isCompiling)
            {
                return UnityEditorCompileState.Compiling;
            }

            return hasCompileFailure
                ? UnityEditorCompileState.Failed
                : UnityEditorCompileState.Ready;
        }
    }
}
