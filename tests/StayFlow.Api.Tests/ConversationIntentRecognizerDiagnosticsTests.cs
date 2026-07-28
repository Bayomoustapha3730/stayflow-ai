using System.Reflection;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Tests;

public sealed class ConversationIntentRecognizerDiagnosticsTests
{
    private static readonly Type RecognizerType = typeof(ConversationIntentRecognizer);
    private static readonly MethodInfo NormalizeMethod = RecognizerType.GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo IndexOfPhraseMethod = RecognizerType.GetMethod("IndexOfPhrase", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo IsFuzzyTokenMatchMethod = RecognizerType.GetMethod("IsFuzzyTokenMatch", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo IntentPhrasesField = RecognizerType.GetField("IntentPhrases", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("What is the Wi-Fi password?", "what is the wifi password")]
    [InlineData("wifi", "wifi")]
    [InlineData("WiFi", "wifi")]
    [InlineData("wireless password", "wireless password")]
    [InlineData("Quel est le mot de passe Wi-Fi ?", "quel est le mot de passe wifi")]
    [InlineData("There is a fire.", "there is a fire")]
    [InlineData("I smell gas.", "i smell gas")]
    [InlineData("When can I get into the apartment?", "when can i get into the apartment")]
    [InlineData("How do I enter?", "how do i enter")]
    [InlineData("Can I bring pets?", "can i bring pets")]
    public void Normalize_ReturnsExpectedCanonicalText(string input, string expected)
    {
        var normalized = Normalize(input);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Vocabulary_ContainsExpectedNormalizedPhrases()
    {
        var vocab = GetNormalizedVocabulary();

        AssertContains(vocab, GuestIntent.WiFi, ["wifi", "wireless", "internet", "network", "password", "wireless password"]);
        AssertContains(vocab, GuestIntent.Emergency, ["fire", "there is smoke", "gas leak", "smell gas", "injured", "ambulance"]);
        AssertContains(vocab, GuestIntent.CheckIn, ["checkin", "arrival", "get into the apartment", "allowed inside"]);
        AssertContains(vocab, GuestIntent.PropertyAccess, ["enter", "entry", "get inside", "access code"]);
        AssertContains(vocab, GuestIntent.PetPolicy, ["pets", "pet", "dog", "cat", "animal"]);

        Assert.All(vocab, pair => Assert.NotEmpty(pair.Value));
    }

    [Theory]
    [InlineData("What is the Wi-Fi password?", GuestIntent.WiFi)]
    [InlineData("Do you have internet?", GuestIntent.WiFi)]
    [InlineData("Quel est le mot de passe Wi-Fi ?", GuestIntent.WiFi)]
    [InlineData("There is a fire.", GuestIntent.Emergency)]
    [InlineData("I smell gas.", GuestIntent.Emergency)]
    [InlineData("When can I get into the apartment?", GuestIntent.CheckIn)]
    [InlineData("How do I enter?", GuestIntent.PropertyAccess)]
    [InlineData("Can I bring pets?", GuestIntent.PetPolicy)]
    [InlineData("What color are the curtains?", GuestIntent.Unknown)]
    public void Recognize_ExpectedPrimaryIntent_WithDiagnostics(string query, GuestIntent expected)
    {
        var recognizer = new ConversationIntentRecognizer();
        var result = recognizer.Recognize(query, maximumIntents: 3);

        var normalizedTokens = string.Join(",", result.NormalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var matchedPhrases = string.Join(",", result.MatchedSignals);
        var matchedIntents = string.Join(",", result.AllIntents());

        Assert.True(
            result.PrimaryIntent == expected,
            $"Expected {expected} but got {result.PrimaryIntent}. normalized='{result.NormalizedQuery}', tokens='{normalizedTokens}', matchedPhrases='{matchedPhrases}', matchedIntents='{matchedIntents}', primary='{result.PrimaryIntent}'.");
    }

    [Fact]
    public void RawScores_HighSignalQueries_BeatUnknown()
    {
        AssertScoreBeatsUnknown("What is the Wi-Fi password?", GuestIntent.WiFi);
        AssertScoreBeatsUnknown("wireless password", GuestIntent.WiFi);
        AssertScoreBeatsUnknown("Do you have internet?", GuestIntent.WiFi);

        AssertScoreBeatsUnknown("There is a fire.", GuestIntent.Emergency);
        AssertScoreBeatsUnknown("I smell gas.", GuestIntent.Emergency);
    }

    [Fact]
    public void Recognize_ConnectFollowUp_WithWifiContext_PrefersWifiWithoutClarification()
    {
        var recognizer = new ConversationIntentRecognizer();
        var result = recognizer.Recognize("How do I connect?", ["WiFi"], maximumIntents: 3);

        Assert.Equal(GuestIntent.WiFi, result.PrimaryIntent);
        Assert.False(result.IsAmbiguous);
        Assert.Contains(result.MatchedSignals, signal => signal.Contains("context:wifi", StringComparison.Ordinal));
    }

    [Fact]
    public void Recognize_ConnectQuestion_WithoutContext_RemainsAmbiguous()
    {
        var recognizer = new ConversationIntentRecognizer();
        var result = recognizer.Recognize("How do I connect?", null, maximumIntents: 3);

        Assert.Equal(GuestIntent.WiFi, result.PrimaryIntent);
        Assert.True(result.IsAmbiguous);
    }

    [Fact]
    public void Recognize_ExplicitMultiIntent_DoesNotForceAmbiguous()
    {
        var recognizer = new ConversationIntentRecognizer();
        var result = recognizer.Recognize("What is the Wi-Fi password and what time is checkout?", null, maximumIntents: 3);

        Assert.Equal(GuestIntent.WiFi, result.PrimaryIntent);
        Assert.Contains(GuestIntent.Checkout, result.SecondaryIntents);
        Assert.False(result.IsAmbiguous);
    }

    private static void AssertScoreBeatsUnknown(string query, GuestIntent expected)
    {
        var scores = ComputeRawScores(query);
        var expectedScore = scores.TryGetValue(expected, out var value) ? value : 0d;
        var unknownScore = scores.TryGetValue(GuestIntent.Unknown, out var unknown) ? unknown : 0d;

        Assert.True(
            expectedScore > unknownScore,
            $"Expected raw score for {expected} to be greater than Unknown. query='{query}', expectedScore={expectedScore:0.###}, unknownScore={unknownScore:0.###}.");
    }

    private static Dictionary<GuestIntent, double> ComputeRawScores(string query)
    {
        var normalized = Normalize(query);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var vocab = GetNormalizedVocabulary();

        var scores = new Dictionary<GuestIntent, double>();

        foreach (var pair in vocab)
        {
            var score = 0d;
            foreach (var phrase in pair.Value.OrderByDescending(item => item.Length))
            {
                var index = IndexOfPhrase(normalized, phrase);
                if (index >= 0)
                {
                    score += phrase.Contains(' ', StringComparison.Ordinal) ? 3.5 : 1.8;
                    continue;
                }

                if (IsFuzzyTokenMatch(tokens, phrase))
                {
                    score += 0.8;
                }
            }

            scores[pair.Key] = score;
        }

        scores.TryAdd(GuestIntent.Unknown, 0d);
        return scores;
    }

    private static Dictionary<GuestIntent, HashSet<string>> GetNormalizedVocabulary()
    {
        var source = (IReadOnlyDictionary<GuestIntent, string[]>)IntentPhrasesField.GetValue(null)!;
        var normalized = new Dictionary<GuestIntent, HashSet<string>>();

        foreach (var pair in source)
        {
            normalized[pair.Key] = pair.Value
                .Select(Normalize)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
        }

        return normalized;
    }

    private static string Normalize(string input)
        => (string)NormalizeMethod.Invoke(null, [input])!;

    private static int IndexOfPhrase(string haystack, string phrase)
        => (int)IndexOfPhraseMethod.Invoke(null, [haystack, phrase])!;

    private static bool IsFuzzyTokenMatch(IReadOnlyCollection<string> tokens, string phrase)
        => (bool)IsFuzzyTokenMatchMethod.Invoke(null, [tokens, phrase])!;

    private static void AssertContains(
        IReadOnlyDictionary<GuestIntent, HashSet<string>> vocab,
        GuestIntent intent,
        IReadOnlyCollection<string> required)
    {
        Assert.True(vocab.TryGetValue(intent, out var phrases), $"Missing intent vocabulary for {intent}.");
        foreach (var phrase in required)
        {
            var normalized = Normalize(phrase);
            Assert.Contains(normalized, phrases!);
        }
    }
}
