namespace LanceServer.Core.Symbol;

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

    public ProcedureUseArgument(int position = 0, bool isOmitted = false)
    {
        Position = position;
        IsOmitted = isOmitted;
    }
}
