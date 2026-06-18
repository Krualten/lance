namespace LanceServer.Core.Document;

/// <summary>
/// The type of the document in the context of the sinumerik nc
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// The type of definition files.
    /// </summary>
    Definition,
    
    /// <summary>
    /// The type of main procedure files.
    /// </summary>
    MainProcedure,
    
    /// <summary>
    /// The type of user, manufacturer or standard cycle procedure files.
    /// </summary>
    CycleSubProcedure,

    /// <summary>
    /// Legacy name for cycle procedure files.
    /// </summary>
    [Obsolete("Use CycleSubProcedure instead.")]
    ManufacturerSubProcedure = CycleSubProcedure,
    
    /// <summary>
    /// The type of sub procedure files.
    /// </summary>
    SubProcedure
}
