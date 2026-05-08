using System;
using System.Linq;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Prepares full compilation units or snippets for C# eval compilation. </summary>
    internal sealed class CsEvalSourcePreparer
    {
        public const string SnippetWrapperVersion = "snippet-wrapper-v1";

        public const string NoWrapperVersion = "none";

        private const string SourcePath = "ucli.cs.eval.cs";

        public CsEvalPreparedSource CreateCompilationUnit (string source)
        {
            return new CsEvalPreparedSource(
                CsEvalSourceKindValues.CompilationUnit,
                source,
                NoWrapperVersion);
        }

        public bool TryCreateSnippet (
            string source,
            out CsEvalPreparedSource preparedSource,
            out CsEvalDiagnostic diagnostic)
        {
            preparedSource = null!;
            diagnostic = null!;

            var syntax = CSharpSyntaxTree.ParseText(source);
            var root = syntax.GetCompilationUnitRoot();
            if (root.Externs.Count != 0 || root.AttributeLists.Count != 0)
            {
                diagnostic = CsEvalDiagnosticMapper.Create(
                    CsEvalDiagnosticIds.SnippetUnsupported,
                    "C# eval snippet supports leading using directives and Run method body statements only.");
                return false;
            }

            if (root.Members.Any(static member => member is not GlobalStatementSyntax))
            {
                diagnostic = CsEvalDiagnosticMapper.Create(
                    CsEvalDiagnosticIds.SnippetUnsupported,
                    "C# eval snippet must not declare namespace, type, or member definitions. Use a full compilation unit for declarations.");
                return false;
            }

            if (root.Members
                .OfType<GlobalStatementSyntax>()
                .Any(static statement => statement.Statement is LocalFunctionStatementSyntax))
            {
                diagnostic = CsEvalDiagnosticMapper.Create(
                    CsEvalDiagnosticIds.SnippetUnsupported,
                    "C# eval snippet must not declare local functions. Use a full compilation unit for helper methods.");
                return false;
            }

            var bodyStart = root.Usings.Count == 0 ? 0 : root.Usings.Last().FullSpan.End;
            var bodyLine = CountLines(source, bodyStart) + 1;
            var userUsings = root.Usings.Count == 0
                ? string.Empty
                : string.Concat(root.Usings.Select(static directive => directive.ToFullString()));
            var bodyText = bodyStart >= source.Length ? string.Empty : source.Substring(bodyStart);
            var isExpression = IsSingleExpression(bodyText);
            var shouldAppendNullReturn = !isExpression && !LastGlobalStatementIsReturn(root);
            var wrappedSource = CreateWrappedSource(userUsings, bodyText, bodyLine, isExpression, shouldAppendNullReturn);
            preparedSource = new CsEvalPreparedSource(
                CsEvalSourceKindValues.Snippet,
                wrappedSource,
                SnippetWrapperVersion);
            return true;
        }

        private static bool IsSingleExpression (string bodyText)
        {
            var trimmed = bodyText.Trim();
            if (trimmed.Length == 0
                || trimmed.EndsWith(";", StringComparison.Ordinal)
                || trimmed.StartsWith("return ", StringComparison.Ordinal)
                || trimmed.StartsWith("return\t", StringComparison.Ordinal)
                || string.Equals(trimmed, "return", StringComparison.Ordinal))
            {
                return false;
            }

            var expression = SyntaxFactory.ParseExpression(trimmed);
            return !expression.ContainsDiagnostics;
        }

        private static bool LastGlobalStatementIsReturn (CompilationUnitSyntax root)
        {
            var lastGlobalStatement = root.Members
                .OfType<GlobalStatementSyntax>()
                .LastOrDefault();
            return lastGlobalStatement?.Statement is ReturnStatementSyntax;
        }

        private static string CreateWrappedSource (
            string userUsings,
            string bodyText,
            int bodyLine,
            bool isExpression,
            bool shouldAppendNullReturn)
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using MackySoft.Ucli.Unity.Execution.CsEval;");
            builder.AppendLine("using UnityEditor;");
            builder.AppendLine("using UnityEngine;");
            if (!string.IsNullOrWhiteSpace(userUsings))
            {
                builder.AppendLine($"#line 1 \"{SourcePath}\"");
                builder.Append(userUsings);
                if (!userUsings.EndsWith("\n", StringComparison.Ordinal))
                {
                    builder.AppendLine();
                }

                builder.AppendLine("#line default");
            }

            builder.AppendLine("namespace MackySoft.Ucli.Unity.Execution.CsEval.Generated");
            builder.AppendLine("{");
            builder.AppendLine("    public static class UcliCsEvalSnippetEntry");
            builder.AppendLine("    {");
            builder.AppendLine("        public static object? Run(UcliCsEvalContext context)");
            builder.AppendLine("        {");
            builder.AppendLine($"#line {bodyLine} \"{SourcePath}\"");
            if (isExpression)
            {
                builder.Append("            return ");
                builder.Append(bodyText.Trim());
                builder.AppendLine(";");
            }
            else if (!string.IsNullOrWhiteSpace(bodyText))
            {
                builder.Append(bodyText);
                if (!bodyText.EndsWith("\n", StringComparison.Ordinal))
                {
                    builder.AppendLine();
                }
            }

            builder.AppendLine("#line default");
            if (shouldAppendNullReturn)
            {
                builder.AppendLine("            return null;");
            }

            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static int CountLines (
            string text,
            int endIndex)
        {
            var count = 0;
            var limit = Math.Min(text.Length, endIndex);
            for (var i = 0; i < limit; i++)
            {
                if (text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }
    }
}
