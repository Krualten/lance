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
    private string? _activeCallPath;
    private int _operateGroupDirectiveDepth;
    
    public SymbolUseListener(SymbolisedDocument document)
    {
        _document = document;
    }

    public override void EnterOperateGroupDirective(SinumerikNCParser.OperateGroupDirectiveContext context)
    {
        _operateGroupDirectiveDepth++;
    }

    public override void ExitOperateGroupDirective(SinumerikNCParser.OperateGroupDirectiveContext context)
    {
        _operateGroupDirectiveDepth--;
    }
    
    /// <summary>
    /// Is called at the end of a user variable assignment.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitUserVariableAssignment(SinumerikNCParser.UserVariableAssignmentContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddIdentifierIfNotPlaceholder(context.userVariableIdentifier());
    }

    /// <summary>
    /// Is called at the end of a array variable assignment.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitArrayVariableAssignment(SinumerikNCParser.ArrayVariableAssignmentContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddIdentifierIfNotPlaceholder(context.userVariableIdentifier());
    }

    /// <summary>
    /// Is called at the end of a variable use.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitVariableUse(SinumerikNCParser.VariableUseContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddIdentifierIfNotPlaceholder(context.userVariableIdentifier());
    }

    /// <summary>
    /// Is called at the end of a macro use.
    /// Creates a new <see cref="SymbolUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitMacroUse(SinumerikNCParser.MacroUseContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a procedure use.
    /// Creates a new <see cref="ProcedureUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitOwnProcedure(SinumerikNCParser.OwnProcedureContext context)
    {
        if (IsOperateGroupMetadata) return;

        if (context.Parent is SinumerikNCParser.ProcedureCallContext)
        {
            return;
        }

        var name = context.NAME();
        if (name == null) return;

        var token = name.Symbol;
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }

        var arguments = context.arguments() != null ? context.arguments().expression().Select(_ => new ProcedureUseArgument()).ToArray() : Array.Empty<ProcedureUseArgument>();

        SymbolUseTable.Add(new ProcedureUse(
            token.Text,
            ParserHelper.GetRangeForToken(token),
            _document.Information.Uri,
            arguments,
            _activeCallPath));
    }

    /// <summary>
    /// Creates a procedure reference for PCALL, preserving its absolute NC directory,
    /// optional file identifier and parameter list.
    /// </summary>
    public override void ExitProcedureCall(SinumerikNCParser.ProcedureCallContext context)
    {
        if (IsOperateGroupMetadata) return;

        var procedure = context.program;
        var name = procedure?.NAME();
        if (name == null)
        {
            return;
        }

        var token = name.Symbol;
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }

        var path = context.pcallDirectory?.GetText();
        if (!SinumerikProgramReference.TryParse(
                (path ?? string.Empty) + token.Text,
                out var identifier,
                out var explicitDirectoryPath,
                out var explicitFileExtension))
        {
            return;
        }

        var arguments = procedure!.arguments() != null
            ? procedure.arguments().expression().Select(_ => new ProcedureUseArgument()).ToArray()
            : Array.Empty<ProcedureUseArgument>();
        var canonicalAbsoluteCall = path != null && !HasExplicitProgramFileIdentifier(token.Text);

        SymbolUseTable.Add(new ProcedureUse(
            identifier,
            ParserHelper.GetRangeForToken(token),
            _document.Information.Uri,
            arguments,
            _activeCallPath,
            explicitDirectoryPath,
            explicitFileExtension,
            canonicalAbsoluteCall));
    }

    /// <summary>
    /// Tracks literal CALLPATH values for subsequent procedure calls. Dynamic expressions
    /// cannot be resolved statically and therefore clear the inferred path.
    /// </summary>
    public override void ExitCallPath(SinumerikNCParser.CallPathContext context)
    {
        if (IsOperateGroupMetadata) return;

        var expression = context.expression();
        if (expression == null)
        {
            _activeCallPath = null;
            return;
        }

        if (TryGetStringLiteral(expression.GetText(), out var literalPath))
        {
            literalPath = literalPath.Trim();
            _activeCallPath = string.IsNullOrEmpty(literalPath) ? null : literalPath;
            return;
        }

        _activeCallPath = null;
    }

    /// <summary>
    /// Resolves literal indirect CALL targets. Variable-based indirect calls remain ordinary
    /// variable uses because their runtime value cannot be inferred safely.
    /// </summary>
    public override void ExitCall(SinumerikNCParser.CallContext context)
    {
        if (IsOperateGroupMetadata) return;

        if (context.CALL_BLOCK() != null)
        {
            var programExpression = context.program;
            if (programExpression != null)
            {
                AddLiteralProgramUse(programExpression);
            }

            return;
        }

        var expression = context.expression();
        if (expression != null)
        {
            AddLiteralProgramUse(expression);
        }
    }

    /// <summary>
    /// Resolves literal ISO program calls. Variable-based targets remain ordinary variable
    /// uses because their runtime value cannot be inferred safely.
    /// </summary>
    public override void ExitIsoCall(SinumerikNCParser.IsoCallContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddLiteralProgramUse(context.expression());
    }

    /// <summary>
    /// Resolves literal external program calls when the referenced file is represented in
    /// the workspace. Variable-based external paths remain ordinary variable uses.
    /// </summary>
    public override void ExitExternalCall(SinumerikNCParser.ExternalCallContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddLiteralProgramUse(context.expression());
    }

    private void AddLiteralProgramUse(ParserRuleContext expression)
    {
        if (!TryGetStringLiteral(expression.GetText(), out var programReference)
            || !SinumerikProgramReference.TryParse(
                programReference,
                out var identifier,
                out var explicitDirectoryPath,
                out var explicitFileExtension))
        {
            return;
        }

        SymbolUseTable.Add(new ProcedureUse(
            identifier,
            ParserHelper.GetRangeFromStartToEndToken(expression.Start, expression.Stop),
            _document.Information.Uri,
            Array.Empty<ProcedureUseArgument>(),
            _activeCallPath,
            explicitDirectoryPath,
            explicitFileExtension));
    }

    /// <summary>
    /// Creates a procedure reference for modal subprogram activation. MCALL without a
    /// program name only deactivates the current modal call and creates no reference.
    /// </summary>
    public override void ExitModalCall(SinumerikNCParser.ModalCallContext context)
    {
        if (IsOperateGroupMetadata) return;

        var name = context.NAME();
        if (name == null)
        {
            return;
        }

        var token = name.Symbol;
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }

        var arguments = context.arguments() != null
            ? context.arguments().expression().Select(_ => new ProcedureUseArgument()).ToArray()
            : Array.Empty<ProcedureUseArgument>();

        SymbolUseTable.Add(new ProcedureUse(
            token.Text,
            ParserHelper.GetRangeForToken(token),
            _document.Information.Uri,
            arguments,
            _activeCallPath));
    }

    /// <summary>
    /// Is called at the end of a extern declaration for a procedure.
    /// Creates a new <see cref="DeclarationProcedureUse"/> and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitProcedureDeclaration(SinumerikNCParser.ProcedureDeclarationContext context)
    {
        if (IsOperateGroupMetadata) return;

        var name = context.NAME();
        if (name == null) return;

        var token = name.Symbol;
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }

        var arguments = context.parameterDeclarations() != null ? context.parameterDeclarations().parameterDeclaration().Select(_ => new ProcedureUseArgument()).ToArray() : Array.Empty<ProcedureUseArgument>();
        var identifier = SinumerikProgramReference.TryParse(token.Text, out var normalizedIdentifier, out _)
            ? normalizedIdentifier
            : token.Text;

        SymbolUseTable.Add(new DeclarationProcedureUse(identifier, ParserHelper.GetRangeForToken(token), _document.Information.Uri, arguments));
    }

    /// <summary>
    /// Is called at the end of a goto label command.
    /// Creates a new <see cref="SymbolUse"/> for the used label and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitGotoLabel(SinumerikNCParser.GotoLabelContext context)
    {
        if (IsOperateGroupMetadata) return;
        AddNameIfNotPlaceholder(context.NAME());
    }

    /// <summary>
    /// Is called at the end of a goto block command.
    /// Creates a new <see cref="SymbolUse"/> for the used block number and adds it to the symbol use table, if it isn't a placeholder.
    /// </summary>
    public override void ExitGotoBlock(SinumerikNCParser.GotoBlockContext context)
    {
        if (IsOperateGroupMetadata) return;
        SymbolUseTable.Add(new SymbolUse(context.GetText(), ParserHelper.GetRangeFromStartToEndToken(context.Start, context.Stop), _document.Information.Uri));
    }

    private bool IsOperateGroupMetadata => _operateGroupDirectiveDepth > 0;

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

    private void AddIdentifierIfNotPlaceholder(ParserRuleContext? identifier)
    {
        if (identifier == null)
        {
            return;
        }

        AddTokenIfNotPlaceholder(identifier.Start);
    }

    private static bool TryGetStringLiteral(string text, out string value)
    {
        value = string.Empty;
        if (text.Length < 2 || !text.StartsWith('"') || !text.EndsWith('"'))
        {
            return false;
        }

        value = text[1..^1];
        return true;
    }

    private static bool HasExplicitProgramFileIdentifier(string programName)
    {
        if (!programName.StartsWith("_N_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return new[] { "_SPF", "_MPF", "_CYC", "_CPF" }.Any(suffix =>
            programName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
