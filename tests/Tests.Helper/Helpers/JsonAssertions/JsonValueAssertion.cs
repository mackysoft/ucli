namespace MackySoft.Tests;

using System.Text.Json;
using Xunit.Sdk;

internal static class JsonValueAssertion
{
    public static void AssertValueKind (JsonAssertionContext context, JsonValueKind expect)
    {
        if (context.Value.ValueKind != expect)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected kind '{expect}' but was '{context.Value.ValueKind}'.");
        }
    }

    public static void AssertString (JsonAssertionContext context, string? expect)
    {
        if (expect is null)
        {
            if (context.Value.ValueKind != JsonValueKind.Null)
            {
                throw new XunitException(
                    $"JSON path '{context.Path}' expected null but was '{context.Value.ValueKind}'.");
            }

            return;
        }

        if (context.Value.ValueKind != JsonValueKind.String)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected string but was '{context.Value.ValueKind}'.");
        }

        Assert.Equal(expect, context.Value.GetString());
    }

    public static void AssertInt32 (JsonAssertionContext context, int expect)
    {
        if (context.Value.ValueKind != JsonValueKind.Number)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected number but was '{context.Value.ValueKind}'.");
        }

        if (!context.Value.TryGetInt32(out var actual))
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected Int32-compatible number but was '{context.Value.GetRawText()}'.");
        }

        Assert.Equal(expect, actual);
    }

    public static void AssertBoolean (JsonAssertionContext context, bool expect)
    {
        if (context.Value.ValueKind != JsonValueKind.True && context.Value.ValueKind != JsonValueKind.False)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected boolean but was '{context.Value.ValueKind}'.");
        }

        Assert.Equal(expect, context.Value.GetBoolean());
    }

    public static void AssertNull (JsonAssertionContext context)
    {
        if (context.Value.ValueKind != JsonValueKind.Null)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected null but was '{context.Value.ValueKind}'.");
        }
    }

    public static void AssertArrayLength (JsonAssertionContext context, int expect)
    {
        if (context.Value.ValueKind != JsonValueKind.Array)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected array but was '{context.Value.ValueKind}'.");
        }

        Assert.Equal(expect, context.Value.GetArrayLength());
    }
}
