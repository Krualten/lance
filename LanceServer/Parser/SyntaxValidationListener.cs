using Antlr4.Runtime;
using LanceServer.RequestHandler.Diagnostic;
using LspTypes;

namespace LanceServer.Parser;

/// <summary>
/// Validates context-sensitive syntax which is intentionally represented in the grammar
/// to support precise recovery and diagnostics.
/// </summary>
public class SyntaxValidationListener : SinumerikNCBaseListener
{
    private readonly IList<Diagnostic> _diagnostics;

    public SyntaxValidationListener(IList<Diagnostic> diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public override void ExitTrailingConcatExpression(
        SinumerikNCParser.TrailingConcatExpressionContext context)
    {
        if (IsMessageText(context))
        {
            return;
        }

        _diagnostics.Add(DiagnosticMessage.ParsingError(
            ParserHelper.GetRangeForToken(context.CONCAT().Symbol),
            "A trailing '<<' operator is only valid in an MSG message text."));
    }

    public override void ExitIncompleteUserVariableAssignment(
        SinumerikNCParser.IncompleteUserVariableAssignmentContext context)
    {
        if (context.ASSIGNMENT()?.Symbol.TokenIndex < 0)
        {
            return;
        }

        _diagnostics.Add(DiagnosticMessage.IncompleteAssignment(
            ParserHelper.GetRangeFromStartToEndToken(context.Start, context.Stop)));
    }

    private static bool IsMessageText(ParserRuleContext context)
    {
        for (var parent = context.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is SinumerikNCParser.PredefinedProcedureContext procedure)
            {
                var messageText = procedure.expression().FirstOrDefault();
                return procedure.MSG() != null
                    && messageText != null
                    && context.Start.TokenIndex >= messageText.Start.TokenIndex
                    && context.Stop.TokenIndex <= messageText.Stop.TokenIndex;
            }

            // Parenthesized expressions are represented by a primary expression between
            // expression nodes. Any other construct means this is nested inside another
            // procedure or function rather than being the MSG text itself.
            if (parent is not SinumerikNCParser.ExpressionContext
                && parent is not SinumerikNCParser.NestedExpressionContext)
            {
                return false;
            }
        }

        return false;
    }
}
