using Antlr4.Runtime;
using LanceServer.RequestHandler.Diagnostic;
using LspTypes;
using System.Text.RegularExpressions;

namespace LanceServer.Parser;

/// <summary>
/// Validates structured IF blocks independently from conditional jump statements.
/// In SINUMERIK, "IF condition GOTOF/GOTOB target" is a complete statement and
/// therefore does not open a block that requires ENDIF.
/// </summary>
internal static class IfStructureValidator
{
    public static IfStructureValidationResult Validate(CommonTokenStream tokenStream)
    {
        tokenStream.Fill();

        var openIfTokens = new List<OpenIf>();
        var unexpectedEndIfTokenIndexes = new HashSet<int>();
        var parserSupersededTokenIndexes = new HashSet<int>();
        var diagnostics = new List<Diagnostic>();

        for (var index = 0; index < tokenStream.Size; index++)
        {
            var token = tokenStream.Get(index);
            if (token.Type == SinumerikNCParser.IF)
            {
                if (!IsConditionalJump(tokenStream, index))
                {
                    openIfTokens.Add(new OpenIf(token, false));
                }

                continue;
            }

            if (token.Type == SinumerikNCParser.EXECSTRING)
            {
                var dynamicControl = GetDynamicControl(tokenStream, index);
                if (dynamicControl == DynamicControl.StructuredIf)
                {
                    openIfTokens.Add(new OpenIf(token, true));
                }
                else if (dynamicControl == DynamicControl.EndIf)
                {
                    CloseIf(
                        token,
                        openIfTokens,
                        diagnostics,
                        unexpectedEndIfTokenIndexes,
                        parserSupersededTokenIndexes,
                        parserSeesEndIf: false);
                }

                continue;
            }

            if (token.Type == SinumerikNCParser.ELSE
                && openIfTokens.Count > 0
                && openIfTokens[^1].IsDynamic)
            {
                parserSupersededTokenIndexes.Add(token.TokenIndex);
                continue;
            }

            if (token.Type != SinumerikNCParser.IF_END)
            {
                continue;
            }

            CloseIf(
                token,
                openIfTokens,
                diagnostics,
                unexpectedEndIfTokenIndexes,
                parserSupersededTokenIndexes,
                parserSeesEndIf: true);
        }

        foreach (var openIf in openIfTokens.Where(openIf => !openIf.IsDynamic))
        {
            diagnostics.Add(DiagnosticMessage.MissingEndIf(ParserHelper.GetRangeForToken(openIf.Token)));
        }

        var hasUnclosedStaticIf = openIfTokens.Any(openIf => !openIf.IsDynamic);
        tokenStream.Seek(0);
        return new IfStructureValidationResult(
            diagnostics,
            unexpectedEndIfTokenIndexes,
            parserSupersededTokenIndexes,
            hasUnclosedStaticIf);
    }

    private static void CloseIf(
        IToken token,
        IList<OpenIf> openIfTokens,
        IList<Diagnostic> diagnostics,
        ISet<int> unexpectedEndIfTokenIndexes,
        ISet<int> parserSupersededTokenIndexes,
        bool parserSeesEndIf)
    {
        if (openIfTokens.Count > 0)
        {
            var openIf = openIfTokens[^1];
            openIfTokens.RemoveAt(openIfTokens.Count - 1);
            if (parserSeesEndIf && openIf.IsDynamic)
            {
                parserSupersededTokenIndexes.Add(token.TokenIndex);
            }

            return;
        }

        unexpectedEndIfTokenIndexes.Add(token.TokenIndex);
        diagnostics.Add(DiagnosticMessage.UnexpectedEndIf(ParserHelper.GetRangeForToken(token)));
    }

    private static bool IsConditionalJump(CommonTokenStream tokenStream, int ifTokenIndex)
    {
        for (var index = ifTokenIndex + 1; index < tokenStream.Size; index++)
        {
            var tokenType = tokenStream.Get(index).Type;
            if (tokenType is SinumerikNCParser.NEWLINE or TokenConstants.EOF)
            {
                return false;
            }

            if (tokenType is SinumerikNCParser.GOTO
                or SinumerikNCParser.GOTO_B
                or SinumerikNCParser.GOTO_C
                or SinumerikNCParser.GOTO_F
                or SinumerikNCParser.GOTO_S)
            {
                return true;
            }
        }

        return false;
    }

    private static DynamicControl GetDynamicControl(CommonTokenStream tokenStream, int execStringTokenIndex)
    {
        var literalText = string.Empty;
        var firstContentIsString = false;
        var hasSeenContent = false;
        for (var index = execStringTokenIndex + 1; index < tokenStream.Size; index++)
        {
            var token = tokenStream.Get(index);
            if (token.Type is SinumerikNCParser.NEWLINE or TokenConstants.EOF)
            {
                break;
            }

            if (token.Type == SinumerikNCParser.STRING && token.Text.Length >= 2)
            {
                if (!hasSeenContent)
                {
                    firstContentIsString = true;
                }

                hasSeenContent = true;
                literalText += token.Text[1..^1];
                continue;
            }

            if (token.Type is not SinumerikNCParser.OPEN_PAREN
                and not SinumerikNCParser.CLOSE_PAREN
                and not SinumerikNCParser.CONCAT)
            {
                hasSeenContent = true;
            }
        }

        if (!firstContentIsString)
        {
            return DynamicControl.None;
        }

        if (Regex.IsMatch(literalText, @"^\s*ENDIF\b", RegexOptions.IgnoreCase))
        {
            return DynamicControl.EndIf;
        }

        if (!Regex.IsMatch(literalText, @"^\s*IF\b", RegexOptions.IgnoreCase))
        {
            return DynamicControl.None;
        }

        return Regex.IsMatch(
            literalText,
            @"\bGOTO(?:B|C|F|S)?\b",
            RegexOptions.IgnoreCase)
            ? DynamicControl.ConditionalIf
            : DynamicControl.StructuredIf;
    }

    private readonly record struct OpenIf(IToken Token, bool IsDynamic);

    private enum DynamicControl
    {
        None,
        StructuredIf,
        ConditionalIf,
        EndIf
    }
}

internal sealed class IfStructureValidationResult
{
    private readonly ISet<int> _unexpectedEndIfTokenIndexes;
    private readonly ISet<int> _parserSupersededTokenIndexes;

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
    public bool HasUnclosedIf { get; }

    public IfStructureValidationResult(
        IReadOnlyList<Diagnostic> diagnostics,
        ISet<int> unexpectedEndIfTokenIndexes,
        ISet<int> parserSupersededTokenIndexes,
        bool hasUnclosedIf)
    {
        Diagnostics = diagnostics;
        _unexpectedEndIfTokenIndexes = unexpectedEndIfTokenIndexes;
        _parserSupersededTokenIndexes = parserSupersededTokenIndexes;
        HasUnclosedIf = hasUnclosedIf;
    }

    public bool SupersedesParserError(IToken offendingToken, string message)
    {
        if (_parserSupersededTokenIndexes.Contains(offendingToken.TokenIndex)
            || (offendingToken.Type == SinumerikNCParser.IF_END
                && _unexpectedEndIfTokenIndexes.Contains(offendingToken.TokenIndex)))
        {
            return true;
        }

        return HasUnclosedIf
            && offendingToken.Type == TokenConstants.EOF
            && (message.Contains("endif", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("no viable alternative at input", StringComparison.Ordinal));
    }
}
