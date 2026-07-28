using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Decodes the authoritative polymorphic selector contract for <c>ucli.resolve</c>. </summary>
    internal static class ResolveSelectorCodec
    {
        /// <summary> Parses one selector from operation arguments. </summary>
        /// <param name="args"> The operation arguments element. </param>
        /// <param name="selector"> The parsed selector when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when selector parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement args,
            [NotNullWhen(true)] out ResolveSelector? selector,
            out string errorMessage)
        {
            selector = null;
            if (!IpcPayloadCodec.TryDeserializeStrict(
                    args,
                    out ResolveSelectorArgs selectorContract,
                    out var readError))
            {
                errorMessage = $"Operation 'args' does not match the resolve selector contract. {readError.Message}";
                return false;
            }

            return UnityObjectReferenceContractMapper.TryMap(
                selectorContract,
                out selector,
                out errorMessage);
        }
    }
}
