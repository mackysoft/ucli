using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterPostReadSourceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPostReadSourceIsPresent_PropagatesSourceFacts ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "edit-1",
                Op: "edit",
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: []),
        ])
        {
            PostReadSource = new IpcExecutePostReadSource(
                IpcExecutePostReadSource.CurrentSchemaVersion,
                [
                    new IpcExecutePostReadSourceStep(
                        OpId: "edit-1",
                        SourceKind: IpcExecutePostReadSourceKindNames.Edit,
                        PlayModeMutation: false,
                        Commit: IpcExecutePostReadCommitNames.Project,
                        PersistenceExpected: true,
                        ExpectedPostState: IpcExecuteExpectedPostStateNames.Deterministic),
                ]),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PostReadSource);
        Assert.Equal(1, result.PostReadSource!.SchemaVersion);
        var sourceStep = Assert.Single(result.PostReadSource.Steps);
        Assert.Equal("edit-1", sourceStep.OpId);
        Assert.Equal(IpcExecutePostReadSourceKindNames.Edit, sourceStep.SourceKind);
        Assert.Equal(IpcExecutePostReadCommitNames.Project, sourceStep.Commit);
        Assert.True(sourceStep.PersistenceExpected);
        Assert.Equal(IpcExecuteExpectedPostStateNames.Deterministic, sourceStep.ExpectedPostState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPostReadSourceIsEmpty_PropagatesSourceFacts ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            PostReadSource = new IpcExecutePostReadSource(IpcExecutePostReadSource.CurrentSchemaVersion, []),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PostReadSource);
        Assert.Empty(result.PostReadSource!.Steps);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPostReadSourceKindIsUnsupported_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "edit-1",
                  "op": "edit",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [],
                  "diagnostics": []
                }
              ],
              "postReadSource": {
                "schemaVersion": 1,
                "steps": [
                  {
                    "opId": "edit-1",
                    "sourceKind": "unsupported",
                    "playModeMutation": false,
                    "commit": "context",
                    "persistenceExpected": true,
                    "expectedPostState": "deterministic"
                  }
                ]
              }
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("postReadSource.steps[0].sourceKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPostReadSourceRequiredFieldIsMissing_ReturnsInternalError ()
    {
        foreach (MissingPostReadSourceFieldCase testCase in GetMissingPostReadSourceFieldCases())
        {
            var response = CreateResponse(CreatePostReadSourcePayload(testCase.StepJson));

            var result = ExecuteResponseConverter.Convert(response);

            Assert.False(result.IsSuccess);
            var error = Assert.Single(result.Errors);
            Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
            Assert.Contains(testCase.ExpectedFieldName, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPostReadSourceCombinationIsInvalid_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePostReadSourcePayload(
            """
            {
              "opId": "edit-1",
              "sourceKind": "operation",
              "playModeMutation": false,
              "commit": null,
              "persistenceExpected": false,
              "expectedPostState": "deterministic"
            }
            """,
            UcliPrimitiveOperationNames.SceneOpen));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("postReadSource.steps[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenEditCommitPersistenceCombinationIsInvalid_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePostReadSourcePayload(
            """
            {
              "opId": "edit-1",
              "sourceKind": "edit",
              "playModeMutation": false,
              "commit": "context",
              "persistenceExpected": false,
              "expectedPostState": "deterministic"
            }
            """));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("postReadSource.steps[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPostReadSourceStepIsMissingForOpResult_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "edit-1",
                  "op": "edit",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [],
                  "diagnostics": []
                }
              ],
              "postReadSource": {
                "schemaVersion": 1,
                "steps": []
              }
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("postReadSource.steps", error.Message, StringComparison.Ordinal);
    }

    private static MissingPostReadSourceFieldCase[] GetMissingPostReadSourceFieldCases ()
    {
        return
        [
            new(
                """
                {
                  "opId": "edit-1",
                  "sourceKind": "edit",
                  "commit": "context",
                  "persistenceExpected": true,
                  "expectedPostState": "deterministic"
                }
                """,
                "playModeMutation"),
            new(
                """
                {
                  "opId": "edit-1",
                  "sourceKind": "edit",
                  "playModeMutation": false,
                  "persistenceExpected": true,
                  "expectedPostState": "deterministic"
                }
                """,
                "commit"),
            new(
                """
                {
                  "opId": "edit-1",
                  "sourceKind": "edit",
                  "playModeMutation": false,
                  "commit": "context",
                  "expectedPostState": "deterministic"
                }
                """,
                "persistenceExpected"),
        ];
    }

    private readonly record struct MissingPostReadSourceFieldCase (
        string StepJson,
        string ExpectedFieldName);

    private static string CreatePostReadSourcePayload (
        string stepJson,
        string op = "edit")
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        return $$"""
        {
          "project": {
            "projectPath": "/repo/UnityProject",
            "projectFingerprint": "{{projectFingerprintText}}",
            "unityVersion": "6000.1.4f1"
          },
          "opResults": [
            {
              "opId": "edit-1",
              "op": "{{op}}",
              "phase": "call",
              "applied": true,
              "changed": true,
              "touched": [],
              "diagnostics": []
            }
          ],
          "postReadSource": {
            "schemaVersion": 1,
            "steps": [
              {{stepJson}}
            ]
          }
        }
        """;
    }
}
