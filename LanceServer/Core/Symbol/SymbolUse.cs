using Range = LspTypes.Range;

namespace LanceServer.Core.Symbol;

/// <inheritdoc />
public class SymbolUse : AbstractSymbolUse
{
    /// <inheritdoc />
    public override string Identifier { get; }

    /// <inheritdoc />
    public override Range Range { get; }

    /// <inheritdoc />
    public override Uri SourceDocument { get; }

    /// <summary>
    /// True when the identifier occurs in a syntactic position that may contain a
    /// freely configured machine axis name. If no declared symbol exists, the use
    /// is treated as an axis address rather than as an unresolved variable.
    /// </summary>
    public bool CanBeMachineAxis { get; }

    /// <summary>
    /// Creates a new symbol use
    /// </summary>
    /// <param name="identifier">The identifier of the symbol used</param>
    /// <param name="range">The position of the usage</param>
    /// <param name="sourceDocument">The source document of the usage</param>
    public SymbolUse(string identifier, Range range, Uri sourceDocument, bool canBeMachineAxis = false)
    {
        Identifier = identifier;
        Range = range;
        SourceDocument = sourceDocument;
        CanBeMachineAxis = canBeMachineAxis;
    }
}
