using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using LanceServer.Core.Configuration;
using LanceServer.Core.Document;

namespace LanceServer.Preprocessor;

/// <inheritdoc />
public class PlaceholderPreprocessor : IPlaceholderPreprocessor
{
    private readonly IConfigurationManager _configurationManager;
    private static readonly ConcurrentDictionary<string, Regex> CompiledRegexes = new();

    public PlaceholderPreprocessor(IConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }
    
    /// <inheritdoc />
    public PlaceholderPreprocessedDocument Filter(ReadDocument document)
    {
        var preprocessorConfiguration = _configurationManager.CustomPreprocessorConfiguration;

        var placeholders = new Dictionary<string, string>();
            
        if (!preprocessorConfiguration.FileExtensions.Contains(document.Information.FileExtension))
        {
            return new PlaceholderPreprocessedDocument(document, document.RawContent, new PlaceholderTable(placeholders));
        }

        var resultBuilder = new StringBuilder();
        var rawLines = Regex.Split(document.RawContent, @"(?<=\n)");
        
        foreach (var placeholder in preprocessorConfiguration.Placeholders)
        {
            var pattern = placeholder;
            if (preprocessorConfiguration.PlaceholderType == PlaceholderType.String)
            {
                pattern = Regex.Escape(placeholder);
            }

            var regex = GetOrAddRegex(pattern);

            foreach (var rawLine in rawLines)
            {
                var matches = regex.Matches(rawLine);
                if (matches.Count == 0)
                {
                    resultBuilder.Append(rawLine);
                    continue;
                }

                var line = rawLine;
                foreach (Match match in matches)
                {
                    if (rawLine.Trim() == match.Value)
                    {
                        line = line.Replace(match.Value, "", StringComparison.Ordinal);
                    }
                    else
                    {
                        var processedMatch = Regex.Replace(match.Value, "[^a-zA-Z0-9_]", "_");
                        line = line.Replace(match.Value, processedMatch, StringComparison.Ordinal);
                        placeholders.TryAdd(processedMatch, match.Value);
                    }
                }
                resultBuilder.Append(line);
            }
        }

        placeholders = placeholders
            .OrderByDescending(pair => pair.Key.Length)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
            
        return new PlaceholderPreprocessedDocument(document, resultBuilder.ToString(), new PlaceholderTable(placeholders));
    }

    private static Regex GetOrAddRegex(string pattern)
    {
        return CompiledRegexes.GetOrAdd(pattern, p => 
            new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant));
    }
}
