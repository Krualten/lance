using Antlr4.Runtime;
using LanceServer.RequestHandler.Diagnostic;
using LspTypes;
using Range = LspTypes.Range;

namespace LanceServer.Parser;

/// <summary>
/// Error listener for the sinumerik nc parser.
/// </summary>
public class ErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    /// <summary>
    /// The diagnostics generated while parsing.
    /// </summary>
    public IList<Diagnostic> Diagnostics = new List<Diagnostic>();

    private IfStructureValidationResult? _ifStructureValidation;

    internal void SetIfStructureValidation(IfStructureValidationResult validation)
    {
        _ifStructureValidation = validation;
        foreach (var diagnostic in validation.Diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }
    }
    
    /// <inheritdoc />
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        if (_ifStructureValidation?.SupersedesParserError(offendingSymbol, msg) == true)
        {
            return;
        }

        Diagnostics.Add(DiagnosticMessage.ParsingError(ParserHelper.GetRangeForToken(offendingSymbol), msg));
    }

    /// <inheritdoc />
    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        var token = msg.Split('\'')[1];
        var characterEnd = charPositionInLine + token.Length;
        var range = new Range { Start = new Position((uint)line - 1, (uint)charPositionInLine), End = new Position((uint)line - 1, (uint)characterEnd) };
        Diagnostics.Add(DiagnosticMessage.LexingError(range, token));
    }
}
