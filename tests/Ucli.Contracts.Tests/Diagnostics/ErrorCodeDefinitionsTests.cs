namespace MackySoft.Ucli.Contracts.Tests.Diagnostics;

public sealed class ErrorCodeDefinitionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ErrorCodeDefinitions_ExposeExpectedPublicLiterals ()
    {
        Assert.Equal("INVALID_ARGUMENT", UcliCoreErrorCodes.InvalidArgument.Value);
        Assert.Equal("NOT_INITIALIZED", UcliCoreErrorCodes.NotInitialized.Value);
        Assert.Equal("SESSION_TOKEN_REQUIRED", IpcSessionErrorCodes.SessionTokenRequired.Value);
        Assert.Equal("SESSION_TOKEN_INVALID", IpcSessionErrorCodes.SessionTokenInvalid.Value);
        Assert.Equal("READ_INDEX_BOOTSTRAP_FAILED", ReadIndexErrorCodes.ReadIndexBootstrapFailed.Value);
        Assert.Equal("READ_INDEX_FORMAT_INVALID", ReadIndexErrorCodes.ReadIndexFormatInvalid.Value);
        Assert.Equal("READ_INDEX_FRESH_REQUIRED", ReadIndexErrorCodes.ReadIndexFreshRequired.Value);
        Assert.Equal("PLAN_TOKEN_REQUIRED", PlanTokenErrorCodes.PlanTokenRequired.Value);
        Assert.Equal("PLAN_TOKEN_INVALID", PlanTokenErrorCodes.PlanTokenInvalid.Value);
        Assert.Equal("PLAN_TOKEN_EXPIRED", PlanTokenErrorCodes.PlanTokenExpired.Value);
        Assert.Equal("PLAN_TOKEN_REQUEST_MISMATCH", PlanTokenErrorCodes.PlanTokenRequestMismatch.Value);
        Assert.Equal("STATE_CHANGED_SINCE_PLAN", PlanTokenErrorCodes.StateChangedSincePlan.Value);
        Assert.Equal("REQUEST_ID_CONFLICT", ExecuteRequestErrorCodes.RequestIdConflict.Value);
        Assert.Equal("OPERATION_CONTRACT_VIOLATION", ExecuteRequestErrorCodes.OperationContractViolation.Value);
        Assert.Equal("EDITOR_STARTING", EditorLifecycleErrorCodes.EditorStarting.Value);
        Assert.Equal("EDITOR_BUSY", EditorLifecycleErrorCodes.EditorBusy.Value);
        Assert.Equal("EDITOR_COMPILING", EditorLifecycleErrorCodes.EditorCompiling.Value);
        Assert.Equal("EDITOR_DOMAIN_RELOADING", EditorLifecycleErrorCodes.EditorDomainReloading.Value);
        Assert.Equal("EDITOR_PLAYMODE", EditorLifecycleErrorCodes.EditorPlaymode.Value);
        Assert.Equal("EDITOR_MODAL_BLOCKED", EditorLifecycleErrorCodes.EditorModalBlocked.Value);
        Assert.Equal("EDITOR_SAFE_MODE", EditorLifecycleErrorCodes.EditorSafeMode.Value);
        Assert.Equal("EDITOR_SHUTTING_DOWN", EditorLifecycleErrorCodes.EditorShuttingDown.Value);
        Assert.Equal("PLAYMODE_NOT_ACTIVE", PlayModeErrorCodes.PlayModeNotActive.Value);
        Assert.Equal("PLAYMODE_REQUIRES_GUI_EDITOR", PlayModeErrorCodes.PlayModeRequiresGuiEditor.Value);
        Assert.Equal("PLAYMODE_PERSISTENCE_FORBIDDEN", PlayModeErrorCodes.PlayModePersistenceForbidden.Value);
        Assert.Equal("SCREENSHOT_REQUIRES_GUI_SESSION", ScreenshotErrorCodes.ScreenshotRequiresGuiSession.Value);
        Assert.Equal("SCREENSHOT_REQUESTED_SIZE_UNSUPPORTED", ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported.Value);
        Assert.Equal("SCREENSHOT_CAPTURE_UNSUPPORTED", ScreenshotErrorCodes.ScreenshotCaptureUnsupported.Value);
        Assert.Equal("DAEMON_EDITOR_MODE_MISMATCH", DaemonErrorCodes.DaemonEditorModeMismatch.Value);
        Assert.Equal("INTERNAL_ERROR", UcliCoreErrorCodes.InternalError.Value);
    }
}
