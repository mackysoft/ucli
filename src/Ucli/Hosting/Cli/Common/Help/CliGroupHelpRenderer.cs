using System.Text;
using ConsoleAppFramework;

namespace MackySoft.Ucli.Hosting.Cli.Common.Help;

/// <summary> Renders help for command groups that do not have executable framework handlers. </summary>
internal static class CliGroupHelpRenderer
{
    /// <summary> Renders one command group and its executable descendants from framework command metadata. </summary>
    public static string Render (
        string groupPath,
        IReadOnlyList<CommandHelpDefinition> commandDefinitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupPath);
        ArgumentNullException.ThrowIfNull(commandDefinitions);

        var prefix = groupPath + " ";
        var descendants = new List<(string RelativePath, string Description)>();
        var maximumRelativePathLength = 0;
        for (var i = 0; i < commandDefinitions.Count; i++)
        {
            var command = commandDefinitions[i];
            if (!command.CommandName.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = command.CommandName[prefix.Length..];
            if (relativePath.Length == 0 || string.IsNullOrWhiteSpace(command.Description))
            {
                throw new InvalidOperationException($"Command metadata is incomplete for '{command.CommandName}'.");
            }

            descendants.Add((relativePath, command.Description));
            maximumRelativePathLength = Math.Max(maximumRelativePathLength, relativePath.Length);
        }

        if (descendants.Count == 0)
        {
            throw new InvalidOperationException($"Command group '{groupPath}' has no executable descendants.");
        }

        descendants.Sort(static (left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));

        var builder = new StringBuilder();
        builder.Append("Usage: ");
        builder.Append(groupPath);
        builder.AppendLine(" <command> [-h|--help] [--version]");
        builder.AppendLine();
        builder.AppendLine("Commands:");
        for (var i = 0; i < descendants.Count; i++)
        {
            var descendant = descendants[i];
            builder.Append("  ");
            builder.Append(descendant.RelativePath);
            builder.Append(' ', maximumRelativePathLength - descendant.RelativePath.Length + 2);
            builder.AppendLine(descendant.Description);
        }

        return builder.ToString();
    }
}
