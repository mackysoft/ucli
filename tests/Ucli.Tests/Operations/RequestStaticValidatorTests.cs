namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
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
    public async Task Validate_WhenOperationsAreNotProvided_SkipsMetadataDependentChecks ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-1", "ucli.unknown", new
                {
                }),
            ]);

        var result = await validator.Validate(
            request,
            RequestStaticValidationCatalog.Unavailable,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenOperationsAreNotProvided_StillValidatesEditLowering ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-invalid",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-invalid",
                      "actions": []
                    }
                    """),
            ]);

        var result = await validator.Validate(
            request,
            RequestStaticValidationCatalog.Unavailable,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.EditStepInvalid);
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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{}""")]
    [InlineData("""{"path":"Assets/Scenes/Main.unity","unexpected":true}""")]
    public async Task Validate_WhenOpStepArgsViolateRegisteredSchema_AddsOperationArgsInvalidError (string argsJson)
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep(
                    stepId: "step-scene-open",
                    operationName: UcliPrimitiveOperationNames.SceneOpen,
                    argsJson: argsJson),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCompSetArgsViolateMinItems_AddsOperationArgsInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-set", UcliPrimitiveOperationNames.CompSet, new
                {
                    target = new
                    {
                        globalObjectId = "GlobalObjectId_V1-2-3-4-5-6",
                    },
                    sets = Array.Empty<object>(),
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetsFindArgsContainNoFilters_AddsOperationArgsInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-assets-find", UcliPrimitiveOperationNames.AssetsFind, new
                {
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenResolvePrefabSelectorIncludesComponentType_AddsOperationArgsInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-resolve", UcliPrimitiveOperationNames.Resolve, new
                {
                    prefab = "Assets/Prefabs/Example.prefab",
                    hierarchyPath = "Root/Child",
                    componentType = "UnityEngine.Transform, UnityEngine.CoreModule",
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoCreateUsesPrefabParentSelector_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-create", UcliPrimitiveOperationNames.GoCreate, new
                {
                    name = "GeneratedChild",
                    parent = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy",
                    },
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoDescribeUsesPrefabTargetSelector_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-describe", UcliPrimitiveOperationNames.GoDescribe, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy",
                    },
                    depth = 1,
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCompSetUsesPrefabComponentSelector_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-set", UcliPrimitiveOperationNames.CompSet, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Armature",
                        componentType = "UnityEngine.Transform, UnityEngine.CoreModule",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "m_LocalPosition.x",
                            value = 1,
                        },
                    },
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetSetUsesProjectAssetPathTarget_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-asset-set", UcliPrimitiveOperationNames.AssetSet, new
                {
                    target = new
                    {
                        projectAssetPath = "ProjectSettings/TagManager.asset",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "tags.Array.size",
                            value = 1,
                        },
                    },
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetSchemaUsesTypeOnly_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-asset-schema", UcliPrimitiveOperationNames.AssetSchema, new
                {
                    type = "MyGame.ConfigAsset, Assembly-CSharp",
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCompSchemaUsesTypeOnly_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-comp-schema", UcliPrimitiveOperationNames.CompSchema, new
                {
                    type = "UnityEngine.Transform, UnityEngine.CoreModule",
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenAssetsFindUsesPathPrefixOnly_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-assets-find", UcliPrimitiveOperationNames.AssetsFind, new
                {
                    pathPrefix = "Assets/Data",
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoDeleteUsesPrefabTargetSelector_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-delete", UcliPrimitiveOperationNames.GoDelete, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Weapon",
                    },
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenGoReparentUsesPrefabSelectors_ReturnsValidResult ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-reparent", UcliPrimitiveOperationNames.GoReparent, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Weapon",
                    },
                    parent = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Hand",
                    },
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabCreateUsesPrefabTargetSelector_AddsOperationArgsInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-prefab-create", UcliPrimitiveOperationNames.PrefabCreate, new
                {
                    target = new
                    {
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy/Weapon",
                    },
                    path = "Assets/Prefabs/Generated.prefab",
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRawOpUsesVarSelector_AddsOperationArgsInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-go-create", UcliPrimitiveOperationNames.GoCreate, new
                {
                    name = "GeneratedChild",
                    parent = new
                    {
                        @var = "created-parent",
                    },
                }),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenRequestSatisfiesAllChecks ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneOpen, new
                {
                    path = "Assets/Scenes/Main.unity",
                }),
                CreateOpStep("step-2", UcliPrimitiveOperationNames.SceneTree, new
                {
                    path = "Assets/Scenes/Main.unity",
                }),
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
    public async Task Validate_WhenSceneCreateAssetOnlyEditDisallowsSceneOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-scene-create-asset",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-scene-create-asset",
                      "on": {
                        "scene": "Assets/Scenes/Main.unity"
                      },
                      "select": {
                        "gameObject": "Root/Spawner",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "createAsset",
                          "path": "Assets/Generated/SpawnConfig.asset",
                          "type": "Game.SpawnConfig, Assembly-CSharp"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.Validate(
            request,
            CreateUnityProject(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.create$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabCreateAssetOnlyEditDisallowsPrefabOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-prefab-create-asset",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-prefab-create-asset",
                      "on": {
                        "prefab": "Assets/Prefabs/Enemy.prefab"
                      },
                      "select": {
                        "gameObject": "Enemy",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "createAsset",
                          "path": "Assets/Generated/EnemyConfig.asset",
                          "type": "Game.EnemyConfig, Assembly-CSharp"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.Validate(
            request,
            CreateUnityProject(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.asset\\.create$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenSceneMutationEditDisallowsSceneOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-scene-ensure",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-scene-ensure",
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
                          "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.Validate(
            request,
            CreateUnityProject(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.comp\\.ensure$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabMutationEditDisallowsPrefabOpen_RemainsValid ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-prefab-ensure",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-prefab-ensure",
                      "on": {
                        "prefab": "Assets/Prefabs/Enemy.prefab"
                      },
                      "select": {
                        "gameObject": "Enemy",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "ensureComponent",
                          "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.Validate(
            request,
            CreateUnityProject(),
            CreateConfig(OperationPolicy.Advanced, "^ucli\\.comp\\.ensure$"),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPrefabEditContainsCreatePrefab_AddsEditStepInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateEditStep(
                    stepId: "edit-prefab-create-prefab",
                    """
                    {
                      "kind": "edit",
                      "id": "edit-prefab-create-prefab",
                      "on": {
                        "prefab": "Assets/Prefabs/Enemy.prefab"
                      },
                      "select": {
                        "gameObject": "Enemy",
                        "cardinality": "one"
                      },
                      "actions": [
                        {
                          "kind": "createPrefab",
                          "path": "Assets/Generated/EnemyChild.prefab"
                        }
                      ],
                      "commit": "none"
                    }
                    """),
            ]);

        var result = await validator.Validate(request, CreateUnityProject(), CreateConfig(OperationPolicy.Advanced, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.EditStepInvalid);
    }

    private static IRequestStaticValidator CreateValidator ()
    {
        var authorizationService = new OperationAuthorizationService();
        return new RequestStaticValidator(authorizationService);
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
                CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneOpen, new
                {
                    path = "Assets/Scenes/Main.unity",
                }),
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
                    CreateOpStep("dup", UcliPrimitiveOperationNames.SceneOpen, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
                    CreateOpStep("dup", UcliPrimitiveOperationNames.SceneTree, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
                ]),
            "operation-not-found" => CreateRequest(
                steps:
                [
                    CreateOpStep("step-1", "ucli.unknown"),
                ]),
            "operation-not-allowed" => CreateRequest(
                steps:
                [
                    CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneSave, new
                    {
                        path = "Assets/Scenes/Main.unity",
                    }),
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
        string operationName,
        object? args = null)
    {
        var stepElement = JsonSerializer.SerializeToElement(new
        {
            kind = "op",
            id = stepId,
            op = operationName,
            args = args ?? new
            {
            },
        });

        return new ValidateRequestStep(
            Kind: IpcRequestStepKind.Op,
            StepId: stepId,
            Op: operationName,
            Element: stepElement);
    }

    private static ValidateRequestStep CreateOpStep (
        string stepId,
        string operationName,
        string argsJson)
    {
        using var argsDocument = JsonDocument.Parse(argsJson);
        var stepElement = JsonSerializer.SerializeToElement(new
        {
            kind = "op",
            id = stepId,
            op = operationName,
            args = argsDocument.RootElement.Clone(),
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

}

internal static class RequestStaticValidatorTestExtensions
{
    public static ValueTask<ValidationResult> Validate (
        this IRequestStaticValidator validator,
        ValidateRequest request,
        IReadOnlyList<UcliOperationDescriptor>? operations,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        return validator.Validate(
            request,
            operations is null
                ? RequestStaticValidationCatalog.Unavailable
                : RequestStaticValidationCatalog.Available(operations),
            config,
            cancellationToken);
    }

    public static async ValueTask<ValidationResult> Validate (
        this IRequestStaticValidator validator,
        ValidateRequest request,
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);

        var operations = await new InMemoryOperationCatalogProvider()
            .GetOperations(cancellationToken)
            .ConfigureAwait(false);
        return await validator.Validate(
                request,
                RequestStaticValidationCatalog.Available(operations),
                config,
                cancellationToken)
            .ConfigureAwait(false);
    }
}