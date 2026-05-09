namespace MackySoft.Tests;

internal static class ErrorInspectTargetAssert
{
    public static void DoesNotUseBroadOrSensitiveTargets (IReadOnlyList<string> inspectTargets)
    {
        foreach (var inspectTarget in inspectTargets)
        {
            Assert.False(string.IsNullOrWhiteSpace(inspectTarget));
            Assert.DoesNotContain("planToken", inspectTarget, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sessionToken", inspectTarget, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual("payload", inspectTarget);
            Assert.NotEqual("payload.plan", inspectTarget);
            Assert.NotEqual("errors", inspectTarget);
            Assert.NotEqual("logs daemon", inspectTarget);
            Assert.NotEqual("logs unity", inspectTarget);
            Assert.NotEqual("ucli logs daemon --level error", inspectTarget);
            Assert.NotEqual("ucli logs unity --level error", inspectTarget);
        }
    }
}
