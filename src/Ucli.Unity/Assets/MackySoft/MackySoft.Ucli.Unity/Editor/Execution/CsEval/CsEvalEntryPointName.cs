using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Represents a parsed <c>Namespace.Type.Method</c> entry point name. </summary>
    internal readonly struct CsEvalEntryPointName
    {
        public const string RequiredMethodName = "Run";

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

            var entryPoint = value!;
            if (!string.Equals(entryPoint, entryPoint.Trim(), StringComparison.Ordinal))
            {
                errorMessage = "Entry point must not contain leading or trailing whitespace.";
                return false;
            }

            var separatorIndex = entryPoint.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex == entryPoint.Length - 1)
            {
                errorMessage = "Entry point must use Namespace.Type.Method form.";
                return false;
            }

            var typeName = entryPoint.Substring(0, separatorIndex);
            if (typeName.IndexOf('.') < 0)
            {
                errorMessage = "Entry point type must include a namespace.";
                return false;
            }

            var methodName = entryPoint.Substring(separatorIndex + 1);
            if (!string.Equals(methodName, RequiredMethodName, StringComparison.Ordinal))
            {
                errorMessage = $"Entry point method must be '{RequiredMethodName}'.";
                return false;
            }

            entryPointName = new CsEvalEntryPointName(typeName, methodName);
            errorMessage = string.Empty;
            return true;
        }
    }
}
