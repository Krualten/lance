namespace LanceServer.Core.Symbol;

/// <summary>
/// Parses program names and NC paths used by indirect CALL statements.
/// </summary>
public static class SinumerikProgramReference
{
    public static bool TryParse(
        string value,
        out string identifier,
        out string? directoryPath)
    {
        return TryParse(value, out identifier, out directoryPath, out _);
    }

    public static bool TryParse(
        string value,
        out string identifier,
        out string? directoryPath,
        out string? fileExtension)
    {
        identifier = string.Empty;
        directoryPath = null;
        fileExtension = null;

        var normalized = value.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        identifier = NormalizeProgramName(segments[^1], out fileExtension);
        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }

        if (segments.Length > 1)
        {
            directoryPath = "/" + string.Join("/", segments[..^1]);
        }

        return true;
    }

    private static string NormalizeProgramName(string programName, out string? fileExtension)
    {
        fileExtension = null;
        var normalized = programName.Trim();
        if (normalized.StartsWith("_N_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..];
        }

        foreach (var suffix in new[] { "_SPF", "_MPF", "_CYC", "_CPF", ".SPF", ".MPF", ".CYC", ".CPF" })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^suffix.Length];
                fileExtension = "." + suffix[^3..].ToLowerInvariant();
                break;
            }
        }

        return normalized;
    }
}
