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
        AddIdentifierIfNotPlaceholder(context.userVariableIdentifier(), canBeMachineAxis: true);
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
        AddIdentifierIfNotPlaceholder(
            context.userVariableIdentifier(),
            canBeMachineAxis: IsSystemVariableIndex(context));
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

        var arguments = GetProcedureArguments(context.arguments());

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

        var arguments = GetProcedureArguments(procedure!.arguments());
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

        var arguments = GetProcedureArguments(context.arguments());

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

        var arguments = context.parameterDeclarations()?.parameterDeclaration()
            .Select((declaration, position) => CreateDeclarationArgument(declaration, position))
            .ToArray() ?? Array.Empty<ProcedureUseArgument>();
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

    private static ProcedureUseArgument[] GetProcedureArguments(SinumerikNCParser.ArgumentsContext? context)
    {
        if (context == null || (context.expression().Length == 0 && context.COMMA().Length == 0))
        {
            return Array.Empty<ProcedureUseArgument>();
        }

        var expressions = context.expression();
        var separators = context.COMMA()
            .Select(comma => comma.Symbol.TokenIndex)
            .ToList();
        var closingBoundary = context.CLOSE_PAREN()?.Symbol.TokenIndex
                              ?? (context.Stop?.TokenIndex ?? context.Start.TokenIndex) + 1;
        separators.Add(closingBoundary);
        var arguments = new ProcedureUseArgument[separators.Count];
        var previousSeparator = context.OPEN_PAREN()?.Symbol.TokenIndex ?? context.Start.TokenIndex;

        for (var position = 0; position < separators.Count; position++)
        {
            var separator = separators[position];
            var expression = expressions.FirstOrDefault(expression =>
                expression.Start.TokenIndex > previousSeparator
                && expression.Stop.TokenIndex < separator);
            arguments[position] = CreateProcedureArgument(position, expression);
            previousSeparator = separator;
        }

        return arguments;
    }

    private static ProcedureUseArgument CreateProcedureArgument(
        int position,
        SinumerikNCParser.ExpressionContext? expression)
    {
        if (expression == null)
        {
            return new ProcedureUseArgument(position, isOmitted: true);
        }

        var primaryExpression = expression is SinumerikNCParser.PrimaryExpressionLabelContext primaryLabel
            ? primaryLabel.primaryExpression()
            : null;

        if (primaryExpression is SinumerikNCParser.VariableUseContext variableUse)
        {
            return new ProcedureUseArgument(
                position,
                referencedIdentifier: variableUse.userVariableIdentifier()?.GetText(),
                isWritableReference: true,
                range: ParserHelper.GetRangeFromStartToEndToken(expression.Start, expression.Stop));
        }

        if (primaryExpression is SinumerikNCParser.RParamUseContext)
        {
            return new ProcedureUseArgument(
                position,
                inferredDataType: DataType.Real,
                isWritableReference: true,
                range: ParserHelper.GetRangeFromStartToEndToken(expression.Start, expression.Stop));
        }

        return new ProcedureUseArgument(
            position,
            inferredDataType: InferExpressionType(expression),
            range: ParserHelper.GetRangeFromStartToEndToken(expression.Start, expression.Stop));
    }

    private static ProcedureUseArgument CreateDeclarationArgument(
        SinumerikNCParser.ParameterDeclarationContext declaration,
        int position)
    {
        var typeContext = declaration switch
        {
            SinumerikNCParser.ParameterDeclarationByValueContext value => value.type(),
            SinumerikNCParser.ParameterDeclarationByReferenceContext reference => reference.type(),
            _ => null
        };

        return new ProcedureUseArgument(
            position,
            inferredDataType: ParseDataType(typeContext?.GetText()),
            isWritableReference: declaration is SinumerikNCParser.ParameterDeclarationByReferenceContext,
            range: ParserHelper.GetRangeFromStartToEndToken(declaration.Start, declaration.Stop));
    }

    private static DataType? InferExpressionType(SinumerikNCParser.ExpressionContext expression)
    {
        switch (expression)
        {
            case SinumerikNCParser.RelationalExpressionContext:
            case SinumerikNCParser.AndExpressionContext:
            case SinumerikNCParser.ExclusiveOrExpressionContext:
            case SinumerikNCParser.InclusiveOrExpressionContext:
                return DataType.Bool;
            case SinumerikNCParser.ToStringExpressionContext:
            case SinumerikNCParser.ConcatExpressionContext:
                return DataType.String;
            case SinumerikNCParser.SignExpressionContext sign:
                return InferPrimaryExpressionType(sign.primaryExpression());
            case SinumerikNCParser.PrimaryExpressionLabelContext primary:
                return InferPrimaryExpressionType(primary.primaryExpression());
            case SinumerikNCParser.AdditiveExpressionContext additive:
                return InferNumericResult(additive.expression());
            case SinumerikNCParser.MultiplicativeExpressionContext multiplicative:
                return InferNumericResult(multiplicative.expression());
            default:
                return null;
        }
    }

    private static DataType? InferPrimaryExpressionType(
        SinumerikNCParser.PrimaryExpressionContext? expression)
    {
        return expression switch
        {
            SinumerikNCParser.ConstantUseContext constant => InferConstantType(constant.constant()),
            SinumerikNCParser.RParamUseContext => DataType.Real,
            SinumerikNCParser.AxisUseContext => DataType.Axis,
            SinumerikNCParser.NestedExpressionContext nested => InferExpressionType(nested.expression()),
            _ => null
        };
    }

    private static DataType? InferConstantType(SinumerikNCParser.ConstantContext? constant)
    {
        if (constant == null)
        {
            return null;
        }

        return constant.Start.Type switch
        {
            SinumerikNCLexer.STRING => DataType.String,
            SinumerikNCLexer.BOOL => DataType.Bool,
            SinumerikNCLexer.REAL_UNSIGNED => DataType.Real,
            SinumerikNCLexer.INT_UNSIGNED or SinumerikNCLexer.HEX or SinumerikNCLexer.BIN => DataType.Int,
            _ => null
        };
    }

    private static DataType? InferNumericResult(
        IEnumerable<SinumerikNCParser.ExpressionContext> expressions)
    {
        var types = expressions.Select(InferExpressionType).ToArray();
        if (types.Any(type => type == null || type is DataType.String or DataType.Axis or DataType.Frame))
        {
            return null;
        }

        return types.Any(type => type == DataType.Real) ? DataType.Real : DataType.Int;
    }

    private static DataType? ParseDataType(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith("string", StringComparison.OrdinalIgnoreCase))
        {
            return DataType.String;
        }

        return Enum.TryParse<DataType>(text, true, out var dataType) ? dataType : null;
    }

    private static bool IsSystemVariableIndex(ParserRuleContext context)
    {
        for (var parent = context.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is SinumerikNCParser.SystemVariableUseContext)
            {
                return true;
            }
        }

        return false;
    }

    private void AddTokenIfNotPlaceholder(IToken token, bool canBeMachineAxis = false)
    {
        if (_document.PlaceholderTable.ContainedPlaceholder(token.Text))
        {
            return;
        }
        
        SymbolUseTable.Add(new SymbolUse(
            token.Text,
            ParserHelper.GetRangeForToken(token),
            _document.Information.Uri,
            canBeMachineAxis));
    }

    private void AddNameIfNotPlaceholder(ITerminalNode? name)
    {
        if (name == null)
        {
            return;
        }

        AddTokenIfNotPlaceholder(name.Symbol);
    }

    private void AddIdentifierIfNotPlaceholder(
        ParserRuleContext? identifier,
        bool canBeMachineAxis = false)
    {
        if (identifier == null)
        {
            return;
        }

        AddTokenIfNotPlaceholder(identifier.Start, canBeMachineAxis);
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
