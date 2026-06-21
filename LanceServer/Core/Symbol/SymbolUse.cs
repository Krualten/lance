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
    /// True when the identifier is the target of an ordinary assignment. Such an
    /// assignment is compatible with a direct modal axis address only when another
    /// use of the same identifier provides explicit axis evidence.
    /// </summary>
    public bool IsMachineAxisAssignmentCandidate { get; }

    /// <summary>
    /// True when the use is inside the true branch of an exact
    /// <c>IF ISVAR("identifier")</c> availability guard.
    /// </summary>
    public bool IsConditionallyAvailable { get; }

    /// <summary>
    /// Creates a new symbol use
    /// </summary>
    /// <param name="identifier">The identifier of the symbol used</param>
    /// <param name="range">The position of the usage</param>
    /// <param name="sourceDocument">The source document of the usage</param>
    public SymbolUse(
        string identifier,
        Range range,
        Uri sourceDocument,
        bool canBeMachineAxis = false,
        bool isMachineAxisAssignmentCandidate = false,
        bool isConditionallyAvailable = false)
    {
        Identifier = identifier;
        Range = range;
        SourceDocument = sourceDocument;
        CanBeMachineAxis = canBeMachineAxis;
        IsMachineAxisAssignmentCandidate = isMachineAxisAssignmentCandidate;
        IsConditionallyAvailable = isConditionallyAvailable;
    }
}
