using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Represents a parsed <c>Namespace.Type.Method</c> entry point name. </summary>
    internal readonly struct CsEvalEntryPointName
    {
        public CsEvalEntryPointName (
            string typeName,
            string methodName)
        {
            TypeName = typeName;
            MethodName = methodName;
        }

        public string TypeName { get; }

        public string MethodName { get; }

        public static bool TryParse (
            string? value,
            out CsEvalEntryPointName entryPointName,
            out string errorMessage)
        {
            entryPointName = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "Entry point must not be empty.";
                return false;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                errorMessage = "Entry point must not contain leading or trailing whitespace.";
                return false;
            }

            var separatorIndex = value.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                errorMessage = "Entry point must use Namespace.Type.Method form.";
                return false;
            }

            var typeName = value.Substring(0, separatorIndex);
            if (typeName.IndexOf('.') < 0)
            {
                errorMessage = "Entry point type must include a namespace.";
                return false;
            }

            var methodName = value.Substring(separatorIndex + 1);
            entryPointName = new CsEvalEntryPointName(typeName, methodName);
            errorMessage = string.Empty;
            return true;
        }
    }
}
