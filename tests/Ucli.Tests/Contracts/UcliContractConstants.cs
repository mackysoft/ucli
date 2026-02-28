namespace MackySoft.Ucli.Tests;

internal static class UcliContractConstants
{
    internal static class CliOption
    {
        public const string Force = "--force";

        public const string OutputPath = "--outputPath";

        public const string ProjectPath = "--projectPath";

        public const string Unknown = "--unknown";
    }

    internal static class Config
    {
        public const int SchemaVersion = 1;

        public const string DefaultOperationAllowlistPattern = "^ucli\\.";

        public const string OperationPolicyDangerous = "dangerous";

        public const string OperationPolicySafe = "safe";

        public const string PlanTokenModeOptional = "optional";

        public const string PlanTokenModeRequired = "required";

        public const string ReadIndexModeDisabled = "disabled";

        public const string ReadIndexModeAllowStale = "allowStale";

        public const string ReadIndexModeRequireFresh = "requireFresh";
    }

    internal static class TestProfile
    {
        public const int SchemaVersion = 1;

        public const string ProjectPath = ".";

        public const string TestPlatformEditMode = "editmode";

        public const string OutputDir = ".ucli/local/artifacts";

        public const int TimeoutSeconds = 1800;
    }

    public const string LocalDirectoryIgnoreEntry = "local/";
}