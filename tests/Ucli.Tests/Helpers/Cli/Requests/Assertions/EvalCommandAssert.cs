using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Tests;

internal static class EvalCommandAssert
{
    public static void HasDedicatedSuccessPayload (CommandExecutionResult result, CsEvalSourceKind sourceKind = CsEvalSourceKind.Snippet)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var output = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(output.RootElement, UcliCommandNames.Eval);
        var payload = output.RootElement.GetProperty("payload");
        Assert.True(payload.TryGetProperty("requestId", out _));
        Assert.True(payload.TryGetProperty("project", out _));
        Assert.Equal("applied", payload.GetProperty("applicationState").GetString());
        var sourceKindText = TextVocabulary.GetText(sourceKind);
        Assert.Equal(sourceKindText, payload.GetProperty("eval").GetProperty("sourceKind").GetString());
        var plan = payload.GetProperty("plan");
        Assert.Equal(sourceKindText, plan.GetProperty("eval").GetProperty("sourceKind").GetString());
        Assert.Equal("notApplied", plan.GetProperty("applicationState").GetString());
        Assert.True(plan.TryGetProperty("planToken", out _));
        Assert.True(payload.TryGetProperty("readPostcondition", out _));
        Assert.False(payload.TryGetProperty("opResults", out _));
    }

    public static void HasDedicatedDispatch (
        RecordingEvalService service,
        string expectedSource,
        CsEvalSourceKind expectedSourceKind,
        bool expectedAllowDangerous,
        bool expectedAllowPlayMode)
    {
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedSource, invocation.Input.Source);
        Assert.Equal(expectedSourceKind, invocation.Input.SourceKind);
        Assert.Equal(expectedAllowDangerous, invocation.Input.AllowDangerous);
        Assert.Equal(expectedAllowPlayMode, invocation.Input.AllowPlayMode);
    }
}
