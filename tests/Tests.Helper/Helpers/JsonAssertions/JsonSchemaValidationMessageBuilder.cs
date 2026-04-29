namespace MackySoft.Tests;

using System.Text;

internal static class JsonSchemaValidationMessageBuilder
{
    public static string Build (IReadOnlyList<string> errors, string? schemaName)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var builder = new StringBuilder();
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            builder.Append("JSON schema validation failed.");
        }
        else
        {
            builder.Append($"JSON schema validation failed. schema={schemaName}");
        }

        foreach (var error in errors)
        {
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(error);
        }

        return builder.ToString();
    }
}
