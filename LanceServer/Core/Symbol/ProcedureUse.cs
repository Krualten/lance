using Range = LspTypes.Range;

namespace LanceServer.Core.Symbol;

/// <summary>
/// A call to a procedure symbol
/// </summary>
public class ProcedureUse : AbstractSymbolUse
{
    /// <inheritdoc />
    public override string Identifier { get; }
    
    /// <inheritdoc />
    public override Range Range { get; }
    
    /// <inheritdoc />
    public override Uri SourceDocument { get; }
    
    /// <summary>
    /// The arguments for the procedure which is called
    /// </summary>
    public ProcedureUseArgument[] Arguments { get; }

    /// <summary>
    /// Statically known CALLPATH directory active at this procedure call, if any.
    /// </summary>
    public string? CallPath { get; }

    /// <summary>
    /// Directory explicitly encoded in a literal indirect CALL program path, if any.
    /// </summary>
    public string? ExplicitDirectoryPath { get; }
    
    public ProcedureUse(
        string identifier,
        Range range,
        Uri sourceDocument,
        ProcedureUseArgument[] arguments,
        string? callPath = null,
        string? explicitDirectoryPath = null)
    {
        Identifier = identifier;
        Range = range;
        SourceDocument = sourceDocument;
        Arguments = arguments;
        CallPath = callPath;
        ExplicitDirectoryPath = explicitDirectoryPath;
    }
}
