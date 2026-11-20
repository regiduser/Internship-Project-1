using StudentApp.Services;

namespace StudentApp.Tests;

public class EmbeddingServiceTests
{
    private static double Similarity(string a, string b) =>
        EmbeddingService.CosineSimilarity(EmbeddingService.Embed(a), EmbeddingService.Embed(b));

    [Fact]
    public void Embed_is_deterministic()
    {
        Assert.Equal(EmbeddingService.Embed("software and coding"), EmbeddingService.Embed("software and coding"));
    }

    [Fact]
    public void Embed_has_fixed_size_and_unit_length()
    {
        var vector = EmbeddingService.Embed("I enjoy machine learning and football");
        Assert.Equal(EmbeddingService.Dimensions, vector.Length);
        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        Assert.Equal(1.0, norm, 5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("the and of")]
    [InlineData("I am interested in the")]
    public void Empty_or_stop_word_only_text_gives_zero_vector(string text)
    {
        Assert.All(EmbeddingService.Embed(text), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Related_words_score_higher_than_unrelated_words()
    {
        var related = Similarity("programming", "software development and coding");
        var unrelated = Similarity("programming", "football and fitness training");

        Assert.True(related > 0.2, $"expected related score > 0.2 but was {related:F3}");
        Assert.True(unrelated < 0.1, $"expected unrelated score < 0.1 but was {unrelated:F3}");
        Assert.True(related > unrelated);
    }

    [Fact]
    public void Identical_text_scores_one()
    {
        Assert.Equal(1.0, Similarity("cloud infrastructure", "cloud infrastructure"), 5);
    }

    [Fact]
    public void Network_belongs_to_networking_not_to_ai()
    {
        // Regression: in the original synonym table "network" was silently re-assigned to the AI group
        // because "neural network" appeared later in the list.
        var concepts = EmbeddingService.ExtractConcepts("network defense");
        Assert.Contains("networking", concepts);
        Assert.Contains("security", concepts);
        Assert.DoesNotContain("ai", concepts);
    }

    [Fact]
    public void A_word_can_belong_to_several_concepts()
    {
        var concepts = EmbeddingService.ExtractConcepts("communication");
        Assert.Contains("networking", concepts);
        Assert.Contains("leadership", concepts);
    }

    [Fact]
    public void Generic_profile_words_do_not_create_matches()
    {
        // Regression: "skills" used to map to the leadership group, so every profile that said
        // "skills" matched every query that said "teamwork".
        Assert.Empty(EmbeddingService.ExtractConcepts("my skills and interests"));
        Assert.True(Similarity("music and the guitar", "football and the fitness") < 0.05);
    }

    [Theory]
    [InlineData("projects", "project")]
    [InlineData("building", "build")]
    [InlineData("running", "run")]
    [InlineData("studies", "study")]
    [InlineData("class", "class")]
    [InlineData("business", "business")]
    [InlineData("tested", "test")]
    public void Stem_collapses_common_endings(string word, string expected)
    {
        Assert.Equal(expected, EmbeddingService.Stem(word));
    }

    [Fact]
    public void Plural_and_singular_forms_match()
    {
        Assert.True(Similarity("website", "websites") > 0.9);
    }

    [Fact]
    public void Single_letter_language_names_are_kept()
    {
        var terms = EmbeddingService.ExtractTerms("I know C and R");
        Assert.Contains("c", terms);
        Assert.Contains("r", terms);
    }

    [Fact]
    public void Extracted_terms_are_distinct_and_stop_words_removed()
    {
        var terms = EmbeddingService.ExtractTerms("The music, the MUSIC and the guitar");
        Assert.Equal(["music", "guitar"], terms);
    }

    [Fact]
    public void CosineSimilarity_handles_edge_cases()
    {
        float[] x = [1, 0, 0], y = [0, 1, 0], zero = [0, 0, 0];
        Assert.Equal(1.0, EmbeddingService.CosineSimilarity(x, x), 6);
        Assert.Equal(0.0, EmbeddingService.CosineSimilarity(x, y), 6);
        Assert.Equal(0.0, EmbeddingService.CosineSimilarity(x, zero), 6);
        Assert.Equal(0.0, EmbeddingService.CosineSimilarity(x, new float[] { 1, 0 }), 6);
        Assert.Equal(0.0, EmbeddingService.CosineSimilarity(ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty), 6);
    }

    [Fact]
    public void Repeating_a_word_helps_but_cannot_dominate()
    {
        var once = Similarity("python", "python and guitar music");
        var stuffed = Similarity("python", "python python python python python python and guitar music");
        Assert.True(stuffed > once);   // repetition still counts a little...
        Assert.True(stuffed < 0.99);   // ...but a stuffed profile is not treated as a perfect match.
    }
}
