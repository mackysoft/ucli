namespace MackySoft.Tests;

using System.Text.Json;
using Xunit.Sdk;

internal static class JsonAssert
{
    public static JsonAssertion For (JsonElement root)
    {
        return new JsonAssertion(root);
    }
}

internal sealed class JsonAssertion
{
    private readonly JsonAssertionContext context;

    public JsonAssertion (JsonElement root)
        : this(new JsonAssertionContext(root, "$"))
    {
    }

    private JsonAssertion (JsonAssertionContext context)
    {
        this.context = context;
    }

    public JsonAssertion HasProperty (string propertyName)
    {
        JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        return this;
    }

    public JsonAssertion HasProperty (string propertyName, Action<JsonAssertion> assertion)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        ApplyNestedAssertion(assertion, propertyContext);
        return this;
    }

    public JsonAssertion HasProperty (string propertyName, int index, Action<JsonAssertion> assertion)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        var indexContext = JsonPathResolver.ResolveIndexOrThrow(propertyContext, index);
        ApplyNestedAssertion(assertion, indexContext);
        return this;
    }

    public JsonAssertion HasIndex (int index)
    {
        JsonPathResolver.ResolveIndexOrThrow(context, index);
        return this;
    }

    public JsonAssertion HasIndex (int index, Action<JsonAssertion> assertion)
    {
        var indexContext = JsonPathResolver.ResolveIndexOrThrow(context, index);
        ApplyNestedAssertion(assertion, indexContext);
        return this;
    }

    public JsonAssertion HasProperties (params string[] propertyNames)
    {
        if (propertyNames is null || propertyNames.Length == 0)
        {
            throw new XunitException("At least one property path is required.");
        }

        foreach (var propertyName in propertyNames)
        {
            JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        }

        return this;
    }

    public JsonAssertion HasValueKind (string propertyName, JsonValueKind expect)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        JsonValueAssertion.AssertValueKind(propertyContext, expect);
        return this;
    }

    public JsonAssertion HasValueKind (JsonValueKind expect)
    {
        JsonValueAssertion.AssertValueKind(context, expect);
        return this;
    }

    public JsonAssertion HasString (string propertyName, string? expect)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        JsonValueAssertion.AssertString(propertyContext, expect);
        return this;
    }

    public JsonAssertion HasString (string? expect)
    {
        JsonValueAssertion.AssertString(context, expect);
        return this;
    }

    public JsonAssertion HasInt32 (string propertyName, int expect)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        JsonValueAssertion.AssertInt32(propertyContext, expect);
        return this;
    }

    public JsonAssertion HasInt32 (int expect)
    {
        JsonValueAssertion.AssertInt32(context, expect);
        return this;
    }

    public JsonAssertion HasBoolean (string propertyName, bool expect)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        JsonValueAssertion.AssertBoolean(propertyContext, expect);
        return this;
    }

    public JsonAssertion HasBoolean (bool expect)
    {
        JsonValueAssertion.AssertBoolean(context, expect);
        return this;
    }

    public JsonAssertion IsNull (string propertyName)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        JsonValueAssertion.AssertNull(propertyContext);
        return this;
    }

    public JsonAssertion IsNull ()
    {
        JsonValueAssertion.AssertNull(context);
        return this;
    }

    public JsonAssertion HasArrayLength (string propertyName, int expect)
    {
        var propertyContext = JsonPathResolver.ResolvePropertyOrThrow(context, propertyName);
        JsonValueAssertion.AssertArrayLength(propertyContext, expect);
        return this;
    }

    public JsonAssertion HasArrayLength (int expect)
    {
        JsonValueAssertion.AssertArrayLength(context, expect);
        return this;
    }

    public JsonAssertion MatchesSchema (JsonSchemaNode schema, string? schemaName = null)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var errors = JsonSchemaValidator.Validate(
            element: context.Value,
            schema: schema,
            path: context.Path);
        if (errors.Count > 0)
        {
            throw new XunitException(JsonSchemaValidationMessageBuilder.Build(errors, schemaName));
        }

        return this;
    }

    private static void ApplyNestedAssertion (Action<JsonAssertion> assertion, JsonAssertionContext nestedContext)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        assertion(new JsonAssertion(nestedContext));
    }
}
