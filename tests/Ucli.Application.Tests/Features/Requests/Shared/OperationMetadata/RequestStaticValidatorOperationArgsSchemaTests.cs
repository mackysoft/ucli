namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorOperationArgsSchemaTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenArgsDoNotSatisfyOperationSchema_AddsOperationArgsInvalidError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep(
                    stepIndex: 0,
                    operationName: UcliPrimitiveOperationNames.SceneOpen,
                    argsJson: """{}"""),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OperationArgsInvalid);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenActualOperationSchemaUsesLocalReferences_AcceptsMatchingArgs ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep(0, UcliPrimitiveOperationNames.GoDescribe, new
                {
                    target = new
                    {
                        kind = "prefabHierarchy",
                        prefab = "Assets/Prefabs/Enemy.prefab",
                        hierarchyPath = "Enemy",
                    },
                    depth = 1,
                }),
            ]);

        var result = await validator.ValidateAsync(
            request,
            ValidationUnityProject,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenRegisteredOperationSchemaRequiresExternalReference_ReturnsInternalErrorWithoutFetching ()
    {
        const string operationName = "ucli.test.external-reference";
        UcliOperationDescriptor[] operations =
        [
            new(
                Name: operationName,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson:
                    """
                    {
                      "type": "object",
                      "properties": {
                        "value": {
                          "$ref": "https://schemas.example.test/value.schema.json"
                        }
                      },
                      "required": ["value"]
                    }
                    """,
                DescriptorDigest: Sha256DigestTestFactory.Compute(operationName),
                VerdictContract: null,
                ResultSchemaJson: null,
                Exposure: UcliOperationExposure.Public),
        ];
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep(0, operationName, new
                {
                    value = 1,
                }),
            ]);

        var result = await validator.ValidateAsync(
            request,
            operations,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenProviderOperationSchemaPatternIsInvalid_ReturnsInternalError ()
    {
        const string operationName = "ucli.test.invalid-pattern";
        UcliOperationDescriptor[] operations =
        [
            new(
                Name: operationName,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson:
                    """
                    {
                      "type": "object",
                      "properties": {
                        "value": {
                          "type": "string",
                          "pattern": "["
                        }
                      },
                      "required": ["value"],
                      "additionalProperties": false
                    }
                    """,
                DescriptorDigest: Sha256DigestTestFactory.Compute(operationName),
                VerdictContract: null,
                ResultSchemaJson: null,
                Exposure: UcliOperationExposure.Public),
        ];
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep(0, operationName),
            ]);

        var result = await validator.ValidateAsync(
            request,
            operations,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }
}
