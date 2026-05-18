using System.Text.Json;
using System.Xml.Linq;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

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
            CreatePayloadSchema(UcliCommandIds.Verify.Name, CreateVerifyPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Init.Name, CreateInitPayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Validate.Name, CreateValidatePayloadSchema()),
            CreatePayloadSchema(UcliCommandIds.Plan.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: true, includePlanToken: true, includePlan: false, includePostReadSource: false)),
            CreatePayloadSchema(UcliCommandIds.Call.Name, CreateRequestExecutionPayloadSchema(includeReadIndex: false, includePlanToken: false, includePlan: true, includePostReadSource: true)),
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
                    Required("kind", StringSchema()),
                    Required("path", StringSchema()),
                    Optional("digest", StringSchema())),
                ObjectSchema(
                    additionalProperties: false,
                    Required("kind", StringSchema()),
                    Required("uri", StringSchema()),
                    Optional("digest", StringSchema())),
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
            Optional("compileGeneration", NullableStringSchema()),
            Optional("domainReloadGeneration", NullableStringSchema()),
            Optional("canAcceptExecutionRequests", BooleanSchema()),
            Optional("editorMode", NullableStringSchema()),
            Optional("observedAtUtc", NullableStringSchema()),
            Optional("actionRequired", NullableStringSchema()),
            Optional("primaryDiagnostic", NullableObjectSchema()),
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
                ["compileGeneration"] = NullableStringSchema(),
                ["domainReloadGeneration"] = NullableStringSchema(),
                ["canAcceptExecutionRequests"] = BooleanSchema(),
                ["observedAtUtc"] = NullableStringSchema(),
                ["actionRequired"] = NullableStringSchema(),
                ["primaryDiagnostic"] = CreateReadyPrimaryDiagnosticSchema(),
            },
        };
    }

    private static Dictionary<string, object?> CreateReadyPrimaryDiagnosticSchema ()
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
            Optional("freshness", NullableStringSchema()),
            Optional("sourceInputsHash", NullableStringSchema()),
            Optional("generatedAtUtc", NullableStringSchema()),
            Optional("code", NullableStringSchema()),
            Optional("message", NullableStringSchema()));
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
                Required("compileGenerationBefore", StringSchema()),
                Required("compileGenerationAfter", StringSchema()),
                Required("diagnostics", ObjectSchema(
                    additionalProperties: false,
                    Required("errorCount", IntegerSchema()),
                    Required("warningCount", IntegerSchema()),
                    Required("primaryDiagnostic", CreateReadyPrimaryDiagnosticSchema()))))),
            Required("domainReload", ObjectSchema(
                additionalProperties: false,
                Required("reloadRequired", BooleanSchema()),
                Required("reloadObserved", BooleanSchema()),
                Required("generationBefore", StringSchema()),
                Required("generationAfter", StringSchema()),
                Required("settled", BooleanSchema()))),
            Required("lifecycle", ObjectSchema(
                additionalProperties: false,
                Required("serverVersion", NullableStringSchema()),
                Required("unityVersion", NullableStringSchema()),
                Required("editorMode", NullableStringSchema()),
                Required("lifecycleState", NullableStringSchema()),
                Required("blockingReason", NullableStringSchema()),
                Required("compileState", NullableStringSchema()),
                Required("compileGeneration", NullableStringSchema()),
                Required("domainReloadGeneration", NullableStringSchema()),
                Required("canAcceptExecutionRequests", BooleanSchema()),
                Required("observedAtUtc", NullableStringSchema()),
                Required("actionRequired", NullableStringSchema()),
                Required("primaryDiagnostic", CreateReadyPrimaryDiagnosticSchema()))));
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
            Required("profileDigest", StringSchema()),
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
            Required("digest", StringSchema()));
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
                    UcliOperationKindValues.Query,
                    UcliOperationKindValues.Command,
                    UcliOperationKindValues.Mutation)),
            Required(
                "policy",
                EnumSchema(
                    OperationPolicyValues.Safe,
                    OperationPolicyValues.Advanced,
                    OperationPolicyValues.Dangerous)),
            Required("description", StringSchema()),
            Required("inputs", ArraySchema(ObjectSchema(
                additionalProperties: false,
                Required("name", StringSchema()),
                Required("description", StringSchema()),
                Required("valueType", StringSchema()),
                Required("constraints", ArraySchema(ObjectSchema(additionalProperties: true)))))),
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
                IpcExecuteTouchedResourceKindNames.Scene,
                IpcExecuteTouchedResourceKindNames.Prefab,
                IpcExecuteTouchedResourceKindNames.Asset,
                IpcExecuteTouchedResourceKindNames.ProjectSettings))),
            Required("planMode", EnumSchema(
                UcliOperationPlanModeValues.ValidationOnly,
                UcliOperationPlanModeValues.ObservesLiveUnity,
                UcliOperationPlanModeValues.MayCreatePreviewState)),
            Required("planSemantics", StringSchema()),
            Required("callSemantics", StringSchema()),
            Required("touchedContract", StringSchema()),
            Required("readPostconditionContract", StringSchema()),
            Required("failureSemantics", StringSchema()),
            Required("dangerousNotes", ArraySchema(StringSchema())));
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
            Optional("compileGeneration", NullableStringSchema()),
            Optional("domainReloadGeneration", NullableStringSchema()),
            Optional("canAcceptExecutionRequests", BooleanSchema()),
            Optional("observedAtUtc", NullableStringSchema()),
            Optional("actionRequired", NullableStringSchema()),
            Optional("primaryDiagnostic", NullableObjectSchema()),
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

    private static Dictionary<string, object?> ArraySchema (Dictionary<string, object?> itemSchema)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "array",
            ["items"] = itemSchema,
        };
    }

    private static Dictionary<string, object?> StringSchema ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "string",
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

    private static Dictionary<string, object?> ReferenceSchema (string reference)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$ref"] = reference,
        };
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
}
