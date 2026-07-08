using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Represents an invalid entry point return value before eval serialization. </summary>
    internal sealed class CsEvalEntryPointReturnValueResolutionException : Exception
    {
        public CsEvalEntryPointReturnValueResolutionException (string message)
            : base(message)
        {
        }
    }
}
