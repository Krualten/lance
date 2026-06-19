using Range = LspTypes.Range;

namespace LanceServer.Core.Symbol;

/// <summary>
/// A symbol representing a function, having zero or more <see cref="ParameterSymbol"/>s
/// </summary>
public class ProcedureSymbol : AbstractSymbol
{
    /// <inheritdoc/>
    public override string Identifier { get; }

    /// <inheritdoc/>
    public override Uri SourceDocument { get; }
        
    /// <inheritdoc/>
    public override Range SymbolRange { get; }
        
    /// <inheritdoc/>
    public override Range IdentifierRange { get; }
        
    /// <inheritdoc/>
    public override string Description => $"procedure in {Path.GetFileName(SourceDocument.LocalPath)}";
        
    /// <inheritdoc/>
    public override string Code => $"proc {Identifier}({string.Join(ParameterDelimiter, Parameters.Select(p => p.Code))})";
        
    /// <inheritdoc/>
    public override string Documentation { get; }
    
    /// <summary>
    /// True if the procedure could need an extern declaration, False otherwise.
    /// This is also dependent on whether or not the caller submits arguments.
    /// </summary>
    public bool MayNeedExternDeclaration { get; }

    /// <summary>
    /// The parameters of this procedure
    /// </summary>
    /// <seealso cref="ProcedureSymbol"/>
    public readonly ParameterSymbol[] Parameters;

    /// <summary>
    /// Checks if the arguments match the parameters.
    /// </summary>
    /// <param name="arguments">The arguments given to the procedure.</param>
    /// <returns>True if the number of arguments matches the parameters required, false otherwise.</returns>
    public bool ArgumentsMatchParameters(ProcedureUseArgument[] arguments)
    {
        if (arguments.Length > Parameters.Length)
        {
            return false;
        }

        for (var index = 0; index < Parameters.Length; index++)
        {
            var omitted = index >= arguments.Length || arguments[index].IsOmitted;
            if (omitted && !Parameters[index].CanBeOmitted)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// EXTERN declarations describe the complete procedure interface and therefore cannot
    /// omit trailing or positional formal parameters.
    /// </summary>
    public bool DeclarationMatchesParameters(ProcedureUseArgument[] arguments)
    {
        if (arguments.Length != Parameters.Length || arguments.Any(argument => argument.IsOmitted))
        {
            return false;
        }

        return arguments.Select((argument, index) =>
                argument.InferredDataType == Parameters[index].DataType
                && argument.IsWritableReference == Parameters[index].IsReferenceValue)
            .All(matches => matches);
    }

    public IEnumerable<int> GetIncompatibleArgumentPositions(
        ProcedureUseArgument[] arguments,
        Func<ProcedureUseArgument, DataType?> resolveDataType)
    {
        for (var index = 0; index < Math.Min(arguments.Length, Parameters.Length); index++)
        {
            var argument = arguments[index];
            if (argument.IsOmitted)
            {
                continue;
            }

            var argumentType = resolveDataType(argument);
            if (!Parameters[index].AcceptsArgument(argumentType, argument.IsWritableReference))
            {
                yield return index;
            }
        }
    }
    
    private const string ParameterDelimiter = ", ";

    public ProcedureSymbol(string identifier, Uri sourceDocument, Range symbolRange, Range identifierRange, ParameterSymbol[] parameters, bool mayNeedExternDeclaration = false, string documentation = "")
    {
        Identifier = identifier;
        SourceDocument = sourceDocument;
        SymbolRange = symbolRange;
        IdentifierRange = identifierRange;
        Parameters = parameters;
        MayNeedExternDeclaration = mayNeedExternDeclaration;
        Documentation = documentation;
    }
}
