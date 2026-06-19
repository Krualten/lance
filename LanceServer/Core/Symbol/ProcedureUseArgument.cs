namespace LanceServer.Core.Symbol;

using Range = LspTypes.Range;

/// <summary>
/// The argument for procedure references.
/// </summary>
public class ProcedureUseArgument
{
    /// <summary>
    /// Zero-based position in the procedure call or declaration.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Whether the caller preserved this parameter position with commas but supplied no value.
    /// </summary>
    public bool IsOmitted { get; }

    /// <summary>
    /// Type inferred directly from the argument syntax, if it is unambiguous.
    /// </summary>
    public DataType? InferredDataType { get; }

    /// <summary>
    /// Identifier of a simple variable or parameter argument whose type can be resolved
    /// from the caller's symbol table.
    /// </summary>
    public string? ReferencedIdentifier { get; }

    /// <summary>
    /// Whether the expression can be passed to a call-by-reference parameter.
    /// </summary>
    public bool IsWritableReference { get; }

    public Range? Range { get; }

    public ProcedureUseArgument(
        int position = 0,
        bool isOmitted = false,
        DataType? inferredDataType = null,
        string? referencedIdentifier = null,
        bool isWritableReference = false,
        Range? range = null)
    {
        Position = position;
        IsOmitted = isOmitted;
        InferredDataType = inferredDataType;
        ReferencedIdentifier = referencedIdentifier;
        IsWritableReference = isWritableReference;
        Range = range;
    }
}
