using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class ArchitectureTestRepository
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static string ToFullPath (string repositoryRelativePath)
    {
        return Path.Combine(RepositoryRoot, repositoryRelativePath);
    }

    internal static IEnumerable<string> EnumerateCSharpSourceFiles (string repositoryRelativeDirectory)
    {
        return Directory
            .EnumerateFiles(ToFullPath(repositoryRelativeDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(static sourceFile =>
            {
                var relativePath = NormalizeRepositoryRelativePath(sourceFile);
                return !relativePath.Contains("/bin/", StringComparison.Ordinal)
                    && !relativePath.Contains("/obj/", StringComparison.Ordinal)
                    && !relativePath.Contains("/Library/", StringComparison.Ordinal)
                    && !relativePath.Contains("/Temp/", StringComparison.Ordinal);
            });
    }

    internal static IEnumerable<string> EnumerateProductionProjectFiles ()
    {
        return Directory
            .EnumerateFiles(ToFullPath("src"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !IsUnityGeneratedProjectFile(relativePath));
    }

    internal static IEnumerable<string> EnumerateTestProjectFiles ()
    {
        return Directory
            .EnumerateFiles(ToFullPath("tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath);
    }

    internal static IEnumerable<string> EnumerateRepositoryAssemblyInfoFiles ()
    {
        var ownedAssemblyInfoRoots = new[]
        {
            "src/Ucli.Application",
            "src/Ucli.Contracts",
            "src/Ucli.Infrastructure",
            "src/Ucli/Hosting",
            "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity",
            "tests/Tests.Helper",
        };

        return ownedAssemblyInfoRoots
            .Select(ToFullPath)
            .SelectMany(static root => Directory.EnumerateFiles(root, "AssemblyInfo.cs", SearchOption.AllDirectories))
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !relativePath.Contains("/bin/", StringComparison.Ordinal)
                                          && !relativePath.Contains("/obj/", StringComparison.Ordinal));
    }

    internal static string NormalizeRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
    }

    internal static string ReadCSharpSourceWithoutCommentsAndStringLiterals (string sourceFile)
    {
        return StripCSharpCommentsAndStringLiterals(File.ReadAllText(sourceFile));
    }

    internal static string[] ReadProjectReferences (string projectPath)
    {
        var projectFullPath = ToFullPath(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectFullPath}");
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRepositoryRelativePath(Path.GetFullPath(Path.Combine(projectDirectory, value!))))
            .ToArray();
    }

    internal static string[] ReadPackageReferences (string projectPath)
    {
        var projectFullPath = ToFullPath(projectPath);
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    internal static string[] ReadUnityAsmdefReferences (string asmdefPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ToFullPath(asmdefPath)));
        if (!document.RootElement.TryGetProperty("references", out var references)
            || references.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return references
            .EnumerateArray()
            .Where(static element => element.ValueKind == JsonValueKind.String)
            .Select(static element => element.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    internal static string[] ReadUnityPackageIds (string packagesConfigPath)
    {
        var document = XDocument.Load(ToFullPath(packagesConfigPath));
        return document
            .Descendants("package")
            .Select(element => element.Attribute("id")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    internal static IEnumerable<string> ReadConcreteTypeNames (string sourceFile)
    {
        var sourceText = ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
        return Regex
            .Matches(
                sourceText,
                @"\b(?:class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b|\brecord\s+(?:class\s+|struct\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\b")
            .Select(static match => match.Groups["name"].Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value));
    }

    internal static string[] ReadInternalsVisibleToAssemblyNames (string assemblyInfoPath)
    {
        var sourceText = File.ReadAllText(ToFullPath(assemblyInfoPath));
        const string marker = "InternalsVisibleTo(\"";
        var friends = new List<string>();
        var searchIndex = 0;
        while (true)
        {
            var markerIndex = sourceText.IndexOf(marker, searchIndex, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return friends.ToArray();
            }

            var valueStart = markerIndex + marker.Length;
            var valueEnd = sourceText.IndexOf('"', valueStart);
            if (valueEnd < 0)
            {
                throw new InvalidOperationException($"Invalid InternalsVisibleTo declaration in {assemblyInfoPath}.");
            }

            friends.Add(sourceText[valueStart..valueEnd]);
            searchIndex = valueEnd + 1;
        }
    }

    internal static IEnumerable<PublicSurfaceDeclaration> ReadPublicSurfaceDeclarations (string sourceFile)
    {
        var sourceText = ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
        var currentNamespace = string.Empty;
        var signature = new StringBuilder();
        var publicTypeBodyDepths = new Stack<int>();
        var signatureStartLine = 0;
        var readsSignature = false;
        var parenthesisDepth = 0;
        var braceDepth = 0;
        var lines = sourceText.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            PopClosedPublicTypeFrames(publicTypeBodyDepths, braceDepth);

            var line = lines[lineIndex].TrimEnd('\r');
            var trimmedLine = line.Trim();
            var lineBraceDelta = CountCharacter(trimmedLine, '{') - CountCharacter(trimmedLine, '}');

            if (!readsSignature)
            {
                currentNamespace = ReadNamespaceDeclaration(trimmedLine) ?? currentNamespace;
                if (!IsPublicSurfaceDeclarationStart(trimmedLine))
                {
                    braceDepth += lineBraceDelta;
                    continue;
                }

                readsSignature = true;
                signature.Clear();
                signatureStartLine = lineIndex + 1;
                parenthesisDepth = 0;
            }

            AppendSignatureLine(signature, trimmedLine);
            parenthesisDepth += CountCharacter(trimmedLine, '(') - CountCharacter(trimmedLine, ')');

            if (!IsPublicSurfaceSignatureComplete(trimmedLine, parenthesisDepth))
            {
                braceDepth += lineBraceDelta;
                continue;
            }

            var signatureText = signature.ToString();
            var isTypeDeclaration = IsTypeDeclaration(signatureText);
            var isPublicSurfaceType = isTypeDeclaration && IsInsidePublicSurfaceContainer(publicTypeBodyDepths, braceDepth);
            if (isPublicSurfaceType || (!isTypeDeclaration && publicTypeBodyDepths.Count > 0))
            {
                yield return new PublicSurfaceDeclaration(
                    NormalizeRepositoryRelativePath(sourceFile),
                    signatureStartLine,
                    currentNamespace,
                    signatureText);
            }

            braceDepth += lineBraceDelta;
            if (isPublicSurfaceType && signatureText.Contains('{', StringComparison.Ordinal))
            {
                publicTypeBodyDepths.Push(braceDepth);
            }

            readsSignature = false;
        }
    }

    private static string StripCSharpCommentsAndStringLiterals (string sourceText)
    {
        var builder = new StringBuilder(sourceText.Length);
        var state = CSharpTriviaState.Normal;
        var rawStringQuoteCount = 0;

        for (var index = 0; index < sourceText.Length; index++)
        {
            var current = sourceText[index];
            var next = index + 1 < sourceText.Length ? sourceText[index + 1] : '\0';

            switch (state)
            {
                case CSharpTriviaState.Normal:
                    if (current == '/' && next == '/')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        index++;
                        state = CSharpTriviaState.LineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        index++;
                        state = CSharpTriviaState.BlockComment;
                    }
                    else if (current == '@' && next == '"')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        index++;
                        state = CSharpTriviaState.VerbatimStringLiteral;
                    }
                    else if (current == '"')
                    {
                        rawStringQuoteCount = CountQuoteRun(sourceText, index);
                        if (rawStringQuoteCount >= 3)
                        {
                            AppendSpaces(builder, rawStringQuoteCount);
                            index += rawStringQuoteCount - 1;
                            state = CSharpTriviaState.RawStringLiteral;
                        }
                        else
                        {
                            builder.Append(' ');
                            state = CSharpTriviaState.StringLiteral;
                        }
                    }
                    else if (current == '\'')
                    {
                        builder.Append(' ');
                        state = CSharpTriviaState.CharacterLiteral;
                    }
                    else
                    {
                        builder.Append(current);
                    }

                    break;

                case CSharpTriviaState.LineComment:
                    if (current == '\n')
                    {
                        builder.Append(current);
                        state = CSharpTriviaState.Normal;
                    }
                    else
                    {
                        builder.Append(' ');
                    }

                    break;

                case CSharpTriviaState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        index++;
                        state = CSharpTriviaState.Normal;
                    }
                    else
                    {
                        AppendTriviaReplacement(builder, current);
                    }

                    break;

                case CSharpTriviaState.StringLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        builder.Append(' ');
                        builder.Append(next == '\n' ? '\n' : ' ');
                        index++;
                    }
                    else if (current == '"')
                    {
                        builder.Append(' ');
                        state = CSharpTriviaState.Normal;
                    }
                    else
                    {
                        AppendTriviaReplacement(builder, current);
                    }

                    break;

                case CSharpTriviaState.VerbatimStringLiteral:
                    if (current == '"' && next == '"')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        index++;
                    }
                    else if (current == '"')
                    {
                        builder.Append(' ');
                        state = CSharpTriviaState.Normal;
                    }
                    else
                    {
                        AppendTriviaReplacement(builder, current);
                    }

                    break;

                case CSharpTriviaState.CharacterLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        builder.Append(' ');
                        builder.Append(next == '\n' ? '\n' : ' ');
                        index++;
                    }
                    else if (current == '\'')
                    {
                        builder.Append(' ');
                        state = CSharpTriviaState.Normal;
                    }
                    else
                    {
                        AppendTriviaReplacement(builder, current);
                    }

                    break;

                case CSharpTriviaState.RawStringLiteral:
                    if (current == '"')
                    {
                        var quoteCount = CountQuoteRun(sourceText, index);
                        if (quoteCount >= rawStringQuoteCount)
                        {
                            AppendSpaces(builder, quoteCount);
                            index += quoteCount - 1;
                            state = CSharpTriviaState.Normal;
                        }
                        else
                        {
                            AppendTriviaReplacement(builder, current);
                        }
                    }
                    else
                    {
                        AppendTriviaReplacement(builder, current);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    private static void AppendTriviaReplacement (StringBuilder builder, char value)
    {
        builder.Append(value == '\n' ? '\n' : ' ');
    }

    private static void AppendSpaces (StringBuilder builder, int count)
    {
        for (var index = 0; index < count; index++)
        {
            builder.Append(' ');
        }
    }

    private static int CountQuoteRun (string value, int startIndex)
    {
        var count = 0;
        for (var index = startIndex; index < value.Length && value[index] == '"'; index++)
        {
            count++;
        }

        return count;
    }

    private static string? ReadNamespaceDeclaration (string trimmedLine)
    {
        const string namespaceKeyword = "namespace ";
        if (!trimmedLine.StartsWith(namespaceKeyword, StringComparison.Ordinal))
        {
            return null;
        }

        return trimmedLine[namespaceKeyword.Length..].Trim().TrimEnd(';', '{').Trim();
    }

    private static bool IsPublicSurfaceDeclarationStart (string trimmedLine)
    {
        if (trimmedLine.Length == 0 || trimmedLine[0] == '[')
        {
            return false;
        }

        return trimmedLine.StartsWith("public ", StringComparison.Ordinal)
            || trimmedLine.StartsWith("protected ", StringComparison.Ordinal);
    }

    private static void AppendSignatureLine (StringBuilder builder, string trimmedLine)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(trimmedLine);
    }

    private static bool IsPublicSurfaceSignatureComplete (string trimmedLine, int parenthesisDepth)
    {
        if (parenthesisDepth > 0)
        {
            return false;
        }

        return trimmedLine.Contains('{', StringComparison.Ordinal)
            || trimmedLine.Contains(';', StringComparison.Ordinal)
            || trimmedLine.Contains("=>", StringComparison.Ordinal)
            || (!IsTypeDeclaration(trimmedLine) && trimmedLine.EndsWith(')'));
    }

    private static bool IsTypeDeclaration (string value)
    {
        return value.Contains(" class ", StringComparison.Ordinal)
            || value.Contains(" struct ", StringComparison.Ordinal)
            || value.Contains(" interface ", StringComparison.Ordinal)
            || value.Contains(" enum ", StringComparison.Ordinal)
            || value.Contains(" record ", StringComparison.Ordinal);
    }

    private static bool IsInsidePublicSurfaceContainer (Stack<int> publicTypeBodyDepths, int braceDepth)
    {
        return publicTypeBodyDepths.Count > 0 || braceDepth == 0;
    }

    private static void PopClosedPublicTypeFrames (Stack<int> publicTypeBodyDepths, int braceDepth)
    {
        while (publicTypeBodyDepths.Count > 0 && braceDepth < publicTypeBodyDepths.Peek())
        {
            publicTypeBodyDepths.Pop();
        }
    }

    private static int CountCharacter (string value, char target)
    {
        var count = 0;
        foreach (var character in value)
        {
            if (character == target)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsUnityGeneratedProjectFile (string relativePath)
    {
        return relativePath.StartsWith("src/Ucli.Unity/", StringComparison.Ordinal)
            && relativePath.EndsWith(".csproj", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot ()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory != null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Ucli.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private enum CSharpTriviaState
    {
        Normal,
        LineComment,
        BlockComment,
        StringLiteral,
        VerbatimStringLiteral,
        CharacterLiteral,
        RawStringLiteral,
    }

    internal readonly record struct PublicSurfaceDeclaration (
        string RelativePath,
        int LineNumber,
        string Namespace,
        string Signature);
}
