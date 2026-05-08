#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Represents a resolved <c>ucli.cs.eval</c> entry point. </summary>
    internal readonly struct CsEvalEntryPointName
    {
        public const string RequiredMethodName = "Run";

        public const string RequiredSignature = "public static object? Run(UcliCsEvalContext context)";

        public const string MatchRule = "Compiled source must contain exactly one public static object? Run(UcliCsEvalContext context) method.";

        public CsEvalEntryPointName (
            string displayName,
            string reflectionTypeName,
            string methodName)
        {
            DisplayName = displayName;
            ReflectionTypeName = reflectionTypeName;
            MethodName = methodName;
        }

        public string DisplayName { get; }

        public string ReflectionTypeName { get; }

        public string MethodName { get; }
    }
}
