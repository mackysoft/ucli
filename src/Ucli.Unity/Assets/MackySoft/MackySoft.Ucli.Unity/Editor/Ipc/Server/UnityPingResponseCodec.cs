using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Encodes ping response payload values for Unity IPC server. </summary>
    internal static class UnityPingResponseCodec
    {
        private const string RuntimeBatchmode = "batchmode";

        /// <summary> Creates one ping response payload from runtime environment values. </summary>
        /// <param name="unityVersion"> The Unity editor version string. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="isCompiling"> Whether Unity editor compilation is currently active. </param>
        /// <returns> The ping response payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" /> or <paramref name="serverVersion" /> is empty or whitespace. </exception>
        public static IpcPingResponse CreatePayload (
            string unityVersion,
            string serverVersion,
            bool isCompiling)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);
            ArgumentException.ThrowIfNullOrWhiteSpace(serverVersion);

            return new IpcPingResponse(
                ServerVersion: serverVersion,
                Runtime: RuntimeBatchmode,
                UnityVersion: unityVersion,
                CompileState: IpcCompileStateCodec.ToValue(isCompiling));
        }
    }
}
