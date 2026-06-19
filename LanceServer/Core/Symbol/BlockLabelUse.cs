using Range = LspTypes.Range;

namespace LanceServer.Core.Symbol;

/// <summary>
/// A start or end position used by CALL ... BLOCK ... TO ....
/// The identifier can either be a caller-side string variable or a label in the
/// statically known target program.
/// </summary>
public class BlockLabelUse : SymbolUse
{
    /// <summary>
    /// The statically known target program. Null means the current program.
    /// </summary>
    public string? TargetProgramIdentifier { get; }

    /// <summary>
    /// Statically known CALLPATH directory active at this call, if any.
    /// </summary>
    public string? CallPath { get; }

    /// <summary>
    /// Directory explicitly encoded in the target program reference, if any.
    /// </summary>
    public string? ExplicitDirectoryPath { get; }

    /// <summary>
    /// File extension explicitly encoded in the target program reference, if any.
    /// </summary>
    public string? ExplicitFileExtension { get; }

    public BlockLabelUse(
        string identifier,
        Range range,
        Uri sourceDocument,
        string? targetProgramIdentifier,
        string? callPath = null,
        string? explicitDirectoryPath = null,
        string? explicitFileExtension = null)
        : base(identifier, range, sourceDocument)
    {
        TargetProgramIdentifier = targetProgramIdentifier;
        CallPath = callPath;
        ExplicitDirectoryPath = explicitDirectoryPath;
        ExplicitFileExtension = explicitFileExtension;
    }
}
