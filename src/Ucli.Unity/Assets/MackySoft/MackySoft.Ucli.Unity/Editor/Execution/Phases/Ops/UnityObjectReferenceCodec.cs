using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Decodes polymorphic operation-argument references into Unity execution references. </summary>
    internal static class UnityObjectReferenceCodec
    {
        /// <summary> Parses one Unity-object reference from a JSON element. </summary>
        /// <param name="element"> The JSON reference contract. </param>
        /// <param name="propertyPath"> The logical property path used in diagnostics. </param>
        /// <param name="aliasReferences"> The request-local alias map. </param>
        /// <param name="reference"> The parsed reference when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement element,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            reference = null;
            if (!IpcPayloadCodec.TryDeserializeStrict(
                    element,
                    out UnityObjectReferenceArgs referenceContract,
                    out var readError))
            {
                errorMessage = $"Operation '{propertyPath}' does not match the Unity-object reference contract. {readError.Message}";
                return false;
            }

            return UnityObjectReferenceContractMapper.TryMap(
                referenceContract,
                propertyPath,
                aliasReferences,
                out reference,
                out errorMessage);
        }
    }
}
