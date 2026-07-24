using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcExecuteContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string GlobalObjectIdText = "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-4-5";

    private static string ProjectPath { get; } = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ucli-tests",
        "repo",
        "UnityProject"));

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithNullProjectPath_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcProjectIdentity(
            projectPath: null!,
            projectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            unityVersion: "6000.1.4f1"));

        Assert.Equal("projectPath", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithEmptyProjectPath_ThrowsArgumentException (string projectPath)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcProjectIdentity(
            projectPath,
            new ProjectFingerprint(ProjectFingerprintText),
            "6000.1.4f1"));

        Assert.Equal("projectPath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithRelativeProjectPath_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcProjectIdentity(
            "relative/UnityProject",
            new ProjectFingerprint(ProjectFingerprintText),
            "6000.1.4f1"));

        Assert.Equal("projectPath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithWindowsRootRelativeProjectPath_ThrowsArgumentException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string projectPath = @"\repo\UnityProject";
        Assert.True(Path.IsPathRooted(projectPath));
        Assert.False(Path.IsPathFullyQualified(projectPath));

        var exception = Assert.Throws<ArgumentException>(() => new IpcProjectIdentity(
            projectPath,
            new ProjectFingerprint(ProjectFingerprintText),
            "6000.1.4f1"));

        Assert.Equal("projectPath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_NormalizesAbsoluteProjectPath ()
    {
        var input = Path.Combine(Path.GetTempPath(), "ucli-project", "nested", "..") + Path.DirectorySeparatorChar;

        var identity = new IpcProjectIdentity(
            input,
            new ProjectFingerprint(ProjectFingerprintText),
            "6000.1.4f1");

        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(input)),
            identity.ProjectPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithNullProjectFingerprint_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcProjectIdentity(
            projectPath: ProjectPath,
            projectFingerprint: null!,
            unityVersion: "6000.1.4f1"));

        Assert.Equal("projectFingerprint", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithNullUnityVersion_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcProjectIdentity(
            projectPath: ProjectPath,
            projectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            unityVersion: null!));

        Assert.Equal("unityVersion", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithEmptyUnityVersion_ThrowsArgumentException (string unityVersion)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcProjectIdentity(
            ProjectPath,
            new ProjectFingerprint(ProjectFingerprintText),
            unityVersion));

        Assert.Equal("unityVersion", exception.ParamName);
    }

    [Theory]
    [InlineData(" 6000.1.4f1")]
    [InlineData("6000.1.4f1 ")]
    [Trait("Size", "Small")]
    public void IpcProjectIdentity_Constructor_WithOuterWhitespaceInUnityVersion_ThrowsArgumentException (string unityVersion)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcProjectIdentity(
            ProjectPath,
            new ProjectFingerprint(ProjectFingerprintText),
            unityVersion));

        Assert.Equal("unityVersion", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_Constructor_WithNullOperationResults_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcExecuteResponse(
            opResults: null!,
            project: CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null));

        Assert.Equal("opResults", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_Constructor_WithNullProject_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcExecuteResponse(
            opResults: Array.Empty<IpcExecuteOperationResult>(),
            project: null!,
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null));

        Assert.Equal("project", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_Constructor_WhenPostReadSourceDoesNotMatchOperationResults_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("edit-1"),
                    Op: IpcExecutePostReadSourceRules.EditOperationName,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: new IpcExecutePostReadSource(IpcExecutePostReadSource.CurrentSchemaVersion, []),
            contractViolations: null));

        Assert.Equal("postReadSource", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_Constructor_WhenPlanTokenIsMissingValue_ThrowsArgumentException (string planToken)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteResponse(
            [],
            CreateProjectIdentity(),
            planToken: planToken,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null));

        Assert.Equal("planToken", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_Constructor_WhenContractViolationDoesNotMatchOperationResult_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("step-1"),
                    Op: UcliPrimitiveOperationNames.ProjectRefresh,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations:
            [
                new IpcExecuteContractViolation(
                    OpId: new IpcExecuteStepId("another-step"),
                    Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                    ExpectedFact: "assurance.mayDirty=false",
                    ObservedResult: "opResults[].changed=true",
                    ApplicationState: IpcApplicationState.Indeterminate),
            ]));

        Assert.Equal("contractViolations", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiredProjectIdentity_PublicSurface_CannotBypassConstructorValidation ()
    {
        var projectIdentityConstructor = Assert.Single(typeof(IpcProjectIdentity).GetConstructors());
        Assert.Collection(
            projectIdentityConstructor.GetParameters(),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType),
            parameter => Assert.Equal(typeof(ProjectFingerprint), parameter.ParameterType),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType));
        Assert.Empty(typeof(IpcProjectIdentity).GetMember("Unknown"));
        Assert.Null(typeof(IpcProjectIdentity).GetProperty(nameof(IpcProjectIdentity.ProjectPath))!.SetMethod);
        Assert.Null(typeof(IpcProjectIdentity).GetProperty(nameof(IpcProjectIdentity.ProjectFingerprint))!.SetMethod);
        Assert.Null(typeof(IpcProjectIdentity).GetProperty(nameof(IpcProjectIdentity.UnityVersion))!.SetMethod);

        var executeResponseConstructor = Assert.Single(typeof(IpcExecuteResponse).GetConstructors());
        Assert.Collection(
            executeResponseConstructor.GetParameters(),
            parameter => Assert.Equal(typeof(IReadOnlyList<IpcExecuteOperationResult>), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IpcProjectIdentity), parameter.ParameterType),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IpcExecuteReadPostcondition), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IpcExecutePostReadSource), parameter.ParameterType),
            parameter => Assert.Equal(typeof(IReadOnlyList<IpcExecuteContractViolation>), parameter.ParameterType));
        Assert.Null(typeof(IpcExecuteResponse).GetProperty(nameof(IpcExecuteResponse.OpResults))!.SetMethod);
        Assert.Null(typeof(IpcExecuteResponse).GetProperty(nameof(IpcExecuteResponse.Project))!.SetMethod);
        Assert.Null(typeof(IpcExecuteResponse).GetProperty(nameof(IpcExecuteResponse.PlanToken))!.SetMethod);
        Assert.Null(typeof(IpcExecuteResponse).GetProperty(nameof(IpcExecuteResponse.ReadPostcondition))!.SetMethod);
        Assert.Null(typeof(IpcExecuteResponse).GetProperty(nameof(IpcExecuteResponse.PostReadSource))!.SetMethod);
        Assert.Null(typeof(IpcExecuteResponse).GetProperty(nameof(IpcExecuteResponse.ContractViolations))!.SetMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteRequest_SerializesOptionalExecutionControlsOnlyWhenSpecified ()
    {
        var requestWithToken = new IpcExecuteRequest(
            Command: UcliCommandIds.Call.Name,
            Arguments: IpcPayloadCodec.SerializeToElement(new
            {
                protocolVersion = 1,
                steps = Array.Empty<object>(),
            }))
        {
            PlanToken = "token-value",
            AllowDangerous = true,
            AllowPlayMode = true,
        };

        var withTokenJson = IpcPayloadCodec.SerializeToElement(requestWithToken);
        Assert.True(withTokenJson.TryGetProperty("planToken", out var planTokenElement));
        Assert.Equal("token-value", planTokenElement.GetString());
        Assert.True(withTokenJson.TryGetProperty("allowDangerous", out var allowDangerousElement));
        Assert.True(allowDangerousElement.GetBoolean());
        Assert.True(withTokenJson.TryGetProperty("allowPlayMode", out var allowPlayModeElement));
        Assert.True(allowPlayModeElement.GetBoolean());
        Assert.False(withTokenJson.TryGetProperty("timeoutMilliseconds", out _));
        Assert.False(withTokenJson.TryGetProperty("failFast", out _));

        var requestWithoutToken = new IpcExecuteRequest(
            Command: UcliCommandIds.Plan.Name,
            Arguments: IpcPayloadCodec.SerializeToElement(new
            {
                protocolVersion = 1,
                steps = Array.Empty<object>(),
            }))
        {
            FailFast = true,
        };
        var withoutTokenJson = IpcPayloadCodec.SerializeToElement(requestWithoutToken);
        Assert.False(withoutTokenJson.TryGetProperty("planToken", out _));
        Assert.False(withoutTokenJson.TryGetProperty("allowDangerous", out _));
        Assert.False(withoutTokenJson.TryGetProperty("allowPlayMode", out _));
        Assert.True(withoutTokenJson.TryGetProperty("failFast", out var failFastElement));
        Assert.True(failFastElement.GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesWithOpResultsContract ()
    {
        var response = new IpcExecuteResponse(
            opResults:
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("op-1"),
                    Op: UcliPrimitiveOperationNames.Resolve,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched:
                    [
                        new IpcExecuteTouchedResource(
                            kind: UcliTouchedResourceKind.Scene,
                            path: "Assets/Scenes/Main.unity",
                            assetGuid: Guid.ParseExact("11111111111111111111111111111111", "N")),
                    ]),
            ],
            project: CreateProjectIdentity(),
            planToken: "issued-token",
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null);

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
            .HasArrayLength("opResults", 1)
            .HasProperty("project", project => project
                .HasString("projectPath", response.Project.ProjectPath)
                .HasString("projectFingerprint", ProjectFingerprintText)
                .HasString("unityVersion", "6000.1.4f1"))
            .HasString("planToken", "issued-token")
            .HasProperty("opResults", 0, opResult => opResult
                .HasString("opId", "op-1")
                .HasString("op", UcliPrimitiveOperationNames.Resolve)
                .HasString("phase", TextVocabulary.GetText(IpcExecuteOperationPhase.Call))
                .HasBoolean("applied", true)
                .HasBoolean("changed", true)
                .HasArrayLength("touched", 1)
                .HasArrayLength("diagnostics", 0)
                .HasProperty("touched", 0, touched => touched
                    .HasString("kind", TextVocabulary.GetText(UcliTouchedResourceKind.Scene))
                    .HasString("path", "Assets/Scenes/Main.unity")
                    .HasString("assetGuid", "11111111-1111-1111-1111-111111111111")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteTouchedResource_WhenAssetGuidIsEmpty_RejectsInvalidValue ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteTouchedResource(
            kind: UcliTouchedResourceKind.Asset,
            path: "Assets/Example.asset",
            assetGuid: Guid.Empty));

        Assert.Equal("assetGuid", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Assets/Example.asset")]
    [InlineData("Assets\\Example.asset")]
    [InlineData("Assets/../Example.asset")]
    [InlineData("/Assets/Example.asset")]
    [Trait("Size", "Small")]
    public void IpcExecuteTouchedResource_WhenPathIsInvalid_RejectsInvalidValue (string? path)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteTouchedResource(
            kind: UcliTouchedResourceKind.Asset,
            path: path!,
            assetGuid: null));

        Assert.Equal("path", exception.ParamName);
    }

    [Theory]
    [InlineData((UcliTouchedResourceKind)0)]
    [InlineData((UcliTouchedResourceKind)999)]
    [Trait("Size", "Small")]
    public void IpcExecuteTouchedResource_WhenKindIsUnsupported_RejectsInvalidValue (UcliTouchedResourceKind kind)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new IpcExecuteTouchedResource(
            kind: kind,
            path: "Assets/Example.asset",
            assetGuid: null));

        Assert.Equal("kind", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_RoundTripsContractViolations ()
    {
        var response = new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("step-1"),
                    Op: UcliPrimitiveOperationNames.ProjectRefresh,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations:
            [
                new IpcExecuteContractViolation(
                    OpId: new IpcExecuteStepId("step-1"),
                    Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                    ExpectedFact: "assurance.mayDirty=false",
                    ObservedResult: "opResults[].changed=true",
                    ApplicationState: IpcApplicationState.Indeterminate),
            ]);

        var jsonElement = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(jsonElement)
            .HasArrayLength("contractViolations", 1)
            .HasProperty("contractViolations", 0, violation => violation
                .HasString("opId", "step-1")
                .HasString("operation", UcliPrimitiveOperationNames.ProjectRefresh)
                .HasString("expectedFact", "assurance.mayDirty=false")
                .HasString("observedResult", "opResults[].changed=true")
                .HasString("applicationState", TextVocabulary.GetText(IpcApplicationState.Indeterminate)));

        var roundTrip = JsonSerializer.Deserialize<IpcExecuteResponse>(
            jsonElement.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        var violationResult = Assert.Single(roundTrip.ContractViolations!);
        Assert.Equal("step-1", violationResult.OpId.Value);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, violationResult.Operation);
        Assert.Equal("assurance.mayDirty=false", violationResult.ExpectedFact);
        Assert.Equal("opResults[].changed=true", violationResult.ObservedResult);
        Assert.Equal(IpcApplicationState.Indeterminate, violationResult.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_RoundTripsRequiredProjectIdentity ()
    {
        var response = new IpcExecuteResponse(
            Array.Empty<IpcExecuteOperationResult>(),
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null);

        var jsonElement = IpcPayloadCodec.SerializeToElement(response);
        var roundTrip = JsonSerializer.Deserialize<IpcExecuteResponse>(
            jsonElement.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.Project, roundTrip.Project);
        Assert.Equal(ProjectFingerprintText, roundTrip.Project.ProjectFingerprint.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationResultFactory_CreatePlanResult_CreatesSharedEnvelopeContract ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(
            new IpcResolveOperationResult(new UnityGlobalObjectId(GlobalObjectIdText)));
        var opResult = IpcExecuteOperationResultFactory.CreatePlanResult(
            opId: new IpcExecuteStepId("resolve"),
            op: UcliPrimitiveOperationNames.Resolve,
            applied: false,
            changed: false,
            touched: Array.Empty<IpcExecuteTouchedResource>(),
            result: payload,
            diagnostics:
            [
                new IpcExecuteDiagnostic(
                    Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                    Severity: UcliDiagnosticSeverity.Warning,
                    CoverageImpact: IpcExecuteDiagnosticCoverageImpact.Partial,
                    Message: "Scene query skipped GameObjects whose names contain '/'."),
            ]);

        var json = IpcPayloadCodec.SerializeToElement(opResult);

        JsonAssert.For(json)
            .HasString("opId", "resolve")
            .HasString("op", UcliPrimitiveOperationNames.Resolve)
            .HasString("phase", TextVocabulary.GetText(IpcExecuteOperationPhase.Plan))
            .HasBoolean("applied", false)
            .HasBoolean("changed", false)
            .HasArrayLength("touched", 0)
            .HasArrayLength("diagnostics", 1)
            .HasProperty("diagnostics", 0, diagnostic => diagnostic
                .HasString("code", "HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS")
                .HasString("severity", TextVocabulary.GetText(UcliDiagnosticSeverity.Warning))
                .HasString("coverageImpact", TextVocabulary.GetText(IpcExecuteDiagnosticCoverageImpact.Partial))
                .HasString("message", "Scene query skipped GameObjects whose names contain '/'."))
            .HasProperty("result", result => result
                .HasString("globalObjectId", GlobalObjectIdText));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationResult_Constructor_WithNullOperationId_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcExecuteOperationResult(
            OpId: null!,
            Op: UcliPrimitiveOperationNames.Resolve,
            Phase: IpcExecuteOperationPhase.Call,
            Applied: false,
            Changed: false,
            Touched: []));

        Assert.Equal("OpId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationResult_Construction_SnapshotsCollections ()
    {
        var touchedResource = new IpcExecuteTouchedResource(
            UcliTouchedResourceKind.Asset,
            "Assets/Example.asset",
            assetGuid: null);
        var diagnostic = new IpcExecuteDiagnostic(
            ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
            UcliDiagnosticSeverity.Warning,
            IpcExecuteDiagnosticCoverageImpact.Partial,
            "Coverage is partial.");
        var touched = new[] { touchedResource };
        var diagnostics = new[] { diagnostic };
        var result = new IpcExecuteOperationResult(
            new IpcExecuteStepId("resolve"),
            UcliPrimitiveOperationNames.Resolve,
            IpcExecuteOperationPhase.Call,
            Applied: false,
            Changed: false,
            Touched: touched)
        {
            Diagnostics = diagnostics,
        };

        touched[0] = new IpcExecuteTouchedResource(
            UcliTouchedResourceKind.Asset,
            "Assets/Other.asset",
            assetGuid: null);
        diagnostics[0] = new IpcExecuteDiagnostic(
            ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
            UcliDiagnosticSeverity.Error,
            IpcExecuteDiagnosticCoverageImpact.Indeterminate,
            "Coverage is indeterminate.");

        Assert.Same(touchedResource, Assert.Single(result.Touched));
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteDiagnostic_Constructor_WithNullCode_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcExecuteDiagnostic(
            null!,
            UcliDiagnosticSeverity.Warning,
            IpcExecuteDiagnosticCoverageImpact.Partial,
            "Coverage is partial."));

        Assert.Equal("Code", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecutePostReadSourceStep_Constructor_WithNullOperationId_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcExecutePostReadSourceStep(
            OpId: null!,
            SourceKind: IpcExecutePostReadSourceKind.Operation,
            PlayModeMutation: false,
            Commit: null,
            PersistenceExpected: false,
            ExpectedPostState: IpcExecuteExpectedPostState.Unavailable));

        Assert.Equal("OpId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteReadPostconditionRequirement_Constructor_WithDefaultTimestamp_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteReadPostconditionRequirement(
            IpcExecuteReadPostconditionSurface.AssetSearch,
            default,
            ScenePath: null));

        Assert.Equal("MinSafeGeneratedAtUtc", exception.ParamName);
    }

    [Theory]
    [InlineData(IpcExecuteReadPostconditionSurface.AssetSearch)]
    [InlineData(IpcExecuteReadPostconditionSurface.GuidPath)]
    [Trait("Size", "Small")]
    public void IpcExecuteReadPostconditionRequirement_Constructor_WhenProjectSurfaceHasScenePath_ThrowsArgumentException (
        IpcExecuteReadPostconditionSurface surface)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcExecuteReadPostconditionRequirement(
            surface,
            DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
            new UnityScenePath("Assets/Scenes/Main.unity")));

        Assert.Equal("ScenePath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteReadPostconditionRequirement_PublicSurface_RequiresConstructorValidatedScenePath ()
    {
        var constructor = Assert.Single(typeof(IpcExecuteReadPostconditionRequirement).GetConstructors());
        Assert.Collection(
            constructor.GetParameters(),
            parameter => Assert.Equal(typeof(IpcExecuteReadPostconditionSurface), parameter.ParameterType),
            parameter => Assert.Equal(typeof(DateTimeOffset), parameter.ParameterType),
            parameter => Assert.Equal(typeof(UnityScenePath), parameter.ParameterType));
        Assert.Null(typeof(IpcExecuteReadPostconditionRequirement).GetProperty(nameof(IpcExecuteReadPostconditionRequirement.ScenePath))!.SetMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResolveOperationResult_SerializesWithCamelCaseContractFields ()
    {
        var payload = new IpcResolveOperationResult(new UnityGlobalObjectId(GlobalObjectIdText));

        var json = IpcPayloadCodec.SerializeToElement(payload);

        JsonAssert.For(json)
            .HasString("globalObjectId", GlobalObjectIdText);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcResolveSelectorArgsSchema_UsesCanonicalResolveSelectorPropertyNames ()
    {
        using var jsonDocument = JsonDocument.Parse(IpcResolveSelectorArgsSchema.Json);

        var properties = jsonDocument.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.GlobalObjectId, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.AssetGuid, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.AssetPath, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.ProjectAssetPath, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.Scene, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.Prefab, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.HierarchyPath, out _));
        Assert.True(properties.TryGetProperty(IpcResolveSelectorPropertyNames.ComponentType, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesReadPostconditionContract ()
    {
        var response = new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("query-1"),
                    Op: UcliPrimitiveOperationNames.SceneQuery,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: null),
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: new UnityScenePath("Assets/Scenes/Main.unity")),
            ]),
            postReadSource: null,
            contractViolations: null);

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
            .HasProperty("readPostcondition", readPostcondition => readPostcondition
                .HasArrayLength("requirements", 2)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", TextVocabulary.GetText(IpcExecuteReadPostconditionSurface.AssetSearch))
                    .HasString("minSafeGeneratedAtUtc", "2026-04-23T00:00:00+00:00"))
                .HasProperty("requirements", 1, requirement => requirement
                    .HasString("surface", TextVocabulary.GetText(IpcExecuteReadPostconditionSurface.SceneTreeLite))
                    .HasString("scenePath", "Assets/Scenes/Main.unity")
                    .HasString("minSafeGeneratedAtUtc", "2026-04-23T00:00:00+00:00")));
        Assert.False(json.GetProperty("readPostcondition").GetProperty("requirements")[0].TryGetProperty("scenePath", out _));

        var roundTrip = JsonSerializer.Deserialize<IpcExecuteResponse>(
            json.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(
            new UnityScenePath("Assets/Scenes/Main.unity"),
            roundTrip.ReadPostcondition!.Requirements[1].ScenePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesPostReadSourceContract ()
    {
        var response = new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("edit-1"),
                    Op: IpcExecutePostReadSourceRules.EditOperationName,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("op-1"),
                    Op: UcliPrimitiveOperationNames.SceneOpen,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: new IpcExecutePostReadSource(
                IpcExecutePostReadSource.CurrentSchemaVersion,
                [
                    new IpcExecutePostReadSourceStep(
                        OpId: new IpcExecuteStepId("edit-1"),
                        SourceKind: IpcExecutePostReadSourceKind.Edit,
                        PlayModeMutation: false,
                        Commit: IpcExecutePostReadCommit.Context,
                        PersistenceExpected: true,
                        ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
                    new IpcExecutePostReadSourceStep(
                        OpId: new IpcExecuteStepId("op-1"),
                        SourceKind: IpcExecutePostReadSourceKind.Operation,
                        PlayModeMutation: false,
                        Commit: null,
                        PersistenceExpected: false,
                        ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
                ]),
            contractViolations: null);

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
            .HasProperty("postReadSource", postReadSource => postReadSource
                .HasInt32("schemaVersion", 1)
                .HasArrayLength("steps", 2)
                .HasProperty("steps", 0, step => step
                    .HasString("opId", "edit-1")
                    .HasString("sourceKind", TextVocabulary.GetText(IpcExecutePostReadSourceKind.Edit))
                    .HasBoolean("playModeMutation", false)
                    .HasString("commit", TextVocabulary.GetText(IpcExecutePostReadCommit.Context))
                    .HasBoolean("persistenceExpected", true)
                    .HasString("expectedPostState", TextVocabulary.GetText(IpcExecuteExpectedPostState.Deterministic)))
                .HasProperty("steps", 1, step => step
                    .HasString("opId", "op-1")
                    .HasString("sourceKind", TextVocabulary.GetText(IpcExecutePostReadSourceKind.Operation))
                    .IsNull("commit")
                    .HasBoolean("persistenceExpected", false)
                    .HasString("expectedPostState", TextVocabulary.GetText(IpcExecuteExpectedPostState.Unavailable))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_SerializesContractViolationsContract ()
    {
        var response = new IpcExecuteResponse(
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("query-1"),
                    Op: UcliPrimitiveOperationNames.SceneQuery,
                    Phase: IpcExecuteOperationPhase.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations:
            [
                new IpcExecuteContractViolation(
                    OpId: new IpcExecuteStepId("query-1"),
                    Operation: UcliPrimitiveOperationNames.SceneQuery,
                    ExpectedFact: "operation.kind=query",
                    ObservedResult: "opResults[].applied=true",
                    ApplicationState: IpcApplicationState.Applied),
            ]);

        var json = IpcPayloadCodec.SerializeToElement(response);
        JsonAssert.For(json)
            .HasArrayLength("contractViolations", 1)
            .HasProperty("contractViolations", 0, violation => violation
                .HasString("opId", "query-1")
                .HasString("operation", UcliPrimitiveOperationNames.SceneQuery)
                .HasString("expectedFact", "operation.kind=query")
                .HasString("observedResult", "opResults[].applied=true")
                .HasString("applicationState", TextVocabulary.GetText(IpcApplicationState.Applied)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteResponse_OmitsPlanTokenWhenNull ()
    {
        var response = new IpcExecuteResponse(
            Array.Empty<IpcExecuteOperationResult>(),
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null);

        var jsonElement = IpcPayloadCodec.SerializeToElement(response);
        Assert.True(jsonElement.TryGetProperty("project", out _));
        Assert.False(jsonElement.TryGetProperty("planToken", out _));
        Assert.False(jsonElement.TryGetProperty("readPostcondition", out _));
        Assert.False(jsonElement.TryGetProperty("postReadSource", out _));
        Assert.False(jsonElement.TryGetProperty("contractViolations", out _));
    }

    private static IpcProjectIdentity CreateProjectIdentity ()
    {
        return new IpcProjectIdentity(
            projectPath: ProjectPath,
            projectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            unityVersion: "6000.1.4f1");
    }
}
