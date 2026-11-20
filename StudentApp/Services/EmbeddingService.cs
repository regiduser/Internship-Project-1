using System.Text.RegularExpressions;

namespace StudentApp.Services;

/// <summary>
/// Builds compact, deterministic feature vectors for lightweight semantic-style search.
/// <para>
/// This is <b>not</b> a trained neural model. It is a transparent "hashing vectorizer":
/// </para>
/// <list type="number">
///   <item>Tokenize, drop stop-words, and lightly stem each word (<c>projects</c> → <c>project</c>).</item>
///   <item>Emit three kinds of features: the word itself, a bigram with the previous word,
///         and one feature per <i>concept group</i> the word belongs to (so
///         <c>coding</c>, <c>software</c> and <c>programming</c> all light up the same
///         "programming" feature).</item>
///   <item>Repeated features count sub-linearly (1 + ln n) so keyword stuffing does not dominate.</item>
///   <item>Each feature is hashed (stable FNV-1a plus an avalanche step) into one of
///         <see cref="Dimensions"/> slots using a <b>signed</b> hash, so unrelated features that
///         collide tend to cancel out instead of piling up.</item>
///   <item>The vector is L2-normalised, which makes cosine similarity a plain dot product.</item>
/// </list>
/// </summary>
public static class EmbeddingService
{
    public const int Dimensions = 512;

    private const float WordWeight = 1.0f;
    private const float ConceptWeight = 0.65f;
    private const float BigramWeight = 0.35f;

    private static readonly Regex TokenPattern = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "am", "an", "and", "are", "as", "at", "be", "but", "by", "for", "from", "has", "have", "i",
        "in", "interested", "into", "is", "it", "like", "love", "my", "of", "on", "or", "student",
        "that", "the", "their", "this", "to", "want", "was", "with", "who", "wants", "enjoys",
        "enjoy", "looking", "find", "describe", "about", "interests", "interest", "skills", "skill",
        "goals", "goal", "me"
    };

    /// <summary>
    /// Concept groups. A word may belong to several groups (e.g. "communication" is both a
    /// networking and a soft-skills word). Words are stored in their stemmed form at start-up.
    /// </summary>
    private static readonly Dictionary<string, string[]> ConceptsByWord = BuildConceptIndex(new Dictionary<string, string>
    {
        ["programming"] = "programming code coding coder software developer development engineer engineering computer computing algorithm algorithms python java csharp c cpp rust app apps application",
        ["security"] = "security cybersecurity cyber defense defence protection hacking ethical hacker encryption cryptography privacy firewall malware pentest",
        ["networking"] = "network networking networks internet connectivity router routers wifi tcp protocol protocols communication",
        ["data"] = "database databases sql data storage records analytics warehouse query queries nosql",
        ["ai"] = "artificial intelligence ai machine learning ml deep neural model models nlp vision llm chatbot",
        ["design"] = "design designer ui ux creative graphics graphic illustration drawing art artist figma",
        ["sports"] = "football soccer sports sport athletics athlete fitness basketball cricket tennis gym running",
        ["music"] = "music singing singer instrument guitar piano songs song audio band",
        ["leadership"] = "leadership leader management manager teamwork team collaboration communication mentoring organising organizing",
        ["math"] = "math maths mathematics statistics statistical quantitative algebra calculus probability",
        ["web"] = "web website websites frontend backend fullstack html css javascript typescript react browser",
        ["cloud"] = "cloud azure aws gcp devops deployment infrastructure docker kubernetes server servers",
        ["writing"] = "writing writer reading books book literature language languages blogging journalism poetry",
        ["business"] = "business entrepreneurship entrepreneur startup startups finance marketing sales economics accounting"
    });

    /// <summary>Turns text into an L2-normalised vector. Empty or stop-word-only text gives a zero vector.</summary>
    public static float[] Embed(string? text)
    {
        var vector = new float[Dimensions];
        var tokens = Tokenize(text);
        if (tokens.Count == 0) return vector;

        // Count features first so repeats can be weighted sub-linearly.
        var features = new Dictionary<string, (float Weight, int Count)>(StringComparer.Ordinal);
        void Count(string feature, float weight) =>
            features[feature] = features.TryGetValue(feature, out var e) ? (weight, e.Count + 1) : (weight, 1);

        for (var i = 0; i < tokens.Count; i++)
        {
            Count("w:" + tokens[i], WordWeight);
            if (ConceptsByWord.TryGetValue(tokens[i], out var concepts))
                foreach (var concept in concepts) Count("c:" + concept, ConceptWeight);
            if (i > 0) Count("b:" + tokens[i - 1] + "_" + tokens[i], BigramWeight);
        }

        foreach (var (feature, (weight, count)) in features)
            AddFeature(vector, feature, weight * (1f + MathF.Log(count)));

        Normalize(vector);
        return vector;
    }

    /// <summary>Cosine similarity of two vectors (0 when either is empty or lengths differ).</summary>
    public static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length || left.Length == 0) return 0;
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }
        if (leftNorm == 0 || rightNorm == 0) return 0;
        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    /// <summary>The distinct (stemmed) words in <paramref name="text"/> after stop-word removal.</summary>
    public static IReadOnlyList<string> ExtractTerms(string? text) => Tokenize(text).Distinct().ToList();

    /// <summary>The distinct concept groups touched by <paramref name="text"/>.</summary>
    public static IReadOnlyList<string> ExtractConcepts(string? text) => Tokenize(text)
        .Where(ConceptsByWord.ContainsKey)
        .SelectMany(t => ConceptsByWord[t])
        .Distinct()
        .ToList();

    // ---------------------------------------------------------------------------------------

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var tokens = new List<string>();
        foreach (Match match in TokenPattern.Matches(text.ToLowerInvariant()))
        {
            var word = match.Value;
            if (StopWords.Contains(word)) continue;
            // Keep single characters only when they are a real language/tool name ("c", "r").
            if (word.Length == 1 && word is not ("c" or "r")) continue;
            tokens.Add(Stem(word));
        }
        return tokens;
    }

    /// <summary>
    /// Deliberately tiny stemmer: collapses plurals and common verb endings so that
    /// "projects"/"project" and "building"/"build" share a feature. Both the input text and the
    /// concept word lists go through the same function, so lookups always agree.
    /// </summary>
    internal static string Stem(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)) return word[..^3] + "y";
        if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal)) return Undouble(word[..^3]);
        if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal)) return Undouble(word[..^2]);
        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)
            && !word.EndsWith("us", StringComparison.Ordinal) && !word.EndsWith("is", StringComparison.Ordinal))
            return word[..^1];
        return word;
    }

    private static string Undouble(string stem) =>
        stem.Length > 2 && stem[^1] == stem[^2] && !"lsz".Contains(stem[^1]) ? stem[..^1] : stem;

    private static void AddFeature(float[] vector, string feature, float weight)
    {
        // FNV-1a is stable across runs/platforms (string.GetHashCode is randomised per process).
        uint hash = 2166136261;
        foreach (var ch in feature) { hash ^= ch; hash *= 16777619; }
        // Murmur3 finalizer: spreads FNV's weak low bits so index and sign are independent.
        hash ^= hash >> 16; hash *= 0x85ebca6b;
        hash ^= hash >> 13; hash *= 0xc2b2ae35;
        hash ^= hash >> 16;

        var index = (int)((hash >> 1) % Dimensions);
        vector[index] += (hash & 1) == 0 ? weight : -weight;
    }

    private static void Normalize(float[] vector)
    {
        double sumSquares = 0;
        foreach (var value in vector) sumSquares += value * value;
        if (sumSquares <= 0) return;
        var norm = Math.Sqrt(sumSquares);
        for (var i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
    }

    private static Dictionary<string, string[]> BuildConceptIndex(Dictionary<string, string> groups)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (concept, words) in groups)
        {
            foreach (var raw in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var stem = Stem(raw.ToLowerInvariant());
                if (!index.TryGetValue(stem, out var list)) index[stem] = list = [];
                if (!list.Contains(concept)) list.Add(concept);
            }
        }
        return index.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
    }
}
