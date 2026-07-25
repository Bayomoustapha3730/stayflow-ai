using System.Text;
using System.Text.RegularExpressions;
using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Services.AI.Grounding;

public sealed record WiFiGroundingEntry(
    string SourceId,
    string Title,
    string? Network,
    string? Password);

public sealed record WiFiGroundingResult(
    IReadOnlyCollection<WiFiGroundingEntry> Entries,
    IReadOnlyCollection<string> DistinctNetworks,
    IReadOnlyCollection<string> DistinctPasswords,
    bool HasNetworkConflict,
    bool HasPasswordConflict)
{
    public bool HasConflict => HasNetworkConflict || HasPasswordConflict;
}

public static partial class DeterministicGrounding
{
    private static readonly string[] WiFiNetworkAliases = ["network name", "wi-fi name", "wifi name", "ssid", "network"];
    private static readonly string[] WiFiPasswordAliases = ["password", "passcode"];
    private static readonly string[] WiFiAllAliases = ["network name", "wi-fi name", "wifi name", "ssid", "network", "password", "passcode"];

    public static WiFiGroundingResult ExtractWiFi(IReadOnlyCollection<AIProviderKnowledgeItem> items)
    {
        var entries = new List<WiFiGroundingEntry>();
        var networks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var passwords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var pairs = ExtractWiFiKeyValuePairs(item.Content);
            var network = FirstMatchingValue(pairs, WiFiNetworkAliases);
            var password = FirstMatchingValue(pairs, WiFiPasswordAliases);

            if (network is null)
            {
                network = ExtractInlineValue(item.Content, WiFiNetworkAliases, WiFiAllAliases);
            }

            if (password is null)
            {
                password = ExtractInlineValue(item.Content, WiFiPasswordAliases, WiFiAllAliases);
            }

            network = SanitizeStructuredValue(network);
            password = SanitizeStructuredValue(password);

            if (!string.IsNullOrWhiteSpace(network) || !string.IsNullOrWhiteSpace(password))
            {
                entries.Add(new WiFiGroundingEntry(item.SourceId, item.Title, network, password));
            }

            if (!string.IsNullOrWhiteSpace(network) && !networks.ContainsKey(network))
            {
                networks[network] = network;
            }

            if (!string.IsNullOrWhiteSpace(password) && !passwords.ContainsKey(password))
            {
                passwords[password] = password;
            }
        }

        return new WiFiGroundingResult(
            entries,
            networks.Values.ToList(),
            passwords.Values.ToList(),
            networks.Count > 1,
            passwords.Count > 1);
    }

    public static string BuildConciseGuestFacingContent(string content, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(content) || maxCharacters <= 0)
        {
            return string.Empty;
        }

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Regex.Replace(line.Trim(), @"\s+", " "))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(RemoveMarkdownHeading)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (normalized.Count == 0)
        {
            return string.Empty;
        }

        var joined = string.Join(" ", normalized);
        joined = DeduplicateStructuredFacts(joined);
        if (joined.Length <= maxCharacters)
        {
            return joined;
        }

        var sentenceSplit = SentenceBoundaryRegex().Split(joined)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        var builder = new StringBuilder();
        foreach (var sentence in sentenceSplit)
        {
            var candidate = builder.Length == 0 ? sentence : $"{builder} {sentence}";
            if (candidate.Length > maxCharacters)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(sentence);
        }

        var compact = builder.Length > 0 ? builder.ToString() : joined[..Math.Min(joined.Length, maxCharacters)];
        return DeduplicateStructuredFacts(compact).Trim();
    }

    private static IReadOnlyCollection<KeyValuePair<string, string>> ExtractWiFiKeyValuePairs(string content)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var matches = WiFiInlinePairRegex().Matches(line);
            foreach (Match match in matches)
            {
                var key = NormalizeKey(match.Groups["key"].Value);
                var value = SanitizeStructuredValue(match.Groups["value"].Value);
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                pairs.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        return pairs;
    }

    private static IReadOnlyCollection<KeyValuePair<string, string>> ExtractKeyValuePairs(string content)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = LinePairRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var key = NormalizeKey(match.Groups["key"].Value);
            var value = NormalizeValue(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        return pairs;
    }

    private static string? FirstMatchingValue(IReadOnlyCollection<KeyValuePair<string, string>> pairs, IReadOnlyCollection<string> aliases)
    {
        var aliasSet = aliases.Select(NormalizeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            if (aliasSet.Contains(pair.Key))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? ExtractInlineValue(string content, IReadOnlyCollection<string> aliases, IReadOnlyCollection<string> stopAliases)
    {
        var stopPattern = string.Join("|", stopAliases
            .Select(Regex.Escape)
            .OrderByDescending(item => item.Length));

        foreach (var alias in aliases)
        {
            var pattern = $@"\b{Regex.Escape(alias)}\b\s*(?:[:=])\s*(?<value>.*?)(?=(?:\b(?:{stopPattern})\b\s*(?:[:=]))|$)";
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var value = SanitizeStructuredValue(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string NormalizeValue(string value)
    {
        return value.Trim().Trim('"', '\'', '`', '.', ',', ';');
    }

    private static string? SanitizeStructuredValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        sanitized = EmbeddedAliasClauseRegex().Replace(sanitized, string.Empty).Trim();
        sanitized = NormalizeValue(sanitized);

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private static string DeduplicateStructuredFacts(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = StructuredFactClauseRegex().Replace(text, match =>
        {
            var key = match.Groups["key"].Value;
            var value = NormalizeValue(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return match.Value;
            }

            var token = $"{NormalizeStructuredFactKeyForDedup(key)}::{value.ToLowerInvariant()}";
            if (seen.Add(token))
            {
                return match.Value;
            }

            return string.Empty;
        });

        return CleanupDuplicateWhitespaceRegex().Replace(result, " ").Trim();
    }

    private static string NormalizeStructuredFactKeyForDedup(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..].TrimStart();
        }

        var words = trimmed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Trim('.', ',', ';', ':').ToLowerInvariant())
            .Where(word => word.Length > 0)
            .ToList();

        if (words.Count >= 2 && string.Equals(words[^1], "code", StringComparison.Ordinal))
        {
            trimmed = $"{words[^2]} code";
        }

        return NormalizeKey(trimmed);
    }

    private static string RemoveMarkdownHeading(string line)
    {
        var normalized = line.Trim();
        while (normalized.StartsWith('#'))
        {
            normalized = normalized[1..].TrimStart();
        }

        return normalized;
    }

    [GeneratedRegex(@"\s*[\.\!\?]\s+")]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"(?<key>network name|wi-fi name|wifi name|ssid|network|password|passcode)\s*(?:[:=])\s*(?<value>.*?)(?=(?:\b(?:network name|wi-fi name|wifi name|ssid|network|password|passcode)\b\s*(?:[:=]))|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WiFiInlinePairRegex();

    [GeneratedRegex(@"\b(?:network name|wi-fi name|wifi name|ssid|network|password|passcode)\b\s*(?:[:=]).*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedAliasClauseRegex();

    [GeneratedRegex(@"(?<lead>\s*(?:,?\s*and\s+(?:the\s+)?)?)?(?<key>wi-?fi network|network|ssid|password|passcode|check-?in code|parking code|[a-z][a-z\- ]{1,25}\s+code)\s*(?:is|:|=)\s*(?<value>[^\n,.;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StructuredFactClauseRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex CleanupDuplicateWhitespaceRegex();

    [GeneratedRegex(@"^\s*(?<key>[A-Za-z][A-Za-z0-9\s\-/]{1,40})\s*(?:[:=])\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LinePairRegex();
}