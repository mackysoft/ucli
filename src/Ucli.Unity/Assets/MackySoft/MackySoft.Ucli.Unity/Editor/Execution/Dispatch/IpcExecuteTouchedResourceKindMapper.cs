using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Maps Unity execution touched-resource kinds to IPC protocol literals. </summary>
    internal static class IpcExecuteTouchedResourceKindMapper
    {
        /// <summary> Converts one touched resource kind to its protocol literal. </summary>
        /// <param name="kind"> The touched resource kind. </param>
        /// <returns> The protocol touched kind literal. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when <paramref name="kind" /> has an unsupported value. </exception>
        public static string ToName (OperationTouchKind kind)
        {
            switch (kind)
            {
                case OperationTouchKind.Scene:
                    return IpcExecuteTouchedResourceKindNames.Scene;

                case OperationTouchKind.Prefab:
                    return IpcExecuteTouchedResourceKindNames.Prefab;

                case OperationTouchKind.Asset:
                    return IpcExecuteTouchedResourceKindNames.Asset;

                case OperationTouchKind.ProjectSettings:
                    return IpcExecuteTouchedResourceKindNames.ProjectSettings;

                default:
                    throw new InvalidOperationException($"Unsupported touched resource kind '{kind}'.");
            }
        }
    }
}
