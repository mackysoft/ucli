using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.SchemaGenerator;

internal static class Program
{
    private const string JsonSchemaDialect = "https://json-schema.org/draft/2020-12/schema";
    private const string SchemaSet = "ucli";
    private const string SchemaSetVersion = "v1";
    private const string SchemaBaseId = "https://schemas.mackysoft.dev/ucli/v1/";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static int Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!TryParseArgs(args, out var outputRoot, out var packageVersion, out var repositoryRoot))
        {
            WriteUsage();
            return 2;
        }

        try
        {
            packageVersion ??= ReadPackageVersion(repositoryRoot);
            WriteSchemas(outputRoot, packageVersion);
            Console.WriteLine($"Generated schemas: {Path.GetFullPath(outputRoot)}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void WriteSchemas (
        string outputRoot,
        string packageVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageVersion);

        var versionRoot = Path.Combine(outputRoot, SchemaSetVersion);
        if (Directory.Exists(versionRoot))
        {
            DeleteExistingVersionRoot(versionRoot);
        }

        Directory.CreateDirectory(versionRoot);

        var schemaFiles = CreateSchemaFiles();
        foreach (var schemaFile in schemaFiles)
        {
            var outputPath = Path.Combine(versionRoot, schemaFile.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? versionRoot);
            File.WriteAllText(outputPath, SerializeJson(schemaFile.Document));
        }

        File.WriteAllText(
            Path.Combine(versionRoot, "schema-manifest.json"),
            SerializeJson(CreateManifest(packageVersion, schemaFiles)));
    }

    private static IReadOnlyList<SchemaFile> CreateSchemaFiles ()
    {
        var files = new List<SchemaFile>
        {
            CreateSchema("cli-output/envelope.schema.json", "cli-output-envelope", null, CreateEnvelopeSchema()),
            CreateSchema("cli-output/defs/project.schema.json", "cli-output-def", null, CreateProjectSchema()),
            CreateSchema("cli-output/defs/read-index.schema.json", "cli-output-def", null, CreateReadIndexSchema()),
            CreateSchema("cli-output/defs/op-result.schema.json", "cli-output-def", null, CreateOperationResultSchema()),
            CreateSchema("cli-output/defs/post-read-source.schema.json", "cli-output-def", null, CreatePostReadSourceSchema()),
            CreateSchema("cli-output/defs/contract-violation.schema.json", "cli-output-def", null, CreateContractViolationSchema()),
            CreateSchema("cli-output/defs/diagnostic.schema.json", "cli-output-def", null, CreateDiagnosticSchema()),
            CreateSchema("cli-output/defs/touched.schema.json", "cli-output-def", null, CreateTouchedSchema()),
            CreateSchema("cli-output/defs/window.schema.json", "cli-output-def", null, CreateWindowSchema()),
            CreateSchema("cli-output/defs/verifier.schema.json", "cli-output-def", null, CreateVerifierSchema()),
            CreateSchema("cli-output/defs/assurance-claim.schema.json", "cli-output-def", null, CreateAssuranceClaimSchema()),
            CreateSchema("cli-output/defs/evidence.schema.json", "cli-output-def", null, CreateEvidenceSchema()),
            CreateSchema("cli-output/defs/report-ref.schema.json", "cli-output-def", null, CreateReportRefSchema()),
            CreateSchema("cli-output/defs/residual-risk.schema.json", "cli-output-def", null, CreateResidualRiskSchema()),
            CreatePayloadSchema(UcliCommandIds.Status.Name, CreateStatusPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Ready.Name, CreateReadyPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Compile.Name, CreateCompilePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.BuildRun.Name, CreateBuildRunPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Verify.Name, CreateVerifyPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Init.Name, CreateInitPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Validate.Name, CreateValidatePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Plan.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: true, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.Call.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: false, includePlanToken: false, includePlan: true, includePostReadSource: true)),
            CreatePayloadSchema(UcliCommandIds.Eval.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: false, includePlanToken: false, includePlan: true, includePostReadSource: true)),
            CreatePayloadSchema(UcliCommandIds.Refresh.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: false, includePlanToken: false, includePlan: false, includePostReadSource: true)),
            CreatePayloadSchema(UcliCommandIds.Resolve.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: false, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.QueryAssetsFind.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: false, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.QuerySceneTree.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: false, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.QueryGoDescribe.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: false, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.QueryCompSchema.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: false, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.QueryAssetSchema.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: false, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.OpsList.Name, CreateOpsListPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.OpsDescribe.Name, CreateOpsDescribePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.CodesList.Name, CreateCodesListPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.CodesDescribe.Name, CreateCodesDescribePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.SkillsList.Name, CreateSkillsListPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.PlayStatus.Name, CreatePlayStatusPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.PlayEnter.Name, CreatePlayEnterPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.PlayExit.Name, CreatePlayExitPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.ScreenshotGame.Name, CreateScreenshotGamePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.ScreenshotScene.Name, CreateScreenshotScenePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.DaemonStart.Name, CreateDaemonStartPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.DaemonStatus.Name, CreateDaemonStatusPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.TestProfileInit.Name, CreateTestProfileInitPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.TestRun.Name, CreateTestRunPayloadSchema()),
            CreateSchema("request/request-envelope.schema.json", "request-envelope", null, CreateRequestEnvelopeSchema()),
            CreateSchema("request/edit-dsl.schema.json", "request-edit-dsl", null, CreateEditDslSchema()),
        };

        return files;
    }

    private static SchemaFile CreatePayloadSchema (
        string command,
        Dictionary<string, object?> schema)
    {
        return CreateSchema(
            $"cli-output/payload/{command}.schema.json",
            "cli-output-payload",
            command,
            schema);
    }

    private static SchemaFile CreateSchema (
        string relativePath,
        string kind,
        string? command,
        Dictionary<string, object?> schema)
    {
        var document = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$schema"] = JsonSchemaDialect,
            ["$id"] = SchemaBaseId + relativePath,
        };

        foreach (var (key, value) in schema)
        {
            document[key] = value;
        }

        return new SchemaFile(relativePath, kind, command, document);
    }

    private static Dictionary<string, object?> CreateManifest (
        string packageVersion,
        IReadOnlyList<SchemaFile> schemaFiles)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaSet"] = SchemaSet,
            ["schemaSetVersion"] = SchemaSetVersion,
            ["protocolVersion"] = IpcProtocol.CurrentVersion,
            ["packageVersion"] = packageVersion,
            ["jsonSchemaDialect"] = JsonSchemaDialect,
            ["schemas"] = schemaFiles
                .Select(static schemaFile =>
                {
                    var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["$id"] = SchemaBaseId + schemaFile.RelativePath,
                        ["path"] = schemaFile.RelativePath,
                        ["kind"] = schemaFile.Kind,
                    };
                    if (schemaFile.Command != null)
                    {
                        entry["command"] = schemaFile.Command;
                    }

                    return entry;
                })
                .ToArray(),
        };
    }

    private static Dictionary<string, object?> CreateEnvelopeSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("protocolVersion", ConstInteger(IpcProtocol.CurrentVersion)),
            Required("command", StringSchema()),
            Required("status", EnumSchema(IpcProtocol.StatusOk, IpcProtocol.StatusError)),
            Required("exitCode", IntegerSchema()),
            Required("message", StringSchema()),
            Required("payload", ObjectSchema(additionalProperties: true)),
            Required("errors", ArraySchema(ObjectSchema(
                additionalProperties: false,
                Required("code", NullableStringSchema()),
                Required("message", NullableStringSchema()),
                Required("opId", NullableStringSchema())))));
    }

    private static Dictionary<string, object?> CreateProjectSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("projectPath", StringSchema()),
            Required("projectFingerprint", StringSchema()),
            Required("unityVersion", StringSchema()));
    }

    private static Dictionary<string, object?> CreateReadIndexSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("used", BooleanSchema()),
            Required("hit", BooleanSchema()),
            Required("source", NullableStringSchema()),
            Required("freshness", NullableStringSchema()),
            Required("generatedAtUtc", NullableStringSchema()),
            Required("fallbackReason", NullableStringSchema()));
    }

    private static Dictionary<string, object?> CreateOperationResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("opId", StringSchema()),
            Required("op", StringSchema()),
            Required("phase", StringSchema()),
            Required("applied", BooleanSchema()),
            Required("changed", BooleanSchema()),
            Required("touched", ArraySchema(ReferenceSchema("../defs/touched.schema.json"))),
            Required("diagnostics", ArraySchema(ReferenceSchema("../defs/diagnostic.schema.json"))),
            Optional("result", AnySchema()),
            Optional("errors", ArraySchema(ObjectSchema(additionalProperties: true))));
    }

    private static Dictionary<string, object?> CreatePostReadSourceSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("schemaVersion", ConstInteger(1)),
            Required("steps", ArraySchema(ObjectSchema(
                additionalProperties: false,
                Required("opId", StringSchema()),
                Required(
                    "sourceKind",
                    EnumSchema(
                        IpcExecutePostReadSourceKindNames.Edit,
                        IpcExecutePostReadSourceKindNames.Operation,
                        IpcExecutePostReadSourceKindNames.Refresh)),
                Required("playModeMutation", BooleanSchema()),
                Required(
                    "commit",
                    EnumValueSchema(
                        IpcExecutePostReadCommitNames.None,
                        IpcExecutePostReadCommitNames.Context,
                        IpcExecutePostReadCommitNames.Project,
                        null)),
                Required("persistenceExpected", BooleanSchema()),
                Required(
                    "expectedPostState",
                    EnumSchema(
                        IpcExecuteExpectedPostStateNames.Deterministic,
                        IpcExecuteExpectedPostStateNames.Unavailable))))));
    }

    private static Dictionary<string, object?> CreateDiagnosticSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Optional("code", NullableStringSchema()),
            Optional("message", NullableStringSchema()),
            Optional("severity", NullableStringSchema()));
    }

    private static Dictionary<string, object?> CreateTouchedSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Optional("kind", NullableStringSchema()),
            Optional("path", NullableStringSchema()),
            Optional("uri", NullableStringSchema()),
            Optional("state", NullableStringSchema()));
    }

    private static Dictionary<string, object?> CreateContractViolationSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("opId", StringSchema()),
            Required("operation", StringSchema()),
            Required("expectedFact", StringSchema()),
            Required("observedResult", StringSchema()),
            Required(
                "applicationState",
                EnumSchema(
                    IpcExecuteApplicationStateNames.NotApplied,
                    IpcExecuteApplicationStateNames.Applied,
                    IpcExecuteApplicationStateNames.Indeterminate,
                    IpcExecuteApplicationStateNames.Unknown)));
    }

    private static Dictionary<string, object?> CreateWindowSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("limit", NullableIntegerSchema()),
            Required("cursor", NullableStringSchema()),
            Required("nextCursor", NullableStringSchema()),
            Required("isComplete", BooleanSchema()),
            Required("totalCount", NullableIntegerSchema()));
    }

    private static Dictionary<string, object?> CreateVerifierSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("id", StringSchema()),
            Required("kind", StringSchema()),
            Required("deterministic", BooleanSchema()),
            Required("required", BooleanSchema()),
            Required("primaryClaims", ArraySchema(StringSchema())),
            Required("effects", ArraySchema(StringSchema())),
            Optional("reportRef", StringSchema()));
    }

    private static Dictionary<string, object?> CreateAssuranceClaimSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("id", StringSchema()),
            Required("status", StringSchema()),
            Required("coverage", StringSchema()),
            Required("required", BooleanSchema()),
            Required("verifierRef", StringSchema()),
            Optional("evidence", ArraySchema(ReferenceSchema("../defs/evidence.schema.json"))),
            Optional("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))));
    }

    private static Dictionary<string, object?> CreateEvidenceSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("kind", StringSchema()),
            Optional("evidenceRef", StringSchema()),
            Optional("data", AnySchema()));
    }

    private static Dictionary<string, object?> CreateReportRefSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oneOf"] = new object?[]
            {
                ObjectSchema(
                    additionalProperties: false,
                    Required("path", StringSchema()),
                    Optional("digest", Sha256LowerHexSchema())),
                ObjectSchema(
                    additionalProperties: false,
                    Required("uri", StringSchema()),
                    Optional("digest", Sha256LowerHexSchema())),
            },
        };
    }

    private static Dictionary<string, object?> CreateResidualRiskSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("code", StringSchema()),
            Required("blocking", BooleanSchema()));
    }

    private static Dictionary<string, object?> CreateStatusPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("daemonStatus", NullableStringSchema()),
            Optional("unityVersion", NullableStringSchema()),
            Optional("serverVersion", NullableStringSchema()),
            Optional("lifecycleState", NullableStringSchema()),
            Optional("blockingReason", NullableStringSchema()),
            Optional("compileState", NullableStringSchema()),
            Optional("generations", NullableUnityGenerationSnapshotSchema()),
            Optional("canAcceptExecutionRequests", BooleanSchema()),
            Optional("editorMode", NullableStringSchema()),
            Optional("observedAtUtc", NullableStringSchema()),
            Optional("actionRequired", NullableStringSchema()),
            Optional("primaryDiagnostic", NullableObjectSchema()),
            Optional("playMode", NullablePlayModeSnapshotSchema()),
            Optional("timeoutMilliseconds", IntegerSchema()));
    }

    private static Dictionary<string, object?> CreateReadyPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("verdict", EnumSchema("pass", "fail", "incomplete")),
            Required("project", ReferenceSchema("../defs/project.schema.json")),
            Required("verifiers", ArraySchema(ReferenceSchema("../defs/verifier.schema.json"))),
            Required("claims", ArraySchema(CreateReadyClaimSchema())),
            Required("reports", ObjectSchema(additionalProperties: true)),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))),
            Required("target", EnumSchema("execution", "mutation", "test", "readIndex")),
            Required("requestedMode", EnumSchema("auto", "daemon", "oneshot")),
            Required("resolvedMode", EnumSchema("daemon", "oneshot", "notApplicable")),
            Required("sessionKind", EnumSchema("daemon", "transientProbe", "artifactOnly")),
            Required("timeoutMilliseconds", IntegerSchema()),
            Required("lifecycle", CreateReadyLifecycleSchema()),
            Required("readIndex", CreateReadyReadIndexSchema()));
    }

    private static Dictionary<string, object?> CreateReadyLifecycleSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new object?[]
            {
                "object",
                "null",
            },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["serverVersion"] = NullableStringSchema(),
                ["unityVersion"] = NullableStringSchema(),
                ["editorMode"] = NullableStringSchema(),
                ["lifecycleState"] = NullableStringSchema(),
                ["blockingReason"] = NullableStringSchema(),
                ["compileState"] = NullableStringSchema(),
                ["generations"] = NullableUnityGenerationSnapshotSchema(),
                ["canAcceptExecutionRequests"] = BooleanSchema(),
                ["observedAtUtc"] = NullableStringSchema(),
                ["actionRequired"] = NullableStringSchema(),
                ["primaryDiagnostic"] = CreatePrimaryDiagnosticSchema(),
                ["playMode"] = NullablePlayModeSnapshotSchema(),
            },
        };
    }

    private static Dictionary<string, object?> CreatePrimaryDiagnosticSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new object?[]
            {
                "object",
                "null",
            },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = StringSchema(),
                ["code"] = NullableStringSchema(),
                ["file"] = NullableStringSchema(),
                ["line"] = NullableIntegerSchema(),
                ["column"] = NullableIntegerSchema(),
                ["message"] = NullableStringSchema(),
            },
            ["required"] = new[]
            {
                "kind",
            },
        };
    }

    private static Dictionary<string, object?> CreateReadyClaimSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("id", StringSchema()),
            Required("status", StringSchema()),
            Required("coverage", StringSchema()),
            Required("required", BooleanSchema()),
            Required("verifierRef", StringSchema()),
            Required("statement", StringSchema()),
            Required("subject", ObjectSchema(additionalProperties: true)),
            Required("validity", CreateReadyClaimValiditySchema()),
            Required("evidence", ArraySchema(ReferenceSchema("../defs/evidence.schema.json"))),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))));
    }

    private static Dictionary<string, object?> CreateReadyClaimValiditySchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("kind", EnumSchema("sessionBound", "probeOnly")),
            Required("guaranteesReusableSession", BooleanSchema()));
    }

    private static Dictionary<string, object?> CreateReadyReadIndexSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new object?[]
            {
                "object",
                "null",
            },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = EnumSchema("allowStale", "requireFresh"),
                ["artifacts"] = ArraySchema(CreateReadyReadIndexArtifactSchema()),
            },
            ["required"] = new[]
            {
                "mode",
                "artifacts",
            },
        };
    }

    private static Dictionary<string, object?> CreateReadyReadIndexArtifactSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required("status", EnumSchema("available", "failed")),
            Required("required", BooleanSchema()),
            Optional("freshness", NullableStringSchema()),
            Optional("sourceInputsHash", NullableStringSchema()),
            Optional("generatedAtUtc", NullableStringSchema()),
            Optional("code", NullableStringSchema()),
            Optional("message", NullableStringSchema()),
            Optional("actionRequired", NullableStringSchema()));
    }

    private static Dictionary<string, object?> CreateCompilePayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("verdict", EnumSchema("pass", "fail", "incomplete")),
            Required("project", ReferenceSchema("../defs/project.schema.json")),
            Required("verifiers", ArraySchema(ReferenceSchema("../defs/verifier.schema.json"))),
            Required("claims", ArraySchema(CreateCompileClaimSchema())),
            Required("reports", ObjectSchema(additionalProperties: true)),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))),
            Required("requestedMode", EnumSchema("auto", "daemon", "oneshot")),
            Required("resolvedMode", EnumSchema("daemon", "oneshot")),
            Required("sessionKind", EnumSchema("daemon", "transientProbe")),
            Required("timeoutMilliseconds", IntegerSchema()),
            Required("compile", CreateCompileEvidenceSchema()));
    }

    private static Dictionary<string, object?> CreateCompileClaimSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("id", StringSchema()),
            Required("status", StringSchema()),
            Required("coverage", StringSchema()),
            Required("required", BooleanSchema()),
            Required("verifierRef", StringSchema()),
            Required("statement", StringSchema()),
            Required("subject", ObjectSchema(additionalProperties: true)),
            Required("evidence", ArraySchema(ReferenceSchema("../defs/evidence.schema.json"))),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))));
    }

    private static Dictionary<string, object?> CreateCompileEvidenceSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("runId", StringSchema()),
            Required("refresh", ObjectSchema(
                additionalProperties: false,
                Required("origin", EnumSchema("assetDatabaseRefresh", "diagnosticsRead")),
                Required("requested", BooleanSchema()),
                Required("startedAtUtc", StringSchema()),
                Required("completedAtUtc", NullableStringSchema()),
                Required("completed", BooleanSchema()))),
            Required("scriptCompilation", ObjectSchema(
                additionalProperties: false,
                Required("started", BooleanSchema()),
                Required("completed", BooleanSchema()),
                Required("compileGenerationBefore", NullableNonNegativeIntegerSchema()),
                Required("compileGenerationAfter", NullableNonNegativeIntegerSchema()),
                Required("diagnostics", ObjectSchema(
                    additionalProperties: false,
                    Required("errorCount", IntegerSchema()),
                    Required("warningCount", IntegerSchema()),
                    Required("primaryDiagnostic", CreatePrimaryDiagnosticSchema()))))),
            Required("domainReload", ObjectSchema(
                additionalProperties: false,
                Required("reloadRequired", BooleanSchema()),
                Required("reloadObserved", BooleanSchema()),
                Required("generationBefore", NullableNonNegativeIntegerSchema()),
                Required("generationAfter", NullableNonNegativeIntegerSchema()),
                Required("settled", BooleanSchema()))),
            Required("lifecycle", ObjectSchema(
                additionalProperties: false,
                Required("serverVersion", NullableStringSchema()),
                Required("unityVersion", NullableStringSchema()),
                Required("editorMode", NullableStringSchema()),
                Required("lifecycleState", NullableStringSchema()),
                Required("blockingReason", NullableStringSchema()),
                Required("compileState", NullableStringSchema()),
                Required("generations", NullableUnityGenerationSnapshotSchema()),
                Required("canAcceptExecutionRequests", BooleanSchema()),
                Required("observedAtUtc", NullableStringSchema()),
                Required("actionRequired", NullableStringSchema()),
                Required("primaryDiagnostic", CreatePrimaryDiagnosticSchema()))));
    }

    private static Dictionary<string, object?> CreateBuildRunPayloadSchema ()
    {
        var schema = OneOfSchema(
            CreateBuildRunSuccessPayloadSchema(),
            CreateBuildRunFailurePayloadSchema());

        schema["$defs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["generation"] = CreateUnityGenerationSnapshotSchema(),
        };

        return schema;
    }

    private static Dictionary<string, object?> CreateBuildRunSuccessPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("verdict", EnumSchema("pass", "fail", "incomplete")),
            Required("project", ReferenceSchema("../defs/project.schema.json")),
            Required("build", CreateBuildRunBuildSchema()),
            Required("verifiers", ArraySchema(ReferenceSchema("../defs/verifier.schema.json"))),
            Required("claims", ArraySchema(CreateBuildRunClaimSchema())),
            Required("reports", CreateBuildRunReportsSchema()),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))));
    }

    private static Dictionary<string, object?> CreateBuildRunFailurePayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("project", ReferenceSchema("../defs/project.schema.json")),
            Optional("dirtyState", CreateBuildRunDirtyStateSchema()),
            Optional("startup", ObjectSchema(additionalProperties: true)),
            Optional("diagnosis", ObjectSchema(additionalProperties: true)),
            Optional("retryDisposition", StringSchema()),
            Optional("safeToRetryImmediately", BooleanSchema()));
    }

    private static Dictionary<string, object?> CreateBuildRunBuildSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("runId", StringSchema()),
            Required("profile", ObjectSchema(
                additionalProperties: false,
                Required("path", StringSchema()),
                Required("digest", Sha256LowerHexSchema()))),
            Required("inputs", CreateBuildRunInputsSchema()),
            Required("runner", CreateBuildRunRunnerSchema()),
            Required("runnerResult", CreateBuildRunRunnerResultSchema()),
            Required("output", ObjectSchema(
                additionalProperties: false,
                Required("manifestRef", ConstString("buildOutputManifest")),
                Required("manifestDigest", Sha256LowerHexSchema()),
                Required("entryCount", IntegerSchema()),
                Required("fileCount", IntegerSchema()),
                Required("totalBytes", IntegerSchema()))),
            Required("generations", ObjectSchema(
                additionalProperties: false,
                Required("before", ReferenceSchema("#/$defs/generation")),
                Required("after", ReferenceSchema("#/$defs/generation")),
                Required("validFor", ReferenceSchema("#/$defs/generation")))),
            Required("summary", ObjectSchema(
                additionalProperties: false,
                Required(
                    "result",
                    BuildRunTerminalResultSchema()),
                Required("durationMilliseconds", IntegerSchema()),
                Required("errorCount", IntegerSchema()),
                Required("warningCount", IntegerSchema()),
                Optional("reportRef", ConstString("buildReport")))),
            Required("logs", ObjectSchema(
                additionalProperties: false,
                Required("reportRef", ConstString("buildLog")),
                Required("entryCount", IntegerSchema()),
                Required("errorCount", IntegerSchema()),
                Required("warningCount", IntegerSchema()),
                Required(
                    "completionReason",
                    EnumSchema(
                        Literal(IpcBuildLogCompletionReason.Completed),
                        Literal(IpcBuildLogCompletionReason.Failed),
                    Literal(IpcBuildLogCompletionReason.Canceled))),
                Required("window", ObjectSchema(
                    additionalProperties: false,
                    Required("startedAtUtc", StringSchema()),
                    Required("completedAtUtc", StringSchema()),
                    Required("cursorStart", NullableStringSchema()),
                    Required("cursorEnd", NullableStringSchema()))))));
    }

    private static Dictionary<string, object?> CreateBuildRunInputsSchema ()
    {
        return OneOfSchema(
            CreateBuildRunExplicitInputsSchema(),
            CreateBuildRunUnityBuildProfileInputsSchema());
    }

    private static Dictionary<string, object?> CreateBuildRunExplicitInputsSchema ()
    {
        return CreateBuildRunResolvedInputsSchema(
            inputKind: Literal(BuildProfileInputsKind.Explicit),
            sceneSources:
            [
                Literal(BuildProfileSceneSource.EditorBuildSettings),
                Literal(BuildProfileSceneSource.Explicit),
            ],
            extraProperties: []);
    }

    private static Dictionary<string, object?> CreateBuildRunUnityBuildProfileInputsSchema ()
    {
        return CreateBuildRunResolvedInputsSchema(
            inputKind: Literal(BuildProfileInputsKind.UnityBuildProfile),
            sceneSources:
            [
                Literal(BuildProfileSceneSource.UnityBuildProfile),
            ],
            extraProperties:
            [
                Required("unityBuildProfile", ObjectSchema(
                    additionalProperties: false,
                    Required("path", CreateBuildRunUnityBuildProfilePathSchema()),
                    Required("digest", Sha256LowerHexSchema()))),
            ]);
    }

    private static Dictionary<string, object?> CreateBuildRunResolvedInputsSchema (
        string inputKind,
        string[] sceneSources,
        SchemaProperty[] extraProperties)
    {
        var properties = new List<SchemaProperty>
        {
            Required("inputKind", ConstString(inputKind)),
            Required("target", ObjectSchema(
                additionalProperties: false,
                Required("stableName", StringSchema()),
                Required("unityBuildTarget", StringSchema()))),
            Required("scenes", ObjectSchema(
                additionalProperties: false,
                Required("source", EnumSchema(sceneSources)),
                Required("paths", ArraySchema(StringSchema())))),
            Required("options", ObjectSchema(
                additionalProperties: false,
                Required("development", BooleanSchema()))),
        };
        properties.AddRange(extraProperties);

        return ObjectSchema(
            additionalProperties: false,
            properties.ToArray());
    }

    private static Dictionary<string, object?> CreateBuildRunRunnerSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required(
                "kind",
                EnumSchema(
                    Literal(IpcBuildRunnerKind.BuildPipeline),
                    Literal(IpcBuildRunnerKind.ExecuteMethod))),
            Required("method", NullableStringSchema()),
            Required("invocation", ObjectSchema(
                additionalProperties: false,
                Required("arguments", StringMapSchema()),
                Required("environment", ObjectSchema(
                    additionalProperties: false,
                    Required("variables", ArraySchema(StringSchema())),
                    Required("secrets", ArraySchema(StringSchema())))))));
    }

    private static Dictionary<string, object?> CreateBuildRunRunnerResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required(
                "source",
                EnumSchema(
                    Literal(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                    Literal(IpcBuildRunnerResultSource.UcliBuildRunnerResult))),
            Required(
                "status",
                BuildRunTerminalResultSchema()));
    }

    private static Dictionary<string, object?> BuildRunTerminalResultSchema ()
    {
        return EnumSchema(
            Literal(IpcBuildReportResult.Succeeded),
            Literal(IpcBuildReportResult.Failed),
            Literal(IpcBuildReportResult.Canceled));
    }

    private static Dictionary<string, object?> CreateBuildRunUnityBuildProfilePathSchema ()
    {
        // NOTE: Keep this pattern aligned with UnityAssetPathContract.IsNormalizedBuildProfileAssetPath.
        return PatternStringSchema(@"^(?!.*[\u0000-\u001F])(?!.*(?:^|/)\.{1,2}(?:/|$))(?!.*//)Assets/(?!.*\\)(?!.*:)(?!.*\.[Mm][Ee][Tt][Aa]$)(?!.*\s$).+$");
    }

    private static Dictionary<string, object?> CreateBuildRunOptionsSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("development", BooleanSchema()));
    }

    private static Dictionary<string, object?> CreateBuildRunClaimSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("id", StringSchema()),
            Required("status", EnumSchema("passed", "failed", "indeterminate", "unverified")),
            Required("coverage", EnumSchema("full", "none")),
            Required("required", BooleanSchema()),
            Required("verifierRef", StringSchema()),
            Required("statement", StringSchema()),
            Required("subject", ObjectSchema(additionalProperties: true)),
            Required("evidence", ArraySchema(ReferenceSchema("../defs/evidence.schema.json"))),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))));
    }

    private static Dictionary<string, object?> CreateBuildRunReportsSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("build", CreateBuildRunReportRefSchema()),
            Optional("buildReport", CreateBuildRunReportRefSchema()),
            Required("buildOutputManifest", CreateBuildRunReportRefSchema()),
            Required("buildLog", CreateBuildRunReportRefSchema()));
    }

    private static Dictionary<string, object?> CreateBuildRunReportRefSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("path", StringSchema()),
            Required("digest", Sha256LowerHexSchema()));
    }

    private static Dictionary<string, object?> CreateBuildRunDirtyStateSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("checked", BooleanSchema()),
            Required("dirty", BooleanSchema()),
            Required(
                "coverage",
                EnumSchema(
                    Literal(IpcBuildDirtyStateCoverage.Full),
                    Literal(IpcBuildDirtyStateCoverage.Partial))),
            Required("items", ArraySchema(ObjectSchema(
                additionalProperties: false,
                Required(
                    "kind",
                    EnumSchema(
                        Literal(IpcBuildDirtyStateItemKind.Scene),
                        Literal(IpcBuildDirtyStateItemKind.Prefab),
                        Literal(IpcBuildDirtyStateItemKind.Asset),
                        Literal(IpcBuildDirtyStateItemKind.ProjectSettings),
                        Literal(IpcBuildDirtyStateItemKind.Unknown))),
                Required("path", StringSchema())))));
    }

    private static Dictionary<string, object?> CreateVerifyPayloadSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oneOf"] = new object?[]
            {
                CreateVerifySuccessPayloadSchema(),
                CreateVerifyErrorPayloadSchema(),
            },
        };
    }

    private static Dictionary<string, object?> CreateVerifySuccessPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("profile", CreateVerifyProfileSchema()),
            Required("verdict", EnumSchema("pass", "fail", "incomplete")),
            Required("project", ReferenceSchema("../defs/project.schema.json")),
            Required("verifiers", ArraySchema(ReferenceSchema("../defs/verifier.schema.json"))),
            Required("claims", ArraySchema(CreateVerifyClaimSchema())),
            Required("reports", ObjectSchema(additionalProperties: true)),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))),
            Required("timeoutMilliseconds", IntegerSchema()));
    }

    private static Dictionary<string, object?> CreateVerifyErrorPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("project", ReferenceSchema("../defs/project.schema.json")),
            Optional("startup", ObjectSchema(additionalProperties: true)),
            Optional("diagnosis", ObjectSchema(additionalProperties: true)),
            Optional("retryDisposition", StringSchema()),
            Optional("safeToRetryImmediately", BooleanSchema()));
    }

    private static Dictionary<string, object?> CreateVerifyProfileSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("source", EnumSchema("builtIn", "file")),
            Required("name", StringSchema()),
            Required("path", NullableStringSchema()),
            Required("digest", Sha256LowerHexSchema()));
    }

    private static Dictionary<string, object?> CreateVerifyClaimSchema ()
    {
        return ObjectSchema(
            additionalProperties: true,
            Required("id", StringSchema()),
            Required("status", EnumSchema("passed", "failed", "indeterminate", "unverified", "outOfScope")),
            Required("coverage", EnumSchema("full", "partial", "none")),
            Required("required", BooleanSchema()),
            Required("verifierRef", StringSchema()),
            Required("statement", StringSchema()),
            Required("subject", ObjectSchema(additionalProperties: true)),
            Required("evidence", ArraySchema(ReferenceSchema("../defs/evidence.schema.json"))),
            Required("residualRisks", ArraySchema(ReferenceSchema("../defs/residual-risk.schema.json"))),
            Optional("validity", ObjectSchema(additionalProperties: true)));
    }

    private static Dictionary<string, object?> CreateInitPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("configPath", StringSchema()),
            Optional("gitignorePath", StringSchema()));
    }

    private static Dictionary<string, object?> CreateValidatePayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("project", ReferenceSchema("../defs/project.schema.json")),
            Optional("readIndex", ReferenceSchema("../defs/read-index.schema.json")));
    }

    private static Dictionary<string, object?> CreateRequestExecutionPayloadSchema (
        bool includeReadIndex,
        bool includePlanToken,
        bool includePlan,
        bool includePostReadSource)
    {
        var properties = new List<SchemaProperty>
        {
            Optional("requestId", StringSchema()),
            Optional("project", ReferenceSchema("../defs/project.schema.json")),
            Optional("opResults", ArraySchema(ReferenceSchema("../defs/op-result.schema.json"))),
            Optional("contractViolations", ArraySchema(ReferenceSchema("../defs/contract-violation.schema.json"))),
            Optional("readPostcondition", ObjectSchema(additionalProperties: true)),
        };

        if (includeReadIndex)
        {
            properties.Add(Optional("readIndex", ReferenceSchema("../defs/read-index.schema.json")));
        }

        if (includePlanToken)
        {
            properties.Add(Optional("planToken", StringSchema()));
        }

        if (includePlan)
        {
            properties.Add(Optional("plan", CreateCallPlanPayloadSchema()));
        }

        if (includePostReadSource)
        {
            properties.Add(Optional("postReadSource", ReferenceSchema("../defs/post-read-source.schema.json")));
        }

        return ObjectSchema(additionalProperties: false, properties.ToArray());
    }

    private static Dictionary<string, object?> CreateCallPlanPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("requestId", StringSchema()),
            Optional("project", ReferenceSchema("../defs/project.schema.json")),
            Optional("opResults", ArraySchema(ReferenceSchema("../defs/op-result.schema.json"))),
            Optional("contractViolations", ArraySchema(ReferenceSchema("../defs/contract-violation.schema.json"))),
            Optional("planToken", StringSchema()));
    }

    private static Dictionary<string, object?> CreateOpsListPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("operations", ArraySchema(ObjectSchema(
                additionalProperties: false,
                Required("name", StringSchema()),
                Required("kind", StringSchema()),
                Required("policy", StringSchema()),
                Required("description", StringSchema())))),
            Optional("readIndex", ReferenceSchema("../defs/read-index.schema.json")));
    }

    private static Dictionary<string, object?> CreateOpsDescribePayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("operation", CreateOpsDescribeOperationSchema()),
            Optional("readIndex", ReferenceSchema("../defs/read-index.schema.json")));
    }

    private static Dictionary<string, object?> CreateOpsDescribeOperationSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required(
                "kind",
                EnumSchema(
                    Literal(UcliOperationKind.Query),
                    Literal(UcliOperationKind.Command),
                    Literal(UcliOperationKind.Mutation))),
            Required(
                "policy",
                EnumSchema(
                    Literal(OperationPolicy.Safe),
                    Literal(OperationPolicy.Advanced),
                    Literal(OperationPolicy.Dangerous))),
            Required(
                "playModeSupport",
                EnumSchema(
                    Literal(UcliOperationPlayModeSupport.Disallowed),
                    Literal(UcliOperationPlayModeSupport.Allowed),
                    Literal(UcliOperationPlayModeSupport.Required))),
            Required("description", StringSchema()),
            Required("inputs", ArraySchema(CreateOpsDescribeInputSchema())),
            Required("resultContract", ObjectSchema(
                additionalProperties: false,
                Required("emitted", BooleanSchema()),
                Required("resultType", StringSchema()),
                Required("description", StringSchema()))),
            Required("assurance", CreateOpsDescribeAssuranceSchema()),
            Optional("codeContract", CreateOpsDescribeCodeContractSchema()),
            Required("argsSchema", ObjectSchema(additionalProperties: true)),
            Required("resultSchema", NullableObjectSchema()));
    }

    private static Dictionary<string, object?> CreateOpsDescribeAssuranceSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("sideEffects", ArraySchema(EnumSchema(
                UcliOperationSideEffectDescriptors.SupportedValues.ToArray()))),
            Required("mayDirty", BooleanSchema()),
            Required("mayPersist", BooleanSchema()),
            Required("touchedKinds", ArraySchema(EnumSchema(
                UcliTouchedResourceKindNames.Scene,
                UcliTouchedResourceKindNames.Prefab,
                UcliTouchedResourceKindNames.Asset,
                UcliTouchedResourceKindNames.ProjectSettings))),
            Required("planMode", EnumSchema(
                Literal(UcliOperationPlanMode.ValidationOnly),
                Literal(UcliOperationPlanMode.ObservesLiveUnity))),
            Required("planSemantics", StringSchema()),
            Required("callSemantics", StringSchema()),
            Required("touchedContract", StringSchema()),
            Required("readPostconditionContract", StringSchema()),
            Required("failureSemantics", StringSchema()),
            Required("dangerousNotes", ArraySchema(StringSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeInputSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required("description", StringSchema()),
            Required(
                "valueType",
                EnumSchema(
                    "string",
                    "boolean",
                    "integer",
                    "number",
                    "object",
                    "array")),
            Required("constraints", ArraySchema(CreateOpsDescribeInputConstraintSchema())),
            Optional("argsPath", InputArgsPathSchema()),
            Optional("variants", ArraySchema(CreateOpsDescribeInputVariantSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeInputVariantSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required("description", StringSchema()),
            Required("fields", ArraySchema(CreateOpsDescribeInputVariantFieldSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeInputVariantFieldSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required("argsPath", VariantFieldArgsPathSchema()),
            Required("description", StringSchema()),
            Required("constraints", ArraySchema(CreateOpsDescribeInputConstraintSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeInputConstraintSchema ()
    {
        return OneOfSchema(
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.NonEmpty)),
            RangeConstraintSchema(Required("min", NumberSchema())),
            RangeConstraintSchema(Required("max", NumberSchema())),
            RangeConstraintSchema(
                Required("min", NumberSchema()),
                Required("max", NumberSchema())),
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.ProjectRelativePath)),
            AssetConstraintSchema(Literal(UcliOperationInputConstraintKind.AssetExists)),
            AssetConstraintSchema(Literal(UcliOperationInputConstraintKind.AssetCreatable)),
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.GlobalObjectId)),
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.HierarchyPath)),
            ReferenceResolvableConstraintSchema(),
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.TypeExists)),
            TypeAssignableToConstraintSchema(),
            SerializedPropertyConstraintSchema(),
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.AssetGuid)),
            ConstraintSchema(Literal(UcliOperationInputConstraintKind.Cursor)));
    }

    private static Dictionary<string, object?> ConstraintSchema (
        string kind,
        params SchemaProperty[] parameters)
    {
        var properties = new List<SchemaProperty>(parameters.Length + 1)
        {
            Required("kind", ConstString(kind)),
        };
        properties.AddRange(parameters);
        return ObjectSchema(additionalProperties: false, properties.ToArray());
    }

    private static Dictionary<string, object?> RangeConstraintSchema (params SchemaProperty[] parameters)
    {
        return ConstraintSchema(Literal(UcliOperationInputConstraintKind.Range), parameters);
    }

    private static Dictionary<string, object?> AssetConstraintSchema (string kind)
    {
        return ConstraintSchema(
            kind,
            Required(
                "assetKind",
                EnumSchema(
                    Literal(UcliOperationAssetKind.Asset),
                    Literal(UcliOperationAssetKind.Prefab),
                    Literal(UcliOperationAssetKind.ProjectSettings),
                    Literal(UcliOperationAssetKind.Scene))));
    }

    private static Dictionary<string, object?> ReferenceResolvableConstraintSchema ()
    {
        return ConstraintSchema(
            Literal(UcliOperationInputConstraintKind.ReferenceResolvable),
            Required(
                "targetKind",
                EnumSchema(
                    Literal(UcliOperationReferenceTargetKind.Asset),
                    Literal(UcliOperationReferenceTargetKind.Component),
                    Literal(UcliOperationReferenceTargetKind.GameObject))));
    }

    private static Dictionary<string, object?> TypeAssignableToConstraintSchema ()
    {
        return ConstraintSchema(
            Literal(UcliOperationInputConstraintKind.TypeAssignableTo),
            Required(
                "typeKind",
                EnumSchema(Literal(UcliOperationTypeKind.Component))));
    }

    private static Dictionary<string, object?> SerializedPropertyConstraintSchema ()
    {
        return ConstraintSchema(
            Literal(UcliOperationInputConstraintKind.SerializedProperty),
            Required(
                "access",
                EnumSchema(Literal(UcliOperationSerializedPropertyAccess.Write))));
    }

    private static Dictionary<string, object?> CreateOpsDescribeCodeContractSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("language", StringSchema()),
            Required("entryPoint", CreateOpsDescribeCodeEntryPointSchema()),
            Required("sourceForms", ArraySchema(CreateOpsDescribeCodeSourceFormSchema())),
            Required("apiTypes", ArraySchema(CreateOpsDescribeCodeApiTypeSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeCodeEntryPointSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("signature", StringSchema()),
            Required("matchRule", StringSchema()),
            Required("requiredStatic", BooleanSchema()),
            Required("parameterTypes", ArraySchema(StringSchema())),
            Required("returnValue", StringSchema()));
    }

    private static Dictionary<string, object?> CreateOpsDescribeCodeSourceFormSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("kind", EnumSchema(
                CsEvalSourceKindValues.CompilationUnit,
                CsEvalSourceKindValues.Snippet)),
            Required("description", StringSchema()));
    }

    private static Dictionary<string, object?> CreateOpsDescribeCodeApiTypeSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required("fullName", StringSchema()),
            Required("description", StringSchema()),
            Required("members", ArraySchema(CreateOpsDescribeCodeApiMemberSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeCodeApiMemberSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("kind", EnumSchema(
                UcliCodeApiMemberKindValues.Property,
                UcliCodeApiMemberKindValues.Method)),
            Required("name", StringSchema()),
            Required("description", StringSchema()),
            Required("type", NullableStringSchema()),
            Required("returnType", NullableStringSchema()),
            Required("parameters", ArraySchema(CreateOpsDescribeCodeApiParameterSchema())));
    }

    private static Dictionary<string, object?> CreateOpsDescribeCodeApiParameterSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("name", StringSchema()),
            Required("type", StringSchema()),
            Required("description", StringSchema()));
    }

    private static Dictionary<string, object?> CreateSkillsListPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("tiers", ArraySchema(SkillTierLiteralSchema())),
            Required("skillNames", ArraySchema(StringSchema())),
            Required("availableTiers", ArraySchema(CreateSkillsListTierSchema())),
            Required("skills", ArraySchema(CreateSkillsListSkillSchema())),
            Required("supportedHosts", ArraySchema(CreateSkillsListSupportedHostSchema())));
    }

    private static Dictionary<string, object?> CreateSkillsListTierSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("tier", SkillTierLiteralSchema()),
            Required("skillCount", NonNegativeIntegerSchema()));
    }

    private static Dictionary<string, object?> CreateSkillsListSkillSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("skillName", StringSchema()),
            Required("displayName", StringSchema()),
            Required("description", StringSchema()),
            Required("dependencies", ArraySchema(StringSchema())),
            Required("tier", SkillTierLiteralSchema()),
            Required("catalogId", StringSchema()),
            Required("skillBundleVersion", PositiveIntegerSchema()),
            Required("contentDigest", Sha256LowerHexSchema()),
            Required("hostArtifacts", ArraySchema(CreateSkillsListHostArtifactSchema())));
    }

    private static Dictionary<string, object?> CreateSkillsListHostArtifactSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("host", StringSchema()),
            Required("path", NullableStringSchema()),
            Required("digest", NullableSha256LowerHexSchema()),
            Required("materializedFrontmatterDigest", Sha256LowerHexSchema()));
    }

    private static Dictionary<string, object?> CreateSkillsListSupportedHostSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("host", StringSchema()),
            Required("projectTargetDirectory", StringSchema()),
            Required("userTargetDirectory", StringSchema()),
            Required("reloadGuidance", StringSchema()));
    }

    private static Dictionary<string, object?> SkillTierLiteralSchema ()
    {
        return EnumSchema("basic", "advanced", "developer");
    }

    private static Dictionary<string, object?> CreateCodesListPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("catalogVersion", IntegerSchema()),
            Required("source", StringSchema()),
            Required("kinds", ArraySchema(StringSchema())),
            Required("codes", ArraySchema(ObjectSchema(
                additionalProperties: false,
                Required("code", StringSchema()),
                Required("kind", StringSchema()),
                Required("category", StringSchema()),
                Required("summary", StringSchema())))));
    }

    private static Dictionary<string, object?> CreateCodesDescribePayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("code", StringSchema()),
            Required("known", BooleanSchema()),
            Required("kind", StringSchema()),
            Required("category", StringSchema()),
            Required("summary", StringSchema()),
            Required("meaning", NullableStringSchema()),
            Required("appearsIn", ArraySchema(StringSchema())),
            Required("appliesTo", ArraySchema(StringSchema())),
            Optional("coverageImpact", ObjectSchema(additionalProperties: true)),
            Optional("verdictSemantics", ObjectSchema(additionalProperties: true)),
            Required("executionSemantics", NullableObjectSchema()),
            Required("inspect", ArraySchema(StringSchema())),
            Required("relatedCodes", ArraySchema(StringSchema())));
    }

    private static Dictionary<string, object?> CreatePlayStatusPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            CreatePlayLifecyclePayloadProperties(Required("timeoutMilliseconds", IntegerSchema())));
    }

    private static Dictionary<string, object?> CreateScreenshotGamePayloadSchema ()
    {
        return OneOfSchema(
            CreateScreenshotPayloadSchema(
                ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.CurrentSurface),
                hasRequestedResolution: false),
            CreateScreenshotPayloadSchema(
                ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.RequestedResolution),
                hasRequestedResolution: true));
    }

    private static Dictionary<string, object?> CreateScreenshotScenePayloadSchema ()
    {
        return CreateScreenshotPayloadSchema(
            ContractLiteralCodec.ToValue(IpcScreenshotTarget.Scene),
            ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.CurrentSurface),
            hasRequestedResolution: false);
    }

    private static Dictionary<string, object?> CreateScreenshotPayloadSchema (
        string target,
        string sizeMode,
        bool hasRequestedResolution)
    {
        var requestedDimensionSchema = hasRequestedResolution
            ? PositiveIntegerSchema()
            : NullSchema();
        return ObjectSchema(
            additionalProperties: false,
            Required("project", ReferenceSchema("../defs/project.schema.json")),
            Required("capture", ObjectSchema(
                additionalProperties: false,
                Required("target", ConstString(target)),
                Required("sizeMode", ConstString(sizeMode)),
                Required("requestedWidth", requestedDimensionSchema),
                Required("requestedHeight", requestedDimensionSchema),
                Required("width", PositiveIntegerSchema()),
                Required("height", PositiveIntegerSchema()),
                Required(
                    "colorSpace",
                    EnumSchema(ContractLiteralCodec.GetLiterals<IpcScreenshotColorSpace>().ToArray())),
                Required("lifecycleStateAtCapture", StringSchema()),
                Required("compileStateAtCapture", StringSchema()),
                Required("domainReloadGeneration", NonNegativeIntegerSchema()),
                Required("playModeState", StringSchema()))),
            Required("artifact", ObjectSchema(
                additionalProperties: false,
                Required(
                    "kind",
                    ConstString(ContractLiteralCodec.ToValue(ScreenshotArtifactKind.Screenshot))),
                Required("mediaType", ConstString(ScreenshotArtifactContract.MediaType)),
                Required("path", StringSchema()),
                Required("digest", Sha256LowerHexSchema()),
                Required("sizeBytes", PositiveIntegerSchema()),
                Required("createdAtUtc", StringSchema()))));
    }

    private static Dictionary<string, object?> CreatePlayEnterPayloadSchema ()
    {
        return OneOfSchema(
            CreatePlayEnterPayloadVariantSchema(
                constrainEnteredState: true,
                CreatePlayEnterEnteredTransitionResultSchema()),
            CreatePlayEnterPayloadVariantSchema(
                constrainEnteredState: true,
                CreatePlayEnterAlreadyEnteredTransitionResultSchema()),
            CreatePlayEnterPayloadVariantSchema(
                constrainEnteredState: false,
                CreatePlayEnterTimeoutTransitionResultSchema()),
            CreatePlayEnterPayloadVariantSchema(
                constrainEnteredState: false,
                CreatePlayEnterBlockedTransitionResultSchema()));
    }

    private static Dictionary<string, object?> CreatePlayEnterPayloadVariantSchema (
        bool constrainEnteredState,
        Dictionary<string, object?> transitionSchema)
    {
        return ObjectSchema(
            additionalProperties: false,
            CreatePlayLifecyclePayloadProperties(
                constrainEnteredState,
                Required("transition", transitionSchema),
                Required("timeoutMilliseconds", IntegerSchema())));
    }

    private static Dictionary<string, object?> CreatePlayExitPayloadSchema ()
    {
        return OneOfSchema(
            CreatePlayExitPayloadVariantSchema(
                PlayLifecyclePayloadState.ReadyStopped,
                CreatePlayExitExitedTransitionResultSchema()),
            CreatePlayExitPayloadVariantSchema(
                PlayLifecyclePayloadState.Stopped,
                CreatePlayExitAlreadyExitedTransitionResultSchema()),
            CreatePlayExitPayloadVariantSchema(
                PlayLifecyclePayloadState.Any,
                CreatePlayExitTimeoutTransitionResultSchema()),
            CreatePlayExitPayloadVariantSchema(
                PlayLifecyclePayloadState.Any,
                CreatePlayExitBlockedTransitionResultSchema()));
    }

    private static Dictionary<string, object?> CreatePlayExitPayloadVariantSchema (
        PlayLifecyclePayloadState payloadState,
        Dictionary<string, object?> transitionSchema)
    {
        return ObjectSchema(
            additionalProperties: false,
            CreatePlayLifecyclePayloadProperties(
                payloadState,
                Required("transition", transitionSchema),
                Required("timeoutMilliseconds", IntegerSchema())));
    }

    private static SchemaProperty[] CreatePlayLifecyclePayloadProperties (params SchemaProperty[] extraProperties)
    {
        return CreatePlayLifecyclePayloadProperties(PlayLifecyclePayloadState.Any, extraProperties);
    }

    private static SchemaProperty[] CreatePlayLifecyclePayloadProperties (
        bool constrainEnteredState,
        params SchemaProperty[] extraProperties)
    {
        return CreatePlayLifecyclePayloadProperties(
            constrainEnteredState ? PlayLifecyclePayloadState.Entered : PlayLifecyclePayloadState.Any,
            extraProperties);
    }

    private static SchemaProperty[] CreatePlayLifecyclePayloadProperties (
        PlayLifecyclePayloadState payloadState,
        params SchemaProperty[] extraProperties)
    {
        var properties = new List<SchemaProperty>
        {
            Required("project", ReferenceSchema("../defs/project.schema.json")),
            Required("daemonStatus", ConstString("running")),
            Required("serverVersion", NullableStringSchema()),
            Required("editorMode", ConstString(Literal(DaemonEditorMode.Gui))),
            Required("lifecycleState", CreateLifecycleStateSchema(payloadState)),
            Required("blockingReason", CreateBlockingReasonSchema(payloadState)),
            Required("compileState", NullableStringSchema()),
            Required("generations", NullableUnityGenerationSnapshotSchema()),
            Required("canAcceptExecutionRequests", CreateCanAcceptExecutionRequestsSchema(payloadState)),
            Required("observedAtUtc", NullableStringSchema()),
            Required("actionRequired", NullableStringSchema()),
            Required("primaryDiagnostic", CreatePrimaryDiagnosticSchema()),
            Required("playMode", CreatePlayModeSnapshotSchema(payloadState)),
        };

        properties.AddRange(extraProperties);
        return properties.ToArray();
    }

    private static Dictionary<string, object?> CreatePlayEnterTransitionResultSchema ()
    {
        return OneOfSchema(
            CreatePlayEnterEnteredTransitionResultSchema(),
            CreatePlayEnterAlreadyEnteredTransitionResultSchema(),
            CreatePlayEnterTimeoutTransitionResultSchema(),
            CreatePlayEnterBlockedTransitionResultSchema());
    }

    private static Dictionary<string, object?> CreatePlayEnterEnteredTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Enter)),
            Required("result", ConstString(IpcPlayTransitionResultNames.Entered)),
            Required("before", CreatePlayLifecycleSnapshotSchema()),
            Required("after", CreateEnteredPlayLifecycleSnapshotSchema()));
    }

    private static Dictionary<string, object?> CreatePlayEnterAlreadyEnteredTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Enter)),
            Required("result", ConstString(IpcPlayTransitionResultNames.AlreadyEntered)),
            Required("before", CreateEnteredPlayLifecycleSnapshotSchema()),
            Required("after", CreateEnteredPlayLifecycleSnapshotSchema()));
    }

    private static Dictionary<string, object?> CreatePlayEnterTimeoutTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Enter)),
            Required("result", ConstString(IpcPlayTransitionResultNames.Timeout)),
            Required("before", CreatePlayLifecycleSnapshotSchema()),
            Required("observed", CreatePlayLifecycleSnapshotSchema()),
            Required("applicationState", ConstString(IpcPlayApplicationStateNames.Indeterminate)));
    }

    private static Dictionary<string, object?> CreatePlayEnterBlockedTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Enter)),
            Required("result", ConstString(IpcPlayTransitionResultNames.Blocked)),
            Required("before", CreatePlayLifecycleSnapshotSchema()),
            Required("observed", CreatePlayLifecycleSnapshotSchema()),
            Required(
                "applicationState",
                EnumSchema(
                    IpcPlayApplicationStateNames.NotApplied,
                    IpcPlayApplicationStateNames.Applied,
                    IpcPlayApplicationStateNames.Indeterminate,
                    IpcPlayApplicationStateNames.Unknown)));
    }

    private static Dictionary<string, object?> CreatePlayExitExitedTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Exit)),
            Required("result", ConstString(IpcPlayTransitionResultNames.Exited)),
            Required("before", CreateEnteredPlayLifecycleSnapshotSchema()),
            Required("after", CreateReadyStoppedPlayLifecycleSnapshotSchema()));
    }

    private static Dictionary<string, object?> CreatePlayExitAlreadyExitedTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Exit)),
            Required("result", ConstString(IpcPlayTransitionResultNames.AlreadyExited)),
            Required("before", CreateStoppedPlayLifecycleSnapshotSchema()),
            Required("after", CreateStoppedPlayLifecycleSnapshotSchema()));
    }

    private static Dictionary<string, object?> CreatePlayExitTimeoutTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Exit)),
            Required("result", ConstString(IpcPlayTransitionResultNames.Timeout)),
            Required("before", CreatePlayLifecycleSnapshotSchema()),
            Required("observed", CreatePlayLifecycleSnapshotSchema()),
            Required("applicationState", ConstString(IpcPlayApplicationStateNames.Indeterminate)));
    }

    private static Dictionary<string, object?> CreatePlayExitBlockedTransitionResultSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("transition", ConstString(IpcPlayTransitionCommandNames.Exit)),
            Required("result", ConstString(IpcPlayTransitionResultNames.Blocked)),
            Required("before", CreatePlayLifecycleSnapshotSchema()),
            Required("observed", CreatePlayLifecycleSnapshotSchema()),
            Required(
                "applicationState",
                EnumSchema(
                    IpcPlayApplicationStateNames.NotApplied,
                    IpcPlayApplicationStateNames.Applied,
                    IpcPlayApplicationStateNames.Indeterminate,
                    IpcPlayApplicationStateNames.Unknown)));
    }

    private static Dictionary<string, object?> CreatePlayLifecycleSnapshotSchema ()
    {
        return CreatePlayLifecycleSnapshotSchema(PlayLifecyclePayloadState.Any);
    }

    private static Dictionary<string, object?> CreateEnteredPlayLifecycleSnapshotSchema ()
    {
        return CreatePlayLifecycleSnapshotSchema(PlayLifecyclePayloadState.Entered);
    }

    private static Dictionary<string, object?> CreateReadyStoppedPlayLifecycleSnapshotSchema ()
    {
        return CreatePlayLifecycleSnapshotSchema(PlayLifecyclePayloadState.ReadyStopped);
    }

    private static Dictionary<string, object?> CreateStoppedPlayLifecycleSnapshotSchema ()
    {
        return CreatePlayLifecycleSnapshotSchema(PlayLifecyclePayloadState.Stopped);
    }

    private static Dictionary<string, object?> CreatePlayLifecycleSnapshotSchema (PlayLifecyclePayloadState payloadState)
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("serverVersion", NullableStringSchema()),
            Required("editorMode", NullableStringSchema()),
            Required("unityVersion", NullableStringSchema()),
            Required("projectFingerprint", NullableStringSchema()),
            Required("lifecycleState", CreateLifecycleStateSchema(payloadState)),
            Required("blockingReason", CreateBlockingReasonSchema(payloadState)),
            Required("compileState", NullableStringSchema()),
            Required("generations", NullableUnityGenerationSnapshotSchema()),
            Required("canAcceptExecutionRequests", CreateCanAcceptExecutionRequestsSchema(payloadState)),
            Required("observedAtUtc", NullableStringSchema()),
            Required("actionRequired", NullableStringSchema()),
            Required("primaryDiagnostic", CreatePrimaryDiagnosticSchema()),
            Required("playMode", payloadState == PlayLifecyclePayloadState.Any ? NullablePlayModeSnapshotSchema() : CreatePlayModeSnapshotSchema(payloadState)));
    }

    private static Dictionary<string, object?> NullablePlayModeSnapshotSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oneOf"] = new object?[]
            {
                CreatePlayModeSnapshotSchema(),
                NullSchema(),
            },
        };
    }

    private static Dictionary<string, object?> CreatePlayModeSnapshotSchema ()
    {
        return CreatePlayModeSnapshotSchema(PlayLifecyclePayloadState.Any);
    }

    private static Dictionary<string, object?> CreateEnteredPlayModeSnapshotSchema ()
    {
        return CreatePlayModeSnapshotSchema(PlayLifecyclePayloadState.Entered);
    }

    private static Dictionary<string, object?> CreatePlayModeSnapshotSchema (PlayLifecyclePayloadState payloadState)
    {
        return ObjectSchema(
            additionalProperties: false,
            Required(
                "state",
                CreatePlayModeStateSchema(payloadState)),
            Required(
                "transition",
                payloadState == PlayLifecyclePayloadState.Any
                    ? EnumSchema(
                        Literal(IpcPlayModeTransition.None),
                        Literal(IpcPlayModeTransition.Entering),
                        Literal(IpcPlayModeTransition.Exiting))
                    : ConstString(Literal(IpcPlayModeTransition.None))),
            Required("isPlaying", CreateIsPlayingSchema(payloadState)),
            Required("isPlayingOrWillChangePlaymode", CreateIsPlayingOrWillChangePlaymodeSchema(payloadState)));
    }

    private static Dictionary<string, object?> CreateLifecycleStateSchema (PlayLifecyclePayloadState payloadState)
    {
        return payloadState switch
        {
            PlayLifecyclePayloadState.Entered => ConstString(ContractLiteralCodec.ToValue(IpcEditorLifecycleState.PlayMode)),
            PlayLifecyclePayloadState.ReadyStopped => ConstString(ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Ready)),
            _ => NullableStringSchema(),
        };
    }

    private static Dictionary<string, object?> CreateBlockingReasonSchema (PlayLifecyclePayloadState payloadState)
    {
        return payloadState switch
        {
            PlayLifecyclePayloadState.Entered => ConstString(ContractLiteralCodec.ToValue(IpcEditorBlockingReason.PlayMode)),
            PlayLifecyclePayloadState.ReadyStopped => NullSchema(),
            _ => NullableStringSchema(),
        };
    }

    private static Dictionary<string, object?> CreateCanAcceptExecutionRequestsSchema (PlayLifecyclePayloadState payloadState)
    {
        return payloadState switch
        {
            PlayLifecyclePayloadState.Entered => ConstBoolean(false),
            PlayLifecyclePayloadState.ReadyStopped => ConstBoolean(true),
            _ => BooleanSchema(),
        };
    }

    private static Dictionary<string, object?> CreatePlayModeStateSchema (PlayLifecyclePayloadState payloadState)
    {
        return payloadState switch
        {
            PlayLifecyclePayloadState.Entered => ConstString(Literal(IpcPlayModeState.Playing)),
            PlayLifecyclePayloadState.Stopped => ConstString(Literal(IpcPlayModeState.Stopped)),
            PlayLifecyclePayloadState.ReadyStopped => ConstString(Literal(IpcPlayModeState.Stopped)),
            _ => EnumSchema(
                Literal(IpcPlayModeState.Stopped),
                Literal(IpcPlayModeState.Entering),
                Literal(IpcPlayModeState.Playing),
                Literal(IpcPlayModeState.Exiting),
                Literal(IpcPlayModeState.Unknown)),
        };
    }

    private static Dictionary<string, object?> CreateIsPlayingSchema (PlayLifecyclePayloadState payloadState)
    {
        return payloadState switch
        {
            PlayLifecyclePayloadState.Entered => ConstBoolean(true),
            PlayLifecyclePayloadState.Stopped => ConstBoolean(false),
            PlayLifecyclePayloadState.ReadyStopped => ConstBoolean(false),
            _ => BooleanSchema(),
        };
    }

    private static Dictionary<string, object?> CreateIsPlayingOrWillChangePlaymodeSchema (PlayLifecyclePayloadState payloadState)
    {
        return payloadState is PlayLifecyclePayloadState.Stopped or PlayLifecyclePayloadState.ReadyStopped
            ? ConstBoolean(false)
            : BooleanSchema();
    }

    private static Dictionary<string, object?> CreateDaemonStartPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("startStatus", StringSchema()),
            Optional("daemonStatus", StringSchema()),
            Optional("lifecycleState", NullableStringSchema()),
            Optional("blockingReason", NullableStringSchema()),
            Optional("canAcceptExecutionRequests", BooleanSchema()),
            Optional("timeoutMilliseconds", IntegerSchema()),
            Optional("session", NullableObjectSchema()),
            Optional("startup", ObjectSchema(additionalProperties: true)),
            Optional("diagnosis", NullableObjectSchema()),
            Optional("retryDisposition", NullableStringSchema()),
            Optional("safeToRetryImmediately", BooleanSchema()));
    }

    private static Dictionary<string, object?> CreateDaemonStatusPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("daemonStatus", StringSchema()),
            Optional("serverVersion", NullableStringSchema()),
            Optional("editorMode", NullableStringSchema()),
            Optional("lifecycleState", NullableStringSchema()),
            Optional("blockingReason", NullableStringSchema()),
            Optional("compileState", NullableStringSchema()),
            Optional("generations", NullableUnityGenerationSnapshotSchema()),
            Optional("canAcceptExecutionRequests", BooleanSchema()),
            Optional("observedAtUtc", NullableStringSchema()),
            Optional("actionRequired", NullableStringSchema()),
            Optional("primaryDiagnostic", NullableObjectSchema()),
            Optional("playMode", NullablePlayModeSnapshotSchema()),
            Optional("timeoutMilliseconds", IntegerSchema()),
            Optional("session", NullableObjectSchema()),
            Optional("diagnosis", NullableObjectSchema()),
            Optional("lastLaunchAttempt", ObjectSchema(additionalProperties: true)));
    }

    private static Dictionary<string, object?> CreateTestProfileInitPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Optional("profilePath", StringSchema()));
    }

    private static Dictionary<string, object?> CreateTestRunPayloadSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("result", NullableStringSchema()),
            Required("errorKind", NullableStringSchema()),
            Required("runId", NullableStringSchema()),
            Required("artifactsDir", NullableStringSchema()),
            Required("summaryJsonPath", NullableStringSchema()));
    }

    private static Dictionary<string, object?> CreateRequestEnvelopeSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("steps", ArraySchema(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["oneOf"] = new object?[]
                {
                    ObjectSchema(
                        additionalProperties: false,
                        Required("kind", ConstString("op")),
                        Required("id", StringSchema()),
                        Required("op", StringSchema()),
                        Required("args", ObjectSchema(additionalProperties: true))),
                    ReferenceSchema("edit-dsl.schema.json"),
                },
            })));
    }

    private static Dictionary<string, object?> CreateEditDslSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("kind", ConstString("edit")),
            Required("id", StringSchema()),
            Required("on", ObjectSchema(additionalProperties: true)),
            Required("select", ObjectSchema(additionalProperties: true)),
            Required("actions", ArraySchema(ObjectSchema(additionalProperties: true))),
            Required("commit", StringSchema()));
    }

    private static SchemaProperty Required (
        string name,
        Dictionary<string, object?> schema)
    {
        return new SchemaProperty(name, schema, true);
    }

    private static SchemaProperty Optional (
        string name,
        Dictionary<string, object?> schema)
    {
        return new SchemaProperty(name, schema, false);
    }

    private static Dictionary<string, object?> ObjectSchema (
        bool additionalProperties,
        params SchemaProperty[] properties)
    {
        var schema = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "object",
        };

        if (!additionalProperties)
        {
            schema["additionalProperties"] = false;
        }

        if (properties.Length > 0)
        {
            schema["properties"] = properties.ToDictionary(
                static property => property.Name,
                static property => (object?)property.Schema,
                StringComparer.Ordinal);

            var required = properties
                .Where(static property => property.Required)
                .Select(static property => property.Name)
                .ToArray();
            if (required.Length > 0)
            {
                schema["required"] = required;
            }
        }

        return schema;
    }

    private static Dictionary<string, object?> StringMapSchema ()
    {
        var schema = ObjectSchema(additionalProperties: true);
        schema["additionalProperties"] = StringSchema();
        return schema;
    }

    private static Dictionary<string, object?> ArraySchema (Dictionary<string, object?> itemSchema)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "array",
            ["items"] = itemSchema,
        };
    }

    private static Dictionary<string, object?> OneOfSchema (params Dictionary<string, object?>[] schemas)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oneOf"] = schemas,
        };
    }

    private static Dictionary<string, object?> StringSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "string",
        };
    }

    private static Dictionary<string, object?> Sha256LowerHexSchema ()
    {
        return PatternStringSchema("^[0-9a-f]{64}$");
    }

    private static Dictionary<string, object?> NullableSha256LowerHexSchema ()
    {
        return OneOfSchema(Sha256LowerHexSchema(), NullSchema());
    }

    private static Dictionary<string, object?> InputArgsPathSchema ()
    {
        var aliasPropertyName = Regex.Escape(UcliOperationContractPropertyNames.Alias);
        return PatternStringSchema($@"^(?=.{{1,256}}$)(?!.*(?:^|\.){aliasPropertyName}(?:\.|$))(?:\$|\$\.[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+){{0,15}})$");
    }

    private static Dictionary<string, object?> VariantFieldArgsPathSchema ()
    {
        var aliasPropertyName = Regex.Escape(UcliOperationContractPropertyNames.Alias);
        return PatternStringSchema($@"^(?=.{{1,256}}$)(?!.*(?:^|\.){aliasPropertyName}(?:\.|$))\$\.[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+){{0,15}}$");
    }

    private static Dictionary<string, object?> PatternStringSchema (string pattern)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "string",
            ["pattern"] = pattern,
        };
    }

    private static Dictionary<string, object?> NullableStringSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new[] { "string", "null" },
        };
    }

    private static Dictionary<string, object?> IntegerSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "integer",
        };
    }

    private static Dictionary<string, object?> NonNegativeIntegerSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "integer",
            ["minimum"] = 0,
        };
    }

    private static Dictionary<string, object?> NullableNonNegativeIntegerSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new[] { "integer", "null" },
            ["minimum"] = 0,
        };
    }

    private static Dictionary<string, object?> CreateUnityGenerationSnapshotSchema ()
    {
        return ObjectSchema(
            additionalProperties: false,
            Required("compileGeneration", NonNegativeIntegerSchema()),
            Required("domainReloadGeneration", NonNegativeIntegerSchema()),
            Required("assetRefreshGeneration", NonNegativeIntegerSchema()),
            Required("playModeGeneration", NonNegativeIntegerSchema()));
    }

    private static Dictionary<string, object?> NullableUnityGenerationSnapshotSchema ()
    {
        return OneOfSchema(
            CreateUnityGenerationSnapshotSchema(),
            NullSchema());
    }

    private static Dictionary<string, object?> PositiveIntegerSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "integer",
            ["minimum"] = 1,
        };
    }

    private static Dictionary<string, object?> NumberSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "number",
        };
    }

    private static Dictionary<string, object?> NullableIntegerSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new[] { "integer", "null" },
        };
    }

    private static Dictionary<string, object?> BooleanSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "boolean",
        };
    }

    private static Dictionary<string, object?> NullableObjectSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = new[] { "object", "null" },
        };
    }

    private static Dictionary<string, object?> AnySchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> NullSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "null",
        };
    }

    private static Dictionary<string, object?> ReferenceSchema (string reference)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$ref"] = reference,
        };
    }

    private static string Literal<TEnum> (TEnum value)
        where TEnum : struct, Enum
    {
        return ContractLiteralCodec.ToValue(value);
    }

    private static Dictionary<string, object?> EnumSchema (params string[] values)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "string",
            ["enum"] = values,
        };
    }

    private static Dictionary<string, object?> EnumValueSchema (params object?[] values)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enum"] = values,
        };
    }

    private static Dictionary<string, object?> ConstString (string value)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "string",
            ["const"] = value,
        };
    }

    private static Dictionary<string, object?> ConstInteger (int value)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "integer",
            ["const"] = value,
        };
    }

    private static Dictionary<string, object?> ConstBoolean (bool value)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "boolean",
            ["const"] = value,
        };
    }

    private static string SerializeJson (object value)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return json.EndsWith('\n') ? json : json + "\n";
    }

    private static void DeleteExistingVersionRoot (string versionRoot)
    {
        var manifestPath = Path.Combine(versionRoot, "schema-manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Refusing to delete schema output directory without a uCLI schema manifest: {versionRoot}");
        }

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        if (!root.TryGetProperty("schemaSet", out var schemaSetElement)
            || !string.Equals(schemaSetElement.GetString(), SchemaSet, StringComparison.Ordinal)
            || !root.TryGetProperty("schemaSetVersion", out var schemaSetVersionElement)
            || !string.Equals(schemaSetVersionElement.GetString(), SchemaSetVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to delete schema output directory with an unexpected manifest: {versionRoot}");
        }

        Directory.Delete(versionRoot, recursive: true);
    }

    private static string ReadPackageVersion (string repositoryRoot)
    {
        var propsPath = Path.Combine(repositoryRoot, "Directory.Build.props");
        if (!File.Exists(propsPath))
        {
            throw new InvalidOperationException($"Directory.Build.props was not found: {propsPath}");
        }

        var document = XDocument.Load(propsPath);
        var version = document.Descendants("Version").SingleOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException($"Version property was not found: {propsPath}");
        }

        return version;
    }

    private static bool TryParseArgs (
        string[] args,
        out string outputRoot,
        out string? packageVersion,
        out string repositoryRoot)
    {
        outputRoot = string.Empty;
        packageVersion = null;
        repositoryRoot = Directory.GetCurrentDirectory();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output":
                    if (!TryReadOptionValue(args, ref i, out outputRoot))
                    {
                        return false;
                    }
                    break;

                case "--package-version":
                    if (!TryReadOptionValue(args, ref i, out packageVersion))
                    {
                        return false;
                    }
                    break;

                case "--repository-root":
                    if (!TryReadOptionValue(args, ref i, out repositoryRoot))
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }

        return !string.IsNullOrWhiteSpace(outputRoot)
            && !string.IsNullOrWhiteSpace(repositoryRoot);
    }

    private static bool TryReadOptionValue (
        string[] args,
        ref int optionIndex,
        out string value)
    {
        if (optionIndex >= args.Length - 1)
        {
            value = string.Empty;
            return false;
        }

        value = args[++optionIndex];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void WriteUsage ()
    {
        Console.Error.WriteLine("usage: Ucli.SchemaGenerator --output <schemas-dir> [--repository-root <repo-root>] [--package-version <version>]");
    }

    private sealed record SchemaFile (
        string RelativePath,
        string Kind,
        string? Command,
        Dictionary<string, object?> Document);

    private sealed record SchemaProperty (
        string Name,
        Dictionary<string, object?> Schema,
        bool Required);

    private enum PlayLifecyclePayloadState
    {
        Any,
        Entered,
        Stopped,
        ReadyStopped,
    }
}
