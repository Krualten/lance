namespace LanceServer.Core.Symbol;

/// <summary>
/// The data type of a symbol with an optional length if the type is a string
/// </summary>
public class CompositeDataType
{
    public DataType DataType { get; }
    private readonly string _length;

    /// <summary>
    /// Instantiates a new <see cref="CompositeDataType"/>
    /// </summary>
    /// <param name="dataType">The data type</param>
    /// <param name="length">The expression of the length as a string if the data type is a string</param>
    public CompositeDataType(DataType dataType, string length = "")
    {
        _length = length;
        DataType = dataType;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        string length = string.Empty;

        if (DataType == DataType.String)
        {
            length = $"[{_length}]";
        }
            
        return $"{DataType.ToString().ToLower()}{length}";
    }
}
