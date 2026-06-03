using System.Collections.Generic;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Authorizes dangerous operation calls after request compilation. </summary>
    internal interface IDangerousOperationCallAuthorizer
    {
        /// <summary> Validates dangerous operations against call intent and project configuration. </summary>
        /// <param name="preparedOperations"> The operations prepared by validate and plan phases. </param>
        /// <param name="allowDangerous"> Whether the caller explicitly confirmed dangerous execution. </param>
        /// <param name="failure"> The operation failure when authorization is denied. </param>
        /// <returns> <see langword="true" /> when all prepared operations are authorized; otherwise <see langword="false" />. </returns>
        bool TryAuthorize (
            IReadOnlyList<PreparedOperation> preparedOperations,
            bool allowDangerous,
            out OperationFailure? failure);
    }
}
