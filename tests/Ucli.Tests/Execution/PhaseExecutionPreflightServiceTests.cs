using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class PhaseExecutionPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsPreparedRequest_WhenInputIsValid ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.True(
            result.IsSuccess,
            result.Error?.Message ?? string.Join(" | ", result.ValidationErrors.Select(static x => $"{x.Code}:{x.Message}")));
        var preparedRequest = Assert.IsType<PhaseExecutionPreparedRequest>(result.PreparedRequest);
        Assert.Equal(RequestInputSource.StandardInput, preparedRequest.InputSource);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", preparedRequest.Request.RequestId);
        Assert.Equal(unityProjectPath, preparedRequest.UnityProject.UnityProjectRoot);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsPreparedRequest_WhenEditInputTargetsDirectComponentSelection ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "edit-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var saveResult = await configStore.Save(
            unityProjectPath,
            new UcliConfig(
                SchemaVersion: 1,
                OperationPolicy: OperationPolicy.Advanced,
                PlanTokenMode: PlanTokenMode.Optional,
                ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
                OperationAllowlist:
                [
                    "^ucli\\.",
                ]),
            CancellationToken.None);
        Assert.True(saveResult.IsSuccess);

        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
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
              ]
            }
            """;
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: configStore,
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.True(
            result.IsSuccess,
            result.Error?.Message ?? string.Join(" | ", result.ValidationErrors.Select(static x => $"{x.Code}:{x.Message}")));
        var preparedRequest = Assert.IsType<PhaseExecutionPreparedRequest>(result.PreparedRequest);
        Assert.NotNull(preparedRequest.Request.Steps);
        var directSelectionStep = Assert.IsType<ValidateRequestStep>(Assert.Single(preparedRequest.Request.Steps!));
        Assert.Equal("edit-1", directSelectionStep.StepId);
        Assert.Equal(IpcRequestStepKind.Edit, directSelectionStep.Kind);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsPreparedRequest_WhenEditInputUsesEnsureAndSetBindings ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "edit-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
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
              ]
            }
            """;
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new StaticConfigStore(CreateAdvancedConfig()),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.True(
            result.IsSuccess,
            result.Error?.Message ?? string.Join(" | ", result.ValidationErrors.Select(static x => $"{x.Code}:{x.Message}")));
        var preparedRequest = Assert.IsType<PhaseExecutionPreparedRequest>(result.PreparedRequest);
        Assert.Equal(RequestInputSource.StandardInput, preparedRequest.InputSource);
        Assert.NotNull(preparedRequest.Request.Steps);
        var bindingStep = Assert.IsType<ValidateRequestStep>(Assert.Single(preparedRequest.Request.Steps!));
        Assert.Equal(IpcRequestStepKind.Edit, bindingStep.Kind);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenRequestPathAndRedirectedStandardInputAreBothProvided ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "input-source-conflict");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestInputReader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult("""{"from":"stdin"}"""),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"from":"file"}"""));
        var service = CreateService(
            requestInputReader: requestInputReader,
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: "request.json", projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenRequestJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "invalid-json");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestInputReader = new RequestInputReader(
            isStandardInputRedirected: static () => true,
            readStandardInputAsync: static _ => Task.FromResult("{"),
            readRequestFileAsync: static (_, _) => Task.FromResult("""{"unused":true}"""));
        var service = CreateService(
            requestInputReader: requestInputReader,
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenRequestContainsUnknownTopLevelProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "request-unknown-property");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ],
              "unknown": 1
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unknown", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationArgsPropertyIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "args-missing");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__"
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("args", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenStepsPropertyIsNotArray ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "ops-invalid-kind");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": {}
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("steps", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationArgsPropertyIsNotObject ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "args-invalid-kind");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": []
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("args", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationContainsUnknownProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "operation-unknown-property");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  },
                  "unknown": 1
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unknown", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationIdContainsOuterWhitespace ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "operation-id-outer-whitespace");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": " op-1 ",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("id", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationNameContainsOuterWhitespace ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "operation-name-outer-whitespace");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": " ucli.scene.open ",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("op", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationAliasContractIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "operation-alias-invalid");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  },
                  "as": 123
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("as", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsInvalidArgument_WhenOperationExpectationContractIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "operation-expectation-invalid");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  },
                  "expect": {
                    "count": 1,
                    "min": 0
                  }
                }
              ]
            }
            """;
        requestJson = ReplaceSceneOpenOperationName(requestJson);
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("expect", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsValidationErrors_WhenStaticValidationFails ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "static-validation-fail");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        const string requestJson = """
            {
              "protocolVersion": 999,
              "requestId": "invalid",
              "steps": [
                {
                  "kind": "op",
                  "id": "dup",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                },
                {
                  "kind": "op",
                  "id": "dup",
                  "op": "ucli.unknown",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Null(result.Error);
        Assert.True(result.ValidationErrors.Count >= 2);
        Assert.Contains(result.ValidationErrors, static x => x.Code == ValidationErrorCodes.ProtocolVersionMismatch);
        Assert.Contains(result.ValidationErrors, static x => x.Code == ValidationErrorCodes.RequestIdInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsExecutionError_WhenProtocolVersionPropertyHasInvalidType ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "protocol-version-type-mismatch");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        const string requestJson = """
            {
              "protocolVersion": "1",
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": []
            }
            """;
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: CreateRequestStaticValidator());

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("protocolVersion", error.Message, StringComparison.Ordinal);
        Assert.Contains("integer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_ReturnsExecutionError_WhenStaticValidationCannotLoadCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("phase-preflight", "validation-catalog-failure");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        const string requestJson = """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
              "steps": [
                {
                  "kind": "op",
                  "id": "op-1",
                  "op": "__SCENE_OPEN_OP__",
                  "args": {
                    "path": "Assets/Scenes/Main.unity"
                  }
                }
              ]
            }
            """;
        var validator = new SpyRequestStaticValidator
        {
            Result = ValidationResult.Failure(ExecutionError.InternalError("catalog discovery failed")),
        };
        var service = CreateService(
            requestInputReader: new StubRequestInputReader(RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput)),
            requestJsonParser: new ValidateRequestJsonParser(),
            unityProjectResolver: new UnityProjectResolver(),
            configStore: new UcliConfigStore(),
            requestStaticValidator: validator);

        var result = await service.Prepare(requestPath: null, projectPath: unityProjectPath, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_PropagatesCancellationTokenToDependencies ()
    {
        var request = new ValidateRequest(
            ProtocolVersion: 1,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            Steps:
            [
                new ValidateRequestStep(
                    Kind: IpcRequestStepKind.Op,
                    StepId: "op-1",
                    Op: UcliPrimitiveOperationNames.SceneOpen,
                    Element: JsonSerializer.SerializeToElement(new
                    {
                        kind = "op",
                        id = "op-1",
                        op = UcliPrimitiveOperationNames.SceneOpen,
                        args = new
                        {
                        },
                    })),
            ]);
        var requestInputReader = new SpyRequestInputReader();
        var parser = new StubValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(request));
        var unityProjectResolver = new StubUnityProjectResolver();
        var configStore = new SpyConfigStore();
        var validator = new SpyRequestStaticValidator();
        var service = CreateService(requestInputReader, parser, unityProjectResolver, configStore, validator);
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        var result = await service.Prepare(requestPath: null, projectPath: null, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, requestInputReader.ReceivedToken);
        Assert.Equal(token, configStore.ReceivedToken);
        Assert.Equal(token, validator.ReceivedToken);
        Assert.NotNull(validator.ReceivedUnityProject);
        Assert.Equal("/tmp/project", validator.ReceivedUnityProject!.UnityProjectRoot);
    }

    private static PhaseExecutionPreflightService CreateService (
        IRequestInputReader requestInputReader,
        IValidateRequestJsonParser requestJsonParser,
        IUnityProjectResolver unityProjectResolver,
        IUcliConfigStore configStore,
        IRequestStaticValidator requestStaticValidator)
    {
        return new PhaseExecutionPreflightService(
            requestInputReader,
            requestJsonParser,
            new ProjectContextResolver(unityProjectResolver, configStore),
            requestStaticValidator);
    }

    private static IRequestStaticValidator CreateRequestStaticValidator ()
    {
        var operationCatalog = new OperationCatalog(new InMemoryOperationCatalogProvider());
        var authorizationService = new OperationAuthorizationService();
        return new RequestStaticValidator(operationCatalog, authorizationService);
    }

    private static UcliConfig CreateAdvancedConfig ()
    {
        return new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Advanced,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                "^ucli\\.",
            ]);
    }

    private static string ReplaceSceneOpenOperationName (string requestJson)
    {
        return requestJson.Replace("__SCENE_OPEN_OP__", UcliPrimitiveOperationNames.SceneOpen, StringComparison.Ordinal);
    }

    private sealed class StubRequestInputReader : IRequestInputReader
    {
        private readonly RequestInputReadResult readResult;

        public StubRequestInputReader (RequestInputReadResult readResult)
        {
            this.readResult = readResult ?? throw new ArgumentNullException(nameof(readResult));
        }

        public ValueTask<RequestInputReadResult> ReadAsync (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(readResult);
        }
    }

    private sealed class SpyRequestInputReader : IRequestInputReader
    {
        public CancellationToken ReceivedToken { get; private set; }

        public ValueTask<RequestInputReadResult> ReadAsync (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return ValueTask.FromResult(RequestInputReadResult.Success(
                json: ReplaceSceneOpenOperationName("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[{"kind":"op","id":"op-1","op":"__SCENE_OPEN_OP__","args":{}}]}"""),
                source: RequestInputSource.StandardInput));
        }
    }

    private sealed class StubValidateRequestJsonParser : IValidateRequestJsonParser
    {
        private readonly ValidateRequestJsonParseResult parseResult;

        public StubValidateRequestJsonParser (ValidateRequestJsonParseResult parseResult)
        {
            this.parseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
        }

        public ValidateRequestJsonParseResult Parse (string requestJson)
        {
            return parseResult;
        }
    }

    private sealed class StubUnityProjectResolver : IUnityProjectResolver
    {
        public UnityProjectResolutionResult Resolve (string? projectPath)
        {
            return UnityProjectResolutionResult.Success(new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/project",
                RepositoryRoot: "/tmp/repository",
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption));
        }
    }

    private sealed class SpyConfigStore : IUcliConfigStore
    {
        public CancellationToken ReceivedToken { get; private set; }

        public string GetConfigPath (string storageRoot)
        {
            return Path.Combine(storageRoot, ".ucli", "config.json");
        }

        public ValueTask<UcliConfigLoadResult> Load (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return ValueTask.FromResult(UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default));
        }

        public ValueTask<UcliConfigSaveResult> Save (
            string storageRoot,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticConfigStore : IUcliConfigStore
    {
        private readonly UcliConfig config;

        public StaticConfigStore (UcliConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string GetConfigPath (string storageRoot)
        {
            return Path.Combine(storageRoot, ".ucli", "config.json");
        }

        public ValueTask<UcliConfigLoadResult> Load (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(UcliConfigLoadResult.Success(config, ConfigSource.File));
        }

        public ValueTask<UcliConfigSaveResult> Save (
            string storageRoot,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyRequestStaticValidator : IRequestStaticValidator
    {
        public ValidationResult Result { get; set; } = ValidationResult.Success();

        public CancellationToken ReceivedToken { get; private set; }

        public ResolvedUnityProjectContext? ReceivedUnityProject { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            ReceivedUnityProject = unityProject;
            return ValueTask.FromResult(Result);
        }
    }
}