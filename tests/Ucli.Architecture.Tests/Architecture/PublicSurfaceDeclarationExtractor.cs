using System.Text;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class PublicSurfaceDeclarationExtractor
{
    internal static IEnumerable<PublicSurfaceDeclaration> Read (string sourceFile)
    {
        var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
        var currentNamespace = string.Empty;
        var signature = new StringBuilder();
        var publicTypeFrames = new Stack<PublicSurfaceTypeFrame>();
        var namespaceBodyDepths = new Stack<int>();
        var signatureStartLine = 0;
        var readsSignature = false;
        var awaitsNamespaceBlock = false;
        var parenthesisDepth = 0;
        var braceDepth = 0;
        var lines = sourceText.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            PopClosedPublicTypeFrames(publicTypeFrames, braceDepth);
            PopClosedNamespaceFrames(namespaceBodyDepths, braceDepth);

            var line = lines[lineIndex].TrimEnd('\r');
            var trimmedLine = line.Trim();
            var lineBraceDelta = CountCharacter(trimmedLine, '{') - CountCharacter(trimmedLine, '}');
            var startsNamespaceBlock = IsBlockScopedNamespaceDeclaration(trimmedLine)
                || (awaitsNamespaceBlock && trimmedLine == "{");
            if (startsNamespaceBlock)
            {
                awaitsNamespaceBlock = false;
            }

            if (!readsSignature)
            {
                var namespaceDeclaration = ReadNamespaceDeclaration(trimmedLine);
                if (namespaceDeclaration is not null)
                {
                    currentNamespace = namespaceDeclaration;
                    awaitsNamespaceBlock = IsNamespaceDeclarationAwaitingBlock(trimmedLine);
                }

                if (!IsPublicSurfaceDeclarationStart(trimmedLine, IsInsidePublicInterface(publicTypeFrames)))
                {
                    braceDepth += lineBraceDelta;
                    if (startsNamespaceBlock)
                    {
                        namespaceBodyDepths.Push(braceDepth);
                    }

                    continue;
                }

                readsSignature = true;
                signature.Clear();
                signatureStartLine = lineIndex + 1;
                parenthesisDepth = 0;
            }

            AppendSignatureLine(signature, trimmedLine);
            parenthesisDepth += CountCharacter(trimmedLine, '(') - CountCharacter(trimmedLine, ')');

            if (!IsPublicSurfaceSignatureComplete(trimmedLine, signature.ToString(), parenthesisDepth))
            {
                braceDepth += lineBraceDelta;
                continue;
            }

            var signatureText = signature.ToString();
            var isTypeDeclaration = IsTypeDeclaration(signatureText);
            var isPublicSurfaceType = isTypeDeclaration && IsInsidePublicSurfaceContainer(publicTypeFrames, namespaceBodyDepths, braceDepth);
            if (isPublicSurfaceType || (!isTypeDeclaration && publicTypeFrames.Count > 0))
            {
                yield return new PublicSurfaceDeclaration(
                    ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile),
                    signatureStartLine,
                    currentNamespace,
                    signatureText);
            }

            braceDepth += lineBraceDelta;
            if (isPublicSurfaceType && signatureText.Contains('{', StringComparison.Ordinal))
            {
                publicTypeFrames.Push(new PublicSurfaceTypeFrame(braceDepth, IsInterfaceDeclaration(signatureText)));
            }

            readsSignature = false;
        }
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

    private static bool IsBlockScopedNamespaceDeclaration (string trimmedLine)
    {
        return trimmedLine.StartsWith("namespace ", StringComparison.Ordinal)
            && trimmedLine.EndsWith('{');
    }

    private static bool IsNamespaceDeclarationAwaitingBlock (string trimmedLine)
    {
        return trimmedLine.StartsWith("namespace ", StringComparison.Ordinal)
            && !trimmedLine.EndsWith(';')
            && !trimmedLine.EndsWith('{');
    }

    private static bool IsPublicSurfaceDeclarationStart (string trimmedLine, bool insidePublicInterface)
    {
        if (trimmedLine.Length == 0 || trimmedLine[0] == '[')
        {
            return false;
        }

        return trimmedLine.StartsWith("public ", StringComparison.Ordinal)
            || trimmedLine.StartsWith("protected ", StringComparison.Ordinal)
            || (insidePublicInterface && IsImplicitPublicInterfaceMemberStart(trimmedLine));
    }

    private static bool IsImplicitPublicInterfaceMemberStart (string trimmedLine)
    {
        return trimmedLine[0] != '{'
            && trimmedLine[0] != '}'
            && !trimmedLine.StartsWith("private ", StringComparison.Ordinal)
            && !trimmedLine.StartsWith("internal ", StringComparison.Ordinal)
            && !trimmedLine.StartsWith("namespace ", StringComparison.Ordinal)
            && !trimmedLine.StartsWith("using ", StringComparison.Ordinal);
    }

    private static void AppendSignatureLine (StringBuilder builder, string trimmedLine)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(trimmedLine);
    }

    private static bool IsPublicSurfaceSignatureComplete (string trimmedLine, string signatureText, int parenthesisDepth)
    {
        if (parenthesisDepth > 0)
        {
            return false;
        }

        if (IsTypeDeclaration(signatureText))
        {
            return trimmedLine.Contains('{', StringComparison.Ordinal)
                || trimmedLine.Contains(';', StringComparison.Ordinal);
        }

        return trimmedLine.Contains('{', StringComparison.Ordinal)
            || trimmedLine.Contains(';', StringComparison.Ordinal)
            || trimmedLine.Contains("=>", StringComparison.Ordinal)
            || trimmedLine.EndsWith(')');
    }

    private static bool IsTypeDeclaration (string value)
    {
        return value.Contains(" class ", StringComparison.Ordinal)
            || value.Contains(" struct ", StringComparison.Ordinal)
            || value.Contains(" interface ", StringComparison.Ordinal)
            || value.Contains(" enum ", StringComparison.Ordinal)
            || value.Contains(" record ", StringComparison.Ordinal);
    }

    private static bool IsInterfaceDeclaration (string value)
    {
        return value.Contains(" interface ", StringComparison.Ordinal);
    }

    private static bool IsInsidePublicSurfaceContainer (
        Stack<PublicSurfaceTypeFrame> publicTypeFrames,
        Stack<int> namespaceBodyDepths,
        int braceDepth)
    {
        return publicTypeFrames.Count > 0
            || braceDepth == 0
            || namespaceBodyDepths.Contains(braceDepth);
    }

    private static bool IsInsidePublicInterface (Stack<PublicSurfaceTypeFrame> publicTypeFrames)
    {
        return publicTypeFrames.Any(static frame => frame.IsInterface);
    }

    private static void PopClosedPublicTypeFrames (Stack<PublicSurfaceTypeFrame> publicTypeFrames, int braceDepth)
    {
        while (publicTypeFrames.Count > 0 && braceDepth < publicTypeFrames.Peek().BodyDepth)
        {
            publicTypeFrames.Pop();
        }
    }

    private static void PopClosedNamespaceFrames (Stack<int> namespaceBodyDepths, int braceDepth)
    {
        while (namespaceBodyDepths.Count > 0 && braceDepth < namespaceBodyDepths.Peek())
        {
            namespaceBodyDepths.Pop();
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

    private readonly record struct PublicSurfaceTypeFrame (
        int BodyDepth,
        bool IsInterface);
}

internal readonly record struct PublicSurfaceDeclaration (
    string RelativePath,
    int LineNumber,
    string Namespace,
    string Signature);
