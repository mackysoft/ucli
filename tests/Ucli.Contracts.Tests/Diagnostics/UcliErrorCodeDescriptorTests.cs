using System.Reflection;

namespace MackySoft.Ucli.Contracts.Tests.Diagnostics;

public sealed class UcliErrorCodeDescriptorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void KnownDescriptors_HaveUniqueCodes ()
    {
        var duplicateCodes = UcliKnownErrorCodeDescriptors.All
            .GroupBy(static descriptor => descriptor.Code)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key.Value)
            .ToArray();

        Assert.Empty(duplicateCodes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void KnownDescriptors_IncludeKnownContractsErrorCodes ()
    {
        var expectedCodes = new[]
        {
            UcliCoreErrorCodes.InvalidArgument,
            UcliCoreErrorCodes.NotInitialized,
            UcliCoreErrorCodes.CommandNotImplemented,
            UcliCoreErrorCodes.InternalError,
            DaemonErrorCodes.DaemonEditorModeMismatch,
            EditorLifecycleErrorCodes.EditorStarting,
            EditorLifecycleErrorCodes.EditorBusy,
            EditorLifecycleErrorCodes.EditorCompiling,
            EditorLifecycleErrorCodes.EditorDomainReloading,
            EditorLifecycleErrorCodes.EditorPlaymode,
            EditorLifecycleErrorCodes.EditorModalBlocked,
            EditorLifecycleErrorCodes.EditorSafeMode,
            EditorLifecycleErrorCodes.EditorShuttingDown,
            ExecuteRequestErrorCodes.RequestIdConflict,
            IpcProtocolErrorCodes.ProtocolVersionMismatch,
            IpcProtocolErrorCodes.IpcMethodNotSupported,
            IpcProtocolErrorCodes.IpcFrameTooLarge,
            IpcSessionErrorCodes.SessionTokenRequired,
            IpcSessionErrorCodes.SessionTokenInvalid,
            IpcTransportErrorCodes.IpcTimeout,
            OperationAuthorizationErrorCodes.OperationNotAllowed,
            PlanTokenErrorCodes.PlanTokenRequired,
            PlanTokenErrorCodes.PlanTokenInvalid,
            PlanTokenErrorCodes.PlanTokenExpired,
            PlanTokenErrorCodes.PlanTokenRequestMismatch,
            PlanTokenErrorCodes.StateChangedSincePlan,
            PlayModeErrorCodes.PlayModeNotActive,
            PlayModeErrorCodes.PlayModeRequiresGuiEditor,
            PlayModeErrorCodes.PlayModePersistenceForbidden,
            ReadIndexErrorCodes.ReadIndexBootstrapFailed,
            ReadIndexErrorCodes.ReadIndexFormatInvalid,
            ReadIndexErrorCodes.ReadIndexFreshRequired,
        };
        var actualCodes = UcliKnownErrorCodeDescriptors.All
            .Select(static descriptor => descriptor.Code)
            .ToHashSet();

        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, actualCodes);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void KnownDescriptors_IncludeEveryContractsErrorCodeDefinition ()
    {
        var expectedCodes = GetErrorCodeDefinitions(typeof(UcliErrorCode).Assembly);
        var actualCodes = UcliKnownErrorCodeDescriptors.All
            .Select(static descriptor => descriptor.Code)
            .ToHashSet();

        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, actualCodes);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void KnownDescriptors_HaveValidRequiredMetadata ()
    {
        var knownCodes = UcliKnownErrorCodeDescriptors.All
            .Select(static descriptor => descriptor.Code)
            .ToHashSet();

        foreach (var descriptor in UcliKnownErrorCodeDescriptors.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Code.Value));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Category));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Summary));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Meaning));
            Assert.NotNull(descriptor.AppliesTo);
            Assert.NotNull(descriptor.PossiblePhases);
            Assert.NotNull(descriptor.ExecutionSemantics);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.ExecutionSemantics.SafeToRetry));
            Assert.True(UcliErrorRetryClassValues.IsKnown(descriptor.ExecutionSemantics.SafeToRetry));
            Assert.NotNull(descriptor.Inspect);
            Assert.NotNull(descriptor.NextActions);
            Assert.NotNull(descriptor.RelatedCodes);

            foreach (var command in descriptor.AppliesTo)
            {
                Assert.True(command.IsValid);
            }

            foreach (var possiblePhase in descriptor.PossiblePhases)
            {
                Assert.False(string.IsNullOrWhiteSpace(possiblePhase));
            }

            foreach (var inspectItem in descriptor.Inspect)
            {
                Assert.False(string.IsNullOrWhiteSpace(inspectItem));
                Assert.DoesNotContain("planToken", inspectItem, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("sessionToken", inspectItem, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual("payload.plan", inspectItem);
            }

            foreach (var nextAction in descriptor.NextActions)
            {
                Assert.NotNull(nextAction);
                Assert.False(string.IsNullOrWhiteSpace(nextAction.Action));
            }

            foreach (var relatedCode in descriptor.RelatedCodes)
            {
                Assert.NotEqual(descriptor.Code, relatedCode);
                Assert.Contains(relatedCode, knownCodes);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProtocolVersionMismatchDescriptor_AppliesToValidate ()
    {
        var descriptor = FindDescriptor(IpcProtocolErrorCodes.ProtocolVersionMismatch);

        Assert.Contains(UcliCommandIds.Validate, descriptor.AppliesTo);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTimeoutDescriptor_MatchesPublishedTimeoutContract ()
    {
        var descriptor = FindDescriptor(IpcTransportErrorCodes.IpcTimeout);

        Assert.Contains(UcliCommandIds.Status, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.DaemonStatus, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.DaemonList, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Ops, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.TestRun, descriptor.AppliesTo);
        Assert.DoesNotContain("payload.executionState", descriptor.Inspect);
        Assert.Contains("payload.readPostcondition", descriptor.Inspect);
    }

    private static UcliErrorCodeDescriptor FindDescriptor (UcliErrorCode code)
    {
        return UcliKnownErrorCodeDescriptors.All.Single(descriptor => descriptor.Code == code);
    }

    private static IReadOnlySet<UcliErrorCode> GetErrorCodeDefinitions (Assembly assembly)
    {
        var codes = new HashSet<UcliErrorCode>();
        var types = assembly.GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: true, IsSealed: true }
                && type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal));

        foreach (var type in types)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(UcliErrorCode))
                {
                    codes.Add((UcliErrorCode)field.GetValue(null)!);
                }
            }
        }

        return codes;
    }
}
