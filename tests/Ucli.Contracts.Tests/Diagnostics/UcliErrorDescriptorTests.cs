using MackySoft.Tests;

namespace MackySoft.Ucli.Contracts.Tests.Diagnostics;

public sealed class UcliErrorDescriptorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void KnownDescriptors_HaveUniqueCodes ()
    {
        var duplicateCodes = UcliKnownErrorDescriptors.All
            .GroupBy(static descriptor => descriptor.Code)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key.Value)
            .ToArray();

        Assert.Empty(duplicateCodes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void KnownDescriptors_IncludeEveryContractsErrorCodeDefinition ()
    {
        var expectedCodes = StaticFieldValueReader.ReadFromStaticClasses<UcliCode>(
            typeof(UcliCode).Assembly,
            "ErrorCodes");
        var actualCodes = UcliKnownErrorDescriptors.All
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
        var knownCodes = UcliKnownErrorDescriptors.All
            .Select(static descriptor => descriptor.Code)
            .ToHashSet();

        foreach (var descriptor in UcliKnownErrorDescriptors.All)
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

            ErrorInspectTargetAssert.DoesNotUseBroadOrSensitiveTargets(descriptor.Inspect);

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
    public void HierarchyPathUnrepresentableObjectsDescriptor_IsRegisteredAsDiagnostic ()
    {
        var descriptor = FindDescriptor(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects);

        Assert.Equal("diagnostic", descriptor.Category);
        Assert.Contains(UcliCommandIds.Query, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Call, descriptor.AppliesTo);
        Assert.Contains("payload.opResults[].diagnostics[]", descriptor.Inspect);
        Assert.Equal(UcliErrorRetryClassValues.ContextDependent, descriptor.ExecutionSemantics.SafeToRetry);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractViolationDescriptor_DoesNotImplyNotApplied ()
    {
        var descriptor = FindDescriptor(ExecuteRequestErrorCodes.OperationContractViolation);

        Assert.Equal("operationContract", descriptor.Category);
        Assert.Contains(UcliCommandIds.Call, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Plan, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Query, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Refresh, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Resolve, descriptor.AppliesTo);
        Assert.Null(descriptor.ExecutionSemantics.ImpliesNotApplied);
        Assert.True(descriptor.ExecutionSemantics.MayBeIndeterminate);
        Assert.Equal(UcliErrorRetryClassValues.ContextDependent, descriptor.ExecutionSemantics.SafeToRetry);
        Assert.Contains("payload.contractViolations[]", descriptor.Inspect);
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

    [Fact]
    [Trait("Size", "Small")]
    public void PlayModeSessionAndStateDescriptors_ApplyToPlayCommandFamily ()
    {
        var lifecycleCodes = new[]
        {
            PlayModeErrorCodes.PlayModeSessionNotAvailable,
            PlayModeErrorCodes.PlayModeStateUnknown,
        };

        foreach (var code in lifecycleCodes)
        {
            var descriptor = FindDescriptor(code);

            Assert.Equal("playMode", descriptor.Category);
            Assert.Contains(UcliCommandIds.PlayStatus, descriptor.AppliesTo);
            Assert.Contains(UcliCommandIds.PlayEnter, descriptor.AppliesTo);
            Assert.Contains(UcliCommandIds.PlayExit, descriptor.AppliesTo);
            Assert.Contains(UcliCommandIds.PlayWait, descriptor.AppliesTo);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayModeTransitionDescriptors_ApplyToTransitionCommands ()
    {
        var transitionCodes = new[]
        {
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            PlayModeErrorCodes.PlayModeTransitionBlocked,
        };

        foreach (var code in transitionCodes)
        {
            var descriptor = FindDescriptor(code);

            Assert.Contains(UcliCommandIds.PlayEnter, descriptor.AppliesTo);
            Assert.Contains(UcliCommandIds.PlayExit, descriptor.AppliesTo);
            Assert.Contains(UcliCommandIds.PlayWait, descriptor.AppliesTo);
            Assert.DoesNotContain(UcliCommandIds.PlayStatus, descriptor.AppliesTo);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayModeLeafSpecificDescriptors_ApplyOnlyToOwningLifecycleCommands ()
    {
        var alreadyChangingDescriptor = FindDescriptor(PlayModeErrorCodes.PlayModeAlreadyChanging);
        var enterRejectedDescriptor = FindDescriptor(PlayModeErrorCodes.PlayModeEnterRejected);
        var exitRejectedDescriptor = FindDescriptor(PlayModeErrorCodes.PlayModeExitRejected);

        Assert.Contains(UcliCommandIds.PlayEnter, alreadyChangingDescriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.PlayExit, alreadyChangingDescriptor.AppliesTo);
        Assert.DoesNotContain(UcliCommandIds.PlayStatus, alreadyChangingDescriptor.AppliesTo);
        Assert.DoesNotContain(UcliCommandIds.PlayWait, alreadyChangingDescriptor.AppliesTo);

        Assert.Equal([UcliCommandIds.PlayEnter], enterRejectedDescriptor.AppliesTo);
        Assert.Equal([UcliCommandIds.PlayExit], exitRejectedDescriptor.AppliesTo);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayModeGuiDescriptor_AppliesToMutationAndLifecycleCommands ()
    {
        var descriptor = FindDescriptor(PlayModeErrorCodes.PlayModeRequiresGuiEditor);

        Assert.Contains(UcliCommandIds.Plan, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Call, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.PlayStatus, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.PlayEnter, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.PlayExit, descriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.PlayWait, descriptor.AppliesTo);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExistingPlayModeMutationDescriptors_RemainScopedToRequestMutationCommands ()
    {
        var notActiveDescriptor = FindDescriptor(PlayModeErrorCodes.PlayModeNotActive);
        var persistenceForbiddenDescriptor = FindDescriptor(PlayModeErrorCodes.PlayModePersistenceForbidden);

        Assert.Contains(UcliCommandIds.Plan, notActiveDescriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Call, notActiveDescriptor.AppliesTo);
        Assert.DoesNotContain(UcliCommandIds.PlayEnter, notActiveDescriptor.AppliesTo);
        Assert.DoesNotContain(UcliCommandIds.PlayExit, notActiveDescriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Plan, persistenceForbiddenDescriptor.AppliesTo);
        Assert.Contains(UcliCommandIds.Call, persistenceForbiddenDescriptor.AppliesTo);
        Assert.DoesNotContain(UcliCommandIds.PlayEnter, persistenceForbiddenDescriptor.AppliesTo);
        Assert.DoesNotContain(UcliCommandIds.PlayExit, persistenceForbiddenDescriptor.AppliesTo);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayModeErrorCodes_DoNotExposePlayModeUnderscorePrefix ()
    {
        var codes = StaticFieldValueReader.ReadFromStaticClasses<UcliCode>(
            typeof(PlayModeErrorCodes).Assembly,
            "ErrorCodes");

        Assert.Contains(PlayModeErrorCodes.PlayModeSessionNotAvailable, codes);
        Assert.DoesNotContain(codes, static code => code.Value.StartsWith("PLAY_MODE_", StringComparison.Ordinal));
    }

    private static UcliErrorDescriptor FindDescriptor (UcliCode code)
    {
        return UcliKnownErrorDescriptors.All.Single(descriptor => descriptor.Code == code);
    }

}
