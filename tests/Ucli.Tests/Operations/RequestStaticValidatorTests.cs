namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

public sealed class RequestStaticValidatorTests
{
    public static TheoryData<string, string> InvalidRequestCases => new()
    {
        { "protocol-version-mismatch", ValidationErrorCodes.ProtocolVersionMismatch },
        { "request-id-invalid", ValidationErrorCodes.RequestIdInvalid },
        { "request-id-not-canonical-d", ValidationErrorCodes.RequestIdInvalid },
        { "steps-required", ValidationErrorCodes.StepsRequired },
        { "step-id-duplicated", ValidationErrorCodes.StepIdDuplicated },
        { "operation-not-found", ValidationErrorCodes.OperationNotFound },
        { "operation-not-allowed", ValidationErrorCodes.OperationNotAllowed },
        { "edit-step-invalid", ValidationErrorCodes.EditStepInvalid },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task Validate_AddsExpectedError_WhenRequestIsInvalid (
        string scenario,
        string expectedErrorCode)
    {
        var validator = CreateValidator();
        var request = CreateInvalidRequest(scenario);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        AssertContainsError(result, expectedErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AddsRequiredErrors_WhenStepsContainsNullElement ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                null,
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.StepIdRequired);
        AssertContainsError(result, ValidationErrorCodes.StepKindRequired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AllowsEmptyStepsAsNoOpRequest ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(steps: Array.Empty<ValidateRequestStep?>());

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenEmptyStepsAndHeaderIsInvalid_PreservesHeaderErrors ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            protocolVersion: IpcProtocol.CurrentVersion + 1,
            requestId: "invalid-request-id",
            steps: Array.Empty<ValidateRequestStep?>());

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.ProtocolVersionMismatch);
        AssertContainsError(result, ValidationErrorCodes.RequestIdInvalid);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"scene":"Assets/Scenes/Main.unity"}""")]
    [InlineData("""{"unknown":"value"}""")]
    public async Task Validate_WhenEditSelectFromArgsAreInvalid_AddsEditStepInvalidError (string fromArgsJson)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-query",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-query",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "from": {
                          "op": "__SCENE_QUERY_OP__",
                          "args": __ARGS__
                        },
                        "cardinality": "all"
                      },
                      "actions": [
                        {
                          "kind": "delete"
                        }
                      ],
                      "commit": "context"
                    }
                    """
                        .Replace("__ARGS__", fromArgsJson, StringComparison.Ordinal)
                        .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal)),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.EditStepInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenRequestSatisfiesAllChecks ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneOpen),
                CreateOpStep("step-2", UcliPrimitiveOperationNames.SceneTree),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestUsesEnsureAndSetBindings ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-1",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-1",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "gameObject": "Root/Spawner",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "ensureComponent",
                          "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                          "as": "collider"
                        },
                        {
                          "kind": "set",
                          "target": "$collider",
                          "values": {
                            "isTrigger": true
                          }
                        }
                      ],
                      "commit": "context"
                    }
                    """),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestUsesSceneQuerySelection ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-query",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-query",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "from": {
                          "op": "__SCENE_QUERY_OP__",
                          "args": {
                            "pathPrefix": "Root/Enemies",
                            "componentType": "Game.EnemySpawner, Assembly-CSharp"
                          }
                        },
                        "cardinality": "all"
                      },
                      "actions": [
                        {
                          "kind": "set",
                          "values": {
                            "spawnInterval": 3.0
                          }
                        }
                      ],
                      "commit": "context"
                    }
                    """
                        .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal)),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenEditRequestTargetsDirectComponentSelection ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-1",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-1",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "gameObject": "Root/Spawner",
                        "component": "Game.EnemySpawner, Assembly-CSharp",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "set",
                          "values": {
                            "spawnInterval": 3.0
                          }
                        }
                      ],
                      "commit": "context"
                    }
                    """),
            ]);

        var result = await validator.Validate(
            request,
            CreateUnityProject(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\."),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogDiscoveryThrows_ReturnsFailureResult ()
    {
        var authorizationService = new OperationAuthorizationService();
        var validator = new RequestStaticValidator(new ThrowingOperationCatalog(), authorizationService);

        var result = await validator.Validate(
            CreateRequest(),
            CreateUnityProject(),
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("operation metadata", error.Message, StringComparison.Ordinal);
    }

    private static IRequestStaticValidator CreateValidator ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());
        var authorizationService = new OperationAuthorizationService();
        return new RequestStaticValidator(catalog, authorizationService);
    }

    private static ValidateRequest CreateRequest (
        int protocolVersion = IpcProtocol.CurrentVersion,
        string? requestId = null,
        IReadOnlyList<ValidateRequestStep?>? steps = null)
    {
        return new ValidateRequest(
            ProtocolVersion: protocolVersion,
            RequestId: requestId ?? Guid.NewGuid().ToString(),
            Steps: steps ??
            [
                CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneOpen),
            ]);
    }

    private static ValidateRequest CreateInvalidRequest (string scenario)
    {
        return scenario switch
        {
            "protocol-version-mismatch" => CreateRequest(protocolVersion: IpcProtocol.CurrentVersion + 1),
            "request-id-invalid" => CreateRequest(requestId: "invalid-request-id"),
            "request-id-not-canonical-d" => CreateRequest(requestId: Guid.NewGuid().ToString("B")),
            "steps-required" => new ValidateRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: Guid.NewGuid().ToString(),
                Steps: null),
            "step-id-duplicated" => CreateRequest(
                steps:
                [
                    CreateOpStep("dup", UcliPrimitiveOperationNames.SceneOpen),
                    CreateOpStep("dup", UcliPrimitiveOperationNames.SceneTree),
                ]),
            "operation-not-found" => CreateRequest(
                steps:
                [
                    CreateOpStep("step-1", "ucli.unknown"),
                ]),
            "operation-not-allowed" => CreateRequest(
                steps:
                [
                    CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneSave),
                ]),
            "edit-step-invalid" => CreateRequest(
                steps:
                [
                    CreateEditStep(
                        stepId: "edit-1",
                        """
                        {
                          "kind": "edit",
                          "id": "edit-1",
                          "on": {
                            "scene": "Assets/Scenes/Main.unity"
                          },
                          "select": {
                            "gameObject": "Root/Spawner",
                            "cardinality": "one"
                          },
                          "actions": [
                            {
                              "kind": "set",
                              "target": "$missing",
                              "values": {
                                "spawnInterval": 3.0
                              }
                            }
                          ],
                          "commit": "context"
                        }
                        """),
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported invalid request scenario."),
        };
    }

    private static ValidateRequestStep CreateOpStep (
        string stepId,
        string operationName)
    {
        var stepElement = JsonSerializer.SerializeToElement(new
        {
            kind = "op",
            id = stepId,
            op = operationName,
            args = new
            {
            },
        });

        return new ValidateRequestStep(
            Kind: IpcRequestStepKind.Op,
            StepId: stepId,
            Op: operationName,
            Element: stepElement);
    }

    private static ValidateRequestStep CreateEditStep (
        string stepId,
        string stepJson)
    {
        using var document = JsonDocument.Parse(stepJson);
        return new ValidateRequestStep(
            Kind: IpcRequestStepKind.Edit,
            StepId: stepId,
            Op: null,
            Element: document.RootElement.Clone());
    }

    private static UcliConfig CreateConfig (
        OperationPolicy operationPolicy,
        params string[] allowlistPatterns)
    {
        return new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: operationPolicy,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist: allowlistPatterns);
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/project",
            RepositoryRoot: "/tmp/repository",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static void AssertContainsError (ValidationResult result, string errorCode)
    {
        Assert.Contains(
            result.Errors,
            error => string.Equals(error.Code, errorCode, StringComparison.Ordinal));
    }

    private sealed class ThrowingOperationCatalog : IOperationCatalog
    {
        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("catalog discovery failed");
        }
    }
}