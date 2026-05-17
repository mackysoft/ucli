using System.Text.Json;

using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            Project = null!,
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'project' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectPropertyIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "opResults": []
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'project' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            null!,
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticsAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: [])
            {
                Diagnostics = null!,
            },
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].diagnostics", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticsPropertyIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "refresh",
                  "op": "ucli.project.refresh",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": []
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].diagnostics", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticSeverityIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: [])
            {
                Diagnostics =
                [
                    new IpcExecuteDiagnostic(
                        ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        "unsupported",
                        IpcExecuteDiagnosticCoverageImpactNames.Partial,
                        "coverage is partial."),
                ],
            },
        ])
        {
            Project = CreateProjectIdentity(),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].diagnostics[0].severity", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticCoverageImpactIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "query",
                  "op": "ucli.scene.query",
                  "phase": "plan",
                  "applied": false,
                  "changed": false,
                  "touched": [],
                  "diagnostics": [
                    {
                      "code": "HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS",
                      "severity": "warning",
                      "message": "coverage is partial."
                    }
                  ]
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].diagnostics[0].coverageImpact", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticCoverageImpactIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: [])
            {
                Diagnostics =
                [
                    new IpcExecuteDiagnostic(
                        ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        IpcExecuteDiagnosticSeverityNames.Warning,
                        "unsupported",
                        "coverage is partial."),
                ],
            },
        ])
        {
            Project = CreateProjectIdentity(),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].diagnostics[0].coverageImpact", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticIsPresent_PropagatesDiagnostic ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "query",
                Op: UcliPrimitiveOperationNames.SceneQuery,
                Phase: IpcExecuteOperationPhaseNames.Plan,
                Applied: false,
                Changed: false,
                Touched: [])
            {
                Diagnostics =
                [
                    new IpcExecuteDiagnostic(
                        ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        IpcExecuteDiagnosticSeverityNames.Warning,
                        IpcExecuteDiagnosticCoverageImpactNames.Partial,
                        "Scene query skipped GameObjects whose names contain '/'."),
                ],
            },
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Project);
        Assert.Equal("/repo/UnityProject", result.Project.ProjectPath);
        Assert.Equal("project-fingerprint", result.Project.ProjectFingerprint);
        Assert.Equal("6000.1.4f1", result.Project.UnityVersion);
        var opResult = Assert.Single(result.OpResults);
        var diagnostic = Assert.Single(opResult.Diagnostics);
        Assert.Equal(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects, diagnostic.Code);
        Assert.Equal(IpcExecuteDiagnosticSeverityNames.Warning, diagnostic.Severity);
        Assert.Equal(IpcExecuteDiagnosticCoverageImpactNames.Partial, diagnostic.CoverageImpact);
        Assert.Equal("Scene query skipped GameObjects whose names contain '/'.", diagnostic.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourcesAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: null!),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: null!,
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: []),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].opId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched:
                [
                    new IpcExecuteTouchedResource(
                        Kind: null!,
                        Path: "Assets/Example.txt",
                        Guid: null),
                ]),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched[0].kind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionRequirementsAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(null!),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: null!,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00")),
            ]),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements[0].surface", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOperationPhaseIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: "unknownPhase",
                Applied: true,
                Changed: true,
                Touched: []),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].phase", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceKindIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched:
                [
                    new IpcExecuteTouchedResource(
                        Kind: "unknownKind",
                        Path: "Assets/Example.txt",
                        Guid: null),
                ]),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched[0].kind", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: "unknownSurface",
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00")),
            ]),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements[0].surface", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenErrorsAreMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors: null!,
            HasFailureStatus: false);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'errors' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenErrorRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors:
            [
                new OperationExecutionError(default, "Unity execution failed.", null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("errors[0].code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenFailureStatusHasNoErrors_ReturnsStatusMessage ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors: [],
            HasFailureStatus: true,
            FailureStatus: "busy");

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("Execute response failed with status 'busy'.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPlanTokenValidationFails_PreservesOperationErrorCode ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors:
            [
                new OperationExecutionError(PlanTokenErrorCodes.PlanTokenInvalid, "Plan token is invalid.", null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlanTokenErrorCodes.PlanTokenInvalid, Assert.Single(result.Errors).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationsAreMissing_UsesEmptyCollection ()
    {
        var response = CreateResponse(new IpcExecuteResponse([]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.ContractViolations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationIsPresent_PropagatesViolation ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])
            {
                Project = CreateProjectIdentity(),
                ContractViolations =
                [
                    new IpcExecuteContractViolation(
                        OpId: "step-1",
                        Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                        ExpectedFact: "assurance.mayDirty=false",
                        ObservedResult: "opResults[].changed=true",
                        ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
                ],
            }),
            Errors:
            [
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    "step-1"),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var violation = Assert.Single(result.ContractViolations);
        Assert.Equal("step-1", violation.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, violation.Operation);
        Assert.Equal("assurance.mayDirty=false", violation.ExpectedFact);
        Assert.Equal("opResults[].changed=true", violation.ObservedResult);
        Assert.Equal(IpcExecuteApplicationStateNames.Indeterminate, violation.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationPayloadHasNoError_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ContractViolations =
            [
                new IpcExecuteContractViolation(
                    OpId: "step-1",
                    Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                    ExpectedFact: "assurance.mayDirty=false",
                    ObservedResult: "opResults[].changed=true",
                    ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
            ],
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("OPERATION_CONTRACT_VIOLATION", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationPayloadHasSuccessStatus_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])
            {
                Project = CreateProjectIdentity(),
                ContractViolations =
                [
                    new IpcExecuteContractViolation(
                        OpId: "step-1",
                        Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                        ExpectedFact: "assurance.mayDirty=false",
                        ObservedResult: "opResults[].changed=true",
                        ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
                ],
            }),
            Errors:
            [
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    "step-1"),
            ],
            HasFailureStatus: false);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("response status", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorOpIdDoesNotMatchPayload_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])
            {
                Project = CreateProjectIdentity(),
                ContractViolations =
                [
                    new IpcExecuteContractViolation(
                        OpId: "step-1",
                        Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                        ExpectedFact: "assurance.mayDirty=false",
                        ObservedResult: "opResults[].changed=true",
                        ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
                ],
            }),
            Errors:
            [
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    "step-2"),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("step-1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorOpIdIsMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])
            {
                Project = CreateProjectIdentity(),
                ContractViolations =
                [
                    new IpcExecuteContractViolation(
                        OpId: "step-1",
                        Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                        ExpectedFact: "assurance.mayDirty=false",
                        ObservedResult: "opResults[].changed=true",
                        ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
                ],
            }),
            Errors:
            [
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("errors[0].opId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorHasNoMatchingPayloadItem_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])
            {
                Project = CreateProjectIdentity(),
                ContractViolations =
                [
                    new IpcExecuteContractViolation(
                        OpId: "step-1",
                        Operation: UcliPrimitiveOperationNames.ProjectRefresh,
                        ExpectedFact: "assurance.mayDirty=false",
                        ObservedResult: "opResults[].changed=true",
                        ApplicationState: IpcExecuteApplicationStateNames.Indeterminate),
                ],
            }),
            Errors:
            [
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    "step-1"),
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    "step-2"),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("step-2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "opId": "step-1",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "indeterminate"
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("contractViolations[0].operation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationApplicationStateIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "opId": "step-1",
                  "operation": "ucli.project.refresh",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "maybeApplied"
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("contractViolations[0].applicationState", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorHasNoPayloadItems_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])
            {
                Project = CreateProjectIdentity(),
            }),
            Errors:
            [
                new OperationExecutionError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "Operation result violated declared assurance facts.",
                    "step-1"),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'contractViolations' field", error.Message, StringComparison.Ordinal);
    }

    private static UnityRequestResponse CreateResponse (IpcExecuteResponse payload)
    {
        if (payload.Project == IpcProjectIdentity.Unknown)
        {
            payload = payload with
            {
                Project = CreateProjectIdentity(),
            };
        }

        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: [],
            HasFailureStatus: false);
    }

    private static IpcProjectIdentity CreateProjectIdentity ()
    {
        return new IpcProjectIdentity(
            ProjectPath: "/repo/UnityProject",
            ProjectFingerprint: "project-fingerprint",
            UnityVersion: "6000.1.4f1");
    }

    private static UnityRequestResponse CreateResponse (string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return new UnityRequestResponse(
            Payload: document.RootElement.Clone(),
            Errors: [],
            HasFailureStatus: false);
    }
}
