using System.Text;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class CSharpSourceScanner
{
    internal static string StripCommentsAndStringLiterals (string sourceText)
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
                    if (TryStripInterpolatedStringLiteral(sourceText, index, builder, out var interpolatedStringEndIndex))
                    {
                        index = interpolatedStringEndIndex;
                    }
                    else if (current == '/' && next == '/')
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

    private static bool TryStripInterpolatedStringLiteral (
        string sourceText,
        int startIndex,
        StringBuilder builder,
        out int endIndex)
    {
        endIndex = startIndex;
        if (TryStripRawInterpolatedStringLiteral(sourceText, startIndex, builder, out endIndex))
        {
            return true;
        }

        if (!TryReadInterpolatedStringPrefixLength(sourceText, startIndex, out var prefixLength, out var isVerbatim))
        {
            return false;
        }

        AppendSpaces(builder, prefixLength);
        var index = startIndex + prefixLength;
        while (index < sourceText.Length)
        {
            var current = sourceText[index];
            var next = index + 1 < sourceText.Length ? sourceText[index + 1] : '\0';

            if (!isVerbatim && current == '\\' && next != '\0')
            {
                builder.Append(' ');
                builder.Append(next == '\n' ? '\n' : ' ');
                index += 2;
                continue;
            }

            if (isVerbatim && current == '"' && next == '"')
            {
                builder.Append(' ');
                builder.Append(' ');
                index += 2;
                continue;
            }

            if (current == '"')
            {
                builder.Append(' ');
                endIndex = index;
                return true;
            }

            if ((current == '{' && next == '{') || (current == '}' && next == '}'))
            {
                builder.Append(' ');
                builder.Append(' ');
                index += 2;
                continue;
            }

            if (current == '{')
            {
                builder.Append(' ');
                var expressionStartIndex = index + 1;
                var expressionEndIndex = FindInterpolatedExpressionEnd(sourceText, expressionStartIndex);
                if (expressionEndIndex < 0)
                {
                    throw new InvalidOperationException("Interpolated string expression is not closed.");
                }

                var expressionText = sourceText[expressionStartIndex..expressionEndIndex];
                AppendInterpolatedExpressionCode(builder, expressionText);
                builder.Append(' ');
                index = expressionEndIndex + 1;
                continue;
            }

            AppendTriviaReplacement(builder, current);
            index++;
        }

        throw new InvalidOperationException("Interpolated string literal is not closed.");
    }

    private static bool TryStripRawInterpolatedStringLiteral (
        string sourceText,
        int startIndex,
        StringBuilder builder,
        out int endIndex)
    {
        endIndex = startIndex;
        if (sourceText[startIndex] != '$')
        {
            return false;
        }

        var dollarCount = CountCharacterRun(sourceText, startIndex, '$');
        var quoteStartIndex = startIndex + dollarCount;
        var quoteCount = CountQuoteRun(sourceText, quoteStartIndex);
        if (quoteCount < 3)
        {
            return false;
        }

        AppendSpaces(builder, dollarCount + quoteCount);
        var index = quoteStartIndex + quoteCount;
        while (index < sourceText.Length)
        {
            var current = sourceText[index];
            if (current == '"')
            {
                var closingQuoteCount = CountQuoteRun(sourceText, index);
                if (closingQuoteCount >= quoteCount)
                {
                    AppendSpaces(builder, closingQuoteCount);
                    endIndex = index + closingQuoteCount - 1;
                    return true;
                }
            }

            if (current == '{')
            {
                var openingBraceCount = CountCharacterRun(sourceText, index, '{');
                if (openingBraceCount >= dollarCount && openingBraceCount < dollarCount * 2)
                {
                    AppendSpaces(builder, openingBraceCount);
                    var expressionStartIndex = index + openingBraceCount;
                    var expressionEndIndex = FindRawInterpolatedExpressionEnd(sourceText, expressionStartIndex, dollarCount);
                    if (expressionEndIndex < 0)
                    {
                        throw new InvalidOperationException("Raw interpolated string expression is not closed.");
                    }

                    var expressionText = sourceText[expressionStartIndex..expressionEndIndex];
                    AppendInterpolatedExpressionCode(builder, expressionText);
                    AppendSpaces(builder, dollarCount);
                    index = expressionEndIndex + dollarCount;
                    continue;
                }
            }

            AppendTriviaReplacement(builder, current);
            index++;
        }

        throw new InvalidOperationException("Raw interpolated string literal is not closed.");
    }

    private static bool TryReadInterpolatedStringPrefixLength (
        string sourceText,
        int startIndex,
        out int prefixLength,
        out bool isVerbatim)
    {
        prefixLength = 0;
        isVerbatim = false;

        if (startIndex + 1 < sourceText.Length
            && sourceText[startIndex] == '$'
            && sourceText[startIndex + 1] == '"')
        {
            prefixLength = 2;
            return true;
        }

        if (startIndex + 2 < sourceText.Length
            && sourceText[startIndex] == '$'
            && sourceText[startIndex + 1] == '@'
            && sourceText[startIndex + 2] == '"')
        {
            prefixLength = 3;
            isVerbatim = true;
            return true;
        }

        if (startIndex + 2 < sourceText.Length
            && sourceText[startIndex] == '@'
            && sourceText[startIndex + 1] == '$'
            && sourceText[startIndex + 2] == '"')
        {
            prefixLength = 3;
            isVerbatim = true;
            return true;
        }

        return false;
    }

    private static void AppendInterpolatedExpressionCode (StringBuilder builder, string expressionText)
    {
        var formatStartIndex = FindTopLevelFormatStart(expressionText);
        if (formatStartIndex < 0)
        {
            builder.Append(StripCommentsAndStringLiterals(expressionText));
            return;
        }

        builder.Append(StripCommentsAndStringLiterals(expressionText[..formatStartIndex]));
        for (var index = formatStartIndex; index < expressionText.Length; index++)
        {
            AppendTriviaReplacement(builder, expressionText[index]);
        }
    }

    private static int FindTopLevelFormatStart (string value)
    {
        var state = CSharpTriviaState.Normal;
        var rawStringQuoteCount = 0;
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var conditionalDepth = 0;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            var next = index + 1 < value.Length ? value[index + 1] : '\0';

            switch (state)
            {
                case CSharpTriviaState.Normal:
                    if (current == '/' && next == '/')
                    {
                        index++;
                        state = CSharpTriviaState.LineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        index++;
                        state = CSharpTriviaState.BlockComment;
                    }
                    else if (current == '@' && next == '"')
                    {
                        index++;
                        state = CSharpTriviaState.VerbatimStringLiteral;
                    }
                    else if (current == '"')
                    {
                        rawStringQuoteCount = CountQuoteRun(value, index);
                        if (rawStringQuoteCount >= 3)
                        {
                            index += rawStringQuoteCount - 1;
                            state = CSharpTriviaState.RawStringLiteral;
                        }
                        else
                        {
                            state = CSharpTriviaState.StringLiteral;
                        }
                    }
                    else if (current == '\'')
                    {
                        state = CSharpTriviaState.CharacterLiteral;
                    }
                    else if (current == '(')
                    {
                        parenthesisDepth++;
                    }
                    else if (current == ')')
                    {
                        parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                    }
                    else if (current == '[')
                    {
                        bracketDepth++;
                    }
                    else if (current == ']')
                    {
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                    }
                    else if (current == '{')
                    {
                        braceDepth++;
                    }
                    else if (current == '}')
                    {
                        braceDepth = Math.Max(0, braceDepth - 1);
                    }
                    else if (parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        if (current == '?' && next != '?' && next != '.' && next != '[')
                        {
                            conditionalDepth++;
                        }
                        else if (current == ':')
                        {
                            if (conditionalDepth > 0)
                            {
                                conditionalDepth--;
                            }
                            else
                            {
                                return index;
                            }
                        }
                    }

                    break;

                case CSharpTriviaState.LineComment:
                    if (current == '\n')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        index++;
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.StringLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.VerbatimStringLiteral:
                    if (current == '"' && next == '"')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.CharacterLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '\'')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.RawStringLiteral:
                    if (current == '"')
                    {
                        var quoteCount = CountQuoteRun(value, index);
                        if (quoteCount >= rawStringQuoteCount)
                        {
                            index += quoteCount - 1;
                            state = CSharpTriviaState.Normal;
                        }
                    }

                    break;
            }
        }

        return -1;
    }

    private static int FindInterpolatedExpressionEnd (string sourceText, int startIndex)
    {
        var state = CSharpTriviaState.Normal;
        var rawStringQuoteCount = 0;
        var braceDepth = 1;

        for (var index = startIndex; index < sourceText.Length; index++)
        {
            var current = sourceText[index];
            var next = index + 1 < sourceText.Length ? sourceText[index + 1] : '\0';

            switch (state)
            {
                case CSharpTriviaState.Normal:
                    if (current == '/' && next == '/')
                    {
                        index++;
                        state = CSharpTriviaState.LineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        index++;
                        state = CSharpTriviaState.BlockComment;
                    }
                    else if (current == '@' && next == '"')
                    {
                        index++;
                        state = CSharpTriviaState.VerbatimStringLiteral;
                    }
                    else if (current == '"')
                    {
                        rawStringQuoteCount = CountQuoteRun(sourceText, index);
                        if (rawStringQuoteCount >= 3)
                        {
                            index += rawStringQuoteCount - 1;
                            state = CSharpTriviaState.RawStringLiteral;
                        }
                        else
                        {
                            state = CSharpTriviaState.StringLiteral;
                        }
                    }
                    else if (current == '\'')
                    {
                        state = CSharpTriviaState.CharacterLiteral;
                    }
                    else if (current == '{')
                    {
                        braceDepth++;
                    }
                    else if (current == '}')
                    {
                        braceDepth--;
                        if (braceDepth == 0)
                        {
                            return index;
                        }
                    }

                    break;

                case CSharpTriviaState.LineComment:
                    if (current == '\n')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        index++;
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.StringLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.VerbatimStringLiteral:
                    if (current == '"' && next == '"')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.CharacterLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '\'')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.RawStringLiteral:
                    if (current == '"')
                    {
                        var quoteCount = CountQuoteRun(sourceText, index);
                        if (quoteCount >= rawStringQuoteCount)
                        {
                            index += quoteCount - 1;
                            state = CSharpTriviaState.Normal;
                        }
                    }

                    break;
            }
        }

        return -1;
    }

    private static int FindRawInterpolatedExpressionEnd (string sourceText, int startIndex, int delimiterBraceCount)
    {
        var state = CSharpTriviaState.Normal;
        var rawStringQuoteCount = 0;
        var braceDepth = 0;

        for (var index = startIndex; index < sourceText.Length; index++)
        {
            var current = sourceText[index];
            var next = index + 1 < sourceText.Length ? sourceText[index + 1] : '\0';

            switch (state)
            {
                case CSharpTriviaState.Normal:
                    if (current == '/' && next == '/')
                    {
                        index++;
                        state = CSharpTriviaState.LineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        index++;
                        state = CSharpTriviaState.BlockComment;
                    }
                    else if (current == '@' && next == '"')
                    {
                        index++;
                        state = CSharpTriviaState.VerbatimStringLiteral;
                    }
                    else if (current == '"')
                    {
                        rawStringQuoteCount = CountQuoteRun(sourceText, index);
                        if (rawStringQuoteCount >= 3)
                        {
                            index += rawStringQuoteCount - 1;
                            state = CSharpTriviaState.RawStringLiteral;
                        }
                        else
                        {
                            state = CSharpTriviaState.StringLiteral;
                        }
                    }
                    else if (current == '\'')
                    {
                        state = CSharpTriviaState.CharacterLiteral;
                    }
                    else if (current == '{')
                    {
                        braceDepth++;
                    }
                    else if (current == '}')
                    {
                        var closingBraceCount = CountCharacterRun(sourceText, index, '}');
                        if (braceDepth == 0 && closingBraceCount >= delimiterBraceCount)
                        {
                            return index;
                        }

                        if (braceDepth > 0)
                        {
                            braceDepth--;
                        }
                    }

                    break;

                case CSharpTriviaState.LineComment:
                    if (current == '\n')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        index++;
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.StringLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.VerbatimStringLiteral:
                    if (current == '"' && next == '"')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.CharacterLiteral:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '\'')
                    {
                        state = CSharpTriviaState.Normal;
                    }

                    break;

                case CSharpTriviaState.RawStringLiteral:
                    if (current == '"')
                    {
                        var quoteCount = CountQuoteRun(sourceText, index);
                        if (quoteCount >= rawStringQuoteCount)
                        {
                            index += quoteCount - 1;
                            state = CSharpTriviaState.Normal;
                        }
                    }

                    break;
            }
        }

        return -1;
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
        return CountCharacterRun(value, startIndex, '"');
    }

    private static int CountCharacterRun (string value, int startIndex, char target)
    {
        var count = 0;
        for (var index = startIndex; index < value.Length && value[index] == target; index++)
        {
            count++;
        }

        return count;
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
}
