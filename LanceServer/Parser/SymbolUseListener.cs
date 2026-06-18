using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using LanceServer.Core.Document;
using LanceServer.Core.Symbol;

namespace LanceServer.Parser;

/// <summary>
/// The listener looking for symbol uses in the code.
/// </summary>
public class SymbolUseListener : SinumerikNCBaseListener
{
    /// <summary>
    /// The list of the found symbol uses.
    /// </summary>
    public IList<AbstractSymbolUse> SymbolUseTable { get; } = new List<AbstractSymbolUse>();
    
    private readonly PlaceholderPreprocessedDocument _document;
    
    public SymbolUseListener(SymbolisedDocument document)
    {
        _document = document;
    }
    
    /// <summary>
    /// Is called at the end of a user variable assignment.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitUserVariableAssignment(SinumerikNCParser.UserVariableAssignmentContext context)
    {
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a array variable assignment.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitArrayVariableAssignment(SinumerikNCParser.ArrayVariableAssignmentContext context)
    {
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a variable use.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitVariableUse(SinumerikNCParser.VariableUseContext context)
    {
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a macro use.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitMacroUse(SinumerikNCParser.MacroUseContext context)
    {
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a procedure use.
    /// Creates a new <see cref="ProcedureUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitOwnProcedure(SinumerikNCParser.OwnProcedureContext context)
    {
        var name = context.NAME();
        if (name == null) return;

        var token = name.Symbol;
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }

        var arguments = context.arguments() != null ? context.arguments().expression().Select(_ => new ProcedureUseArgument()).ToArray() : Array.Empty<ProcedureUseArgument>();

        SymbolUseTable.Add(new ProcedureUse(token.Text, ParserHelper.GetRangeForToken(token), _document.Information.Uri, arguments));
    }

    /// <summary>
    /// Is called at the end of a extern declaration for a procedure.
    /// Creates a new <see cref="DeclarationProcedureUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitProcedureDeclaration(SinumerikNCParser.ProcedureDeclarationContext context)
    {
        var name = context.NAME();
        if (name == null) return;

        var token = name.Symbol;
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }

        var arguments = context.parameterDeclarations() != null ? context.parameterDeclarations().parameterDeclaration().Select(_ => new ProcedureUseArgument()).ToArray() : Array.Empty<ProcedureUseArgument>();
        
        SymbolUseTable.Add(new DeclarationProcedureUse(token.Text, ParserHelper.GetRangeForToken(token), _document.Information.Uri, arguments));
    }

    /// <summary>
    /// Is called at the end of a goto label command.
    /// Creates a new <see cref="SymbolUse"/> for the used label and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitGotoLabel(SinumerikNCParser.GotoLabelContext context)
    {
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a goto block command.
    /// Creates a new <see cref="SymbolUse"/> for the used block number and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitGotoBlock(SinumerikNCParser.GotoBlockContext context)
    {
        SymbolUseTable.Add(new SymbolUse(context.GetText(), ParserHelper.GetRangeFromStartToEndToken(context.Start, context.Stop), _document.Information.Uri));
    }

    private void AddTokenIfNotPlaceholder(IToken token)
    {
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }
        
        SymbolUseTable.Add(new SymbolUse(token.Text, ParserHelper.GetRangeForToken(token), _document.Information.Uri));
    }

    private void AddNameIfNotPlaceholder(ITerminalNode? name)
    {
        if (name == null)
        {
            return;
        }

        AddTokenIfNotPlaceholder(name.Symbol);
    }
}
