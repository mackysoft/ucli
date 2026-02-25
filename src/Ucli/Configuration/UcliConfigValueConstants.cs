namespace MackySoft.Ucli.Configuration
{
    /// <summary> Defines string literals used in <c>.ucli/config.json</c> enum-like fields. </summary>
    internal static class UcliConfigValueConstants
    {
        /// <summary> Gets the operation policy value for <see cref="OperationPolicy.Safe" />. </summary>
        public const string OperationPolicySafe = "safe";

        /// <summary> Gets the operation policy value for <see cref="OperationPolicy.Advanced" />. </summary>
        public const string OperationPolicyAdvanced = "advanced";

        /// <summary> Gets the operation policy value for <see cref="OperationPolicy.Dangerous" />. </summary>
        public const string OperationPolicyDangerous = "dangerous";

        /// <summary> Gets the plan token mode value for <see cref="PlanTokenMode.Optional" />. </summary>
        public const string PlanTokenModeOptional = "optional";

        /// <summary> Gets the plan token mode value for <see cref="PlanTokenMode.Required" />. </summary>
        public const string PlanTokenModeRequired = "required";
    }
}
